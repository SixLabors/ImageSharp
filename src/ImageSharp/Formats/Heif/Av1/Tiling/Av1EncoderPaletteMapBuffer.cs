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

    private readonly Av1PlaneRegion<byte> luma;
    private readonly Av1PlaneRegion<byte> chroma;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderPaletteMapBuffer"/> class.
    /// </summary>
    /// <param name="storage">The superblock-owned backing storage.</param>
    public Av1EncoderPaletteMapBuffer(Memory<byte> storage)
    {
        int mapArea = MapLength * MapLength;
        Rectangle bounds = new(0, 0, MapLength, MapLength);
        this.luma = new Av1PlaneRegion<byte>(storage[..mapArea], MapLength, bounds);
        this.chroma = new Av1PlaneRegion<byte>(storage.Slice(mapArea, mapArea), MapLength, bounds);
    }

    /// <summary>
    /// Gets a block-sized view of the luma or shared chroma color-index map.
    /// </summary>
    /// <param name="planeType">The luma or shared chroma plane class.</param>
    /// <param name="width">The padded plane-block width.</param>
    /// <param name="height">The padded plane-block height.</param>
    /// <returns>The reusable map region beginning at the workspace origin.</returns>
    public Av1PlaneRegion<byte> GetMap(Av1PlaneType planeType, int width, int height)
        => (planeType == Av1PlaneType.Y ? this.luma : this.chroma).GetSubRegion(0, 0, width, height);
}
