// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Motion.Av1MotionSearchSettings;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Owns motion-search traversal while closed sample operators measure prediction errors.
/// </summary>
internal static partial class Av1MotionSearchBase
{
    /// <summary>
    /// Retains the integer winner's distortion and rate separately for fractional refinement.
    /// </summary>
    public readonly struct FullPixelResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FullPixelResult"/> struct.
        /// </summary>
        /// <param name="vector">The displacement in full samples.</param>
        /// <param name="variance">The variance in the eight-bit error domain.</param>
        /// <param name="squaredError">The squared residual sum in the eight-bit error domain.</param>
        /// <param name="motionCost">The variance-domain motion-rate cost.</param>
        public FullPixelResult(Point vector, int variance, int squaredError, int motionCost)
        {
            this.Vector = vector;
            this.Variance = variance;
            this.SquaredError = squaredError;
            this.MotionCost = motionCost;
        }

        /// <summary>
        /// Gets the displacement in full samples.
        /// </summary>
        public Point Vector { get; }

        /// <summary>
        /// Gets the normalized residual variance.
        /// </summary>
        public int Variance { get; }

        /// <summary>
        /// Gets the normalized squared residual sum.
        /// </summary>
        public int SquaredError { get; }

        /// <summary>
        /// Gets the variance-domain motion-rate cost.
        /// </summary>
        public int MotionCost { get; }

