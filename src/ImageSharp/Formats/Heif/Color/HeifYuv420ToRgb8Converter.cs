// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif.Color;

/// <summary>
/// Converts full-range eight-bit HEIF 4:2:0 planes with an unspecified matrix to packed RGB pixels.
/// </summary>
internal static partial class HeifYuv420ToRgb8Converter
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
    /// Determines whether the specialized integer conversion supports the supplied plane and color description.
    /// </summary>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
    /// <param name="lumaBitDepth">The luma sample precision in bits.</param>
    /// <param name="chromaBitDepth">The chroma sample precision in bits.</param>
    /// <param name="isFullRange">Whether the samples use the complete numeric range.</param>
    /// <param name="matrixCoefficients">The H.273 matrix-coefficient code point.</param>
    /// <param name="mode">The resolved H.273 conversion operation.</param>
    /// <returns><see langword="true"/> when the planes can use this converter; otherwise, <see langword="false"/>.</returns>
    public static bool IsSupported(
        int subsamplingX,
        int subsamplingY,
        int lumaBitDepth,
        int chromaBitDepth,
        bool isFullRange,
        CicpMatrixCoefficients matrixCoefficients,
        HeifColorConversionMode mode)
        => subsamplingX == 1
            && subsamplingY == 1
            && lumaBitDepth == 8
            && chromaBitDepth == 8
            && isFullRange
            && matrixCoefficients == CicpMatrixCoefficients.Unspecified
            && mode == HeifColorConversionMode.Coefficients;

    /// <summary>
    /// Converts supported HEIF component planes to packed pixels using integer SIMD with a scalar tail.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <typeparam name="TSource">The codec adapter that exposes reconstructed component rows.</typeparam>
    /// <param name="configuration">The configuration used for allocation and pixel conversion.</param>
    /// <param name="source">The reconstructed component-plane source.</param>
    /// <param name="image">The destination image frame.</param>
    /// <param name="parameters">The resolved H.273 conversion parameters.</param>
    /// <param name="sourceX">The horizontal luma-sample offset of the output window.</param>
    /// <param name="sourceY">The vertical luma-sample offset of the output window.</param>
    public static void Convert<TPixel, TSource>(
        Configuration configuration,
        TSource source,
        ImageFrame<TPixel> image,
        in HeifColorConversionParameters parameters,
        int sourceX,
        int sourceY)
        where TPixel : unmanaged, IPixel<TPixel>
        where TSource : struct, IHeifYuv420ToRgb8Source
    {
        ConversionParameters conversionParameters = new(in parameters);
        using IMemoryOwner<byte> componentOwner = configuration.MemoryAllocator.Allocate<byte>(image.Width * 3);
        Span<byte> components = componentOwner.GetSpan();
        Span<byte> red = components[..image.Width];
        Span<byte> green = components.Slice(image.Width, image.Width);
        Span<byte> blue = components.Slice(image.Width * 2, image.Width);

        // The value-type source closes the row-access contract at the call site. Constrained calls are therefore
        // devirtualized without boxing while keeping codec-specific buffer ownership outside the color pipeline.
        for (int y = 0; y < image.Height; y++)
        {
            int lumaY = sourceY + y;

            // The codec boundary validates 4:2:0 crop offsets in complete chroma-sample units. Each native chroma
            // sample therefore covers one 2x2 luma cell without an alignment branch in the SIMD loop.
            ReadOnlySpan<ushort> luma = source.GetLumaRow(lumaY).Slice(sourceX, image.Width);
            ReadOnlySpan<ushort> chromaBlue = source.GetChromaBlueRow(lumaY >> 1).Slice(sourceX >> 1);
            ReadOnlySpan<ushort> chromaRed = source.GetChromaRedRow(lumaY >> 1).Slice(sourceX >> 1);

            ConvertRow<FixedPointCoefficientOperator>(luma, chromaBlue, chromaRed, red, green, blue, in conversionParameters);

            Span<TPixel> destination = image.PixelBuffer.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.PackFromRgbPlanes(red, green, blue, destination);
        }
    }
}
