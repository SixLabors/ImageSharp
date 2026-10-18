// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Writers;

/// <summary>
/// Packs interleaved unsigned 16-bit RGB or RGBA samples in the TIFF file's byte order.
/// </summary>
/// <typeparam name="TPixel">The source pixel format.</typeparam>
internal sealed class TiffRgb16Writer<TPixel> : TiffCompositeColorWriter<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TiffRgb16Writer{TPixel}"/> class.
    /// </summary>
    /// <param name="image">The source frame.</param>
    /// <param name="encodingSize">The encoded region size.</param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="configuration">The pixel conversion configuration.</param>
    /// <param name="entriesCollector">The TIFF directory entries.</param>
    /// <param name="bitsPerPixel">The total bits per pixel, 48 or 64.</param>
    /// <param name="converter">The converter from sRGB to the retained profile, or null.</param>
    /// <param name="clearTransparentPixels">Whether to clear RGB where alpha is zero in the output row.</param>
    public TiffRgb16Writer(
        ImageFrame<TPixel> image,
        Size encodingSize,
        MemoryAllocator memoryAllocator,
        Configuration configuration,
        TiffEncoderEntriesCollector entriesCollector,
        int bitsPerPixel,
        ColorProfileConverter? converter,
        bool clearTransparentPixels)
        : base(image, encodingSize, memoryAllocator, configuration, entriesCollector, converter, clearTransparentPixels)
        => this.BitsPerPixel = bitsPerPixel;

    /// <inheritdoc />
    public override int BitsPerPixel { get; }

    /// <inheritdoc />
    protected override void EncodePixels<TPixelFrom>(ReadOnlySpan<TPixelFrom> source, Span<byte> destination)
    {
        // The TIFF header declares the host byte order. Bulk pixel conversion writes 16-bit components directly into the strip.
        // RGB output omits alpha; RGBA output uses the straight alpha representation of Rgba64.
        if (this.BitsPerPixel == 48)
        {
            PixelOperations<TPixelFrom>.Instance.ToRgb48Bytes(this.Configuration, source, destination, source.Length);
        }
        else
        {
            PixelOperations<TPixelFrom>.Instance.ToRgba64Bytes(this.Configuration, source, destination, source.Length);
        }
    }
}
