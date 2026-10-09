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
        /// <summary>
        /// The source samples at the block origin.
        /// </summary>
        private readonly ReadOnlySpan<TSample> source;

        /// <summary>
        /// The fixed packed predictor, or empty for a single-reference search.
        /// </summary>
        private readonly ReadOnlySpan<TSample> secondPrediction;

        /// <summary>
        /// The packed six-bit blend mask, or empty for equal weights or a single-reference search.
        /// </summary>
        private readonly ReadOnlySpan<byte> mask;

        /// <summary>
        /// The complete retained reference storage, including its border.
        /// </summary>
        private readonly ReadOnlySpan<TSample> reference;

        /// <summary>
        /// The source row stride in samples.
        /// </summary>
        private readonly int sourceStride;

        /// <summary>
        /// The reference row stride in samples.
        /// </summary>
        private readonly int referenceStride;

        /// <summary>
        /// The reference index of the current block origin.
        /// </summary>
        private readonly int referenceOrigin;

        /// <summary>
        /// The prediction dimensions.
        /// </summary>
        private readonly Size blockSize;

        /// <summary>
        /// The permitted full-sample displacements, with exclusive upper edges.
        /// </summary>
        private readonly Rectangle bounds;

        /// <summary>
        /// The spatial reference in eighth-sample units. The variance rate uses it.
        /// </summary>
        private readonly Av1MotionVector referenceVector;

        /// <summary>
        /// The spatial reference rounded to full samples, in eighth-sample units. The SAD rate uses it.
        /// </summary>
        private readonly Av1MotionVector integerReferenceVector;

        /// <summary>
        /// The retained motion-rate tables.
        /// </summary>
        private readonly Av1MotionVectorCosts costs;

        /// <summary>
        /// The shift that brings high-bit-depth errors to the eight-bit error domain.
        /// </summary>
        private readonly int precisionShift;

        /// <summary>
        /// The quantizer-derived rate scale for absolute differences.
        /// </summary>
        private readonly int sadPerBit;

        /// <summary>
        /// The block rate multiplier for variance costs.
        /// </summary>
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
        /// <param name="secondPrediction">The fixed packed predictor, or empty for a single-reference search.</param>
        /// <param name="mask">The packed six-bit blend mask, or empty for equal weights or a single-reference search.</param>
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
            int rateMultiplier,
            ReadOnlySpan<TSample> secondPrediction,
            ReadOnlySpan<byte> mask)
        {
            this.source = source;
            this.secondPrediction = secondPrediction;
            this.mask = mask;
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

            // Nearest full-sample rounding breaks half-sample ties away from zero. The SAD rate uses integer differences from that rounded reference.
            // The variance rate keeps the original subpixel difference.
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
        /// <param name="intraBlockCopy">Whether the search uses same-frame displacement and its mesh policy.</param>
        /// <param name="costList">Five costs: center, left, down, right, and up. It is empty when neighborhood publication is disabled.</param>
        /// <param name="secondBest">The preceding integer winner, when the selected traversal supplies one.</param>
        /// <param name="forceMesh">Whether the mesh search runs whatever the variance of the winner is. The temporal filter search sets it.</param>
        /// <param name="meshPruneDistance">
        /// The largest winner distance from the start that skips the mesh search, -1 to never skip it, or <see langword="null"/> for the frame motion policy.
        /// </param>
        /// <returns>The integer winner with its retained variance, squared error, and motion cost.</returns>
        public FullPixelResult Search(
            Point start,
            int stepParameter,
            FullPixelSearchMethod method,
            Av1MotionSearchSites sites,
            Av1MotionSearchSettings settings,
            bool keyFrame,
            bool fineMeshInterval,
            bool intraBlockCopy,
            Span<int> costList,
            out Point? secondBest,
            bool forceMesh = false,
            int? meshPruneDistance = null)
        {
            Point clampedStart = ClampToBounds(start, this.bounds);
            int rowStep = 1;
            if (this.secondPrediction.IsEmpty && this.blockSize.Height >= 16)
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

            // An alternate-row search can alias vertical texture.
            // If its final candidate shows that aliasing, the loop repeats the full search with full SAD.
            // The candidate state and the cost-list state both restart.
            while (true)
            {
                secondBest = null;
                SadCost cost = new(this, rowStep);
                FullPixelResult best;
                bool centerCostOnly = false;
                if (method <= FullPixelSearchMethod.ClampedDiamond)
                {
                    best = SearchDiamond(ref cost, clampedStart, stepParameter, sites, this.bounds, true, ref secondBest);
                }
                else
                {
                    best = this.SearchPattern(clampedStart, stepParameter, method, sites, rowStep, out centerCostOnly);
                }

                if (centerCostOnly && !costList.IsEmpty)
                {
                    // An initial finest-scale winner skips the four-point refinement stage.
                    // The search does not publish its neighbors, so fractional pruning sees them as unavailable.
                    costList.Fill(int.MaxValue);
                    costList[0] = this.GetSadCost(best.Vector, rowStep);
                }
                else if (!costList.IsEmpty)
                {
                    this.FillCostList(best.Vector, rowStep, costList);
                }

                int areaLog2 = BitOperations.Log2((uint)(this.blockSize.Width * this.blockSize.Height));
                bool runMesh = forceMesh || (this.secondPrediction.IsEmpty &&
                    method is FullPixelSearchMethod.NStep or FullPixelSearchMethod.EightPointNStep &&
                    best.Cost > (settings.MeshErrorThreshold >> (14 - areaLog2)));

                // The code measures the distance from the original start of the caller, before the range clamp.
                int pruneDistance = meshPruneDistance ?? (settings.MeshPruningLevel == 2 ? 4 : -1);
                if (!intraBlockCopy && pruneDistance >= 0 &&
                    Math.Max(Math.Abs(start.X - best.Vector.X), Math.Abs(start.Y - best.Vector.Y)) <= pruneDistance)
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
                    FullPixelResult mesh = SearchMesh(
                        ref cost, best.Vector, settings.GetMeshPattern(intraBlockCopy), fineMeshInterval, this.bounds, ref secondBest);

                    // The mesh publishes its neighborhood and preceding winner before its final variance comparison.
                    // This order gives the later fractional selection the search state of the mesh winner, also when the mesh winner loses.
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
        /// Refines a compound displacement through three adjacent eight-point searches.
        /// </summary>
        /// <param name="start">The initial integer-pixel displacement.</param>
        /// <param name="sadCost">The selected absolute-difference and motion-rate cost.</param>
        /// <returns>The selected integer displacement.</returns>
        public Point RefineCompound(Point start, out int sadCost)
        {
            const int searchRange = 3;
            const int gridStride = (2 * searchRange) + 1;
            Span<byte> visited = stackalloc byte[gridStride * gridStride];
            visited.Clear();
            ReadOnlySpan<sbyte> columns = [0, -1, 1, 0, -1, -1, 1, 1];
            ReadOnlySpan<sbyte> rows = [-1, 0, 0, 1, -1, 1, -1, 1];
            Point best = ClampToBounds(start, this.bounds);
            sadCost = this.GetSadCost(best, 1);
            int center = (searchRange * gridStride) + searchRange;
            visited[center] = 1;

            // The visited grid is relative to the clamped start. Three one-pixel moves fit in seven rows and columns.
            // The loop also marks rejected sites, so it measures no site twice.
            for (int iteration = 0; iteration < searchRange; iteration++)
            {
                int bestSite = -1;
                for (int site = 0; site < columns.Length; site++)
                {
                    int gridIndex = center + (rows[site] * gridStride) + columns[site];
                    if (visited[gridIndex] != 0)
                    {
                        continue;
                    }

                    visited[gridIndex] = 1;
                    Point candidate = new(best.X + columns[site], best.Y + rows[site]);
                    int offset = this.referenceOrigin + (candidate.Y * this.referenceStride) + candidate.X;
                    if (this.bounds.Contains(candidate) && this.TryImproveSad(candidate, offset, 1, ref sadCost))
                    {
                        bestSite = site;
                    }
                }

                if (bestSite < 0)
                {
                    break;
                }

                best = new Point(best.X + columns[bestSite], best.Y + rows[bestSite]);
                center += (rows[bestSite] * gridStride) + columns[bestSite];
            }

            return best;
        }

        /// <summary>
        /// Selects an initial scale, then walks adjacent sites around each winning direction before it reduces the scale.
        /// </summary>
        /// <param name="start">The clamped initial displacement in full samples.</param>
        /// <param name="stepParameter">The number of outer scales to exclude.</param>
        /// <param name="method">The pattern search method.</param>
        /// <param name="sites">The retained geometry configured for this method and reference stride.</param>
        /// <param name="rowStep">One to measure every row, or two to measure alternate rows.</param>
        /// <param name="centerCostOnly">Whether a four-point pattern kept a finest-scale initial winner. The caller publishes only its center cost.</param>
        /// <returns>The winner with its variance cost.</returns>
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

                // Four-point patterns have a separate decision at the entry of the final stage.
                // When the initial scale is already zero, the search publishes its initial winner without another directional walk.
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
        /// Tests the complete stage around a fixed center. On equal cost, the earlier candidate stays.
        /// </summary>
        /// <param name="center">The center displacement in full samples.</param>
        /// <param name="stage">The stage, ordered from the smallest search radius.</param>
        /// <param name="sites">The retained geometry configured for this method and reference stride.</param>
        /// <param name="rowStep">One to measure every row, or two to measure alternate rows.</param>
        /// <param name="bestCost">The best cost so far. Each improving site lowers it.</param>
        /// <returns>The index of the best improving site, or -1 when no site improves the cost.</returns>
        private int FindBestSite(Point center, int stage, Av1MotionSearchSites sites, int rowStep, ref int bestCost)
        {
            ReadOnlySpan<Av1MotionSearchSites.Site> stageSites = sites.GetSites(stage);
            int centerIndex = this.referenceOrigin + (center.Y * this.referenceStride) + center.X;
            int count = sites.GetCandidateCount(stage);
            int radius = sites.GetRadius(stage);
            if (center.X - radius >= this.bounds.Left && center.X + radius < this.bounds.Right &&
                center.Y - radius >= this.bounds.Top && center.Y + radius < this.bounds.Bottom)
            {
                // Interior pattern stages visit full four-site groups. For a six-site hexagon, only the boundary path visits the final two sites.
                // Thus the range test changes the selection.
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
        /// <param name="center">The current winner in full samples.</param>
        /// <param name="stage">The stage, ordered from the smallest search radius.</param>
        /// <param name="direction">The index of the site that moved the search to <paramref name="center"/>.</param>
        /// <param name="sites">The retained geometry configured for this method and reference stride.</param>
        /// <param name="rowStep">One to measure every row, or two to measure alternate rows.</param>
        /// <param name="bestCost">The best cost so far. Each improving site lowers it.</param>
        /// <returns>The final winner in full samples.</returns>
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
        /// Publishes SAD-plus-rate values at the center and its four axial neighbors for fractional pruning.
        /// </summary>
        /// <param name="best">The integer winner in full samples.</param>
        /// <param name="rowStep">One to measure every row, or two to measure alternate rows.</param>
        /// <param name="costList">Receives five costs: center, left, down, right, and up. An out-of-range neighbor gets <see cref="int.MaxValue"/>.</param>
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
        /// Measures a candidate at a known reference index, so a site offset avoids a stride multiplication.
        /// </summary>
        /// <param name="vector">The candidate displacement in full samples.</param>
        /// <param name="referenceIndex">The reference index of the candidate.</param>
        /// <param name="rowStep">One to measure every row, or two to measure alternate rows.</param>
        /// <param name="bestCost">The best cost so far, replaced when the candidate costs less.</param>
        /// <returns><see langword="true"/> when the candidate has a strictly lower cost.</returns>
        private bool TryImproveSad(Point vector, int referenceIndex, int rowStep, ref int bestCost)
        {
            // The rate term is never negative, so a SAD that already reaches the best cost cannot win. That test skips the rate lookup.
            int sad = this.GetSad(referenceIndex, rowStep, 0);
            if (sad >= bestCost)
            {
                return false;
            }

            int cost = sad + this.GetSadRateCost(vector);
            if (cost >= bestCost)
            {
                return false;
            }

            bestCost = cost;
            return true;
        }

        /// <summary>
        /// Measures the absolute-difference cost plus the motion-rate cost, in the eight-bit error domain.
        /// </summary>
        /// <param name="vector">The displacement in full samples.</param>
        /// <param name="rowStep">One to measure every row, or two to measure alternate rows.</param>
        /// <returns>The SAD-plus-rate cost.</returns>
        private int GetSadCost(Point vector, int rowStep) => this.GetSad(vector, rowStep, 0) + this.GetSadRateCost(vector);

        /// <summary>
        /// Gets the motion-rate cost of a displacement in the absolute-difference domain. The rate is measured from the spatial reference
        /// rounded to full samples.
        /// </summary>
        /// <param name="vector">The displacement in full samples.</param>
        /// <returns>The rate cost, which is never negative.</returns>
        private int GetSadRateCost(Point vector)
        {
            int rate = this.costs.GetCost(new Av1MotionVector(vector.Y * 8, vector.X * 8), this.integerReferenceVector);
            return Av1RateDistortion.GetMotionSearchSadCost(this.sadPerBit, rate, 0);
        }

        /// <summary>
        /// Measures raw sample differences and truncates only after alternate-row scaling.
        /// </summary>
        /// <param name="vector">The displacement in full samples.</param>
        /// <param name="rowStep">One to measure every row, or two to measure alternate rows.</param>
        /// <param name="firstRow">The first measured row, zero or one.</param>
        /// <returns>The absolute-difference sum in the eight-bit error domain.</returns>
        private int GetSad(Point vector, int rowStep, int firstRow)
            => this.GetSad(this.referenceOrigin + (vector.Y * this.referenceStride) + vector.X, rowStep, firstRow);

        /// <summary>
        /// Measures the requested row parity at a retained reference offset.
        /// </summary>
        /// <param name="referenceIndex">The reference index of the candidate.</param>
        /// <param name="rowStep">One to measure every row, or two to measure alternate rows.</param>
        /// <param name="firstRow">The first measured row, zero or one.</param>
        /// <returns>The absolute-difference sum in the eight-bit error domain, truncated by the precision shift.</returns>
        private int GetSad(int referenceIndex, int rowStep, int firstRow)
        {
            referenceIndex += firstRow * this.referenceStride;
            int sad = this.secondPrediction.IsEmpty ? TOperator.SumAbsoluteDifferences(
                this.source[(firstRow * this.sourceStride)..],
                this.sourceStride,
                this.reference[referenceIndex..],
                this.referenceStride,
                this.blockSize.Width,
                this.blockSize.Height - firstRow,
                rowStep) : TOperator.SumCompoundAbsoluteDifferences(
                    this.source[(firstRow * this.sourceStride)..],
                    this.sourceStride,
                    this.reference[referenceIndex..],
                    this.referenceStride,
                    this.secondPrediction[(firstRow * this.blockSize.Width)..],
                    this.mask.IsEmpty ? this.mask : this.mask[(firstRow * this.blockSize.Width)..],
                    this.blockSize.Width,
                    this.blockSize.Height - firstRow,
                    rowStep);

            return sad >> this.precisionShift;
        }

        /// <summary>
        /// Retains normalized moments and subpixel-reference motion rate for a completed integer winner.
        /// </summary>
        /// <param name="vector">The integer-pixel displacement relative to the source block.</param>
        /// <returns>The normalized variance, squared error, and motion cost.</returns>
        public FullPixelResult GetVarianceResult(Point vector)
        {
            int referenceIndex = this.referenceOrigin + (vector.Y * this.referenceStride) + vector.X;
            int sum;
            long squares;
            if (this.secondPrediction.IsEmpty)
            {
                TOperator.GetMoments(
                    this.source,
                    this.sourceStride,
                    this.reference[referenceIndex..],
                    this.referenceStride,
                    this.blockSize.Width,
                    this.blockSize.Height,
                    out sum,
                    out squares);
            }
            else
            {
                TOperator.GetCompoundMoments(
                    this.source,
                    this.sourceStride,
                    this.reference[referenceIndex..],
                    this.referenceStride,
                    this.secondPrediction,
                    this.mask,
                    this.blockSize.Width,
                    this.blockSize.Height,
                    out sum,
                    out squares);

                sum = -sum;
            }

            if (this.precisionShift != 0)
            {
                // Signed sums and squared sums have different scales. The code rounds each one before it removes the mean.
                // Cancellation can make the rounded variance negative, so the code clamps the final variance to zero.
                sum = (sum + (1 << (this.precisionShift - 1))) >> this.precisionShift;
                int squaredShift = this.precisionShift * 2;
                squares = (squares + (1L << (squaredShift - 1))) >> squaredShift;
            }

            int variance = (int)Math.Max(squares - (((long)sum * sum) / (this.blockSize.Width * this.blockSize.Height)), 0);
            int rate = this.costs.GetCost(new Av1MotionVector(vector.Y * 8, vector.X * 8), this.referenceVector);
            int motionCost = Av1RateDistortion.GetMotionSearchCost(this.rateMultiplier, rate, 0);
            return new FullPixelResult(vector, variance, (int)squares, motionCost);
        }

        /// <summary>
        /// Measures the coding-pass error of a full-sample candidate for the shared diamond and mesh searches.
        /// It fixes the row step of the absolute differences for one search path.
        /// </summary>
        private readonly ref struct SadCost : IFullPixelCost
        {
            /// <summary>
            /// The search that owns the source, reference and rate state.
            /// </summary>
            private readonly FullPixelSearch<TSample, TOperator> search;

            /// <summary>
            /// One to measure every row, or two to measure alternate rows.
            /// </summary>
            private readonly int rowStep;

            /// <summary>
            /// Initializes a new instance of the <see cref="SadCost"/> struct.
            /// </summary>
            /// <param name="search">The search that owns the source, reference and rate state.</param>
            /// <param name="rowStep">One to measure every row, or two to measure alternate rows.</param>
            public SadCost(FullPixelSearch<TSample, TOperator> search, int rowStep)
            {
                this.search = search;
                this.rowStep = rowStep;
            }

            /// <summary>
            /// Gets the reference index of a candidate.
            /// </summary>
            /// <param name="vector">The candidate displacement in full samples.</param>
            /// <returns>The reference index of the displaced block origin.</returns>
            public int GetReferenceIndex(Point vector) => this.search.referenceOrigin + (vector.Y * this.search.referenceStride) + vector.X;

            /// <summary>
            /// Measures the single or compound absolute difference at the row step of this path.
            /// </summary>
            /// <param name="referenceIndex">The reference index of the displaced block origin.</param>
            /// <returns>The absolute-difference sum in the eight-bit error domain.</returns>
            public int GetSad(int referenceIndex) => this.search.GetSad(referenceIndex, this.rowStep, 0);

            /// <summary>
            /// Gets the motion-rate cost of a candidate in the absolute-difference domain.
            /// </summary>
            /// <param name="vector">The candidate displacement in full samples.</param>
            /// <returns>The rate cost, which is never negative.</returns>
            public int GetSadRateCost(Point vector) => this.search.GetSadRateCost(vector);

            /// <summary>
            /// Measures the variance and the motion cost of a finished search path. It always measures every row.
            /// </summary>
            /// <param name="vector">The winner of the path in full samples.</param>
            /// <returns>The normalized variance, squared error, and motion cost.</returns>
            public FullPixelResult GetVarianceResult(Point vector) => this.search.GetVarianceResult(vector);
        }
    }
}
