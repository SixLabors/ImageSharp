// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Owns the reusable spatial, transform, and reconstruction storage for AV1 block encoding.
/// </summary>
internal sealed class Av1EncoderBlockWorkspace : IDisposable
{
    /// <summary>
    /// The maximum number of spatial residual samples in one coding block. Motion search measures the residual
    /// of a whole block, up to 128x128, before transforms divide it.
    /// </summary>
    public const int MaximumResidualCount = (1 << Av1Constants.MaxSuperBlockSizeLog2) * (1 << Av1Constants.MaxSuperBlockSizeLog2);

    /// <summary>
    /// The maximum number of coded coefficients after AV1 removes the uncoded half of 64-point axes.
    /// </summary>
    public const int MaximumCoefficientCount = (Av1Constants.MaxTransformSize / 2) * (Av1Constants.MaxTransformSize / 2);

    /// <summary>
    /// The base workspace length in signed-integer storage elements, excluding inter-motion state.
    /// </summary>
    public const int StorageLength =
        ResidualStorageLength +
        MaximumCoefficientCount +
        MaximumCoefficientCount +
        Av1TransformWorkspace.MaximumLength +
        SharedModeDecisionStorageLength +
        PartitionContextStorageLength;

    private const int ResidualStorageLength = MaximumResidualCount / 2;
    private const int MotionSearchSiteCount = 6;
    private const int LoopFilterLevelCount = 4;
    private const int MotionSearchPredictionSampleCount = 128 * (128 + 8);
    private const int TransformCoefficientOffset = ResidualStorageLength;
    private const int DequantizedCoefficientOffset = TransformCoefficientOffset + MaximumCoefficientCount;
    private const int TransformWorkspaceOffset = DequantizedCoefficientOffset + MaximumCoefficientCount;
    private const int InterPredictionSampleStorageOffset = TransformWorkspaceOffset + Av1TransformWorkspace.MaximumLength;
    private const int InterPredictionSampleStorageLength =
        ((Av1EncoderInterPredictionWorkspace<ushort>.SampleBufferCount *
        Av1EncoderInterPredictionWorkspace<ushort>.MaximumSampleCount) +
        Av1EncoderInterPredictionWorkspace<ushort>.TransformSampleCount) *
        sizeof(ushort) /
        sizeof(int);

    private const int InterPredictionResidualStorageOffset =
        InterPredictionSampleStorageOffset + InterPredictionSampleStorageLength;

    private const int InterPredictionResidualStorageLength =
        Av1EncoderInterPredictionWorkspace<ushort>.MaximumSampleCount *
        sizeof(short) /
        sizeof(int);

    private const int InterPredictionScratchStorageOffset =
        InterPredictionResidualStorageOffset + InterPredictionResidualStorageLength;

    private const int InterPredictionScratchStorageLength =
        Av1EncoderInterPredictionWorkspace<ushort>.PredictionScratchCount *
        sizeof(short) /
        sizeof(int);

    private const int InterPredictionCoefficientStorageOffset =
        InterPredictionScratchStorageOffset + InterPredictionScratchStorageLength;

    private const int InterPredictionCoefficientStorageLength =
        (Av1EncoderInterPredictionWorkspace<ushort>.CoefficientBufferCount *
        Av1EncoderInterPredictionWorkspace<ushort>.MaximumSampleCount) +
        Av1EncoderInterPredictionWorkspace<ushort>.TransformSampleCount +
        (4 * (1 << (Av1Constants.MaxSuperBlockSizeLog2 - Av1Constants.ModeInfoSizeLog2)) / sizeof(int));

    private const int InterPredictionStorageLength =
        InterPredictionSampleStorageLength +
        InterPredictionResidualStorageLength +
        InterPredictionScratchStorageLength +
        InterPredictionCoefficientStorageLength;

    private const int ModeDecisionStorageLength = Av1EncoderModeDecisionWorkspace<ushort>.StorageLength;

    // Motion search and compound evaluation run sequentially. The larger compound region holds
    // two unsigned predictions and a byte mask; transform trials borrow its prediction area.
    private const int CompoundScratchStorageLength =
        Av1EncoderInterPredictionWorkspace<ushort>.MaximumSampleCount * ((2 * sizeof(ushort)) + sizeof(byte)) / sizeof(int);

    private const int InterSearchStorageLength = InterPredictionStorageLength + CompoundScratchStorageLength;

    private const int SharedModeDecisionStorageLength = ModeDecisionStorageLength > InterSearchStorageLength
        ? ModeDecisionStorageLength
        : InterSearchStorageLength;

    private const int PartitionContextStorageOffset =
        InterPredictionSampleStorageOffset + SharedModeDecisionStorageLength;

    private const int MaximumPartitionEdgeUnitCount =
        2 * (1 << (Av1Constants.MaxSuperBlockSizeLog2 - Av1Constants.ModeInfoSizeLog2));

    private const int PartitionContextBytesPerEdgeUnit =
        Av1PartitionContext.StorageSize + (4 * sizeof(byte)) + Av1EncoderPaletteInfo.StorageSize;

    private const int PartitionContextSlotByteLength =
        MaximumPartitionEdgeUnitCount * PartitionContextBytesPerEdgeUnit;

    private const int PartitionContextSlotLength =
        PartitionContextSlotByteLength / sizeof(int);

    private const int PartitionTrialLevelCount =
        Av1Constants.MaxSuperBlockSizeLog2 - 3 + 1;

    private const int PartitionContextStorageLength =
        PartitionContextSlotLength * PartitionTrialLevelCount;

    /// <summary>
    /// Owns the complete reusable block workspace in 32-bit elements so every transform region is naturally aligned.
    /// </summary>
    private readonly IMemoryOwner<int> owner;

