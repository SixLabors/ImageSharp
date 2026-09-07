// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
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
    /// The maximum number of spatial residual samples in one AV1 transform block.
    /// </summary>
    public const int MaximumResidualCount = Av1Constants.MaxTransformSize * Av1Constants.MaxTransformSize;

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
    private const int MotionSearchPredictionSampleCount = 128 * (128 + 8);
    private const int MotionSearchSiteStorageOffset = StorageLength + Av1MotionVectorCosts.StorageLength;
    private const int TransformCoefficientOffset = ResidualStorageLength;
    private const int DequantizedCoefficientOffset = TransformCoefficientOffset + MaximumCoefficientCount;
    private const int TransformWorkspaceOffset = DequantizedCoefficientOffset + MaximumCoefficientCount;
    private const int InterPredictionSampleStorageOffset = TransformWorkspaceOffset + Av1TransformWorkspace.MaximumLength;
    private const int InterPredictionSampleStorageLength =
        Av1EncoderInterPredictionWorkspace<ushort>.SampleBufferCount *
        Av1EncoderInterPredictionWorkspace<ushort>.MaximumSampleCount *
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
        Av1EncoderInterPredictionWorkspace<ushort>.CoefficientBufferCount *
        Av1EncoderInterPredictionWorkspace<ushort>.MaximumSampleCount;

    private const int InterPredictionStorageLength =
        InterPredictionSampleStorageLength +
        InterPredictionResidualStorageLength +
        InterPredictionScratchStorageLength +
        InterPredictionCoefficientStorageLength;

    private const int ModeDecisionStorageLength = Av1EncoderModeDecisionWorkspace<ushort>.StorageLength;
    private const int InterSearchStorageLength =
        InterPredictionStorageLength + (MotionSearchPredictionSampleCount * sizeof(ushort) / sizeof(int));

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
    /// Reuses the fixed-capacity reference-vector stack for every inter block in the frame.
    /// </summary>
    private Av1ReferenceMotionVectors referenceMotionVectors;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderBlockWorkspace"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing the encoder allocator.</param>
    public Av1EncoderBlockWorkspace(Configuration configuration)
        : this(configuration, allocateInterMotionCosts: false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderBlockWorkspace"/> class for a fixed encoding mode.
    /// </summary>
    /// <param name="configuration">The configuration providing the encoder allocator.</param>
    /// <param name="allocateInterMotionCosts">Whether the worker will encode inter frames.</param>
    public Av1EncoderBlockWorkspace(Configuration configuration, bool allocateInterMotionCosts)
    {
        // Motion rates belong to the worker, not a block candidate or frame. Keep both precision pairs after
        // the existing scratch regions so sequence frames can change precision while retaining one owner.
        int length = StorageLength +
            (allocateInterMotionCosts ? Av1MotionVectorCosts.StorageLength + (MotionSearchSiteCount * Av1MotionSearchSites.StorageLength) : 0);

        this.owner = configuration.MemoryAllocator.Allocate<int>(length);
        if (allocateInterMotionCosts)
        {
            // Each shape retains its offsets across frames. A zero stride marks its first use; every populated
            // site and stage is subsequently overwritten when the reference stride changes.
            Span<int> storage = this.owner.Memory.Span;
            for (int index = 0; index < MotionSearchSiteCount; index++)
            {
                storage[MotionSearchSiteStorageOffset + ((index + 1) * Av1MotionSearchSites.StorageLength) - 1] = 0;
            }
        }
    }

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
    /// Borrows the inter-motion rate tables for the current frame's precision.
    /// </summary>
    /// <param name="precision">The fractional precision selected by the frame.</param>
    /// <returns>The worker's reusable motion-rate view.</returns>
    public Av1MotionVectorCosts GetMotionVectorCosts(Av1MotionVectorPrecision precision)
        => new(this.owner.Memory.Span.Slice(StorageLength, Av1MotionVectorCosts.StorageLength), precision);

    /// <summary>
    /// Borrows prediction samples for motion search while retaining the selected inter reconstruction.
    /// </summary>
    /// <typeparam name="TSample">The frame's unsigned sample storage type.</typeparam>
    /// <returns>The reusable search prediction span.</returns>
    public Span<TSample> GetMotionSearchPrediction<TSample>()
        where TSample : unmanaged
    {
        // Intra trials have finished before inter search starts. Reuse their storage beyond the live inter
        // candidate buffers; the extra eight rows accommodate separable filtering of a 128x128 prediction.
        int offset = InterPredictionSampleStorageOffset + InterPredictionStorageLength;
        return MemoryMarshal.Cast<int, TSample>(this.owner.Memory.Span[offset..])[..MotionSearchPredictionSampleCount];
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

        int offset = MotionSearchSiteStorageOffset + ((int)shape * Av1MotionSearchSites.StorageLength);
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
        // Conventional intra search finishes before reference prediction begins for the same block.
        // Both phases can therefore reuse this aligned region without extending the owner or preserving stale scratch.
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
            ..(Av1EncoderInterPredictionWorkspace<TSample>.SampleBufferCount *
                Av1EncoderInterPredictionWorkspace<TSample>.MaximumSampleCount)];

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
    /// Releases the reusable block workspace.
    /// </summary>
    public void Dispose() => this.owner.Dispose();
}
