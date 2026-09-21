// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <content>
/// Provides the vector kernels of the symmetric-even halving filter.
/// </content>
internal static partial class Av1PlaneDownsampler
{
    /// <summary>
    /// The samples of clamped padding held on each side of a separated stream.
    /// </summary>
    /// <remarks>
    /// Tap three reads O[k-2] and E[k+2], so two samples on each side cover the kernel's reach. The
    /// right side also absorbs a whole trailing vector, so that the last band is an ordinary load.
    /// </remarks>
    private const int SeparatedPadding = 2 + VectorPadding;

    /// <summary>
    /// The samples of slack held at the end of a row so that a whole vector can always be stored.
    /// </summary>
    /// <remarks>
    /// The widest supported vector holds sixty-four bytes, so that much slack lets the final band of
    /// any row be written without a masked store or a scalar tail.
    /// </remarks>
    private const int VectorPadding = 64;

    /// <summary>
    /// Splits one row into its even-position and odd-position samples.
    /// </summary>
    /// <param name="source">The source row.</param>
    /// <param name="halvedWidth">The number of samples in each separated stream.</param>
    /// <param name="even">Receives the even-position samples at <see cref="SeparatedPadding"/>.</param>
    /// <param name="odd">Receives the odd-position samples at <see cref="SeparatedPadding"/>.</param>
    /// <remarks>
    /// Two adjacent samples share one sixteen-bit element, which holds the even-position sample in its
    /// low byte. Narrowing the elements of two source vectors therefore yields one whole vector of the
    /// even stream, and narrowing them after a byte shift yields the odd stream. Narrowing is defined
    /// as a plain concatenation of the narrowed elements, so unlike the reference's lane-local byte
    /// shuffle it needs no lane correction afterwards.
    /// </remarks>
    private static void SeparateCore(ReadOnlySpan<byte> source, int halvedWidth, Span<byte> even, Span<byte> odd)
    {
        ref byte sourceBase = ref MemoryMarshal.GetReference(source);
        ref byte evenBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(even), SeparatedPadding);
        ref byte oddBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(odd), SeparatedPadding);

        int index = 0;
        if (Vector256.IsHardwareAccelerated && halvedWidth >= Vector256<byte>.Count)
        {
            // Two adjacent samples share one sixteen-bit element, and the element holds the
            // even-position sample in its low byte. Narrowing the elements therefore yields the even
            // stream, and narrowing them after a byte shift yields the odd stream. Narrowing is
            // defined as a plain concatenation of the narrowed elements, so unlike a byte shuffle it
            // needs no reasoning about lane locality.
            for (; index + Vector256<byte>.Count <= halvedWidth; index += Vector256<byte>.Count)
            {
                Vector256<ushort> low = Vector256.LoadUnsafe(ref sourceBase, (nuint)(index * 2)).AsUInt16();
                Vector256<ushort> high = Vector256.LoadUnsafe(
                    ref sourceBase,
                    (nuint)((index * 2) + Vector256<byte>.Count)).AsUInt16();

                Vector256.Narrow(low, high).StoreUnsafe(ref evenBase, (nuint)index);
                Vector256.Narrow(
                    Vector256.ShiftRightLogical(low, 8),
                    Vector256.ShiftRightLogical(high, 8)).StoreUnsafe(ref oddBase, (nuint)index);
            }
        }
        else if (Vector128.IsHardwareAccelerated && halvedWidth >= Vector128<byte>.Count)
        {
            for (; index + Vector128<byte>.Count <= halvedWidth; index += Vector128<byte>.Count)
            {
                Vector128<ushort> low = Vector128.LoadUnsafe(ref sourceBase, (nuint)(index * 2)).AsUInt16();
                Vector128<ushort> high = Vector128.LoadUnsafe(
                    ref sourceBase,
                    (nuint)((index * 2) + Vector128<byte>.Count)).AsUInt16();

                Vector128.Narrow(low, high).StoreUnsafe(ref evenBase, (nuint)index);
                Vector128.Narrow(
                    Vector128.ShiftRightLogical(low, 8),
                    Vector128.ShiftRightLogical(high, 8)).StoreUnsafe(ref oddBase, (nuint)index);
            }
        }

        // The remainder is shorter than one vector of stream samples.
        for (; index < halvedWidth; index++)
        {
            Unsafe.Add(ref evenBase, index) = source[index * 2];
            Unsafe.Add(ref oddBase, index) = source[(index * 2) + 1];
        }
    }

    /// <summary>
    /// Accumulates the four taps of one row from its separated streams.
    /// </summary>
    /// <param name="even">The even-position samples, offset by <see cref="SeparatedPadding"/>.</param>
    /// <param name="odd">The odd-position samples, offset by <see cref="SeparatedPadding"/>.</param>
    /// <param name="halvedWidth">The number of output samples.</param>
    /// <param name="destination">The halved row.</param>
    /// <remarks>
    /// Each tap is one pair of shifted loads of the two streams, so the whole accumulation is vertical
    /// and the padding written by the separation removes every boundary test. The sums are carried in
    /// thirty-two bit lanes, because a tap product of a byte by fifty-six plus the rounding term
    /// exceeds sixteen bits once the four taps are added.
    /// </remarks>
    private static void FilterSeparated(
        ReadOnlySpan<byte> even,
        ReadOnlySpan<byte> odd,
        int halvedWidth,
        Span<byte> destination)
    {
        ref byte evenBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(even), SeparatedPadding);
        ref byte oddBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(odd), SeparatedPadding);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        ReadOnlySpan<short> filter = HalfFilter;

        int index = 0;
        if (Vector256.IsHardwareAccelerated && halvedWidth >= Vector256<byte>.Count)
        {
            for (; index < halvedWidth; index += Vector256<byte>.Count)
            {
                nuint offset = (nuint)index;

                // tap 0: E[k] + O[k]
                Start256(
                    Vector256.LoadUnsafe(ref evenBase, offset),
                    Vector256.LoadUnsafe(ref oddBase, offset),
                    filter[0],
                    out Vector256<int> sum0,
                    out Vector256<int> sum1,
                    out Vector256<int> sum2,
                    out Vector256<int> sum3);

                // tap 1: O[k-1] + E[k+1]
                AddTap256(ref oddBase, ref evenBase, offset, -1, 1, filter[1], ref sum0, ref sum1, ref sum2, ref sum3);

                // tap 2: E[k-1] + O[k+1]
                AddTap256(ref evenBase, ref oddBase, offset, -1, 1, filter[2], ref sum0, ref sum1, ref sum2, ref sum3);

                // tap 3: O[k-2] + E[k+2]
                AddTap256(ref oddBase, ref evenBase, offset, -2, 2, filter[3], ref sum0, ref sum1, ref sum2, ref sum3);

                Av1NonDirectionalIntraPredictorBase.Narrow(
                    Round256(sum0),
                    Round256(sum1),
                    Round256(sum2),
                    Round256(sum3)).StoreUnsafe(ref destinationBase, offset);
            }

            return;
        }

        if (Vector128.IsHardwareAccelerated && halvedWidth >= Vector128<byte>.Count)
        {
            for (; index < halvedWidth; index += Vector128<byte>.Count)
            {
                nuint offset = (nuint)index;
                Start128(
                    Vector128.LoadUnsafe(ref evenBase, offset),
                    Vector128.LoadUnsafe(ref oddBase, offset),
                    filter[0],
                    out Vector128<int> sum0,
                    out Vector128<int> sum1,
                    out Vector128<int> sum2,
                    out Vector128<int> sum3);

                AddTap128(ref oddBase, ref evenBase, offset, -1, 1, filter[1], ref sum0, ref sum1, ref sum2, ref sum3);
                AddTap128(ref evenBase, ref oddBase, offset, -1, 1, filter[2], ref sum0, ref sum1, ref sum2, ref sum3);
                AddTap128(ref oddBase, ref evenBase, offset, -2, 2, filter[3], ref sum0, ref sum1, ref sum2, ref sum3);

                Av1NonDirectionalIntraPredictorBase.Narrow(
                    Round128(sum0),
                    Round128(sum1),
                    Round128(sum2),
                    Round128(sum3)).StoreUnsafe(ref destinationBase, offset);
            }

            return;
        }

        for (; index < halvedWidth; index++)
        {
            int sum = 1 << (FilterBits - 1);
            sum += (Unsafe.Add(ref evenBase, index) + Unsafe.Add(ref oddBase, index)) * filter[0];
            sum += (Unsafe.Add(ref oddBase, index - 1) + Unsafe.Add(ref evenBase, index + 1)) * filter[1];
            sum += (Unsafe.Add(ref evenBase, index - 1) + Unsafe.Add(ref oddBase, index + 1)) * filter[2];
            sum += (Unsafe.Add(ref oddBase, index - 2) + Unsafe.Add(ref evenBase, index + 2)) * filter[3];
            Unsafe.Add(ref destinationBase, index) = (byte)Av1Math.Clip3(0, 255, sum >> FilterBits);
        }
    }

    /// <summary>
    /// Accumulates the four taps of one band of columns.
    /// </summary>
    /// <param name="intermediate">The halved-width samples.</param>
    /// <param name="intermediateStride">The intermediate row stride.</param>
    /// <param name="height">The intermediate height.</param>
    /// <param name="position">The source row the sampling position follows.</param>
    /// <param name="halvedWidth">The number of columns.</param>
    /// <param name="destination">The halved row.</param>
    /// <remarks>
    /// The eight source rows are resolved once per band, with the rows outside the plane replaced by
    /// the first or last row. Every tap is then a whole-row load, so no column is ever gathered.
    /// </remarks>
    private static void FilterColumnBand(
        ReadOnlySpan<byte> intermediate,
        int intermediateStride,
        int height,
        int position,
        int halvedWidth,
        Span<byte> destination)
    {
        ref byte intermediateBase = ref MemoryMarshal.GetReference(intermediate);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        ReadOnlySpan<short> filter = HalfFilter;
        int last = height - 1;

        int index = 0;
        if (Vector256.IsHardwareAccelerated && halvedWidth >= Vector256<byte>.Count)
        {
            for (; index < halvedWidth; index += Vector256<byte>.Count)
            {
                nuint offset = (nuint)index;
                Start256(
                    LoadRow256(ref intermediateBase, intermediateStride, position, last, offset),
                    LoadRow256(ref intermediateBase, intermediateStride, position + 1, last, offset),
                    filter[0],
                    out Vector256<int> sum0,
                    out Vector256<int> sum1,
                    out Vector256<int> sum2,
                    out Vector256<int> sum3);

                for (int tap = 1; tap < HalfTapCount; tap++)
                {
                    Accumulate256(
                        LoadRow256(ref intermediateBase, intermediateStride, position - tap, last, offset),
                        LoadRow256(ref intermediateBase, intermediateStride, position + 1 + tap, last, offset),
                        filter[tap],
                        ref sum0,
                        ref sum1,
                        ref sum2,
                        ref sum3);
                }

                Av1NonDirectionalIntraPredictorBase.Narrow(
                    Round256(sum0),
                    Round256(sum1),
                    Round256(sum2),
                    Round256(sum3)).StoreUnsafe(ref destinationBase, offset);
            }

            return;
        }

        if (Vector128.IsHardwareAccelerated && halvedWidth >= Vector128<byte>.Count)
        {
            for (; index < halvedWidth; index += Vector128<byte>.Count)
            {
                nuint offset = (nuint)index;
                Start128(
                    LoadRow128(ref intermediateBase, intermediateStride, position, last, offset),
                    LoadRow128(ref intermediateBase, intermediateStride, position + 1, last, offset),
                    filter[0],
                    out Vector128<int> sum0,
                    out Vector128<int> sum1,
                    out Vector128<int> sum2,
                    out Vector128<int> sum3);

                for (int tap = 1; tap < HalfTapCount; tap++)
                {
                    Accumulate128(
                        LoadRow128(ref intermediateBase, intermediateStride, position - tap, last, offset),
                        LoadRow128(ref intermediateBase, intermediateStride, position + 1 + tap, last, offset),
                        filter[tap],
                        ref sum0,
                        ref sum1,
                        ref sum2,
                        ref sum3);
                }

                Av1NonDirectionalIntraPredictorBase.Narrow(
                    Round128(sum0),
                    Round128(sum1),
                    Round128(sum2),
                    Round128(sum3)).StoreUnsafe(ref destinationBase, offset);
            }

            return;
        }

        for (; index < halvedWidth; index++)
        {
            int sum = 1 << (FilterBits - 1);
            for (int tap = 0; tap < HalfTapCount; tap++)
            {
                int above = Math.Clamp(position - tap, 0, last);
                int below = Math.Clamp(position + 1 + tap, 0, last);
                sum += (Unsafe.Add(ref intermediateBase, (above * intermediateStride) + index) +
                    Unsafe.Add(ref intermediateBase, (below * intermediateStride) + index)) * filter[tap];
            }

            Unsafe.Add(ref destinationBase, index) = (byte)Av1Math.Clip3(0, 255, sum >> FilterBits);
        }
    }
}
