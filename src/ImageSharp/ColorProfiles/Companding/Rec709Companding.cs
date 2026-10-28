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
/// Inputs use normalized RGB component units, not native integer channel values.
/// Compress accepts linear RGB. Expand accepts nonlinear RGB encoded with this transfer curve.
/// SDR inputs use [0,1]. Extended inputs use [-65504,65504] in the same normalized units.
/// Overloads without a colorRange parameter expect extended inputs.
/// RGB results are clamped to the selected normalized range. Alpha remains unchanged.
/// After curve evaluation, NaN results map to the lower bound; infinite results map to the corresponding bounds.
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
        // Rounded coefficients create a jump where the linear and power sections meet.
        // Table interpolation across this jump is limited to eight-bit SDR input.
        if (bitDepth == 8 && colorRange == PixelColorRange.Sdr)
        {
            CompandingUtilities.Compand(vectors, CompandingUtilities.GetCompressLookupTable<Rec2020CompandingKey>(CompressFunction));
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
        // Rounded coefficients create a jump where the linear and power sections meet.
        // Table interpolation across this jump is limited to eight-bit SDR input.
        if (bitDepth == 8 && colorRange == PixelColorRange.Sdr)
        {
            CompandingUtilities.Compand(vectors, CompandingUtilities.GetExpandLookupTable<Rec2020CompandingKey>(ExpandFunction));
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
        // Rounded coefficients create a jump where the linear and power sections meet.
        // Table interpolation across this jump is limited to eight-bit SDR input.
        if (bitDepth == 8 && colorRange == PixelColorRange.Sdr)
        {
            return CompandingUtilities.Compand(vector, CompandingUtilities.GetCompressLookupTable<Rec2020CompandingKey>(CompressFunction));
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
        // Rounded coefficients create a jump where the linear and power sections meet.
        // Table interpolation across this jump is limited to eight-bit SDR input.
        if (bitDepth == 8 && colorRange == PixelColorRange.Sdr)
        {
            return CompandingUtilities.Compand(vector, CompandingUtilities.GetExpandLookupTable<Rec2020CompandingKey>(ExpandFunction));
        }

        return ExpandCore(vector, colorRange);
    }

    /// <summary>
    /// Expands nonlinear RGB within the requested color range and retains alpha.
    /// </summary>
    /// <param name="vectors">The normalized nonlinear RGB colors. Each vector contains RGB in X, Y, Z and unchanged alpha in W.</param>
    /// <param name="colorRange">The range applied to the converted RGB components.</param>
    private static void ExpandCore(Span<Vector4> vectors, PixelColorRange colorRange)
        => CompandingUtilities.Compand(vectors, new ExpandOperator(1.099F, 0.099F, 0.081F), colorRange);

    /// <summary>
    /// Expands nonlinear RGB within the requested color range and retains alpha.
    /// </summary>
    /// <param name="vector">The normalized nonlinear RGB color. X, Y, Z contain RGB; W contains unchanged alpha.</param>
    /// <param name="colorRange">The range applied to the converted RGB components.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    private static Vector4 ExpandCore(Vector4 vector, PixelColorRange colorRange)
        => CompandingUtilities.Compand(vector, new ExpandOperator(1.099F, 0.099F, 0.081F), colorRange);

    /// <summary>
    /// Compresses linear RGB within the requested color range and retains alpha.
    /// </summary>
    /// <param name="vectors">The normalized linear RGB colors. Each vector contains RGB in X, Y, Z and unchanged alpha in W.</param>
    /// <param name="colorRange">The range applied to the converted RGB components.</param>
    private static void CompressCore(Span<Vector4> vectors, PixelColorRange colorRange)
        => CompandingUtilities.Compand(vectors, new CompressOperator(1.099F, 0.099F, 0.018F), colorRange);

    /// <summary>
    /// Compresses linear RGB within the requested color range and retains alpha.
    /// </summary>
    /// <param name="vector">The normalized linear RGB color. X, Y, Z contain RGB; W contains unchanged alpha.</param>
    /// <param name="colorRange">The range applied to the converted RGB components.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    private static Vector4 CompressCore(Vector4 vector, PixelColorRange colorRange)
        => CompandingUtilities.Compand(vector, new CompressOperator(1.099F, 0.099F, 0.018F), colorRange);

    private class Rec2020CompandingKey;
}
