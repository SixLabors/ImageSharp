// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Motion.Av1MotionSearchSettings;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

internal static partial class Av1MotionSearchBase
{
    /// <summary>
    /// Retains the fractional winner and the error statistics used to select it.
    /// </summary>
    public readonly struct FractionalResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FractionalResult"/> struct.
        /// </summary>
        /// <param name="vector">The displacement in eighth-sample units.</param>
        /// <param name="variance">The normalized residual variance.</param>
        /// <param name="squaredError">The normalized squared residual sum.</param>
        /// <param name="motionCost">The variance-domain rate cost.</param>
        public FractionalResult(Av1MotionVector vector, int variance, int squaredError, int motionCost)
        {
            this.Vector = vector;
            this.Variance = variance;
            this.SquaredError = squaredError;
            this.MotionCost = motionCost;
        }

        /// <summary>
        /// Gets the displacement in eighth-sample units.
        /// </summary>
        public Av1MotionVector Vector { get; }

        /// <summary>
        /// Gets the normalized residual variance.
        /// </summary>
        public int Variance { get; }

        /// <summary>
        /// Gets the normalized squared residual sum.
        /// </summary>
        public int SquaredError { get; }

        /// <summary>
        /// Gets the variance-domain rate cost.
        /// </summary>
        public int MotionCost { get; }