    /// <summary>
    /// The retained search-site offset after any inter-frame motion-rate tables.
    /// </summary>
    private readonly int motionSearchSiteStorageOffset;

    private readonly int displacementCostStorageOffset;
    private readonly int transformProbabilityStorageOffset;
    private readonly int interpolationProbabilityStorageOffset;
    private readonly int referenceFrameNumberStorageOffset;
    private readonly int loopFilterLevelStorageOffset;
    private readonly int modeThresholdStorageOffset;
    private readonly int simpleMotionStorageOffset;
    private readonly int simpleMotionStorageLength;
    private readonly int intraPartitionStorageOffset;
    private readonly int sourceLogVarianceStorageOffset;
    private readonly int interpolationSearchStorageOffset;
    private readonly int singleReferenceFilterCostStorageOffset;
    private readonly int singleReferenceSimpleCostStorageOffset;
    private readonly int interModeStorageOffset;
    private readonly int interModeModelStorageOffset;
    private readonly int compoundSearchStorageOffset;

    /// <summary>
    /// Reuses the fixed-capacity reference-vector stack for every inter block in the frame.
    /// </summary>
    private Av1ReferenceMotionVectors referenceMotionVectors;

    /// <summary>
    /// Reuses prediction estimates and retained vectors across real-time block searches.
    /// </summary>
    private Av1EstimatedInterSearchState estimatedInterSearchState;

    /// <summary>
    /// The restoration stripe rows retained across frames once restoration is enabled.
    /// </summary>
    private Av1LoopRestorationBoundary? restorationBoundary;

    /// <summary>
    /// The eight-bit trial planes retained across frames.
    /// </summary>
    private Av1EncoderFrameBuffer<byte>? restorationByteTrial;

    /// <summary>
    /// The high-bit-depth trial planes retained across frames.
    /// </summary>
    private Av1EncoderFrameBuffer<ushort>? restorationHighBitDepthTrial;

    /// <summary>
    /// Retained syntax and transform choices for the current superblock search.
    /// </summary>
    private Av1EncoderPartitionTree? partitionTree;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderBlockWorkspace"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing the encoder allocator.</param>
    public Av1EncoderBlockWorkspace(Configuration configuration)
        : this(configuration, allocateInterMotionCosts: false, allocateDisplacementCosts: false, Av1BlockSize.Block64x64)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderBlockWorkspace"/> class for a fixed encoding mode.
    /// </summary>
    /// <param name="configuration">The configuration providing the encoder allocator.</param>
    /// <param name="allocateInterMotionCosts">Whether the worker will encode inter frames.</param>
    /// <param name="allocateDisplacementCosts">Whether the worker can encode intra-block copy.</param>
    /// <param name="superblockSize">The sequence's square superblock size.</param>
    public Av1EncoderBlockWorkspace(
        Configuration configuration,
        bool allocateInterMotionCosts,
        bool allocateDisplacementCosts,
        Av1BlockSize superblockSize)
    {
        this.Configuration = configuration;

        // Motion rates belong to the worker, not a block candidate or frame. Keep both precision pairs after
        // the existing scratch regions so sequence frames can change precision while retaining one owner.
        // Block-copy capacity appends its independent integer pair at the end of that same allocation.
        this.motionSearchSiteStorageOffset = StorageLength + (allocateInterMotionCosts ? Av1MotionVectorCosts.StorageLength : 0);
        bool allocateSearchSites = allocateInterMotionCosts || allocateDisplacementCosts;
        int length = this.motionSearchSiteStorageOffset +
            (allocateSearchSites ? MotionSearchSiteCount * Av1MotionSearchSites.StorageLength : 0) +
            (allocateDisplacementCosts ? Av1MotionVectorCosts.IntegerStorageLength : 0);

        this.displacementCostStorageOffset = length - Av1MotionVectorCosts.IntegerStorageLength;
        this.transformProbabilityStorageOffset = length;
        length += Av1TransformTypeProbabilities.StorageLength + Av1TransformTypeProbabilities.FrameLength;
        this.interpolationProbabilityStorageOffset = length;
        length += Av1InterpolationProbabilities.StorageLength;
        this.referenceFrameNumberStorageOffset = length;
        length += Av1Constants.ReferenceFrameCount;
        this.loopFilterLevelStorageOffset = length;
        length += LoopFilterLevelCount;
        this.modeThresholdStorageOffset = length;
        if (allocateInterMotionCosts)
        {
            length += (int)Av1BlockSize.AllSizes * Av1ModeThresholds.ModeCount;
        }

        // Prediction records survive all mode trials in a block; fitted models survive superblocks
        // until the tile ends. Keep both outside scratch regions that transforms overwrite.
        this.interModeStorageOffset = (length + 1) & ~1;
        this.interModeModelStorageOffset = this.interModeStorageOffset +
            ((((Av1InterModeCandidate.Capacity * Unsafe.SizeOf<Av1InterModeCandidate>()) + 7) & ~7) / sizeof(int));

        this.compoundSearchStorageOffset = this.interModeModelStorageOffset +
            ((int)Av1BlockSize.AllSizes * Unsafe.SizeOf<Av1InterModeRateDistortionModel>() / sizeof(int));

        this.interpolationSearchStorageOffset = this.compoundSearchStorageOffset +
            (Av1CompoundSearchRecord.Capacity * Unsafe.SizeOf<Av1CompoundSearchRecord>() / sizeof(int));

        this.singleReferenceFilterCostStorageOffset = this.interpolationSearchStorageOffset +
            (Av1InterpolationSearchRecord.Capacity * Unsafe.SizeOf<Av1InterpolationSearchRecord>() / sizeof(int));

        // Modeled filter costs and complete translation costs share the same mode/DRL/reference indexing.
        // Both survive compound trials but are reset at the next coding block.
        int singleReferenceCostLength = 4 * 3 * Av1Constants.ReferenceFrameCount * sizeof(long) / sizeof(int);
        this.singleReferenceSimpleCostStorageOffset = this.singleReferenceFilterCostStorageOffset + singleReferenceCostLength;
        if (allocateInterMotionCosts)
        {
            length = this.singleReferenceSimpleCostStorageOffset + singleReferenceCostLength;
        }

        this.simpleMotionStorageOffset = length;
        if (allocateInterMotionCosts)
        {
            // Include the 4x4 leaves used by simple-motion split features, even though those leaves
            // do not own further partition searches. Geometry fixes this storage for the worker's lifetime.
            int leafCount = superblockSize.GetWidth() * superblockSize.GetHeight() / 16;
            int nodeCount = ((4 * leafCount) - 1) / 3;
            this.simpleMotionStorageLength = nodeCount * Unsafe.SizeOf<Av1SimpleMotionData>() / sizeof(int);
            length += this.simpleMotionStorageLength;
        }

        this.intraPartitionStorageOffset = length;
        length += 20 + (4 * 2 * 2) + (20 * 4 * 4) + (20 * 8 * 8);
        this.sourceLogVarianceStorageOffset = (length + 1) & ~1;
        length = this.sourceLogVarianceStorageOffset +
            (superblockSize.GetWidth() * superblockSize.GetHeight() / 16 * sizeof(double) / sizeof(int));
        this.owner = this.MemoryAllocator.Allocate<int>(length);
        this.TransformTypeProbabilities.Clear();
        this.InterpolationProbabilities.Clear();
        this.PreviousLoopFilterLevels.Clear();
        if (allocateSearchSites)
        {
            // Each shape retains its offsets across frames. A zero stride marks its first use; every populated
            // site and stage is subsequently overwritten when the reference stride changes.
            Span<int> storage = this.owner.Memory.Span;
            for (int index = 0; index < MotionSearchSiteCount; index++)
            {
                storage[this.motionSearchSiteStorageOffset + ((index + 1) * Av1MotionSearchSites.StorageLength) - 1] = 0;
            }
        }
    }