        /// <summary>
        /// Gets the total cost used to compare completed search paths.
        /// </summary>
        public int Cost => this.Variance + this.MotionCost;
    }

    /// <summary>
    /// Borrows source, reference, and rate state for all integer candidates of a prediction block.
    /// </summary>
    /// <typeparam name="TSample">The unsigned component storage type.</typeparam>
    /// <typeparam name="TOperator">The closed sample-error operator.</typeparam>
    public readonly ref struct FullPixelSearch<TSample, TOperator>
        where TSample : unmanaged
        where TOperator : struct, IMotionSearchOperator<TSample>
    {
        private readonly ReadOnlySpan<TSample> source;
        private readonly ReadOnlySpan<TSample> reference;
        private readonly int sourceStride;
        private readonly int referenceStride;
        private readonly int referenceOrigin;
        private readonly Size blockSize;
        private readonly Rectangle bounds;
        private readonly Av1MotionVector referenceVector;
        private readonly Av1MotionVector integerReferenceVector;
        private readonly Av1MotionVectorCosts costs;
        private readonly int precisionShift;
        private readonly int sadPerBit;
        private readonly int rateMultiplier;

        /// <summary>
        /// Initializes a new instance of the <see cref="FullPixelSearch{TSample, TOperator}"/> struct.
        /// </summary>
        /// <param name="source">Source samples beginning at the block origin.</param>
        /// <param name="sourceStride">The source row stride in samples.</param>
        /// <param name="reference">The complete retained reference storage including its border.</param>
        /// <param name="referenceStride">The reference row stride in samples.</param>
        /// <param name="referenceOrigin">The reference index corresponding to the current block origin.</param>
        /// <param name="blockSize">The prediction dimensions.</param>
        /// <param name="bounds">The permitted displacement rectangle, with exclusive upper edges.</param>
        /// <param name="referenceVector">The spatial reference in eighth-sample units.</param>
        /// <param name="costs">The retained motion-rate tables.</param>
        /// <param name="bitDepth">The coded component precision.</param>
        /// <param name="sadPerBit">The quantizer-derived rate scale for absolute differences.</param>
        /// <param name="rateMultiplier">The block rate multiplier for variance costs.</param>
        public FullPixelSearch(
            ReadOnlySpan<TSample> source,
            int sourceStride,
            ReadOnlySpan<TSample> reference,
            int referenceStride,
            int referenceOrigin,
            Size blockSize,
            Rectangle bounds,
            Av1MotionVector referenceVector,
            Av1MotionVectorCosts costs,
            Av1BitDepth bitDepth,
            int sadPerBit,
            int rateMultiplier)
        {
            this.source = source;
            this.sourceStride = sourceStride;
            this.reference = reference;
            this.referenceStride = referenceStride;
            this.referenceOrigin = referenceOrigin;
            this.blockSize = blockSize;
            this.bounds = bounds;
            this.referenceVector = referenceVector;
            this.costs = costs;
            this.precisionShift = bitDepth.GetBitCount() - 8;
            this.sadPerBit = sadPerBit;
            this.rateMultiplier = rateMultiplier;

            // Nearest full-sample rounding breaks half-sample ties away from zero. SAD compares integer
            // differences from that rounded reference; variance retains the original subpixel difference.
            int row = (referenceVector.Row + 3 + (referenceVector.Row >= 0 ? 1 : 0)) >> 3;
            int column = (referenceVector.Column + 3 + (referenceVector.Column >= 0 ? 1 : 0)) >> 3;
            this.integerReferenceVector = new Av1MotionVector(row * 8, column * 8);
        }

        /// <summary>
        /// Runs the selected full-pixel search, restarts, mesh decision, and neighboring cost publication.
        /// </summary>
        /// <param name="start">The initial displacement in full samples.</param>
        /// <param name="stepParameter">The number of outer search stages already excluded by frame and block policy.</param>
        /// <param name="method">The block-selected search method.</param>
        /// <param name="sites">The retained geometry configured for this method and reference stride.</param>
        /// <param name="settings">The resolved frame motion policy.</param>
        /// <param name="keyFrame">Whether key-frame policy prevents adaptive alternate-row SAD.</param>
        /// <param name="fineMeshInterval">Whether content classification caps the initial mesh interval at four.</param>
        /// <param name="costList">Five costs: center, left, down, right, and up; empty when neighborhood publication is disabled.</param>
        /// <param name="secondBest">The preceding integer winner, when the selected traversal supplies one.</param>
        /// <returns>The integer winner with its retained variance, squared error, and motion cost.</returns>
        public FullPixelResult Search(
            Point start,
            int stepParameter,
            FullPixelSearchMethod method,
            Av1MotionSearchSites sites,
            Av1MotionSearchSettings settings,
            bool keyFrame,
            bool fineMeshInterval,
            Span<int> costList,
            out Point? secondBest)
        {
            Point clampedStart = this.Clamp(start);
            int rowStep = 1;
            if (this.blockSize.Height >= 16)
            {
                if (settings.DownsampledSadLevel == 2)
                {
                    rowStep = 2;
                }
                else if (settings.DownsampledSadLevel == 1 && !keyFrame)
                {
                    int evenSad = this.GetSad(clampedStart, 2, 0);
                    int oddSad = this.GetSad(clampedStart, 2, 1);
                    if ((Math.Abs(evenSad - oddSad) * 4) < evenSad)
                    {
                        rowStep = 2;
                    }
                }
            }

            // An alternate-row search may alias vertical texture. If its final candidate exposes that aliasing,
            // repeat the same complete search with full SAD; candidate state and cost-list state both restart.
            while (true)
            {
                secondBest = null;
                FullPixelResult best;
                bool centerCostOnly = false;
                if (method <= FullPixelSearchMethod.ClampedDiamond)
                {
                    best = this.SearchDiamond(clampedStart, stepParameter, sites, rowStep, ref secondBest);
                }
                else
                {
                    best = this.SearchPattern(clampedStart, stepParameter, method, sites, rowStep, out centerCostOnly);
                }

                if (centerCostOnly && !costList.IsEmpty)
                {
                    // An initial finest-scale winner skips the four-point refinement stage. Its neighbors
                    // have not been published, so fractional pruning must see them as unavailable.
                    costList.Fill(int.MaxValue);
                    costList[0] = this.GetSadCost(best.Vector, rowStep);
                }
                else if (!costList.IsEmpty)
                {
                    this.FillCostList(best.Vector, rowStep, costList);
                }

                int areaLog2 = BitOperations.Log2((uint)(this.blockSize.Width * this.blockSize.Height));
                bool runMesh = method is FullPixelSearchMethod.NStep or FullPixelSearchMethod.EightPointNStep
                    && best.Cost > (settings.MeshErrorThreshold >> (14 - areaLog2));

                // Distance is measured from the caller's original start, before range clamping.
                if (settings.MeshPruningLevel == 2 &&
                    Math.Max(Math.Abs(start.X - best.Vector.X), Math.Abs(start.Y - best.Vector.Y)) <= 4)
                {
                    runMesh = false;
                }

                if (rowStep == 2)
                {
                    int fullSad = this.GetSad(best.Vector, 1, 0);
                    int skippedSad = this.GetSad(best.Vector, 2, 0);
                    int threshold = (this.blockSize.Width * this.blockSize.Height) >> 4;
                    if (fullSad > threshold && Math.Abs(skippedSad - fullSad) * 10 >= Math.Max(fullSad, 1) * 9)
                    {
                        rowStep = 1;
                        continue;
                    }
                }

                if (runMesh)
                {
                    FullPixelResult mesh = this.SearchMesh(
                        best.Vector, settings.GetMeshPattern(intraBlockCopy: false), fineMeshInterval, rowStep, ref secondBest);

                    // The mesh publishes its neighborhood and preceding winner before its final variance comparison.
                    // Keep that publication order so later fractional selection sees the same retained search state.
                    if (!costList.IsEmpty)
                    {
                        this.FillCostList(mesh.Vector, rowStep, costList);
                    }

                    if (mesh.Cost < best.Cost)
                    {
                        best = mesh;
                    }
                }

                return best;
            }
        }

        /// <summary>
        /// Runs decreasing-radius searches from the same start and compares their winners using variance.
        /// </summary>
        private FullPixelResult SearchDiamond(
            Point start,
            int stepParameter,
            Av1MotionSearchSites sites,
            int rowStep,
            ref Point? secondBest)
        {
            int startCost = this.GetSadCost(start, rowStep);
            Point winner = this.SearchDiamondSteps(start, startCost, stepParameter, sites, rowStep, ref secondBest, out int centeredSteps);
            FullPixelResult best = this.GetVarianceResult(winner);
            int furtherSteps = sites.StageCount - 1 - stepParameter;
            while (centeredSteps < furtherSteps)
            {
                centeredSteps++;
                winner = this.SearchDiamondSteps(
                    start, startCost, stepParameter + centeredSteps, sites, rowStep, ref secondBest, out int skippedSteps);

                FullPixelResult candidate = this.GetVarianceResult(winner);
                if (candidate.Cost < best.Cost)
                {
                    best = candidate;
                }

                centeredSteps += skippedSteps;
            }

            return best;
        }

        /// <summary>
        /// Visits ordered sites once per radius, retaining initial center stays for later restart pruning.
        /// </summary>
        private Point SearchDiamondSteps(
            Point start,
            int startCost,
            int stepParameter,
            Av1MotionSearchSites sites,
            int rowStep,
            ref Point? secondBest,
            out int centeredSteps)
        {
            Point best = start;
            int bestCost = startCost;
            bool movedFromStart = false;
            centeredSteps = 0;
            for (int stage = sites.StageCount - stepParameter - 1; stage >= 0; stage--)
            {
                ReadOnlySpan<Av1MotionSearchSites.Site> stageSites = sites.GetSites(stage);
                int centerIndex = this.referenceOrigin + (best.Y * this.referenceStride) + best.X;
                int bestSite = 0;
                for (int index = 1; index <= sites.GetCandidateCount(stage); index++)
                {
                    Av1MotionSearchSites.Site site = stageSites[index];
                    Point candidate = new(best.X + site.Column, best.Y + site.Row);
                    if (this.bounds.Contains(candidate) && this.TryImproveSad(candidate, centerIndex + site.Offset, rowStep, ref bestCost))
                    {
                        bestSite = index;
                    }
                }

                if (bestSite != 0)
                {
                    secondBest = best;
                    Av1MotionSearchSites.Site site = stageSites[bestSite];
                    best = new Point(best.X + site.Column, best.Y + site.Row);
                    movedFromStart = true;
                }

                if (!movedFromStart)
                {
                    centeredSteps++;
                }

                // Repeated outer radii can be skipped after a center stay; after a move they must remain eligible.
                if (bestSite == 0 && stage > 2)
                {
                    while (stage > 2 && sites.GetRadius(stage - 1) == sites.GetRadius(stage))
                    {
                        centeredSteps++;
                        stage--;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Selects an initial scale, then walks adjacent sites around each winning direction before reducing scale.
        /// </summary>
        private FullPixelResult SearchPattern(
            Point start,
            int stepParameter,
            FullPixelSearchMethod method,
            Av1MotionSearchSites sites,
            int rowStep,
            out bool centerCostOnly)
        {
            bool initialSearch = method is FullPixelSearchMethod.Hexagon or FullPixelSearchMethod.BigDiamond;
            centerCostOnly = false;
            int minimumStep = method switch
            {
                FullPixelSearchMethod.FastBigDiamond => 8,
                FullPixelSearchMethod.FastDiamond => 9,
                FullPixelSearchMethod.VeryFastDiamond => 10,
                _ => 0
            };

            int initialScale = 10 - Math.Min(Math.Max(stepParameter, minimumStep), 10);
            int bestCost = this.GetSadCost(start, rowStep);
            Point best = start;
            int direction = -1;
            if (initialSearch)
            {
                int maximumScale = initialScale;
                initialScale = -1;
                for (int scale = 0; scale <= maximumScale; scale++)
                {
                    int candidateIndex = this.FindBestSite(start, scale, sites, rowStep, ref bestCost);
                    if (candidateIndex >= 0)
                    {
                        initialScale = scale;
                        direction = candidateIndex;
                    }
                }

                if (initialScale >= 0)
                {
                    Av1MotionSearchSites.Site site = sites.GetSites(initialScale)[direction];
                    best = new Point(start.X + site.Column, start.Y + site.Row);
                }
            }

            if (initialScale >= 0)
            {
                bool fourPointFinalStage = sites.GetCandidateCount(0) == 4;
                centerCostOnly = fourPointFinalStage && initialSearch && initialScale == 0;
                int lastScale = fourPointFinalStage ? 1 : 0;
                for (int scale = initialScale; scale >= lastScale; scale--)
                {
                    ReadOnlySpan<Av1MotionSearchSites.Site> stageSites = sites.GetSites(scale);
                    if (!initialSearch || scale != initialScale)
                    {
                        int candidateIndex = this.FindBestSite(best, scale, sites, rowStep, ref bestCost);
                        if (candidateIndex < 0)
                        {
                            continue;
                        }

                        direction = candidateIndex;
                        Av1MotionSearchSites.Site site = stageSites[direction];
                        best = new Point(best.X + site.Column, best.Y + site.Row);
                    }

                    best = this.FollowPatternDirection(best, scale, direction, sites, rowStep, ref bestCost);
                }

                // Four-point patterns retain a separate final-stage entry decision. When the initial scale
                // is already zero, its initial winner is published without another directional walk.
                if (fourPointFinalStage && (!initialSearch || initialScale != 0))
                {
                    int candidateIndex = this.FindBestSite(best, 0, sites, rowStep, ref bestCost);
                    if (candidateIndex >= 0)
                    {
                        Av1MotionSearchSites.Site site = sites.GetSites(0)[candidateIndex];
                        best = new Point(best.X + site.Column, best.Y + site.Row);
                        best = this.FollowPatternDirection(best, 0, candidateIndex, sites, rowStep, ref bestCost);
                    }
                }
            }

            return this.GetVarianceResult(best);
        }

        /// <summary>
        /// Tests the complete stage around a fixed center, keeping the first candidate on equal cost.
        /// </summary>
        private int FindBestSite(Point center, int stage, Av1MotionSearchSites sites, int rowStep, ref int bestCost)
        {
            ReadOnlySpan<Av1MotionSearchSites.Site> stageSites = sites.GetSites(stage);
            int centerIndex = this.referenceOrigin + (center.Y * this.referenceStride) + center.X;
            int count = sites.GetCandidateCount(stage);
            int radius = sites.GetRadius(stage);
            if (center.X - radius >= this.bounds.Left && center.X + radius < this.bounds.Right &&
                center.Y - radius >= this.bounds.Top && center.Y + radius < this.bounds.Bottom)
            {
                // Interior pattern stages visit complete four-site groups. For a six-site hexagon the final
                // two sites are visited only by the boundary path, so range classification affects selection.
                count &= ~3;
            }

            int bestIndex = -1;
            for (int index = 0; index < count; index++)
            {
                Av1MotionSearchSites.Site site = stageSites[index];
                Point candidate = new(center.X + site.Column, center.Y + site.Row);
                if (this.bounds.Contains(candidate) && this.TryImproveSad(candidate, centerIndex + site.Offset, rowStep, ref bestCost))
                {
                    bestIndex = index;
                }
            }

            return bestIndex;
        }

        /// <summary>
        /// Walks the previous, same, and next directions around the ring until none improves the current center.
        /// </summary>
        private Point FollowPatternDirection(
            Point center,
            int stage,
            int direction,
            Av1MotionSearchSites sites,
            int rowStep,
            ref int bestCost)
        {
            int count = sites.GetCandidateCount(stage);
            ReadOnlySpan<Av1MotionSearchSites.Site> stageSites = sites.GetSites(stage);
            while (true)
            {
                int centerIndex = this.referenceOrigin + (center.Y * this.referenceStride) + center.X;
                int bestIndex = -1;
                for (int relative = -1; relative <= 1; relative++)
                {
                    int index = (direction + relative + count) % count;
                    Av1MotionSearchSites.Site site = stageSites[index];
                    Point candidate = new(center.X + site.Column, center.Y + site.Row);
                    if (this.bounds.Contains(candidate) && this.TryImproveSad(candidate, centerIndex + site.Offset, rowStep, ref bestCost))
                    {
                        bestIndex = index;
                    }
                }

                if (bestIndex < 0)
                {
                    return center;
                }

                direction = bestIndex;
                Av1MotionSearchSites.Site winningSite = stageSites[direction];
                center = new Point(center.X + winningSite.Column, center.Y + winningSite.Row);
            }
        }

        /// <summary>
        /// Runs content-selected mesh passes, adjusting the initial range to the current displacement magnitude.
        /// </summary>
        private FullPixelResult SearchMesh(Point start, ReadOnlySpan<int> pattern, bool fineInterval, int rowStep, ref Point? secondBest)
        {
            int originalRange = pattern[0];
            int interval = pattern[1];
            int range = Math.Min(Math.Max(originalRange, (5 * Math.Max(Math.Abs(start.X), Math.Abs(start.Y))) / 4), 256);
            interval = Math.Max(interval, range / (originalRange / interval));
            if (fineInterval)
            {
                interval = Math.Min(interval, 4);
            }

            Point best = this.SearchMeshPass(start, range, interval, rowStep, ref secondBest);
            if (interval > 1 && range > 7)
            {
                for (int pass = 1; pass < 4; pass++)
                {
                    best = this.SearchMeshPass(best, pattern[pass * 2], pattern[(pass * 2) + 1], rowStep, ref secondBest);
                    if (pattern[(pass * 2) + 1] == 1)
                    {
                        break;
                    }
                }
            }

            return this.GetVarianceResult(best);
        }

        /// <summary>
        /// Scans mesh rows from a fixed center; each strict replacement retains the previous winner.
        /// </summary>
        private Point SearchMeshPass(Point start, int range, int interval, int rowStep, ref Point? secondBest)
        {
            start = this.Clamp(start);
            Point best = start;
            int bestCost = this.GetSadCost(start, rowStep);
            int minimumRow = Math.Max(-range, this.bounds.Top - start.Y);
            int maximumRow = Math.Min(range, this.bounds.Bottom - 1 - start.Y);
            int minimumColumn = Math.Max(-range, this.bounds.Left - start.X);
            int maximumColumn = Math.Min(range, this.bounds.Right - 1 - start.X);
            int columnStep = interval > 1 ? interval : 4;
            for (int row = minimumRow; row <= maximumRow; row += interval)
            {
                for (int column = minimumColumn; column <= maximumColumn; column += columnStep)
                {
                    // A complete unit-step group visits four adjacent columns in order. The partial terminal
                    // group has an exclusive end; preserve that edge rule rather than widening the searched set.
                    int count = interval > 1 ? 1 : column + 3 <= maximumColumn ? 4 : maximumColumn - column;
                    for (int index = 0; index < count; index++)
                    {
                        Point candidate = new(start.X + column + index, start.Y + row);
                        if (this.TryImproveSad(candidate, rowStep, ref bestCost))
                        {
                            secondBest = best;
                            best = candidate;
                        }
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Publishes SAD-plus-rate values at the center and its four axial neighbors for fractional pruning.
        /// </summary>
        private void FillCostList(Point best, int rowStep, Span<int> costList)
        {
            costList[0] = this.GetSadCost(best, rowStep);
            ReadOnlySpan<sbyte> offsets = [0, -1, 1, 0, 0, 1, -1, 0];
            for (int index = 0; index < 4; index++)
            {
                Point candidate = new(best.X + offsets[(index * 2) + 1], best.Y + offsets[index * 2]);
                costList[index + 1] = this.bounds.Contains(candidate) ? this.GetSadCost(candidate, rowStep) : int.MaxValue;
            }
        }

        /// <summary>
        /// Clamps a starting displacement to the prediction-distinct full-pixel range.
        /// </summary>
        private Point Clamp(Point vector)
            => new(Math.Clamp(vector.X, this.bounds.Left, this.bounds.Right - 1), Math.Clamp(vector.Y, this.bounds.Top, this.bounds.Bottom - 1));

        /// <summary>
        /// Rejects candidates whose prediction error alone already reaches the best combined cost.
        /// </summary>
        private bool TryImproveSad(Point vector, int rowStep, ref int bestCost)
            => this.TryImproveSad(vector, this.referenceOrigin + (vector.Y * this.referenceStride) + vector.X, rowStep, ref bestCost);

        /// <summary>
        /// Measures a candidate using the retained site's offset, avoiding repeated stride multiplication.
        /// </summary>
        private bool TryImproveSad(Point vector, int referenceIndex, int rowStep, ref int bestCost)
        {
            int sad = this.GetSad(referenceIndex, rowStep, 0);
            if (sad >= bestCost)
            {
                return false;
            }

            int rate = this.costs.GetCost(new Av1MotionVector(vector.Y * 8, vector.X * 8), this.integerReferenceVector);
            int cost = Av1RateDistortion.GetMotionSearchSadCost(this.sadPerBit, rate, sad);
            if (cost >= bestCost)
            {
                return false;
            }

            bestCost = cost;
            return true;
        }

        /// <summary>
        /// Measures the complete absolute-difference cost in the eight-bit error domain.
        /// </summary>
        private int GetSadCost(Point vector, int rowStep)
        {
            int sad = this.GetSad(vector, rowStep, 0);
            int rate = this.costs.GetCost(new Av1MotionVector(vector.Y * 8, vector.X * 8), this.integerReferenceVector);
            return Av1RateDistortion.GetMotionSearchSadCost(this.sadPerBit, rate, sad);
        }

        /// <summary>
        /// Measures raw sample differences and truncates only after alternate-row scaling.
        /// </summary>
        private int GetSad(Point vector, int rowStep, int firstRow)
            => this.GetSad(this.referenceOrigin + (vector.Y * this.referenceStride) + vector.X, rowStep, firstRow);

        /// <summary>
        /// Measures the requested row parity at a retained reference offset.
        /// </summary>
        private int GetSad(int referenceIndex, int rowStep, int firstRow)
        {
            referenceIndex += firstRow * this.referenceStride;
            int sad = TOperator.SumAbsoluteDifferences(
                this.source[(firstRow * this.sourceStride)..],
                this.sourceStride,
                this.reference[referenceIndex..],
                this.referenceStride,
                this.blockSize.Width,
                this.blockSize.Height - firstRow,
                rowStep);

            return sad >> this.precisionShift;
        }

        /// <summary>
        /// Retains normalized moments and subpixel-reference motion rate for a completed integer winner.
        /// </summary>
        private FullPixelResult GetVarianceResult(Point vector)
        {
            int referenceIndex = this.referenceOrigin + (vector.Y * this.referenceStride) + vector.X;
            TOperator.GetMoments(
                this.source,
                this.sourceStride,
                this.reference[referenceIndex..],
                this.referenceStride,
                this.blockSize.Width,
                this.blockSize.Height,
                out int sum,
                out long squares);

            if (this.precisionShift != 0)
            {
                // Signed sums and squared sums have different scales. Round each before removing the mean;
                // cancellation may make the rounded variance negative, so clamp the final variance to zero.
                sum = (sum + (1 << (this.precisionShift - 1))) >> this.precisionShift;
                int squaredShift = this.precisionShift * 2;
                squares = (squares + (1L << (squaredShift - 1))) >> squaredShift;
            }

            int variance = (int)Math.Max(squares - (((long)sum * sum) / (this.blockSize.Width * this.blockSize.Height)), 0);
            int rate = this.costs.GetCost(new Av1MotionVector(vector.Y * 8, vector.X * 8), this.referenceVector);
            int motionCost = Av1RateDistortion.GetMotionSearchCost(this.rateMultiplier, rate, 0);
            return new FullPixelResult(vector, variance, (int)squares, motionCost);
        }
    }
}
