// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.ColorProfiles.Companding;

/// <summary>
/// Implements Rec. 2020 companding function.
/// </summary>
/// <remarks>
/// <see href="http://en.wikipedia.org/wiki/Rec._2020"/>
/// </remarks>
public static class Rec2020Companding
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
    /// Compresses the linear vectors to their nonlinear equivalents with respect to the energy.
    /// </summary>
    /// <param name="vectors">The span of vectors.</param>
    public static void Compress(Span<Vector4> vectors)
        => CompandingUtilities.Compand(vectors, CompandingUtilities.GetCompressLookupTable<Rec2020CompandingKey>(CompressFunction));

    /// <summary>
    /// Expands the nonlinear vectors to their linear equivalents with respect to the energy.
    /// </summary>
    /// <param name="vectors">The span of vectors.</param>
    public static void Expand(Span<Vector4> vectors)
        => CompandingUtilities.Compand(vectors, CompandingUtilities.GetExpandLookupTable<Rec2020CompandingKey>(ExpandFunction));

    /// <summary>
    /// Compresses the linear vector to its nonlinear equivalent with respect to the energy.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    public static Vector4 Compress(Vector4 vector)
        => CompandingUtilities.Compand(vector, CompandingUtilities.GetCompressLookupTable<Rec2020CompandingKey>(CompressFunction));

    /// <summary>
    /// Expands the nonlinear vector to its linear equivalent with respect to the energy.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    public static Vector4 Expand(Vector4 vector)
        => CompandingUtilities.Compand(vector, CompandingUtilities.GetExpandLookupTable<Rec2020CompandingKey>(ExpandFunction));

    private class Rec2020CompandingKey;
}
