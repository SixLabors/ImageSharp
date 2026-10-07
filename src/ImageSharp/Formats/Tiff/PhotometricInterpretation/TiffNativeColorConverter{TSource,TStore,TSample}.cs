// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Reads TIFF components and writes pixels when no color calculation is needed.
/// </summary>
/// <typeparam name="TSource">The stored byte layout.</typeparam>
/// <typeparam name="TStore">The destination pixel layout.</typeparam>
/// <typeparam name="TSample">The decoded component type.</typeparam>
internal readonly struct TiffNativeColorConverter<TSource, TStore, TSample> : ITiffColorPipeline<TSample>
    where TSource : struct, ITiffColorSource<TSample>
    where TStore : struct, ITiffColorStore<TSample, TSample>
    where TSample : unmanaged
{
    private readonly TSource source;
    private readonly TStore store;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffNativeColorConverter{TSource, TStore, TSample}"/> struct.
    /// </summary>
    /// <param name="source">The stored source layout.</param>
    /// <param name="store">The destination pixel layout.</param>
    public TiffNativeColorConverter(TSource source, TStore store)
    {
        this.source = source;
        this.store = store;
    }

    /// <inheritdoc/>
    public static int Pixels512 => Vector512<TSample>.Count;

    /// <inheritdoc/>
    public static int Pixels256 => Vector256<TSample>.Count;

    /// <inheritdoc/>
    public static int Pixels128 => Vector128<TSample>.Count;

    /// <inheritdoc/>
    public static int DestinationElementsPerPixel => TStore.WordsPerPixel;

    /// <summary>
    /// Converts all complete pixels in the supplied source block.
    /// </summary>
    /// <param name="source0">The packed source or first component plane.</param>
    /// <param name="source1">The second component plane, if used.</param>
    /// <param name="source2">The third component plane, if used.</param>
    /// <param name="source3">The fourth component plane, if used.</param>
    /// <param name="destination">The destination pixel words.</param>
    /// <param name="source">The stored source layout.</param>
    /// <param name="store">The destination pixel layout.</param>
    public static void Convert(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, Span<TSample> destination, TSource source, TStore store)
    {
        TiffNativeColorConverter<TSource, TStore, TSample> pipeline = new(source, store);
        TiffColorTraversal<TiffNativeColorConverter<TSource, TStore, TSample>, TSample>.Convert(source0, source1, source2, source3, destination, destination.Length / TStore.WordsPerPixel, pipeline);
    }

    /// <inheritdoc/>
    public void Convert512(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, Span<TSample> destination, int index)
    {
        this.source.Read(source0, source1, source2, source3, index, out Vector512<TSample> c0, out Vector512<TSample> c1, out Vector512<TSample> c2, out Vector512<TSample> c3);
        this.store.Store(ref MemoryMarshal.GetReference(destination[(index * TStore.WordsPerPixel)..]), c0, c1, c2, c3);
    }

    /// <inheritdoc/>
    public void Convert256(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, Span<TSample> destination, int index)
    {
        this.source.Read(source0, source1, source2, source3, index, out Vector256<TSample> c0, out Vector256<TSample> c1, out Vector256<TSample> c2, out Vector256<TSample> c3);
        this.store.Store(ref MemoryMarshal.GetReference(destination[(index * TStore.WordsPerPixel)..]), c0, c1, c2, c3);
    }

    /// <inheritdoc/>
    public void Convert128(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, Span<TSample> destination, int index)
    {
        this.source.Read(source0, source1, source2, source3, index, out Vector128<TSample> c0, out Vector128<TSample> c1, out Vector128<TSample> c2, out Vector128<TSample> c3);
        this.store.Store(ref MemoryMarshal.GetReference(destination[(index * TStore.WordsPerPixel)..]), c0, c1, c2, c3);
    }

    /// <inheritdoc/>
    public void ConvertScalar(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, Span<TSample> destination, int index)
    {
        this.source.Read(source0, source1, source2, source3, index, out TSample c0, out TSample c1, out TSample c2, out TSample c3);
        this.store.Store(ref MemoryMarshal.GetReference(destination[(index * TStore.WordsPerPixel)..]), c0, c1, c2, c3);
    }
}
