// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp;

/// <summary>
/// Defines a stateless operation over packed pixel components.
/// </summary>
internal interface IComponentShuffle
{
    /// <summary>
    /// Reorders one packed pixel.
    /// </summary>
    /// <param name="source">The source components, with the first component in the least-significant byte.</param>
    /// <returns>The reordered packed components.</returns>
    public static abstract uint Invoke(uint source);

    /// <summary>
    /// Reorders the packed pixels in a 128-bit vector.
    /// </summary>
    /// <param name="source">The source pixels.</param>
    /// <returns>The reordered pixels.</returns>
    public static abstract Vector128<byte> Invoke(Vector128<byte> source);
}
