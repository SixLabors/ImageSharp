// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Motion.Av1MotionSearchSettings;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

internal static partial class Av1MotionSearchBase
{
    /// <summary>
    /// Converts a full-sample motion extent to the initial number of excluded search stages.
    /// </summary>
    /// <param name="size">The frame dimension or retained spatial motion magnitude.</param>
    /// <returns>The initial search-step parameter.</returns>
    public static int GetInitialStepParameter(int size)
    {
        size = Math.Max(size, 16);
        int step = 0;
        while ((size << step) < 1023)
        {
            step++;
        }

        return Math.Min(step, 9);
    }

    /// <summary>
    /// Collects weighted temporal starting vectors for one prediction block.
    /// </summary>
    /// <param name="spatialStart">The rounded spatial reference displacement in full samples.</param>
    /// <param name="temporalVectors">The temporal analysis vectors at the block's analysis-grid origin.</param>
    /// <param name="temporalStride">The analysis row stride in vectors.</param>
    /// <param name="analysisSize">The number of analysis columns and rows covered by the block.</param>
    /// <param name="candidates">Storage for the spatial start and every covered analysis block.</param>
    /// <param name="totalWeight">The represented weight, or zero when analysis is incomplete.</param>
    /// <returns>The number of collected starting candidates.</returns>
    public static int CollectStartingCandidates(
        Point spatialStart,
        ReadOnlySpan<Av1MotionVector> temporalVectors,
        int temporalStride,
        Size analysisSize,
        Span<StartingCandidate> candidates,
        out int totalWeight)
    {
        candidates[0] = new StartingCandidate(spatialStart, 0);
        totalWeight = 0;
        int count = 1;
        int analysisCount = analysisSize.Width * analysisSize.Height;
        if (analysisCount != 0)
        {
            // The spatial start receives one vote per analysis block before temporal votes are added.
            // It therefore remains among the first starts even when the temporal field is fragmented.
            candidates[0] = new StartingCandidate(spatialStart, analysisCount);
            for (int y = 0; y < analysisSize.Height; y++)
            {
                for (int x = 0; x < analysisSize.Width; x++)
                {
                    Av1MotionVector vector = temporalVectors[(y * temporalStride) + x];
                    if (vector.Row == short.MinValue && vector.Column == short.MinValue)
                    {
                        // Analysis may end partway through a block. Retain the collected prefix, but do not
                        // apply completed-field weighting or reorder it as if all temporal votes were available.
                        return count;
                    }

                    Point position = new(vector.Column >> 3, vector.Row >> 3);
                    int rowGroup = (position.Y + 3 + (position.Y >= 0 ? 1 : 0)) >> 3;
                    int columnGroup = (position.X + 3 + (position.X >= 0 ? 1 : 0)) >> 3;
                    int index = 0;
                    for (; index < count; index++)
                    {
                        Point existing = candidates[index].Vector;

                        // Temporal starts are grouped into rounded eight-sample cells after conversion to
                        // full samples. Keep the first representative position while accumulating its votes.
                        if (((existing.Y + 3 + (existing.Y >= 0 ? 1 : 0)) >> 3) == rowGroup
                            && ((existing.X + 3 + (existing.X >= 0 ? 1 : 0)) >> 3) == columnGroup)
                        {
                            candidates[index] = new StartingCandidate(existing, candidates[index].Weight + 1);
                            break;
                        }
                    }

                    if (index == count)
                    {
                        candidates[count++] = new StartingCandidate(position, 1);
                    }
                }
            }

            totalWeight = 2 * analysisCount;
            if (count > 2)
            {
                candidates[..count].Sort(default(StartingCandidateWeightComparer));
            }
        }

        return count;
    }

    /// <summary>
    /// Holds one weighted full-sample starting position from spatial or temporal analysis.
    /// </summary>
    public readonly struct StartingCandidate
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StartingCandidate"/> struct.
        /// </summary>
        /// <param name="vector">The starting displacement in full samples.</param>
        /// <param name="weight">The number of represented analysis blocks.</param>
        public StartingCandidate(Point vector, int weight)
        {
            this.Vector = vector;
            this.Weight = weight;
        }

