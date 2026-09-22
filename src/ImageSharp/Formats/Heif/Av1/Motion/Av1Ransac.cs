// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Fits a warp model to a set of point correspondences, ignoring the ones that do not agree.
/// </summary>
/// <remarks>
/// <para>
/// A dense flow field describes every part of a frame, including the parts that move on their own.
/// Fitting one model to all of it would let those parts pull the model away from the motion that the
/// frame as a whole has. This search therefore fits a model to a few points at a time, counts how
/// many of the remaining points agree with it, and keeps the models that the most points agree with.
/// </para>
/// <para>Reference: ransac(), ransac_internal(), find_rotzoom(), find_affine() and score_affine().</para>
/// </remarks>
internal static class Av1Ransac
{
    /// <summary>
    /// The models fitted from random points before any is refined.
    /// </summary>
    /// <remarks>Reference: NUM_TRIALS.</remarks>
    private const int TrialCount = 20;

    /// <summary>
    /// The times one kept model is refitted to the points that agree with it.
    /// </summary>
    /// <remarks>Reference: NUM_REFINES.</remarks>
    private const int RefineCount = 5;

    /// <summary>
    /// The points a model needs for every point it fits, before the result is trusted.
    /// </summary>
    /// <remarks>Reference: MINPTS_MULTIPLIER.</remarks>
    private const int PointMultiplier = 5;

    /// <summary>
    /// The distance, in samples, within which a point is said to agree with a model.
    /// </summary>
    /// <remarks>Reference: INLIER_THRESHOLD.</remarks>
    private const double InlierThreshold = 1.25;

    /// <summary>
    /// The share of the points that must agree with a model before it is kept.
    /// </summary>
    /// <remarks>Reference: MIN_INLIER_PROB.</remarks>
    private const double MinimumInlierProbability = 0.1;

    /// <summary>
    /// The magnitude below which a pivot is treated as zero.
    /// </summary>
    /// <remarks>Reference: TINY_NEAR_ZERO.</remarks>
    private const double TinyNearZero = 1.0E-16;

    /// <summary>
    /// Defines how one family of warp models is fitted to a set of points.
    /// </summary>
    /// <remarks>Reference: RansacModelInfo.</remarks>
    public interface IAv1RansacModel
    {
        /// <summary>
        /// Gets the fewest points from which this family can be fitted.
        /// </summary>
        /// <remarks>
        /// The fewest is used, not a comfortable number, because every extra point is another chance
        /// of drawing one that does not belong to the motion of the frame.
        /// </remarks>
        public static abstract int MinimumPoints { get; }

        /// <summary>
        /// Fits one model of this family to the named points.
        /// </summary>
        /// <param name="points">Every correspondence.</param>
        /// <param name="indices">The correspondences to fit.</param>
        /// <param name="indexCount">The correspondences to read from <paramref name="indices"/>.</param>
        /// <param name="parameters">The six model parameters.</param>
        /// <returns>Whether the fit succeeded.</returns>
        public static abstract bool FindTransformation(
            ReadOnlySpan<Av1Correspondence> points, ReadOnlySpan<int> indices, int indexCount, Span<double> parameters);
    }

