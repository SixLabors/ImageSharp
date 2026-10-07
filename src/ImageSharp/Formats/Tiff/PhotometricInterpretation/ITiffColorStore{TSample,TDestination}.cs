// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Writes converted component groups in the destination pixel layout.
/// </summary>
/// <typeparam name="TSample">The source component type.</typeparam>
/// <typeparam name="TDestination">The destination storage word type.</typeparam>
internal interface ITiffColorStore<TSample, TDestination>
    where TSample : unmanaged
    where TDestination : unmanaged
{
    /// <summary>
    /// Gets the number of destination storage words per pixel.
    /// </summary>
    static abstract int WordsPerPixel { get; }

    /// <summary>
    /// Stores one scalar pixel.
    /// </summary>
    /// <param name="destination">The first destination element of the pixel.</param>
    /// <param name="c0">The first decoded component.</param>
    /// <param name="c1">The second decoded component.</param>
    /// <param name="c2">The third decoded component.</param>
    /// <param name="c3">The fourth decoded component.</param>
    void Store(ref TDestination destination, TSample c0, TSample c1, TSample c2, TSample c3);

    /// <summary>
    /// Stores the pixels represented by four 128-bit source-layout registers.
    /// </summary>
    /// <param name="destination">The first destination element of the group.</param>
    /// <param name="c0">The first decoded register.</param>
    /// <param name="c1">The second decoded register.</param>
    /// <param name="c2">The third decoded register.</param>
    /// <param name="c3">The fourth decoded register.</param>
    void Store(ref TDestination destination, Vector128<TSample> c0, Vector128<TSample> c1, Vector128<TSample> c2, Vector128<TSample> c3);

    /// <summary>
    /// Stores the pixels represented by four 256-bit source-layout registers.
    /// </summary>
    /// <param name="destination">The first destination element of the group.</param>
    /// <param name="c0">The first decoded register.</param>
    /// <param name="c1">The second decoded register.</param>
    /// <param name="c2">The third decoded register.</param>
    /// <param name="c3">The fourth decoded register.</param>
    void Store(ref TDestination destination, Vector256<TSample> c0, Vector256<TSample> c1, Vector256<TSample> c2, Vector256<TSample> c3);

    /// <summary>
    /// Stores the pixels represented by four 512-bit source-layout registers.
    /// </summary>
    /// <param name="destination">The first destination element of the group.</param>
    /// <param name="c0">The first decoded register.</param>
    /// <param name="c1">The second decoded register.</param>
    /// <param name="c2">The third decoded register.</param>
    /// <param name="c3">The fourth decoded register.</param>
    void Store(ref TDestination destination, Vector512<TSample> c0, Vector512<TSample> c1, Vector512<TSample> c2, Vector512<TSample> c3);
}