    /// <summary>
    /// Gets the four convolution outputs retained throughout a 64x64 partition search.
    /// </summary>
    public Span<float> IntraPartitionFeatures => MemoryMarshal.Cast<int, float>(
        this.owner.Memory.Span.Slice(this.intraPartitionStorageOffset, 20 + (4 * 2 * 2) + (20 * 4 * 4) + (20 * 8 * 8)));

    /// <summary>
    /// Gets the source log variances for the current superblock's 4x4 cells.
    /// </summary>
    public Span<double> SourceLogVariances => MemoryMarshal.Cast<int, double>(
        this.owner.Memory.Span[this.sourceLogVarianceStorageOffset..]);

    /// <summary>
    /// Gets temporary storage for the 65x65 normalized input and the first 20-channel 16x16 layer.
    /// </summary>
    public Span<float> IntraPartitionScratch => MemoryMarshal.Cast<int, float>(
        this.owner.Memory.Span.Slice(InterPredictionSampleStorageOffset, (65 * 65) + (20 * 16 * 16)));

    /// <summary>
    /// Gets frame-role probabilities retained for the sequence's lifetime.
    /// </summary>
    public Span<int> TransformTypeProbabilities => this.owner.Memory.Span.Slice(
        this.transformProbabilityStorageOffset, Av1TransformTypeProbabilities.StorageLength);

    /// <summary>
    /// Gets transform counts borrowed by the current picture's coefficient writer.
    /// </summary>
    public Memory<int> TransformTypeCounts => this.owner.Memory.Slice(
        this.transformProbabilityStorageOffset + Av1TransformTypeProbabilities.StorageLength, Av1TransformTypeProbabilities.FrameLength);

    /// <summary>
    /// Gets the prediction records retained until the block's transform search finishes.
    /// </summary>
    public Span<Av1InterModeCandidate> InterModeCandidates => MemoryMarshal.Cast<int, Av1InterModeCandidate>(
        this.owner.Memory.Span[this.interModeStorageOffset..this.interModeModelStorageOffset])[..Av1InterModeCandidate.Capacity];

    /// <summary>
    /// Gets the residual estimates for each block geometry in the current tile.
    /// </summary>
    public Span<Av1InterModeRateDistortionModel> InterModeModels => MemoryMarshal.Cast<int, Av1InterModeRateDistortionModel>(
        this.owner.Memory.Span.Slice(this.interModeModelStorageOffset, this.compoundSearchStorageOffset - this.interModeModelStorageOffset));

    /// <summary>
    /// Gets the predictor-pair estimates retained until the current block search ends.
    /// </summary>
    public Span<Av1CompoundSearchRecord> CompoundSearchRecords => MemoryMarshal.Cast<int, Av1CompoundSearchRecord>(
        this.owner.Memory.Span[this.compoundSearchStorageOffset..this.interpolationSearchStorageOffset]);

    /// <summary>
    /// Gets the interpolation decisions retained for the current block.
    /// </summary>
    public Span<Av1InterpolationSearchRecord> InterpolationSearchRecords => MemoryMarshal.Cast<int, Av1InterpolationSearchRecord>(
        this.owner.Memory.Span[this.interpolationSearchStorageOffset..this.singleReferenceFilterCostStorageOffset]);

