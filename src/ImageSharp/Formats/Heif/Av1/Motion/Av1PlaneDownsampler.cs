// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Halves the resolution of an eight-bit plane with the normative symmetric-even filter.
/// </summary>
/// <remarks>
/// <para>
/// The image pyramid of dense flow estimation uses only repeated halving, so this class implements only the factor-of-two resize.
/// </para>
/// <para>
/// In both passes, one vector of lanes is one vector of outputs. The horizontal pass splits a row into its even-position and odd-position samples once.
/// After that, each of the eight taps is a plain shifted load of one of those two streams.
/// The vertical pass combines eight whole rows, so it never gathers a column.
/// Neither pass tests a boundary per sample. The horizontal pass reads clamped padding, and the vertical pass clamps each row index once.
/// </para>
/// </remarks>
internal static partial class Av1PlaneDownsampler
{
    /// <summary>
    /// The fractional bits carried by the filter taps.
    /// </summary>
    private const int FilterBits = 7;

    /// <summary>
    /// The number of samples repeated at each end of a separated stream.
    /// </summary>
    /// <remarks>
    /// Output <c>k</c> reads the even stream as far as <c>k + 2</c> and the odd stream as far back as <c>k - 2</c>.
    /// Thus two repeated samples at each end cover every read of the first and last outputs.
    /// The repeats hold the first and last source sample, which is the clamped value of a kernel read past the end of a line.
    /// </remarks>
    private const int SeparationPadding = 2;

    /// <summary>
    /// Halves both axes of one eight-bit plane.
    /// </summary>
    /// <param name="allocator">The allocator of the intermediate plane.</param>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The source row stride.</param>
    /// <param name="width">The source width.</param>
    /// <param name="height">The source height.</param>
    /// <param name="destination">The halved samples.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <remarks>
    /// An odd extent produces one more output than half of it, and the extra output reads past the end of the line.
    /// The clamped padding of each prepared stream covers that read, so both parities take the same path.
    /// </remarks>
    public static void Halve(
        MemoryAllocator allocator,
        ReadOnlySpan<byte> source,
        int sourceStride,
        int width,
        int height,
        Span<byte> destination,
        int destinationStride)
    {
        int halvedWidth = (width + 1) >> 1;
        int halvedHeight = (height + 1) >> 1;

        // The intermediate plane holds every source row at halved width.
        // Every row reuses the two separated streams, so one allocation holds the plane and one pair of stream buffers.
        int separatedLength = halvedWidth + (2 * SeparationPadding);
        using IMemoryOwner<byte> downsampleStorageOwner = allocator.Allocate<byte>((halvedWidth * height) + (2 * separatedLength));

        Span<byte> downsampleStorage = downsampleStorageOwner.Memory.Span;
        Span<byte> intermediate = downsampleStorage[..(halvedWidth * height)];
        Span<byte> even = downsampleStorage.Slice(halvedWidth * height, separatedLength);
        Span<byte> odd = downsampleStorage.Slice((halvedWidth * height) + separatedLength, separatedLength);

        HalveRows(source, sourceStride, width, height, halvedWidth, intermediate, even, odd);
        HalveColumns(intermediate, halvedWidth, height, halvedHeight, destination, destinationStride);
    }

    /// <summary>
    /// Halves the width of every source row into the intermediate plane.
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The source row stride.</param>
    /// <param name="width">The source width.</param>
    /// <param name="height">The source height.</param>
    /// <param name="halvedWidth">The halved width.</param>
    /// <param name="intermediate">The halved-width samples, at a stride of <paramref name="halvedWidth"/>.</param>
    /// <param name="even">The buffer of even-position samples.</param>
    /// <param name="odd">The buffer of odd-position samples.</param>
    /// <remarks>
    /// Output <c>k</c> is the weighted sum of source samples <c>2k - 3</c> through <c>2k + 4</c>.
    /// With <c>E[k] = source[2k]</c> and <c>O[k] = source[2k + 1]</c>, the eight kernel positions are <c>O[k-2]</c>, <c>E[k-1]</c>, <c>O[k-1]</c>,
    /// <c>E[k]</c>, <c>O[k]</c>, <c>E[k+1]</c>, <c>O[k+1]</c> and <c>E[k+2]</c>.
    /// Thus every tap is a shifted read of one of two streams, and the accumulation needs no shuffle.
    /// </remarks>
    private static void HalveRows(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int width,
        int height,
        int halvedWidth,
        Span<byte> intermediate,
        Span<byte> even,
        Span<byte> odd)
    {
        ref byte evenBase = ref MemoryMarshal.GetReference(even);
        ref byte oddBase = ref MemoryMarshal.GetReference(odd);
        ref byte intermediateBase = ref MemoryMarshal.GetReference(intermediate);

        for (int row = 0; row < height; row++)
        {
            Separate(source.Slice(row * sourceStride, width), halvedWidth, even, odd);

            // The arguments follow the kernel positions, not the order in which the streams were built.
            Filter<SymmetricEvenOperator>.Apply(
                ref Unsafe.Add(ref oddBase, SeparationPadding - 2),
                ref Unsafe.Add(ref evenBase, SeparationPadding - 1),
                ref Unsafe.Add(ref oddBase, SeparationPadding - 1),
                ref Unsafe.Add(ref evenBase, SeparationPadding),
                ref Unsafe.Add(ref oddBase, SeparationPadding),
                ref Unsafe.Add(ref evenBase, SeparationPadding + 1),
                ref Unsafe.Add(ref oddBase, SeparationPadding + 1),
                ref Unsafe.Add(ref evenBase, SeparationPadding + 2),
                ref Unsafe.Add(ref intermediateBase, row * halvedWidth),
                halvedWidth);
        }
    }

