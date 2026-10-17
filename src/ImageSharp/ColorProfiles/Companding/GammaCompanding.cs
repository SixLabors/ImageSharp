// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.ColorProfiles.Companding;

/// <summary>
/// Implements gamma companding.
/// Negative components use the magnitude curve with the original sign.
/// </summary>
/// <remarks>
/// <see href="http://www.brucelindbloom.com/index.html?Eqn_RGB_to_XYZ.html"/>
/// <see href="http://www.brucelindbloom.com/index.html?Eqn_XYZ_to_RGB.html"/>
/// </remarks>
public static partial class GammaCompanding
{
    private static Func<double, double, double> CompressFunction => (d, m) => Math.Pow(d, 1 / m);

    private static Func<double, double, double> ExpandFunction => Math.Pow;

    /// <summary>
    /// Compresses linear RGB without clipping and retains alpha.
    /// </summary>
    /// <param name="vectors">The span of vectors.</param>
    /// <param name="gamma">The gamma value.</param>
    public static void Compress(Span<Vector4> vectors, double gamma)
        => CompressExtended(vectors, gamma);

    /// <summary>
    /// Compresses linear RGB at the requested component precision and retains alpha.
    /// </summary>
    /// <param name="vectors">The colors to transform.</param>
    /// <param name="gamma">The gamma value.</param>
    /// <param name="bitDepth">The required precision per color component.</param>
    /// <param name="colorRange">The input color range. Only eight-bit SDR conversion clips RGB to [0,1] and uses lookup tables.</param>
    public static void Compress(Span<Vector4> vectors, double gamma, int bitDepth, PixelColorRange colorRange)
    {
        // Gamma compression rises sharply near zero. Table interpolation loses higher-precision color values.
        // Keep both directions on the eight-bit SDR path; other inputs use unclipped analytic conversion.
        if (bitDepth == 8 && colorRange == PixelColorRange.Sdr)
        {
            CompandingUtilities.Compand(vectors, CompandingUtilities.GetCompressLookupTable<GammaCompandingKey>(CompressFunction, gamma));
        }
        else
        {
            CompressExtended(vectors, gamma);
        }
    }

    /// <summary>
    /// Expands nonlinear RGB without clipping and retains alpha.
    /// </summary>
    /// <param name="vectors">The span of vectors.</param>
    /// <param name="gamma">The gamma value.</param>
    public static void Expand(Span<Vector4> vectors, double gamma)
        => ExpandExtended(vectors, gamma);

    /// <summary>
    /// Expands nonlinear RGB at the requested component precision and retains alpha.
    /// </summary>
    /// <param name="vectors">The colors to transform.</param>
    /// <param name="gamma">The gamma value.</param>
    /// <param name="bitDepth">The required precision per color component.</param>
    /// <param name="colorRange">The input color range. Only eight-bit SDR conversion clips RGB to [0,1] and uses lookup tables.</param>
    public static void Expand(Span<Vector4> vectors, double gamma, int bitDepth, PixelColorRange colorRange)
    {
        // Gamma compression rises sharply near zero. Table interpolation loses higher-precision color values.
        // Keep both directions on the eight-bit SDR path; other inputs use unclipped analytic conversion.
        if (bitDepth == 8 && colorRange == PixelColorRange.Sdr)
        {
            CompandingUtilities.Compand(vectors, CompandingUtilities.GetExpandLookupTable<GammaCompandingKey>(ExpandFunction, gamma));
        }
        else
        {
            ExpandExtended(vectors, gamma);
        }
    }

    /// <summary>
    /// Compresses linear RGB without clipping and retains alpha.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <param name="gamma">The gamma value.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    public static Vector4 Compress(Vector4 vector, double gamma)
        => CompressExtended(vector, gamma);

    /// <summary>
    /// Compresses linear RGB at the requested component precision and retains alpha.
    /// </summary>
    /// <param name="vector">The colors to transform.</param>
    /// <param name="gamma">The gamma value.</param>
    /// <param name="bitDepth">The required precision per color component.</param>
    /// <param name="colorRange">The input color range. Only eight-bit SDR conversion clips RGB to [0,1] and uses lookup tables.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    public static Vector4 Compress(Vector4 vector, double gamma, int bitDepth, PixelColorRange colorRange)
    {
        // Gamma compression rises sharply near zero. Table interpolation loses higher-precision color values.
        // Keep both directions on the eight-bit SDR path; other inputs use unclipped analytic conversion.
        if (bitDepth == 8 && colorRange == PixelColorRange.Sdr)
        {
            return CompandingUtilities.Compand(vector, CompandingUtilities.GetCompressLookupTable<GammaCompandingKey>(CompressFunction, gamma));
        }

        return CompressExtended(vector, gamma);
    }

    /// <summary>
    /// Expands nonlinear RGB without clipping and retains alpha.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <param name="gamma">The gamma value.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    public static Vector4 Expand(Vector4 vector, double gamma)
        => ExpandExtended(vector, gamma);

    /// <summary>
    /// Expands nonlinear RGB at the requested component precision and retains alpha.
    /// </summary>
    /// <param name="vector">The colors to transform.</param>
    /// <param name="gamma">The gamma value.</param>
    /// <param name="bitDepth">The required precision per color component.</param>
    /// <param name="colorRange">The input color range. Only eight-bit SDR conversion clips RGB to [0,1] and uses lookup tables.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    public static Vector4 Expand(Vector4 vector, double gamma, int bitDepth, PixelColorRange colorRange)
    {
        // Gamma compression rises sharply near zero. Table interpolation loses higher-precision color values.
        // Keep both directions on the eight-bit SDR path; other inputs use unclipped analytic conversion.
        if (bitDepth == 8 && colorRange == PixelColorRange.Sdr)
        {
            return CompandingUtilities.Compand(vector, CompandingUtilities.GetExpandLookupTable<GammaCompandingKey>(ExpandFunction, gamma));
        }

        return ExpandExtended(vector, gamma);
    }

    /// <summary>
    /// Expands nonlinear RGB without clipping the result and retains alpha.
    /// </summary>
    /// <param name="vectors">The colors, including values outside the SDR range.</param>
    /// <param name="gamma">The gamma of the nonlinear signal.</param>
    internal static void ExpandExtended(Span<Vector4> vectors, double gamma)
        => TensorPrimitives_.Invoke(vectors, new ExpandOperator(gamma));

    /// <summary>
    /// Expands nonlinear RGB without clipping the result and retains alpha.
    /// </summary>
    /// <param name="vector">The color, including values outside the SDR range.</param>
    /// <param name="gamma">The gamma of the nonlinear signal.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    internal static Vector4 ExpandExtended(Vector4 vector, double gamma)
        => TensorPrimitives_.Invoke(vector, new ExpandOperator(gamma));

    /// <summary>
    /// Compresses linear RGB without clipping the result and retains alpha.
    /// </summary>
    /// <param name="vectors">The colors, including values outside the SDR range.</param>
    /// <param name="gamma">The gamma of the nonlinear signal.</param>
    internal static void CompressExtended(Span<Vector4> vectors, double gamma)
        => TensorPrimitives_.Invoke(vectors, new CompressOperator(gamma));

    /// <summary>
    /// Compresses linear RGB without clipping the result and retains alpha.
    /// </summary>
    /// <param name="vector">The color, including values outside the SDR range.</param>
    /// <param name="gamma">The gamma of the nonlinear signal.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    internal static Vector4 CompressExtended(Vector4 vector, double gamma)
        => TensorPrimitives_.Invoke(vector, new CompressOperator(gamma));

    private class GammaCompandingKey;
}