    /// <summary>
    /// Gets modeled interpolation costs in single-reference mode, dynamic-reference index, and reference order.
    /// </summary>
    public Span<long> SingleReferenceFilterCosts => MemoryMarshal.Cast<int, long>(
        this.owner.Memory.Span[this.singleReferenceFilterCostStorageOffset..this.singleReferenceSimpleCostStorageOffset]);

    /// <summary>
    /// Gets translation costs in single-reference mode, dynamic-reference index, and reference order.
    /// </summary>
    public Span<long> SingleReferenceSimpleCosts => MemoryMarshal.Cast<int, long>(
        this.owner.Memory.Span[this.singleReferenceSimpleCostStorageOffset..this.simpleMotionStorageOffset]);

    /// <summary>
    /// Gets interpolation probabilities by frame role and entropy context.
    /// </summary>
    public Span<int> InterpolationProbabilities => this.owner.Memory.Span.Slice(
        this.interpolationProbabilityStorageOffset, Av1InterpolationProbabilities.ProbabilityLength);

    /// <summary>
    /// Gets the current frame's interpolation context counts.
    /// </summary>
    public Memory<int> InterpolationCounts => this.owner.Memory.Slice(
        this.interpolationProbabilityStorageOffset + Av1InterpolationProbabilities.ProbabilityLength, Av1InterpolationProbabilities.FrameLength);

    /// <summary>
    /// Gets the interpolation symbols emitted for the current frame.
    /// </summary>
    public Memory<int> SelectedInterpolationCounts => this.owner.Memory.Slice(
        this.interpolationProbabilityStorageOffset + Av1InterpolationProbabilities.ProbabilityLength + Av1InterpolationProbabilities.FrameLength,
        Av1InterpolationProbabilities.FilterCount);

    /// <summary>
    /// Gets the unwrapped frame numbers retained in the decoded reference slots.
    /// </summary>
    public Span<int> ReferenceFrameNumbers => this.owner.Memory.Span.Slice(
        this.referenceFrameNumberStorageOffset, Av1Constants.ReferenceFrameCount);

    /// <summary>
    /// Gets the base quantizer index of the frame retained in each decoded reference slot.
    /// </summary>
    public int[] ReferenceBaseQIndices { get; } = new int[Av1Constants.ReferenceFrameCount];

    /// <summary>
    /// Gets the references that the square blocks of the current superblock picked, one bit per reference type,
    /// for each 4x4 position in a 32 by 32 grid. Reference: x->picked_ref_frames_mask.
    /// </summary>
    public int[] PickedReferenceFrameMasks { get; } = new int[32 * 32];

    /// <summary>
    /// Gets the OBMC search target of the current block, one entry per luma sample. Reference: obmc_buffer.wsrc.
    /// </summary>
    public int[] ObmcWeightedSource { get; } = new int[128 * 128];

    /// <summary>
    /// Gets the OBMC prediction weights of the current block, one entry per luma sample. Reference: obmc_buffer.mask.
    /// </summary>
    public int[] ObmcMask { get; } = new int[128 * 128];

    /// <summary>
    /// Gets the luma vertical, luma horizontal, U, and V deblocking levels retained from the preceding frame.
    /// </summary>
    /// <remarks>
    /// This mirrors libaom <c>ppi-&gt;filter_level[0..1]</c>, <c>filter_level_u</c>, and <c>filter_level_v</c>,
    /// which a full-image level search of an inter frame uses as its starting point.
    /// </remarks>
    public Span<int> PreviousLoopFilterLevels => this.owner.Memory.Span.Slice(
        this.loopFilterLevelStorageOffset, LoopFilterLevelCount);

    /// <summary>
    /// Gets or sets the number of completed coded frames.
    /// </summary>
    public int EncodedFrameCount { get; set; }

    /// <summary>
    /// Gets or sets the frame rate multiplier of the preceding coded frame. Reference: td.mb.rdmult, which
    /// loopfilter_frame() sets to cpi->rd.RDMULT after each frame and the block searches restore after use.
    /// </summary>
    public int PreviousFrameRateMultiplier { get; set; }

    /// <summary>
    /// Gets the per-size mode history retained throughout one superblock row.
    /// </summary>
    public Span<int> ModeThresholdFactors => this.owner.Memory.Span.Slice(
        this.modeThresholdStorageOffset, (int)Av1BlockSize.AllSizes * Av1ModeThresholds.ModeCount);

    /// <summary>
    /// Gets or sets the quantizer-dependent base scale shared by the frame's mode thresholds.
    /// </summary>
    public int ModeThresholdQuantizerFactor { get; set; }

    /// <summary>
    /// Gets or sets the Q12 threshold multiplier used when the best mode has no residual.
    /// </summary>
    public int ModeThresholdSkipMultiplier { get; set; }

    /// <summary>
    /// Gets the worker's motion-feature nodes in breadth-first quadtree order.
    /// </summary>
    public Span<Av1SimpleMotionData> SimpleMotionData => MemoryMarshal.Cast<int, Av1SimpleMotionData>(
        this.owner.Memory.Span.Slice(this.simpleMotionStorageOffset, this.simpleMotionStorageLength));

    /// <summary>
    /// Gets the allocator used by frame-scoped encoder stages.
    /// </summary>
    public MemoryAllocator MemoryAllocator => this.Configuration.MemoryAllocator;

    /// <summary>
    /// Gets the configuration used by frame-scoped encoder buffers.
    /// </summary>
    public Configuration Configuration { get; }

    /// <summary>
    /// Gets or sets the search policy for the current picture.
    /// </summary>
    public Av1EncoderSpeedSettings SpeedSettings { get; set; }

