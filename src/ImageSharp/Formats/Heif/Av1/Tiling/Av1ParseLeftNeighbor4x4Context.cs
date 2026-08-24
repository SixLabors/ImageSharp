// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores entropy, partition, and transform contexts for 4-by-4 blocks left of the current block.
/// </summary>
internal sealed class Av1ParseLeftNeighbor4x4Context : IDisposable
{
    /// <summary>
    /// The region containing transform-height contexts.
    /// </summary>
    private const int TransformHeightRegionIndex = 0;

    /// <summary>
    /// The region containing partition-height contexts.
    /// </summary>
    private const int PartitionHeightRegionIndex = 1;

    /// <summary>
    /// The first region containing a color plane's coefficient contexts.
    /// </summary>
    private const int PlaneContextRegionStart = 2;

    /// <summary>
    /// Owns the contiguous left-neighbor storage until this instance is disposed.
    /// </summary>
    private IMemoryOwner<int>? memory;

    /// <summary>
    /// The number of superblock-row entries stored in each logical region.
    /// </summary>
    private readonly int contextLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1ParseLeftNeighbor4x4Context"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing the memory allocator.</param>
    /// <param name="planesCount">The number of color planes.</param>
    /// <param name="superblockModeInfoSize">The superblock height in 4x4 mode-information rows.</param>
    public Av1ParseLeftNeighbor4x4Context(Configuration configuration, int planesCount, int superblockModeInfoSize)
    {
        this.contextLength = superblockModeInfoSize;
        int regionCount = PlaneContextRegionStart + planesCount;
        int totalLength = checked(regionCount * superblockModeInfoSize);

        // Every region spans the same superblock height and shares the tile-reader lifetime.
        // One clean rent replaces the jagged array and its per-region arrays while preserving zero initialization.
        this.memory = configuration.MemoryAllocator.Allocate<int>(totalLength, AllocationOptions.Clean);
    }

    /// <summary>
    /// Gets a buffer holding the partition context of the left 4x4 blocks corresponding
    /// to the current super block row.
    /// </summary>
    public Span<int> LeftPartitionHeight => this.GetRegion(PartitionHeightRegionIndex);

    /// <summary>
    /// Gets a buffer holding the transform sizes of the left 4x4 blocks corresponding
    /// to the current super block row.
    /// </summary>
    public Span<int> LeftTransformHeight => this.GetRegion(TransformHeightRegionIndex);

    /// <summary>
    /// Returns the left-neighbor storage to the configured memory allocator.
    /// </summary>
    public void Dispose()
    {
        this.memory?.Dispose();
        this.memory = null;
    }

    /// <summary>
    /// Resets all left-neighbor state for a new superblock row.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header describing the superblock size and color planes.</param>
    public void Clear(ObuSequenceHeader sequenceHeader)
    {
        int blockCount = sequenceHeader.SuperblockModeInfoSize;
        int planeCount = sequenceHeader.ColorConfig.PlaneCount;
        this.LeftTransformHeight[..blockCount].Fill(Av1TransformSize.Size64x64.GetHeight());
        this.LeftPartitionHeight[..blockCount].Clear();
        for (int i = 0; i < planeCount; i++)
        {
            this.GetContext(i)[..blockCount].Clear();
        }
    }

    /// <summary>
    /// Updates the left partition context for every 4x4 row covered by a block.
    /// </summary>
    /// <param name="modeInfoLocation">The block origin in frame mode-information units.</param>
    /// <param name="superblockInfo">The active superblock location.</param>
    /// <param name="subSize">The size produced by the decoded partition.</param>
    /// <param name="blockSize">The parent block size.</param>
    public void UpdatePartition(Point modeInfoLocation, Av1SuperblockInfo superblockInfo, Av1BlockSize subSize, Av1BlockSize blockSize)
    {
        // The left context is reused for each superblock row, so address it relative to the superblock origin.
        int startIndex = (modeInfoLocation.Y - superblockInfo.ModeInfoPosition.Y) & Av1PartitionContext.Mask;
        int bh = blockSize.Get4x4HighCount();
        int value = Av1PartitionContext.GetLeftContext(subSize);
        DebugGuard.MustBeLessThanOrEqualTo(startIndex, this.LeftPartitionHeight.Length - bh, nameof(startIndex));
        this.LeftPartitionHeight.Slice(startIndex, bh).Fill(value);
    }

    /// <summary>
    /// Updates the left transform-size context for every 4x4 row covered by a block.
    /// </summary>
    /// <param name="modeInfoLocation">The block origin in frame mode-information units.</param>
    /// <param name="superblockInfo">The active superblock location.</param>
    /// <param name="transformSize">The selected transform size.</param>
    /// <param name="blockSize">The decoded block size.</param>
    /// <param name="skip">A value indicating whether the block omits residual coefficients.</param>
    public void UpdateTransformation(Point modeInfoLocation, Av1SuperblockInfo superblockInfo, Av1TransformSize transformSize, Av1BlockSize blockSize, bool skip)
    {
        int startIndex = modeInfoLocation.Y - superblockInfo.ModeInfoPosition.Y;
        int transformHeight = transformSize.GetHeight();
        int n4h = blockSize.Get4x4HighCount();
        if (skip)
        {
            // Skipped blocks expose the full block height as their effective transform extent.
            transformHeight = n4h << Av1Constants.ModeInfoSizeLog2;
        }

        DebugGuard.MustBeLessThanOrEqualTo(startIndex, this.LeftTransformHeight.Length - n4h, nameof(startIndex));
        this.LeftTransformHeight.Slice(startIndex, n4h).Fill(transformHeight);
    }

    /// <summary>
    /// Clears a range of left coefficient contexts for one plane.
    /// </summary>
    /// <param name="plane">The zero-based plane index.</param>
    /// <param name="offset">The first context index to clear.</param>
    /// <param name="length">The number of context entries to clear.</param>
    public void ClearContext(int plane, int offset, int length)
        => this.GetContext(plane).Slice(offset, length).Clear();

    /// <summary>
    /// Gets the coefficient context column for the specified plane.
    /// </summary>
    /// <param name="plane">The zero-based plane index.</param>
    /// <returns>The coefficient contexts for the plane.</returns>
    public Span<int> GetContext(int plane) => this.GetRegion(PlaneContextRegionStart + plane);

    /// <summary>
    /// Gets one logical column from the contiguous left-neighbor allocation.
    /// </summary>
    /// <param name="regionIndex">The zero-based logical region index.</param>
    /// <returns>The requested context column.</returns>
    private Span<int> GetRegion(int regionIndex)
    {
        ObjectDisposedException.ThrowIf(this.memory is null, this);
        return this.memory.Memory.Span.Slice(regionIndex * this.contextLength, this.contextLength);
    }
}