        /// <summary>
        /// Gets the full-sample displacement.
        /// </summary>
        public Point Vector { get; }

        /// <summary>
        /// Gets the number of represented analysis blocks.
        /// </summary>
        public int Weight { get; }
    }

    /// <summary>
    /// Retains motion-search results and mode decisions for one differential-reference choice.
    /// </summary>
    public struct ReferenceSearchResult
    {
        public Av1MotionVector ReferenceVector;
        public Av1MotionVector FullVector;
        public Av1MotionVector Vector;
        public int FullRate;
        public int FullCost;
        public int Rate;
        public int DrlRate;
        public bool HasFullResult;
        public bool IsValid;
        public bool Skip;
    }

    /// <summary>
    /// Retains the six possible starts and three reference results across one block's new-motion modes.
    /// </summary>
    public struct SingleReferenceState
    {
        public InlineArray6<Point> Starts;
        public InlineArray6<byte> StartReferenceIndices;
        public InlineArray3<ReferenceSearchResult> References;
        public int StartCount;
    }

    /// <summary>
    /// Coordinates single-reference starting candidates, full-pixel search, fractional refinement, and winner estimation.
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample-specific prediction and error operations.</typeparam>
    public readonly ref struct SingleReferenceSearch<TSample, TOperator>
        where TSample : unmanaged
        where TOperator : struct, IMotionSearchOperator<TSample>
    {
        private readonly ReadOnlySpan<TSample> source;
        private readonly int sourceStride;
        private readonly ReadOnlySpan<TSample> reference;
        private readonly int referenceStride;
        private readonly int referenceOrigin;
        private readonly Av1BlockSize blockSize;
        private readonly Rectangle frameBounds;
        private readonly Av1EncoderBlockWorkspace workspace;
        private readonly Span<TSample> prediction;
        private readonly Span<short> residual;
        private readonly Span<short> convolutionScratch;
        private readonly Span<int> quantized;
        private readonly Av1SymbolEncoder writer;
        private readonly ReadOnlySpan<byte> aboveContexts;
        private readonly ReadOnlySpan<byte> leftContexts;
        private readonly Av1BitDepth bitDepth;
        private readonly int qIndex;
        private readonly int dcDeltaQ;
        private readonly int sharpness;
        private readonly bool lossless;
        private readonly int rateMultiplier;
        private readonly int transformSizeRate;
        private readonly int noSkipRate;
        private readonly int skipRate;
        private readonly Av1InterpolationFilter horizontalFilter;
        private readonly Av1InterpolationFilter verticalFilter;
        private readonly Av1MotionVectorCosts motionCosts;

        /// <summary>
        /// Initializes a new instance of the <see cref="SingleReferenceSearch{TSample, TOperator}"/> struct.
        /// </summary>
        /// <param name="source">The source samples at the prediction-block origin.</param>
        /// <param name="sourceStride">The source row stride.</param>
        /// <param name="reference">The complete bordered reference plane.</param>
        /// <param name="referenceStride">The reference row stride.</param>
        /// <param name="referenceOrigin">The reference origin corresponding to zero displacement.</param>
        /// <param name="blockSize">The containing prediction block size.</param>
        /// <param name="frameBounds">The full-sample frame search limits before differential-vector limits.</param>
        /// <param name="workspace">The worker transform and search-site storage.</param>
        /// <param name="prediction">The worker search prediction buffer, also reused for final predictions.</param>
        /// <param name="residual">The packed block residual destination.</param>
        /// <param name="convolutionScratch">The signed intermediate storage for final prediction.</param>
        /// <param name="quantized">The scratch quantized coefficients for one transform.</param>
        /// <param name="writer">The current tile probability state.</param>
        /// <param name="aboveContexts">The incoming top coefficient contexts.</param>
        /// <param name="leftContexts">The incoming left coefficient contexts.</param>
        /// <param name="bitDepth">The coded sample precision.</param>
        /// <param name="qIndex">The effective segment quantizer index.</param>
        /// <param name="dcDeltaQ">The luma DC quantizer adjustment.</param>
        /// <param name="sharpness">The quantization sharpness setting.</param>
        /// <param name="lossless">Whether the segment is coded losslessly.</param>
        /// <param name="rateMultiplier">The block rate-distortion multiplier.</param>
        /// <param name="transformSizeRate">The transform partition rate used by winner estimation.</param>
        /// <param name="noSkipRate">The rate of a non-skipped prediction block.</param>
        /// <param name="skipRate">The rate of a skipped prediction block.</param>
        /// <param name="horizontalFilter">The final horizontal interpolation family.</param>
        /// <param name="verticalFilter">The final vertical interpolation family.</param>
        /// <param name="motionCosts">The retained differential motion-rate table.</param>
        public SingleReferenceSearch(
            ReadOnlySpan<TSample> source,
            int sourceStride,
            ReadOnlySpan<TSample> reference,
            int referenceStride,
            int referenceOrigin,
            Av1BlockSize blockSize,
            Rectangle frameBounds,
            Av1EncoderBlockWorkspace workspace,
            Span<TSample> prediction,
            Span<short> residual,
            Span<short> convolutionScratch,
            Span<int> quantized,
            Av1SymbolEncoder writer,
            ReadOnlySpan<byte> aboveContexts,
            ReadOnlySpan<byte> leftContexts,
            Av1BitDepth bitDepth,
            int qIndex,
            int dcDeltaQ,
            int sharpness,
            bool lossless,
            int rateMultiplier,
            int transformSizeRate,
            int noSkipRate,
            int skipRate,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            Av1MotionVectorCosts motionCosts)
        {
            this.source = source;
            this.sourceStride = sourceStride;
            this.reference = reference;
            this.referenceStride = referenceStride;
            this.referenceOrigin = referenceOrigin;
            this.blockSize = blockSize;
            this.frameBounds = frameBounds;
            this.workspace = workspace;
            this.prediction = prediction;
            this.residual = residual;
            this.convolutionScratch = convolutionScratch;
            this.quantized = quantized;
            this.writer = writer;
            this.aboveContexts = aboveContexts;
            this.leftContexts = leftContexts;
            this.bitDepth = bitDepth;
            this.qIndex = qIndex;
            this.dcDeltaQ = dcDeltaQ;
            this.sharpness = sharpness;
            this.lossless = lossless;
            this.rateMultiplier = rateMultiplier;
            this.transformSizeRate = transformSizeRate;
            this.noSkipRate = noSkipRate;
            this.skipRate = skipRate;
            this.horizontalFilter = horizontalFilter;
            this.verticalFilter = verticalFilter;
            this.motionCosts = motionCosts;
        }

        /// <summary>
        /// Searches one differential-reference choice while retaining state for subsequent choices.
        /// </summary>
        /// <param name="settings">The resolved frame search policy.</param>
        /// <param name="frameStepParameter">The frame's initial number of excluded outer search stages.</param>
        /// <param name="spatialMagnitude">The largest full-sample magnitude in this reference's spatial context.</param>
        /// <param name="showFrame">Whether the current frame is presented.</param>
        /// <param name="searchRange">The optional range reduction, or the maximum integer for no reduction.</param>
        /// <param name="forceInteger">Whether the frame prohibits fractional motion vectors.</param>
        /// <param name="allowHighPrecision">Whether eighth-sample vectors are permitted.</param>
        /// <param name="fineMeshInterval">Whether content classification caps the first mesh interval.</param>
        /// <param name="referenceIndex">The current dynamic-reference index.</param>
        /// <param name="referenceVector">The differential coding reference in eighth-sample units.</param>
        /// <param name="drlRate">The syntax rate selecting this differential reference.</param>
        /// <param name="starts">Weighted starting positions in decreasing weight order.</param>
        /// <param name="totalWeight">The total represented weight before selecting the first two starts.</param>
        /// <param name="state">The block's retained results; initialize once before its first new-motion mode.</param>
        /// <param name="result">The selected displacement and prediction-error statistics.</param>
        /// <returns>Whether the search produced a valid candidate.</returns>
        public bool Search(
            Av1MotionSearchSettings settings,
            int frameStepParameter,
            int spatialMagnitude,
            bool showFrame,
            int searchRange,
            bool forceInteger,
            bool allowHighPrecision,
            bool fineMeshInterval,
            int referenceIndex,
            Av1MotionVector referenceVector,
            int drlRate,
            ReadOnlySpan<StartingCandidate> starts,
            int totalWeight,
            ref SingleReferenceState state,
            out FractionalResult result)
        {
            ref ReferenceSearchResult current = ref state.References[referenceIndex];
            current.ReferenceVector = referenceVector;
            current.DrlRate = drlRate;
            int stepParameter = frameStepParameter;
            if (settings.AutomaticStepSizeLevel != 0 && showFrame)
            {
                stepParameter = (GetInitialStepParameter(spatialMagnitude) + frameStepParameter) / 2;
            }

            // The frame may supply many temporal starts, but only its first two ranked candidates enter
            // this search. Record both before searching: the weight cutoff does not undo start history.
            int candidateCount = Math.Min(2, starts.Length);
            Span<bool> rejected = stackalloc bool[2];
            rejected.Clear();
            Point fullReference = new(
                (referenceVector.Column + 3 + (referenceVector.Column >= 0 ? 1 : 0)) >> 3,
                (referenceVector.Row + 3 + (referenceVector.Row >= 0 ? 1 : 0)) >> 3);

            if (settings.StartCandidatePruningLevel != 0)
            {
                for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
                {
                    Point start = starts[candidateIndex].Vector;
                    for (int historyIndex = 0; historyIndex < state.StartCount; historyIndex++)
                    {
                        int previousIndex = state.StartReferenceIndices[historyIndex];
                        ref ReferenceSearchResult previous = ref state.References[previousIndex];
                        if (!previous.IsValid && previousIndex != referenceIndex)
                        {
                            continue;
                        }

                        Point previousStart = state.Starts[historyIndex];
                        Av1MotionVector previousReference = previous.ReferenceVector;
                        int previousColumn = (previousReference.Column + 3 + (previousReference.Column >= 0 ? 1 : 0)) >> 3;
                        int previousRow = (previousReference.Row + 3 + (previousReference.Row >= 0 ? 1 : 0)) >> 3;
                        int startX = Math.Abs(start.X - previousStart.X);
                        int startY = Math.Abs(start.Y - previousStart.Y);
                        int referenceX = Math.Abs(fullReference.X - previousColumn);
                        int referenceY = Math.Abs(fullReference.Y - previousRow);
                        bool duplicates = settings.StartCandidatePruningLevel >= 2
                            ? startX <= 1 && startY <= 1 && referenceX <= 1 && referenceY <= 1
                            : startX + startY <= 1 && referenceX + referenceY <= 1;

                        if (duplicates)
                        {
                            rejected[candidateIndex] = true;
                            break;
                        }
                    }

                    if (!rejected[candidateIndex])
                    {
                        state.Starts[state.StartCount] = start;
                        state.StartReferenceIndices[state.StartCount++] = (byte)referenceIndex;
                    }
                }
            }

            FullPixelSearchMethod method = settings.GetFullPixelMethod(this.blockSize);
            Av1MotionSearchSites sites = this.workspace.GetMotionSearchSites(method, this.referenceStride);
            if (searchRange < int.MaxValue)
            {
                if (searchRange < 1)
                {
                    stepParameter = sites.StageCount;
                }
                else
                {
                    while (sites.StageCount - stepParameter - 1 > 0
                        && sites.GetRadius(sites.StageCount - stepParameter - 1) > (searchRange << 1))
                    {
                        stepParameter++;
                    }
                }
            }

            Size size = new(this.blockSize.GetWidth(), this.blockSize.GetHeight());
            FullPixelSearch<TSample, TOperator> fullSearch = new(
                this.source,
                this.sourceStride,
                this.reference,
                this.referenceStride,
                this.referenceOrigin,
                size,
                referenceVector.GetFullPixelSearchBounds(this.frameBounds),
                referenceVector,
                this.motionCosts,
                this.bitDepth,
                Av1RateDistortion.GetMotionSearchSadPerBit(this.qIndex, this.bitDepth),
                this.rateMultiplier);

            FullPixelResult best = default;
            Point? second = null;
            bool hasBest = false;
            int sumWeight = 0;
            for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                if (rejected[candidateIndex])
                {
                    continue;
                }

                // Non-realtime motion policy disables neighborhood publication. Fractional pruning therefore
                // measures its own candidates instead of fitting the optional five-cost integer surface.
                FullPixelResult candidate = fullSearch.Search(
                    starts[candidateIndex].Vector,
                    stepParameter,
                    method,
                    sites,
                    settings,
                    keyFrame: false,
                    fineMeshInterval,
                    Span<int>.Empty,
                    out Point? candidateSecond);

                if (candidate.Cost < (hasBest ? best.Cost : int.MaxValue))
                {
                    best = candidate;
                    second = candidateSecond;
                    hasBest = true;
                }

                sumWeight += starts[candidateIndex].Weight;
                if (4 * sumWeight > 3 * totalWeight)
                {
                    break;
                }
            }

            result = default;
            if (!hasBest)
            {
                return false;
            }

            Av1MotionVector integerVector = new(best.Vector.Y * 8, best.Vector.X * 8);
            int integerRate = ((this.motionCosts.GetCost(integerVector, referenceVector) * 108) + 64) >> 7;
            current.FullVector = integerVector;
            current.FullCost = best.Cost;
            current.FullRate = integerRate;
            current.HasFullResult = true;
            int pruningLevel = settings.ReferenceCandidatePruningLevel;
            if (pruningLevel >= 2)
            {
                for (int previousIndex = 0; previousIndex < referenceIndex; previousIndex++)
                {
                    ref ReferenceSearchResult previous = ref state.References[previousIndex];
                    if (!previous.HasFullResult)
                    {
                        continue;
                    }

                    if (previous.FullVector == integerVector && previous.FullRate + previous.DrlRate <= integerRate + drlRate)
                    {
                        return false;
                    }

                    // Level three permits a quarter more search error; level four compares the original
                    // error directly. This only prunes when the earlier reference also has cheaper selection syntax.
                    int threshold = pruningLevel == 3 ? previous.FullCost + (previous.FullCost >> 2) : previous.FullCost;
                    if (pruningLevel >= 3 && best.Cost > threshold && previous.DrlRate < drlRate)
                    {
                        return false;
                    }
                }
            }

            result = new FractionalResult(integerVector, best.Variance, best.SquaredError, best.MotionCost);
            if (!forceInteger && best.Cost < int.MaxValue)
            {
                Rectangle fractionalBounds = referenceVector.GetSubpixelSearchBounds(this.frameBounds);
                FractionalSearch<TSample, TOperator> fractionalSearch = new(
                    this.source,
                    this.sourceStride,
                    this.reference,
                    this.referenceStride,
                    this.referenceOrigin,
                    this.prediction,
                    size,
                    fractionalBounds,
                    referenceVector,
                    this.motionCosts,
                    this.bitDepth,
                    this.rateMultiplier);

                Span<Av1MotionVector> centers = stackalloc Av1MotionVector[3];
                centers.Fill(new Av1MotionVector(short.MinValue, short.MinValue));
                int firstCost = fractionalSearch.Search(
                    integerVector,
                    best,
                    settings.FractionalMethod,
                    SearchPrecision.EighthSample,
                    allowHighPrecision,
                    settings.FractionalIterationsPerStep,
                    settings.FractionalInterpolationTaps,
                    ReadOnlySpan<int>.Empty,
                    centers,
                    out result);

                if (second.HasValue && second.Value != best.Vector && settings.SecondCandidateSelection <= CandidateSelection.Variance)
                {
                    Point secondPoint = second.Value;
                    Av1MotionVector secondStart = new(secondPoint.Y * 8, secondPoint.X * 8);
                    if (fractionalBounds.Contains(secondStart.Column, secondStart.Row))
                    {
                        int secondCost = fractionalSearch.Search(
                            secondStart,
                            null,
                            settings.FractionalMethod,
                            SearchPrecision.EighthSample,
                            allowHighPrecision,
                            settings.FractionalIterationsPerStep,
                            settings.FractionalInterpolationTaps,
                            ReadOnlySpan<int>.Empty,
                            centers,
                            out FractionalResult secondResult);

                        if (settings.SecondCandidateSelection == CandidateSelection.RateDistortion && secondCost != int.MaxValue)
                        {
                            long firstRateDistortion = this.EstimateCandidate(result.Vector, referenceVector);
                            long secondRateDistortion = this.EstimateCandidate(secondResult.Vector, referenceVector);
                            if (secondRateDistortion < firstRateDistortion)
                            {
                                result = secondResult;
                            }
                        }
                        else if (secondCost < firstCost)
                        {
                            result = secondResult;
                        }
                    }
                }

                if (pruningLevel >= 1)
                {
                    int fractionalRate = ((this.motionCosts.GetCost(result.Vector, referenceVector) * 108) + 64) >> 7;
                    for (int previousIndex = 0; previousIndex < referenceIndex; previousIndex++)
                    {
                        ref ReferenceSearchResult previous = ref state.References[previousIndex];
                        if (!previous.IsValid || previous.Vector != result.Vector)
                        {
                            continue;
                        }

                        // A previously skipped matching mode remains skipped regardless of rate. Otherwise,
                        // preserve the earlier mode whenever its motion-plus-reference syntax is no more expensive.
                        if (previous.Skip || previous.Rate + previous.DrlRate <= fractionalRate + drlRate)
                        {
                            current.Skip = true;
                            break;
                        }
                    }
                }
            }

            // Weight only the motion-vector syntax. The transform and differential-reference rates retain
            // their own 1/512-bit units; applying this factor to their sum would change the mode decision.
            current.Rate = ((this.motionCosts.GetCost(result.Vector, referenceVector) * 108) + 64) >> 7;
            current.Vector = result.Vector;
            current.IsValid = true;
            return true;
        }

        /// <summary>
        /// Compares a refined vector using final prediction, transform rate, and differential motion rate.
        /// </summary>
        /// <param name="vector">The refined candidate vector.</param>
        /// <param name="referenceVector">The differential coding reference.</param>
        /// <returns>The rate-distortion estimate excluding the block skip-header cost.</returns>
        private long EstimateCandidate(Av1MotionVector vector, Av1MotionVector referenceVector)
        {
            int width = this.blockSize.GetWidth();
            int height = this.blockSize.GetHeight();
            int origin = this.referenceOrigin + ((vector.Row >> 3) * this.referenceStride) + (vector.Column >> 3);
            TOperator.PreparePrediction(
                this.source,
                this.sourceStride,
                this.reference,
                this.referenceStride,
                origin,
                this.prediction,
                this.residual,
                this.convolutionScratch,
                width,
                height,
                this.horizontalFilter,
                this.verticalFilter,
                (vector.Column & 7) << 1,
                (vector.Row & 7) << 1,
                this.bitDepth.GetBitCount());

            Av1TransformBlockEncoder.EstimateInterTransform(
                this.workspace,
                this.residual,
                width,
                this.quantized,
                this.writer,
                this.aboveContexts,
                this.leftContexts,
                this.blockSize,
                new Size(width, height),
                this.blockSize.GetMaximumTransformSize(),
                this.qIndex,
                this.dcDeltaQ,
                this.bitDepth,
                this.sharpness,
                this.lossless,
                this.rateMultiplier,
                this.transformSizeRate,
                this.noSkipRate,
                this.skipRate,
                long.MaxValue,
                out Av1RateDistortionStatistics statistics,
                out _,
                out _);

            int motionRate = ((this.motionCosts.GetCost(vector, referenceVector) * 108) + 64) >> 7;
            return Av1RateDistortion.GetCost(this.rateMultiplier, statistics.Rate + motionRate, statistics.Distortion);
        }
    }

    /// <summary>
    /// Orders temporal starts by descending represented analysis weight.
    /// </summary>
    private readonly struct StartingCandidateWeightComparer : IComparer<StartingCandidate>
    {
        /// <inheritdoc/>
        public int Compare(StartingCandidate x, StartingCandidate y) => y.Weight.CompareTo(x.Weight);
    }
}
