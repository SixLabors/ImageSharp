// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.ColorProfiles.Companding;

/// <summary>
/// Implements L* companding.
/// </summary>
/// <remarks>
/// Inputs use normalized RGB component units, not native integer channel values.
/// Compress accepts linear RGB. Expand accepts nonlinear RGB encoded with this transfer curve.
/// Nonlinear components use L*/100, so 1 represents L* = 100.
/// SDR inputs use [0,1]. Extended inputs use [-65504,65504] in the same normalized units.
/// Overloads without a colorRange parameter expect extended inputs.
/// RGB results are clamped to the selected normalized range. Alpha remains unchanged.
/// After curve evaluation, NaN results map to the lower bound; infinite results map to the corresponding bounds.
/// For more info see:
/// <see href="http://www.brucelindbloom.com/index.html?Eqn_RGB_to_XYZ.html"/>
/// <see href="http://www.brucelindbloom.com/index.html?Eqn_XYZ_to_RGB.html"/>
/// </remarks>
public static partial class LCompanding
{
    private static Func<double, double, double> CompressFunction
        => (d, _) =>
        {
            if (d <= CieConstants.Epsilon)
            {
                return (d * CieConstants.Kappa) / 100;
            }

            return (1.16 * Math.Pow(d, 0.3333333)) - 0.16;
        };

    private static Func<double, double, double> ExpandFunction
        => (d, _) =>
        {
            if (d <= 0.08)
            {
                return (100 * d) / CieConstants.Kappa;
            }

            return Numerics.Pow3(((float)(d + 0.16f)) / 1.16f);
        };

    /// <summary>
    /// Compresses linear RGB within the extended color range and retains alpha.
    /// </summary>
    /// <param name="vectors">The normalized linear RGB colors. Each vector contains RGB in X, Y, Z and unchanged alpha in W.</param>
    public static void Compress(Span<Vector4> vectors)
        => CompressCore(vectors, PixelColorRange.Extended);

    /// <summary>
    /// Compresses linear RGB at the requested component precision and retains alpha.
    /// </summary>
    /// <param name="vectors">The normalized linear RGB colors. Each vector contains RGB in X, Y, Z and unchanged alpha in W.</param>
    /// <param name="bitDepth">The required precision per color component.</param>
    /// <param name="colorRange">The expected normalized RGB input range and the result limits: [0,1] for SDR or [-65504,65504] for extended RGB.</param>
    public static void Compress(Span<Vector4> vectors, int bitDepth, PixelColorRange colorRange)
    {
        // Measured table interpolation error is below 0.020 of a sixteen-bit channel level for this curve.
        // Extended ranges and precision above sixteen bits use analytic conversion.
        if (bitDepth <= 16 && colorRange == PixelColorRange.Sdr)
        {
            CompandingUtilities.Compand(vectors, CompandingUtilities.GetCompressLookupTable<LCompandingKey>(CompressFunction));
        }
        else
        {
            CompressCore(vectors, colorRange);
        }
    }

    /// <summary>
    /// Expands nonlinear RGB within the extended color range and retains alpha.
    /// </summary>
    /// <param name="vectors">The normalized nonlinear RGB colors. Each vector contains RGB in X, Y, Z and unchanged alpha in W.</param>
    public static void Expand(Span<Vector4> vectors)
        => ExpandCore(vectors, PixelColorRange.Extended);

    /// <summary>
    /// Expands nonlinear RGB at the requested component precision and retains alpha.
    /// </summary>
    /// <param name="vectors">The normalized nonlinear RGB colors. Each vector contains RGB in X, Y, Z and unchanged alpha in W.</param>
    /// <param name="bitDepth">The required precision per color component.</param>
    /// <param name="colorRange">The expected normalized RGB input range and the result limits: [0,1] for SDR or [-65504,65504] for extended RGB.</param>
    public static void Expand(Span<Vector4> vectors, int bitDepth, PixelColorRange colorRange)
    {
        // Measured table interpolation error is below 0.020 of a sixteen-bit channel level for this curve.
        // Extended ranges and precision above sixteen bits use analytic conversion.
        if (bitDepth <= 16 && colorRange == PixelColorRange.Sdr)
        {
            CompandingUtilities.Compand(vectors, CompandingUtilities.GetExpandLookupTable<LCompandingKey>(ExpandFunction));
        }
        else
        {
            ExpandCore(vectors, colorRange);
        }
    }

