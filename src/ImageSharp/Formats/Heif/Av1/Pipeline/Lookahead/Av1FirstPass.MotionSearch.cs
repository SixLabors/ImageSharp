// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <content>
/// Searches full-sample motion with the first-pass configuration. The search uses the first-pass site geometry and every row of the block in each
/// absolute difference. A mesh search follows when the result is poor.
/// </content>
/// <remarks>
/// The first pass resets the motion speed features before it searches, so it never samples alternate rows, whatever the frame size and speed.
/// The coding-pass search settings cannot express that for large frames or the faster speeds, so the first pass keeps its own traversal over
/// the shared sample kernels.
/// </remarks>
internal sealed partial class Av1FirstPass<TSample, TOperator>
{
    /// <summary>
    /// Gets the range and interval pairs of the four mesh passes at speeds zero through five. Faster speeds use the last row.
    /// </summary>
    private static ReadOnlySpan<int> MeshPatterns =>
    [
        64, 8, 28, 4, 15, 1, 7, 1,
        64, 8, 28, 4, 15, 1, 7, 1,
        64, 8, 14, 2, 7, 1, 7, 1,
        64, 16, 24, 8, 12, 4, 7, 1,
        64, 16, 24, 8, 12, 4, 7, 1,
        64, 16, 24, 8, 12, 4, 7, 1
    ];

    /// <summary>
    /// Searches one reference for a unit from a start vector. The result replaces the best so far when its squared error, vector cost and
    /// new-vector surcharge together are less than the best error.
    /// </summary>
    /// <param name="frame">The frame being measured.</param>
    /// <param name="reference">The reference plane.</param>
    /// <param name="blockSize">The measured block size.</param>
    /// <param name="unitRow">The unit row.</param>
    /// <param name="unitColumn">The unit column.</param>
    /// <param name="referenceVector">The starting vector, which is also the reference of the vector cost.</param>
    /// <param name="bestVector">The best vector so far, replaced when this search improves on it.</param>
    /// <param name="bestError">The best error so far, replaced when this search improves on it.</param>
    private void SearchFirstPassMotion(
        ref FrameContext frame,
        ReadOnlySpan<TSample> reference,
        Av1BlockSize blockSize,
        int unitRow,
        int unitColumn,
        Av1MotionVector referenceVector,
        ref Point bestVector,
        ref int bestError)
    {
        int unitSize = 4 << frame.UnitLog2;
        int x = unitColumn * unitSize;
        int y = unitRow * unitSize;
        Point start = GetFullMotionVector(referenceVector);
        int stepParameter = this.reduceMotionVectorStepParameter + this.searchRange;

        // Screen content with block copy starts its mesh with a fine interval.
        bool fineSearchInterval = this.isScreenContentType && this.allowIntraBlockCopy;

        // The limits of the unit narrow to the vectors that are codable against the reference vector.
        FullMotionVectorLimits limits = this.motionLimits;
        SetMotionVectorSearchRange(ref limits, referenceVector);

        // The largest sharpness keeps the block and an eight-sample margin inside the visible frame.
        if (this.sharpness == 3)
        {
            int topMargin = y + 8;
            int leftMargin = x + 8;
            int bottomMargin = Math.Max(this.height - blockSize.GetHeight() - topMargin + 16, -topMargin);
            int rightMargin = Math.Max(this.width - blockSize.GetWidth() - leftMargin + 16, -leftMargin);
            limits.RowMinimum = Math.Max(limits.RowMinimum, -topMargin);
            limits.RowMaximum = Math.Min(limits.RowMaximum, bottomMargin);
            limits.ColumnMinimum = Math.Max(limits.ColumnMinimum, -leftMargin);
            limits.ColumnMaximum = Math.Min(limits.ColumnMaximum, rightMargin);
        }

        int meshSpeed = Math.Min((int)this.speed, 5);
        MotionSearch search = new()
        {
            Source = frame.Source,
            SourceStride = frame.SourceStride,
            SourceIndex = frame.SourceOrigin + (y * frame.SourceStride) + x,
            Reference = reference,
            ReferenceStride = frame.ReconstructionStride,
            ReferenceIndex = frame.ReconstructionOrigin + (y * frame.ReconstructionStride) + x,
            Width = blockSize.GetWidth(),
            Height = blockSize.GetHeight(),
            AreaLog2 = blockSize.Get4x4WidthLog2() + blockSize.Get4x4HeightLog2(),
            Limits = limits,
            ReferenceVector = referenceVector,
            FullReferenceVector = start,
            Costs = frame.Costs,
            SadPerBit = this.sadPerBit,
            RateMultiplier = frame.RateMultiplier,
            PrecisionShift = this.bitDepth.GetBitCount() - 8,
            Sites = frame.Sites,
            MeshThreshold = frame.MeshThreshold,
            PruneMesh = this.pruneMeshSearch,
            MeshPattern = MeshPatterns.Slice(meshSpeed * 8, 8),
            FineInterval = fineSearchInterval
        };

        int error = search.Search(start, stepParameter, out Point searchVector);
        if (error < int.MaxValue)
        {
            error = search.GetPredictionSquaredError(searchVector) + NewMotionVectorModePenalty;
        }

        if (error < bestError)
        {
            bestError = error;
            bestVector = searchVector;
        }
    }

