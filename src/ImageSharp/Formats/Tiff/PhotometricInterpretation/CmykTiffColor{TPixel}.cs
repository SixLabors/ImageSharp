// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Formats.Tiff.Compression;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

internal class CmykTiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly ColorProfileConverter colorProfileConverter;
    private readonly Configuration configuration;
    private const float Inv255 = 1f / 255f;

    private readonly TiffDecoderCompressionType compression;

    public CmykTiffColor(
        TiffDecoderCompressionType compression,
        Configuration configuration,
        DecoderOptions decoderOptions,
        ImageFrameMetadata metadata,
        MemoryAllocator allocator)
    {
        this.compression = compression;
        this.configuration = configuration;

        if (decoderOptions.TryGetIccProfileForColorConversion(metadata.IccProfile, out IccProfile? iccProfile))
        {
            ColorConversionOptions options = new()
            {
                SourceIccProfile = iccProfile,
                TargetIccProfile = CompactSrgbV4Profile.Profile,
                MemoryAllocator = allocator
            };

            this.colorProfileConverter = new ColorProfileConverter(options);
        }
        else
        {
            ColorConversionOptions options = new()
            {
                MemoryAllocator = allocator
            };

            this.colorProfileConverter = new ColorProfileConverter(options);
        }
    }

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        int offset = 0;
        if (this.compression == TiffDecoderCompressionType.Jpeg)
        {
            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    pixelRow[x] = TPixel.FromVector4(new Vector4(data[offset] * Inv255, data[offset + 1] * Inv255, data[offset + 2] * Inv255, 1.0f));

                    offset += 3;
                }
            }

            return;
        }

        // Allocate temporary buffers to hold the CMYK -> RGB conversion.
        // This should be the maximum width of a row.
        using IMemoryOwner<Rgb> rgbBuffer = this.colorProfileConverter.Options.MemoryAllocator.Allocate<Rgb>(width);
        using IMemoryOwner<Vector4> vectorBuffer = this.colorProfileConverter.Options.MemoryAllocator.Allocate<Vector4>(width);

        Span<Rgb> rgbRow = rgbBuffer.Memory.Span;
        Span<Vector4> vectorRow = vectorBuffer.Memory.Span;

        // Reuse the Vector4 buffer as CMYK storage since both are 4-float structs, avoiding an extra allocation.
        Span<Cmyk> cmykRow = MemoryMarshal.Cast<Vector4, Cmyk>(vectorRow);

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

            // Collect CMYK pixels.
            // ByteToNormalizedFloat efficiently converts packed 4-byte component data
            // to normalized 0-1 floats using SIMD.
            SimdUtils.ByteToNormalizedFloat(data.Slice(offset, width * 4), MemoryMarshal.Cast<Cmyk, float>(cmykRow));
            offset += width * 4;

            // Convert CMYK -> RGB -> Vector4 -> TPixel
            this.colorProfileConverter.Convert<Cmyk, Rgb>(cmykRow, rgbRow);
            Rgb.ToScaledVector4(rgbRow, vectorRow);
            PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, vectorRow, pixelRow, PixelConversionModifiers.Scale);
        }
    }
}
