// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Heif.Color;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc.Color;

/// <summary>
/// Converts full-range eight-bit HEVC 4:2:0 planes with an unspecified matrix to packed RGB pixels.
/// </summary>
internal static partial class HevcYuv420ToRgb8Converter
{
    /// <summary>
    /// The fixed-point precision used for H.273 matrix coefficients.
    /// </summary>
    private const int CoefficientShift = 8;

    /// <summary>
    /// The half-unit bias used before fixed-point coefficient results are shifted to integer samples.
    /// </summary>
    private const int RoundingBias = 1 << (CoefficientShift - 1);

    /// <summary>
    /// The neutral code value for full-range eight-bit chroma.
    /// </summary>
    private const int ChromaMidpoint = 128;

    /// <summary>
    /// Determines whether the specialized integer conversion supports the supplied picture and color description.
    /// </summary>
    /// <param name="picture">The reconstructed HEVC picture.</param>
    /// <param name="colorProfile">The effective H.273 color description.</param>
    /// <param name="mode">The resolved H.273 conversion operation.</param>
    /// <returns><see langword="true"/> when the picture can use this converter; otherwise, <see langword="false"/>.</returns>
    public static bool IsSupported(HevcPictureBuffer picture, CicpProfile colorProfile, HeifColorConversionMode mode)
        => picture.ChromaFormat == 1
            && !picture.SeparateColorPlane
            && picture.BitDepthLuma == 8
            && picture.BitDepthChroma == 8
            && colorProfile.FullRange
            && colorProfile.MatrixCoefficients == CicpMatrixCoefficients.Unspecified
            && mode == HeifColorConversionMode.Coefficients;

    /// <summary>
    /// Converts a supported HEVC picture to packed pixels using integer SIMD with a scalar tail.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <param name="configuration">The configuration used for allocation and pixel conversion.</param>
    /// <param name="picture">The reconstructed HEVC picture.</param>
    /// <param name="image">The destination image frame.</param>
    /// <param name="parameters">The resolved H.273 conversion parameters.</param>
    /// <param name="sourceX">The horizontal luma-sample offset of the output window.</param>
    /// <param name="sourceY">The vertical luma-sample offset of the output window.</param>
    public static void Convert<TPixel>(
        Configuration configuration,
        HevcPictureBuffer picture,
        ImageFrame<TPixel> image,
        in HeifColorConversionParameters parameters,
        int sourceX,
        int sourceY)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ConversionParameters conversionParameters = new(in parameters);
        using IMemoryOwner<byte> componentOwner = configuration.MemoryAllocator.Allocate<byte>(image.Width * 3);
        Span<byte> components = componentOwner.GetSpan();
        Span<byte> red = components[..image.Width];
        Span<byte> green = components.Slice(image.Width, image.Width);
        Span<byte> blue = components.Slice(image.Width * 2, image.Width);

        for (int y = 0; y < image.Height; y++)
        {
            int lumaY = sourceY + y;

            // HEVC expresses 4:2:0 conformance-window offsets in complete chroma sample units, so both source
            // offsets are even here. The unspecified-matrix presentation replicates each native chroma sample
            // across its 2x2 luma cell before applying the default BT.601 coefficients.
            ReadOnlySpan<ushort> luma = picture.GetRowSpan(HevcPlane.Y, lumaY).Slice(sourceX, image.Width);
            ReadOnlySpan<ushort> chromaBlue = picture.GetRowSpan(HevcPlane.Cb, lumaY >> 1).Slice(sourceX >> 1);
            ReadOnlySpan<ushort> chromaRed = picture.GetRowSpan(HevcPlane.Cr, lumaY >> 1).Slice(sourceX >> 1);

            ConvertRow(luma, chromaBlue, chromaRed, red, green, blue, in conversionParameters);

            Span<TPixel> destination = image.PixelBuffer.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.PackFromRgbPlanes(red, green, blue, destination);
        }
    }
}
