// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Stores the common decisions and retained winners of the superblock search.
/// </content>
internal static partial class Av1IntraSuperblockEncoder
{
    /// <summary>
    /// One asymmetric sub-block's cached luma decision.
    /// </summary>
    internal struct Av1AsymmetricModeCacheEntry
    {
        /// <summary>
        /// Whether a source candidate produced this decision.
        /// </summary>
        public bool Active;

        /// <summary>
        /// The luma prediction mode that the source candidate selected.
        /// </summary>
        public Av1PredictionMode Mode;

        /// <summary>
        /// The filter-intra mode of the source candidate, or
        /// <see cref="Av1FilterIntraMode.AllFilterIntraModes"/> when it used none.
        /// </summary>
        public Av1FilterIntraMode FilterIntraMode;

        /// <summary>
        /// The first reference of the source candidate, or <see cref="Av1ReferenceFrameType.Intra"/> for an intra
        /// decision.
        /// </summary>
        public Av1ReferenceFrameType ReferenceFrame;

        /// <summary>
        /// The second reference of a compound source candidate. For any other candidate, a value that is not an inter reference.
        /// </summary>
        public Av1ReferenceFrameType SecondaryReferenceFrame;
    }

    /// <summary>Retains one luma candidate until the full transform search.</summary>
    internal struct LumaCandidate
    {
        /// <summary>The retained prediction mode.</summary>
        public Av1PredictionMode Mode;

        /// <summary>The retained directional angle adjustment.</summary>
        public int AngleDelta;

        /// <summary>The retained filter-intra mode.</summary>
        public Av1FilterIntraMode FilterMode;

        /// <summary>The retained palette decision.</summary>
        public Av1EncoderPaletteInfo Palette;

        /// <summary>The estimated syntax rate of the palette header.</summary>
        public int PaletteHeaderRate;

        /// <summary>The candidate rate-distortion cost.</summary>
        public long Cost;
    }

    /// <summary>Contains the mutable decisions shared by control flow and typed sample workers.</summary>
    /// <remarks>
    /// The typed worker embeds this value for one superblock. The shared controller references that same value.
    /// Candidate evaluation can update budgets, caches and replay state without copying them between worker types.
    /// Sample buffers and their owners remain in the existing frame and block workspace.
    /// </remarks>
    internal struct SearchState
    {
        /// <summary>The selected block rate and distortion.</summary>
        public Av1RateDistortionStatistics SelectedBlockStatistics;

        /// <summary>
        /// One bit for each available reference type whose frame has another size than the current frame. The prediction from such a
        /// reference scales, and the motion search reads it through its resized copy.
        /// </summary>
        public int ScaledReferenceMask;

        /// <summary>The reference used by simple motion and partition pruning.</summary>
        public Av1ReferenceFrameType SimpleMotionReference;

        /// <summary>Whether the available-reference mask includes the golden reference.</summary>
        public bool HasDistinctGoldenReference;

        /// <summary>The picture coding state and current frame policy.</summary>
        public Av1PictureControlSet Picture;

        /// <summary>The superblock whose partition and block decisions are being searched.</summary>
        public Av1Superblock Superblock;

        /// <summary>The frame-owned coefficient and transform-state storage.</summary>
        public Av1EncoderCoefficientBuffer CoefficientBuffer;

        /// <summary>The existing reusable block and search workspace.</summary>
        public Av1EncoderBlockWorkspace BlockWorkspace;

        /// <summary>The current frame quantization parameters.</summary>
        public ObuQuantizationParameters Quantization;

        /// <summary>The native sample precision selected by the frame.</summary>
        public Av1BitDepth BitDepth;

        /// <summary>The frame rate-distortion multiplier before block adjustments.</summary>
        public int BaseRateMultiplier;

        /// <summary>The configured adjustment of the frame rate-distortion multiplier.</summary>
        public int RateMultiplierModifier;

        /// <summary>
        /// The quantizer of the superblock: the frame quantizer moved by any delta quantizer.
        /// </summary>
        public int SuperblockQIndex;

        /// <summary>
        /// The quantizer of the current block or partition node: the superblock quantizer moved by the segment of the last block setup.
        /// </summary>
        public int BlockQIndex;

        /// <summary>
        /// The segment of the block being searched.
        /// </summary>
        public int BlockSegmentId;

