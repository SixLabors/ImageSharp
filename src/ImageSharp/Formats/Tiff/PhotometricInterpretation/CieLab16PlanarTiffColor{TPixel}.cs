// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements decoding pixel data with photometric interpretation of type 'CieLab' with the planar configuration.
/// Each channel is represented with 16 bits.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class CieLab16PlanarTiffColor<TPixel> : TiffBasePlanarColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly ColorProfileConverter colorProfileConverter;
    private readonly Configuration configuration;
    private readonly bool isBigEndian;

    // libtiff encodes 16-bit Lab as:
    // L* : unsigned [0, 65535] mapping to [0, 100]
    // a*, b* : signed [-32768, 32767], values are 256x the 1976 a*, b* values.
    private const float Inv65535 = 1f / 65535f;
    private const float Inv256 = 1f / 256f;

    public CieLab16PlanarTiffColor(
        Configuration configuration,
        DecoderOptions decoderOptions,
        ImageFrameMetadata metadata,
        MemoryAllocator allocator,
        bool isBigEndian)
    {
        this.isBigEndian = isBigEndian;
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
    public override void Decode(IMemoryOwner<byte>[] data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        Span<byte> lPlane = data[0].GetSpan();
        Span<byte> aPlane = data[1].GetSpan();
        Span<byte> bPlane = data[2].GetSpan();

        // Allocate temporary buffers to hold the LAB -> RGB conversion.
        // This should be the maximum width of a row.
        using IMemoryOwner<Rgb> rgbBuffer = this.colorProfileConverter.Options.MemoryAllocator.Allocate<Rgb>(width);
        using IMemoryOwner<Vector4> vectorBuffer = this.colorProfileConverter.Options.MemoryAllocator.Allocate<Vector4>(width);

        Span<Rgb> rgbRow = rgbBuffer.Memory.Span;
        Span<Vector4> vectorRow = vectorBuffer.Memory.Span;

        // Reuse the rgbRow span for lab data since both are 3-float structs, avoiding an extra allocation.
        Span<CieLab> cieLabRow = MemoryMarshal.Cast<Rgb, CieLab>(rgbRow);

        int stride = width * 2;

        if (this.isBigEndian)
        {
            for (int y = 0; y < height; y++)
            {
                int rowBase = y * stride;
                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(top + y).Slice(left, width);

                for (int x = 0; x < width; x++)
                {
                    int i = rowBase + (x * 2);

                    ushort lRaw = TiffUtilities.ConvertToUShortBigEndian(lPlane.Slice(i, 2));
                    short aRaw = unchecked((short)TiffUtilities.ConvertToUShortBigEndian(aPlane.Slice(i, 2)));
                    short bRaw = unchecked((short)TiffUtilities.ConvertToUShortBigEndian(bPlane.Slice(i, 2)));

                    float l = lRaw * 100f * Inv65535;
                    float a = aRaw * Inv256;
                    float b = bRaw * Inv256;

                    cieLabRow[x] = new CieLab(l, a, b);
                }

                // Convert CIE Lab -> Rgb -> Vector4 -> TPixel
                this.colorProfileConverter.Convert<CieLab, Rgb>(cieLabRow, rgbRow);
                Rgb.ToScaledVector4(rgbRow, vectorRow);
                PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, vectorRow, pixelRow, PixelConversionModifiers.Scale);
            }

            return;
        }

        for (int y = 0; y < height; y++)
        {
            int rowBase = y * stride;
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(top + y).Slice(left, width);

            for (int x = 0; x < width; x++)
            {
                int i = rowBase + (x * 2);

                ushort lRaw = TiffUtilities.ConvertToUShortLittleEndian(lPlane.Slice(i, 2));
                short aRaw = unchecked((short)TiffUtilities.ConvertToUShortLittleEndian(aPlane.Slice(i, 2)));
                short bRaw = unchecked((short)TiffUtilities.ConvertToUShortLittleEndian(bPlane.Slice(i, 2)));

                float l = lRaw * 100f * Inv65535;
                float a = aRaw * Inv256;
                float b = bRaw * Inv256;

                cieLabRow[x] = new CieLab(l, a, b);
            }

            // Convert CIE Lab -> Rgb -> Vector4 -> TPixel
            this.colorProfileConverter.Convert<CieLab, Rgb>(cieLabRow, rgbRow);
            Rgb.ToScaledVector4(rgbRow, vectorRow);
            PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, vectorRow, pixelRow, PixelConversionModifiers.Scale);
        }
    }
}
