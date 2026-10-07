// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Reads TIFF components, applies color rules, and writes destination pixels.
/// </summary>
/// <typeparam name="TSource">The stored byte layout.</typeparam>
/// <typeparam name="TOperator">The color arithmetic.</typeparam>
/// <typeparam name="TStore">The destination layout.</typeparam>
/// <typeparam name="TSample">The decoded component type.</typeparam>
/// <typeparam name="TDestination">The destination storage element type.</typeparam>
internal readonly struct TiffOperatedColorConverter<TSource, TOperator, TStore, TSample, TDestination> : ITiffColorPipeline<TDestination>
    where TSource : struct, ITiffColorSource<TSample>
    where TOperator : struct, ITiffColorOperator<TSample>
    where TStore : struct, ITiffColorStore<TSample, TDestination>
    where TSample : unmanaged
    where TDestination : unmanaged
{
    private readonly TSource source;
    private readonly TOperator colorOperator;
    private readonly TStore store;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="TiffOperatedColorConverter{TSource, TOperator, TStore, TSample, TDestination}"/> struct.
    /// </summary>
    /// <param name="source">The source byte layout.</param>
    /// <param name="colorOperator">The color operation.</param>
    /// <param name="store">The destination layout.</param>
    public TiffOperatedColorConverter(TSource source, TOperator colorOperator, TStore store)
    {
        this.source = source;
        this.colorOperator = colorOperator;
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
    /// <param name="colorOperator">The color operation.</param>
    /// <param name="store">The destination pixel layout.</param>
    public static void Convert(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, Span<TDestination> destination, TSource source, TOperator colorOperator, TStore store)
    {
        TiffOperatedColorConverter<TSource, TOperator, TStore, TSample, TDestination> pipeline = new(source, colorOperator, store);
        TiffColorTraversal<TiffOperatedColorConverter<TSource, TOperator, TStore, TSample, TDestination>, TDestination>.Convert(source0, source1, source2, source3, destination, destination.Length / TStore.WordsPerPixel, pipeline);
    }

    /// <inheritdoc/>
    public void Convert512(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, Span<TDestination> destination, int index)
    {
        this.source.Read(source0, source1, source2, source3, index, out Vector512<TSample> c0, out Vector512<TSample> c1, out Vector512<TSample> c2, out Vector512<TSample> c3);
        this.colorOperator.Convert(ref c0, ref c1, ref c2, ref c3);
        this.store.Store(ref MemoryMarshal.GetReference(destination[(index * TStore.WordsPerPixel)..]), c0, c1, c2, c3);
    }

    /// <inheritdoc/>
    public void Convert256(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, Span<TDestination> destination, int index)
    {
        this.source.Read(source0, source1, source2, source3, index, out Vector256<TSample> c0, out Vector256<TSample> c1, out Vector256<TSample> c2, out Vector256<TSample> c3);
        this.colorOperator.Convert(ref c0, ref c1, ref c2, ref c3);
        this.store.Store(ref MemoryMarshal.GetReference(destination[(index * TStore.WordsPerPixel)..]), c0, c1, c2, c3);
    }

    /// <inheritdoc/>
    public void Convert128(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, Span<TDestination> destination, int index)
    {
        this.source.Read(source0, source1, source2, source3, index, out Vector128<TSample> c0, out Vector128<TSample> c1, out Vector128<TSample> c2, out Vector128<TSample> c3);
        this.colorOperator.Convert(ref c0, ref c1, ref c2, ref c3);
        this.store.Store(ref MemoryMarshal.GetReference(destination[(index * TStore.WordsPerPixel)..]), c0, c1, c2, c3);
    }

    /// <inheritdoc/>
    public void ConvertScalar(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, Span<TDestination> destination, int index)
    {
        this.source.Read(source0, source1, source2, source3, index, out TSample c0, out TSample c1, out TSample c2, out TSample c3);
        this.colorOperator.Convert(ref c0, ref c1, ref c2, ref c3);
        this.store.Store(ref MemoryMarshal.GetReference(destination[(index * TStore.WordsPerPixel)..]), c0, c1, c2, c3);
    }
}