        /// <summary>
        /// The kind of encode that a block of the estimated inter search represents. It decides the cyclic refresh update of the block.
        /// </summary>
        public EstimatedLeafEncode EstimatedLeafEncode;

        /// <summary>
        /// The cost of the unsplit block that a split trial child of a merge trial compares against.
        /// </summary>
        public long MergeNoneCost;

        /// <summary>
        /// The split cost of the children before the current split trial child.
        /// </summary>
        public Av1RateDistortionStatistics MergeSplitStatistics;

        /// <summary>
        /// The position of the current split trial child, from 0 to 3.
        /// </summary>
        public int MergeChildIndex;

        /// <summary>
        /// The multiplier that the merge trial prices its costs with.
        /// </summary>
        public int MergeRateMultiplier;

        /// <summary>
        /// The prediction error of the best new vector of each single reference in the block being searched, or
        /// <see cref="int.MaxValue"/> before one is found.
        /// </summary>
        public InlineArray8<uint> BestSingleReferenceSses;

        /// <summary>
        /// The luma prediction error that the last model or fractional search of each first reference left in the block being searched,
        /// or <see cref="int.MaxValue"/> before one. A forced integer vector skips the fractional search and reads the value that an
        /// earlier candidate left.
        /// </summary>
        public InlineArray8<uint> PredictionSses;

        /// <summary>
        /// The best estimate of the mode loop when the transform search of the kept candidates found no mode below the block budget, or
        /// invalid otherwise. The skip mode comparison still reads it.
        /// </summary>
        public Av1RateDistortionStatistics LeftoverInterEstimate;

        /// <summary>
        /// The luma cost that an intra mode of an inter frame must beat. This is the luma cost of the inter winner. After the transform
        /// search of the kept candidates, it is the luma cost of the cheapest candidate that the search completed, even one above the
        /// block budget.
        /// </summary>
        public long InterLumaThreshold;

        /// <summary>The rate-distortion multiplier of the active block.</summary>
        public int RateMultiplier;

        /// <summary>The number of retained luma coefficients before the next coded block.</summary>
        public int CodedAreaLuma;

        /// <summary>The number of retained chroma coefficients before the next coded block.</summary>
        public int CodedAreaChroma;

        /// <summary>The retained partition-tree node being reconstructed for output.</summary>
        public int ReplayNodeIndex;

        /// <summary>The retained partition type being reconstructed for output.</summary>
        public Av1PartitionType ReplayPartition;

        /// <summary>The luma origin of the retained partition.</summary>
        public Point ReplayPartitionOrigin;

        /// <summary>The size of the retained partition parent.</summary>
        public Av1BlockSize ReplayParentSize;

        /// <summary>The retained luma candidates for the full transform search.</summary>
        public InlineArray3<LumaCandidate> LumaCandidates;

        /// <summary>The number of retained luma candidates.</summary>
        public int LumaCandidateCount;

        /// <summary>The costs of retained unsplit inter transforms.</summary>
        public InlineArray16<long> InterTransformNoSplitCosts;

        /// <summary>Whether inter candidates are being compared by prediction estimates.</summary>
        public bool EstimateInterCandidates;

        /// <summary>Whether the luma search found no candidate within its budget.</summary>
        public bool LumaSearchFailed;

        /// <summary>
        /// The cost of the last completed inter trial before the image tune bias.
        /// </summary>
        public long UnbiasedInterTrialCost;

        /// <summary>
        /// The sharpness 3 distortion offset of the luma samples that the intra luma search of an inter frame leaves for its chroma
        /// search. The chroma search adds it to its own cost.
        /// </summary>
        public long IntraSmoothingOffset;

        /// <summary>The number of retained inter candidates.</summary>
        public int InterCandidateCount;

        /// <summary>The number of cached compound-search results.</summary>
        public int CompoundSearchRecordCount;

        /// <summary>The number of cached interpolation-search results.</summary>
        public int InterpolationSearchRecordCount;

        /// <summary>The smallest retained inter prediction estimate.</summary>
        public long BestInterEstimate;

        /// <summary>The smallest inter prediction cost for all coded planes.</summary>
        public long BestInterPredictionCost;

        /// <summary>The smallest inter prediction cost for luma.</summary>
        public long BestInterLumaPredictionCost;

        /// <summary>The local warped-motion model of the current candidate.</summary>
        public Av1GlobalMotionParameters WarpedModel;

        /// <summary>Whether the current candidate uses the retained warped-motion model.</summary>
        public bool UseWarpedPrediction;

