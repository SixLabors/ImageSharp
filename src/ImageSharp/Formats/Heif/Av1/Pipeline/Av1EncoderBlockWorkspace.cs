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
    /// The largest number of 16x16 temporal dependency blocks in a superblock. A 128x128 superblock holds 8x8 of these blocks.
    /// </summary>
    public const int TplSuperblockBlockCount = 8 * 8;

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
        MaximumCoefficientCount +
        MaximumCoefficientCount +
        (2 * SearchReconstructionStorageLength) +
        Av1TransformWorkspace.MaximumLength +
        SharedModeDecisionStorageLength +
        PartitionContextStorageLength;

    private const int ResidualStorageLength = MaximumResidualCount / 2;
    private const int MotionSearchSiteCount = 6;
    private const int LoopFilterLevelCount = 4;
    private const int MotionSearchPredictionSampleCount = 128 * (128 + 8);
    private const int TransformCoefficientOffset = ResidualStorageLength;
    private const int DequantizedCoefficientOffset = TransformCoefficientOffset + MaximumCoefficientCount;
    private const int SearchDequantizedCoefficientOffset = DequantizedCoefficientOffset + MaximumCoefficientCount;
    private const int SearchCoefficientOffset = SearchDequantizedCoefficientOffset + MaximumCoefficientCount;
    private const int SearchReconstructionOffset = SearchCoefficientOffset + MaximumCoefficientCount;
    private const int SearchReconstructionSampleCount = Av1Constants.MaxTransformSize * Av1Constants.MaxTransformSize;
    private const int SearchReconstructionStorageLength = SearchReconstructionSampleCount * sizeof(ushort) / sizeof(int);
    private const int TransformWorkspaceOffset = SearchReconstructionOffset + (2 * SearchReconstructionStorageLength);
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

    private const int InterFilterRowStorageOffset =
        InterPredictionResidualStorageOffset + InterPredictionResidualStorageLength;

    private const int InterFilterRowStorageLength =
        Av1EncoderInterPredictionWorkspace<ushort>.FilterRowCount *
        sizeof(short) /
        sizeof(int);

    private const int InterPredictionCoefficientStorageOffset =
        InterFilterRowStorageOffset + InterFilterRowStorageLength;

    private const int InterPredictionCoefficientStorageLength =
        (Av1EncoderInterPredictionWorkspace<ushort>.CoefficientBufferCount *
        Av1EncoderInterPredictionWorkspace<ushort>.MaximumSampleCount) +
        Av1EncoderInterPredictionWorkspace<ushort>.TransformSampleCount +
        (4 * (1 << (Av1Constants.MaxSuperBlockSizeLog2 - Av1Constants.ModeInfoSizeLog2)) / sizeof(int));

    private const int InterPredictionStorageLength =
        InterPredictionSampleStorageLength +
        InterPredictionResidualStorageLength +
        InterFilterRowStorageLength +
        InterPredictionCoefficientStorageLength;

    private const int ModeDecisionStorageLength = Av1EncoderModeDecisionWorkspace<ushort>.StorageLength;

    // Motion search and compound evaluation run one after the other. The larger compound region holds two unsigned predictions and a byte mask.
    // Transform trials borrow its prediction area.
    private const int CompoundPredictionStorageLength =
        Av1EncoderInterPredictionWorkspace<ushort>.MaximumSampleCount * ((2 * sizeof(ushort)) + sizeof(byte)) / sizeof(int);

    private const int InterSearchStorageLength = InterPredictionStorageLength + CompoundPredictionStorageLength;

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

    private readonly int[] modeThresholdQuantizerFactors = new int[Av1Constants.MaxSegmentCount];

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

        // Motion rates belong to the worker, not to a block candidate or a frame. Both precision pairs follow the block search regions,
        // so sequence frames can change precision with one owner. Intra block copy appends its own integer pair at the end of the same allocation.
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

        // The levels that seed the next search, then the levels the last frame header coded.
        this.loopFilterLevelStorageOffset = length;
        length += 2 * LoopFilterLevelCount;
        this.modeThresholdStorageOffset = length;
        if (allocateInterMotionCosts)
        {
            length += (int)Av1BlockSize.AllSizes * Av1ModeThresholds.ModeCount;
        }

        // Prediction records survive all mode trials in a block. Fitted models survive superblocks until the tile ends.
        // Both stay outside the prediction and coefficient regions that transforms overwrite.
        this.interModeStorageOffset = (length + 1) & ~1;
        this.interModeModelStorageOffset = this.interModeStorageOffset +
            ((((Av1InterModeCandidate.Capacity * Unsafe.SizeOf<Av1InterModeCandidate>()) + 7) & ~7) / sizeof(int));

        this.compoundSearchStorageOffset = this.interModeModelStorageOffset +
            ((int)Av1BlockSize.AllSizes * Unsafe.SizeOf<Av1InterModeRateDistortionModel>() / sizeof(int));

        this.interpolationSearchStorageOffset = this.compoundSearchStorageOffset +
            (Av1CompoundSearchRecord.Capacity * Unsafe.SizeOf<Av1CompoundSearchRecord>() / sizeof(int));

        this.singleReferenceFilterCostStorageOffset = this.interpolationSearchStorageOffset +
            (Av1InterpolationSearchRecord.Capacity * Unsafe.SizeOf<Av1InterpolationSearchRecord>() / sizeof(int));

        // Modeled filter costs and complete translation costs use the same index order: mode, DRL index, then reference.
        // Both survive compound trials. The next coding block resets them.
        int singleReferenceCostLength = 4 * 3 * Av1Constants.ReferenceFrameCount * sizeof(long) / sizeof(int);
        this.singleReferenceSimpleCostStorageOffset = this.singleReferenceFilterCostStorageOffset + singleReferenceCostLength;
        if (allocateInterMotionCosts)
        {
            length = this.singleReferenceSimpleCostStorageOffset + singleReferenceCostLength;
        }

        this.simpleMotionStorageOffset = length;
        if (allocateInterMotionCosts)
        {
            // Include the 4x4 leaves that the simple-motion split features use, although these leaves own no further partition search.
            // The superblock geometry fixes this storage for the lifetime of the worker. A full quad tree over the leaves has (4 * leaves - 1) / 3 nodes.
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
        this.CodedLoopFilterLevels.Clear();
        if (allocateSearchSites)
        {
            // Each shape retains its offsets across frames. A zero stride marks its first use.
            // When the reference stride changes, the search writes every populated site and stage again.
            Span<int> storage = this.owner.Memory.Span;
            for (int index = 0; index < MotionSearchSiteCount; index++)
            {
                storage[this.motionSearchSiteStorageOffset + ((index + 1) * Av1MotionSearchSites.StorageLength) - 1] = 0;
            }
        }
    }

    /// <summary>
    /// Gets the storage that holds every search buffer of the workspace. A caller reads it once per frame pass and
    /// passes it to the overloads that slice one buffer from it.
    /// </summary>
    public Span<int> Storage => this.owner.Memory.Span;

    /// <summary>
    /// Gets the source log variances for the current superblock's 4x4 cells.
    /// </summary>
    public Span<double> SourceLogVariances => this.GetSourceLogVariances(this.owner.Memory.Span);

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
    /// Gets the residual estimates for each block geometry in the current tile.
    /// </summary>
    public Span<Av1InterModeRateDistortionModel> InterModeModels => this.GetInterModeModels(this.owner.Memory.Span);

    /// <summary>
    /// Gets interpolation probabilities by frame role and entropy context.
    /// </summary>
    public Span<int> InterpolationProbabilities => this.GetInterpolationProbabilities(this.owner.Memory.Span);

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
    /// Gets the unwrapped display order of the frame retained in each decoded reference slot.
    /// </summary>
    public Span<int> ReferenceFrameNumbers => this.GetReferenceFrameNumbers(this.owner.Memory.Span);

    /// <summary>
    /// Gets the base quantizer index of the frame retained in each decoded reference slot.
    /// </summary>
    public int[] ReferenceBaseQIndices { get; } = new int[Av1Constants.ReferenceFrameCount];

    /// <summary>
    /// Gets the luma transform search results of the recent inter blocks of the superblock.
    /// </summary>
    public Av1MacroblockRateDistortionRecord MacroblockRateDistortionRecord { get; } = new();

    /// <summary>
    /// Gets the references that the square blocks of the current superblock picked, one bit per reference type,
    /// for each 4x4 position in a 32 by 32 grid.
    /// </summary>
    public int[] PickedReferenceFrameMasks { get; } = new int[32 * 32];

    /// <summary>
    /// Gets the OBMC search target of the current block, one entry per luma sample.
    /// </summary>
    public int[] ObmcWeightedSource { get; } = new int[128 * 128];

    /// <summary>
    /// Gets the OBMC prediction weights of the current block, one entry per luma sample.
    /// </summary>
    public int[] ObmcMask { get; } = new int[128 * 128];

    /// <summary>
    /// Gets the average-removed subsampled luma that the chroma mode search of the current intra block used for chroma-from-luma prediction.
    /// The samples use the fixed stride of the chroma-from-luma buffer. The winner refinement of an intra block in an inter frame predicts from them again.
    /// The encoder stores luma for chroma-from-luma only before the chroma mode search, and the winner refinement does not store it again.
    /// </summary>
    public short[] ChromaFromLumaSearchSamples { get; } = new short[Prediction.ChromaFromLuma.Av1ChromaFromLumaContext.BufferLength];

    /// <summary>
    /// Gets the luma vertical, luma horizontal, U, and V deblocking levels retained from the preceding frame.
    /// </summary>
    /// <remarks>
    /// A full-image level search of an inter frame starts from these levels.
    /// </remarks>
    public Span<int> PreviousLoopFilterLevels => this.owner.Memory.Span.Slice(
        this.loopFilterLevelStorageOffset, LoopFilterLevelCount);

    /// <summary>
    /// Gets the luma vertical, luma horizontal, U, and V deblocking levels that the last coded frame header carried.
    /// </summary>
    /// <remarks>
    /// The levels stay from the loop filter of one frame to the loop filter of the next frame. The measuring pack of the recode loop writes them.
    /// </remarks>
    public Span<int> CodedLoopFilterLevels => this.owner.Memory.Span.Slice(
        this.loopFilterLevelStorageOffset + LoopFilterLevelCount, LoopFilterLevelCount);

    /// <summary>
    /// Gets or sets the unwrapped display order of the current frame. Without a lookahead, it is the number of completed coded frames.
    /// </summary>
    public int EncodedFrameCount { get; set; }

    /// <summary>
    /// Gets or sets the number of frames shown before the current frame. Without a lookahead, it equals <see cref="EncodedFrameCount"/>.
    /// </summary>
    public int FrameNumber { get; set; }

    /// <summary>
    /// Gets or sets the frame rate multiplier of the preceding coded frame. The loop filter stage sets it after each frame.
    /// The block searches restore it after use.
    /// </summary>
    public int PreviousFrameRateMultiplier { get; set; }

    /// <summary>
    /// Gets or sets the regularized importance of the latest superblock whose temporal dependency statistics gave one.
    /// It persists across superblocks and frames. It starts at zero, which leaves the rate multipliers of coding blocks unscaled.
    /// </summary>
    public double RegularizedImportance { get; set; }

    /// <summary>
    /// Gets or sets the rate multiplier that the motion vector error per bit derives from. It persists across blocks, superblocks and frames.
    /// The frame setup, the superblock quantizer setup, the block setups of the SSIM tunes and each block mode search set it.
    /// The simple motion searches of the partition search read the value set last.
    /// </summary>
    public int ErrorPerBitRateMultiplier { get; set; }

    /// <summary>
    /// Gets the temporal dependency inter cost of each 16x16 block of the current superblock, scaled by sixteen.
    /// The costs are in raster order with the block stride of the superblock.
    /// </summary>
    public long[] TplSuperblockInterCosts { get; } = new long[TplSuperblockBlockCount];

    /// <summary>
    /// Gets the temporal dependency intra cost of each 16x16 block of the current superblock, scaled by sixteen,
    /// at the indices of <see cref="TplSuperblockInterCosts"/>.
    /// </summary>
    public long[] TplSuperblockIntraCosts { get; } = new long[TplSuperblockBlockCount];

    /// <summary>
    /// Gets the temporal dependency motion vectors of each 16x16 block of the current superblock.
    /// Each block has seven vectors, one for each reference from LAST to ALTREF.
    /// </summary>
    public Av1MotionVector[] TplSuperblockVectors { get; } =
        new Av1MotionVector[TplSuperblockBlockCount * Tpl.Av1TplModelConstants.InterReferenceCount];

    /// <summary>
    /// Gets the per-size mode history retained throughout one superblock row.
    /// </summary>
    public Span<int> ModeThresholdFactors => this.GetModeThresholdFactors(this.owner.Memory.Span);

    /// <summary>
    /// Gets the quantizer-dependent base scale of the mode thresholds of the frame, one per segment.
    /// </summary>
    public Span<int> ModeThresholdQuantizerFactors => this.modeThresholdQuantizerFactors;

    /// <summary>
    /// Gets or sets the variance segment that the last 16x16 partition node measured. The blocks of 16x16 and smaller take this segment.
    /// It keeps its value across superblocks and frames.
    /// </summary>
    public int MacroblockEnergy { get; set; }

    /// <summary>
    /// Gets or sets the block size of the measure in <see cref="SubBlockEnergyDifference"/>. It keeps its value across blocks and frames.
    /// So a later block of the same size uses the stored difference again. It starts as the smallest block size.
    /// </summary>
    public Av1BlockSize SubBlockEnergyBlockSize { get; set; }

    /// <summary>
    /// Gets or sets the sub-block energy difference measured for <see cref="SubBlockEnergyBlockSize"/>.
    /// </summary>
    public int SubBlockEnergyDifference { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the luma in the transform is a noise pattern.
    /// At high bit depth sharpness 3, a noise pattern is a block destination that is smoother than a source with low detail.
    /// The block search sets this value for the residual trials that it measures. A noise pattern keeps more coefficients in the coefficient optimization.
    /// </summary>
    public bool LumaNoisePattern { get; set; }

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
    public Av1EncoderOptions EncoderOptions { get; set; } =
        Av1EncoderOptions.Create(HeifEncodingSpeed.Level6, Av1Tuning.Psnr, enableRestoration: true, allIntra: true);

    /// <summary>
    /// Gets or sets a value indicating whether the last transform candidate was quantized without its quantization matrices.
    /// The SATD gate of the coefficient optimization can drop the matrices.
    /// </summary>
    public bool CandidateMatricesDropped { get; set; }

    /// <summary>
    /// Gets or sets the quantization matrix level of the luma plane. The flat level 15 turns the matrix off.
    /// A frame without quantization matrices and a lossless segment use the flat level.
    /// </summary>
    public int LumaQuantizationMatrixLevel { get; set; } = Av1ScanOrderConstants.QuantizationMatrixLevelCount - 1;

    /// <summary>
    /// Gets or sets the quantization matrix level of the chroma planes, which share one level.
    /// </summary>
    public int ChromaQuantizationMatrixLevel { get; set; } = Av1ScanOrderConstants.QuantizationMatrixLevelCount - 1;

    /// <summary>
    /// Gets or sets a value indicating whether the residual beyond the frame edge is filled from its visible part.
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
    /// Gets the coefficients of one row of mode-estimation transforms across the widest block, eight 16x16 transforms of a 128-sample row.
    /// It spans the forward and dequantized coefficient workspaces.
    /// </summary>
    public Span<int> EstimationRowCoefficients
        => this.owner.Memory.Span.Slice(TransformCoefficientOffset, 2 * MaximumCoefficientCount);

    /// <summary>
    /// Gets the maximum-size dequantized reconstruction coefficient workspace.
    /// </summary>
    public Span<int> DequantizedCoefficients
        => this.owner.Memory.Span.Slice(DequantizedCoefficientOffset, MaximumCoefficientCount);

    /// <summary>
    /// Gets the dequantized coefficients of the best candidate of a transform type search, which the search swaps
    /// with <see cref="DequantizedCoefficients"/> on each improvement. Mode estimation reconstructs each of its
    /// transforms here, because its row of coefficients spans <see cref="DequantizedCoefficients"/>.
    /// </summary>
    public Span<int> SearchDequantizedCoefficients
        => this.owner.Memory.Span.Slice(SearchDequantizedCoefficientOffset, MaximumCoefficientCount);

    /// <summary>
    /// Gets the quantized coefficients of the best candidate of a transform type search whose caller has no
    /// spare coefficient storage of its own.
    /// </summary>
    public Span<int> SearchCoefficients
        => this.owner.Memory.Span.Slice(SearchCoefficientOffset, MaximumCoefficientCount);

    /// <summary>
    /// Gets the storage of both transform type search reconstructions, which a caller reads once for its loop.
    /// </summary>
    public Span<int> SearchReconstructions
        => this.owner.Memory.Span.Slice(SearchReconstructionOffset, 2 * SearchReconstructionStorageLength);

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
    /// Gets one of the two transform type search reconstructions from their storage.
    /// </summary>
    /// <typeparam name="TSample">The reconstructed sample type.</typeparam>
    /// <param name="searchReconstructions">The storage from <see cref="SearchReconstructions"/>.</param>
    /// <param name="index">The reconstruction slot, zero or one.</param>
    /// <returns>The reconstruction storage.</returns>
    public static Span<TSample> GetSearchReconstruction<TSample>(Span<int> searchReconstructions, int index)
        where TSample : unmanaged
        => MemoryMarshal.Cast<int, TSample>(
            searchReconstructions.Slice(index * SearchReconstructionStorageLength, SearchReconstructionStorageLength))[..SearchReconstructionSampleCount];

    /// <summary>
    /// Selects the quantization matrix levels of a segment. A lossless segment always uses the flat level.
    /// </summary>
    /// <param name="quantization">The frame quantization parameters.</param>
    /// <param name="lossless">Whether the segment of the block is lossless.</param>
    public void SetQuantizationMatrixLevels(ObuQuantizationParameters quantization, bool lossless)
    {
        int flat = Av1ScanOrderConstants.QuantizationMatrixLevelCount - 1;
        bool useMatrix = quantization.IsUsingQMatrix && !lossless;
        this.LumaQuantizationMatrixLevel = useMatrix ? quantization.QMatrix[(int)Av1Plane.Y] : flat;
        this.ChromaQuantizationMatrixLevel = useMatrix ? quantization.QMatrix[(int)Av1Plane.U] : flat;
    }

    /// <summary>
    /// Gets the forward quantization matrix of a transform block, or an empty span for a flat matrix.
    /// </summary>
    /// <param name="componentType">The luma or chroma component.</param>
    /// <param name="transformSize">The transform size.</param>
    /// <param name="transformType">The transform type. One-dimensional and identity transforms use a flat matrix.</param>
    /// <returns>The raster-order weights.</returns>
    public ReadOnlySpan<byte> GetQuantizationMatrix(Av1ComponentType componentType, Av1TransformSize transformSize, Av1TransformType transformType)
    {
        int level = componentType == Av1ComponentType.Luminance ? this.LumaQuantizationMatrixLevel : this.ChromaQuantizationMatrixLevel;
        return level >= Av1ScanOrderConstants.QuantizationMatrixLevelCount - 1 || transformType >= Av1TransformType.Identity
            ? default
            : Av1QuantizationMatrixLookup.GetQuantizationMatrix(level, componentType == Av1ComponentType.Luminance ? Av1Plane.Y : Av1Plane.U, transformSize);
    }

    /// <summary>
    /// Gets the forward quantization matrix of a transform block in the order of the weighted distortion measure,
    /// or an empty span for a flat matrix.
    /// </summary>
    /// <param name="componentType">The luma or chroma component.</param>
    /// <param name="transformSize">The transform size.</param>
    /// <param name="transformType">The transform type. One-dimensional and identity transforms use a flat matrix.</param>
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
    /// </summary>
    /// <param name="componentType">The luma or chroma component.</param>
    /// <param name="transformSize">The transform size.</param>
    /// <param name="transformType">The transform type. One-dimensional and identity transforms use a flat matrix.</param>
    /// <returns>The raster-order weights.</returns>
    public ReadOnlySpan<byte> GetInverseQuantizationMatrix(Av1ComponentType componentType, Av1TransformSize transformSize, Av1TransformType transformType)
    {
        int level = componentType == Av1ComponentType.Luminance ? this.LumaQuantizationMatrixLevel : this.ChromaQuantizationMatrixLevel;
        return level >= Av1ScanOrderConstants.QuantizationMatrixLevelCount - 1 || transformType >= Av1TransformType.Identity
            ? default
            : Av1InverseQuantizationLookup.GetQuantizationMatrix(level, componentType == Av1ComponentType.Luminance ? Av1Plane.Y : Av1Plane.U, transformSize);
    }

    /// <summary>
    /// Gets the coefficient optimization weights of a transform block from the encoder options and the quantization matrix levels.
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

        // A luma noise pattern keeps more coefficients. It uses a larger rate shift and a larger minimum end-of-block cutoff.
        // The rate shift of the image tunes has priority over the noise pattern.
        bool noisePattern = componentType == Av1ComponentType.Luminance && this.LumaNoisePattern;
        int rateShift = options.Tuning.IsImageTuning() ? 7 : noisePattern ? 6 : 5;
        ReadOnlySpan<byte> inverseWeights = this.GetInverseQuantizationMatrix(componentType, transformSize, transformType);
        return new Av1CoefficientOptimizationWeights(options.Sharpness, rateShift, noisePattern ? 8 : 5, distortionWeights, inverseWeights);
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
        // A worker belongs to one still image or sequence. Its sample type and chroma layout remain fixed, so the
        // same typed frame serves every candidate and subsequent frame of its size. The layers of a layered image
        // have their own sizes, and a layer of another size replaces the frame.
        if (typeof(TSample) == typeof(byte))
        {
            if (this.restorationByteTrial is { } previous &&
                (previous.Frame.Width != source.Width || previous.Frame.Height != source.Height))
            {
                previous.Dispose();
                this.restorationByteTrial = null;
            }

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

        if (this.restorationHighBitDepthTrial is { } previousHighBitDepth &&
            (previousHighBitDepth.Frame.Width != source.Width || previousHighBitDepth.Frame.Height != source.Height))
        {
            previousHighBitDepth.Dispose();
            this.restorationHighBitDepthTrial = null;
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
    /// Borrows the integer rates retained by a worker with intra-block-copy capacity from the workspace storage.
    /// </summary>
    /// <param name="storage">The storage of the workspace, from <see cref="Storage"/>.</param>
    /// <returns>The worker's displacement-rate view.</returns>
    public Av1MotionVectorCosts GetDisplacementVectorCosts(Span<int> storage)
        => new(storage.Slice(this.displacementCostStorageOffset, Av1MotionVectorCosts.IntegerStorageLength), Av1MotionVectorPrecision.Integer);

    /// <summary>
    /// Borrows prediction samples for motion search while retaining the selected inter reconstruction.
    /// </summary>
    /// <typeparam name="TSample">The frame's unsigned sample storage type.</typeparam>
    /// <returns>The reusable search prediction span.</returns>
    public Span<TSample> GetMotionSearchPrediction<TSample>()
        where TSample : unmanaged
    {
        // Motion search borrows the compound prediction region before compound candidates use it. The extra
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
    /// <returns>The compound mask storage, one byte per luma sample of the largest block.</returns>
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
    /// <typeparam name="TSample">The unsigned sample storage type of the frame.</typeparam>
    /// <param name="prediction">Receives the predictor storage for the largest transform block.</param>
    /// <param name="above">Receives the storage of the above edge.</param>
    /// <param name="left">Receives the storage of the left edge.</param>
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
        => this.GetMotionSearchSites(this.owner.Memory.Span, method, stride);

    /// <summary>
    /// Gets the retained full-pixel search geometry for the reference plane's current stride from the workspace storage.
    /// </summary>
    /// <param name="storage">The storage of the workspace, from <see cref="Storage"/>.</param>
    /// <param name="method">The block-selected search method.</param>
    /// <param name="stride">The reference row stride in samples.</param>
    /// <returns>The configured non-owning search-site view.</returns>
    public Av1MotionSearchSites GetMotionSearchSites(Span<int> storage, Av1MotionSearchSettings.FullPixelSearchMethod method, int stride)
    {
        // Fast diamond variants differ in stage selection, so they share the big-diamond geometry slot.
        Av1MotionSearchSettings.FullPixelSearchMethod shape = method > Av1MotionSearchSettings.FullPixelSearchMethod.BigDiamond
            ? Av1MotionSearchSettings.FullPixelSearchMethod.BigDiamond
            : method;

        int offset = this.motionSearchSiteStorageOffset + ((int)shape * Av1MotionSearchSites.StorageLength);
        Av1MotionSearchSites sites = new(storage.Slice(offset, Av1MotionSearchSites.StorageLength));
        sites.Configure(shape, stride);
        return sites;
    }

    /// <summary>
    /// Gets the disjoint edge snapshot used to restore one square partition-search level from the workspace storage.
    /// </summary>
    /// <param name="storage">The storage of the workspace, from <see cref="Storage"/>.</param>
    /// <param name="blockSize">The square partition node being evaluated.</param>
    /// <returns>The maximum-size byte view reserved for that node depth.</returns>
    public static Span<byte> GetPartitionContextStorage(Span<int> storage, Av1BlockSize blockSize)
    {
        // Each square depth has its own slot, so a deeper level never overwrites the snapshot of its parent.
        int blockSizeLog2 = Av1Math.Log2(blockSize.GetWidth());
        int slotIndex = Av1Constants.MaxSuperBlockSizeLog2 - blockSizeLog2;
        return MemoryMarshal.AsBytes(storage.Slice(PartitionContextStorageOffset + (slotIndex * PartitionContextSlotLength), PartitionContextSlotLength));
    }

    /// <summary>
    /// Gets the reusable storage used while comparing spatial, chroma-from-luma, filter-intra, and palette candidates.
    /// </summary>
    /// <typeparam name="TSample">The native sample type selected by the encoder pipeline.</typeparam>
    /// <returns>The typed mode-decision workspace.</returns>
    public Av1EncoderModeDecisionWorkspace<TSample> GetModeDecisionWorkspace<TSample>()
        where TSample : unmanaged
    {
        // Inter and intra searches run one after the other for each block. Their predictions and coefficients share this region.
        // Only the retained syntax survives the transition between the two search families.
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

        Span<short> filterRowStorage = MemoryMarshal.Cast<int, short>(storage.Slice(InterFilterRowStorageOffset, InterFilterRowStorageLength));

        filterRowStorage = filterRowStorage[..Av1EncoderInterPredictionWorkspace<TSample>.FilterRowCount];

        Span<int> coefficientStorage = storage.Slice(
            InterPredictionCoefficientStorageOffset,
            InterPredictionCoefficientStorageLength);

        return new Av1EncoderInterPredictionWorkspace<TSample>(sampleStorage, residualStorage, filterRowStorage, coefficientStorage);
    }

    /// <summary>
    /// Gets the intra winner retained while a later palette candidate reuses the prediction storage, from the workspace storage.
    /// </summary>
    /// <param name="storage">The storage of the workspace, from <see cref="Storage"/>.</param>
    /// <param name="planeCount">The number of component planes in this block.</param>
    /// <returns>The block-local syntax, transform-state, and palette storage.</returns>
    public static Av1EncoderPartitionTree.ModeContext GetIntraWinnerContext(Span<int> storage, int planeCount)
    {
        // Inter search has finished before this context becomes live. Its larger shared allocation
        // leaves room beyond the intra mode-decision storage for the retained winner, without another owner or allocation.
        Span<byte> winnerStorage = MemoryMarshal.AsBytes(storage.Slice(
            InterPredictionSampleStorageOffset + ModeDecisionStorageLength,
            SharedModeDecisionStorageLength - ModeDecisionStorageLength));

        return new(winnerStorage, planeCount);
    }

    /// <summary>
    /// Gets the four convolution outputs retained throughout a 64x64 partition search from the workspace storage.
    /// </summary>
    /// <param name="storage">The storage of the workspace, from <see cref="Storage"/>.</param>
    /// <returns>The retained convolution outputs.</returns>
    public Span<float> GetIntraPartitionFeatures(Span<int> storage)
        => MemoryMarshal.Cast<int, float>(storage.Slice(this.intraPartitionStorageOffset, 20 + (4 * 2 * 2) + (20 * 4 * 4) + (20 * 8 * 8)));

    /// <summary>
    /// Gets the source log variances for the current superblock's 4x4 cells from the workspace storage.
    /// </summary>
    /// <param name="storage">The storage of the workspace, from <see cref="Storage"/>.</param>
    /// <returns>The source log variances, negative for a cell that is not measured yet.</returns>
    public Span<double> GetSourceLogVariances(Span<int> storage) => MemoryMarshal.Cast<int, double>(storage[this.sourceLogVarianceStorageOffset..]);

    /// <summary>
    /// Gets temporary storage for the 65x65 normalized input and the first 20-channel 16x16 layer from the workspace storage.
    /// </summary>
    /// <param name="storage">The storage of the workspace, from <see cref="Storage"/>.</param>
    /// <returns>The temporary convolution storage.</returns>
    public static Span<float> GetIntraPartitionConvolutionStorage(Span<int> storage)
        => MemoryMarshal.Cast<int, float>(storage.Slice(InterPredictionSampleStorageOffset, (65 * 65) + (20 * 16 * 16)));

    /// <summary>
    /// Gets the prediction records retained until the block's transform search finishes, from the workspace storage.
    /// </summary>
    /// <param name="storage">The storage of the workspace, from <see cref="Storage"/>.</param>
    /// <returns>The prediction records.</returns>
    public Span<Av1InterModeCandidate> GetInterModeCandidates(Span<int> storage)
        => MemoryMarshal.Cast<int, Av1InterModeCandidate>(
            storage[this.interModeStorageOffset..this.interModeModelStorageOffset])[..Av1InterModeCandidate.Capacity];

    /// <summary>
    /// Gets the residual estimates for each block geometry in the current tile from the workspace storage.
    /// </summary>
    /// <param name="storage">The storage of the workspace, from <see cref="Storage"/>.</param>
    /// <returns>The residual estimates, one per block size.</returns>
    public Span<Av1InterModeRateDistortionModel> GetInterModeModels(Span<int> storage)
        => MemoryMarshal.Cast<int, Av1InterModeRateDistortionModel>(
            storage.Slice(this.interModeModelStorageOffset, this.compoundSearchStorageOffset - this.interModeModelStorageOffset));

    /// <summary>
    /// Gets the predictor-pair estimates retained until the current block search ends, from the workspace storage.
    /// </summary>
    /// <param name="storage">The storage of the workspace, from <see cref="Storage"/>.</param>
    /// <returns>The predictor-pair estimates.</returns>
    public Span<Av1CompoundSearchRecord> GetCompoundSearchRecords(Span<int> storage)
        => MemoryMarshal.Cast<int, Av1CompoundSearchRecord>(storage[this.compoundSearchStorageOffset..this.interpolationSearchStorageOffset]);

    /// <summary>
    /// Gets the interpolation decisions retained for the current block from the workspace storage.
    /// </summary>
    /// <param name="storage">The storage of the workspace, from <see cref="Storage"/>.</param>
    /// <returns>The interpolation decisions.</returns>
    public Span<Av1InterpolationSearchRecord> GetInterpolationSearchRecords(Span<int> storage)
        => MemoryMarshal.Cast<int, Av1InterpolationSearchRecord>(storage[this.interpolationSearchStorageOffset..this.singleReferenceFilterCostStorageOffset]);

    /// <summary>
    /// Gets modeled interpolation costs in single-reference mode, dynamic-reference index, and reference order from the workspace storage.
    /// </summary>
    /// <param name="storage">The storage of the workspace, from <see cref="Storage"/>.</param>
    /// <returns>The modeled interpolation costs.</returns>
    public Span<long> GetSingleReferenceFilterCosts(Span<int> storage)
        => MemoryMarshal.Cast<int, long>(storage[this.singleReferenceFilterCostStorageOffset..this.singleReferenceSimpleCostStorageOffset]);

    /// <summary>
    /// Gets translation costs in single-reference mode, dynamic-reference index, and reference order from the workspace storage.
    /// </summary>
    /// <param name="storage">The storage of the workspace, from <see cref="Storage"/>.</param>
    /// <returns>The translation costs.</returns>
    public Span<long> GetSingleReferenceSimpleCosts(Span<int> storage)
        => MemoryMarshal.Cast<int, long>(storage[this.singleReferenceSimpleCostStorageOffset..this.simpleMotionStorageOffset]);

    /// <summary>
    /// Gets interpolation probabilities by frame role and entropy context from the workspace storage.
    /// </summary>
    /// <param name="storage">The storage of the workspace, from <see cref="Storage"/>.</param>
    /// <returns>The interpolation probabilities.</returns>
    public Span<int> GetInterpolationProbabilities(Span<int> storage)
        => storage.Slice(this.interpolationProbabilityStorageOffset, Av1InterpolationProbabilities.ProbabilityLength);

    /// <summary>
    /// Gets the unwrapped display order of the frame retained in each decoded reference slot from the workspace storage.
    /// </summary>
    /// <param name="storage">The storage of the workspace, from <see cref="Storage"/>.</param>
    /// <returns>The display order of each reference slot.</returns>
    public Span<int> GetReferenceFrameNumbers(Span<int> storage) => storage.Slice(this.referenceFrameNumberStorageOffset, Av1Constants.ReferenceFrameCount);

    /// <summary>
    /// Gets the per-size mode history retained throughout one superblock row from the workspace storage.
    /// </summary>
    /// <param name="storage">The storage of the workspace, from <see cref="Storage"/>.</param>
    /// <returns>The mode threshold factors, one per mode of each block size.</returns>
    public Span<int> GetModeThresholdFactors(Span<int> storage)
        => storage.Slice(this.modeThresholdStorageOffset, (int)Av1BlockSize.AllSizes * Av1ModeThresholds.ModeCount);

    /// <summary>
    /// Gets the worker's motion-feature nodes in breadth-first quadtree order from the workspace storage.
    /// </summary>
    /// <param name="storage">The storage of the workspace, from <see cref="Storage"/>.</param>
    /// <returns>The motion-feature nodes.</returns>
    public Span<Av1SimpleMotionData> GetSimpleMotionData(Span<int> storage)
        => MemoryMarshal.Cast<int, Av1SimpleMotionData>(storage.Slice(this.simpleMotionStorageOffset, this.simpleMotionStorageLength));

    /// <summary>
    /// Gets the variance tree storage of partition analysis from the workspace storage. Partition analysis completes before
    /// any transform of the superblock, so it spans the forward and dequantized coefficient workspaces.
    /// </summary>
    /// <param name="storage">The storage of the workspace, from <see cref="Storage"/>.</param>
    /// <returns>The partition analysis storage.</returns>
    public static Span<int> GetPartitionAnalysisStorage(Span<int> storage) => storage.Slice(TransformCoefficientOffset, 2 * MaximumCoefficientCount);

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