    /// <summary>
    /// Fits the models of one family that the most correspondences agree with.
    /// </summary>
    /// <typeparam name="TModel">The family of models to fit.</typeparam>
    /// <param name="allocator">The allocator of the index storage.</param>
    /// <param name="points">The correspondences to fit.</param>
    /// <param name="models">The models to fill, best first.</param>
    /// <returns>Whether any model was fitted.</returns>
    /// <remarks>Reference: ransac_internal().</remarks>
    public static bool Run<TModel>(MemoryAllocator allocator, ReadOnlySpan<Av1Correspondence> points, ReadOnlySpan<Av1MotionModel> models)
        where TModel : struct, IAv1RansacModel
    {
        int pointCount = points.Length;
        int minimumPoints = TModel.MinimumPoints;
        for (int i = 0; i < models.Length; i++)
        {
            models[i].Reset();
        }

        if (pointCount == 0 || pointCount < minimumPoints * PointMultiplier)
        {
            return false;
        }

        int minimumInliers = Math.Max((int)(MinimumInlierProbability * pointCount), minimumPoints);

        // One allocation holds the indices of every kept model plus the model under test, so a swap
        // of two models is a swap of two offsets rather than a copy of their indices.
        using IMemoryOwner<int> indexOwner = allocator.Allocate<int>(pointCount * (models.Length + 1));
        Span<int> indexStorage = indexOwner.Memory.Span;
        Span<Trial> trials = stackalloc Trial[models.Length + 1];
        for (int i = 0; i <= models.Length; i++)
        {
            trials[i] = new Trial(i * pointCount);
        }

        // The generator is seeded from the number of points, so one frame pair always draws the same
        // points and the encoder stays deterministic.
        uint seed = (uint)pointCount;
        Span<int> selected = stackalloc int[minimumPoints];
        Span<double> trialParameters = stackalloc double[Av1MotionModel.ParameterCount];
        int currentIndex = models.Length;
        int worstKeptIndex = 0;

        for (int trial = 0; trial < TrialCount; trial++)
        {
            Pick(pointCount, selected, ref seed);
            if (!TModel.FindTransformation(points, selected, minimumPoints, trialParameters))
            {
                continue;
            }

            Score(trialParameters, points, indexStorage.Slice(trials[currentIndex].Offset, pointCount), ref trials[currentIndex]);
            if (trials[currentIndex].InlierCount < minimumInliers)
            {
                continue;
            }

            if (!IsBetter(trials[currentIndex], trials[worstKeptIndex]))
            {
                continue;
            }

            // The parameters are not kept here. A model is refitted to all of its own inliers below,
            // which is a better model than the one the few drawn points gave. Exchanging the two
            // entries carries the offset of the agreeing points with the counts, so the kept model
            // takes the indices that were just written and the next trial writes over the ones the
            // model it replaced had.
            (trials[currentIndex], trials[worstKeptIndex]) = (trials[worstKeptIndex], trials[currentIndex]);

            worstKeptIndex = 0;
            for (int i = 1; i < models.Length; i++)
            {
                if (IsBetter(trials[worstKeptIndex], trials[i]))
                {
                    worstKeptIndex = i;
                }
            }
        }

        Sort(trials[..models.Length]);

        for (int i = 0; i < models.Length; i++)
        {
            if (trials[i].InlierCount <= 0)
            {
                continue;
            }

            bool fitted = true;
            for (int refinement = 0; refinement < RefineCount; refinement++)
            {
                ReadOnlySpan<int> inliers = indexStorage.Slice(trials[i].Offset, pointCount);
                if (!TModel.FindTransformation(points, inliers, trials[i].InlierCount, trialParameters))
                {
                    // A refit that fails leaves no better model to fall back on, so this output keeps
                    // the model that does nothing.
                    fitted = false;
                    break;
                }

                Score(trialParameters, points, indexStorage.Slice(trials[currentIndex].Offset, pointCount), ref trials[currentIndex]);

                // More inliers means the refit is worth repeating. The same number, or fewer, means
                // the model has settled, and the refit is still the one whose parameters are held.
                if (trials[currentIndex].InlierCount <= trials[i].InlierCount)
                {
                    break;
                }

                (trials[currentIndex], trials[i]) = (trials[i], trials[currentIndex]);
            }

            if (!fitted)
            {
                continue;
            }

            models[i].Set(trialParameters, points, indexStorage.Slice(trials[i].Offset, trials[i].InlierCount));
        }

        return true;
    }

    /// <summary>
    /// Counts the correspondences that agree with one model and adds up how far they miss by.
    /// </summary>
    /// <remarks>
    /// A rotation with a zoom is scored the same way a full affine model is, because its parameters
    /// are laid out as an affine model whose last two entries were derived rather than fitted.
    /// Reference: score_affine().
    /// </remarks>
    private static void Score(
        ReadOnlySpan<double> parameters, ReadOnlySpan<Av1Correspondence> points, Span<int> inliers, ref Trial trial)
    {
        const double Threshold = InlierThreshold * InlierThreshold;
        int count = 0;
        double total = 0;
        for (int i = 0; i < points.Length; i++)
        {
            Av1Correspondence point = points[i];
            double projectedX = (parameters[2] * point.X) + (parameters[3] * point.Y) + parameters[0];
            double projectedY = (parameters[4] * point.X) + (parameters[5] * point.Y) + parameters[1];
            double differenceX = projectedX - point.ReferenceX;
            double differenceY = projectedY - point.ReferenceY;
            double squaredError = (differenceX * differenceX) + (differenceY * differenceY);
            if (squaredError < Threshold)
            {
                inliers[count++] = i;
                total += squaredError;
            }
        }

        trial.InlierCount = count;
        trial.SumSquaredError = total;
    }

    /// <summary>
    /// Adds one equation to the accumulated normal equations of a least-squares problem.
    /// </summary>
    /// <remarks>Reference: least_squares_accumulate().</remarks>
    private static void Accumulate(Span<double> matrix, Span<double> vector, ReadOnlySpan<double> row, double value, int size)
    {
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                matrix[(i * size) + j] += row[i] * row[j];
            }

