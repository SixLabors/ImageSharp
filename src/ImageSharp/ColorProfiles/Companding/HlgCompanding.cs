// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.ColorProfiles.Companding;

/// <summary>
/// Applies the ARIB STD-B67 hybrid log-gamma component transfer curve.
/// </summary>
/// <remarks>
/// RGB components use normalized units. Negative components use the magnitude curve with their original sign.
/// Expand produces normalized scene-linear RGB. Compress accepts scene-linear RGB.
/// Display-luminance adjustment is separate from this component transfer curve.
/// See <see href="https://www.arib.or.jp/english/html/overview/doc/2-STD-B67v2_0.pdf">ARIB STD-B67</see>, Table 2-3.
/// RGB results are bounded to [-65504,65504]. NaN maps to the lower bound; infinities map to the corresponding bounds.
/// Alpha remains unchanged.
/// </remarks>
public static partial class HlgCompanding
{
    /// <summary>
    /// Converts nonlinear RGB to linear RGB and retains alpha.
    /// </summary>
    /// <param name="vectors">The normalized nonlinear colors with RGB in X, Y, Z and unchanged alpha in W.</param>
    public static void Expand(Span<Vector4> vectors)
        => CompandingUtilities.Compand(vectors, default(ExpandOperator), PixelColorRange.Extended);

    /// <summary>
    /// Converts nonlinear RGB to linear RGB and retains alpha.
    /// </summary>
    /// <param name="vector">The normalized nonlinear color with RGB in X, Y, Z and unchanged alpha in W.</param>
    /// <returns>The converted RGB color with unchanged alpha.</returns>
    public static Vector4 Expand(Vector4 vector)
        => CompandingUtilities.Compand(vector, default(ExpandOperator), PixelColorRange.Extended);

    /// <summary>
    /// Converts linear RGB to nonlinear RGB and retains alpha.
    /// </summary>
    /// <param name="vectors">The normalized linear colors with RGB in X, Y, Z and unchanged alpha in W.</param>
    public static void Compress(Span<Vector4> vectors)
        => CompandingUtilities.Compand(vectors, default(CompressOperator), PixelColorRange.Extended);

    /// <summary>
    /// Converts linear RGB to nonlinear RGB and retains alpha.
    /// </summary>
    /// <param name="vector">The normalized linear color with RGB in X, Y, Z and unchanged alpha in W.</param>
    /// <returns>The converted RGB color with unchanged alpha.</returns>
    public static Vector4 Compress(Vector4 vector)
        => CompandingUtilities.Compand(vector, default(CompressOperator), PixelColorRange.Extended);
}
