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
/// Reuses strip and row storage for TIFF sample packing without changing the source frame.
/// </summary>
/// <typeparam name="TPixel">The source pixel format.</typeparam>
internal abstract class TiffCompositeColorWriter<TPixel> : TiffBaseColorWriter<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly ColorProfileConverter? converter;
    private readonly bool clearTransparentPixels;
    private IMemoryOwner<byte>? stripBuffer;
    private IMemoryOwner<float>? conversionBuffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffCompositeColorWriter{TPixel}"/> class.
    /// </summary>
    /// <param name="image">The source frame.</param>
    /// <param name="encodingSize">The encoded region size.</param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="configuration">The pixel conversion configuration.</param>
    /// <param name="entriesCollector">The directory entries.</param>
    /// <param name="converter">The converter from sRGB to the retained profile, or null when no ICC conversion is required.</param>
    /// <param name="clearTransparentPixels">Whether to set RGB to zero where alpha is zero in the converted row.</param>
    protected TiffCompositeColorWriter(
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
        this.clearTransparentPixels = clearTransparentPixels;
    }

    /// <summary>
    /// Gets the row storage shared by pixel conversion, ICC conversion and float sample packing.
    /// </summary>
    /// <returns>Four floats per pixel for RGBA, plus three floats per pixel for ICC input when needed.</returns>
    protected Span<float> GetConversionBuffer()
    {
        int components = this.converter is null ? 4 : 7;
        this.conversionBuffer ??= this.MemoryAllocator.Allocate<float>(this.Width * components);
        return this.conversionBuffer.GetSpan();
    }

    /// <summary>
    /// Packs source pixels at the selected TIFF depth through the bulk pixel conversion API.
    /// </summary>
    /// <remarks>
    /// Unchanged rows use the frame's TPixel format. ICC conversion and transparent-pixel clearing use RgbaVector rows.
    /// Keep those rows in floating point until final packing. Converting them back to TPixel could round values before TIFF sample packing.
    /// </remarks>
    /// <typeparam name="TPixelFrom">The pixel format of the row supplied to the packer.</typeparam>
    /// <param name="source">The source pixels in their declared alpha representation.</param>
    /// <param name="destination">The output storage for all samples in source.</param>
    protected abstract void EncodePixels<TPixelFrom>(ReadOnlySpan<TPixelFrom> source, Span<byte> destination)
        where TPixelFrom : unmanaged, IPixel<TPixelFrom>;

    /// <inheritdoc />
    protected override void EncodeStrip(int y, int height, TiffBaseCompressor compressor)
    {
        int width = this.Width;
        int bytesPerRow = this.BytesPerRow;
        Buffer2D<TPixel> pixels = this.Image.PixelBuffer;
        int byteCount = bytesPerRow * height;
        this.stripBuffer ??= this.MemoryAllocator.Allocate<byte>(byteCount);
        Span<byte> output = this.stripBuffer.GetSpan()[..byteCount];
        int lastRow = y + height;
        ColorProfileConverter? converter = this.converter;

        // Every packed byte is overwritten, so the strip buffer does not need an initial clear.
        // Reuse the first strip's allocation. The final strip can use a shorter slice.
        if (converter is null && !this.clearTransparentPixels)
        {
            if (pixels.RowStride == width && pixels.DangerousTryGetSingleMemory(out Memory<TPixel> memory))
            {
                // A tightly packed strip can use one bulk call directly from image memory.
                // Cropped or segmented buffers use rows so padding and segment boundaries are never encoded.
                ReadOnlySpan<TPixel> source = memory.Span.Slice(y * width, height * width);
                this.EncodePixels(source, output);
            }
            else
            {
                for (int row = y; row < lastRow; row++)
                {
                    ReadOnlySpan<TPixel> source = pixels.DangerousGetRowSpan(row)[..width];
                    this.EncodePixels(source, output.Slice((row - y) * bytesPerRow, bytesPerRow));
                }
            }
        }
        else
        {
            // Reuse one allocation across all rows and strips: four floats per pixel hold straight RGBA.
            // ICC needs three additional floats per pixel for its RGB input.
            // Keep the converted values in RgbaVector until the writer packs the selected TIFF depth.
            Span<float> storage = this.GetConversionBuffer();
            Span<Vector4> vectors = MemoryMarshal.Cast<float, Vector4>(storage[..(width * 4)]);
            Span<Rgb> rgb = MemoryMarshal.Cast<float, Rgb>(storage[(width * 4)..]);

            for (int row = y; row < lastRow; row++)
            {
                ReadOnlySpan<TPixel> source = pixels.DangerousGetRowSpan(row)[..width];
                if (converter is null)
                {
                    PixelOperations<TPixel>.Instance.ToUnassociatedScaledVector4(this.Configuration, source, vectors);
                }
                else
                {
                    converter.Convert(this.Configuration, source, vectors, rgb);
                }

                // Clear after conversion and before packing. Only zero-alpha pixels in the encoder's row are replaced.
                // The source frame remains unchanged.
                if (this.clearTransparentPixels)
                {
                    EncodingUtilities.ReplaceTransparentPixels(vectors);
                }

                // RgbaVector exposes the converted row to the existing pixel packers without copying or rounding it back to TPixel.
                ReadOnlySpan<RgbaVector> converted = MemoryMarshal.Cast<Vector4, RgbaVector>(vectors);
                this.EncodePixels(converted, output.Slice((row - y) * bytesPerRow, bytesPerRow));
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