    /// <summary>
    /// Gets or sets the encoder configuration of the current picture.
    /// </summary>
    public Av1EncoderOptions EncoderOptions { get; set; } = Av1EncoderOptions.Create(HeifEncodingSpeed.Level6);

    /// <summary>
    /// Gets or sets a value indicating whether the last transform candidate was quantized without its quantization
    /// matrices. Reference: the av1_setup_quant() call of skip_trellis_opt_based_on_satd().
    /// </summary>
    public bool CandidateMatricesDropped { get; set; }

    /// <summary>
    /// Gets or sets the quantization matrix level of the luma plane; the flat level 15 turns the matrix off.
    /// Reference: qmatrix_level_y, or NUM_QM_LEVELS - 1 when av1_use_qmatrix() is false.
    /// </summary>
    public int LumaQuantizationMatrixLevel { get; set; } = Av1ScanOrderConstants.QuantizationMatrixLevelCount - 1;

    /// <summary>
    /// Gets or sets the quantization matrix level of the chroma planes, which share one level.
    /// Reference: qmatrix_level_u.
    /// </summary>
    public int ChromaQuantizationMatrixLevel { get; set; } = Av1ScanOrderConstants.QuantizationMatrixLevelCount - 1;

    /// <summary>
    /// Gets or sets a value indicating whether the residual beyond the frame edge is filled from its visible part.
    /// Reference: cpi->do_border_pad.
    /// </summary>
    public bool BorderPad { get; set; }

    /// <summary>
    /// Gets or sets the right and bottom limits of the luma samples that a distortion measures.
    /// </summary>
    public Size LumaVisibleBoundary { get; set; }

    /// <summary>
    /// Gets or sets the right and bottom limits of the chroma samples that a distortion measures.
    /// </summary>
    public Size ChromaVisibleBoundary { get; set; }

    /// <summary>
    /// Gets or sets the current mode-evaluation stage.
    /// </summary>
    public Av1EncoderEvaluationStage EvaluationStage { get; set; }

    /// <summary>
    /// Gets reusable mode storage for partition search.
    /// </summary>
    public Av1EncoderPartitionTree PartitionTree => this.partitionTree ??= new(this.MemoryAllocator);

    /// <summary>
    /// Gets the preserved restoration stripe rows for this worker.
    /// </summary>
    public Av1LoopRestorationBoundary RestorationBoundary => this.restorationBoundary ??= new(this.MemoryAllocator);

    /// <summary>
    /// Gets the maximum-size spatial residual workspace as a compact 16-bit view of the aligned owner.
    /// </summary>
    public Span<short> Residual
        => MemoryMarshal.Cast<int, short>(this.owner.Memory.Span[..ResidualStorageLength]);

    /// <summary>
    /// Gets the maximum-size forward-transform coefficient workspace.
    /// </summary>
    public Span<int> TransformCoefficients
        => this.owner.Memory.Span.Slice(TransformCoefficientOffset, MaximumCoefficientCount);

    /// <summary>
    /// Gets the scratch storage of partition analysis, which completes before any transform of the superblock.
    /// It spans the forward and dequantized coefficient workspaces, enough for the moment tree of a 128x128
    /// superblock down to 8x8.
    /// </summary>
    public Span<int> PartitionAnalysisScratch
        => this.owner.Memory.Span.Slice(TransformCoefficientOffset, 2 * MaximumCoefficientCount);

    /// <summary>
    /// Gets the maximum-size dequantized reconstruction coefficient workspace.
    /// </summary>
    public Span<int> DequantizedCoefficients
        => this.owner.Memory.Span.Slice(DequantizedCoefficientOffset, MaximumCoefficientCount);

    /// <summary>
    /// Gets the reusable two-dimensional transform workspace.
    /// </summary>
    public Span<int> TransformWorkspace
        => this.owner.Memory.Span.Slice(TransformWorkspaceOffset, Av1TransformWorkspace.MaximumLength);

    /// <summary>
    /// Gets the reusable reference-vector stack used by inter mode decision and syntax writing.
    /// </summary>
    public ref Av1ReferenceMotionVectors ReferenceMotionVectors => ref this.referenceMotionVectors;

    /// <summary>
    /// Gets the real-time candidate state owned by this worker.
    /// </summary>
    public ref Av1EstimatedInterSearchState EstimatedInterSearchState => ref this.estimatedInterSearchState;

    /// <summary>
    /// Selects the quantization matrix levels of a frame. Reference: set_qmatrix().
    /// </summary>
    /// <param name="quantization">The frame quantization parameters.</param>
    public void SetQuantizationMatrixLevels(ObuQuantizationParameters quantization)
    {
        int flat = Av1ScanOrderConstants.QuantizationMatrixLevelCount - 1;
        bool useMatrix = quantization.IsUsingQMatrix && quantization.BaseQIndex != 0;
        this.LumaQuantizationMatrixLevel = useMatrix ? quantization.QMatrix[(int)Av1Plane.Y] : flat;
        this.ChromaQuantizationMatrixLevel = useMatrix ? quantization.QMatrix[(int)Av1Plane.U] : flat;
    }

    /// <summary>
    /// Gets the forward quantization matrix of a transform block, or an empty span for a flat matrix.
    /// Reference: av1_get_qmatrix().
    /// </summary>
    /// <param name="componentType">The luma or chroma component.</param>
    /// <param name="transformSize">The transform size.</param>
    /// <param name="transformType">The transform type; one-dimensional and identity transforms use a flat matrix.</param>
    /// <returns>The raster-order weights.</returns>
    public ReadOnlySpan<byte> GetQuantizationMatrix(Av1ComponentType componentType, Av1TransformSize transformSize, Av1TransformType transformType)
    {
        int level = componentType == Av1ComponentType.Luminance ? this.LumaQuantizationMatrixLevel : this.ChromaQuantizationMatrixLevel;
        return level >= Av1ScanOrderConstants.QuantizationMatrixLevelCount - 1 || transformType >= Av1TransformType.Identity
            ? default
            : Av1QuantizationMatrixLookup.GetQuantizationMatrix(level, componentType == Av1ComponentType.Luminance ? Av1Plane.Y : Av1Plane.U, transformSize);
    }

