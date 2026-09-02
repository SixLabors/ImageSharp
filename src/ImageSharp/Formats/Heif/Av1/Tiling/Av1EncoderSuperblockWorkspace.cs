// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

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
    /// The complete workspace length in packed final-block storage elements.
    /// </summary>
    public const int StorageLength = MaximumFinalBlockCount + ((MaximumPartitionCount + Av1EncoderBlockStruct.StorageSize - 1) / Av1EncoderBlockStruct.StorageSize);

    private readonly IMemoryOwner<Av1EncoderBlockStruct> owner;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderSuperblockWorkspace"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing the encoder allocator.</param>
    public Av1EncoderSuperblockWorkspace(Configuration configuration)
        => this.owner = configuration.MemoryAllocator.Allocate<Av1EncoderBlockStruct>(StorageLength, AllocationOptions.Clean);

    /// <summary>
    /// Gets the maximum-size final-block decision span in partition traversal order.
    /// </summary>
    public Span<Av1EncoderBlockStruct> FinalBlocks => this.owner.Memory.Span[..MaximumFinalBlockCount];

    /// <summary>
    /// Gets the maximum-size partition-type span in partition-tree preorder.
    /// </summary>
    public Span<byte> PartitionTypes
        => MemoryMarshal.AsBytes(this.owner.Memory.Span[MaximumFinalBlockCount..])[..MaximumPartitionCount];

    /// <summary>
    /// Clears all decisions before the workspace is reused for another superblock.
    /// </summary>
    public void Reset() => this.owner.Memory.Span.Clear();

    /// <summary>
    /// Releases the reusable superblock workspace.
    /// </summary>
    public void Dispose() => this.owner.Dispose();
}