        /// <summary>
        /// Set while the estimated real-time search predicts each reference of another size from its copy resized to the frame size,
        /// without scaling. The selected block then predicts from the original reference.
        /// </summary>
        public bool PredictsFromSearchReferences;

        /// <summary>
        /// Set while the candidate predicts with OBMC, with the block and its neighbor availability.
        /// </summary>
        public bool UseObmcPrediction;

        /// <summary>The luma origin of the active OBMC candidate.</summary>
        public Point ObmcBlockOrigin;

        /// <summary>
        /// The reference of the current OBMC motion search, whose fractional candidates scale when the reference has
        /// another size than the frame.
        /// </summary>
        public Av1ReferenceFrameType ObmcSearchReference;

        /// <summary>The coding size of the active OBMC candidate.</summary>
        public Av1BlockSize ObmcBlockSize;

        /// <summary>Whether the current OBMC candidate can read its above neighbor.</summary>
        public bool ObmcAboveAvailable;

        /// <summary>Whether the current OBMC candidate can read its left neighbor.</summary>
        public bool ObmcLeftAvailable;

        /// <summary>The motion-mode candidates retained for their final transform search.</summary>
        public InlineArray10<Av1MotionModeWinner> MotionModeWinners;

        /// <summary>The number of retained motion-mode candidates.</summary>
        public int MotionModeWinnerCount;

        /// <summary>The configured maximum number of retained motion-mode candidates.</summary>
        public int MotionModeWinnerLimit;

        /// <summary>Whether the retained motion modes are in their final search.</summary>
        public bool EvaluatingMotionModeWinners;

        /// <summary>The modes omitted from the search, indexed by reference type.</summary>
        public InlineArray8<uint> InterModeSkipMasks;

        /// <summary>
        /// The skip flag that the last completed transform search of the inter mode search leaves. The final encode of an inter winner
        /// adds it to the flags of the winner.
        /// </summary>
        public bool TransformSearchSkip;

        /// <summary>
        /// Whether a motion mode trial of the current mode search reached its transform search. Each such trial clears the leftover skip
        /// flag before it searches.
        /// </summary>
        public bool TransformSearchReset;

        /// <summary>
        /// The reference types that a rectangular block does not search, one bit per reference type.
        /// </summary>
        public int SkipReferenceFrameMask;

        /// <summary>
        /// The predicted-vector SAD of each reference, and the best of the references that precede and follow the frame.
        /// </summary>
        public InlineArray8<int> PredictionVectorSads;

        /// <summary>The smallest predicted-vector SAD among preceding references.</summary>
        public int BestPastPredictionVectorSad;

        /// <summary>The smallest predicted-vector SAD among following references.</summary>
        public int BestFuturePredictionVectorSad;

        /// <summary>Whether the full search is evaluating retained inter candidates.</summary>
        public bool SearchingRetainedCandidates;

        /// <summary>The variance-based source cost retained for inter pruning.</summary>
        public long InterSourceVarianceCost;

        /// <summary>The source variance of the last searched block.</summary>
        public int InterSourceVariance;

        /// <summary>Whether the retry must search until it obtains a valid partition.</summary>
        public bool MustFindValidPartition;

        /// <summary>
        /// The selected reference-coded block kept no coefficient. The frame grid marks it skipped, but the partition context keeps the
        /// searched flag.
        /// </summary>
        public bool EncodedWithoutCoefficients;

        /// <summary>
        /// Set while a leaf is reconstructed for the partition search. The mode search keeps the types that it searched. Only an encode
        /// of the decision writes DCT_DCT for a luma block that quantized to nothing.
        /// </summary>
        public bool KeepSearchedZeroBlockTypes;

        /// <summary>The active block budget used for early termination.</summary>
        public long BlockCostLimit;

        /// <summary>The partition-size limit after the superblock-size cap.</summary>
        public Av1BlockSize MaximumPartitionSize;

        /// <summary>The cached luma decisions for the current asymmetric partition.</summary>
        public InlineArray3<Av1AsymmetricModeCacheEntry> AsymmetricModeCache;

        /// <summary>The cached luma decision of the active asymmetric leaf.</summary>
        public Av1AsymmetricModeCacheEntry ActiveModeCache;

        /// <summary>Whether the cached intra partition-model features describe this node.</summary>
        public bool IntraPartitionFeaturesValid;