    /// <summary>
    /// Halves the height of the intermediate plane into the destination.
    /// </summary>
    /// <param name="intermediate">The halved-width samples.</param>
    /// <param name="halvedWidth">The intermediate width and stride.</param>
    /// <param name="height">The intermediate height.</param>
    /// <param name="halvedHeight">The destination height.</param>
    /// <param name="destination">The halved samples.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <remarks>
    /// The kernel runs along a column, but the method never gathers a column. Output row <c>r</c> samples between source rows <c>2r</c> and <c>2r + 1</c>.
    /// Thus its eight tap streams are the eight whole rows <c>2r - 3</c> through <c>2r + 4</c>, each clamped to the plane.
    /// A clamped row index repeats the first or last row, which gives the same result as a clamp of each sample.
    /// </remarks>
    private static void HalveColumns(
        ReadOnlySpan<byte> intermediate,
        int halvedWidth,
        int height,
        int halvedHeight,
        Span<byte> destination,
        int destinationStride)
    {
        ref byte intermediateBase = ref MemoryMarshal.GetReference(intermediate);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        int last = height - 1;

        for (int row = 0; row < halvedHeight; row++)
        {
            int position = row * 2;

            Filter<SymmetricEvenOperator>.Apply(
                ref Row(ref intermediateBase, halvedWidth, position - 3, last),
                ref Row(ref intermediateBase, halvedWidth, position - 2, last),
                ref Row(ref intermediateBase, halvedWidth, position - 1, last),
                ref Row(ref intermediateBase, halvedWidth, position, last),
                ref Row(ref intermediateBase, halvedWidth, position + 1, last),
                ref Row(ref intermediateBase, halvedWidth, position + 2, last),
                ref Row(ref intermediateBase, halvedWidth, position + 3, last),
                ref Row(ref intermediateBase, halvedWidth, position + 4, last),
                ref Unsafe.Add(ref destinationBase, row * destinationStride),
                halvedWidth);
        }
    }

    /// <summary>
    /// Gets the first sample of one intermediate row, clamped to the plane.
    /// </summary>
    /// <param name="intermediate">The first sample of the intermediate plane.</param>
    /// <param name="stride">The intermediate row stride.</param>
    /// <param name="row">The requested row, which can lie outside the plane.</param>
    /// <param name="last">The index of the final row.</param>
    /// <returns>The first sample of the clamped row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref byte Row(ref byte intermediate, int stride, int row, int last)
        => ref Unsafe.Add(ref intermediate, Math.Clamp(row, 0, last) * stride);

    /// <summary>
    /// Splits one source row into its even-position and odd-position samples, with clamped padding.
    /// </summary>
    /// <param name="source">The source row.</param>
    /// <param name="halvedWidth">The number of samples in each separated stream.</param>
    /// <param name="even">Receives the even-position samples at <see cref="SeparationPadding"/>.</param>
    /// <param name="odd">Receives the odd-position samples at <see cref="SeparationPadding"/>.</param>
    /// <remarks>
    /// An odd source width leaves the final odd-position sample past the end of the row.
    /// The method writes the final source sample there, which is the clamped value of the kernel read.
    /// </remarks>
    private static void Separate(ReadOnlySpan<byte> source, int halvedWidth, Span<byte> even, Span<byte> odd)
    {
        int last = source.Length - 1;

        // An odd row ends with one sample after its last full pair. The bulk separation covers every full pair.
        // Then the code writes that trailing sample to both streams. In the odd stream, it is the clamped value of the read past the end.
        int pairs = source.Length >> 1;
        SeparatePairs(source, pairs, even[SeparationPadding..], odd[SeparationPadding..]);

        if (pairs < halvedWidth)
        {
            even[SeparationPadding + pairs] = source[last];
            odd[SeparationPadding + pairs] = source[last];
        }

        // The repeats at each end stand in for the clamped reads of the first and last outputs.
        byte first = source[0];
        byte final = source[last];
        for (int index = 0; index < SeparationPadding; index++)
        {
            even[index] = first;
            odd[index] = first;
            even[SeparationPadding + halvedWidth + index] = final;
            odd[SeparationPadding + halvedWidth + index] = final;
        }
    }

