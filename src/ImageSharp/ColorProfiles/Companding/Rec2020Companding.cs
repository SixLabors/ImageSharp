// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.ColorProfiles.Companding;

/// <summary>
/// Implements Rec. 2020 companding function.
/// </summary>
/// <remarks>
/// <see href="http://en.wikipedia.org/wiki/Rec._2020"/>
/// </remarks>
public static partial class Rec2020Companding
{
    private const double Alpha = 1.09929682680944;
    private const double AlphaMinusOne = Alpha - 1;
    private const double Beta = 0.018053968510807;
    private const double InverseBeta = Beta * 4.5;
    private const double Epsilon = 1 / 0.45;

    private static Func<double, double, double> CompressFunction
        => (d, _) =>
        {
            if (d < Beta)
            {
                return 4.5 * d;
            }

            return (Alpha * Math.Pow(d, 0.45)) - AlphaMinusOne;
        };

    private static Func<double, double, double> ExpandFunction
        => (d, _) =>
        {
            if (d < InverseBeta)
            {
                return d / 4.5;
            }

            return Math.Pow((d + AlphaMinusOne) / Alpha, Epsilon);
        };

    /// <summary>
    /// Compresses linear RGB without clipping and retains alpha.
    /// </summary>
    /// <param name="vectors">The span of vectors.</param>
    public static void Compress(Span<Vector4> vectors)
        => CompressExtended(vectors);

    /// <summary>
    /// Compresses linear RGB at the requested component precision and retains alpha.
    /// </summary>
    /// <param name="vectors">The colors to transform.</param>
    /// <param name="bitDepth">The required precision per color component.</param>
    /// <param name="colorRange">The input color range. SDR conversion at sixteen-bit precision or lower clips RGB to [0,1] and uses lookup tables.</param>
    public static void Compress(Span<Vector4> vectors, int bitDepth, PixelColorRange colorRange)
    {
        // Measured table interpolation error is below 0.008 of a sixteen-bit channel level for this curve.
        // Extended ranges and precision above sixteen bits use unclipped analytic conversion.
        if (bitDepth <= 16 && colorRange == PixelColorRange.Sdr)
        {
            CompandingUtilities.Compand(vectors, CompandingUtilities.GetCompressLookupTable<Rec2020CompandingKey>(CompressFunction));
        }
        else
        {
            CompressExtended(vectors);
        }
    }

    /// <summary>
    /// Expands nonlinear RGB without clipping and retains alpha.
    /// </summary>
    /// <param name="vectors">The span of vectors.</param>
    public static void Expand(Span<Vector4> vectors)
        => ExpandExtended(vectors);

    /// <summary>
    /// Expands nonlinear RGB at the requested component precision and retains alpha.
    /// </summary>
    /// <param name="vectors">The colors to transform.</param>
    /// <param name="bitDepth">The required precision per color component.</param>
    /// <param name="colorRange">The input color range. SDR conversion at sixteen-bit precision or lower clips RGB to [0,1] and uses lookup tables.</param>
    public static void Expand(Span<Vector4> vectors, int bitDepth, PixelColorRange colorRange)
    {
        // Measured table interpolation error is below 0.008 of a sixteen-bit channel level for this curve.
        // Extended ranges and precision above sixteen bits use unclipped analytic conversion.
        if (bitDepth <= 16 && colorRange == PixelColorRange.Sdr)
        {
            CompandingUtilities.Compand(vectors, CompandingUtilities.GetExpandLookupTable<Rec2020CompandingKey>(ExpandFunction));
        }
        else
        {
            ExpandExtended(vectors);
        }
    }

    /// <summary>
    /// Compresses linear RGB without clipping and retains alpha.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    public static Vector4 Compress(Vector4 vector)
        => CompressExtended(vector);

    /// <summary>
    /// Compresses linear RGB at the requested component precision and retains alpha.
    /// </summary>
    /// <param name="vector">The colors to transform.</param>
    /// <param name="bitDepth">The required precision per color component.</param>
    /// <param name="colorRange">The input color range. SDR conversion at sixteen-bit precision or lower clips RGB to [0,1] and uses lookup tables.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    public static Vector4 Compress(Vector4 vector, int bitDepth, PixelColorRange colorRange)
    {
        // Measured table interpolation error is below 0.008 of a sixteen-bit channel level for this curve.
        // Extended ranges and precision above sixteen bits use unclipped analytic conversion.
        if (bitDepth <= 16 && colorRange == PixelColorRange.Sdr)
        {
            return CompandingUtilities.Compand(vector, CompandingUtilities.GetCompressLookupTable<Rec2020CompandingKey>(CompressFunction));
        }

        return CompressExtended(vector);
    }

    /// <summary>
    /// Expands nonlinear RGB without clipping and retains alpha.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    public static Vector4 Expand(Vector4 vector)
        => ExpandExtended(vector);

    /// <summary>
    /// Expands nonlinear RGB at the requested component precision and retains alpha.
    /// </summary>
    /// <param name="vector">The colors to transform.</param>
    /// <param name="bitDepth">The required precision per color component.</param>
    /// <param name="colorRange">The input color range. SDR conversion at sixteen-bit precision or lower clips RGB to [0,1] and uses lookup tables.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    public static Vector4 Expand(Vector4 vector, int bitDepth, PixelColorRange colorRange)
    {
        // Measured table interpolation error is below 0.008 of a sixteen-bit channel level for this curve.
        // Extended ranges and precision above sixteen bits use unclipped analytic conversion.
        if (bitDepth <= 16 && colorRange == PixelColorRange.Sdr)
        {
            return CompandingUtilities.Compand(vector, CompandingUtilities.GetExpandLookupTable<Rec2020CompandingKey>(ExpandFunction));
        }

        return ExpandExtended(vector);
    }

    /// <summary>
    /// Expands nonlinear RGB without clipping the result and retains alpha.
    /// </summary>
    /// <param name="vectors">The colors, including values outside the SDR range.</param>
    internal static void ExpandExtended(Span<Vector4> vectors)
        => TensorPrimitives_.Invoke(vectors, default(ExpandOperator));

    /// <summary>
    /// Expands nonlinear RGB without clipping the result and retains alpha.
    /// </summary>
    /// <param name="vector">The color, including values outside the SDR range.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    internal static Vector4 ExpandExtended(Vector4 vector)
        => TensorPrimitives_.Invoke(vector, default(ExpandOperator));

    /// <summary>
    /// Compresses linear RGB without clipping the result and retains alpha.
    /// </summary>
    /// <param name="vectors">The colors, including values outside the SDR range.</param>
    internal static void CompressExtended(Span<Vector4> vectors)
        => TensorPrimitives_.Invoke(vectors, default(CompressOperator));

    /// <summary>
    /// Compresses linear RGB without clipping the result and retains alpha.
    /// </summary>
    /// <param name="vector">The color, including values outside the SDR range.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    internal static Vector4 CompressExtended(Vector4 vector)
        => TensorPrimitives_.Invoke(vector, default(CompressOperator));

    private class Rec2020CompandingKey;
}
