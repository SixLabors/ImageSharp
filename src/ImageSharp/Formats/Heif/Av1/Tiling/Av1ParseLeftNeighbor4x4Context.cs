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
    private IMemoryOwner<byte>? memory;

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
        // Each byte holds a transform extent up to 128, a five-bit partition mask, or three level bits and a two-bit DC sign.
        this.memory = configuration.MemoryAllocator.Allocate<byte>(totalLength, AllocationOptions.Clean);
    }

    /// <summary>
    /// Gets all of the left-neighbor storage. The tile reader reads it once per tile and passes it to every accessor.
    /// </summary>
    /// <returns>The whole left-neighbor storage.</returns>
    public Span<byte> GetStorage()
    {
        ObjectDisposedException.ThrowIf(this.memory is null, this);
        return this.memory.Memory.Span;
    }

    /// <summary>
    /// Gets the partition context of the left 4x4 blocks of the current superblock row.
    /// </summary>
    /// <param name="storage">All of the left-neighbor storage, from <see cref="GetStorage"/>.</param>
    /// <returns>The partition contexts.</returns>
    public Span<byte> GetPartitionHeights(Span<byte> storage) => this.GetRegion(storage, PartitionHeightRegionIndex);

    /// <summary>
    /// Gets the transform sizes of the left 4x4 blocks of the current superblock row.
    /// </summary>
    /// <param name="storage">All of the left-neighbor storage, from <see cref="GetStorage"/>.</param>
    /// <returns>The transform heights.</returns>
    public Span<byte> GetTransformHeights(Span<byte> storage) => this.GetRegion(storage, TransformHeightRegionIndex);

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
        Span<byte> storage = this.GetStorage();
        this.GetTransformHeights(storage)[..blockCount].Fill((byte)Av1TransformSize.Size64x64.GetHeight());
        this.GetPartitionHeights(storage)[..blockCount].Clear();
        for (int i = 0; i < planeCount; i++)
        {
            this.GetContext(storage, i)[..blockCount].Clear();
        }
    }

    /// <summary>
    /// Updates the left partition context for every 4x4 row covered by a block.
    /// </summary>
    /// <param name="storage">All of the left-neighbor storage, from <see cref="GetStorage"/>.</param>
    /// <param name="modeInfoLocation">The block origin in frame mode-information units.</param>
    /// <param name="superblockInfo">The active superblock location.</param>
    /// <param name="subSize">The size produced by the decoded partition.</param>
    /// <param name="blockSize">The parent block size.</param>
    public void UpdatePartition(Span<byte> storage, Point modeInfoLocation, Av1SuperblockInfo superblockInfo, Av1BlockSize subSize, Av1BlockSize blockSize)
    {
        // The left context is reused for each superblock row, so address it relative to the superblock origin.
        int startIndex = (modeInfoLocation.Y - superblockInfo.ModeInfoPosition.Y) & Av1PartitionContext.Mask;
        int bh = blockSize.Get4x4HighCount();
        byte value = (byte)Av1PartitionContext.GetLeftContext(subSize);
        DebugGuard.MustBeLessThanOrEqualTo(startIndex, this.contextLength - bh, nameof(startIndex));
        this.GetPartitionHeights(storage).Slice(startIndex, bh).Fill(value);
    }

    /// <summary>
    /// Updates the left transform-size context for every 4x4 row covered by a block.
    /// </summary>
    /// <param name="storage">All of the left-neighbor storage, from <see cref="GetStorage"/>.</param>
    /// <param name="modeInfoLocation">The block origin in frame mode-information units.</param>
    /// <param name="superblockInfo">The active superblock location.</param>
    /// <param name="transformSize">The selected transform size.</param>
    /// <param name="blockSize">The decoded block size.</param>
    /// <param name="skip">A value indicating whether the block omits residual coefficients.</param>
    public void UpdateTransformation(
        Span<byte> storage,
        Point modeInfoLocation,
        Av1SuperblockInfo superblockInfo,
        Av1TransformSize transformSize,
        Av1BlockSize blockSize,
        bool skip)
    {
        int startIndex = modeInfoLocation.Y - superblockInfo.ModeInfoPosition.Y;
        byte transformHeight = (byte)transformSize.GetHeight();
        int n4h = blockSize.Get4x4HighCount();
        if (skip)
        {
            // Skipped blocks expose the full block height as their effective transform extent.
            transformHeight = (byte)(n4h << Av1Constants.ModeInfoSizeLog2);
        }

        DebugGuard.MustBeLessThanOrEqualTo(startIndex, this.contextLength - n4h, nameof(startIndex));
        this.GetTransformHeights(storage).Slice(startIndex, n4h).Fill(transformHeight);
    }

    /// <summary>
    /// Clears a range of left coefficient contexts for one plane.
    /// </summary>
    /// <param name="storage">All of the left-neighbor storage, from <see cref="GetStorage"/>.</param>
    /// <param name="plane">The zero-based plane index.</param>
    /// <param name="offset">The first context index to clear.</param>
    /// <param name="length">The number of context entries to clear.</param>
    public void ClearContext(Span<byte> storage, int plane, int offset, int length)
        => this.GetContext(storage, plane).Slice(offset, length).Clear();

    /// <summary>
    /// Gets the coefficient context column for the specified plane.
    /// </summary>
    /// <param name="storage">All of the left-neighbor storage, from <see cref="GetStorage"/>.</param>
    /// <param name="plane">The zero-based plane index.</param>
    /// <returns>The coefficient contexts for the plane.</returns>
    public Span<byte> GetContext(Span<byte> storage, int plane) => this.GetRegion(storage, PlaneContextRegionStart + plane);

    /// <summary>
    /// Gets one logical column from the left-neighbor storage.
    /// </summary>
    /// <param name="storage">All of the left-neighbor storage, from <see cref="GetStorage"/>.</param>
    /// <param name="regionIndex">The zero-based logical region index.</param>
    /// <returns>The requested context column.</returns>
    private Span<byte> GetRegion(Span<byte> storage, int regionIndex)
        => storage.Slice(regionIndex * this.contextLength, this.contextLength);
}
