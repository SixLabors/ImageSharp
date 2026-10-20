// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.ColorProfiles.Companding;

/// <summary>
/// Implements the Rec. 709 companding function.
/// </summary>
/// <remarks>
/// http://en.wikipedia.org/wiki/Rec._709
/// </remarks>
public static partial class Rec709Companding
{
    private const double Epsilon = 1 / 0.45;

    private static Func<double, double, double> CompressFunction
        => (d, _) =>
        {
            if (d < 0.018)
            {
                return 4.5 * d;
            }

            return (1.099 * Math.Pow(d, 0.45)) - 0.099;
        };

    private static Func<double, double, double> ExpandFunction
        => (d, _) =>
        {
            if (d < 0.081)
            {
                return d / 4.5;
            }

            return Math.Pow((d + 0.099) / 1.099, Epsilon);
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
    /// <param name="colorRange">The input color range. Only eight-bit SDR conversion clips RGB to [0,1] and uses lookup tables.</param>
    public static void Compress(Span<Vector4> vectors, int bitDepth, PixelColorRange colorRange)
    {
        // Rounded coefficients create a jump where the linear and power sections meet.
        // Table interpolation across this jump is limited to eight-bit SDR input.
        if (bitDepth == 8 && colorRange == PixelColorRange.Sdr)
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
    /// <param name="colorRange">The input color range. Only eight-bit SDR conversion clips RGB to [0,1] and uses lookup tables.</param>
    public static void Expand(Span<Vector4> vectors, int bitDepth, PixelColorRange colorRange)
    {
        // Rounded coefficients create a jump where the linear and power sections meet.
        // Table interpolation across this jump is limited to eight-bit SDR input.
        if (bitDepth == 8 && colorRange == PixelColorRange.Sdr)
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
    /// <param name="colorRange">The input color range. Only eight-bit SDR conversion clips RGB to [0,1] and uses lookup tables.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    public static Vector4 Compress(Vector4 vector, int bitDepth, PixelColorRange colorRange)
    {
        // Rounded coefficients create a jump where the linear and power sections meet.
        // Table interpolation across this jump is limited to eight-bit SDR input.
        if (bitDepth == 8 && colorRange == PixelColorRange.Sdr)
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
    /// <param name="colorRange">The input color range. Only eight-bit SDR conversion clips RGB to [0,1] and uses lookup tables.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    public static Vector4 Expand(Vector4 vector, int bitDepth, PixelColorRange colorRange)
    {
        // Rounded coefficients create a jump where the linear and power sections meet.
        // Table interpolation across this jump is limited to eight-bit SDR input.
        if (bitDepth == 8 && colorRange == PixelColorRange.Sdr)
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
        => CompandingUtilities.Compand(vectors, new ExpandOperator(1.099F, 0.099F, 0.081F));

    /// <summary>
    /// Expands nonlinear RGB without clipping the result and retains alpha.
    /// </summary>
    /// <param name="vector">The color, including values outside the SDR range.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    internal static Vector4 ExpandExtended(Vector4 vector)
        => CompandingUtilities.Compand(vector, new ExpandOperator(1.099F, 0.099F, 0.081F));

    /// <summary>
    /// Compresses linear RGB without clipping the result and retains alpha.
    /// </summary>
    /// <param name="vectors">The colors, including values outside the SDR range.</param>
    internal static void CompressExtended(Span<Vector4> vectors)
        => CompandingUtilities.Compand(vectors, new CompressOperator(1.099F, 0.099F, 0.018F));

    /// <summary>
    /// Compresses linear RGB without clipping the result and retains alpha.
    /// </summary>
    /// <param name="vector">The color, including values outside the SDR range.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    internal static Vector4 CompressExtended(Vector4 vector)
        => CompandingUtilities.Compand(vector, new CompressOperator(1.099F, 0.099F, 0.018F));

    private class Rec2020CompandingKey;
}