    /// <summary>
    /// Writes the even-position and odd-position sample of every complete pair.
    /// </summary>
    /// <param name="source">The source row.</param>
    /// <param name="pairs">The number of complete sample pairs.</param>
    /// <param name="even">Receives the even-position samples.</param>
    /// <param name="odd">Receives the odd-position samples.</param>
    /// <remarks>
    /// A pair of adjacent bytes is one sixteen-bit lane on a little-endian runtime, and a narrow of such a lane keeps its low byte.
    /// Thus the even samples are the narrow of two loaded vectors. The odd samples are the narrow of the same vectors shifted down by eight bits.
    /// Each stage reads two source vectors and writes one vector to each stream, so each store holds half the bytes of the loads.
    /// A runtime that is not little-endian uses the scalar loop, where the pair order is explicit.
    /// </remarks>
    private static void SeparatePairs(ReadOnlySpan<byte> source, int pairs, Span<byte> even, Span<byte> odd)
    {
        ref byte sourceBase = ref MemoryMarshal.GetReference(source);
        ref byte evenBase = ref MemoryMarshal.GetReference(even);
        ref byte oddBase = ref MemoryMarshal.GetReference(odd);
        int i = 0;

        if (BitConverter.IsLittleEndian)
        {
            if (Vector512.IsHardwareAccelerated)
            {
                int vectorEnd = pairs - Vector512<byte>.Count;
                for (; i <= vectorEnd; i += Vector512<byte>.Count)
                {
                    Vector512<ushort> low = Vector512.LoadUnsafe(ref sourceBase, (nuint)(i * 2)).AsUInt16();
                    Vector512<ushort> high = Vector512.LoadUnsafe(ref sourceBase, (nuint)((i * 2) + Vector512<byte>.Count)).AsUInt16();

                    Vector512.Narrow(low, high).StoreUnsafe(ref evenBase, (nuint)i);
                    Vector512.Narrow(low >>> 8, high >>> 8).StoreUnsafe(ref oddBase, (nuint)i);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int vectorEnd = pairs - Vector256<byte>.Count;
                for (; i <= vectorEnd; i += Vector256<byte>.Count)
                {
                    Vector256<ushort> low = Vector256.LoadUnsafe(ref sourceBase, (nuint)(i * 2)).AsUInt16();
                    Vector256<ushort> high = Vector256.LoadUnsafe(ref sourceBase, (nuint)((i * 2) + Vector256<byte>.Count)).AsUInt16();

                    // Vector256.Narrow returns the lanes in source order.
                    // The x86 pack instruction works per 128-bit half, and the runtime adds the permute that restores the order.
                    Vector256.Narrow(low, high).StoreUnsafe(ref evenBase, (nuint)i);
                    Vector256.Narrow(low >>> 8, high >>> 8).StoreUnsafe(ref oddBase, (nuint)i);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int vectorEnd = pairs - Vector128<byte>.Count;
                for (; i <= vectorEnd; i += Vector128<byte>.Count)
                {
                    Vector128<ushort> low = Vector128.LoadUnsafe(ref sourceBase, (nuint)(i * 2)).AsUInt16();
                    Vector128<ushort> high = Vector128.LoadUnsafe(ref sourceBase, (nuint)((i * 2) + Vector128<byte>.Count)).AsUInt16();

                    Vector128.Narrow(low, high).StoreUnsafe(ref evenBase, (nuint)i);
                    Vector128.Narrow(low >>> 8, high >>> 8).StoreUnsafe(ref oddBase, (nuint)i);
                }
            }
        }

        for (; i < pairs; i++)
        {
            Unsafe.Add(ref evenBase, i) = Unsafe.Add(ref sourceBase, i * 2);
            Unsafe.Add(ref oddBase, i) = Unsafe.Add(ref sourceBase, (i * 2) + 1);
        }
    }
}
