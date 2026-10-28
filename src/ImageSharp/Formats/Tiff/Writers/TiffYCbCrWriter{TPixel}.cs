// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Formats.Tiff.Compression;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Writers;

/// <summary>
/// Writes full-resolution eight-bit TIFF YCbCr samples without changing the source frame.
/// </summary>
/// <typeparam name="TPixel">The source pixel format.</typeparam>
internal sealed class TiffYCbCrWriter<TPixel> : TiffBaseColorWriter<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly ColorProfileConverter? converter;
    private readonly ColorConversionOptions conversionOptions;
    private readonly bool profileUsesYCbCr;
    private readonly bool clearTransparentPixels;

    private IMemoryOwner<byte>? stripBuffer;
    private IMemoryOwner<float>? conversionBuffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffYCbCrWriter{TPixel}"/> class.
    /// </summary>
    /// <param name="image">The source frame in working sRGB.</param>
    /// <param name="encodingSize">The encoded region size.</param>
    /// <param name="memoryAllocator">The allocator for reusable row and strip storage.</param>
    /// <param name="configuration">The pixel conversion configuration.</param>
    /// <param name="entriesCollector">The frame's TIFF directory entries.</param>
    /// <param name="converter">The converter to the retained RGB or YCbCr profile, or null.</param>
    /// <param name="clearTransparentPixels">Whether to clear fully transparent RGB before conversion.</param>
    public TiffYCbCrWriter(
        ImageFrame<TPixel> image,
        Size encodingSize,
        MemoryAllocator memoryAllocator,
        Configuration configuration,
        TiffEncoderEntriesCollector entriesCollector,
        ColorProfileConverter? converter,
        bool clearTransparentPixels)
        : base(image, encodingSize, memoryAllocator, configuration, entriesCollector)
    {
        this.converter = converter;
        this.profileUsesYCbCr = converter?.Options.TargetIccProfile?.Header.DataColorSpace == IccColorSpaceType.YCbCr;
        this.clearTransparentPixels = clearTransparentPixels;

        // Use the inverse of the decoder's TIFF matrix, including its 128/255 chroma offset.
        // A generic BT.601 transform uses a different offset and would not invert these stored byte codes.
        this.conversionOptions = new ColorConversionOptions
        {
            MemoryAllocator = memoryAllocator,
            YCbCrTransform = new TiffYCbCrOperator().ProfileTransform
        };
    }

    /// <inheritdoc />
    public override int BitsPerPixel => 24;

    /// <inheritdoc />
    protected override void EncodeStrip(int y, int height, TiffBaseCompressor compressor)
    {
        int width = this.Width;
        int bytesPerRow = this.BytesPerRow;
        int byteCount = height * bytesPerRow;

        // Keep strip and conversion storage for the writer's lifetime, rather than renting for each row.
        this.stripBuffer ??= this.MemoryAllocator.Allocate<byte>(byteCount);
        this.conversionBuffer ??= this.MemoryAllocator.Allocate<float>(width * 7);

        Span<byte> output = this.stripBuffer.GetSpan()[..byteCount];
        Span<float> storage = this.conversionBuffer.GetSpan();

        // The first four floats per pixel retain alpha until transparent RGB has been cleared.
        // The remaining three floats hold RGB and then YCbCr in the same storage.
        Span<Vector4> vectors = MemoryMarshal.Cast<float, Vector4>(storage[..(width * 4)]);
        Span<float> components = storage.Slice(width * 4, width * 3);
        Span<Rgb> rgb = MemoryMarshal.Cast<float, Rgb>(components);
        Span<YCbCr> yCbCr = MemoryMarshal.Cast<float, YCbCr>(components);

        for (int row = y; row < y + height; row++)
        {
            ReadOnlySpan<TPixel> source = this.Image.PixelBuffer.DangerousGetRowSpan(row)[..width];
            PixelOperations<TPixel>.Instance.ToUnassociatedScaledVector4(this.Configuration, source, vectors);

            if (this.clearTransparentPixels)
            {
                EncodingUtilities.ReplaceTransparentPixels(vectors);
            }

            Rgb.FromScaledVector4(vectors, rgb);

            ColorProfileConverter? converter = this.converter;
            if (converter is null)
            {
                // Without a retained profile, apply only the TIFF RGB-to-YCbCr matrix.
                YCbCr.FromProfileConnectionSpace(this.conversionOptions, rgb, yCbCr);
            }
            else if (this.profileUsesYCbCr)
            {
                // This profile produces the stored Y/Cb/Cr values. Applying a second matrix would convert them twice.
                converter.Convert<Rgb, YCbCr>(rgb, yCbCr);
            }
            else
            {
                // RGB profiles produce device RGB. The TIFF matrix converts that RGB to stored YCbCr afterward.
                converter.Convert<Rgb, Rgb>(rgb, rgb);
                YCbCr.FromProfileConnectionSpace(this.conversionOptions, rgb, yCbCr);
            }

            // No chroma samples are discarded. Quantize once after color conversion into the 1x1 sampling layout.
            SimdUtils.NormalizedFloatToByteSaturate(components, output.Slice((row - y) * bytesPerRow, bytesPerRow));
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
