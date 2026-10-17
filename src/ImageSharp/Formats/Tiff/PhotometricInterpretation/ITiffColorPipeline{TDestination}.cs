// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Reads TIFF components, applies color rules, and writes destination pixels in complete groups.
/// </summary>
/// <typeparam name="TDestination">The destination element type.</typeparam>
internal interface ITiffColorPipeline<TDestination>
    where TDestination : unmanaged
{
    /// <summary>
    /// Gets the number of pixels processed by a 512-bit register operation.
    /// </summary>
    static abstract int Pixels512 { get; }

    /// <summary>
    /// Gets the number of pixels processed by a 256-bit register operation.
    /// </summary>
    static abstract int Pixels256 { get; }

    /// <summary>
    /// Gets the number of pixels processed by a 128-bit register operation.
    /// </summary>
    static abstract int Pixels128 { get; }

    /// <summary>
    /// Gets the number of destination elements that store one complete pixel.
    /// </summary>
    static abstract int DestinationElementsPerPixel { get; }

    /// <summary>
    /// Processes one complete 512-bit source group.
    /// </summary>
    /// <param name="source0">The packed source or first component plane.</param>
    /// <param name="source1">The second component plane, when used.</param>
    /// <param name="source2">The third component plane, when used.</param>
    /// <param name="source3">The fourth component plane, when used.</param>
    /// <param name="destination">The bounded destination group.</param>
    /// <param name="index">The first pixel of this group.</param>
    void Convert512(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        Span<TDestination> destination,
        int index);

    /// <summary>
    /// Processes one complete 256-bit source group.
    /// </summary>
    /// <param name="source0">The packed source or first component plane.</param>
    /// <param name="source1">The second component plane, when used.</param>
    /// <param name="source2">The third component plane, when used.</param>
    /// <param name="source3">The fourth component plane, when used.</param>
    /// <param name="destination">The bounded destination group.</param>
    /// <param name="index">The first pixel of this group.</param>
    void Convert256(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        Span<TDestination> destination,
        int index);

    /// <summary>
    /// Processes one complete 128-bit source group.
    /// </summary>
    /// <param name="source0">The packed source or first component plane.</param>
    /// <param name="source1">The second component plane, when used.</param>
    /// <param name="source2">The third component plane, when used.</param>
    /// <param name="source3">The fourth component plane, when used.</param>
    /// <param name="destination">The bounded destination group.</param>
    /// <param name="index">The first pixel of this group.</param>
    void Convert128(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        Span<TDestination> destination,
        int index);

    /// <summary>
    /// Processes one remaining pixel after the SIMD groups.
    /// </summary>
    /// <param name="source0">The packed source or first component plane.</param>
    /// <param name="source1">The second component plane, when used.</param>
    /// <param name="source2">The third component plane, when used.</param>
    /// <param name="source3">The fourth component plane, when used.</param>
    /// <param name="destination">The bounded destination group.</param>
    /// <param name="index">The remaining pixel index.</param>
    void ConvertScalar(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        Span<TDestination> destination,
        int index);
}