    /// <summary>
    /// Rounds an eighth-sample vector to the nearest full sample, with ties away from zero.
    /// </summary>
    /// <param name="vector">The vector in eighth samples.</param>
    /// <returns>The vector in full samples, as column and row.</returns>
    private static Point GetFullMotionVector(Av1MotionVector vector)
        => new(
            (vector.Column + 3 + (vector.Column >= 0 ? 1 : 0)) >> 3,
            (vector.Row + 3 + (vector.Row >= 0 ? 1 : 0)) >> 3);

    /// <summary>
    /// Intersects limits with the full-sample vectors whose difference from a reference vector stays codable. The lower bounds round up and the
    /// upper bounds round down.
    /// </summary>
    /// <param name="limits">The limits to narrow.</param>
    /// <param name="vector">The reference vector in eighth samples.</param>
    private static void SetMotionVectorSearchRange(ref FullMotionVectorLimits limits, Av1MotionVector vector)
    {
        // The codable components lie strictly between -2^14 and 2^14 eighth samples.
        const int lowest = -(1 << 14);
        const int highest = 1 << 14;
        int columnMinimum = Math.Max(((vector.Column + 7) >> 3) - MaximumFullPixelValue, (lowest >> 3) + 1);
        int rowMinimum = Math.Max(((vector.Row + 7) >> 3) - MaximumFullPixelValue, (lowest >> 3) + 1);
        int columnMaximum = Math.Min((vector.Column >> 3) + MaximumFullPixelValue, (highest >> 3) - 1);
        int rowMaximum = Math.Min((vector.Row >> 3) + MaximumFullPixelValue, (highest >> 3) - 1);

        limits.ColumnMinimum = Math.Max(limits.ColumnMinimum, columnMinimum);
        limits.ColumnMaximum = Math.Min(limits.ColumnMaximum, columnMaximum);
        limits.RowMinimum = Math.Max(limits.RowMinimum, rowMinimum);
        limits.RowMaximum = Math.Min(limits.RowMaximum, rowMaximum);

        // An empty intersection collapses to its minimum so that the limits always hold at least one vector.
        limits.ColumnMaximum = Math.Max(limits.ColumnMinimum, limits.ColumnMaximum);
        limits.RowMaximum = Math.Max(limits.RowMinimum, limits.RowMaximum);
    }

