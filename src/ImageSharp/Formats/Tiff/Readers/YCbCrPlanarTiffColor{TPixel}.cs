// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Readers;

/// <summary>
/// Implements decoding pixel data with photometric interpretation of type 'YCbCr' with the planar configuration.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class YCbCrPlanarTiffColor<TPixel> : TiffBasePlanarColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly TiffYCbCrOperator colorOperator;
    private readonly ushort[] ycbcrSubSampling;

    private readonly Configuration configuration;

    private readonly ColorProfileConverter? iccConverter;

    private readonly ColorConversionOptions? rgbSourceOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="YCbCrPlanarTiffColor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The image configuration.</param>
    /// <param name="metadata">The frame metadata containing the source ICC profile.</param>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="referenceBlackAndWhite">The TIFF coding ranges.</param>
    /// <param name="coefficients">The TIFF luma coefficients.</param>
    /// <param name="ycbcrSubSampling">The chroma subsampling factors.</param>
    public YCbCrPlanarTiffColor(
        Configuration configuration,
        ImageFrameMetadata metadata,
        MemoryAllocator allocator,
        Rational[] referenceBlackAndWhite,
        Rational[] coefficients,
        ushort[] ycbcrSubSampling)
    {
        this.configuration = configuration;
        this.colorOperator = new TiffYCbCrOperator(referenceBlackAndWhite, coefficients);
        this.ycbcrSubSampling = ycbcrSubSampling;

        if (IccProfile.TryGetIccProfileForColorConversion(metadata.IccProfile, out IccProfile? profile))
        {
            IccColorSpaceType dataSpace = profile.Header.DataColorSpace;

            if (dataSpace is not IccColorSpaceType.Rgb and not IccColorSpaceType.YCbCr)
            {
                TiffThrowHelper.ThrowNotSupported($"A {dataSpace} ICC profile cannot describe TIFF YCbCr samples or their decoded RGB values.");
            }

            // An RGB ICC profile expects RGB converted from TIFF YCbCr.
            // A YCbCr ICC profile expects the normalized stored channels instead.
            if (dataSpace == IccColorSpaceType.Rgb)
            {
                // Set TIFF's ranges and luma matrix once for the RGB-profile source conversion, before ICC maps it to sRGB.
                this.rgbSourceOptions = new ColorConversionOptions { YCbCrTransform = this.colorOperator.ProfileTransform, MemoryAllocator = allocator };
            }

            ColorConversionOptions options = new()
            {
                SourceIccProfile = profile,
                TargetIccProfile = CompactSrgbV4Profile.Profile,
                MemoryAllocator = allocator
            };

            this.iccConverter = new ColorProfileConverter(options);
        }
    }

    /// <inheritdoc/>
    public override void Decode(IMemoryOwner<byte>[] data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        Span<byte> yData = data[0].GetSpan();
        Span<byte> cbData = data[1].GetSpan();
        Span<byte> crData = data[2].GetSpan();

        if (this.ycbcrSubSampling != null && !(this.ycbcrSubSampling[0] == 1 && this.ycbcrSubSampling[1] == 1))
        {
            ReverseChromaSubSampling(width, height, this.ycbcrSubSampling[0], this.ycbcrSubSampling[1], cbData, crData);
        }

        int offset = 0;
        int widthPadding = 0;
        if (this.ycbcrSubSampling != null)
        {
            // Round to the next integer multiple of horizontalSubSampling.
            widthPadding = TiffUtilities.PaddingToNextInteger(width, this.ycbcrSubSampling[0]);
        }

        ColorProfileConverter? iccConverter = this.iccConverter;

        ColorConversionOptions? rgbSourceOptions = this.rgbSourceOptions;

        if (iccConverter is not null)
        {
            using IMemoryOwner<Rgb> rgbBuffer = iccConverter.Options.MemoryAllocator.Allocate<Rgb>(width);
            using IMemoryOwner<Vector4> vectorBuffer = iccConverter.Options.MemoryAllocator.Allocate<Vector4>(width);

            Span<Rgb> rgbRow = rgbBuffer.Memory.Span;
            Span<Vector4> vectorRow = vectorBuffer.Memory.Span;

            // YCbCr and RGB each contain three floats. The shared converter loads each group before its RGB store,
            // so these views can safely share one full-row buffer for the source codes and converted output.
            Span<YCbCr> ycbcrRow = MemoryMarshal.Cast<Rgb, YCbCr>(rgbRow);

            // Reuse the Vector4 output row as three planar float lanes while reading Y, Cb, and Cr.
            // After interleaving the source codes, this storage holds the converted RGB output vectors.
            Span<float> scratch = MemoryMarshal.Cast<Vector4, float>(vectorRow);
            Span<float> yLane = scratch[..width];
            Span<float> cbLane = scratch.Slice(width, width);
            Span<float> crLane = scratch.Slice(width * 2, width);

            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

                SimdUtils.ByteToNormalizedFloat(yData.Slice(offset, width), yLane);
                SimdUtils.ByteToNormalizedFloat(cbData.Slice(offset, width), cbLane);
                SimdUtils.ByteToNormalizedFloat(crData.Slice(offset, width), crLane);

                // Interleave the stored channels once. The resulting row supplies either ICC conversion path.
                SimdUtils.PackedNormalizeInterleave3(yLane, cbLane, crLane, MemoryMarshal.Cast<Rgb, float>(rgbRow), 1F);

                if (rgbSourceOptions is not null)
                {
                    // Apply TIFF's YCbCr matrix to produce the RGB values expected by the embedded RGB ICC profile.
                    // The stored byte codes describe bounded RGB. Clip matrix overshoot before ICC, without rounding fractional samples.
                    YCbCr.ToProfileConnectionSpace(rgbSourceOptions, ycbcrRow, rgbRow);
                    Numerics.Clamp(MemoryMarshal.Cast<Rgb, float>(rgbRow), 0F, 1F);
                    iccConverter.Convert<Rgb, Rgb>(rgbRow, rgbRow);
                }
                else
                {
                    // A YCbCr source profile consumes the normalized stored channels directly.
                    iccConverter.Convert<YCbCr, Rgb>(ycbcrRow, rgbRow);
                }

                // The ICC result is sRGB; PixelOperations applies the destination pixel's scaling and alpha contract.
                Rgb.ToScaledVector4(rgbRow, vectorRow);
                PixelOperations<TPixel>.Instance.FromUnassociatedScaledVector4Destructive(this.configuration, vectorRow, pixelRow);
                offset += width + widthPadding;
            }

            return;
        }

        // Bound the byte RGB block to sixteen pixels. The converter uses full 512-, 256-, then 128-bit groups
        // and a scalar tail; PixelOperations packs each completed block into the destination pixel format.
        InlineArray16<Rgba32> rgbaBuffer = default;
        Span<Rgba32> rgbaBlock = rgbaBuffer;

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            for (int x = 0; x < width; x += rgbaBlock.Length)
            {
                int count = Math.Min(width - x, rgbaBlock.Length);
                Span<Rgba32> rgba = rgbaBlock[..count];
                TiffYCbCr8ColorConverter.Convert(
                    yData.Slice(offset + x, count),
                    cbData.Slice(offset + x, count),
                    crData.Slice(offset + x, count),
                    rgba,
                    new TiffYCbCr8Source(planar: true),
                    this.colorOperator);

                PixelOperations<TPixel>.Instance.FromRgba32(this.configuration, rgba, pixelRow.Slice(x, count));
            }

            offset += width + widthPadding;
        }
    }

    private static void ReverseChromaSubSampling(int width, int height, int horizontalSubSampling, int verticalSubSampling, Span<byte> planarCb, Span<byte> planarCr)
    {
        // If width and height are not multiples of ChromaSubsampleHoriz and ChromaSubsampleVert respectively,
        // then the source data will be padded.
        width += TiffUtilities.PaddingToNextInteger(width, horizontalSubSampling);
        height += TiffUtilities.PaddingToNextInteger(height, verticalSubSampling);

        for (int row = height - 1; row >= 0; row--)
        {
            for (int col = width - 1; col >= 0; col--)
            {
                int offset = (row * width) + col;
                int subSampleOffset = (row / verticalSubSampling * (width / horizontalSubSampling)) + (col / horizontalSubSampling);
                planarCb[offset] = planarCb[subSampleOffset];
                planarCr[offset] = planarCr[subSampleOffset];
            }
        }
    }
}