        /// <summary>The logarithmic quantizer feature of the cached intra partition model.</summary>
        public float IntraPartitionLogQuantizer;

        /// <summary>The superblock source-change classification used by motion search.</summary>
        public Av1SourceSadLevel SourceSadLevel;

        /// <summary>Whether source analysis classified a lighting change.</summary>
        public bool SourceLightingChange;

        /// <summary>Whether source analysis found a small squared mean difference.</summary>
        public bool SourceLowSumDifference;

        /// <summary>Whether this search applies the configured temporal source filter.</summary>
        public bool FilterTemporalSource;

        /// <summary>The motion vector retained for partition pruning.</summary>
        public Av1MotionVector PartitionMotion;

        /// <summary>The motion vector retained for superblock pruning.</summary>
        public Av1MotionVector SuperblockMotion;

        /// <summary>The reference type of the retained partition motion.</summary>
        public Av1ReferenceFrameType PartitionReference;

        /// <summary>Whether partition trials use the retained motion vector.</summary>
        public bool UsePartitionMotion;

        /// <summary>
        /// The inter-prediction buffer that holds the luma prediction the estimated search built last, or -1.
        /// </summary>
        public int LastLumaPredictionBuffer;

        /// <summary>
        /// The color sensitivity of the block searched last, after its block-level check. It starts from the superblock sensitivity.
        /// </summary>
        public InlineArray2<byte> BlockColorSensitivity;

        /// <summary>The reference-pruning level of the current estimated search.</summary>
        public int EstimatedReferencePruning;

        /// <summary>The superblock chroma sensitivity before block-level checks.</summary>
        public InlineArray2<byte> SuperblockColorSensitivity;

        /// <summary>
        /// Whether each 64x64 unit of the superblock still leaves CDEF off, in raster order. The superblock sets the flags, and each
        /// block search can clear them.
        /// </summary>
        public InlineArray4<bool> CdefSkipUnits;

        /// <summary>The chroma sensitivity measured against the golden reference.</summary>
        public InlineArray2<byte> GoldenColorSensitivity;

        /// <summary>The chroma sensitivity measured against the alternate reference.</summary>
        public InlineArray2<byte> AlternateColorSensitivity;

        /// <summary>
        /// Whether the temporal dependency model keeps each reference type, INTRA to ALTREF, from the selective reference pruning in the
        /// current superblock.
        /// </summary>
        public InlineArray8<bool> TplKeepReferenceFrames;

        /// <summary>
        /// The number of 16x16 model blocks of the superblock inside the frame whose costs and vectors the block workspace holds, or zero
        /// without them.
        /// </summary>
        public int TplSuperblockBlockCount;

        /// <summary>
        /// The number of model blocks per superblock row of the gathered costs and vectors.
        /// </summary>
        public int TplSuperblockStride;

        /// <summary>
        /// Whether the inter mode search of the current block skips modes by the reference costs of the model.
        /// </summary>
        public bool TplInterModePruning;

        /// <summary>
        /// The model prediction error of each reference LAST to ALTREF, summed over the current block.
        /// </summary>
        public InlineArray7<long> TplReferenceInterCosts;

        /// <summary>
        /// The smallest nonzero entry of <see cref="SearchState.TplReferenceInterCosts"/> among the references that the selective pruning keeps.
        /// </summary>
        public long TplBestInterCost;

        /// <summary>
        /// Whether the mode decision workspace holds the luma gradients of the current superblock.
        /// </summary>
        public bool LumaGradientsCached;

        /// <summary>
        /// Whether the mode decision workspace holds the blue-difference gradients of the current superblock.
        /// </summary>
        public bool ChromaGradientsCached;

        /// <summary>
        /// Whether <see cref="SearchState.SourceVarianceValue"/> holds the source variance of the block at
        /// <see cref="SearchState.SourceVarianceOrigin"/> with size <see cref="SearchState.SourceVarianceSize"/>.
        /// </summary>
        public bool SourceVarianceValid;

        /// <summary>
        /// The luma origin of the block whose source variance is kept.
        /// </summary>
        public Point SourceVarianceOrigin;

        /// <summary>
        /// The size of the block whose source variance is kept.
        /// </summary>
        public Av1BlockSize SourceVarianceSize;

        /// <summary>
        /// The per-sample source variance of the last measured block. The source does not change during the encode, so
        /// every later request for the same block reads it here instead of measuring the block again.
        /// </summary>
        public int SourceVarianceValue;

