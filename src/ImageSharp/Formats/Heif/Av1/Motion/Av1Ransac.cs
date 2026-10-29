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
/// A single model fitted to all of the field lets those parts pull the model away from the motion of the frame as a whole.
/// Thus this search fits a model to a few points at a time and counts how many of the remaining points agree with it.
/// It keeps the models that the most points agree with.
/// </para>
/// </remarks>
internal static class Av1Ransac
{
    /// <summary>
    /// The number of models fitted from random points before any model is refined.
    /// </summary>
    private const int TrialCount = 20;

    /// <summary>
    /// The maximum number of times that the search refits one kept model to the points that agree with it.
    /// </summary>
    private const int RefineCount = 5;

    /// <summary>
    /// The number of correspondences that the search needs for each point of the smallest fit. With fewer correspondences, the search fits no model.
    /// </summary>
    private const int PointMultiplier = 5;

    /// <summary>
    /// The distance, in samples, within which a point agrees with a model.
    /// </summary>
    private const double InlierThreshold = 1.25;

    /// <summary>
    /// The share of the points that must agree with a model before it is kept.
    /// </summary>
    private const double MinimumInlierProbability = 0.1;

    /// <summary>
    /// The magnitude below which a pivot is treated as zero.
    /// </summary>
    private const double TinyNearZero = 1.0E-16;

    /// <summary>
    /// Defines how one family of warp models is fitted to a set of points.
    /// </summary>
    public interface IAv1RansacModel
    {
        /// <summary>
        /// Gets the fewest points from which this family can be fitted.
        /// </summary>
        /// <remarks>
        /// The search draws only the fewest points. Every extra point is another chance to draw one that does not follow the motion of the frame.
        /// </remarks>
        public static abstract int MinimumPoints { get; }

        /// <summary>
        /// Fits one model of this family to the selected points.
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

        // One allocation holds the indices of every kept model and of the model under test.
        // Thus a swap of two models is a swap of two offsets, not a copy of their indices.
        using IMemoryOwner<int> indexOwner = allocator.Allocate<int>(pointCount * (models.Length + 1));
        Span<int> indexStorage = indexOwner.Memory.Span;
        Span<Trial> trials = stackalloc Trial[models.Length + 1];
        for (int i = 0; i <= models.Length; i++)
        {
            trials[i] = new Trial(i * pointCount);
        }

        // The point count seeds the generator, so one frame pair always draws the same points and the encoder stays deterministic.
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

            // The parameters are not kept here.
            // The loop below refits each model to all of its own inliers. That gives a better model than the few drawn points.
            // The exchange of the two entries moves the offset of the agreeing points with the counts.
            // Thus the kept model takes the indices that the last score wrote, and the next trial writes over the indices of the replaced model.
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
                    // After a failed refit, no better model is available. Thus this output keeps the identity model.
                    fitted = false;
                    break;
                }

                Score(trialParameters, points, indexStorage.Slice(trials[currentIndex].Offset, pointCount), ref trials[currentIndex]);

                // More inliers means that another refit is worth the cost. The same number, or fewer, means that the model is stable.
                // In that case, the parameters of the latest refit stay in use.
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
    /// <param name="parameters">The six model parameters.</param>
    /// <param name="points">Every correspondence.</param>
    /// <param name="inliers">Receives the index of each agreeing correspondence.</param>
    /// <param name="trial">Receives the count and the total squared miss of the agreeing correspondences.</param>
    /// <remarks>
    /// A rotation with a zoom uses the same score as a full affine model.
    /// Its parameters have the affine layout, with the last two entries derived, not fitted.
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
    /// <param name="matrix">The normal matrix, row major, updated in place.</param>
    /// <param name="vector">The right-hand side, updated in place.</param>
    /// <param name="row">The coefficients of the equation.</param>
    /// <param name="value">The target value of the equation.</param>
    /// <param name="size">The number of unknowns.</param>
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
    /// <param name="size">The number of unknowns.</param>
    /// <param name="matrix">The system matrix, row major. The method overwrites it.</param>
    /// <param name="vector">The right-hand side. The method overwrites it.</param>
    /// <param name="result">Receives the solution.</param>
    /// <returns>Whether the system had a solution.</returns>
    private static bool Solve(int size, Span<double> matrix, Span<double> vector, Span<double> result)
    {
        for (int k = 0; k < size - 1; k++)
        {
            // The largest remaining magnitude moves to the diagonal. This keeps the elimination stable.
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
    /// <param name="pointCount">The number of points to draw from.</param>
    /// <param name="selected">Receives the drawn indices.</param>
    /// <param name="seed">The generator state, advanced in place.</param>
    /// <remarks>
    /// The method draws again when a value repeats an earlier one. This is cheap while the points far outnumber the draws.
    /// That is true here, because a model is fitted from two or three points.
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
    /// <param name="seed">The generator state, advanced in place.</param>
    /// <param name="range">The exclusive upper limit of the result.</param>
    /// <returns>A value from zero up to <paramref name="range"/>.</returns>
    /// <remarks>
    /// The scaling reads the top bits of the output, not a remainder.
    /// A remainder reads the bottom bits, which are the weaker half of the output of this generator.
    /// </remarks>
    private static uint Next(ref uint seed, uint range)
    {
        seed = (uint)((seed * 1103515245UL) + 12345);
        return (uint)(((ulong)seed * range) >> 32);
    }

    /// <summary>
    /// Ranks one trial against another: more agreeing points first, then a smaller total miss.
    /// </summary>
    /// <param name="left">The first trial.</param>
    /// <param name="right">The second trial.</param>
    /// <returns>Whether <paramref name="left"/> ranks before <paramref name="right"/>.</returns>
    private static bool IsBetter(Trial left, Trial right)
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
    /// <param name="trials">The kept trials, sorted in place.</param>
    /// <remarks>
    /// The list holds one entry per wanted model, and the caller wants only a few models.
    /// Thus an insertion sort orders it with no allocation and no comparer.
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
    /// Fits a rotation and a zoom, which is an affine model whose two axes keep their right angle and their common scale.
    /// </summary>
    public readonly struct RotationZoomModel : IAv1RansacModel
    {
        /// <inheritdoc/>
        public static int MinimumPoints => 2;

        /// <inheritdoc/>
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

                // The horizontal output is the first equation, and the vertical output is the second.
                // Both use the same two rotation and zoom parameters. This is the difference from a full affine model.
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

            // The remaining two parameters are not free. A rotation with a zoom has the same scale on both axes and keeps them at a right angle.
            parameters[4] = -parameters[3];
            parameters[5] = parameters[2];
            return true;
        }
    }

    /// <summary>
    /// Holds what one fitted model achieved, and where its agreeing points are stored.
    /// </summary>
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
        /// Gets or sets the number of correspondences that agree with the model.
        /// </summary>
        public int InlierCount { get; set; }

        /// <summary>
        /// Gets or sets the total squared distance by which the agreeing points miss.
        /// </summary>
        public double SumSquaredError { get; set; }
    }
}