    /// <summary>
    /// Borrows the source block, reference plane, limits and costs of one full-sample search.
    /// </summary>
    private ref struct MotionSearch
    {
        /// <summary>The complete source plane.</summary>
        public ReadOnlySpan<TSample> Source;

        /// <summary>The source row stride.</summary>
        public int SourceStride;

        /// <summary>The source index of the block origin.</summary>
        public int SourceIndex;

        /// <summary>The complete bordered reference plane.</summary>
        public ReadOnlySpan<TSample> Reference;

        /// <summary>The reference row stride.</summary>
        public int ReferenceStride;

        /// <summary>The reference index of the undisplaced block origin.</summary>
        public int ReferenceIndex;

        /// <summary>The block width.</summary>
        public int Width;

        /// <summary>The block height.</summary>
        public int Height;

        /// <summary>The base-two logarithm of the block area in 4x4 units.</summary>
        public int AreaLog2;

        /// <summary>The inclusive vector limits.</summary>
        public FullMotionVectorLimits Limits;

        /// <summary>The reference of the variance-domain vector cost, in eighth samples.</summary>
        public Av1MotionVector ReferenceVector;

        /// <summary>The reference of the absolute-difference vector cost, in full samples.</summary>
        public Point FullReferenceVector;

        /// <summary>The vector rate tables.</summary>
        public Av1MotionVectorCosts Costs;

        /// <summary>The rate scale of the absolute-difference domain.</summary>
        public int SadPerBit;

        /// <summary>The rate multiplier of the variance domain.</summary>
        public int RateMultiplier;

        /// <summary>The shift that reduces absolute differences to eight-bit precision.</summary>
        public int PrecisionShift;

        /// <summary>The first-pass site geometry.</summary>
        public Av1MotionSearchSites Sites;

        /// <summary>The 16x16 variance above which a mesh follows the site search.</summary>
        public int MeshThreshold;

        /// <summary>Whether a site winner near its start skips the mesh.</summary>
        public bool PruneMesh;

        /// <summary>The range and interval pairs of the mesh passes.</summary>
        public ReadOnlySpan<int> MeshPattern;

        /// <summary>Whether the first mesh interval is capped at four.</summary>
        public bool FineInterval;

        /// <summary>
        /// Runs the site search. When its result is poor and not pruned, a mesh search around its winner follows.
        /// </summary>
        /// <param name="start">The unclamped start.</param>
        /// <param name="stepParameter">The number of outer stages to skip.</param>
        /// <param name="best">Receives the winning vector.</param>
        /// <returns>The winning variance plus vector cost.</returns>
        public readonly int Search(Point start, int stepParameter, out Point best)
        {
            int variance = this.FullPixelDiamond(start, stepParameter, out best);

            // The threshold is scaled from a 16x16 block to the block area.
            bool runMesh = variance > (this.MeshThreshold >> (10 - this.AreaLog2));

            // A winner close to the unclamped start skips the mesh.
            if (this.PruneMesh && Math.Max(Math.Abs(start.Y - best.Y), Math.Abs(start.X - best.X)) <= 4)
            {
                runMesh = false;
            }

            if (runMesh)
            {
                int meshVariance = this.FullPixelExhaustive(best, out Point meshBest);
                if (meshVariance < variance)
                {
                    variance = meshVariance;
                    best = meshBest;
                }
            }

            return variance;
        }

        /// <summary>
        /// Measures the squared error of a vector plus its variance-domain cost. High bit depths round the error to eight-bit precision.
        /// </summary>
        /// <param name="vector">The full-sample vector.</param>
        /// <returns>The squared error plus the vector cost.</returns>
        public readonly int GetPredictionSquaredError(Point vector)
        {
            this.GetMoments(vector, out _, out long squares);
            return (int)squares + this.GetVectorErrorCost(vector);
        }

        /// <summary>
        /// Repeats site searches of decreasing initial radius from the same clamped start and keeps the lowest variance cost. A search that stays
        /// at its center for several stages lets the next restart skip them.
        /// </summary>
        /// <param name="start">The unclamped start.</param>
        /// <param name="stepParameter">The number of outer stages the first search skips.</param>
        /// <param name="best">Receives the winning vector.</param>
        /// <returns>The winning variance plus vector cost.</returns>
        private readonly int FullPixelDiamond(Point start, int stepParameter, out Point best)
        {
            start = this.Clamp(start);
            int startSad = this.GetSadCost(start);
            this.DiamondSearchSad(start, startSad, stepParameter, out int centerSteps, out best);
            int bestCost = this.GetVarianceCost(best);

            int furtherSteps = this.Sites.StageCount - 1 - stepParameter;
            int step = centerSteps;
            while (step < furtherSteps)
            {
                step++;
                this.DiamondSearchSad(start, startSad, stepParameter + step, out int skippedSteps, out Point candidate);
                int candidateCost = this.GetVarianceCost(candidate);
                if (candidateCost < bestCost)
                {
                    bestCost = candidateCost;
                    best = candidate;
                }

                step += skippedSteps;
            }

            return bestCost;
        }

        /// <summary>
        /// Moves to the best site of each stage from the outermost searched radius inward. Each site costs its absolute differences plus its
        /// vector cost. Stages searched before the first move count as center stays.
        /// </summary>
        /// <param name="start">The clamped start.</param>
        /// <param name="startSad">The absolute-difference cost of the start.</param>
        /// <param name="searchStep">The number of outer stages to skip.</param>
        /// <param name="centerSteps">Receives the number of stages searched before the first move.</param>
        /// <param name="best">Receives the winning vector.</param>
        /// <returns>The winning absolute-difference cost.</returns>
        private readonly int DiamondSearchSad(Point start, int startSad, int searchStep, out int centerSteps, out Point best)
        {
            best = start;
            int bestSad = startSad;
            bool offCenter = false;
            centerSteps = 0;
            for (int step = this.Sites.StageCount - searchStep - 1; step >= 0; step--)
            {
                ReadOnlySpan<Av1MotionSearchSites.Site> sites = this.Sites.GetSites(step);
                int count = this.Sites.GetCandidateCount(step);
                int bestSite = 0;

                // Every stage tests each site against the limits. When all sites of a stage lie inside the limits, every test passes and the
                // result is the same as a search without the test.
                for (int index = 1; index <= count; index++)
                {
                    Av1MotionSearchSites.Site site = sites[index];
                    Point candidate = new(best.X + site.Column, best.Y + site.Row);
                    if (!this.IsInRange(candidate))
                    {
                        continue;
                    }

                    // The vector cost is added only when the difference alone can still win.
                    int sad = this.GetSad(candidate);
                    if (sad < bestSad)
                    {
                        int cost = sad + this.GetVectorSadCost(candidate);
                        if (cost < bestSad)
                        {
                            bestSad = cost;
                            bestSite = index;
                        }
                    }
                }

                if (bestSite != 0)
                {
                    Av1MotionSearchSites.Site site = sites[bestSite];
                    best = new Point(best.X + site.Column, best.Y + site.Row);
                    offCenter = true;
                }

                if (!offCenter)
                {
                    centerSteps++;
                }

                // A center stay skips the stages that repeat its radius. The first-pass radii never repeat.
                if (bestSite == 0 && step > 2)
                {
                    while (this.Sites.GetRadius(step - 1) == this.Sites.GetRadius(step) && step > 2)
                    {
                        centerSteps++;
                        step--;
                    }
                }
            }

            return bestSad;
        }

        /// <summary>
        /// Runs the mesh passes around a vector. The first pass widens its range to cover the magnitude of the vector and keeps its interval
        /// ratio. Later passes narrow until an interval of one.
        /// </summary>
        /// <param name="start">The vector around which the mesh starts.</param>
        /// <param name="best">Receives the winning vector.</param>
        /// <returns>The winning variance plus vector cost.</returns>
        private readonly int FullPixelExhaustive(Point start, out Point best)
        {
            const int minimumRange = 7;
            const int maximumRange = 256;
            best = start;
            int range = this.MeshPattern[0];
            int interval = this.MeshPattern[1];
            if (range < minimumRange || range > maximumRange || interval < 1 || interval > range)
            {
                return int.MaxValue;
            }

            int baselineIntervalDivisor = range / interval;
            range = Math.Max(range, (5 * Math.Max(Math.Abs(best.Y), Math.Abs(best.X))) / 4);
            range = Math.Min(range, maximumRange);
            interval = Math.Max(interval, range / baselineIntervalDivisor);
            if (this.FineInterval)
            {
                interval = Math.Min(interval, 4);
            }

            int bestCost = this.ExhaustiveMeshSearch(best, range, interval, out best);
            if (interval > 1 && range > minimumRange)
            {
                for (int pass = 1; pass < 4; pass++)
                {
                    bestCost = this.ExhaustiveMeshSearch(best, this.MeshPattern[pass * 2], this.MeshPattern[(pass * 2) + 1], out best);
                    if (this.MeshPattern[(pass * 2) + 1] == 1)
                    {
                        break;
                    }
                }
            }

            if (bestCost < int.MaxValue)
            {
                bestCost = this.GetVarianceCost(best);
            }

            return bestCost;
        }

        /// <summary>
        /// Visits a grid of vectors around a clamped center. A unit interval visits whole groups of four columns. A final partial group stops one
        /// column short of the range.
        /// </summary>
        /// <param name="start">The center.</param>
        /// <param name="range">The grid radius.</param>
        /// <param name="step">The row interval, and the column interval above one.</param>
        /// <param name="best">Receives the winning vector.</param>
        /// <returns>The winning absolute-difference cost.</returns>
        private readonly int ExhaustiveMeshSearch(Point start, int range, int step, out Point best)
        {
            start = this.Clamp(start);
            best = start;
            int bestSad = this.GetSadCost(start);
            int columnStep = step > 1 ? step : 4;
            int startRow = Math.Max(-range, this.Limits.RowMinimum - start.Y);
            int startColumn = Math.Max(-range, this.Limits.ColumnMinimum - start.X);
            int endRow = Math.Min(range, this.Limits.RowMaximum - start.Y);
            int endColumn = Math.Min(range, this.Limits.ColumnMaximum - start.X);
            for (int row = startRow; row <= endRow; row += step)
            {
                for (int column = startColumn; column <= endColumn; column += columnStep)
                {
                    int count = step > 1 ? 1 : column + 3 <= endColumn ? 4 : endColumn - column;
                    for (int i = 0; i < count; i++)
                    {
                        Point candidate = new(start.X + column + i, start.Y + row);

                        // The vector cost is added only when the difference alone can still win.
                        int sad = this.GetSad(candidate);
                        if (sad < bestSad)
                        {
                            int cost = sad + this.GetVectorSadCost(candidate);
                            if (cost < bestSad)
                            {
                                bestSad = cost;
                                best = candidate;
                            }
                        }
                    }
                }
            }

            return bestSad;
        }

        /// <summary>
        /// Measures the variance of a vector plus its variance-domain cost.
        /// </summary>
        /// <param name="vector">The full-sample vector.</param>
        /// <returns>The variance plus the vector cost.</returns>
        private readonly int GetVarianceCost(Point vector)
        {
            this.GetMoments(vector, out int sum, out long squares);

            // At high bit depths the sum and the squares round separately, which can make the variance negative. The variance clamps at zero.
            long variance = Math.Max(squares - (((long)sum * sum) / (this.Width * this.Height)), 0);
            return (int)variance + this.GetVectorErrorCost(vector);
        }

        /// <summary>
        /// Measures the signed and squared differences of a vector. At the higher bit depths, each rounds to eight-bit precision.
        /// </summary>
        /// <param name="vector">The full-sample vector.</param>
        /// <param name="sum">Receives the rounded signed sum.</param>
        /// <param name="squares">Receives the rounded squared sum.</param>
        private readonly void GetMoments(Point vector, out int sum, out long squares)
        {
            TOperator.GetMoments(
                this.Source[this.SourceIndex..],
                this.SourceStride,
                this.Reference[(this.ReferenceIndex + (vector.Y * this.ReferenceStride) + vector.X)..],
                this.ReferenceStride,
                this.Width,
                this.Height,
                out sum,
                out squares);

            if (this.PrecisionShift != 0)
            {
                sum = (sum + (1 << (this.PrecisionShift - 1))) >> this.PrecisionShift;
                int squaredShift = 2 * this.PrecisionShift;
                squares = (squares + (1L << (squaredShift - 1))) >> squaredShift;
            }
        }

        /// <summary>
        /// Measures the absolute differences of a vector plus its absolute-difference cost.
        /// </summary>
        /// <param name="vector">The full-sample vector.</param>
        /// <returns>The absolute differences plus the vector cost.</returns>
        private readonly int GetSadCost(Point vector) => this.GetSad(vector) + this.GetVectorSadCost(vector);

        /// <summary>
        /// Measures the absolute differences of a vector at eight-bit precision.
        /// </summary>
        /// <param name="vector">The full-sample vector.</param>
        /// <returns>The absolute differences.</returns>
        private readonly int GetSad(Point vector)
            => TOperator.SumAbsoluteDifferences(
                this.Source[this.SourceIndex..],
                this.SourceStride,
                this.Reference[(this.ReferenceIndex + (vector.Y * this.ReferenceStride) + vector.X)..],
                this.ReferenceStride,
                this.Width,
                this.Height) >> this.PrecisionShift;

        /// <summary>
        /// Gets the absolute-difference cost of a vector relative to the full-sample reference vector.
        /// </summary>
        /// <param name="vector">The full-sample vector.</param>
        /// <returns>The vector cost.</returns>
        private readonly int GetVectorSadCost(Point vector)
        {
            Av1MotionVector difference = new((vector.Y - this.FullReferenceVector.Y) * 8, (vector.X - this.FullReferenceVector.X) * 8);
            int rate = this.Costs.GetCost(difference, default);
            return Av1RateDistortion.GetMotionSearchSadCost(this.SadPerBit, rate, 0);
        }

        /// <summary>
        /// Gets the variance-domain cost of a vector relative to the reference vector.
        /// </summary>
        /// <param name="vector">The full-sample vector.</param>
        /// <returns>The vector cost.</returns>
        private readonly int GetVectorErrorCost(Point vector)
        {
            int rate = this.Costs.GetCost(new Av1MotionVector(vector.Y * 8, vector.X * 8), this.ReferenceVector);
            return Av1RateDistortion.GetMotionSearchCost(this.RateMultiplier, rate, 0);
        }

        /// <summary>
        /// Clamps a vector to the limits.
        /// </summary>
        /// <param name="vector">The full-sample vector.</param>
        /// <returns>The clamped vector.</returns>
        private readonly Point Clamp(Point vector)
            => new(
                Math.Clamp(vector.X, this.Limits.ColumnMinimum, this.Limits.ColumnMaximum),
                Math.Clamp(vector.Y, this.Limits.RowMinimum, this.Limits.RowMaximum));

        /// <summary>
        /// Tests whether a vector lies inside the limits.
        /// </summary>
        /// <param name="vector">The full-sample vector.</param>
        /// <returns>Whether the vector is inside the limits.</returns>
        private readonly bool IsInRange(Point vector)
            => vector.X >= this.Limits.ColumnMinimum && vector.X <= this.Limits.ColumnMaximum &&
                vector.Y >= this.Limits.RowMinimum && vector.Y <= this.Limits.RowMaximum;
    }
}