    /// <summary>
    /// Gets the forward quantization matrix of a transform block in the order of the weighted distortion measure, or
    /// an empty span for a flat matrix. Reference: the qmatrix and scan that av1_block_error_qm() reads.
    /// </summary>
    /// <param name="componentType">The luma or chroma component.</param>
    /// <param name="transformSize">The transform size.</param>
    /// <param name="transformType">The transform type; one-dimensional and identity transforms use a flat matrix.</param>
    /// <returns>The weight of each raster coefficient.</returns>
    public ReadOnlySpan<byte> GetDistortionWeights(Av1ComponentType componentType, Av1TransformSize transformSize, Av1TransformType transformType)
    {
        int level = componentType == Av1ComponentType.Luminance ? this.LumaQuantizationMatrixLevel : this.ChromaQuantizationMatrixLevel;
        return level >= Av1ScanOrderConstants.QuantizationMatrixLevelCount - 1 || transformType >= Av1TransformType.Identity
            ? default
            : Av1QuantizationMatrixLookup.GetDistortionWeights(level, componentType == Av1ComponentType.Luminance ? Av1Plane.Y : Av1Plane.U, transformSize);
    }

    /// <summary>
    /// Gets the inverse quantization matrix of a transform block, or an empty span for a flat matrix.
    /// Reference: av1_get_iqmatrix().
    /// </summary>
    /// <param name="componentType">The luma or chroma component.</param>
    /// <param name="transformSize">The transform size.</param>
    /// <param name="transformType">The transform type; one-dimensional and identity transforms use a flat matrix.</param>
    /// <returns>The raster-order weights.</returns>
    public ReadOnlySpan<byte> GetInverseQuantizationMatrix(Av1ComponentType componentType, Av1TransformSize transformSize, Av1TransformType transformType)
    {
        int level = componentType == Av1ComponentType.Luminance ? this.LumaQuantizationMatrixLevel : this.ChromaQuantizationMatrixLevel;
        return level >= Av1ScanOrderConstants.QuantizationMatrixLevelCount - 1 || transformType >= Av1TransformType.Identity
            ? default
            : Av1InverseQuantizationLookup.GetQuantizationMatrix(level, componentType == Av1ComponentType.Luminance ? Av1Plane.Y : Av1Plane.U, transformSize);
    }

    /// <summary>
    /// Gets the coefficient optimization weights of a transform block. Reference: the configuration reads of
    /// av1_optimize_txb().
    /// </summary>
    /// <param name="componentType">The luma or chroma component.</param>
    /// <param name="transformSize">The transform size.</param>
    /// <param name="transformType">The transform type.</param>
    /// <returns>The sharpness, rate shift and matrices.</returns>
    public Av1CoefficientOptimizationWeights GetCoefficientOptimizationWeights(
        Av1ComponentType componentType,
        Av1TransformSize transformSize,
        Av1TransformType transformType)
    {
        Av1EncoderOptions options = this.EncoderOptions;
        ReadOnlySpan<byte> distortionWeights = options.DistortionMetric == Av1DistortionMetric.QuantizationMatrixPsnr
            ? this.GetQuantizationMatrix(componentType, transformSize, transformType)
            : default;
        return new Av1CoefficientOptimizationWeights(
            options.Sharpness,
            options.Tuning == Av1Tuning.Iq ? 7 : 5,
            distortionWeights,
            this.GetInverseQuantizationMatrix(componentType, transformSize, transformType));
    }

    /// <summary>
    /// Gets the trial reconstruction reused by restoration searches in this worker.
    /// </summary>
    /// <typeparam name="TSample">The worker's fixed unsigned sample storage type.</typeparam>
    /// <param name="source">The source frame defining the worker's plane geometry.</param>
    /// <param name="colorFormat">The source plane sampling layout.</param>
    /// <returns>The reusable trial frame.</returns>
    public Av1EncoderFrame<TSample> GetRestorationTrial<TSample>(Av1EncoderFrame<TSample> source, Av1ColorFormat colorFormat)
        where TSample : unmanaged
    {
        // A worker belongs to one still image or fixed-size sequence. Its sample type and chroma
        // layout remain fixed, so the same typed frame can serve every candidate and subsequent frame.
        if (typeof(TSample) == typeof(byte))
        {
            Av1EncoderFrameBuffer<byte> trial = this.restorationByteTrial ??= new(
                this.Configuration,
                source.Width,
                source.Height,
                source.LumaBitDepth,
                colorFormat,
                source.ChromaPositionX,
                source.ChromaPositionY,
                32);

            return ((Av1EncoderFrameBuffer<TSample>)(object)trial).Frame;
        }

        Av1EncoderFrameBuffer<ushort> highBitDepthTrial = this.restorationHighBitDepthTrial ??= new(
            this.Configuration,
            source.Width,
            source.Height,
            source.LumaBitDepth,
            colorFormat,
            source.ChromaPositionX,
            source.ChromaPositionY,
            32);

        return ((Av1EncoderFrameBuffer<TSample>)(object)highBitDepthTrial).Frame;
    }

