// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
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
    /// The complete workspace length in signed-integer storage elements.
    /// </summary>
    public const int StorageLength =
        ResidualStorageLength +
        MaximumCoefficientCount +
        MaximumCoefficientCount +
        Av1TransformWorkspace.MaximumLength +
        SharedModeDecisionStorageLength +
        PartitionContextStorageLength;

    private const int ResidualStorageLength = MaximumResidualCount / 2;
    private const int TransformCoefficientOffset = ResidualStorageLength;
    private const int DequantizedCoefficientOffset = TransformCoefficientOffset + MaximumCoefficientCount;
    private const int TransformWorkspaceOffset = DequantizedCoefficientOffset + MaximumCoefficientCount;
    private const int IntraBlockCopySampleStorageOffset = TransformWorkspaceOffset + Av1TransformWorkspace.MaximumLength;
    private const int IntraBlockCopySampleStorageLength =
        Av1EncoderIntraBlockCopyWorkspace<ushort>.SampleBufferCount *
        Av1EncoderIntraBlockCopyWorkspace<ushort>.MaximumSampleCount *
        sizeof(ushort) /
        sizeof(int);

    private const int IntraBlockCopyResidualStorageOffset =
        IntraBlockCopySampleStorageOffset + IntraBlockCopySampleStorageLength;

    private const int IntraBlockCopyResidualStorageLength =
        Av1EncoderIntraBlockCopyWorkspace<ushort>.MaximumSampleCount *
        sizeof(short) /
        sizeof(int);

    private const int IntraBlockCopyCoefficientStorageOffset =
        IntraBlockCopyResidualStorageOffset + IntraBlockCopyResidualStorageLength;

    private const int IntraBlockCopyCoefficientStorageLength =
        Av1EncoderIntraBlockCopyWorkspace<ushort>.CoefficientBufferCount *
        Av1EncoderIntraBlockCopyWorkspace<ushort>.MaximumSampleCount;

    private const int IntraBlockCopyStorageLength =
        IntraBlockCopySampleStorageLength +
        IntraBlockCopyResidualStorageLength +
        IntraBlockCopyCoefficientStorageLength;

    private const int ModeDecisionStorageLength = Av1EncoderModeDecisionWorkspace<ushort>.StorageLength;
    private const int SharedModeDecisionStorageLength = ModeDecisionStorageLength > IntraBlockCopyStorageLength
        ? ModeDecisionStorageLength
        : IntraBlockCopyStorageLength;

    private const int PartitionContextStorageOffset =
        IntraBlockCopySampleStorageOffset + SharedModeDecisionStorageLength;

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
    /// Initializes a new instance of the <see cref="Av1EncoderBlockWorkspace"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing the encoder allocator.</param>
    public Av1EncoderBlockWorkspace(Configuration configuration)
        => this.owner = configuration.MemoryAllocator.Allocate<int>(StorageLength);

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
        // Conventional intra search finishes before intra-block-copy search begins for the same block.
        // Both phases can therefore reuse this aligned region without extending the owner or preserving stale scratch.
        Span<int> storage = this.owner.Memory.Span.Slice(
            IntraBlockCopySampleStorageOffset,
            SharedModeDecisionStorageLength);

        return new(storage[..Av1EncoderModeDecisionWorkspace<TSample>.StorageLength]);
    }

    /// <summary>
    /// Gets the reusable storage used while comparing intra-block-copy candidates.
    /// </summary>
    /// <typeparam name="TSample">The native sample type selected by the encoder pipeline.</typeparam>
    /// <returns>The typed intra-block-copy workspace.</returns>
    public Av1EncoderIntraBlockCopyWorkspace<TSample> GetIntraBlockCopyWorkspace<TSample>()
        where TSample : unmanaged
    {
        Span<int> storage = this.owner.Memory.Span;
        Span<TSample> sampleStorage = MemoryMarshal
            .Cast<int, TSample>(storage.Slice(IntraBlockCopySampleStorageOffset, IntraBlockCopySampleStorageLength));

        sampleStorage = sampleStorage[
            ..(Av1EncoderIntraBlockCopyWorkspace<TSample>.SampleBufferCount *
                Av1EncoderIntraBlockCopyWorkspace<TSample>.MaximumSampleCount)];

        Span<short> residualStorage = MemoryMarshal
            .Cast<int, short>(storage.Slice(IntraBlockCopyResidualStorageOffset, IntraBlockCopyResidualStorageLength));

        residualStorage = residualStorage[..Av1EncoderIntraBlockCopyWorkspace<TSample>.MaximumSampleCount];

        Span<int> coefficientStorage = storage.Slice(
            IntraBlockCopyCoefficientStorageOffset,
            IntraBlockCopyCoefficientStorageLength);

        return new Av1EncoderIntraBlockCopyWorkspace<TSample>(
            sampleStorage,
            residualStorage,
            coefficientStorage);
    }

    /// <summary>
    /// Releases the reusable block workspace.
    /// </summary>
    public void Dispose() => this.owner.Dispose();
}
