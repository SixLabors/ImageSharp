// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Writers;

/// <summary>
/// Packs interleaved unsigned 32-bit RGB or RGBA samples in the TIFF file's byte order.
/// </summary>
/// <typeparam name="TPixel">The source pixel format.</typeparam>
internal sealed class TiffRgb32Writer<TPixel> : TiffCompositeColorWriter<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TiffRgb32Writer{TPixel}"/> class.
    /// </summary>
    /// <param name="image">The source frame.</param>
    /// <param name="encodingSize">The encoded region size.</param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="configuration">The pixel conversion configuration.</param>
    /// <param name="entriesCollector">The TIFF directory entries.</param>
    /// <param name="bitsPerPixel">The total bits per pixel, 96 or 128.</param>
    /// <param name="converter">The converter from sRGB to the retained profile, or null.</param>
    /// <param name="clearTransparentPixels">Whether to clear RGB where alpha is zero in the output row.</param>
    public TiffRgb32Writer(
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
        // The TIFF header declares the host byte order. Bulk pixel conversion writes 32-bit integer components directly into the strip.
        // These layouts use Rgb96 or Rgba128, not the floating-point layouts with the same total bit depth.
        if (this.BitsPerPixel == 96)
        {
            PixelOperations<TPixelFrom>.Instance.To(this.Configuration, source, MemoryMarshal.Cast<byte, Rgb96>(destination));
        }
        else
        {
            PixelOperations<TPixelFrom>.Instance.To(this.Configuration, source, MemoryMarshal.Cast<byte, Rgba128>(destination));
        }
    }
}
