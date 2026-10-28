// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

/// <summary>
/// Reads TIFF components from byte-aligned or bit-packed samples into scalar values and SIMD registers.
/// </summary>
/// <typeparam name="TSample">The decoded component type.</typeparam>
internal interface ITiffComponentSource<TSample>
    where TSample : unmanaged
{
    /// <summary>
    /// Gets the number of stored color components used by the source layout.
    /// </summary>
    int ComponentCount { get; }

    /// <summary>
    /// Reads one sample in the component units defined by this source layout.
    /// </summary>
    /// <param name="source0">The packed bytes or first component plane.</param>
    /// <param name="source1">The second component plane, if planar.</param>
    /// <param name="source2">The third component plane, if planar.</param>
    /// <param name="source3">The fourth component plane, if planar.</param>
    /// <param name="index">The pixel index within the supplied row block.</param>
    /// <param name="c0">The first decoded component.</param>
    /// <param name="c1">The second decoded component.</param>
    /// <param name="c2">The third decoded component.</param>
    /// <param name="c3">The fourth decoded component.</param>
    void Read(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        int index,
        out TSample c0,
        out TSample c1,
        out TSample c2,
        out TSample c3);

    /// <summary>
    /// Reads one 128-bit group in the component units and register layout defined by this source.
    /// </summary>
    /// <param name="source0">The packed bytes or first component plane.</param>
    /// <param name="source1">The second component plane, if planar.</param>
    /// <param name="source2">The third component plane, if planar.</param>
    /// <param name="source3">The fourth component plane, if planar.</param>
    /// <param name="index">The first pixel index within the supplied row block.</param>
    /// <param name="c0">The first decoded register.</param>
    /// <param name="c1">The second decoded register.</param>
    /// <param name="c2">The third decoded register.</param>
    /// <param name="c3">The fourth decoded register.</param>
    void Read(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        int index,
        out Vector128<TSample> c0,
        out Vector128<TSample> c1,
        out Vector128<TSample> c2,
        out Vector128<TSample> c3);

    /// <summary>
    /// Reads one 256-bit group in the component units and register layout defined by this source.
    /// </summary>
    /// <param name="source0">The packed bytes or first component plane.</param>
    /// <param name="source1">The second component plane, if planar.</param>
    /// <param name="source2">The third component plane, if planar.</param>
    /// <param name="source3">The fourth component plane, if planar.</param>
    /// <param name="index">The first pixel index within the supplied row block.</param>
    /// <param name="c0">The first decoded register.</param>
    /// <param name="c1">The second decoded register.</param>
    /// <param name="c2">The third decoded register.</param>
    /// <param name="c3">The fourth decoded register.</param>
    void Read(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        int index,
        out Vector256<TSample> c0,
        out Vector256<TSample> c1,
        out Vector256<TSample> c2,
        out Vector256<TSample> c3);

    /// <summary>
    /// Reads one 512-bit group in the component units and register layout defined by this source.
    /// </summary>
    /// <param name="source0">The packed bytes or first component plane.</param>
    /// <param name="source1">The second component plane, if planar.</param>
    /// <param name="source2">The third component plane, if planar.</param>
    /// <param name="source3">The fourth component plane, if planar.</param>
    /// <param name="index">The first pixel index within the supplied row block.</param>
    /// <param name="c0">The first decoded register.</param>
    /// <param name="c1">The second decoded register.</param>
    /// <param name="c2">The third decoded register.</param>
    /// <param name="c3">The fourth decoded register.</param>
    void Read(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        int index,
        out Vector512<TSample> c0,
        out Vector512<TSample> c1,
        out Vector512<TSample> c2,
        out Vector512<TSample> c3);
}