        /// <summary>
        /// Gets the combined selection cost.
        /// </summary>
        public int Cost => this.Variance + this.MotionCost;
    }

    /// <summary>
    /// Refines an unscaled reference prediction while borrowing the worker's sample and entropy storage.
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The closed prediction and error operator.</typeparam>
    public readonly ref struct FractionalSearch<TSample, TOperator>
        where TSample : unmanaged
        where TOperator : struct, IMotionSearchOperator<TSample>
    {
        private readonly ReadOnlySpan<TSample> source;
        private readonly ReadOnlySpan<TSample> reference;
        private readonly Span<TSample> prediction;
        private readonly int sourceStride;
        private readonly int referenceStride;
        private readonly int referenceOrigin;
        private readonly Size blockSize;
        private readonly Rectangle bounds;
        private readonly Av1MotionVector referenceVector;
        private readonly Av1MotionVectorCosts costs;
        private readonly int bitDepth;
        private readonly int rateMultiplier;

        /// <summary>
        /// Initializes a new instance of the <see cref="FractionalSearch{TSample, TOperator}"/> struct.
        /// </summary>
        /// <param name="source">Source samples beginning at the block origin.</param>
        /// <param name="sourceStride">The source row stride in samples.</param>
        /// <param name="reference">The complete bordered reference plane.</param>
        /// <param name="referenceStride">The reference row stride in samples.</param>
        /// <param name="referenceOrigin">The reference index corresponding to the current block origin.</param>
        /// <param name="prediction">The worker's reusable fractional prediction buffer.</param>
        /// <param name="blockSize">The prediction dimensions.</param>
        /// <param name="bounds">The permitted eighth-sample displacements, with exclusive upper edges.</param>
        /// <param name="referenceVector">The spatial entropy reference in eighth-sample units.</param>
        /// <param name="costs">The retained motion-rate tables.</param>
        /// <param name="bitDepth">The coded component precision.</param>
        /// <param name="rateMultiplier">The block rate multiplier.</param>
        public FractionalSearch(
            ReadOnlySpan<TSample> source,
            int sourceStride,
            ReadOnlySpan<TSample> reference,
            int referenceStride,
            int referenceOrigin,
            Span<TSample> prediction,
            Size blockSize,
            Rectangle bounds,
            Av1MotionVector referenceVector,
            Av1MotionVectorCosts costs,
            Av1BitDepth bitDepth,
            int rateMultiplier)
        {
            this.source = source;
            this.sourceStride = sourceStride;
            this.reference = reference;
            this.referenceStride = referenceStride;
            this.referenceOrigin = referenceOrigin;
            this.prediction = prediction;
            this.blockSize = blockSize;
            this.bounds = bounds;
            this.referenceVector = referenceVector;
            this.costs = costs;
            this.bitDepth = bitDepth.GetBitCount();
            this.rateMultiplier = rateMultiplier;
        }

        /// <summary>
        /// Runs the selected fractional tree and publishes its retained winner.
        /// </summary>
        /// <param name="start">The initial eighth-sample displacement.</param>
        /// <param name="startStatistics">Integer-search statistics when that starting prediction was already measured.</param>
        /// <param name="method">The fractional decision policy.</param>
        /// <param name="precision">The finest permitted search step.</param>
        /// <param name="allowHighPrecision">Whether eighth-sample candidates are enabled by the frame.</param>
        /// <param name="iterationsPerStep">The number of refinement levels at each precision.</param>
        /// <param name="taps">The full tree's interpolation tap count.</param>
        /// <param name="costList">The integer center, left, down, right, and up costs, or an empty span when unavailable.</param>
        /// <param name="previousCenters">Three retained precision centers, or an empty span when duplicate pruning is disabled.</param>
        /// <param name="result">The selected vector and its error statistics.</param>
        /// <returns>The selected cost, or <see cref="int.MaxValue"/> when a previously searched center terminates the path.</returns>
        public int Search(
            Av1MotionVector start,
            FullPixelResult? startStatistics,
            FractionalSearchMethod method,
            SearchPrecision precision,
            bool allowHighPrecision,
            int iterationsPerStep,
            int taps,
            ReadOnlySpan<int> costList,
            Span<Av1MotionVector> previousCenters,
            out FractionalResult result)
        {
            // Integer search has already paid for these moments. Retain that exact error domain, including
            // its signed high-depth rounding, until a fractional candidate strictly improves the total cost.
            if (startStatistics.HasValue)
            {
                FullPixelResult statistics = startStatistics.Value;
                result = new FractionalResult(start, statistics.Variance, statistics.SquaredError, statistics.MotionCost);
            }
            else
            {
                result = this.Measure(start, method == FractionalSearchMethod.TwoLevelTree ? taps : 2);
            }

            int rounds = Math.Min(3 - (int)precision, allowHighPrecision ? 3 : 2);
            for (int iteration = 0, step = 4; iteration < rounds; iteration++, step >>= 1)
            {
                Av1MotionVector center = result.Vector;
                if (!previousCenters.IsEmpty)
                {
                    // Each slot belongs to one precision. Another starting candidate reaching the same
                    // center has the same remaining tree, so the caller can discard this duplicate path.
                    if (previousCenters[iteration] == center)
                    {
                        return int.MaxValue;
                    }

                    previousCenters[iteration] = center;
                }

                bool finiteNeighborhood = costList.Length == 5
                    && costList[0] != int.MaxValue
                    && costList[1] != int.MaxValue
                    && costList[2] != int.MaxValue
                    && costList[3] != int.MaxValue
                    && costList[4] != int.MaxValue;

                if (iteration == 0 && method == FractionalSearchMethod.PrunedTree && finiteNeighborhood)
                {
                    // Half-sample pruning chooses one quadrant from the integer cost surface. Ties select
                    // right and up here; the measured-cardinal tree below instead breaks ties left and up.
                    int column = costList[1] < costList[3] ? -step : step;
                    int row = costList[2] < costList[4] ? step : -step;
                    this.Check(new Av1MotionVector(center.Row, center.Column + column), 2, ref result);
                    this.Check(new Av1MotionVector(center.Row + row, center.Column), 2, ref result);
                    this.Check(new Av1MotionVector(center.Row + row, center.Column + column), 2, ref result);
                    continue;
                }

                if (iteration == 0 && method == FractionalSearchMethod.MorePrunedTree && finiteNeighborhood
                    && costList[0] < costList[1] && costList[0] < costList[2]
                    && costList[0] < costList[3] && costList[0] < costList[4])
                {
                    // A strictly lower center gives positive curvature on both axes. The minimum of each
                    // fitted parabola is (negative-side cost - positive-side cost) / (2 * curvature).
                    // Multiplying that location by two gives half-sample units; signed division rounds
                    // the displacement to the nearest such unit before converting it to eighth samples.
                    int columnNumerator = costList[1] - costList[3];
                    int columnDenominator = costList[1] - (2 * costList[0]) + costList[3];
                    int rowNumerator = costList[4] - costList[2];
                    int rowDenominator = costList[4] - (2 * costList[0]) + costList[2];
                    int column = (columnNumerator + (columnNumerator < 0 ? -columnDenominator / 2 : columnDenominator / 2))
                        / columnDenominator;
                    int row = (rowNumerator + (rowNumerator < 0 ? -rowDenominator / 2 : rowDenominator / 2)) / rowDenominator;

                    if ((row | column) != 0)
                    {
                        this.Check(new Av1MotionVector(center.Row + (row * step), center.Column + (column * step)), 2, ref result);
                    }

                    continue;
                }

                int selectedTaps = method == FractionalSearchMethod.TwoLevelTree ? taps : 2;
                int left = this.Check(new Av1MotionVector(center.Row, center.Column - step), selectedTaps, ref result);
                int right = this.Check(new Av1MotionVector(center.Row, center.Column + step), selectedTaps, ref result);
                int up = this.Check(new Av1MotionVector(center.Row - step, center.Column), selectedTaps, ref result);
                int down = this.Check(new Av1MotionVector(center.Row + step, center.Column), selectedTaps, ref result);
                int diagonalRow = up <= down ? -step : step;
                int diagonalColumn = left <= right ? -step : step;
                this.Check(new Av1MotionVector(center.Row + diagonalRow, center.Column + diagonalColumn), selectedTaps, ref result);

                if (iterationsPerStep > 1 && result.Vector != center)
                {
                    // All second-level sites are anchored to the first-level winner. Updating that winner
                    // while measuring these sites must not move the remaining sites of the same level.
                    Av1MotionVector winner = result.Vector;
                    if (method == FractionalSearchMethod.TwoLevelTree)
                    {
                        if (winner.Row == center.Row)
                        {
                            diagonalRow = -diagonalRow;
                        }
                        else if (winner.Column == center.Column)
                        {
                            diagonalColumn = -diagonalColumn;
                        }

                        int previousCost = result.Cost;
                        this.Check(new Av1MotionVector(winner.Row + diagonalRow, winner.Column), selectedTaps, ref result);
                        this.Check(new Av1MotionVector(winner.Row, winner.Column + diagonalColumn), selectedTaps, ref result);

                        // Extend to the outward diagonal only when an outward cardinal site improved.
                        if (result.Cost < previousCost)
                        {
                            this.Check(new Av1MotionVector(winner.Row + diagonalRow, winner.Column + diagonalColumn), selectedTaps, ref result);
                        }
                    }
                    else if (winner.Row != center.Row && winner.Column != center.Column)
                    {
                        this.Check(new Av1MotionVector(winner.Row, winner.Column + diagonalColumn), 2, ref result);
                        this.Check(new Av1MotionVector(winner.Row + diagonalRow, winner.Column), 2, ref result);
                    }
                    else if (winner.Row == center.Row)
                    {
                        this.Check(new Av1MotionVector(winner.Row + step, winner.Column + diagonalColumn), 2, ref result);
                        this.Check(new Av1MotionVector(winner.Row - step, winner.Column + diagonalColumn), 2, ref result);
                        this.Check(new Av1MotionVector(winner.Row - diagonalRow, winner.Column), 2, ref result);
                    }
                    else
                    {
                        this.Check(new Av1MotionVector(winner.Row + diagonalRow, winner.Column + step), 2, ref result);
                        this.Check(new Av1MotionVector(winner.Row + diagonalRow, winner.Column - step), 2, ref result);
                        this.Check(new Av1MotionVector(winner.Row, winner.Column - diagonalColumn), 2, ref result);
                    }
                }
            }

            return result.Cost;
        }

        /// <summary>
        /// Measures an in-range candidate and replaces the retained winner only for a strictly smaller cost.
        /// </summary>
        private int Check(Av1MotionVector vector, int taps, ref FractionalResult best)
        {
            if (!this.bounds.Contains(vector.Column, vector.Row))
            {
                return int.MaxValue;
            }

            FractionalResult candidate = this.Measure(vector, taps);
            if (candidate.Cost < best.Cost)
            {
                best = candidate;
            }

            return candidate.Cost;
        }

        /// <summary>
        /// Filters the borrowed reference and measures prediction-minus-source moments in the search error domain.
        /// </summary>
        private FractionalResult Measure(Av1MotionVector vector, int taps)
        {
            int referenceIndex = this.referenceOrigin + ((vector.Row >> 3) * this.referenceStride) + (vector.Column >> 3);
            TOperator.Predict(
                this.reference,
                this.referenceStride,
                referenceIndex,
                this.prediction,
                this.blockSize.Width,
                this.blockSize.Height,
                vector.Column & 7,
                vector.Row & 7,
                taps,
                this.bitDepth);

            TOperator.GetMoments(
                this.prediction,
                this.blockSize.Width,
                this.source,
                this.sourceStride,
                this.blockSize.Width,
                this.blockSize.Height,
                out int sum,
                out long squares);

            int precisionShift = this.bitDepth - 8;
            if (precisionShift != 0)
            {
                // Prediction is the first operand: signed rounding is asymmetric for negative residual
                // sums. Normalize that sum and its squares independently before subtracting the mean.
                sum = (sum + (1 << (precisionShift - 1))) >> precisionShift;
                int squaredShift = precisionShift * 2;
                squares = (squares + (1L << (squaredShift - 1))) >> squaredShift;
            }

            int variance = (int)Math.Max(squares - (((long)sum * sum) / (this.blockSize.Width * this.blockSize.Height)), 0);
            int rate = this.costs.GetCost(vector, this.referenceVector);
            int motionCost = Av1RateDistortion.GetMotionSearchCost(this.rateMultiplier, rate, 0);
            return new FractionalResult(vector, variance, (int)squares, motionCost);
        }
    }
}
