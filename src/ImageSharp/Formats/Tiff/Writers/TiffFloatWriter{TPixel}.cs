// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Writers;

/// <summary>
/// Writes interleaved 32-bit floating-point grayscale, RGB, or RGBA samples.
/// Color conversion retains values outside [0,1] within the shared normalized range [-65504,65504].
/// </summary>
/// <typeparam name="TPixel">The source pixel format.</typeparam>
internal sealed class TiffFloatWriter<TPixel> : TiffCompositeColorWriter<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly bool associated;
    private readonly TiffGrayFloatOperator.FromRgb grayscaleOperator;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffFloatWriter{TPixel}"/> class.
    /// </summary>
    /// <param name="image">The source frame.</param>
    /// <param name="encodingSize">The encoded region size.</param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="configuration">The pixel conversion configuration.</param>
    /// <param name="entriesCollector">The TIFF directory entries.</param>
    /// <param name="bitsPerPixel">The total depth: 32 for grayscale, 96 for RGB, or 128 for RGBA.</param>
    /// <param name="whiteIsZero">Whether grayscale output stores one minus the intensity.</param>
    /// <param name="converter">The converter from sRGB to the retained profile, or null.</param>
    /// <param name="clearTransparentPixels">Whether to clear RGB where alpha is zero in the output row.</param>
    public TiffFloatWriter(
        ImageFrame<TPixel> image,
        Size encodingSize,
        MemoryAllocator memoryAllocator,
        Configuration configuration,
        TiffEncoderEntriesCollector entriesCollector,
        int bitsPerPixel,
        bool whiteIsZero,
        ColorProfileConverter? converter,
        bool clearTransparentPixels)
        : base(image, encodingSize, memoryAllocator, configuration, entriesCollector, converter, clearTransparentPixels)
    {
        this.BitsPerPixel = bitsPerPixel;
        PixelTypeInfo pixelInfo = TPixel.GetPixelTypeInfo();
        this.associated = bitsPerPixel == 128 && pixelInfo.AlphaRepresentation == PixelAlphaRepresentation.Associated;

        // Grayscale ICC output repeats its device value in RGB. Keep one copy without a second luminance calculation.
        // A one-component source also supplies its grayscale value directly.
        bool singleComponentSource = pixelInfo.ComponentInfo?.ComponentCount == 1 || (bitsPerPixel == 32 && converter is not null);
        this.grayscaleOperator = new TiffGrayFloatOperator.FromRgb(new TiffGrayFloatOperator(whiteIsZero, singleComponentSource));
    }

    /// <inheritdoc />
    public override int BitsPerPixel { get; }

    /// <inheritdoc />
    protected override void EncodePixels<TPixelFrom>(ReadOnlySpan<TPixelFrom> source, Span<byte> destination)
    {
        if (this.BitsPerPixel == 128)
        {
            // RGBA TIFF matches Vector4 storage. Pack directly into the strip without another row copy.
            // The original TPixel determines stored association, even after ICC supplies straight RgbaVector values.
            Span<Vector4> output = MemoryMarshal.Cast<byte, Vector4>(destination);
            if (this.associated)
            {
                PixelOperations<TPixelFrom>.Instance.ToAssociatedScaledVector4(this.Configuration, source, output);
            }
            else
            {
                PixelOperations<TPixelFrom>.Instance.ToUnassociatedScaledVector4(this.Configuration, source, output);
            }

            return;
        }

        // Use the same row storage as ICC. After ICC, source already occupies this storage and the pixel API performs an in-place copy.
        // Without ICC, this allocation is reused for every row. RGB and grayscale output omit alpha and use straight colors.
        Span<float> storage = this.GetConversionBuffer();
        Span<Vector4> vectors = MemoryMarshal.Cast<float, Vector4>(storage[..(this.Width * 4)]);
        int samplesPerPixel = this.BitsPerPixel / 32;

        // The direct strip path can supply several rows in one span. Limit each block to the reusable row's capacity.
        // ICC already supplies one row in this same storage; it therefore needs no additional staging allocation.
        while (!source.IsEmpty)
        {
            int count = Math.Min(source.Length, vectors.Length);
            Span<Vector4> row = vectors[..count];
            Span<float> samples = MemoryMarshal.Cast<byte, float>(destination[..(count * samplesPerPixel * sizeof(float))]);
            PixelOperations<TPixelFrom>.Instance.ToUnassociatedScaledVector4(this.Configuration, source[..count], row);

            if (samplesPerPixel == 3)
            {
                // The shared SIMD packer removes alpha without changing the floating-point RGB values.
                Rgb.FromScaledVector4(row, MemoryMarshal.Cast<float, Rgb>(samples));
            }
            else
            {
                // The input contains four native-endian RGBA floats per pixel. Each loaded register holds one channel across pixels.
                // FromRgb calculates one grayscale value per lane. The store writes that first component as contiguous float samples.
                // The shared traversal selects 512-, 256- and 128-bit groups, then handles any remaining pixels with the scalar operation.
                TiffOperatedColorConverter<TiffFloat32Source, TiffGrayFloatOperator.FromRgb, TiffGrayFloat32Store, float, float>.Convert(
                    MemoryMarshal.AsBytes(row),
                    [],
                    [],
                    [],
                    samples,
                    new TiffFloat32Source(isBigEndian: !BitConverter.IsLittleEndian, hasAlpha: true),
                    this.grayscaleOperator,
                    default);
            }

            source = source[count..];
            destination = destination[(count * samplesPerPixel * sizeof(float))..];
        }
    }
}
