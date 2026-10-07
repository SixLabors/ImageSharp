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
    /// Adds the temporal dependency vectors of the 16x16 blocks that a prediction block covers to its starting
    /// candidates, after the spatial start at index zero. Each vector rounds to full samples, and vectors whose full
    /// samples round to the same eight-sample cell merge into the first of them, which counts their votes. The
    /// spatial start weighs as much as every covered block together, so it stays among the tested starts. When every
    /// covered block has a vector, the candidates are ordered by decreasing weight and the total weight is twice the
    /// covered block count; at the first block without a vector the collected prefix is kept unordered and the total
    /// stays zero. Reference: get_mv_candidate_from_tpl().
    /// </summary>
    /// <param name="superblockVectors">
    /// The temporal dependency vectors of the superblock, seven per 16x16 block, one for each reference LAST to ALTREF.
    /// Reference: sb_enc->tpl_mv.
    /// </param>
    /// <param name="superblockBlockCount">
    /// The number of superblock blocks inside the frame, zero without statistics. Reference: sb_enc->tpl_data_count.
    /// </param>
    /// <param name="superblockStride">The number of blocks per superblock row. Reference: sb_enc->tpl_stride.</param>
    /// <param name="superblockModeInfoSize">The superblock size in mode-information units.</param>
    /// <param name="modeInfoPosition">The prediction block position in mode-information units.</param>
    /// <param name="blockSize">The prediction block size.</param>
    /// <param name="referenceIndex">The searched reference, zero for LAST.</param>
    /// <param name="candidates">Holds the spatial start at index zero, and receives the collected candidates.</param>
    /// <param name="totalWeight">Receives the total weight, or zero when the candidates are not ordered.</param>
    /// <returns>The number of starting candidates, at least one.</returns>
    public static int CollectStartingCandidates(
        ReadOnlySpan<Av1MotionVector> superblockVectors,
        int superblockBlockCount,
        int superblockStride,
        int superblockModeInfoSize,
        Point modeInfoPosition,
        Av1BlockSize blockSize,
        int referenceIndex,
        Span<StartingCandidate> candidates,
        out int totalWeight)
    {
        totalWeight = 0;
        int count = 1;
        if (superblockBlockCount == 0)
        {
            return count;
        }

        // A 16x16 model block spans four mode-information units. A prediction block narrower or shorter than one
        // covers no whole model block and adds nothing.
        const int ModelModeInfo = 4;
        int wide = blockSize.Get4x4WideCount() / ModelModeInfo;
        int high = blockSize.Get4x4HighCount() / ModelModeInfo;
        if (wide < 1 || high < 1)
        {
            return count;
        }

        int offsetRow = modeInfoPosition.Y % superblockModeInfoSize;
        int offsetColumn = modeInfoPosition.X % superblockModeInfoSize;
        int start = ((offsetRow / ModelModeInfo) * superblockStride) + (offsetColumn / ModelModeInfo);
        candidates[0] = new StartingCandidate(candidates[0].Vector, wide * high);
        const int References = 7;
        for (int k = 0; k < high; k++)
        {
            for (int l = 0; l < wide; l++)
            {
                Av1MotionVector vector = superblockVectors[((start + (k * superblockStride) + l) * References) + referenceIndex];
                if (vector.Row == short.MinValue && vector.Column == short.MinValue)
                {
                    // An unsearched reference or a block outside the frame ends the collection. Reference: the
                    // INVALID_MV test that clears valid.
                    return count;
                }

                // GET_MV_RAWPEL() rounds the eighth-sample vector to full samples, and RIGHT_SHIFT_MV() applies the
                // same rounding to group full samples into eight-sample cells.
                Point position = new(RoundToFullSample(vector.Column), RoundToFullSample(vector.Row));
                int rowCell = RoundToFullSample(position.Y);
                int columnCell = RoundToFullSample(position.X);
                int index = 0;
                for (; index < count; index++)
                {
                    Point existing = candidates[index].Vector;
                    if (RoundToFullSample(existing.Y) == rowCell && RoundToFullSample(existing.X) == columnCell)
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

        totalWeight = 2 * high * wide;
        if (count > 2)
        {
            SortByDecreasingWeight(candidates[..count]);
        }

        return count;
    }

    /// <summary>
    /// Rounds an eighth-sample value to the nearest full sample, halves away from zero. Reference: GET_MV_RAWPEL()
    /// and RIGHT_SHIFT_MV().
    /// </summary>
    /// <param name="value">The value in eighth samples.</param>
    /// <returns>The value in full samples.</returns>
    private static int RoundToFullSample(int value) => (value + 3 + (value >= 0 ? 1 : 0)) >> 3;

#pragma warning disable CA1517 // False positive: https://github.com/dotnet/sdk/issues/53388
    /// <summary>
    /// Orders starting candidates by decreasing weight with the C library qsort of the x64 reference build, which links
    /// the Microsoft C runtime; its order of equal weights is the order this reproduces. A range of up to eight entries
    /// is sorted by moving its first largest entry, in comparator order, to its end. A longer range is partitioned
    /// around the median of its first, middle and last entries into entries ordered no later than the partition entry,
    /// entries equal to it, and entries ordered after it; the smaller part is sorted first and the larger is kept on
    /// an explicit stack. Reference: qsort() with compare_weight().
    /// </summary>
    /// <param name="candidates">The candidates, sorted in place.</param>
    private static void SortByDecreasingWeight(Span<StartingCandidate> candidates)
    {
        // The explicit stack holds at most log2 of the length entries; the runtime sizes it for any 64-bit length.
        const int StackSize = (8 * 8) - 2;
        const int Cutoff = 8;
        Span<int> lowStack = stackalloc int[StackSize];
        Span<int> highStack = stackalloc int[StackSize];
        int stackPointer = 0;
        int low = 0;
        int high = candidates.Length - 1;
        while (true)
        {
            int size = high - low + 1;
            if (size <= Cutoff)
            {
                // shortsort(): the first entry that compares greatest moves to the end of the shrinking range.
                for (int end = high; end > low; end--)
                {
                    int greatest = low;
                    for (int index = low + 1; index <= end; index++)
                    {
                        if (CompareWeight(candidates[index], candidates[greatest]) > 0)
                        {
                            greatest = index;
                        }
                    }

                    (candidates[greatest], candidates[end]) = (candidates[end], candidates[greatest]);
                }
            }
            else
            {
                // The median of three moves into the middle.
                int middle = low + (size / 2);
                if (CompareWeight(candidates[low], candidates[middle]) > 0)
                {
                    (candidates[low], candidates[middle]) = (candidates[middle], candidates[low]);
                }

                if (CompareWeight(candidates[low], candidates[high]) > 0)
                {
                    (candidates[low], candidates[high]) = (candidates[high], candidates[low]);
                }

                if (CompareWeight(candidates[middle], candidates[high]) > 0)
                {
                    (candidates[middle], candidates[high]) = (candidates[high], candidates[middle]);
                }

                // The two scans exchange entries on the wrong side of the partition entry, which the exchange may
                // move. Each scan is split in two so that the partition entry is never compared with itself.
                int lowScan = low;
                int highScan = high;
                while (true)
                {
                    if (middle > lowScan)
                    {
                        do
                        {
                            lowScan++;
                        }
                        while (lowScan < middle && CompareWeight(candidates[lowScan], candidates[middle]) <= 0);
                    }

                    if (middle <= lowScan)
                    {
                        do
                        {
                            lowScan++;
                        }
                        while (lowScan <= high && CompareWeight(candidates[lowScan], candidates[middle]) <= 0);
                    }

                    do
                    {
                        highScan--;
                    }
                    while (highScan > middle && CompareWeight(candidates[highScan], candidates[middle]) > 0);

                    if (highScan < lowScan)
                    {
                        break;
                    }

                    (candidates[lowScan], candidates[highScan]) = (candidates[highScan], candidates[lowScan]);
                    if (middle == highScan)
                    {
                        middle = lowScan;
                    }
                }

                // Entries equal to the partition entry that sit next to it join neither part.
                highScan++;
                if (middle < highScan)
                {
                    do
                    {
                        highScan--;
                    }
                    while (highScan > middle && CompareWeight(candidates[highScan], candidates[middle]) == 0);
                }

                if (middle >= highScan)
                {
                    do
                    {
                        highScan--;
                    }
                    while (highScan > low && CompareWeight(candidates[highScan], candidates[middle]) == 0);
                }

                if (highScan - low >= high - lowScan)
                {
                    if (low < highScan)
                    {
                        lowStack[stackPointer] = low;
                        highStack[stackPointer++] = highScan;
                    }

                    if (lowScan < high)
                    {
                        low = lowScan;
                        continue;
                    }
                }
                else
                {
                    if (lowScan < high)
                    {
                        lowStack[stackPointer] = lowScan;
                        highStack[stackPointer++] = high;
                    }

                    if (low < highScan)
                    {
                        high = highScan;
                        continue;
                    }
                }
            }

            if (--stackPointer < 0)
            {
                return;
            }

            low = lowStack[stackPointer];
            high = highStack[stackPointer];
        }
    }
#pragma warning restore CA1517

    /// <summary>
    /// Compares two starting candidates so that a heavier candidate orders first. Reference: compare_weight().
    /// </summary>
    /// <param name="left">The first candidate.</param>
    /// <param name="right">The second candidate.</param>
    /// <returns>A positive value when <paramref name="left"/> is lighter, negative when heavier, otherwise zero.</returns>
    private static int CompareWeight(StartingCandidate left, StartingCandidate right)
        => left.Weight < right.Weight ? 1 : left.Weight > right.Weight ? -1 : 0;

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
        private readonly Point blockOrigin;
        private readonly Rectangle frameBounds;
        private readonly Size visibleFrameSize;
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
        private readonly Av1MotionVectorCosts motionCosts;

        /// <summary>
        /// The reference of another size from which the rate-distortion search predicts its fractional candidates, or
        /// the default value for a reference of the frame size.
        /// </summary>
        private readonly ScaledReference<TSample> scaledReference;

        /// <summary>
        /// Initializes a new instance of the <see cref="SingleReferenceSearch{TSample, TOperator}"/> struct.
        /// </summary>
        /// <param name="source">The source samples at the prediction-block origin.</param>
        /// <param name="sourceStride">The source row stride.</param>
        /// <param name="reference">The complete bordered reference plane.</param>
        /// <param name="referenceStride">The reference row stride.</param>
        /// <param name="referenceOrigin">The reference origin corresponding to zero displacement.</param>
        /// <param name="blockSize">The containing prediction block size.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="frameBounds">The full-sample frame search limits before differential-vector limits.</param>
        /// <param name="visibleFrameSize">The visible frame size, which bounds the search at sharpness 3.</param>
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
        /// <param name="sharpness">
        /// The encoder sharpness, which sets the quantizer rounding and, at 3, keeps the search near the frame.
        /// </param>
        /// <param name="lossless">Whether the segment is coded losslessly.</param>
        /// <param name="rateMultiplier">The block rate-distortion multiplier.</param>
        /// <param name="transformSizeRate">The transform partition rate used by winner estimation.</param>
        /// <param name="noSkipRate">The rate of a non-skipped prediction block.</param>
        /// <param name="skipRate">The rate of a skipped prediction block.</param>
        /// <param name="motionCosts">The retained differential motion-rate table.</param>
        /// <param name="scaledReference">
        /// The reference of another size from which the rate-distortion search predicts its fractional candidates, or
        /// the default value for a reference of the frame size.
        /// </param>
        public SingleReferenceSearch(
            ReadOnlySpan<TSample> source,
            int sourceStride,
            ReadOnlySpan<TSample> reference,
            int referenceStride,
            int referenceOrigin,
            Av1BlockSize blockSize,
            Point blockOrigin,
            Rectangle frameBounds,
            Size visibleFrameSize,
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
            Av1MotionVectorCosts motionCosts,
            ScaledReference<TSample> scaledReference = default)
        {
            this.scaledReference = scaledReference;
            this.source = source;
            this.sourceStride = sourceStride;
            this.reference = reference;
            this.referenceStride = referenceStride;
            this.referenceOrigin = referenceOrigin;
            this.blockSize = blockSize;
            this.blockOrigin = blockOrigin;
            this.frameBounds = frameBounds;
            this.visibleFrameSize = visibleFrameSize;
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
            this.motionCosts = motionCosts;
        }

        /// <summary>
        /// Returns the full-pixel search range around a reference vector. Reference: av1_set_mv_search_range() and
        /// the sharpness margins of av1_make_default_fullpel_ms_params().
        /// </summary>
        /// <param name="referenceVector">The differential coding predictor.</param>
        /// <returns>The range, with exclusive right and bottom edges.</returns>
        private Rectangle GetFullPixelBounds(Av1MotionVector referenceVector)
        {
            Rectangle bounds = referenceVector.GetFullPixelSearchBounds(this.frameBounds);
            return this.sharpness == 3
                ? Av1MotionVector.ClampToSharpnessMargins(bounds, this.blockOrigin, this.GetBlockDimensions(), this.visibleFrameSize, 1)
                : bounds;
        }

        /// <summary>
        /// Returns the eighth-sample search range around a reference vector. Reference:
        /// av1_set_subpel_mv_search_range() and the sharpness margins of av1_make_default_subpel_ms_params().
        /// </summary>
        /// <param name="referenceVector">The differential coding predictor.</param>
        /// <returns>The range, with exclusive right and bottom edges.</returns>
        private Rectangle GetSubpixelBounds(Av1MotionVector referenceVector)
        {
            Rectangle bounds = referenceVector.GetSubpixelSearchBounds(this.frameBounds);
            return this.sharpness == 3
                ? Av1MotionVector.ClampToSharpnessMargins(
                    bounds, this.blockOrigin, this.GetBlockDimensions(), this.visibleFrameSize, Av1MotionVector.SubpixelScale)
                : bounds;
        }

        /// <summary>
        /// Returns the luma size of the searched block.
        /// </summary>
        /// <returns>The block width and height.</returns>
        private Size GetBlockDimensions() => new(this.blockSize.GetWidth(), this.blockSize.GetHeight());

        /// <summary>
        /// Searches a new-motion candidate using prediction error and motion rate.
        /// </summary>
        /// <param name="settings">The resolved motion-search policy.</param>
        /// <param name="frameStepParameter">The initial number of excluded outer search stages.</param>
        /// <param name="referenceVector">The differential coding predictor.</param>
        /// <param name="forceInteger">Whether fractional motion is prohibited.</param>
        /// <param name="allowHighPrecision">Whether eighth-sample motion is permitted.</param>
        /// <param name="frameLowMotion">The preceding frame's percentage of low-motion blocks.</param>
        /// <param name="sourceSad">The source-change classification.</param>
        /// <param name="sourceVariance">The normalized source variance.</param>
        /// <param name="bestCost">The best complete mode cost found so far.</param>
        /// <param name="motionRate">The selected vector's coding rate.</param>
        /// <param name="result">The selected motion and prediction-error statistics.</param>
        /// <returns>Whether the selected vector differs from its coding predictor.</returns>
        public bool SearchEstimated(
            Av1MotionSearchSettings settings,
            int frameStepParameter,
            Av1MotionVector referenceVector,
            bool forceInteger,
            bool allowHighPrecision,
            int frameLowMotion,
            Av1SourceSadLevel sourceSad,
            uint sourceVariance,
            long bestCost,
            out int motionRate,
            out FractionalResult result)
        {
            Size size = new(this.blockSize.GetWidth(), this.blockSize.GetHeight());

            // combined_motion_search() starts at get_fullmv_from_mv(), which rounds to the nearest full sample.
            Point start = new(
                (referenceVector.Column + 3 + (referenceVector.Column >= 0 ? 1 : 0)) >> 3,
                (referenceVector.Row + 3 + (referenceVector.Row >= 0 ? 1 : 0)) >> 3);

            FullPixelSearchMethod method = settings.GetEstimatedFullPixelMethod(this.blockSize, sourceSad);
            FullPixelSearch<TSample, TOperator> fullSearch = new(
                this.source,
                this.sourceStride,
                this.reference,
                this.referenceStride,
                this.referenceOrigin,
                size,
                this.GetFullPixelBounds(referenceVector),
                referenceVector,
                this.motionCosts,
                this.bitDepth,
                Av1RateDistortion.GetMotionSearchSadPerBit(this.qIndex, this.bitDepth),
                this.rateMultiplier,
                [],
                []);

            // These five costs describe the integer winner and its cardinal neighbors. Fractional
            // pruning uses the same neighborhood, so retain it across both search stages.
            Span<int> costs = stackalloc int[5];
            FullPixelResult integerResult = fullSearch.Search(
                start,
                frameStepParameter,
                method,
                this.workspace.GetMotionSearchSites(method, this.referenceStride),
                settings,
                false,
                false,
                false,
                costs,
                out _);

            Av1MotionVector vector = new(integerResult.Vector.Y * 8, integerResult.Vector.X * 8);
            motionRate = ((this.motionCosts.GetCost(vector, referenceVector) * 108) + 64) >> 7;
            result = new FractionalResult(vector, integerResult.Variance, integerResult.SquaredError, integerResult.MotionCost);
            if (Av1RateDistortion.GetCost(this.rateMultiplier, motionRate, 0) > bestCost)
            {
                // combined_motion_search() skips the fractional search here and leaves tmp_mv holding the
                // full-sample vector, which the union then reads as an eighth-sample vector. The rate stays the
                // rate of the full-sample vector in eighth samples.
                result = new FractionalResult(
                    new Av1MotionVector(integerResult.Vector.Y, integerResult.Vector.X),
                    integerResult.Variance,
                    integerResult.SquaredError,
                    integerResult.MotionCost);
            }
            else if (!forceInteger)
            {
                bool fullPixelPerformedWell = (this.blockSize == Av1BlockSize.Block64x64 && unchecked((uint)integerResult.Cost * 40U) < 62267 * 7)
                    || (this.blockSize == Av1BlockSize.Block32x32 && unchecked((uint)integerResult.Cost * 8U) < 42380)
                    || (this.blockSize == Av1BlockSize.Block16x16 && unchecked((uint)integerResult.Cost * 8U) < 10127);

                SearchPrecision precision = settings.GetEstimatedFractionalPrecision(
                    this.blockSize,
                    integerResult.Vector,
                    referenceVector,
                    start,
                    frameLowMotion,
                    sourceSad,
                    sourceVariance,
                    fullPixelPerformedWell);

                FractionalSearch<TSample, TOperator> fractionalSearch = new(
                    this.source,
                    this.sourceStride,
                    this.reference,
                    this.referenceStride,
                    this.referenceOrigin,
                    this.prediction,
                    size,
                    this.GetSubpixelBounds(referenceVector),
                    referenceVector,
                    this.motionCosts,
                    this.bitDepth,
                    this.rateMultiplier,
                    [],
                    []);

                fractionalSearch.Search(
                    vector,
                    integerResult,
                    settings.GetEstimatedFractionalMethod(sourceSad, sourceVariance, fullPixelPerformedWell),
                    precision,
                    allowHighPrecision,
                    settings.FractionalIterationsPerStep,
                    settings.FractionalInterpolationTaps,
                    costs,
                    [],
                    out result);

                motionRate = ((this.motionCosts.GetCost(result.Vector, referenceVector) * 108) + 64) >> 7;
            }

            return result.Vector != referenceVector;
        }

        /// <summary>
        /// Refines a full-sample projection estimate to fractional precision. Reference: the constant-bitrate
        /// branch of search_new_mv(), which runs find_fractional_mv_step() from the av1_int_pro_motion_estimation()
        /// result without start statistics or a cost list, and stops at the precision that subpel_select() gives
        /// for a zero start.
        /// </summary>
        /// <param name="settings">The resolved frame search policy.</param>
        /// <param name="integerVector">The projection result in full samples.</param>
        /// <param name="referenceVector">The differential coding reference in eighth-sample units.</param>
        /// <param name="allowHighPrecision">Whether eighth-sample vectors are permitted.</param>
        /// <param name="frameLowMotion">The running zero-motion percentage of the frame.</param>
        /// <param name="sourceSad">The superblock's source-change classification.</param>
        /// <param name="sourceVariance">The normalized source variance.</param>
        /// <param name="motionRate">The weighted vector rate.</param>
        /// <param name="result">The refined vector and its prediction-error statistics.</param>
        /// <returns><see langword="true"/> when the refined vector differs from the reference vector.</returns>
        public bool RefineProjection(
            Av1MotionSearchSettings settings,
            Point integerVector,
            Av1MotionVector referenceVector,
            bool allowHighPrecision,
            int frameLowMotion,
            Av1SourceSadLevel sourceSad,
            uint sourceVariance,
            out int motionRate,
            out FractionalResult result)
        {
            Size size = new(this.blockSize.GetWidth(), this.blockSize.GetHeight());
            SearchPrecision precision = settings.GetEstimatedFractionalPrecision(
                this.blockSize,
                integerVector,
                referenceVector,
                Point.Empty,
                frameLowMotion,
                sourceSad,
                sourceVariance,
                false);

            FractionalSearch<TSample, TOperator> fractionalSearch = new(
                this.source,
                this.sourceStride,
                this.reference,
                this.referenceStride,
                this.referenceOrigin,
                this.prediction,
                size,
                this.GetSubpixelBounds(referenceVector),
                referenceVector,
                this.motionCosts,
                this.bitDepth,
                this.rateMultiplier,
                [],
                []);

            fractionalSearch.Search(
                new Av1MotionVector(integerVector.Y * 8, integerVector.X * 8),
                null,
                settings.FractionalMethod,
                precision,
                allowHighPrecision,
                settings.FractionalIterationsPerStep,
                settings.FractionalInterpolationTaps,
                [],
                [],
                out result);

            motionRate = ((this.motionCosts.GetCost(result.Vector, referenceVector) * 108) + 64) >> 7;
            return result.Vector != referenceVector;
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
        /// <param name="horizontalFilter">The horizontal interpolation family that the block holds during the search.</param>
        /// <param name="verticalFilter">The vertical interpolation family that the block holds during the search.</param>
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
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
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
                this.GetFullPixelBounds(referenceVector),
                referenceVector,
                this.motionCosts,
                this.bitDepth,
                Av1RateDistortion.GetMotionSearchSadPerBit(this.qIndex, this.bitDepth),
                this.rateMultiplier,
                [],
                []);

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
                    intraBlockCopy: false,
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
                Rectangle fractionalBounds = this.GetSubpixelBounds(referenceVector);
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
                    this.rateMultiplier,
                    [],
                    [],
                    this.scaledReference);

                Span<Av1MotionVector> centers = stackalloc Av1MotionVector[3];
                centers.Fill(new Av1MotionVector(short.MinValue, short.MinValue));
                int firstCost = fractionalSearch.Search(
                    integerVector,
                    best,
                    settings.FractionalMethod,
                    settings.FractionalPrecision,
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
                            settings.FractionalPrecision,
                            allowHighPrecision,
                            settings.FractionalIterationsPerStep,
                            settings.FractionalInterpolationTaps,
                            ReadOnlySpan<int>.Empty,
                            centers,
                            out FractionalResult secondResult);

                        if (settings.SecondCandidateSelection == CandidateSelection.RateDistortion && secondCost != int.MaxValue)
                        {
                            long firstRateDistortion = this.EstimateCandidate(
                                result.Vector, referenceVector, horizontalFilter, verticalFilter);

                            long secondRateDistortion = this.EstimateCandidate(
                                secondResult.Vector, referenceVector, horizontalFilter, verticalFilter);

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
        /// <param name="horizontalFilter">The horizontal interpolation family of the prediction.</param>
        /// <param name="verticalFilter">The vertical interpolation family of the prediction.</param>
        /// <returns>The rate-distortion estimate excluding the block skip-header cost.</returns>
        private long EstimateCandidate(
            Av1MotionVector vector,
            Av1MotionVector referenceVector,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter)
        {
            int width = this.blockSize.GetWidth();
            int height = this.blockSize.GetHeight();
            if (this.scaledReference.IsScaled)
            {
                // A scaled reference predicts from the reference itself. Reference: the av1_enc_build_inter_predictor()
                // calls of av1_single_motion_search() after the buffers are swapped back.
                this.scaledReference.Predict<TOperator>(
                    vector, this.prediction, new Size(width, height), horizontalFilter, verticalFilter, this.bitDepth.GetBitCount());

                TOperator.Subtract(this.source, this.sourceStride, this.prediction, this.residual, width, height);
            }
            else
            {
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
                    horizontalFilter,
                    verticalFilter,
                    (vector.Column & 7) << 1,
                    (vector.Row & 7) << 1,
                    this.bitDepth.GetBitCount());
            }

            // A block crossing the frame edge is subtracted with the DCT_DCT border padding. Reference: the
            // av1_subtract_txb() call of av1_estimate_txfm_yrd().
            Av1TransformBlockEncoder.PadBorderResidual(
                this.workspace, Av1Plane.Y, this.blockOrigin, this.residual, width, width, height, Av1TransformType.DctDct);

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
}