            vector[i] += row[i] * value;
        }
    }

    /// <summary>
    /// Solves a small dense system by elimination with partial pivoting.
    /// </summary>
    /// <returns>Whether the system had a solution.</returns>
    /// <remarks>Reference: linsolve().</remarks>
    private static bool Solve(int size, Span<double> matrix, Span<double> vector, Span<double> result)
    {
        for (int k = 0; k < size - 1; k++)
        {
            // Bringing the largest remaining magnitude to the diagonal keeps the elimination stable.
            for (int i = size - 1; i > k; i--)
            {
                if (Math.Abs(matrix[((i - 1) * size) + k]) < Math.Abs(matrix[(i * size) + k]))
                {
                    for (int j = 0; j < size; j++)
                    {
                        (matrix[(i * size) + j], matrix[((i - 1) * size) + j]) =
                            (matrix[((i - 1) * size) + j], matrix[(i * size) + j]);
                    }

                    (vector[i], vector[i - 1]) = (vector[i - 1], vector[i]);
                }
            }

            for (int i = k; i < size - 1; i++)
            {
                if (Math.Abs(matrix[(k * size) + k]) < TinyNearZero)
                {
                    return false;
                }

                double factor = matrix[((i + 1) * size) + k] / matrix[(k * size) + k];
                for (int j = 0; j < size; j++)
                {
                    matrix[((i + 1) * size) + j] -= factor * matrix[(k * size) + j];
                }

                vector[i + 1] -= factor * vector[k];
            }
        }

        for (int i = size - 1; i >= 0; i--)
        {
            if (Math.Abs(matrix[(i * size) + i]) < TinyNearZero)
            {
                return false;
            }

            double known = 0;
            for (int j = i + 1; j < size; j++)
            {
                known += matrix[(i * size) + j] * result[j];
            }

            result[i] = (vector[i] - known) / matrix[(i * size) + i];
        }

        return true;
    }

    /// <summary>
    /// Draws distinct point indices, every set and every order equally likely.
    /// </summary>
    /// <remarks>
    /// A drawn value that repeats an earlier one is drawn again. That is cheap while the points far
    /// outnumber the draws, which they do here, because a model is fitted from two or three points.
    /// Reference: lcg_pick().
    /// </remarks>
    private static void Pick(int pointCount, Span<int> selected, ref uint seed)
    {
        for (int i = 0; i < selected.Length; i++)
        {
            bool repeated;
            do
            {
                int value = (int)Next(ref seed, (uint)pointCount);
                selected[i] = value;
                repeated = false;
                for (int j = 0; j < i; j++)
                {
                    if (selected[j] == value)
                    {
                        repeated = true;
                        break;
                    }
                }
            }
            while (repeated);
        }
    }

    /// <summary>
    /// Advances the generator and scales its output into the wanted range.
    /// </summary>
    /// <remarks>
    /// The scaling reads the top bits of the output rather than taking a remainder of the bottom
    /// ones, which are the weaker half of what this generator produces.
    /// Reference: lcg_next() and lcg_randint().
    /// </remarks>
    private static uint Next(ref uint seed, uint range)
    {
        seed = (uint)((seed * 1103515245UL) + 12345);
        return (uint)(((ulong)seed * range) >> 32);
    }

    /// <summary>
    /// Ranks one trial against another: more agreeing points first, then a smaller total miss.
    /// </summary>
    /// <remarks>Reference: is_better_motion() and compare_motions().</remarks>
    private static bool IsBetter(in Trial left, in Trial right)
    {
        if (left.InlierCount != right.InlierCount)
        {
            return left.InlierCount > right.InlierCount;
        }

        return left.SumSquaredError < right.SumSquaredError;
    }

    /// <summary>
    /// Orders the kept trials, best first.
    /// </summary>
    /// <remarks>
    /// The list holds one entry per wanted model, which the caller keeps to a handful, so an
    /// insertion sort orders it with no allocation and no comparer.
    /// Reference: the qsort of ransac_internal().
    /// </remarks>
    private static void Sort(Span<Trial> trials)
    {
        for (int i = 1; i < trials.Length; i++)
        {
            Trial current = trials[i];
            int j = i - 1;
            while (j >= 0 && IsBetter(current, trials[j]))
            {
                trials[j + 1] = trials[j];
                j--;
            }

            trials[j + 1] = current;
        }
    }

    /// <summary>
    /// Fits a rotation and a zoom, which is an affine model whose two axes keep their right angle
    /// and their common scale.
    /// </summary>
    public readonly struct RotationZoomModel : IAv1RansacModel
    {
        /// <inheritdoc/>
        public static int MinimumPoints => 2;

        /// <inheritdoc/>
        /// <remarks>Reference: find_rotzoom().</remarks>
        public static bool FindTransformation(
            ReadOnlySpan<Av1Correspondence> points, ReadOnlySpan<int> indices, int indexCount, Span<double> parameters)
        {
            const int Size = 4;
            Span<double> matrix = stackalloc double[Size * Size];
            Span<double> vector = stackalloc double[Size];
            Span<double> row = stackalloc double[Size];
            matrix.Clear();
            vector.Clear();

            for (int i = 0; i < indexCount; i++)
            {
                Av1Correspondence point = points[indices[i]];

                // The horizontal output is the first equation and the vertical output the second.
                // Both share the same two rotation and zoom parameters, which is what separates this
                // family from a full affine model.
                row[0] = 1;
                row[1] = 0;
                row[2] = point.X;
                row[3] = point.Y;
                Accumulate(matrix, vector, row, point.ReferenceX, Size);

                row[0] = 0;
                row[1] = 1;
                row[2] = point.Y;
                row[3] = -point.X;
                Accumulate(matrix, vector, row, point.ReferenceY, Size);
            }

            if (!Solve(Size, matrix, vector, parameters))
            {
                return false;
            }

            // The remaining two parameters are not free: a rotation with a zoom has the same scale on
            // both axes and keeps them at a right angle.
            parameters[4] = -parameters[3];
            parameters[5] = parameters[2];
            return true;
        }
    }

    /// <summary>
    /// Fits a full affine model, whose two axes scale and shear on their own.
    /// </summary>
    public readonly struct AffineModel : IAv1RansacModel
    {
        /// <inheritdoc/>
        public static int MinimumPoints => 3;

        /// <inheritdoc/>
        /// <remarks>
        /// The six parameters split into the three that make the horizontal output and the three that
        /// make the vertical one, and neither group appears in the other equation. Solving the two
        /// groups apart is therefore exact and costs less than one six-parameter solve.
        /// Reference: find_affine().
        /// </remarks>
        public static bool FindTransformation(
            ReadOnlySpan<Av1Correspondence> points, ReadOnlySpan<int> indices, int indexCount, Span<double> parameters)
        {
            const int Size = 3;
            Span<double> horizontalMatrix = stackalloc double[Size * Size];
            Span<double> verticalMatrix = stackalloc double[Size * Size];
            Span<double> horizontalVector = stackalloc double[Size];
            Span<double> verticalVector = stackalloc double[Size];
            Span<double> row = stackalloc double[Size];
            Span<double> horizontalResult = stackalloc double[Size];
            Span<double> verticalResult = stackalloc double[Size];
            horizontalMatrix.Clear();
            verticalMatrix.Clear();
            horizontalVector.Clear();
            verticalVector.Clear();

            for (int i = 0; i < indexCount; i++)
            {
                Av1Correspondence point = points[indices[i]];
                row[0] = 1;
                row[1] = point.X;
                row[2] = point.Y;
                Accumulate(horizontalMatrix, horizontalVector, row, point.ReferenceX, Size);
                Accumulate(verticalMatrix, verticalVector, row, point.ReferenceY, Size);
            }

            if (!Solve(Size, horizontalMatrix, horizontalVector, horizontalResult) ||
                !Solve(Size, verticalMatrix, verticalVector, verticalResult))
            {
                return false;
            }

            parameters[0] = horizontalResult[0];
            parameters[1] = verticalResult[0];
            parameters[2] = horizontalResult[1];
            parameters[3] = horizontalResult[2];
            parameters[4] = verticalResult[1];
            parameters[5] = verticalResult[2];
            return true;
        }
    }

    /// <summary>
    /// Holds what one fitted model achieved, and where its agreeing points are stored.
    /// </summary>
    /// <remarks>Reference: RANSAC_MOTION.</remarks>
    private struct Trial
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Trial"/> struct.
        /// </summary>
        /// <param name="offset">The index at which this trial's agreeing points begin.</param>
        public Trial(int offset) => this.Offset = offset;

        /// <summary>
        /// Gets the index at which this trial's agreeing points begin.
        /// </summary>
        public int Offset { get; }

        /// <summary>
        /// Gets or sets the correspondences that agree with the model.
        /// </summary>
        public int InlierCount { get; set; }

        /// <summary>
        /// Gets or sets the total squared distance by which the agreeing points miss.
        /// </summary>
        public double SumSquaredError { get; set; }
    }
}
