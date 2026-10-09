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
    /// Adds the temporal dependency vectors of the 16x16 blocks that a prediction block covers to its starting candidates.
    /// The spatial start stays at index zero.
    /// Each vector rounds to full samples. A vector in the same eight-sample cell as an earlier candidate merges into that candidate, which counts the votes.
    /// The spatial start weighs as much as all covered blocks together, so it stays among the tested starts.
    /// When every covered block has a vector, the method sorts the candidates by decreasing weight. The total weight is then twice the covered block count.
    /// At the first block without a vector, the collected prefix stays unsorted and the total stays zero.
    /// </summary>
    /// <param name="superblockVectors">
    /// The temporal dependency vectors of the superblock, seven per 16x16 block, one for each reference LAST to ALTREF.
    /// </param>
    /// <param name="superblockBlockCount">The number of superblock blocks inside the frame, zero without statistics.</param>
    /// <param name="superblockStride">The number of blocks per superblock row.</param>
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

        // A 16x16 model block spans four mode-information units.
        // A prediction block that is narrower or shorter than one model block covers no whole model block and adds nothing.
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
                    // An unsearched reference or a block outside the frame holds the invalid vector. It ends the collection.
                    return count;
                }

                // The first rounding changes the eighth-sample vector to full samples.
                // The second rounding uses the same arithmetic to group full samples into eight-sample cells.
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
    /// Rounds an eighth-sample value to the nearest full sample. Halves round away from zero.
    /// </summary>
    /// <param name="value">The value in eighth samples.</param>
    /// <returns>The value in full samples.</returns>
    private static int RoundToFullSample(int value) => (value + 3 + (value >= 0 ? 1 : 0)) >> 3;

