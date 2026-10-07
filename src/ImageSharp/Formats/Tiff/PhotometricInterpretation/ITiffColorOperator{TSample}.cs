// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Applies TIFF color and alpha rules to decoded component groups.
/// </summary>
/// <typeparam name="TSample">The decoded component type.</typeparam>
internal interface ITiffColorOperator<TSample>
    where TSample : unmanaged
{
    /// <summary>
    /// Gets the alpha representation of the converted, scaled vector samples.
    /// A value of <see cref="PixelAlphaRepresentation.None"/> means the operator supplies opaque alpha.
    /// </summary>
    PixelAlphaRepresentation AlphaRepresentation { get; }

    /// <summary>
    /// Converts one pixel in the source's component units.
    /// </summary>
    /// <param name="c0">The first source component.</param>
    /// <param name="c1">The second source component.</param>
    /// <param name="c2">The third source component.</param>
    /// <param name="c3">The fourth source component.</param>
    void Convert(ref TSample c0, ref TSample c1, ref TSample c2, ref TSample c3);

    /// <summary>
    /// Converts one 128-bit group in the source's register layout.
    /// </summary>
    /// <param name="c0">The first source register.</param>
    /// <param name="c1">The second source register.</param>
    /// <param name="c2">The third source register.</param>
    /// <param name="c3">The fourth source register.</param>
    void Convert(ref Vector128<TSample> c0, ref Vector128<TSample> c1, ref Vector128<TSample> c2, ref Vector128<TSample> c3);

    /// <summary>
    /// Converts one 256-bit group in the source's register layout.
    /// </summary>
    /// <param name="c0">The first source register.</param>
    /// <param name="c1">The second source register.</param>
    /// <param name="c2">The third source register.</param>
    /// <param name="c3">The fourth source register.</param>
    void Convert(ref Vector256<TSample> c0, ref Vector256<TSample> c1, ref Vector256<TSample> c2, ref Vector256<TSample> c3);

    /// <summary>
    /// Converts one 512-bit group in the source's register layout.
    /// </summary>
    /// <param name="c0">The first source register.</param>
    /// <param name="c1">The second source register.</param>
    /// <param name="c2">The third source register.</param>
    /// <param name="c3">The fourth source register.</param>
    void Convert(ref Vector512<TSample> c0, ref Vector512<TSample> c1, ref Vector512<TSample> c2, ref Vector512<TSample> c3);
}
