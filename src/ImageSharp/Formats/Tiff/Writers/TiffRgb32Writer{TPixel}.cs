// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Writers;

/// <summary>
/// Writes chunky unsigned 32-bit color samples in the TIFF file's native byte order.
/// </summary>
/// <typeparam name="TPixel">The source pixel format.</typeparam>
internal sealed class TiffRgb32Writer<TPixel> : TiffCompositeColorWriter<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly int bitsPerPixel;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffRgb32Writer{TPixel}"/> class.
    /// </summary>
    /// <param name="image">The source frame.</param>
    /// <param name="encodingSize">The encoded region size.</param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="entriesCollector">The TIFF directory collector.</param>
    /// <param name="bitsPerPixel">The total bits per pixel, 96 or 128.</param>
    public TiffRgb32Writer(
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
        // The TIFF header declares the host byte order, so the destination pixels
        // can be written directly to the strip in their native integer layout.
        if (this.bitsPerPixel == 96)
        {
            // The shared bulk operation copies matching pixels exactly. Other source
            // formats use the established pixel conversion path.
            PixelOperations<TPixel>.Instance.To(this.Configuration, pixels, MemoryMarshal.Cast<byte, Rgb96>(buffer));
        }
        else
        {
            PixelOperations<TPixel>.Instance.To(this.Configuration, pixels, MemoryMarshal.Cast<byte, Rgba128>(buffer));
        }
    }
}