    /// <summary>
    /// Borrows the inter-motion rate tables for the current frame's precision.
    /// </summary>
    /// <param name="precision">The fractional precision selected by the frame.</param>
    /// <returns>The worker's reusable motion-rate view.</returns>
    public Av1MotionVectorCosts GetMotionVectorCosts(Av1MotionVectorPrecision precision)
        => new(this.owner.Memory.Span.Slice(StorageLength, Av1MotionVectorCosts.StorageLength), precision);

    /// <summary>
    /// Borrows the integer rates retained by a worker with intra-block-copy capacity.
    /// </summary>
    /// <returns>The worker's displacement-rate view.</returns>
    public Av1MotionVectorCosts GetDisplacementVectorCosts()
        => new(this.owner.Memory.Span.Slice(this.displacementCostStorageOffset, Av1MotionVectorCosts.IntegerStorageLength), Av1MotionVectorPrecision.Integer);

    /// <summary>
    /// Borrows prediction samples for motion search while retaining the selected inter reconstruction.
    /// </summary>
    /// <typeparam name="TSample">The frame's unsigned sample storage type.</typeparam>
    /// <returns>The reusable search prediction span.</returns>
    public Span<TSample> GetMotionSearchPrediction<TSample>()
        where TSample : unmanaged
    {
        // Motion search borrows the compound scratch before compound candidates use it. The extra
        // eight rows accommodate separable filtering of a 128x128 prediction.
        int offset = InterPredictionSampleStorageOffset + InterPredictionStorageLength;
        return MemoryMarshal.Cast<int, TSample>(this.owner.Memory.Span[offset..])[..MotionSearchPredictionSampleCount];
    }

    /// <summary>
    /// Borrows two unsigned intermediate blocks after single-reference motion search has completed.
    /// </summary>
    /// <param name="first">The first compound prediction intermediate.</param>
    /// <param name="second">The second compound prediction intermediate.</param>
    /// <remarks>The returned spans remain valid until another block operation reuses the motion-search prediction region.</remarks>
    public void GetCompoundPredictionIntermediates(out Span<ushort> first, out Span<ushort> second)
    {
        // Compound selection follows the NEWMV searches. Both full-block intermediates reuse the
        // motion-search region, with the luma blend mask immediately after them.
        int offset = InterPredictionSampleStorageOffset + InterPredictionStorageLength;
        Span<ushort> storage = MemoryMarshal.Cast<int, ushort>(this.owner.Memory.Span[offset..]);
        first = storage[..Av1EncoderInterPredictionWorkspace<ushort>.MaximumSampleCount];
        second = storage.Slice(
            Av1EncoderInterPredictionWorkspace<ushort>.MaximumSampleCount,
            Av1EncoderInterPredictionWorkspace<ushort>.MaximumSampleCount);
    }

    /// <summary>
    /// Borrows the luma-resolution compound mask storage following both intermediate blocks.
    /// </summary>
    public Span<byte> GetCompoundPredictionMask()
    {
        int offset = InterPredictionSampleStorageOffset + InterPredictionStorageLength;
        Span<byte> storage = MemoryMarshal.AsBytes(this.owner.Memory.Span[offset..]);
        int maskOffset = 2 * Av1EncoderInterPredictionWorkspace<ushort>.MaximumSampleCount * sizeof(ushort);
        return storage.Slice(maskOffset, Av1EncoderInterPredictionWorkspace<ushort>.MaximumSampleCount);
    }

    /// <summary>
    /// Borrows one inter-intra predictor and two extended reference edges after motion search has completed.
    /// </summary>
    public void GetInterIntraStorage<TSample>(
        out Span<TSample> prediction,
        out Span<TSample> above,
        out Span<TSample> left)
        where TSample : unmanaged
    {
        // Inter-intra and two-reference compound trials run only after motion search, so their temporary storage can
        // reuse the large search region without extending the per-worker allocation or aliasing retained predictions.
        int offset = InterPredictionSampleStorageOffset + InterPredictionStorageLength;
        Span<TSample> storage = MemoryMarshal.Cast<int, TSample>(this.owner.Memory.Span[offset..]);
        int sampleCount = Av1EncoderInterPredictionWorkspace<TSample>.TransformSampleCount;
        int edgeCount = (2 * Av1Constants.MaxTransformSize) + 1;
        prediction = storage[..sampleCount];
        above = storage.Slice(sampleCount, edgeCount);
        left = storage.Slice(sampleCount + edgeCount, edgeCount);
    }

    /// <summary>
    /// Gets the retained full-pixel search geometry for the reference plane's current stride.
    /// </summary>
    /// <param name="method">The block-selected search method.</param>
    /// <param name="stride">The reference row stride in samples.</param>
    /// <returns>The configured non-owning search-site view.</returns>
    public Av1MotionSearchSites GetMotionSearchSites(Av1MotionSearchSettings.FullPixelSearchMethod method, int stride)
    {
        // Fast diamond variants differ in stage selection, so they share the big-diamond geometry slot.
        Av1MotionSearchSettings.FullPixelSearchMethod shape = method > Av1MotionSearchSettings.FullPixelSearchMethod.BigDiamond
            ? Av1MotionSearchSettings.FullPixelSearchMethod.BigDiamond
            : method;

        int offset = this.motionSearchSiteStorageOffset + ((int)shape * Av1MotionSearchSites.StorageLength);
        Av1MotionSearchSites sites = new(this.owner.Memory.Span.Slice(offset, Av1MotionSearchSites.StorageLength));
        sites.Configure(shape, stride);
        return sites;
    }