        /// <summary>
        /// The luma origin of the current superblock, which locates a block in the gradient cache.
        /// </summary>
        public Point GradientSuperblockOrigin;

        /// <summary>The horizontal chroma subsampling exponent.</summary>
        public int ChromaSubsamplingX;

        /// <summary>The coded luma dimensions used by block search thresholds.</summary>
        public Size SourceSize;

        /// <summary>The vertical chroma subsampling exponent.</summary>
        public int ChromaSubsamplingY;

        /// <summary>Whether the frame has no chroma planes.</summary>
        public bool IsMonochrome;

        /// <summary>
        /// Gets a value indicating whether the segment of the block being searched codes losslessly. The flag comes from the
        /// segment quantizer without the delta quantizer of the superblock, so it can differ from a zero block quantizer index.
        /// </summary>
        public readonly bool BlockLossless => this.Picture.Parent.FrameHeader.LosslessArray[this.BlockSegmentId];

        /// <summary>
        /// Gets a value indicating whether the frame decides its blocks with the estimated inter-frame search, which encodes each winner
        /// in place.
        /// </summary>
        public readonly bool UsesEstimatedInterSearch =>
            !this.Picture.Parent.FrameHeader.IsIntra && this.Picture.Parent.SpeedSettings.UseEstimatedInterModeDecision;

        /// <summary>
        /// Gets a value indicating whether the high bit depth sharpness 3 rules apply: a bit depth above 8 with sharpness 3.
        /// </summary>
        public readonly bool UsesHighBitDepthSharpness => this.BitDepth.GetBitCount() > 8 && this.BlockWorkspace.EncoderOptions.Sharpness == 3;

        /// <summary>
        /// Gets a value indicating whether 8-bit sharpness 3 adds a charge to a block that is smoother than its source. A high bit depth
        /// uses other rules. Intra frames, golden frames and alternate reference frames get no charge.
        /// </summary>
        public readonly bool ChargesSmoothing =>
            this.BlockWorkspace.EncoderOptions.Sharpness == 3 && !this.UsesHighBitDepthSharpness && !this.Picture.Parent.FrameHeader.IsIntra &&
            this.Picture.Parent.FrameUpdateType is not (Av1FrameUpdateType.Golden or Av1FrameUpdateType.Alternate);

        /// <summary>
        /// Gets a value indicating whether high bit depth sharpness 3 adds a texture-loss charge to a block cost. Only intra frames get no
        /// charge. Golden frames and alternate reference frames get the charge.
        /// </summary>
        public readonly bool ChargesHighBitDepthTextureLoss => this.UsesHighBitDepthSharpness && !this.Picture.Parent.FrameHeader.IsIntra;

        /// <summary>
        /// Gets a value indicating whether the full-pixel searches cap their first mesh interval. This applies to the alternate reference
        /// frames of screen content at good quality speeds up to 2.
        /// </summary>
        public readonly bool UsesFineSearchInterval =>
            this.Picture.Parent.IsScreenContent &&
            this.Picture.Parent.FrameUpdateType == Av1FrameUpdateType.Alternate &&
            !this.Picture.Parent.SpeedSettings.IsRealtime &&
            this.Picture.Parent.EncodingSpeed <= HeifEncodingSpeed.Level2;

        /// <summary>
        /// Gets the quantizer scale of the mode thresholds of the segment of the block.
        /// </summary>
        public readonly int ModeThresholdQuantizerFactor => this.BlockWorkspace.ModeThresholdQuantizerFactors[this.BlockSegmentId];

        /// <summary>
        /// Gets the Q12 mode threshold multiplier of the block. When skippable inter modes are pruned, it goes from 2.5 at quantizer zero
        /// to 1 at quantizer 255, rounded to the nearest integer in Q12. Otherwise it is 1.
        /// </summary>
        public readonly int ModeThresholdSkipMultiplier => this.Picture.Parent.SpeedSettings.PruneSkippableInterModes
            ? 10240 - (((this.BlockQIndex * 6144) + 127) / 255)
            : 4096;
    }

    /// <summary>
    /// Retains candidate decisions while typed workers reuse prediction and reconstruction buffers.
    /// </summary>
    private struct SingleReferenceSearchState
    {
        /// <summary>The winner before the current new-motion list entry.</summary>
        private SingleReferenceWinner previous;

        /// <summary>The winner selected so far.</summary>
        public SingleReferenceWinner Winner;

