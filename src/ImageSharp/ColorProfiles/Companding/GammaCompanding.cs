// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.ColorProfiles.Companding;

/// <summary>
/// Implements gamma companding.
/// </summary>
/// <remarks>
/// <see href="http://www.brucelindbloom.com/index.html?Eqn_RGB_to_XYZ.html"/>
/// <see href="http://www.brucelindbloom.com/index.html?Eqn_XYZ_to_RGB.html"/>
/// </remarks>
public static class GammaCompanding
{
    private static Func<double, double, double> CompressFunction => (d, m) => Math.Pow(d, 1 / m);

    private static Func<double, double, double> ExpandFunction => Math.Pow;

    /// <summary>
    /// Compresses the linear vectors to their nonlinear equivalents with respect to the energy.
    /// </summary>
    /// <param name="vectors">The span of vectors.</param>
    /// <param name="gamma">The gamma value.</param>
    public static void Compress(Span<Vector4> vectors, double gamma)
        => CompandingUtilities.Compand(vectors, CompandingUtilities.GetCompressLookupTable<GammaCompandingKey>(CompressFunction, gamma));

    /// <summary>
    /// Expands the nonlinear vectors to their linear equivalents with respect to the energy.
    /// </summary>
    /// <param name="vectors">The span of vectors.</param>
    /// <param name="gamma">The gamma value.</param>
    public static void Expand(Span<Vector4> vectors, double gamma)
        => CompandingUtilities.Compand(vectors, CompandingUtilities.GetExpandLookupTable<GammaCompandingKey>(ExpandFunction, gamma));

    /// <summary>
    /// Compresses the linear vector to its nonlinear equivalent with respect to the energy.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <param name="gamma">The gamma value.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    public static Vector4 Compress(Vector4 vector, double gamma)
        => CompandingUtilities.Compand(vector, CompandingUtilities.GetCompressLookupTable<GammaCompandingKey>(CompressFunction, gamma));

    /// <summary>
    /// Expands the nonlinear vector to its linear equivalent with respect to the energy.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <param name="gamma">The gamma value.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    public static Vector4 Expand(Vector4 vector, double gamma)
        => CompandingUtilities.Compand(vector, CompandingUtilities.GetExpandLookupTable<GammaCompandingKey>(ExpandFunction, gamma));

    private class GammaCompandingKey;
}
