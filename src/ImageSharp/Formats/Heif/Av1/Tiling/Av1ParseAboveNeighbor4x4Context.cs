// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores entropy, partition, and transform contexts for 4-by-4 blocks above the current block.
/// </summary>
internal sealed class Av1ParseAboveNeighbor4x4Context : IDisposable
{
    /// <summary>
    /// The region containing transform-width contexts.
    /// </summary>
    private const int TransformWidthRegionIndex = 0;

    /// <summary>
    /// The region containing partition-width contexts.
    /// </summary>
    private const int PartitionWidthRegionIndex = 1;

    /// <summary>
    /// The first region containing a color plane's coefficient contexts.
    /// </summary>
    private const int PlaneContextRegionStart = 2;

    /// <summary>
    /// Owns the contiguous above-neighbor storage until this instance is disposed.
    /// </summary>
    private IMemoryOwner<byte>? memory;

    /// <summary>
    /// The number of mode-information columns stored in each logical region.
    /// </summary>
    private readonly int contextLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1ParseAboveNeighbor4x4Context"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing the memory allocator.</param>
    /// <param name="planesCount">The number of color planes.</param>
    /// <param name="modeInfoColumnCount">The frame width in 4x4 mode-information columns.</param>
    public Av1ParseAboveNeighbor4x4Context(Configuration configuration, int planesCount, int modeInfoColumnCount)
    {
        this.contextLength = modeInfoColumnCount;
        int regionCount = PlaneContextRegionStart + planesCount;
        int totalLength = checked(regionCount * modeInfoColumnCount);

        // Every region spans the same aligned frame width and shares the tile-reader lifetime.
        // Each byte holds a transform extent up to 128, a five-bit partition mask, or three level bits and a two-bit DC sign.
        this.memory = configuration.MemoryAllocator.Allocate<byte>(totalLength, AllocationOptions.Clean);
    }

    /// <summary>
    /// Gets all of the above-neighbor storage. The tile reader reads it once per tile and passes it to every accessor.
    /// </summary>
    /// <returns>The whole above-neighbor storage.</returns>
    public Span<byte> GetStorage()
    {
        ObjectDisposedException.ThrowIf(this.memory is null, this);
        return this.memory.Memory.Span;
    }

    /// <summary>
    /// Gets the partition context of the previous 4x4 block row.
    /// </summary>
    /// <param name="storage">All of the above-neighbor storage, from <see cref="GetStorage"/>.</param>
    /// <returns>The partition contexts.</returns>
    public Span<byte> GetPartitionWidths(Span<byte> storage) => this.GetRegion(storage, PartitionWidthRegionIndex);

    /// <summary>
    /// Gets the transform sizes of the previous 4x4 block row.
    /// </summary>
    /// <param name="storage">All of the above-neighbor storage, from <see cref="GetStorage"/>.</param>
    /// <returns>The transform widths.</returns>
    public Span<byte> GetTransformWidths(Span<byte> storage) => this.GetRegion(storage, TransformWidthRegionIndex);

    /// <summary>
    /// Gets the coefficient context row for the specified plane.
    /// </summary>
    /// <param name="storage">All of the above-neighbor storage, from <see cref="GetStorage"/>.</param>
    /// <param name="plane">The zero-based plane index.</param>
    /// <returns>The coefficient contexts for the plane.</returns>
    public Span<byte> GetContext(Span<byte> storage, int plane) => this.GetRegion(storage, PlaneContextRegionStart + plane);

    /// <summary>
    /// Returns the above-neighbor storage to the configured memory allocator.
    /// </summary>
    public void Dispose()
    {
        this.memory?.Dispose();
        this.memory = null;
    }