        /// <summary>Initializes a new instance of the <see cref="SingleReferenceSearchState"/> struct.</summary>
        /// <param name="filter">The interpolation filter used before the first candidate.</param>
        public SingleReferenceSearchState(Av1InterpolationFilter filter)
        {
            this = default;
            this.Winner.Statistics = Av1RateDistortionStatistics.Invalid;
            this.Winner.MotionMode = Av1MotionMode.SimpleTranslation;
            this.Winner.HorizontalFilter = filter;
            this.Winner.VerticalFilter = filter;
        }

        /// <summary>Retains the decisions that preceded a new-motion list entry.</summary>
        public void SaveWinner()
        {
            // The typed worker holds the matching samples and coefficients in its existing spare views.
            // Only syntax and transform decisions belong to this snapshot. No sample buffer is copied.
            this.previous = this.Winner;
        }

        /// <summary>Restores the decisions when the new-motion entry finishes at its reference vector.</summary>
        public void RestoreWinner() => this.Winner = this.previous;
    }

    /// <summary>
    /// Holds the syntax and transform decisions of one single-reference candidate, without sample storage.
    /// </summary>
    private struct SingleReferenceWinner
    {
        /// <summary>The rate and distortion of the retained candidate.</summary>
        public Av1RateDistortionStatistics Statistics;

        /// <summary>The retained motion vector.</summary>
        public Av1MotionVector Vector;

        /// <summary>The motion-vector syntax mode.</summary>
        public Av1PredictionMode Mode;

        /// <summary>The coded index in the reference-vector list.</summary>
        public int ReferenceIndex;

        /// <summary>Whether the candidate codes no transform coefficients.</summary>
        public bool Skip;

        /// <summary>Whether the candidate blends inter and intra predictions.</summary>
        public bool InterIntra;

        /// <summary>The retained translation, OBMC or warped motion mode.</summary>
        public Av1MotionMode MotionMode;

        /// <summary>The intra prediction used by an inter-intra candidate.</summary>
        public Av1InterIntraMode InterIntraMode;

        /// <summary>Whether the inter-intra blend uses a wedge.</summary>
        public bool InterIntraWedge;

        /// <summary>The retained inter-intra wedge index.</summary>
        public int InterIntraWedgeIndex;

        /// <summary>The retained luma transform decisions.</summary>
        public InlineArray64<Av1EncoderTransformBlockState> LumaStates;

        /// <summary>The retained luma transform sizes.</summary>
        public InlineArray16<Av1TransformSize> LumaSizes;

        /// <summary>The retained blue-difference transform decisions.</summary>
        public InlineArray16<Av1EncoderTransformBlockState> BlueState;

        /// <summary>The retained red-difference transform decisions.</summary>
        public InlineArray16<Av1EncoderTransformBlockState> RedState;

        /// <summary>The retained vertical interpolation filter.</summary>
        public Av1InterpolationFilter VerticalFilter;

        /// <summary>The retained horizontal interpolation filter.</summary>
        public Av1InterpolationFilter HorizontalFilter;

        /// <summary>Retains a translation, OBMC or warped candidate after the typed worker selects it.</summary>
        /// <param name="statistics">The candidate's rate and distortion.</param>
        /// <param name="vector">The candidate's motion vector.</param>
        /// <param name="mode">The motion-vector syntax mode.</param>
        /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
        /// <param name="verticalFilter">The vertical interpolation filter.</param>
        /// <param name="referenceIndex">The coded reference-list index.</param>
        /// <param name="skip">Whether the candidate codes no transform coefficients.</param>
        /// <param name="motionMode">The translation, OBMC or warped mode.</param>
        /// <param name="lumaStates">The luma transform decisions.</param>
        /// <param name="lumaSizes">The luma transform sizes.</param>
        /// <param name="blueState">The blue-difference transform decisions.</param>
        /// <param name="redState">The red-difference transform decisions.</param>
        public void SelectInter(
            Av1RateDistortionStatistics statistics,
            Av1MotionVector vector,
            Av1PredictionMode mode,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int referenceIndex,
            bool skip,
            Av1MotionMode motionMode,
            in InlineArray64<Av1EncoderTransformBlockState> lumaStates,
            in InlineArray16<Av1TransformSize> lumaSizes,
            in InlineArray16<Av1EncoderTransformBlockState> blueState,
            in InlineArray16<Av1EncoderTransformBlockState> redState)
        {
            this.Statistics = statistics;
            this.Vector = vector;
            this.Mode = mode;
            this.HorizontalFilter = horizontalFilter;
            this.VerticalFilter = verticalFilter;
            this.ReferenceIndex = referenceIndex;
            this.Skip = skip;
            this.InterIntra = false;
            this.MotionMode = motionMode;

            // Later trials overwrite the workspace. Retain their transform decisions once, in this stack-owned snapshot.
            this.LumaStates = lumaStates;
            this.LumaSizes = lumaSizes;
            this.BlueState = blueState;
            this.RedState = redState;
        }

