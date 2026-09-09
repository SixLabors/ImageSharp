// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Owns the reusable final-block and partition decisions for one AV1 superblock.
/// </summary>
internal sealed class Av1EncoderSuperblockWorkspace : IDisposable
{
    /// <summary>
    /// The maximum number of 4x4 final blocks in a 128x128 superblock.
    /// </summary>
    public const int MaximumFinalBlockCount = 1 << (2 * (Av1Constants.MaxSuperBlockSizeLog2 - Av1Constants.ModeInfoSizeLog2));

    /// <summary>
    /// The maximum number of partition nodes in the complete 128x128 through 8x8 quadtree.
    /// </summary>
    public const int MaximumPartitionCount = 1 + 4 + 16 + 64 + 256;

    /// <summary>
    /// The decision-region length in packed final-block storage elements.
    /// </summary>
    public const int DecisionStorageLength = MaximumFinalBlockCount + ((MaximumPartitionCount + Av1EncoderBlockStruct.StorageSize - 1) / Av1EncoderBlockStruct.StorageSize);

    /// <summary>
    /// The byte length of the aligned final-block and partition decision region.
    /// </summary>
    public const int DecisionStorageByteLength = DecisionStorageLength * Av1EncoderBlockStruct.StorageSize;

    /// <summary>
    /// The complete byte length of the decision and palette-map regions.
    /// </summary>
    public const int StorageByteLength = DecisionStorageByteLength + Av1EncoderPaletteMapBuffer.StorageLength;

    private const int PartitionStorageOffset = MaximumFinalBlockCount * Av1EncoderBlockStruct.StorageSize;

    private readonly IMemoryOwner<byte> owner;
    private readonly Av1EncoderPaletteMapBuffer paletteMaps;
    private Av1EncoderPaletteInfo paletteInfo;
    private Av1ReferenceMotionVectors referenceMotionVectors;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderSuperblockWorkspace"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing the encoder allocator.</param>
    public Av1EncoderSuperblockWorkspace(Configuration configuration)
    {
        this.owner = configuration.MemoryAllocator.Allocate<byte>(StorageByteLength);
        Memory<byte> storage = this.owner.Memory[..StorageByteLength];

        // Decisions and palette maps have the same serial superblock lifetime. Keeping both regions in one
        // owner preserves their distinct layouts while removing a separate palette allocation and cleanup path.
        this.paletteMaps = new Av1EncoderPaletteMapBuffer(
            storage.Slice(DecisionStorageByteLength, Av1EncoderPaletteMapBuffer.StorageLength));

        this.Reset();
    }

    /// <summary>
    /// Gets the maximum-size final-block decision span in partition traversal order.
    /// </summary>
    public Span<Av1EncoderBlockStruct> FinalBlocks
        => MemoryMarshal.Cast<byte, Av1EncoderBlockStruct>(this.owner.Memory.Span[..DecisionStorageByteLength])[..MaximumFinalBlockCount];

    /// <summary>
    /// Gets the maximum-size partition-type span in partition-tree preorder.
    /// </summary>
    public Span<byte> PartitionTypes => this.owner.Memory.Span.Slice(PartitionStorageOffset, MaximumPartitionCount);

    /// <summary>
    /// Gets the palette sizes and colors selected for the block currently being written.
    /// </summary>
    public ref Av1EncoderPaletteInfo PaletteInfo => ref this.paletteInfo;

    /// <summary>
    /// Gets the reusable reference-vector stack used while writing inter syntax.
    /// </summary>
    public ref Av1ReferenceMotionVectors ReferenceMotionVectors => ref this.referenceMotionVectors;

    /// <summary>
    /// Gets the reusable palette maps within the superblock-workspace owner.
    /// </summary>
    /// <returns>The reusable luma and chroma palette maps.</returns>
    public Av1EncoderPaletteMapBuffer GetPaletteMaps() => this.paletteMaps;

    /// <summary>
    /// Clears all decisions before the workspace is reused for another superblock.
    /// </summary>
    public void Reset()
    {
        // Zero selects a real filter-intra kernel, so each cleared block must carry the disabled sentinel explicitly.
        Av1EncoderBlockStruct initialBlock = new()
        {
            FilterIntraMode = Av1FilterIntraMode.AllFilterIntraModes
        };

        this.FinalBlocks.Fill(initialBlock);
        this.PartitionTypes.Clear();
        this.paletteInfo = default;
    }

    /// <summary>
    /// Releases the reusable superblock workspace.
    /// </summary>
    public void Dispose()
    {
        this.paletteMaps.Dispose();
        this.owner.Dispose();
    }
}