    /// <summary>
    /// Resets above-neighbor state for the active tile-column range.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header describing the color planes.</param>
    /// <param name="modeInfoColumnStart">The first mode-information column in the tile.</param>
    /// <param name="modeInfoColumnEnd">The exclusive end mode-information column in the tile.</param>
    public void Clear(ObuSequenceHeader sequenceHeader, int modeInfoColumnStart, int modeInfoColumnEnd)
    {
        int planeCount = sequenceHeader.ColorConfig.PlaneCount;
        int width = Av1Math.AlignPowerOf2(
            modeInfoColumnEnd - modeInfoColumnStart,
            sequenceHeader.SuperblockSizeLog2 - Av1Constants.ModeInfoSizeLog2);

        // An edge transform reads its full nominal extent, also when the visible tile ends sooner. As a result, the reset also covers
        // the superblock padding. The coefficient contexts of a subsampled chroma plane have half the width.
        Span<byte> storage = this.GetStorage();
        this.GetTransformWidths(storage)[..width].Fill((byte)Av1TransformSize.Size64x64.GetWidth());
        this.GetPartitionWidths(storage)[..width].Clear();
        for (int i = 0; i < planeCount; i++)
        {
            int planeWidth = i > 0 && sequenceHeader.ColorConfig.SubSamplingX ? width >> 1 : width;
            this.GetContext(storage, i)[..planeWidth].Clear();
        }
    }

    /// <summary>
    /// Updates the above partition context for every 4x4 column covered by a block.
    /// </summary>
    /// <param name="storage">All of the above-neighbor storage, from <see cref="GetStorage"/>.</param>
    /// <param name="modeInfoLocation">The block origin in frame mode-information units.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="subSize">The size produced by the decoded partition.</param>
    /// <param name="blockSize">The parent block size.</param>
    public void UpdatePartition(Span<byte> storage, Point modeInfoLocation, Av1TileInfo tileInfo, Av1BlockSize subSize, Av1BlockSize blockSize)
    {
        // Above contexts are tile-local even though block positions are frame-relative.
        int startIndex = modeInfoLocation.X - tileInfo.ModeInfoColumnStart;
        int bw = blockSize.Get4x4WideCount();
        byte value = (byte)Av1PartitionContext.GetAboveContext(subSize);

        DebugGuard.MustBeLessThanOrEqualTo(startIndex, this.contextLength - bw, nameof(startIndex));
        this.GetPartitionWidths(storage).Slice(startIndex, bw).Fill(value);
    }

    /// <summary>
    /// Updates the above transform-size context for every 4x4 column covered by a block.
    /// </summary>
    /// <param name="storage">All of the above-neighbor storage, from <see cref="GetStorage"/>.</param>
    /// <param name="modeInfoLocation">The block origin in frame mode-information units.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="transformSize">The selected transform size.</param>
    /// <param name="blockSize">The decoded block size.</param>
    /// <param name="skip">A value indicating whether the block omits residual coefficients.</param>
    public void UpdateTransformation(
        Span<byte> storage,
        Point modeInfoLocation,
        Av1TileInfo tileInfo,
        Av1TransformSize transformSize,
        Av1BlockSize blockSize,
        bool skip)
    {
        int startIndex = modeInfoLocation.X - tileInfo.ModeInfoColumnStart;
        byte transformWidth = (byte)transformSize.GetWidth();
        int n4w = blockSize.Get4x4WideCount();
        if (skip)
        {
            // Skipped blocks expose the full block width as their effective transform extent.
            transformWidth = (byte)(n4w << Av1Constants.ModeInfoSizeLog2);
        }

        DebugGuard.MustBeLessThanOrEqualTo(startIndex, this.contextLength - n4w, nameof(startIndex));
        this.GetTransformWidths(storage).Slice(startIndex, n4w).Fill(transformWidth);
    }

    /// <summary>
    /// Clears a range of above coefficient contexts for one plane.
    /// </summary>
    /// <param name="storage">All of the above-neighbor storage, from <see cref="GetStorage"/>.</param>
    /// <param name="plane">The zero-based plane index.</param>
    /// <param name="offset">The first context index to clear.</param>
    /// <param name="length">The number of context entries to clear.</param>
    public void ClearContext(Span<byte> storage, int plane, int offset, int length)
        => this.GetContext(storage, plane).Slice(offset, length).Clear();

    /// <summary>
    /// Gets one logical row from the above-neighbor storage.
    /// </summary>
    /// <param name="storage">All of the above-neighbor storage, from <see cref="GetStorage"/>.</param>
    /// <param name="regionIndex">The zero-based logical region index.</param>
    /// <returns>The requested context row.</returns>
    private Span<byte> GetRegion(Span<byte> storage, int regionIndex)
        => storage.Slice(regionIndex * this.contextLength, this.contextLength);
}
