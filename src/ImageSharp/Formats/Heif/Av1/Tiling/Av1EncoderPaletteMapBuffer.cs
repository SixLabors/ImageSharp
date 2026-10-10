// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Exposes reusable luma and chroma palette color-index maps over superblock-workspace storage.
/// </summary>
internal sealed class Av1EncoderPaletteMapBuffer
{
    /// <summary>
    /// The width and height of each maximum-superblock map.
    /// </summary>
    public const int MapLength = 1 << Av1Constants.MaxSuperBlockSizeLog2;

    /// <summary>
    /// The combined byte length of the luma and chroma maps.
    /// </summary>
    public const int StorageLength = 2 * MapLength * MapLength;

    /// <summary>
    /// The storage of the luma map.
    /// </summary>
    private readonly Memory<byte> luma;

    /// <summary>
    /// The storage of the shared chroma map.
    /// </summary>
    private readonly Memory<byte> chroma;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderPaletteMapBuffer"/> class.
    /// </summary>
    /// <param name="storage">The superblock-owned backing storage.</param>
    public Av1EncoderPaletteMapBuffer(Memory<byte> storage)
    {
        int mapArea = MapLength * MapLength;
        this.luma = storage[..mapArea];
        this.chroma = storage.Slice(mapArea, mapArea);
    }

    /// <summary>
    /// Gets a block-sized view of the luma or shared chroma color-index map.
    /// </summary>
    /// <remarks>
    /// The view is contiguous, with a stride of the block width, so that the nearest-color search writes the indices
    /// of a block that is wholly inside the frame straight into the map.
    /// </remarks>
    /// <param name="planeType">The luma or shared chroma plane class.</param>
    /// <param name="width">The padded plane-block width.</param>
    /// <param name="height">The padded plane-block height.</param>
    /// <returns>The reusable map region beginning at the workspace origin.</returns>
    public Av1PlaneRegion<byte> GetMap(Av1PlaneType planeType, int width, int height)
        => new(planeType == Av1PlaneType.Y ? this.luma : this.chroma, width, new Rectangle(0, 0, width, height));
}
