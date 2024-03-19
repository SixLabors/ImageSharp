// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.ColorProfiles.Companding;

/// <summary>
/// Implements L* companding.
/// </summary>
/// <remarks>
/// For more info see:
/// <see href="http://www.brucelindbloom.com/index.html?Eqn_RGB_to_XYZ.html"/>
/// <see href="http://www.brucelindbloom.com/index.html?Eqn_XYZ_to_RGB.html"/>
/// </remarks>
public static class LCompanding
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
    /// Compresses the linear vectors to their nonlinear equivalents with respect to the energy.
    /// </summary>
    /// <param name="vectors">The span of vectors.</param>
    public static void Compress(Span<Vector4> vectors)
        => CompandingUtilities.Compand(vectors, CompandingUtilities.GetLookupTable<LCompandingKey>(CompressFunction).Value);

    /// <summary>
    /// Expands the nonlinear vectors to their linear equivalents with respect to the energy.
    /// </summary>
    /// <param name="vectors">The span of vectors.</param>
    public static void Expand(Span<Vector4> vectors)
        => CompandingUtilities.Compand(vectors, CompandingUtilities.GetLookupTable<LCompandingKey>(ExpandFunction).Value);

    /// <summary>
    /// Compresses the linear vector to its nonlinear equivalent with respect to the energy.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    public static Vector4 Compress(Vector4 vector)
        => CompandingUtilities.Compand(vector, CompandingUtilities.GetLookupTable<LCompandingKey>(CompressFunction).Value);

    /// <summary>
    /// Compresses the linear vector to its nonlinear equivalent with respect to the energy.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    public static Vector4 Expand(Vector4 vector)
        => CompandingUtilities.Compand(vector, CompandingUtilities.GetLookupTable<LCompandingKey>(ExpandFunction).Value);

    private class LCompandingKey;
}
