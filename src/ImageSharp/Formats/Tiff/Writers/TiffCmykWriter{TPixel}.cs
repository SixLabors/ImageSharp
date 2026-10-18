// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Tiff.Compression;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Writers;

/// <summary>
/// Writes four unsigned CMYK ink samples per pixel without changing the source frame.
/// </summary>
/// <typeparam name="TPixel">The source pixel format.</typeparam>
internal sealed class TiffCmykWriter<TPixel> : TiffBaseColorWriter<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly ColorProfileConverter? converter;
    private readonly ColorConversionOptions conversionOptions;
    private readonly bool clearTransparentPixels;
    private IMemoryOwner<byte>? stripBuffer;
    private IMemoryOwner<float>? conversionBuffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffCmykWriter{TPixel}"/> class.
    /// </summary>
    /// <param name="image">The source frame.</param>
    /// <param name="encodingSize">The encoded region size.</param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="configuration">The pixel conversion configuration.</param>
    /// <param name="entriesCollector">The frame's TIFF directory entries.</param>
    /// <param name="bitsPerPixel">The resolved total depth, 32 or 64.</param>
    /// <param name="converter">The converter from working sRGB to the retained CMYK profile, or null.</param>
    /// <param name="clearTransparentPixels">Whether to clear fully transparent source RGB before converting it to inks.</param>
    public TiffCmykWriter(
        ImageFrame<TPixel> image,
        Size encodingSize,
        MemoryAllocator memoryAllocator,
        Configuration configuration,
        TiffEncoderEntriesCollector entriesCollector,
        int bitsPerPixel,
        ColorProfileConverter? converter,
        bool clearTransparentPixels)
        : base(image, encodingSize, memoryAllocator, configuration, entriesCollector)
    {
        this.BitsPerPixel = bitsPerPixel;
        this.converter = converter;
        this.conversionOptions = converter?.Options ?? new ColorConversionOptions { MemoryAllocator = memoryAllocator };
        this.clearTransparentPixels = clearTransparentPixels;
    }

    /// <inheritdoc />
    public override int BitsPerPixel { get; }

    /// <inheritdoc />
    protected override void EncodeStrip(int y, int height, TiffBaseCompressor compressor)
    {
        int width = this.Width;
        int bytesPerRow = this.BytesPerRow;
        int byteCount = height * bytesPerRow;
        this.stripBuffer ??= this.MemoryAllocator.Allocate<byte>(byteCount);
        this.conversionBuffer ??= this.MemoryAllocator.Allocate<float>(width * 7);
        Span<byte> output = this.stripBuffer.GetSpan()[..byteCount];
        Span<float> storage = this.conversionBuffer.GetSpan();

        // Four floats per pixel first hold straight RGBA, then CMYK. The separate three-float RGB row
        // retains the input while ICC writes all four ink channels. Both buffers are reused across rows and strips.
        Span<Vector4> vectors = MemoryMarshal.Cast<float, Vector4>(storage[..(width * 4)]);
        Span<Rgb> rgb = MemoryMarshal.Cast<float, Rgb>(storage[(width * 4)..]);
        Span<Cmyk> cmyk = MemoryMarshal.Cast<Vector4, Cmyk>(vectors);
        int lastRow = y + height;

        for (int row = y; row < lastRow; row++)
        {
            ReadOnlySpan<TPixel> source = this.Image.PixelBuffer.DangerousGetRowSpan(row)[..width];
            PixelOperations<TPixel>.Instance.ToUnassociatedScaledVector4(this.Configuration, source, vectors);

            if (this.clearTransparentPixels)
            {
                // Alpha belongs to the source pixels. Clear RGB while that alpha is still available;
                // the fourth component becomes black ink after conversion and must never be treated as alpha.
                EncodingUtilities.ReplaceTransparentPixels(vectors);
            }

            Rgb.FromScaledVector4(vectors, rgb);
            if (this.converter is null)
            {
                // Without a retained profile, use the shared RGB-to-CMYK model directly. The working RGB space is unchanged.
                Cmyk.FromProfileConnectionSpace(this.conversionOptions, rgb, cmyk);
            }
            else
            {
                // ICC produces device C, M, Y and K. Preserve that separation until the final sample quantization.
                this.converter.Convert<Rgb, Cmyk>(rgb, cmyk);
            }

            Span<byte> samples = output.Slice((row - y) * bytesPerRow, bytesPerRow);
            ReadOnlySpan<float> inks = MemoryMarshal.Cast<Cmyk, float>(cmyk);
            if (this.BitsPerPixel == 32)
            {
                SimdUtils.NormalizedFloatToByteSaturate(inks, samples);
            }
            else
            {
                // TIFF's header declares the host byte order. Pack all four independent ink channels as native ushort words.
                SimdUtils.NormalizedFloatToUInt16Saturate(inks, MemoryMarshal.Cast<byte, ushort>(samples));
            }
        }

        compressor.CompressStrip(output, height);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        this.stripBuffer?.Dispose();
        this.conversionBuffer?.Dispose();
    }
}
