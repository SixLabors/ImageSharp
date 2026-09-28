// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Writers;

/// <summary>
/// Writes chunky 16-bit RGB or RGBA samples in the TIFF file's native byte order.
/// </summary>
/// <typeparam name="TPixel">The source pixel format.</typeparam>
internal sealed class TiffRgb16Writer<TPixel> : TiffCompositeColorWriter<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly int bitsPerPixel;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffRgb16Writer{TPixel}"/> class.
    /// </summary>
    /// <param name="image">The source frame.</param>
    /// <param name="encodingSize">The encoded region size.</param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="entriesCollector">The TIFF directory collector.</param>
    /// <param name="bitsPerPixel">The total bits per pixel, 48 or 64.</param>
    public TiffRgb16Writer(
        ImageFrame<TPixel> image,
        Size encodingSize,
        MemoryAllocator memoryAllocator,
        Configuration configuration,
        TiffEncoderEntriesCollector entriesCollector,
        int bitsPerPixel)
        : base(image, encodingSize, memoryAllocator, configuration, entriesCollector)
    {
        this.bitsPerPixel = bitsPerPixel;
    }

    /// <inheritdoc />
    public override int BitsPerPixel => this.bitsPerPixel;

    /// <inheritdoc />
    protected override void EncodePixels(Span<TPixel> pixels, Span<byte> buffer)
    {
        // TIFF's header declares the host byte order, so the existing bulk conversions
        // can write native-endian 16-bit samples without a second pass over the strip.
        if (this.bitsPerPixel == 48)
        {
            PixelOperations<TPixel>.Instance.ToRgb48Bytes(this.Configuration, pixels, buffer, pixels.Length);
        }
        else
        {
            PixelOperations<TPixel>.Instance.ToRgba64Bytes(this.Configuration, pixels, buffer, pixels.Length);
        }
    }
}
