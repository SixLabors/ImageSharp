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
/// Inputs use normalized RGB component units, not native integer channel values.
/// Compress accepts linear RGB. Expand accepts nonlinear RGB encoded with this transfer curve.
/// SDR inputs use [0,1]. Extended inputs use [-65504,65504] in the same normalized units.
/// Overloads without a colorRange parameter expect extended inputs.
/// RGB results are clamped to the selected normalized range. Alpha remains unchanged.
/// After curve evaluation, NaN results map to the lower bound; infinite results map to the corresponding bounds.
/// <see href="http://www.brucelindbloom.com/index.html?Eqn_RGB_to_XYZ.html"/>
/// <see href="http://www.brucelindbloom.com/index.html?Eqn_XYZ_to_RGB.html"/>
/// </remarks>
public static partial class GammaCompanding
{
    private static Func<double, double, double> CompressFunction => (d, m) => Math.Pow(d, 1 / m);

    private static Func<double, double, double> ExpandFunction => Math.Pow;

    /// <summary>
    /// Compresses linear RGB within the extended color range and retains alpha.
    /// </summary>
    /// <param name="vectors">The normalized linear RGB colors. Each vector contains RGB in X, Y, Z and unchanged alpha in W.</param>
    /// <param name="gamma">The gamma value.</param>
    public static void Compress(Span<Vector4> vectors, double gamma)
        => CompressCore(vectors, gamma, PixelColorRange.Extended);

    /// <summary>
    /// Compresses linear RGB at the requested component precision and retains alpha.
    /// </summary>
    /// <param name="vectors">The normalized linear RGB colors. Each vector contains RGB in X, Y, Z and unchanged alpha in W.</param>
    /// <param name="gamma">The gamma value.</param>
    /// <param name="bitDepth">The required precision per color component.</param>
    /// <param name="colorRange">The expected normalized RGB input range and the result limits: [0,1] for SDR or [-65504,65504] for extended RGB.</param>
    public static void Compress(Span<Vector4> vectors, double gamma, int bitDepth, PixelColorRange colorRange)
    {
        // Gamma compression rises sharply near zero. Table interpolation loses higher-precision color values.
        // Keep both directions on the eight-bit SDR path; other inputs use analytic conversion.
        if (bitDepth == 8 && colorRange == PixelColorRange.Sdr)
        {
            CompandingUtilities.Compand(vectors, CompandingUtilities.GetCompressLookupTable<GammaCompandingKey>(CompressFunction, gamma));
        }
        else
        {
            CompressCore(vectors, gamma, colorRange);
        }
    }

    /// <summary>
    /// Expands nonlinear RGB within the extended color range and retains alpha.
    /// </summary>
    /// <param name="vectors">The normalized nonlinear RGB colors. Each vector contains RGB in X, Y, Z and unchanged alpha in W.</param>
    /// <param name="gamma">The gamma value.</param>
    public static void Expand(Span<Vector4> vectors, double gamma)
        => ExpandCore(vectors, gamma, PixelColorRange.Extended);

    /// <summary>
    /// Expands nonlinear RGB at the requested component precision and retains alpha.
    /// </summary>
    /// <param name="vectors">The normalized nonlinear RGB colors. Each vector contains RGB in X, Y, Z and unchanged alpha in W.</param>
    /// <param name="gamma">The gamma value.</param>
    /// <param name="bitDepth">The required precision per color component.</param>
    /// <param name="colorRange">The expected normalized RGB input range and the result limits: [0,1] for SDR or [-65504,65504] for extended RGB.</param>
    public static void Expand(Span<Vector4> vectors, double gamma, int bitDepth, PixelColorRange colorRange)
    {
        // Gamma compression rises sharply near zero. Table interpolation loses higher-precision color values.
        // Keep both directions on the eight-bit SDR path; other inputs use analytic conversion.
        if (bitDepth == 8 && colorRange == PixelColorRange.Sdr)
        {
            CompandingUtilities.Compand(vectors, CompandingUtilities.GetExpandLookupTable<GammaCompandingKey>(ExpandFunction, gamma));
        }
        else
        {
            ExpandCore(vectors, gamma, colorRange);
        }
    }

    /// <summary>
    /// Compresses linear RGB within the extended color range and retains alpha.
    /// </summary>
    /// <param name="vector">The normalized linear RGB color. X, Y, Z contain RGB; W contains unchanged alpha.</param>
    /// <param name="gamma">The gamma value.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    public static Vector4 Compress(Vector4 vector, double gamma)
        => CompressCore(vector, gamma, PixelColorRange.Extended);

