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
/// The coding-pass search settings cannot express that for large frames or the faster speeds, so the first pass runs the shared diamond and
/// mesh searches directly with its own error measure and mesh decision.
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

        MotionSearchCost cost = new()
        {
            Source = frame.Source,
            SourceStride = frame.SourceStride,
            SourceIndex = frame.SourceOrigin + (y * frame.SourceStride) + x,
            Reference = reference,
            ReferenceStride = frame.ReconstructionStride,
            ReferenceIndex = frame.ReconstructionOrigin + (y * frame.ReconstructionStride) + x,
            Width = blockSize.GetWidth(),
            Height = blockSize.GetHeight(),
            ReferenceVector = referenceVector,
            FullReferenceVector = start,
            Costs = frame.Costs,
            SadPerBit = this.sadPerBit,
            RateMultiplier = frame.RateMultiplier,
            PrecisionShift = this.bitDepth.GetBitCount() - 8
        };

        // The shared searches take exclusive upper edges. The first pass does not use the preceding winner that they report.
        Rectangle bounds = Rectangle.FromLTRB(limits.ColumnMinimum, limits.RowMinimum, limits.ColumnMaximum + 1, limits.RowMaximum + 1);
        Point? secondBest = null;
        Av1MotionSearchBase.FullPixelResult result = Av1MotionSearchBase.SearchDiamond(
            ref cost, Av1MotionSearchBase.ClampToBounds(start, bounds), stepParameter, frame.Sites, bounds, true, ref secondBest);

        // A poor site-search result continues with a mesh search. The threshold is scaled from a 16x16 block to the block area.
        int areaLog2 = blockSize.Get4x4WidthLog2() + blockSize.Get4x4HeightLog2();
        bool runMesh = result.Cost > (frame.MeshThreshold >> (10 - areaLog2));

        // A winner close to the unclamped start skips the mesh.
        if (this.pruneMeshSearch && Math.Max(Math.Abs(start.Y - result.Vector.Y), Math.Abs(start.X - result.Vector.X)) <= 4)
        {
            runMesh = false;
        }

        if (runMesh)
        {
            int meshSpeed = Math.Min((int)this.speed, 5);
            Av1MotionSearchBase.FullPixelResult mesh = Av1MotionSearchBase.SearchMesh(
                ref cost, result.Vector, MeshPatterns.Slice(meshSpeed * 8, 8), fineSearchInterval, bounds, ref secondBest);

            if (mesh.Cost < result.Cost)
            {
                result = mesh;
            }
        }

        // The motion error is the squared error of the winner plus its vector cost and the new-vector surcharge.
        int error = result.SquaredError + result.MotionCost + NewMotionVectorModePenalty;
        if (error < bestError)
        {
            bestError = error;
            bestVector = result.Vector;
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
    /// Borrows the source block, reference plane and costs of one full-sample search. It measures every row of the block and serves as the
    /// error measure of the shared diamond and mesh searches.
    /// </summary>
    private ref struct MotionSearchCost : Av1MotionSearchBase.IFullPixelCost
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

        /// <summary>
        /// Gets the reference index of a candidate.
        /// </summary>
        /// <param name="vector">The full-sample vector.</param>
        /// <returns>The reference index of the displaced block origin.</returns>
        public readonly int GetReferenceIndex(Point vector) => this.ReferenceIndex + (vector.Y * this.ReferenceStride) + vector.X;

        /// <summary>
        /// Measures the absolute differences of every row of the block at eight-bit precision.
        /// </summary>
        /// <param name="referenceIndex">The reference index of the displaced block origin.</param>
        /// <returns>The absolute differences.</returns>
        public readonly int GetSad(int referenceIndex)
            => TOperator.SumAbsoluteDifferences(
                this.Source[this.SourceIndex..],
                this.SourceStride,
                this.Reference[referenceIndex..],
                this.ReferenceStride,
                this.Width,
                this.Height) >> this.PrecisionShift;

        /// <summary>
        /// Gets the absolute-difference cost of a vector relative to the full-sample reference vector.
        /// </summary>
        /// <param name="vector">The full-sample vector.</param>
        /// <returns>The vector cost, which is never negative.</returns>
        public readonly int GetSadRateCost(Point vector)
        {
            Av1MotionVector difference = new((vector.Y - this.FullReferenceVector.Y) * 8, (vector.X - this.FullReferenceVector.X) * 8);
            int rate = this.Costs.GetCost(difference, default);
            return Av1RateDistortion.GetMotionSearchSadCost(this.SadPerBit, rate, 0);
        }

        /// <summary>
        /// Measures the variance and the squared error of a vector, and its variance-domain cost relative to the reference vector.
        /// </summary>
        /// <param name="vector">The full-sample vector.</param>
        /// <returns>The variance, squared error and vector cost.</returns>
        public readonly Av1MotionSearchBase.FullPixelResult GetVarianceResult(Point vector)
        {
            TOperator.GetMoments(
                this.Source[this.SourceIndex..],
                this.SourceStride,
                this.Reference[this.GetReferenceIndex(vector)..],
                this.ReferenceStride,
                this.Width,
                this.Height,
                out int sum,
                out long squares);

            // At the higher bit depths the sum and the squares each round to eight-bit precision. The separate rounding can make the variance
            // negative, so the variance clamps at zero.
            if (this.PrecisionShift != 0)
            {
                sum = (sum + (1 << (this.PrecisionShift - 1))) >> this.PrecisionShift;
                int squaredShift = 2 * this.PrecisionShift;
                squares = (squares + (1L << (squaredShift - 1))) >> squaredShift;
            }

            long variance = Math.Max(squares - (((long)sum * sum) / (this.Width * this.Height)), 0);
            int rate = this.Costs.GetCost(new Av1MotionVector(vector.Y * 8, vector.X * 8), this.ReferenceVector);
            int motionCost = Av1RateDistortion.GetMotionSearchCost(this.RateMultiplier, rate, 0);
            return new Av1MotionSearchBase.FullPixelResult(vector, (int)variance, (int)squares, motionCost);
        }
    }
}
