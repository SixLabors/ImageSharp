// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Owns the reusable luma and chroma palette color-index maps for encoder block decisions.
/// </summary>
internal sealed class Av1EncoderPaletteMapBuffer : IDisposable
{
    /// <summary>
    /// The width and height of each maximum-superblock map.
    /// </summary>
    public const int MapLength = 1 << Av1Constants.MaxSuperBlockSizeLog2;

    /// <summary>
    /// The combined byte length of the luma and chroma maps.
    /// </summary>
    public const int StorageLength = 2 * MapLength * MapLength;

    private readonly IMemoryOwner<byte> owner;
    private readonly Buffer2D<byte> luma;
    private readonly Buffer2D<byte> chroma;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderPaletteMapBuffer"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing the encoder allocator.</param>
    public Av1EncoderPaletteMapBuffer(Configuration configuration)
    {
        this.owner = configuration.MemoryAllocator.Allocate<byte>(StorageLength);
        int mapArea = MapLength * MapLength;
        Memory<byte> storage = this.owner.Memory[..StorageLength];
        this.luma = Buffer2D<byte>.WrapMemory(storage[..mapArea], MapLength, MapLength);
        this.chroma = Buffer2D<byte>.WrapMemory(storage[mapArea..], MapLength, MapLength);
    }

    /// <summary>
    /// Gets a block-sized view of the luma or shared chroma color-index map.
    /// </summary>
    /// <param name="planeType">The luma or shared chroma plane class.</param>
    /// <param name="width">The padded plane-block width.</param>
    /// <param name="height">The padded plane-block height.</param>
    /// <returns>The reusable map region beginning at the workspace origin.</returns>
    public Buffer2DRegion<byte> GetMap(Av1PlaneType planeType, int width, int height)
        => new(
            planeType == Av1PlaneType.Y ? this.luma : this.chroma,
            new Rectangle(0, 0, width, height));

    /// <summary>
    /// Returns the shared palette-map owner to the configured allocator.
    /// </summary>
    public void Dispose()
    {
        this.luma.Dispose();
        this.chroma.Dispose();
        this.owner.Dispose();
    }
}