    /// <summary>
    /// Compresses linear RGB within the extended color range and retains alpha.
    /// </summary>
    /// <param name="vector">The normalized linear RGB color. X, Y, Z contain RGB; W contains unchanged alpha.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    public static Vector4 Compress(Vector4 vector)
        => CompressCore(vector, PixelColorRange.Extended);

    /// <summary>
    /// Compresses linear RGB at the requested component precision and retains alpha.
    /// </summary>
    /// <param name="vector">The normalized linear RGB color. X, Y, Z contain RGB; W contains unchanged alpha.</param>
    /// <param name="bitDepth">The required precision per color component.</param>
    /// <param name="colorRange">The expected normalized RGB input range and the result limits: [0,1] for SDR or [-65504,65504] for extended RGB.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    public static Vector4 Compress(Vector4 vector, int bitDepth, PixelColorRange colorRange)
    {
        // Measured table interpolation error is below 0.020 of a sixteen-bit channel level for this curve.
        // Extended ranges and precision above sixteen bits use analytic conversion.
        if (bitDepth <= 16 && colorRange == PixelColorRange.Sdr)
        {
            return CompandingUtilities.Compand(vector, CompandingUtilities.GetCompressLookupTable<LCompandingKey>(CompressFunction));
        }

        return CompressCore(vector, colorRange);
    }

    /// <summary>
    /// Expands nonlinear RGB within the extended color range and retains alpha.
    /// </summary>
    /// <param name="vector">The normalized nonlinear RGB color. X, Y, Z contain RGB; W contains unchanged alpha.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    public static Vector4 Expand(Vector4 vector)
        => ExpandCore(vector, PixelColorRange.Extended);

    /// <summary>
    /// Expands nonlinear RGB at the requested component precision and retains alpha.
    /// </summary>
    /// <param name="vector">The normalized nonlinear RGB color. X, Y, Z contain RGB; W contains unchanged alpha.</param>
    /// <param name="bitDepth">The required precision per color component.</param>
    /// <param name="colorRange">The expected normalized RGB input range and the result limits: [0,1] for SDR or [-65504,65504] for extended RGB.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    public static Vector4 Expand(Vector4 vector, int bitDepth, PixelColorRange colorRange)
    {
        // Measured table interpolation error is below 0.020 of a sixteen-bit channel level for this curve.
        // Extended ranges and precision above sixteen bits use analytic conversion.
        if (bitDepth <= 16 && colorRange == PixelColorRange.Sdr)
        {
            return CompandingUtilities.Compand(vector, CompandingUtilities.GetExpandLookupTable<LCompandingKey>(ExpandFunction));
        }

        return ExpandCore(vector, colorRange);
    }

    /// <summary>
    /// Expands nonlinear RGB within the requested color range and retains alpha.
    /// </summary>
    /// <param name="vectors">The normalized nonlinear RGB colors. Each vector contains RGB in X, Y, Z and unchanged alpha in W.</param>
    /// <param name="colorRange">The range applied to the converted RGB components.</param>
    private static void ExpandCore(Span<Vector4> vectors, PixelColorRange colorRange)
        => CompandingUtilities.Compand(vectors, default(ExpandOperator), colorRange);

    /// <summary>
    /// Expands nonlinear RGB within the requested color range and retains alpha.
    /// </summary>
    /// <param name="vector">The normalized nonlinear RGB color. X, Y, Z contain RGB; W contains unchanged alpha.</param>
    /// <param name="colorRange">The range applied to the converted RGB components.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    private static Vector4 ExpandCore(Vector4 vector, PixelColorRange colorRange)
        => CompandingUtilities.Compand(vector, default(ExpandOperator), colorRange);

    /// <summary>
    /// Compresses linear RGB within the requested color range and retains alpha.
    /// </summary>
    /// <param name="vectors">The normalized linear RGB colors. Each vector contains RGB in X, Y, Z and unchanged alpha in W.</param>
    /// <param name="colorRange">The range applied to the converted RGB components.</param>
    private static void CompressCore(Span<Vector4> vectors, PixelColorRange colorRange)
        => CompandingUtilities.Compand(vectors, default(CompressOperator), colorRange);

    /// <summary>
    /// Compresses linear RGB within the requested color range and retains alpha.
    /// </summary>
    /// <param name="vector">The normalized linear RGB color. X, Y, Z contain RGB; W contains unchanged alpha.</param>
    /// <param name="colorRange">The range applied to the converted RGB components.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    private static Vector4 CompressCore(Vector4 vector, PixelColorRange colorRange)
        => CompandingUtilities.Compand(vector, default(CompressOperator), colorRange);

    private class LCompandingKey;
}
