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
/// Implements decoding pixel data with photometric interpretation of type 'YCbCr'.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class YCbCrTiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly MemoryAllocator memoryAllocator;

    private readonly TiffYCbCrOperator colorOperator;

    private readonly ushort[] ycbcrSubSampling;

    private readonly Configuration configuration;

    private readonly ColorProfileConverter? iccConverter;

    private readonly ColorConversionOptions? rgbSourceOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="YCbCrTiffColor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The image configuration.</param>
    /// <param name="metadata">The frame metadata containing the source ICC profile.</param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="referenceBlackAndWhite">The TIFF coding ranges.</param>
    /// <param name="coefficients">The TIFF luma coefficients.</param>
    /// <param name="ycbcrSubSampling">The chroma subsampling factors.</param>
    public YCbCrTiffColor(
        Configuration configuration,
        ImageFrameMetadata metadata,
        MemoryAllocator memoryAllocator,
        Rational[] referenceBlackAndWhite,
        Rational[] coefficients,
        ushort[] ycbcrSubSampling)
    {
        this.configuration = configuration;
        this.memoryAllocator = memoryAllocator;
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
                this.rgbSourceOptions = new ColorConversionOptions { YCbCrTransform = this.colorOperator.ProfileTransform, MemoryAllocator = memoryAllocator };
            }

            ColorConversionOptions options = new()
            {
                SourceIccProfile = profile,
                TargetIccProfile = CompactSrgbV4Profile.Profile,
                MemoryAllocator = memoryAllocator
            };

            this.iccConverter = new ColorProfileConverter(options);
        }
    }

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        ReadOnlySpan<byte> ycbcrData = data;
        if (this.ycbcrSubSampling != null && !(this.ycbcrSubSampling[0] == 1 && this.ycbcrSubSampling[1] == 1))
        {
            // 4 extra rows and columns for possible padding.
            int paddedWidth = width + 4;
            int paddedHeight = height + 4;
            int requiredBytes = paddedWidth * paddedHeight * 3;
            using IMemoryOwner<byte> tmpBuffer = this.memoryAllocator.Allocate<byte>(requiredBytes);
            Span<byte> tmpBufferSpan = tmpBuffer.GetSpan();
            ReverseChromaSubSampling(width, height, this.ycbcrSubSampling[0], this.ycbcrSubSampling[1], data, tmpBufferSpan);
            ycbcrData = tmpBufferSpan;
            this.DecodeYCbCrData(pixels, left, top, width, height, ycbcrData);
            return;
        }

        this.DecodeYCbCrData(pixels, left, top, width, height, ycbcrData);
    }

    /// <summary>
    /// Decodes upsampled YCbCr rows into the requested pixel format.
    /// </summary>
    /// <param name="pixels">The destination pixels.</param>
    /// <param name="left">The destination left position.</param>
    /// <param name="top">The destination top position.</param>
    /// <param name="width">The decoded row width.</param>
    /// <param name="height">The decoded row count.</param>
    /// <param name="ycbcrData">The upsampled YCbCr samples.</param>
    private void DecodeYCbCrData(Buffer2D<TPixel> pixels, int left, int top, int width, int height, ReadOnlySpan<byte> ycbcrData)
    {
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
            using IMemoryOwner<Rgb> rgbBuffer = this.memoryAllocator.Allocate<Rgb>(width);
            using IMemoryOwner<Vector4> vectorBuffer = this.memoryAllocator.Allocate<Vector4>(width);

            Span<Rgb> rgbRow = rgbBuffer.Memory.Span;
            Span<Vector4> vectorRow = vectorBuffer.Memory.Span;

            // YCbCr and RGB each contain three floats. The shared converter loads each group before its RGB store,
            // so these views can safely share one full-row buffer for the source codes and converted output.
            Span<YCbCr> ycbcrRow = MemoryMarshal.Cast<Rgb, YCbCr>(rgbRow);

            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

                // Normalize the upsampled stored codes to 0..1; a YCbCr source profile needs these channels unchanged.
                SimdUtils.ByteToNormalizedFloat(ycbcrData.Slice(offset, width * 3), MemoryMarshal.Cast<Rgb, float>(rgbRow));

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
                offset += (width + widthPadding) * 3;
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
                    ycbcrData.Slice(offset + (x * 3), count * 3),
                    [],
                    [],
                    rgba,
                    new TiffYCbCr8Source(planar: false),
                    this.colorOperator);

                PixelOperations<TPixel>.Instance.FromRgba32(this.configuration, rgba, pixelRow.Slice(x, count));
            }

            offset += (width + widthPadding) * 3;
        }
    }

    private static void ReverseChromaSubSampling(int width, int height, int horizontalSubSampling, int verticalSubSampling, ReadOnlySpan<byte> source, Span<byte> destination)
    {
        // If width and height are not multiples of ChromaSubsampleHoriz and ChromaSubsampleVert respectively,
        // then the source data will be padded.
        width += TiffUtilities.PaddingToNextInteger(width, horizontalSubSampling);
        height += TiffUtilities.PaddingToNextInteger(height, verticalSubSampling);
        int blockWidth = width / horizontalSubSampling;
        int blockHeight = height / verticalSubSampling;
        int cbCrOffsetInBlock = horizontalSubSampling * verticalSubSampling;
        int blockByteCount = cbCrOffsetInBlock + 2;

        for (int blockRow = blockHeight - 1; blockRow >= 0; blockRow--)
        {
            for (int blockCol = blockWidth - 1; blockCol >= 0; blockCol--)
            {
                int blockOffset = (blockRow * blockWidth) + blockCol;
                ReadOnlySpan<byte> blockData = source.Slice(blockOffset * blockByteCount, blockByteCount);
                byte cr = blockData[cbCrOffsetInBlock + 1];
                byte cb = blockData[cbCrOffsetInBlock];

                for (int row = verticalSubSampling - 1; row >= 0; row--)
                {
                    for (int col = horizontalSubSampling - 1; col >= 0; col--)
                    {
                        int offset = 3 * ((((blockRow * verticalSubSampling) + row) * width) + (blockCol * horizontalSubSampling) + col);
                        destination[offset + 2] = cr;
                        destination[offset + 1] = cb;
                        destination[offset] = blockData[(row * horizontalSubSampling) + col];
                    }
                }
            }
        }
    }
}
