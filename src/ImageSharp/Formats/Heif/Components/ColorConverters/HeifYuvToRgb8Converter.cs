// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif.Components;

/// <summary>
/// Converts HEIF YUV planes directly to packed eight-bit RGB pixels.
/// </summary>
internal static partial class HeifYuvToRgb8Converter
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
    /// Determines whether the specialized fixed-point conversion supports the supplied plane and color description.
    /// </summary>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
    /// <param name="lumaBitDepth">The luma sample precision in bits.</param>
    /// <param name="chromaBitDepth">The chroma sample precision in bits.</param>
    /// <param name="isFullRange">Whether the samples use the complete numeric range.</param>
    /// <param name="matrixCoefficients">The H.273 matrix-coefficient code point.</param>
    /// <param name="mode">The resolved H.273 conversion operation.</param>
    /// <returns><see langword="true"/> when the planes can use this converter; otherwise, <see langword="false"/>.</returns>
    public static bool SupportsFixedPointConversion(
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
    /// <typeparam name="TBuffer">The codec adapter that exposes reconstructed component rows.</typeparam>
    /// <param name="configuration">The configuration used for allocation and pixel conversion.</param>
    /// <param name="buffer">The reconstructed component-plane buffer.</param>
    /// <param name="destination">The destination pixel region.</param>
    /// <param name="parameters">The resolved H.273 conversion parameters.</param>
    /// <param name="sourceX">The horizontal luma-sample offset of the output window.</param>
    /// <param name="sourceY">The vertical luma-sample offset of the output window.</param>
    /// <param name="sourceSize">The extent of the source region before rotation and mirroring.</param>
    /// <param name="transform">The rotation and mirroring applied within the destination region.</param>
    public static void ConvertFixedPoint<TPixel, TBuffer>(
        Configuration configuration,
        TBuffer buffer,
        Buffer2DRegion<TPixel> destination,
        in HeifColorConversionParameters parameters,
        int sourceX,
        int sourceY,
        Size sourceSize,
        HeifPixelTransform transform)
        where TPixel : unmanaged, IPixel<TPixel>
        where TBuffer : struct, IHeifPlanarSampleBuffer<ushort>
    {
        ConversionParameters conversionParameters = new(in parameters);
        Matrix3x2 matrix = transform.GetMatrix(sourceSize);
        Size step = new((int)matrix.M11, (int)matrix.M12);
        using IMemoryOwner<byte> componentOwner = configuration.MemoryAllocator.Allocate<byte>(sourceSize.Width * 3);
        Span<byte> components = componentOwner.GetSpan();
        Span<byte> red = components[..sourceSize.Width];
        Span<byte> green = components.Slice(sourceSize.Width, sourceSize.Width);
        Span<byte> blue = components.Slice(sourceSize.Width * 2, sourceSize.Width);

        // The value-type buffer closes the row-access contract at the call site. Constrained calls are therefore
        // devirtualized without boxing while keeping codec-specific buffer ownership outside the color pipeline.
        for (int y = 0; y < sourceSize.Height; y++)
        {
            int lumaY = sourceY + y;

            // The codec boundary validates 4:2:0 crop offsets in complete chroma-sample units. Each native chroma
            // sample therefore covers one 2x2 luma cell without an alignment branch in the SIMD loop.
            ReadOnlySpan<ushort> luma = buffer.GetLumaRowSpan(lumaY).Slice(sourceX, sourceSize.Width);
            ReadOnlySpan<ushort> chromaBlue = buffer.GetChromaBlueRowSpan(lumaY >> 1).Slice(sourceX >> 1);
            ReadOnlySpan<ushort> chromaRed = buffer.GetChromaRedRowSpan(lumaY >> 1).Slice(sourceX >> 1);

            ConvertRow<FixedPointCoefficientOperator>(
                luma,
                chromaBlue,
                chromaRed,
                red,
                green,
                blue,
                1,
                in conversionParameters);

            if (transform.IsIdentity)
            {
                Span<TPixel> output = destination.DangerousGetRowSpan(y);
                PixelOperations<TPixel>.Instance.PackFromRgbPlanes(red, green, blue, output);
            }
            else
            {
                // Noncontiguous output cannot be passed to the planar SIMD packer. Matrix arithmetic above
                // remains vectorized; each packed pixel is written once at its final transformed coordinate.
                Point point = HeifPixelTransform.Transform(0, y, matrix);

                for (int x = 0; x < sourceSize.Width; x++)
                {
                    destination.DangerousGetRowSpan(point.Y)[point.X] = TPixel.FromRgb24(new Rgb24(red[x], green[x], blue[x]));
                    point += step;
                }
            }
        }
    }
}
