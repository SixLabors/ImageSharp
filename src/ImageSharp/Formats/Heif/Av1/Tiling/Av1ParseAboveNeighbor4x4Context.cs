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
    private IMemoryOwner<int>? memory;

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
        // One clean rent replaces the jagged array and its per-region arrays while preserving zero initialization.
        this.memory = configuration.MemoryAllocator.Allocate<int>(totalLength, AllocationOptions.Clean);
    }

    /// <summary>
    /// Gets a buffer holding the partition context of the previous 4x4 block row.
    /// </summary>
    public Span<int> AbovePartitionWidth => this.GetRegion(PartitionWidthRegionIndex);

    /// <summary>
    /// Gets a buffer holding the transform sizes of the previous 4x4 block row.
    /// </summary>
    public Span<int> AboveTransformWidth => this.GetRegion(TransformWidthRegionIndex);

    /// <summary>
    /// Gets the coefficient context row for the specified plane.
    /// </summary>
    /// <param name="plane">The zero-based plane index.</param>
    /// <returns>The coefficient contexts for the plane.</returns>
    public Span<int> GetContext(int plane) => this.GetRegion(PlaneContextRegionStart + plane);

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
        int width = modeInfoColumnEnd - modeInfoColumnStart;
        this.AboveTransformWidth[..width].Fill(Av1TransformSize.Size64x64.GetWidth());
        this.AbovePartitionWidth[..width].Clear();
        for (int i = 0; i < planeCount; i++)
        {
            this.GetContext(i)[..width].Clear();
        }
    }

    /// <summary>
    /// Updates the above partition context for every 4x4 column covered by a block.
    /// </summary>
    /// <param name="modeInfoLocation">The block origin in frame mode-information units.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="subSize">The size produced by the decoded partition.</param>
    /// <param name="blockSize">The parent block size.</param>
    public void UpdatePartition(Point modeInfoLocation, Av1TileInfo tileInfo, Av1BlockSize subSize, Av1BlockSize blockSize)
    {
        // Above contexts are tile-local even though block positions are frame-relative.
        int startIndex = modeInfoLocation.X - tileInfo.ModeInfoColumnStart;
        int bw = blockSize.Get4x4WideCount();
        int value = Av1PartitionContext.GetAboveContext(subSize);

        DebugGuard.MustBeLessThanOrEqualTo(startIndex, this.AboveTransformWidth.Length - bw, nameof(startIndex));
        this.AbovePartitionWidth.Slice(startIndex, bw).Fill(value);
    }

    /// <summary>
    /// Updates the above transform-size context for every 4x4 column covered by a block.
    /// </summary>
    /// <param name="modeInfoLocation">The block origin in frame mode-information units.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="transformSize">The selected transform size.</param>
    /// <param name="blockSize">The decoded block size.</param>
    /// <param name="skip">A value indicating whether the block omits residual coefficients.</param>
    public void UpdateTransformation(Point modeInfoLocation, Av1TileInfo tileInfo, Av1TransformSize transformSize, Av1BlockSize blockSize, bool skip)
    {
        int startIndex = modeInfoLocation.X - tileInfo.ModeInfoColumnStart;
        int transformWidth = transformSize.GetWidth();
        int n4w = blockSize.Get4x4WideCount();
        if (skip)
        {
            // Skipped blocks expose the full block width as their effective transform extent.
            transformWidth = n4w << Av1Constants.ModeInfoSizeLog2;
        }

        DebugGuard.MustBeLessThanOrEqualTo(startIndex, this.AboveTransformWidth.Length - n4w, nameof(startIndex));
        this.AboveTransformWidth.Slice(startIndex, n4w).Fill(transformWidth);
    }

    /// <summary>
    /// Clears a range of above coefficient contexts for one plane.
    /// </summary>
    /// <param name="plane">The zero-based plane index.</param>
    /// <param name="offset">The first context index to clear.</param>
    /// <param name="length">The number of context entries to clear.</param>
    public void ClearContext(int plane, int offset, int length)
        => this.GetContext(plane).Slice(offset, length).Clear();

    /// <summary>
    /// Gets one logical row from the contiguous above-neighbor allocation.
    /// </summary>
    /// <param name="regionIndex">The zero-based logical region index.</param>
    /// <returns>The requested context row.</returns>
    private Span<int> GetRegion(int regionIndex)
    {
        ObjectDisposedException.ThrowIf(this.memory is null, this);
        return this.memory.Memory.Span.Slice(regionIndex * this.contextLength, this.contextLength);
    }
}
