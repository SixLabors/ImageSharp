// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.PixelFormats.PixelBlenders;

/// <summary>
/// Defines a Porter-Duff equation that can be applied by the shared pixel-blending traversal.
/// </summary>
internal interface IPixelBlenderOperator
{
    /// <summary>
    /// Blends one pixel represented by four RGBA lanes.
    /// </summary>
    /// <param name="background">The background RGBA lanes.</param>
    /// <param name="source">The source RGBA lanes.</param>
    /// <param name="amount">The source opacity in the range 0 through 1.</param>
    /// <returns>The blended RGBA lanes.</returns>
    public static abstract Vector4 Invoke(Vector4 background, Vector4 source, float amount);

    /// <summary>
    /// Blends two pixels represented by two consecutive groups of four RGBA lanes.
    /// </summary>
    /// <param name="background">The background RGBA lanes.</param>
    /// <param name="source">The source RGBA lanes.</param>
    /// <param name="amount">The source opacity repeated across each pixel's four lanes.</param>
    /// <returns>The blended RGBA lanes.</returns>
    public static abstract Vector256<float> Invoke(
        Vector256<float> background,
        Vector256<float> source,
        Vector256<float> amount);

    /// <summary>
    /// Blends four pixels represented by four consecutive groups of four RGBA lanes.
    /// </summary>
    /// <param name="background">The background RGBA lanes.</param>
    /// <param name="source">The source RGBA lanes.</param>
    /// <param name="amount">The source opacity repeated across each pixel's four lanes.</param>
    /// <returns>The blended RGBA lanes.</returns>
    public static abstract Vector512<float> Invoke(
        Vector512<float> background,
        Vector512<float> source,
        Vector512<float> amount);
}