        /// <summary>Retains an inter-intra candidate after the typed worker selects it.</summary>
        /// <param name="statistics">The candidate's rate and distortion.</param>
        /// <param name="vector">The candidate's motion vector.</param>
        /// <param name="mode">The motion-vector syntax mode.</param>
        /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
        /// <param name="verticalFilter">The vertical interpolation filter.</param>
        /// <param name="referenceIndex">The coded reference-list index.</param>
        /// <param name="skip">Whether the candidate codes no transform coefficients.</param>
        /// <param name="interIntraMode">The intra prediction used by the blend.</param>
        /// <param name="useWedge">Whether the blend uses a wedge.</param>
        /// <param name="wedgeIndex">The wedge index.</param>
        /// <param name="lumaStates">The luma transform decisions.</param>
        /// <param name="lumaSizes">The luma transform sizes.</param>
        /// <param name="blueState">The blue-difference transform decisions.</param>
        /// <param name="redState">The red-difference transform decisions.</param>
        public void SelectInterIntra(
            Av1RateDistortionStatistics statistics,
            Av1MotionVector vector,
            Av1PredictionMode mode,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int referenceIndex,
            bool skip,
            Av1InterIntraMode interIntraMode,
            bool useWedge,
            int wedgeIndex,
            in InlineArray64<Av1EncoderTransformBlockState> lumaStates,
            in InlineArray16<Av1TransformSize> lumaSizes,
            in InlineArray16<Av1EncoderTransformBlockState> blueState,
            in InlineArray16<Av1EncoderTransformBlockState> redState)
        {
            this.SelectInter(
                statistics,
                vector,
                mode,
                horizontalFilter,
                verticalFilter,
                referenceIndex,
                skip,
                Av1MotionMode.SimpleTranslation,
                in lumaStates,
                in lumaSizes,
                in blueState,
                in redState);

            this.InterIntra = true;
            this.InterIntraMode = interIntraMode;
            this.InterIntraWedge = useWedge;
            this.InterIntraWedgeIndex = wedgeIndex;
        }

        /// <summary>Writes the retained syntax and transform decisions before final reconstruction.</summary>
        /// <param name="modeInfo">The block mode decisions to update.</param>
        /// <param name="block">The block syntax state to update.</param>
        /// <param name="states">The caller-owned transform-state output.</param>
        public readonly void WriteDecisions(ref Av1MacroBlockModeInfo modeInfo, ref Av1EncoderBlockStruct block, Span<Av1EncoderTransformBlockState> states)
        {
            // The existing output layout reserves 64 luma entries and 16 entries for each chroma plane.
            // Keep the remaining entries unchanged, as the typed worker initializes the complete output.
            this.LumaStates[..].CopyTo(states);
            this.LumaSizes[..].CopyTo(modeInfo.Block.InterTransformSizes);
            this.BlueState[..].CopyTo(states[64..80]);
            this.RedState[..].CopyTo(states[80..96]);

            modeInfo.Block.Mode = this.Mode;
            modeInfo.Block.Skip = this.Skip;
            modeInfo.Block.MotionMode = this.MotionMode;
            if (this.InterIntra)
            {
                modeInfo.Block.SecondaryReferenceFrame = Av1ReferenceFrameType.Intra;
                modeInfo.Block.InterIntraMode = this.InterIntraMode;
                modeInfo.Block.UseInterIntraWedge = this.InterIntraWedge;
                modeInfo.Block.InterIntraWedgeIndex = (byte)this.InterIntraWedgeIndex;
            }

            modeInfo.Block.VerticalInterpolationFilter = this.VerticalFilter;
            modeInfo.Block.HorizontalInterpolationFilter = this.HorizontalFilter;
            block.ReferenceMotionVectorIndex = this.ReferenceIndex;
        }
    }
}