#pragma warning disable CA1517 // False positive: https://github.com/dotnet/sdk/issues/53388
    /// <summary>
    /// Sorts starting candidates by decreasing weight with the quicksort algorithm of the Microsoft C runtime.
    /// The sort is not stable. This algorithm gives equal weights the same order as x64 Windows builds of other AV1 encoders.
    /// The method sorts a range of up to eight entries by moving its first largest entry, in comparator order, to its end.
    /// It partitions a longer range around the median of its first, middle and last entries.
    /// The parts are the entries ordered no later than the partition entry, the entries equal to it, and the entries ordered after it.
    /// The method sorts the smaller part first and keeps the larger part on an explicit stack.
    /// </summary>
    /// <param name="candidates">The candidates, sorted in place.</param>
    private static void SortByDecreasingWeight(Span<StartingCandidate> candidates)
    {
        // The explicit stack holds at most log2 of the length entries. The size covers any 64-bit length, as in the C runtime.
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
                // The short sort moves the first entry that compares greatest to the end of the shrinking range.
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

                // The two scans exchange entries on the wrong side of the partition entry. The exchange can move the partition entry.
                // Each scan has two parts, so the partition entry is never compared with itself.
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

                // The smaller part runs next. The larger part waits on the stack.
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
    /// Compares two starting candidates so that a heavier candidate orders first.
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
        /// <summary>
        /// The differential coding reference in eighth-sample units.
        /// </summary>
        public Av1MotionVector ReferenceVector;

        /// <summary>
        /// The full-pixel winner in eighth-sample units.
        /// </summary>
        public Av1MotionVector FullVector;

        /// <summary>
        /// The final selected vector in eighth-sample units.
        /// </summary>
        public Av1MotionVector Vector;

        /// <summary>
        /// The weighted motion rate of the full-pixel winner.
        /// </summary>
        public int FullRate;

        /// <summary>
        /// The variance-plus-rate cost of the full-pixel winner.
        /// </summary>
        public int FullCost;

        /// <summary>
        /// The weighted motion rate of the final vector.
        /// </summary>
        public int Rate;

        /// <summary>
        /// The syntax rate that selects this differential reference.
        /// </summary>
        public int DrlRate;

        /// <summary>
        /// Whether the full-pixel fields hold a result.
        /// </summary>
        public bool HasFullResult;

        /// <summary>
        /// Whether the search produced a final vector for this choice.
        /// </summary>
        public bool IsValid;

        /// <summary>
        /// Whether an earlier choice with the same final vector makes this choice redundant.
        /// </summary>
        public bool Skip;
    }

    /// <summary>
    /// Retains the six possible starts and three reference results across one block's new-motion modes.
    /// </summary>
    public struct SingleReferenceState
    {
        /// <summary>
        /// The full-sample starts that the block searched so far, across its differential-reference choices.
        /// </summary>
        public InlineArray6<Point> Starts;

        /// <summary>
        /// The differential-reference index of each recorded start.
        /// </summary>
        public InlineArray6<byte> StartReferenceIndices;

        /// <summary>
        /// The result of each differential-reference choice.
        /// </summary>
        public InlineArray3<ReferenceSearchResult> References;

        /// <summary>
        /// The number of recorded starts.
        /// </summary>
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
        /// <summary>
        /// The source samples at the prediction-block origin.
        /// </summary>
        private readonly ReadOnlySpan<TSample> source;

        /// <summary>
        /// The source row stride.
        /// </summary>
        private readonly int sourceStride;

        /// <summary>
        /// The complete bordered reference plane.
        /// </summary>
        private readonly ReadOnlySpan<TSample> reference;

        /// <summary>
        /// The reference row stride.
        /// </summary>
        private readonly int referenceStride;

        /// <summary>
        /// The reference index of zero displacement.
        /// </summary>
        private readonly int referenceOrigin;

        /// <summary>
        /// The prediction block size.
        /// </summary>
        private readonly Av1BlockSize blockSize;

        /// <summary>
        /// The luma block origin.
        /// </summary>
        private readonly Point blockOrigin;

        /// <summary>
        /// The full-sample frame search limits before differential-vector limits.
        /// </summary>
        private readonly Rectangle frameBounds;

        /// <summary>
        /// The visible frame size, which bounds the search at sharpness 3.
        /// </summary>
        private readonly Size visibleFrameSize;

        /// <summary>
        /// The worker transform and search-site storage.
        /// </summary>
        private readonly Av1EncoderBlockWorkspace workspace;

        /// <summary>
        /// The worker search prediction buffer, also reused for final predictions.
        /// </summary>
        private readonly Span<TSample> prediction;

        /// <summary>
        /// The frame luma plane that gets each prediction of the winner estimate, or the default value when the search writes no frame.
        /// </summary>
        private readonly Av1PlaneRegion<TSample> frame;

        /// <summary>
        /// The packed block residual destination.
        /// </summary>
        private readonly Span<short> residual;

        /// <summary>
        /// The signed intermediate storage for final prediction.
        /// </summary>
        private readonly Span<short> convolutionStorage;

        /// <summary>
        /// The quantized coefficients of one transform.
        /// </summary>
        private readonly Span<int> quantized;

        /// <summary>
        /// The current tile probability state.
        /// </summary>
        private readonly Av1SymbolEncoder writer;

        /// <summary>
        /// The incoming top coefficient contexts.
        /// </summary>
        private readonly ReadOnlySpan<byte> aboveContexts;

        /// <summary>
        /// The incoming left coefficient contexts.
        /// </summary>
        private readonly ReadOnlySpan<byte> leftContexts;

        /// <summary>
        /// The coded sample precision.
        /// </summary>
        private readonly Av1BitDepth bitDepth;

        /// <summary>
        /// The effective segment quantizer index.
        /// </summary>
        private readonly int qIndex;

        /// <summary>
        /// The luma DC quantizer adjustment.
        /// </summary>
        private readonly int dcDeltaQ;

        /// <summary>
        /// The encoder sharpness, which sets the quantizer rounding and, at 3, keeps the search near the frame.
        /// </summary>
        private readonly int sharpness;

        /// <summary>
        /// Whether the segment is coded losslessly.
        /// </summary>
        private readonly bool lossless;

        /// <summary>
        /// The block rate-distortion multiplier.
        /// </summary>
        private readonly int rateMultiplier;

        /// <summary>
        /// The transform partition rate used by winner estimation.
        /// </summary>
        private readonly int transformSizeRate;

        /// <summary>
        /// The rate of a non-skipped prediction block.
        /// </summary>
        private readonly int noSkipRate;

        /// <summary>
        /// The rate of a skipped prediction block.
        /// </summary>
        private readonly int skipRate;

        /// <summary>
        /// The retained differential motion-rate table.
        /// </summary>
        private readonly Av1MotionVectorCosts motionCosts;

        /// <summary>
        /// The reference of another size from which the rate-distortion search predicts its fractional candidates.
        /// The default value stands for a reference of the frame size.
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
        /// <param name="frame">
        /// The frame luma plane that gets each prediction of the winner estimate, or the default value when the search writes no frame.
        /// </param>
        /// <param name="residual">The packed block residual destination.</param>
        /// <param name="convolutionStorage">The signed intermediate storage for final prediction.</param>
        /// <param name="quantized">The quantized coefficients of one transform trial.</param>
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
        /// The reference of another size from which the rate-distortion search predicts its fractional candidates.
        /// The default value stands for a reference of the frame size.
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
            Av1PlaneRegion<TSample> frame,
            Span<short> residual,
            Span<short> convolutionStorage,
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
            this.frame = frame;
            this.residual = residual;
            this.convolutionStorage = convolutionStorage;
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
        /// Returns the full-pixel search range around a reference vector. At sharpness 3, the range also keeps the block near the visible frame.
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
        /// Returns the eighth-sample search range around a reference vector. At sharpness 3, the range also keeps the block near the visible frame.
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

            // The search starts at the reference vector, rounded to the nearest full sample.
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

            // These five costs describe the integer winner and its cardinal neighbors.
            // Fractional pruning uses the same neighborhood, so the buffer stays in use across both search stages.
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

            // The reported motion rate is the vector rate scaled by 108/128, with rounding.
            Av1MotionVector vector = new(integerResult.Vector.Y * 8, integerResult.Vector.X * 8);
            motionRate = ((this.motionCosts.GetCost(vector, referenceVector) * 108) + 64) >> 7;
            result = new FractionalResult(vector, integerResult.Variance, integerResult.SquaredError, integerResult.MotionCost);
            if (Av1RateDistortion.GetCost(this.rateMultiplier, motionRate, 0) > bestCost)
            {
                // When the rate alone is already too expensive, the fractional search does not run.
                // The result keeps the full-sample vector values unscaled, so they read as an eighth-sample vector.
                // This matches the output of other AV1 encoders. The rate stays the rate of the full-sample vector in eighth samples.
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
                    frameLowMotion,
                    sourceSad,
                    sourceVariance);

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
        /// Refines a full-sample projection estimate to fractional precision.
        /// The fractional search runs without start statistics or a cost list.
        /// It stops at the estimated precision for the projection result.
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
                frameLowMotion,
                sourceSad,
                sourceVariance);

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
        /// <param name="tables">The rate tables of the tile, which price a rate-distortion candidate estimate.</param>
        /// <param name="transformCoefficients">The forward transform output of a candidate estimate.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficients of a candidate estimate.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
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
        /// <param name="state">The retained results of the block. The caller initializes it once before the first new-motion mode of the block.</param>
        /// <param name="result">The selected displacement and prediction-error statistics.</param>
        /// <returns>Whether the search produced a valid candidate.</returns>
        public bool Search(
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
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

            // The frame can supply many temporal starts, but only the first two ranked candidates enter this search.
            // The code records both before the search, because the weight cutoff does not remove them from the start history.
            int candidateCount = Math.Min(2, starts.Length);
            Span<bool> rejected = stackalloc bool[2];
            rejected.Clear();
            Point fullReference = new(
                (referenceVector.Column + 3 + (referenceVector.Column >= 0 ? 1 : 0)) >> 3,
                (referenceVector.Row + 3 + (referenceVector.Row >= 0 ? 1 : 0)) >> 3);

            if (settings.StartCandidatePruningLevel != 0)
            {
                Span<Point> searchStarts = state.Starts;
                Span<byte> startReferenceIndices = state.StartReferenceIndices;
                Span<ReferenceSearchResult> references = state.References;
                for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
                {
                    Point start = starts[candidateIndex].Vector;
                    for (int historyIndex = 0; historyIndex < state.StartCount; historyIndex++)
                    {
                        int previousIndex = startReferenceIndices[historyIndex];
                        ref ReferenceSearchResult previous = ref references[previousIndex];
                        if (!previous.IsValid && previousIndex != referenceIndex)
                        {
                            continue;
                        }

                        Point previousStart = searchStarts[historyIndex];
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
                        searchStarts[state.StartCount] = start;
                        startReferenceIndices[state.StartCount++] = (byte)referenceIndex;
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

                // This motion policy disables neighborhood publication.
                // Thus fractional pruning measures its own candidates and does not fit the five-cost integer surface.
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
                Span<ReferenceSearchResult> references = state.References;
                for (int previousIndex = 0; previousIndex < referenceIndex; previousIndex++)
                {
                    ref ReferenceSearchResult previous = ref references[previousIndex];
                    if (!previous.HasFullResult)
                    {
                        continue;
                    }

                    if (previous.FullVector == integerVector && previous.FullRate + previous.DrlRate <= integerRate + drlRate)
                    {
                        return false;
                    }

                    // Level three permits a quarter more search error. Level four compares the original error directly.
                    // This prunes only when the earlier reference also has cheaper selection syntax.
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
                                in tables,
                                transformCoefficients,
                                dequantizedCoefficients,
                                transformWorkspace,
                                result.Vector,
                                referenceVector,
                                horizontalFilter,
                                verticalFilter);

                            long secondRateDistortion = this.EstimateCandidate(
                                in tables,
                                transformCoefficients,
                                dequantizedCoefficients,
                                transformWorkspace,
                                secondResult.Vector,
                                referenceVector,
                                horizontalFilter,
                                verticalFilter);

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
                    Span<ReferenceSearchResult> references = state.References;
                    for (int previousIndex = 0; previousIndex < referenceIndex; previousIndex++)
                    {
                        ref ReferenceSearchResult previous = ref references[previousIndex];
                        if (!previous.IsValid || previous.Vector != result.Vector)
                        {
                            continue;
                        }

                        // A previously skipped matching mode stays skipped, whatever the rate is.
                        // Otherwise, the earlier mode wins when its motion and reference syntax costs no more.
                        if (previous.Skip || previous.Rate + previous.DrlRate <= fractionalRate + drlRate)
                        {
                            current.Skip = true;
                            break;
                        }
                    }
                }
            }

            // The weight applies only to the motion-vector syntax. The transform and differential-reference rates keep their own 1/512-bit units.
            // A factor on their sum gives a different mode decision.
            current.Rate = ((this.motionCosts.GetCost(result.Vector, referenceVector) * 108) + 64) >> 7;
            current.Vector = result.Vector;
            current.IsValid = true;
            return true;
        }

        /// <summary>
        /// Estimates the rate-distortion cost of a refined vector from its final prediction, transform rate, and differential motion rate.
        /// </summary>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="transformCoefficients">The forward transform output of the estimate.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficients of the estimate.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="vector">The refined candidate vector.</param>
        /// <param name="referenceVector">The differential coding reference.</param>
        /// <param name="horizontalFilter">The horizontal interpolation family of the prediction.</param>
        /// <param name="verticalFilter">The vertical interpolation family of the prediction.</param>
        /// <returns>The rate-distortion estimate excluding the block skip-header cost.</returns>
        private long EstimateCandidate(
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            Av1MotionVector vector,
            Av1MotionVector referenceVector,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter)
        {
            int width = this.blockSize.GetWidth();
            int height = this.blockSize.GetHeight();
            if (this.scaledReference.IsScaled)
            {
                // A scaled reference predicts from the reference itself, with its scale factors.
                this.scaledReference.Predict<TOperator>(
                    vector, this.prediction, new Size(width, height), horizontalFilter, verticalFilter, this.bitDepth.GetBitCount());
            }
            else
            {
                // The final predictor takes its phases in sixteenth samples, so the eighth-sample fraction doubles.
                int origin = this.referenceOrigin + ((vector.Row >> 3) * this.referenceStride) + (vector.Column >> 3);
                TOperator.BuildPrediction(
                    this.reference,
                    this.referenceStride,
                    origin,
                    this.prediction,
                    width,
                    this.convolutionStorage,
                    width,
                    height,
                    horizontalFilter,
                    verticalFilter,
                    (vector.Column & 7) << 1,
                    (vector.Row & 7) << 1,
                    this.bitDepth.GetBitCount());
            }

            // Both predictors write a packed prediction, so the residual uses the block width as the prediction stride.
            TOperator.SubtractPrediction(this.source, this.sourceStride, this.prediction, width, this.residual, width, height);

            // The prediction also goes into the frame, before the transform estimate.
            Av1TransformBlockEncoder.WriteFrameSamples(this.frame, this.frame.Samples, this.blockOrigin, this.prediction, width, width, height);

            // The residual of a block that crosses the frame edge gets the border padding of the DCT_DCT transform type.
            Av1TransformBlockEncoder.PadBorderResidual(
                this.workspace, Av1Plane.Y, this.blockOrigin, this.residual, width, width, height, Av1TransformType.DctDct);

            Av1TransformBlockEncoder.EstimateInterTransform(
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                this.residual,
                width,
                this.quantized,
                this.writer,
                in tables,
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
