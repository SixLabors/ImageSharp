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
/// The image pyramid that dense flow estimation walks is built by repeated halving, so only the factor-of-two path of
/// the reference resizer is needed here.
/// </para>
/// <para>
/// Both passes are shaped so that one vector of lanes is one vector of outputs. The horizontal pass separates a row
/// into its even-position and odd-position samples once, after which each of the eight taps is a plain shifted load
/// of one of those two streams. The vertical pass combines eight whole rows, so it never gathers a column. Neither
/// pass tests a boundary, because the clamping that the reference performs per sample is written into the padding of
/// the prepared streams.
/// </para>
/// <para>
/// Reference: av1_resize_plane_to_half, av1_resize_horz_dir_c, av1_resize_vert_dir_c and down2_symeven, with the
/// separated layout of av1_resize_horz_dir_avx2 and the row-band layout of av1_resize_vert_dir_avx2.
/// </para>
/// </remarks>
internal static partial class Av1PlaneDownsampler
{
    /// <summary>
    /// The fractional bits carried by the filter taps.
    /// </summary>
    /// <remarks>Reference: FILTER_BITS.</remarks>
    private const int FilterBits = 7;

    /// <summary>
    /// The number of samples repeated at each end of a separated stream.
    /// </summary>
    /// <remarks>
    /// Output <c>k</c> reads the even stream as far as <c>k + 2</c> and the odd stream as far back as
    /// <c>k - 2</c>, so two repeated samples at each end cover every read of the first and last
    /// outputs. Those repeats hold the first and last source sample, which is what the reference
    /// reads when its kernel reaches past the end of a line.
    /// </remarks>
    private const int SeparationPadding = 2;

    /// <summary>
    /// Gets whether one output extent is exactly half of its input extent.
    /// </summary>
    /// <param name="length">The input extent.</param>
    /// <param name="halvedLength">The output extent.</param>
    /// <returns>Whether the output extent is the halved input extent.</returns> <remarks>Reference: get_down2_length
    /// and should_resize_by_half.</remarks>
    public static bool IsHalved(int length, int halvedLength)
        => (length + 1) >> 1 == halvedLength;

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
    /// An odd extent produces one more output than half of it, and the extra output reads past the
    /// end of the line. The clamped padding of each prepared stream covers that read, so both
    /// parities take the same path.
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

        // The intermediate plane holds every source row at halved width. The two separated streams
        // are reused by every row, so one pair of buffers is allocated beside it.
        int separatedLength = halvedWidth + (2 * SeparationPadding);
        using IMemoryOwner<byte> scratchOwner = allocator.Allocate<byte>((halvedWidth * height) + (2 * separatedLength));

        Span<byte> scratch = scratchOwner.Memory.Span;
        Span<byte> intermediate = scratch[..(halvedWidth * height)];
        Span<byte> even = scratch.Slice(halvedWidth * height, separatedLength);
        Span<byte> odd = scratch.Slice((halvedWidth * height) + separatedLength, separatedLength);

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
    /// <param name="even">The scratch buffer of even-position samples.</param>
    /// <param name="odd">The scratch buffer of odd-position samples.</param>
    /// <remarks>
    /// Output <c>k</c> is the weighted sum of source samples <c>2k - 3</c> through <c>2k + 4</c>.
    /// With <c>E[k] = source[2k]</c> and <c>O[k] = source[2k + 1]</c>, the eight kernel positions are
    /// <c>O[k-2]</c>, <c>E[k-1]</c>, <c>O[k-1]</c>, <c>E[k]</c>, <c>O[k]</c>, <c>E[k+1]</c>,
    /// <c>O[k+1]</c> and <c>E[k+2]</c>. Every tap is therefore a shifted read of one of two streams,
    /// and the accumulation needs no shuffle at all.
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

            // The tap streams are named by their kernel position, so the two bases below are read in
            // the order the operator expects rather than in the order the streams were built.
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
    /// The kernel runs along a column, but no column is ever gathered. Output row <c>r</c> samples
    /// between source rows <c>2r</c> and <c>2r + 1</c>, so its eight tap streams are the eight whole
    /// rows <c>2r - 3</c> through <c>2r + 4</c>, each clamped to the plane. Clamping a row index
    /// repeats the first or last row, which is the clamping the reference performs per sample.
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
    /// <param name="row">The requested row, which may lie outside the plane.</param>
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
    /// An odd source width leaves the final odd-position sample past the end of the row. It is
    /// written as the final source sample, which is what the clamped reference kernel reads there.
    /// </remarks>
    private static void Separate(ReadOnlySpan<byte> source, int halvedWidth, Span<byte> even, Span<byte> odd)
    {
        int last = source.Length - 1;

        // Only the final output of an odd row reads past the end, so the bulk separation covers
        // every complete pair and the single trailing sample is written afterwards.
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
    /// A pair of adjacent bytes is one sixteen-bit lane on a little-endian runtime, and narrowing
    /// such a lane keeps its low byte. The even samples are therefore the narrowing of two loaded
    /// vectors, and the odd samples are the narrowing of the same vectors shifted down by eight
    /// bits. Each stage consumes two source vectors and writes one, so the byte counts differ
    /// between the loads and the store. A runtime that is not little-endian takes the scalar loop,
    /// where the pair order is explicit.
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

                    // Vector256.Narrow interleaves its two 128-bit halves on x86, so the result is
                    // permuted back into source order before it is stored.
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
