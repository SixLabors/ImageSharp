// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.ColorProfiles.Companding;

/// <summary>
/// Implements sRGB companding.
/// </summary>
/// <remarks>
/// For more info see:
/// <see href="http://www.brucelindbloom.com/index.html?Eqn_RGB_to_XYZ.html"/>
/// <see href="http://www.brucelindbloom.com/index.html?Eqn_XYZ_to_RGB.html"/>
/// </remarks>
public static class SRgbCompanding
{
    private static Func<double, double, double> CompressFunction
        => (d, _) =>
        {
            if (d <= (0.04045 / 12.92))
            {
                return d * 12.92;
            }

            return (1.055 * Math.Pow(d, 1.0 / 2.4)) - 0.055;
        };

    private static Func<double, double, double> ExpandFunction
        => (d, _) =>
        {
            if (d <= 0.04045)
            {
                return d / 12.92;
            }

            return Math.Pow((d + 0.055) / 1.055, 2.4);
        };

    /// <summary>
    /// Compresses the linear vectors to their nonlinear equivalents with respect to the energy.
    /// </summary>
    /// <param name="vectors">The span of vectors.</param>
    public static void Compress(Span<Vector4> vectors)
        => CompandingUtilities.Compand(vectors, CompandingUtilities.GetCompressLookupTable<SRgbCompandingKey>(CompressFunction));

    /// <summary>
    /// Expands the nonlinear vectors to their linear equivalents with respect to the energy.
    /// </summary>
    /// <param name="vectors">The span of vectors.</param>
    public static void Expand(Span<Vector4> vectors)
        => CompandingUtilities.Compand(vectors, CompandingUtilities.GetExpandLookupTable<SRgbCompandingKey>(ExpandFunction));

    /// <summary>
    /// Compresses the linear vector to its nonlinear equivalent with respect to the energy.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    public static Vector4 Compress(Vector4 vector)
        => CompandingUtilities.Compand(vector, CompandingUtilities.GetCompressLookupTable<SRgbCompandingKey>(CompressFunction));

    /// <summary>
    /// Expands the nonlinear vector to its linear equivalent with respect to the energy.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    public static Vector4 Expand(Vector4 vector)
        => CompandingUtilities.Compand(vector, CompandingUtilities.GetExpandLookupTable<SRgbCompandingKey>(ExpandFunction));

    private class SRgbCompandingKey;
}
