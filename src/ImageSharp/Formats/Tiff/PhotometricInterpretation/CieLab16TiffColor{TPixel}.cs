// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using static SixLabors.ImageSharp.ColorProfiles.ColorProfileConverterExtensionsIcc;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements decoding pixel data with photometric interpretation of type 'CieLab'.
/// Each channel is represented with 16 bits.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class CieLab16TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly ColorProfileConverter colorProfileConverter;
    private readonly IccProfile? iccProfile;
    private readonly Configuration configuration;
    private readonly bool isBigEndian;

    // libtiff encodes 16-bit Lab as:
    // L* : unsigned [0, 65535] mapping to [0, 100]
    // a*, b* : signed [-32768, 32767], values are 256x the 1976 a*, b* values.
    private const float Inv65535 = 1f / 65535f;
    private const float Inv256 = 1f / 256f;

    public CieLab16TiffColor(
        Configuration configuration,
        DecoderOptions decoderOptions,
        ImageFrameMetadata metadata,
        MemoryAllocator allocator,
        bool isBigEndian)
    {
        this.isBigEndian = isBigEndian;
        this.configuration = configuration;

        _ = decoderOptions.TryGetIccProfileForColorConversion(metadata.IccProfile, out this.iccProfile);
        this.colorProfileConverter = new ColorProfileConverter(new ColorConversionOptions { MemoryAllocator = allocator });
    }

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        int offset = 0;

        MemoryAllocator allocator = this.colorProfileConverter.Options.MemoryAllocator;
        IccTransform? transform = CreateDecodingTransform(this.iccProfile);

        // Allocate temporary buffers to hold the LAB -> RGB conversion.
        // This should be the maximum width of a row.
        using IMemoryOwner<Rgb> rgbBuffer = allocator.Allocate<Rgb>(width);
        using IMemoryOwner<Vector4> vectorBuffer = allocator.Allocate<Vector4>(width);

        Span<Rgb> rgbRow = rgbBuffer.Memory.Span;
        Span<Vector4> vectorRow = vectorBuffer.Memory.Span;

        // Reuse the rgbRow span for lab data since both are 3-float structs, avoiding an extra allocation.
        Span<CieLab> cieLabRow = MemoryMarshal.Cast<Rgb, CieLab>(rgbRow);

        if (this.isBigEndian)
        {
            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

                for (int x = 0; x < pixelRow.Length; x++)
                {
                    ushort lRaw = TiffUtilities.ConvertToUShortBigEndian(data.Slice(offset, 2));
                    offset += 2;
                    short aRaw = unchecked((short)TiffUtilities.ConvertToUShortBigEndian(data.Slice(offset, 2)));
                    offset += 2;
                    short bRaw = unchecked((short)TiffUtilities.ConvertToUShortBigEndian(data.Slice(offset, 2)));
                    offset += 2;

                    float l = lRaw * 100f * Inv65535;
                    float a = aRaw * Inv256;
                    float b = bRaw * Inv256;

                    cieLabRow[x] = new CieLab(l, a, b);
                }

                // Convert CIE Lab -> Rgb -> Vector4 -> TPixel
                if (transform is IccTransform iccTransform)
                {
                    iccTransform.Convert<CieLab, Rgb>(cieLabRow, rgbRow, allocator);
                }
                else
                {
                    this.colorProfileConverter.Convert<CieLab, Rgb>(cieLabRow, rgbRow);
                }

                Rgb.ToScaledVector4(rgbRow, vectorRow);
                PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, vectorRow, pixelRow, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);
            }

            return;
        }

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

            for (int x = 0; x < pixelRow.Length; x++)
            {
                ushort lRaw = TiffUtilities.ConvertToUShortLittleEndian(data.Slice(offset, 2));
                offset += 2;
                short aRaw = unchecked((short)TiffUtilities.ConvertToUShortLittleEndian(data.Slice(offset, 2)));
                offset += 2;
                short bRaw = unchecked((short)TiffUtilities.ConvertToUShortLittleEndian(data.Slice(offset, 2)));
                offset += 2;

                float l = lRaw * 100f * Inv65535;
                float a = aRaw * Inv256;
                float b = bRaw * Inv256;

                cieLabRow[x] = new CieLab(l, a, b);
            }

            // Convert CIE Lab -> Rgb -> Vector4 -> TPixel
            if (transform is IccTransform iccTransform)
            {
                iccTransform.Convert<CieLab, Rgb>(cieLabRow, rgbRow, allocator);
            }
            else
            {
                this.colorProfileConverter.Convert<CieLab, Rgb>(cieLabRow, rgbRow);
            }

            Rgb.ToScaledVector4(rgbRow, vectorRow);
            PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, vectorRow, pixelRow, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);
        }
    }
}