    /// <summary>
    /// Gets the disjoint edge snapshot used to restore one square partition-search level.
    /// </summary>
    /// <param name="blockSize">The square partition node being evaluated.</param>
    /// <returns>The maximum-size byte view reserved for that node depth.</returns>
    public Span<byte> GetPartitionContextStorage(Av1BlockSize blockSize)
    {
        int blockSizeLog2 = Av1Math.Log2(blockSize.GetWidth());
        int slotIndex = Av1Constants.MaxSuperBlockSizeLog2 - blockSizeLog2;
        Span<int> storage = this.owner.Memory.Span.Slice(
            PartitionContextStorageOffset + (slotIndex * PartitionContextSlotLength),
            PartitionContextSlotLength);

        return MemoryMarshal.AsBytes(storage);
    }

    /// <summary>
    /// Gets the reusable storage used while comparing spatial, chroma-from-luma, filter-intra, and palette candidates.
    /// </summary>
    /// <typeparam name="TSample">The native sample type selected by the encoder pipeline.</typeparam>
    /// <returns>The typed mode-decision workspace.</returns>
    public Av1EncoderModeDecisionWorkspace<TSample> GetModeDecisionWorkspace<TSample>()
        where TSample : unmanaged
    {
        // Inter and intra searches run sequentially for each block. Their prediction and coefficient
        // scratch share this region; only retained syntax survives the transition between families.
        Span<int> storage = this.owner.Memory.Span.Slice(
            InterPredictionSampleStorageOffset,
            SharedModeDecisionStorageLength);

        return new(storage[..Av1EncoderModeDecisionWorkspace<TSample>.StorageLength]);
    }

    /// <summary>
    /// Gets the reusable storage used while comparing single-reference or intra-block-copy candidates.
    /// </summary>
    /// <typeparam name="TSample">The native sample type selected by the encoder pipeline.</typeparam>
    /// <returns>The typed inter-prediction workspace.</returns>
    public Av1EncoderInterPredictionWorkspace<TSample> GetInterPredictionWorkspace<TSample>()
        where TSample : unmanaged
    {
        Span<int> storage = this.owner.Memory.Span;
        Span<TSample> sampleStorage = MemoryMarshal
            .Cast<int, TSample>(storage.Slice(InterPredictionSampleStorageOffset, InterPredictionSampleStorageLength));

        sampleStorage = sampleStorage[
            ..((Av1EncoderInterPredictionWorkspace<TSample>.SampleBufferCount *
                Av1EncoderInterPredictionWorkspace<TSample>.MaximumSampleCount) +
                Av1EncoderInterPredictionWorkspace<TSample>.TransformSampleCount)];

        Span<short> residualStorage = MemoryMarshal
            .Cast<int, short>(storage.Slice(InterPredictionResidualStorageOffset, InterPredictionResidualStorageLength));

        residualStorage = residualStorage[..Av1EncoderInterPredictionWorkspace<TSample>.MaximumSampleCount];

        Span<short> predictionScratch = MemoryMarshal
            .Cast<int, short>(storage.Slice(InterPredictionScratchStorageOffset, InterPredictionScratchStorageLength));

        predictionScratch = predictionScratch[..Av1EncoderInterPredictionWorkspace<TSample>.PredictionScratchCount];

        Span<int> coefficientStorage = storage.Slice(
            InterPredictionCoefficientStorageOffset,
            InterPredictionCoefficientStorageLength);

        return new Av1EncoderInterPredictionWorkspace<TSample>(
            sampleStorage,
            residualStorage,
            predictionScratch,
            coefficientStorage);
    }

    /// <summary>
    /// Gets the intra winner retained while a later palette candidate reuses prediction scratch.
    /// </summary>
    /// <param name="planeCount">The number of component planes in this block.</param>
    /// <returns>The block-local syntax, transform-state, and palette storage.</returns>
    public Av1EncoderPartitionTree.ModeContext GetIntraWinnerContext(int planeCount)
    {
        // Inter search has finished before this context becomes live. Its larger shared allocation
        // leaves room beyond intra scratch for the retained winner, without another owner or allocation.
        Span<byte> storage = MemoryMarshal.AsBytes(this.owner.Memory.Span.Slice(
            InterPredictionSampleStorageOffset + ModeDecisionStorageLength,
            SharedModeDecisionStorageLength - ModeDecisionStorageLength));

        return new(storage, planeCount);
    }

    /// <summary>
    /// Releases the reusable block workspace.
    /// </summary>
    public void Dispose()
    {
        this.restorationByteTrial?.Dispose();
        this.restorationHighBitDepthTrial?.Dispose();
        this.partitionTree?.Dispose();
        this.restorationBoundary?.Dispose();
        this.owner.Dispose();
    }

    /// <summary>
    /// Gets the right and bottom limits of the samples that a distortion measures in one plane.
    /// </summary>
    /// <param name="plane">The plane.</param>
    /// <returns>The visible plane width and height.</returns>
    public Size GetVisibleBoundary(Av1Plane plane)
        => plane == Av1Plane.Y ? this.LumaVisibleBoundary : this.ChromaVisibleBoundary;

    /// <summary>
    /// Gets the number of columns and rows of a block that lie inside the visible boundary, never less than zero.
    /// Reference: get_visible_dimensions() with clip_dims set.
    /// </summary>
    /// <param name="plane">The plane.</param>
    /// <param name="origin">The block origin in plane samples.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <returns>The visible block width and height.</returns>
    public Size GetVisibleSize(Av1Plane plane, Point origin, int width, int height)
    {
        Size boundary = this.GetVisibleBoundary(plane);
        return new Size(
            Math.Clamp(boundary.Width - origin.X, 0, width),
            Math.Clamp(boundary.Height - origin.Y, 0, height));
    }
}
