// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.PixelFormats.PixelBlenders;

/// <summary>
/// Defines color blending and alpha composition for pixel vectors.
/// </summary>
internal interface IPixelBlenderOperator
{
    // Operators own composition arithmetic and declare the alpha representation required by their equations.
    // The shared blender owns SIMD traversal. PixelOperations owns pixel conversion; pixel formats own rounding and storage layout.

    /// <summary>
    /// Gets a value indicating whether composition uses pixel vectors with associated alpha.
    /// </summary>
    public static abstract bool IsAssociatedAlpha { get; }

    /// <summary>
    /// Blends one source pixel vector with one background pixel vector.
    /// </summary>
    /// <param name="background">The background pixel vector.</param>
    /// <param name="source">The source pixel vector.</param>
    /// <param name="amount">The source opacity in the range 0 through 1.</param>
    /// <returns>The blended pixel vector.</returns>
    public static abstract Vector4 Invoke(Vector4 background, Vector4 source, float amount);

    /// <summary>
    /// Blends two source pixel vectors with two background pixel vectors.
    /// </summary>
    /// <param name="background">The background pixel vectors.</param>
    /// <param name="source">The source pixel vectors.</param>
    /// <param name="amount">The source opacity repeated across each pixel's four lanes.</param>
    /// <returns>The blended pixel vectors.</returns>
    public static abstract Vector256<float> Invoke(Vector256<float> background, Vector256<float> source, Vector256<float> amount);

    /// <summary>
    /// Blends four source pixel vectors with four background pixel vectors.
    /// </summary>
    /// <param name="background">The background pixel vectors.</param>
    /// <param name="source">The source pixel vectors.</param>
    /// <param name="amount">The source opacity repeated across each pixel's four lanes.</param>
    /// <returns>The blended pixel vectors.</returns>
    public static abstract Vector512<float> Invoke(Vector512<float> background, Vector512<float> source, Vector512<float> amount);
}