    /// <summary>
    /// Compresses linear RGB at the requested component precision and retains alpha.
    /// </summary>
    /// <param name="vector">The normalized linear RGB color. X, Y, Z contain RGB; W contains unchanged alpha.</param>
    /// <param name="gamma">The gamma value.</param>
    /// <param name="bitDepth">The required precision per color component.</param>
    /// <param name="colorRange">The expected normalized RGB input range and the result limits: [0,1] for SDR or [-65504,65504] for extended RGB.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    public static Vector4 Compress(Vector4 vector, double gamma, int bitDepth, PixelColorRange colorRange)
    {
        // Gamma compression rises sharply near zero. Table interpolation loses higher-precision color values.
        // Keep both directions on the eight-bit SDR path; other inputs use analytic conversion.
        if (bitDepth == 8 && colorRange == PixelColorRange.Sdr)
        {
            return CompandingUtilities.Compand(vector, CompandingUtilities.GetCompressLookupTable<GammaCompandingKey>(CompressFunction, gamma));
        }

        return CompressCore(vector, gamma, colorRange);
    }

    /// <summary>
    /// Expands nonlinear RGB within the extended color range and retains alpha.
    /// </summary>
    /// <param name="vector">The normalized nonlinear RGB color. X, Y, Z contain RGB; W contains unchanged alpha.</param>
    /// <param name="gamma">The gamma value.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    public static Vector4 Expand(Vector4 vector, double gamma)
        => ExpandCore(vector, gamma, PixelColorRange.Extended);

    /// <summary>
    /// Expands nonlinear RGB at the requested component precision and retains alpha.
    /// </summary>
    /// <param name="vector">The normalized nonlinear RGB color. X, Y, Z contain RGB; W contains unchanged alpha.</param>
    /// <param name="gamma">The gamma value.</param>
    /// <param name="bitDepth">The required precision per color component.</param>
    /// <param name="colorRange">The expected normalized RGB input range and the result limits: [0,1] for SDR or [-65504,65504] for extended RGB.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    public static Vector4 Expand(Vector4 vector, double gamma, int bitDepth, PixelColorRange colorRange)
    {
        // Gamma compression rises sharply near zero. Table interpolation loses higher-precision color values.
        // Keep both directions on the eight-bit SDR path; other inputs use analytic conversion.
        if (bitDepth == 8 && colorRange == PixelColorRange.Sdr)
        {
            return CompandingUtilities.Compand(vector, CompandingUtilities.GetExpandLookupTable<GammaCompandingKey>(ExpandFunction, gamma));
        }

        return ExpandCore(vector, gamma, colorRange);
    }

    /// <summary>
    /// Expands nonlinear RGB within the requested color range and retains alpha.
    /// </summary>
    /// <param name="vectors">The normalized nonlinear RGB colors. Each vector contains RGB in X, Y, Z and unchanged alpha in W.</param>
    /// <param name="gamma">The gamma of the nonlinear signal.</param>
    /// <param name="colorRange">The range applied to the converted RGB components.</param>
    private static void ExpandCore(Span<Vector4> vectors, double gamma, PixelColorRange colorRange)
        => CompandingUtilities.Compand(vectors, new ExpandOperator(gamma), colorRange);

    /// <summary>
    /// Expands nonlinear RGB within the requested color range and retains alpha.
    /// </summary>
    /// <param name="vector">The normalized nonlinear RGB color. X, Y, Z contain RGB; W contains unchanged alpha.</param>
    /// <param name="gamma">The gamma of the nonlinear signal.</param>
    /// <param name="colorRange">The range applied to the converted RGB components.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    private static Vector4 ExpandCore(Vector4 vector, double gamma, PixelColorRange colorRange)
        => CompandingUtilities.Compand(vector, new ExpandOperator(gamma), colorRange);

    /// <summary>
    /// Compresses linear RGB within the requested color range and retains alpha.
    /// </summary>
    /// <param name="vectors">The normalized linear RGB colors. Each vector contains RGB in X, Y, Z and unchanged alpha in W.</param>
    /// <param name="gamma">The gamma of the nonlinear signal.</param>
    /// <param name="colorRange">The range applied to the converted RGB components.</param>
    private static void CompressCore(Span<Vector4> vectors, double gamma, PixelColorRange colorRange)
        => CompandingUtilities.Compand(vectors, new CompressOperator(gamma), colorRange);

    /// <summary>
    /// Compresses linear RGB within the requested color range and retains alpha.
    /// </summary>
    /// <param name="vector">The normalized linear RGB color. X, Y, Z contain RGB; W contains unchanged alpha.</param>
    /// <param name="gamma">The gamma of the nonlinear signal.</param>
    /// <param name="colorRange">The range applied to the converted RGB components.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    private static Vector4 CompressCore(Vector4 vector, double gamma, PixelColorRange colorRange)
        => CompandingUtilities.Compand(vector, new CompressOperator(gamma), colorRange);

    private class GammaCompandingKey;
}
