// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <content>
/// Provides packed projection kernels for AV1 directional intra prediction. Contiguous zone-one references map one output sample to each lane.
/// Upsampled references use native byte or 16-bit table shuffles to select alternating half-sample positions.
/// Zone-two left projections build four independent coordinates per vector. Their bases advance by the directional derivative, not by a fixed memory stride.
/// Every interpolation uses Q5 weights. The scalar continuation keeps the same rounding and endpoint-extension rules.
/// </content>
internal static partial class Av1DirectionalIntraPredictor
{
    /// <summary>
    /// Implements the directional traversal for one closed interpolation operator.
    /// </summary>
    private static partial class Predictor<TOperator>
        where TOperator : struct, IDirectionalPredictionOperator
    {
        /// <summary>
        /// Gets the indices of even bytes in one upsampled reference vector.
        /// </summary>
        private static Vector128<byte> EvenByteIndices => Vector128.Create((byte)0, 2, 4, 6, 8, 10, 12, 14, 0, 2, 4, 6, 8, 10, 12, 14);

        /// <summary>
        /// Gets the indices of odd bytes in one upsampled reference vector.
        /// </summary>
        private static Vector128<byte> OddByteIndices => Vector128.Create((byte)1, 3, 5, 7, 9, 11, 13, 15, 1, 3, 5, 7, 9, 11, 13, 15);

        /// <summary>
        /// Gets the indices of even high-bit-depth samples in one upsampled reference vector.
        /// </summary>
        private static Vector128<short> EvenShortIndices => Vector128.Create((short)0, 2, 4, 6, 0, 2, 4, 6);

        /// <summary>
        /// Gets the indices of odd high-bit-depth samples in one upsampled reference vector.
        /// </summary>
        private static Vector128<short> OddShortIndices => Vector128.Create((short)1, 3, 5, 7, 1, 3, 5, 7);

        /// <summary>
        /// Predicts one 8-bit zone 1 block using contiguous SIMD projection rows.
        /// </summary>
        /// <param name="destination">The destination block origin.</param>
        /// <param name="destinationStride">The destination row stride.</param>
        /// <param name="above">The projected top reference.</param>
        /// <param name="upsample">Whether the reference contains half-sample positions.</param>
        /// <param name="derivative">The Q6 projection derivative.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        private static void PredictZone1(Span<byte> destination, int destinationStride, ReadOnlySpan<byte> above, bool upsample, int derivative, int width, int height)
        {
            int upsampleShift = upsample ? 1 : 0;
            int maximumBasis = (width + height - 1) << upsampleShift;
            int fractionBits = 6 - upsampleShift;
            int projection = derivative;

            // The register-resident kernel reads up to sixteen samples past every base position.
            // The length test makes sure that the padded edge buffer holds them.
            if (Vector256.IsHardwareAccelerated && width <= 16 && above.Length >= maximumBasis + 17)
            {
                PredictZone1Wide(destination, destinationStride, above, upsampleShift, derivative, width, height, maximumBasis);
                return;
            }

            for (int row = 0; row < height; row++, projection += derivative)
            {
                int basis = projection >> fractionBits;
                int weight = ((projection << upsampleShift) & 0x3F) >> 1;
                InterpolateRow(destination.Slice(row * destinationStride, width), above, basis, weight, upsample, maximumBasis);
            }
        }

        /// <summary>
        /// Predicts one 8-bit zone 1 block of at most sixteen columns with one vector per row.
        /// </summary>
        /// <remarks>
        /// Each row loads the left taps <c>a[x]</c> and the right taps <c>a[x + 1]</c> for sixteen lanes.
        /// It forms <c>(a[x] * 32 + 16 + (a[x + 1] - a[x]) * shift) &gt;&gt; 5</c> in 16-bit lanes.
        /// Then it blends the lanes past the final reference sample with that sample.
        /// When the base of a row reaches the final sample, every remaining row is that sample.
        /// </remarks>
        /// <param name="destination">The destination block origin.</param>
        /// <param name="destinationStride">The destination row stride.</param>
        /// <param name="above">The projected top reference with at least sixteen readable samples past the final one.</param>
        /// <param name="upsampleShift">One when the reference contains half-sample positions.</param>
        /// <param name="derivative">The Q6 projection derivative.</param>
        /// <param name="width">The block width, at most sixteen.</param>
        /// <param name="height">The block height.</param>
        /// <param name="maximumBasis">The final extended reference coordinate.</param>
        private static void PredictZone1Wide(Span<byte> destination, int destinationStride, ReadOnlySpan<byte> above, int upsampleShift, int derivative, int width, int height, int maximumBasis)
        {
            ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
            ref byte referenceBase = ref MemoryMarshal.GetReference(above);
            int fractionBits = 6 - upsampleShift;
            Vector128<byte> finalSample = Vector128.Create(Unsafe.Add(ref referenceBase, maximumBasis));
            Vector128<sbyte> laneIndices = Vector128.Create((sbyte)0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15);
            Vector256<short> sixteen = Vector256.Create((short)16);
            int projection = derivative;
            for (int row = 0; row < height; row++, projection += derivative)
            {
                int basis = projection >> fractionBits;
                int validCount = (maximumBasis - basis) >> upsampleShift;
                if (validCount <= 0)
                {
                    for (; row < height; row++)
                    {
                        StoreRow(finalSample, ref Unsafe.Add(ref destinationBase, row * destinationStride), width);
                    }

                    return;
                }

                Vector128<byte> a0;
                Vector128<byte> a1;
                Vector256<short> shift;
                if (upsampleShift != 0)
                {
                    // The even half-samples are the left taps and the odd ones the right taps.
                    a0 = Vector128.Shuffle(Vector128.LoadUnsafe(ref referenceBase, (nuint)basis), Vector128.Create((byte)0, 2, 4, 6, 8, 10, 12, 14, 1, 3, 5, 7, 9, 11, 13, 15));
                    a1 = Vector128_.ShiftRightBytesInVector(a0, 8);
                    shift = Vector256.Create((short)(((projection << upsampleShift) & 0x3F) >> 1));
                }
                else
                {
                    a0 = Vector128.LoadUnsafe(ref referenceBase, (nuint)basis);
                    a1 = Vector128.LoadUnsafe(ref referenceBase, (nuint)(basis + 1));
                    shift = Vector256.Create((short)((projection & 0x3F) >> 1));
                }

                // The weighted sum is a Q5 blend of two bytes plus the rounding term, so it stays in 0..8176.
                // Thus 16-bit lanes hold it exactly, and the logical shift by five gives the rounded sample.
                Vector256<short> left = Vector256_.Widen(a0);
                Vector256<short> right = Vector256_.Widen(a1);
                Vector256<short> result = Vector256.ShiftRightLogical(((left << 5) + sixteen) + Vector256_.MultiplyLow(right - left, shift), 5);
                Vector128<byte> samples = Vector128_.PackUnsignedSaturate(result.GetLower(), result.GetUpper());

                // Lanes at or past the valid count read past the final reference sample. The mask replaces them with that sample.
                Vector128<byte> mask = Vector128.GreaterThan(Vector128.Create((sbyte)Math.Min(validCount, width)), laneIndices).AsByte();
                StoreRow(Vector128.ConditionalSelect(mask, samples, finalSample), ref Unsafe.Add(ref destinationBase, row * destinationStride), width);
            }
        }

        /// <summary>
        /// Stores the first four, eight or sixteen bytes of one predicted row.
        /// </summary>
        /// <param name="row">The predicted row samples.</param>
        /// <param name="destination">The first destination sample of the row.</param>
        /// <param name="width">The row width. Any value other than eight or sixteen stores four bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void StoreRow(Vector128<byte> row, ref byte destination, int width)
        {
            if (width == 16)
            {
                row.StoreUnsafe(ref destination);
            }
            else if (width == 8)
            {
                Unsafe.WriteUnaligned(ref destination, row.AsUInt64().ToScalar());
            }
            else
            {
                Unsafe.WriteUnaligned(ref destination, row.AsUInt32().ToScalar());
            }
        }

        /// <summary>
        /// Predicts one high-bit-depth zone 1 block using contiguous SIMD projection rows.
        /// </summary>
        /// <param name="destination">The destination block origin.</param>
        /// <param name="destinationStride">The destination row stride.</param>
        /// <param name="above">The projected top reference.</param>
        /// <param name="upsample">Whether the reference contains half-sample positions.</param>
        /// <param name="derivative">The Q6 projection derivative.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        private static void PredictZone1(Span<short> destination, int destinationStride, ReadOnlySpan<short> above, bool upsample, int derivative, int width, int height)
        {
            int upsampleShift = upsample ? 1 : 0;
            int maximumBasis = (width + height - 1) << upsampleShift;
            int fractionBits = 6 - upsampleShift;
            int projection = derivative;

            for (int row = 0; row < height; row++, projection += derivative)
            {
                int basis = projection >> fractionBits;
                int weight = ((projection << upsampleShift) & 0x3F) >> 1;
                InterpolateRow(destination.Slice(row * destinationStride, width), above, basis, weight, upsample, maximumBasis);
            }
        }

        /// <summary>
        /// Predicts one 8-bit zone 2 block using vectorized left gathers and contiguous top projection rows.
        /// </summary>
        /// <param name="destination">The destination block origin.</param>
        /// <param name="destinationStride">The destination row stride.</param>
        /// <param name="above">The projected top reference.</param>
        /// <param name="left">The projected left reference.</param>
        /// <param name="upsampleAbove">Whether the top reference contains half-sample positions.</param>
        /// <param name="upsampleLeft">Whether the left reference contains half-sample positions.</param>
        /// <param name="dx">The horizontal Q6 derivative.</param>
        /// <param name="dy">The vertical Q6 derivative.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        private static void PredictZone2(Span<byte> destination, int destinationStride, ReadOnlySpan<byte> above, ReadOnlySpan<byte> left, bool upsampleAbove, bool upsampleLeft, int dx, int dy, int width, int height)
        {
            int aboveShift = upsampleAbove ? 1 : 0;
            int minimumTopBasis = -(1 << aboveShift);
            int topFractionBits = 6 - aboveShift;
            int topBasisIncrement = 1 << aboveShift;
            int topProjection = -dx;

            for (int row = 0; row < height; row++, topProjection -= dx)
            {
                int topBasis = topProjection >> topFractionBits;

                // Columns whose top base falls before the top-left corner project onto the left edge. These columns form a prefix of the row.
                int leftCount = 0;
                while (leftCount < width && topBasis < minimumTopBasis)
                {
                    leftCount++;
                    topBasis += topBasisIncrement;
                }

                Span<byte> destinationRow = destination.Slice(row * destinationStride, width);
                int leftProjection = (row << 6) - dy;
                InterpolateLeft(destinationRow[..leftCount], left, leftProjection, dy, upsampleLeft);

                if (leftCount < width)
                {
                    int topWeight = ((topProjection << aboveShift) & 0x3F) >> 1;
                    InterpolateRow(destinationRow[leftCount..], above, topBasis, topWeight, upsampleAbove, int.MaxValue);
                }
            }
        }

        /// <summary>
        /// Predicts one high-bit-depth zone 2 block using vectorized left gathers and contiguous top projection rows.
        /// </summary>
        /// <param name="destination">The destination block origin.</param>
        /// <param name="destinationStride">The destination row stride.</param>
        /// <param name="above">The projected top reference.</param>
        /// <param name="left">The projected left reference.</param>
        /// <param name="upsampleAbove">Whether the top reference contains half-sample positions.</param>
        /// <param name="upsampleLeft">Whether the left reference contains half-sample positions.</param>
        /// <param name="dx">The horizontal Q6 derivative.</param>
        /// <param name="dy">The vertical Q6 derivative.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        private static void PredictZone2(Span<short> destination, int destinationStride, ReadOnlySpan<short> above, ReadOnlySpan<short> left, bool upsampleAbove, bool upsampleLeft, int dx, int dy, int width, int height)
        {
            int aboveShift = upsampleAbove ? 1 : 0;
            int minimumTopBasis = -(1 << aboveShift);
            int topFractionBits = 6 - aboveShift;
            int topBasisIncrement = 1 << aboveShift;
            int topProjection = -dx;

            for (int row = 0; row < height; row++, topProjection -= dx)
            {
                int topBasis = topProjection >> topFractionBits;

                // Columns whose top base falls before the top-left corner project onto the left edge. These columns form a prefix of the row.
                int leftCount = 0;
                while (leftCount < width && topBasis < minimumTopBasis)
                {
                    leftCount++;
                    topBasis += topBasisIncrement;
                }

                Span<short> destinationRow = destination.Slice(row * destinationStride, width);
                int leftProjection = (row << 6) - dy;
                InterpolateLeft(destinationRow[..leftCount], left, leftProjection, dy, upsampleLeft);

                if (leftCount < width)
                {
                    int topWeight = ((topProjection << aboveShift) & 0x3F) >> 1;
                    InterpolateRow(destinationRow[leftCount..], above, topBasis, topWeight, upsampleAbove, int.MaxValue);
                }
            }
        }

        /// <summary>
        /// Interpolates one 8-bit projection row.
        /// </summary>
        /// <param name="destination">The destination row.</param>
        /// <param name="reference">The projected reference samples.</param>
        /// <param name="basis">The first integral reference coordinate.</param>
        /// <param name="weight">The right-sample interpolation weight.</param>
        /// <param name="upsample">Whether consecutive output samples advance two reference positions.</param>
        /// <param name="maximumBasis">The final extended reference coordinate, or <see cref="int.MaxValue"/> when the row cannot reach it.</param>
        private static void InterpolateRow(Span<byte> destination, ReadOnlySpan<byte> reference, int basis, int weight, bool upsample, int maximumBasis)
        {
            ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
            ref byte referenceBase = ref MemoryMarshal.GetReference(reference);
            int basisIncrement = upsample ? 2 : 1;

            // Columns whose base is before the final extended coordinate interpolate. The remaining columns take the final sample.
            int validCount = maximumBasis == int.MaxValue || basis >= maximumBasis
                ? maximumBasis == int.MaxValue ? destination.Length : 0
                : Math.Min(destination.Length, ((maximumBasis - 1 - basis) / basisIncrement) + 1);

            int index = 0;

            if (!upsample)
            {
                // One index advances through all supported widths. Each narrower path takes only the remainder of the wider path.
                // Thus each sample is written once, and the destination needs no padding.
                if (Vector512.IsHardwareAccelerated)
                {
                    nuint vectorCount = Numerics.Vector512Count<byte>(validCount - index);
                    for (; vectorCount > 0; vectorCount--, index += Vector512<byte>.Count)
                    {
                        Vector512<byte> left = Vector512.LoadUnsafe(ref referenceBase, (nuint)(basis + index));
                        Vector512<byte> right = Vector512.LoadUnsafe(ref referenceBase, (nuint)(basis + index + 1));
                        TOperator.Interpolate(left, right, weight).StoreUnsafe(ref destinationBase, (nuint)index);
                    }
                }

                if (Vector256.IsHardwareAccelerated)
                {
                    nuint vectorCount = Numerics.Vector256Count<byte>(validCount - index);
                    for (; vectorCount > 0; vectorCount--, index += Vector256<byte>.Count)
                    {
                        Vector256<byte> left = Vector256.LoadUnsafe(ref referenceBase, (nuint)(basis + index));
                        Vector256<byte> right = Vector256.LoadUnsafe(ref referenceBase, (nuint)(basis + index + 1));
                        TOperator.Interpolate(left, right, weight).StoreUnsafe(ref destinationBase, (nuint)index);
                    }
                }

                if (Vector128.IsHardwareAccelerated)
                {
                    nuint vectorCount = Numerics.Vector128Count<byte>(validCount - index);
                    for (; vectorCount > 0; vectorCount--, index += Vector128<byte>.Count)
                    {
                        Vector128<byte> left = Vector128.LoadUnsafe(ref referenceBase, (nuint)(basis + index));
                        Vector128<byte> right = Vector128.LoadUnsafe(ref referenceBase, (nuint)(basis + index + 1));
                        TOperator.Interpolate(left, right, weight).StoreUnsafe(ref destinationBase, (nuint)index);
                    }
                }
            }
            else if (Vector128.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = validCount - 8;
                for (; index <= oneVectorFromEnd; index += 8)
                {
                    Vector128<byte> source = Vector128.LoadUnsafe(ref referenceBase, (nuint)(basis + (index * 2)));

                    // ShuffleNative maps to a byte-table lookup on AdvSimd and to PSHUFB on x86. The even half-sample positions are the left taps.
                    // The odd half-sample positions are the right taps. Thus sixteen loaded samples give eight output samples in the low lanes.
                    Vector128<byte> left = Vector128.ShuffleNative(source, EvenByteIndices);
                    Vector128<byte> right = Vector128.ShuffleNative(source, OddByteIndices);
                    Vector128<byte> prediction = TOperator.Interpolate(left, right, weight);
                    Unsafe.As<byte, ulong>(ref Unsafe.Add(ref destinationBase, index)) = prediction.AsUInt64().ToScalar();
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                // The four-lane path handles the remainder of the wider paths, for both plain and upsampled references.
                // Each lane reads its own pair of reference samples. All lanes share the interpolation weight of the row.
                int oneVectorFromEnd = validCount - 4;
                for (; index <= oneVectorFromEnd; index += 4)
                {
                    int source = basis + (index * basisIncrement);
                    Vector128<int> left = Vector128.Create(
                        (int)Unsafe.Add(ref referenceBase, source),
                        Unsafe.Add(ref referenceBase, source + basisIncrement),
                        Unsafe.Add(ref referenceBase, source + (2 * basisIncrement)),
                        Unsafe.Add(ref referenceBase, source + (3 * basisIncrement)));

                    Vector128<int> right = Vector128.Create(
                        (int)Unsafe.Add(ref referenceBase, source + 1),
                        Unsafe.Add(ref referenceBase, source + basisIncrement + 1),
                        Unsafe.Add(ref referenceBase, source + (2 * basisIncrement) + 1),
                        Unsafe.Add(ref referenceBase, source + (3 * basisIncrement) + 1));

                    StoreFourBytes(TOperator.Interpolate(left, right, Vector128.Create(weight)), ref Unsafe.Add(ref destinationBase, index));
                }
            }

            for (; index < validCount; index++)
            {
                int source = basis + (index * basisIncrement);
                Unsafe.Add(ref destinationBase, index) = (byte)(((Unsafe.Add(ref referenceBase, source) * (32 - weight)) + (Unsafe.Add(ref referenceBase, source + 1) * weight) + 16) >> 5);
            }

            if (validCount < destination.Length)
            {
                destination[validCount..].Fill(Unsafe.Add(ref referenceBase, maximumBasis));
            }
        }

        /// <summary>
        /// Interpolates one high-bit-depth projection row.
        /// </summary>
        /// <param name="destination">The destination row.</param>
        /// <param name="reference">The projected reference samples.</param>
        /// <param name="basis">The first integral reference coordinate.</param>
        /// <param name="weight">The right-sample interpolation weight.</param>
        /// <param name="upsample">Whether consecutive output samples advance two reference positions.</param>
        /// <param name="maximumBasis">The final extended reference coordinate, or <see cref="int.MaxValue"/> when the row cannot reach it.</param>
        private static void InterpolateRow(Span<short> destination, ReadOnlySpan<short> reference, int basis, int weight, bool upsample, int maximumBasis)
        {
            ref short destinationBase = ref MemoryMarshal.GetReference(destination);
            ref short referenceBase = ref MemoryMarshal.GetReference(reference);
            int basisIncrement = upsample ? 2 : 1;

            // Columns whose base is before the final extended coordinate interpolate. The remaining columns take the final sample.
            int validCount = maximumBasis == int.MaxValue || basis >= maximumBasis
                ? maximumBasis == int.MaxValue ? destination.Length : 0
                : Math.Min(destination.Length, ((maximumBasis - 1 - basis) / basisIncrement) + 1);

            int index = 0;

            if (!upsample)
            {
                // High-bit-depth samples use signed 16-bit storage. The interpolation widens them to 32-bit lanes before the Q5 weighted sum.
                // Thus a 12-bit sample cannot overflow an intermediate lane. The index advances through the widths as in the 8-bit overload.
                if (Vector512.IsHardwareAccelerated)
                {
                    nuint vectorCount = Numerics.Vector512Count<short>(validCount - index);
                    for (; vectorCount > 0; vectorCount--, index += Vector512<short>.Count)
                    {
                        Vector512<short> left = Vector512.LoadUnsafe(ref referenceBase, (nuint)(basis + index));
                        Vector512<short> right = Vector512.LoadUnsafe(ref referenceBase, (nuint)(basis + index + 1));
                        TOperator.Interpolate(left, right, weight).StoreUnsafe(ref destinationBase, (nuint)index);
                    }
                }

                if (Vector256.IsHardwareAccelerated)
                {
                    nuint vectorCount = Numerics.Vector256Count<short>(validCount - index);
                    for (; vectorCount > 0; vectorCount--, index += Vector256<short>.Count)
                    {
                        Vector256<short> left = Vector256.LoadUnsafe(ref referenceBase, (nuint)(basis + index));
                        Vector256<short> right = Vector256.LoadUnsafe(ref referenceBase, (nuint)(basis + index + 1));
                        TOperator.Interpolate(left, right, weight).StoreUnsafe(ref destinationBase, (nuint)index);
                    }
                }

                if (Vector128.IsHardwareAccelerated)
                {
                    nuint vectorCount = Numerics.Vector128Count<short>(validCount - index);
                    for (; vectorCount > 0; vectorCount--, index += Vector128<short>.Count)
                    {
                        Vector128<short> left = Vector128.LoadUnsafe(ref referenceBase, (nuint)(basis + index));
                        Vector128<short> right = Vector128.LoadUnsafe(ref referenceBase, (nuint)(basis + index + 1));
                        TOperator.Interpolate(left, right, weight).StoreUnsafe(ref destinationBase, (nuint)index);
                    }
                }
            }
            else if (Vector128.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = validCount - 4;
                for (; index <= oneVectorFromEnd; index += 4)
                {
                    Vector128<short> source = Vector128.LoadUnsafe(ref referenceBase, (nuint)(basis + (index * 2)));
                    Vector128<short> left = Vector128.ShuffleNative(source, EvenShortIndices);
                    Vector128<short> right = Vector128.ShuffleNative(source, OddShortIndices);
                    Vector128<short> prediction = TOperator.Interpolate(left, right, weight);
                    Unsafe.As<short, ulong>(ref Unsafe.Add(ref destinationBase, index)) = prediction.AsUInt64().ToScalar();
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = validCount - 4;
                for (; index <= oneVectorFromEnd; index += 4)
                {
                    int source = basis + (index * basisIncrement);
                    Vector128<int> left = Vector128.Create(
                        (int)Unsafe.Add(ref referenceBase, source),
                        Unsafe.Add(ref referenceBase, source + basisIncrement),
                        Unsafe.Add(ref referenceBase, source + (2 * basisIncrement)),
                        Unsafe.Add(ref referenceBase, source + (3 * basisIncrement)));

                    Vector128<int> right = Vector128.Create(
                        (int)Unsafe.Add(ref referenceBase, source + 1),
                        Unsafe.Add(ref referenceBase, source + basisIncrement + 1),
                        Unsafe.Add(ref referenceBase, source + (2 * basisIncrement) + 1),
                        Unsafe.Add(ref referenceBase, source + (3 * basisIncrement) + 1));

                    StoreFourShorts(TOperator.Interpolate(left, right, Vector128.Create(weight)), ref Unsafe.Add(ref destinationBase, index));
                }
            }

            for (; index < validCount; index++)
            {
                int source = basis + (index * basisIncrement);
                Unsafe.Add(ref destinationBase, index) = (short)(((Unsafe.Add(ref referenceBase, source) * (32 - weight)) + (Unsafe.Add(ref referenceBase, source + 1) * weight) + 16) >> 5);
            }

            if (validCount < destination.Length)
            {
                destination[validCount..].Fill(Unsafe.Add(ref referenceBase, maximumBasis));
            }
        }

        /// <summary>
        /// Interpolates the left-edge prefix of one 8-bit zone 2 row.
        /// </summary>
        /// <param name="destination">The destination prefix.</param>
        /// <param name="left">The projected left reference.</param>
        /// <param name="projection">The first Q6 left projection.</param>
        /// <param name="derivative">The Q6 derivative subtracted between columns.</param>
        /// <param name="upsample">Whether the left reference contains half-sample positions.</param>
        private static void InterpolateLeft(Span<byte> destination, ReadOnlySpan<byte> left, int projection, int derivative, bool upsample)
        {
            ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
            ref byte leftBase = ref MemoryMarshal.GetReference(left);
            int upsampleShift = upsample ? 1 : 0;
            int fractionBits = 6 - upsampleShift;
            int index = 0;

            if (Vector128.IsHardwareAccelerated)
            {
                // Zone-two left references are not contiguous across output columns. Each lane computes its own projection, base and weight.
                // Direct lane construction needs no temporary buffer for gather indices. The scalar loop continues at the same index.
                int oneVectorFromEnd = destination.Length - 4;
                for (; index <= oneVectorFromEnd; index += 4)
                {
                    int projection0 = projection - (index * derivative);
                    int projection1 = projection0 - derivative;
                    int projection2 = projection1 - derivative;
                    int projection3 = projection2 - derivative;
                    int basis0 = projection0 >> fractionBits;
                    int basis1 = projection1 >> fractionBits;
                    int basis2 = projection2 >> fractionBits;
                    int basis3 = projection3 >> fractionBits;
                    Vector128<int> source0 = Vector128.Create((int)Unsafe.Add(ref leftBase, basis0), Unsafe.Add(ref leftBase, basis1), Unsafe.Add(ref leftBase, basis2), Unsafe.Add(ref leftBase, basis3));
                    Vector128<int> source1 = Vector128.Create((int)Unsafe.Add(ref leftBase, basis0 + 1), Unsafe.Add(ref leftBase, basis1 + 1), Unsafe.Add(ref leftBase, basis2 + 1), Unsafe.Add(ref leftBase, basis3 + 1));
                    Vector128<int> weights = Vector128.Create(
                        ((projection0 << upsampleShift) & 0x3F) >> 1,
                        ((projection1 << upsampleShift) & 0x3F) >> 1,
                        ((projection2 << upsampleShift) & 0x3F) >> 1,
                        ((projection3 << upsampleShift) & 0x3F) >> 1);

                    StoreFourBytes(TOperator.Interpolate(source0, source1, weights), ref Unsafe.Add(ref destinationBase, index));
                }
            }

            for (; index < destination.Length; index++)
            {
                int currentProjection = projection - (index * derivative);
                int basis = currentProjection >> fractionBits;
                int weight = ((currentProjection << upsampleShift) & 0x3F) >> 1;
                Unsafe.Add(ref destinationBase, index) = (byte)(((Unsafe.Add(ref leftBase, basis) * (32 - weight)) + (Unsafe.Add(ref leftBase, basis + 1) * weight) + 16) >> 5);
            }
        }

        /// <summary>
        /// Interpolates the left-edge prefix of one high-bit-depth zone 2 row.
        /// </summary>
        /// <param name="destination">The destination prefix.</param>
        /// <param name="left">The projected left reference.</param>
        /// <param name="projection">The first Q6 left projection.</param>
        /// <param name="derivative">The Q6 derivative subtracted between columns.</param>
        /// <param name="upsample">Whether the left reference contains half-sample positions.</param>
        private static void InterpolateLeft(Span<short> destination, ReadOnlySpan<short> left, int projection, int derivative, bool upsample)
        {
            ref short destinationBase = ref MemoryMarshal.GetReference(destination);
            ref short leftBase = ref MemoryMarshal.GetReference(left);
            int upsampleShift = upsample ? 1 : 0;
            int fractionBits = 6 - upsampleShift;
            int index = 0;

            if (Vector128.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = destination.Length - 4;
                for (; index <= oneVectorFromEnd; index += 4)
                {
                    int projection0 = projection - (index * derivative);
                    int projection1 = projection0 - derivative;
                    int projection2 = projection1 - derivative;
                    int projection3 = projection2 - derivative;
                    int basis0 = projection0 >> fractionBits;
                    int basis1 = projection1 >> fractionBits;
                    int basis2 = projection2 >> fractionBits;
                    int basis3 = projection3 >> fractionBits;
                    Vector128<int> source0 = Vector128.Create((int)Unsafe.Add(ref leftBase, basis0), Unsafe.Add(ref leftBase, basis1), Unsafe.Add(ref leftBase, basis2), Unsafe.Add(ref leftBase, basis3));
                    Vector128<int> source1 = Vector128.Create((int)Unsafe.Add(ref leftBase, basis0 + 1), Unsafe.Add(ref leftBase, basis1 + 1), Unsafe.Add(ref leftBase, basis2 + 1), Unsafe.Add(ref leftBase, basis3 + 1));
                    Vector128<int> weights = Vector128.Create(
                        ((projection0 << upsampleShift) & 0x3F) >> 1,
                        ((projection1 << upsampleShift) & 0x3F) >> 1,
                        ((projection2 << upsampleShift) & 0x3F) >> 1,
                        ((projection3 << upsampleShift) & 0x3F) >> 1);

                    StoreFourShorts(TOperator.Interpolate(source0, source1, weights), ref Unsafe.Add(ref destinationBase, index));
                }
            }

            for (; index < destination.Length; index++)
            {
                int currentProjection = projection - (index * derivative);
                int basis = currentProjection >> fractionBits;
                int weight = ((currentProjection << upsampleShift) & 0x3F) >> 1;
                Unsafe.Add(ref destinationBase, index) = (short)(((Unsafe.Add(ref leftBase, basis) * (32 - weight)) + (Unsafe.Add(ref leftBase, basis + 1) * weight) + 16) >> 5);
            }
        }

        /// <summary>
        /// Stores four widened predictions as packed 8-bit samples.
        /// </summary>
        /// <param name="prediction">The widened predictions.</param>
        /// <param name="destination">The first destination sample.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void StoreFourBytes(Vector128<int> prediction, ref byte destination)
        {
            Vector128<ushort> narrowed16 = Vector128.Narrow(prediction.AsUInt32(), Vector128<uint>.Zero);
            Vector128<byte> narrowed8 = Vector128.Narrow(narrowed16, Vector128<ushort>.Zero);
            Unsafe.As<byte, uint>(ref destination) = narrowed8.AsUInt32().GetElement(0);
        }

        /// <summary>
        /// Stores four widened predictions as packed high-bit-depth samples.
        /// </summary>
        /// <param name="prediction">The widened predictions.</param>
        /// <param name="destination">The first destination sample.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void StoreFourShorts(Vector128<int> prediction, ref short destination)
        {
            Vector128<short> narrowed = Vector128.Narrow(prediction, Vector128<int>.Zero);
            Unsafe.As<short, ulong>(ref destination) = narrowed.AsUInt64().ToScalar();
        }
    }

    /// <summary>
    /// Implements directional intra prediction through one closed interpolation operator.
    /// </summary>
    /// <typeparam name="TOperator">The directional interpolation arithmetic.</typeparam>
    private static partial class Predictor<TOperator>
        where TOperator : struct, IDirectionalPredictionOperator
    {
        /// <summary>
        /// Gets the Q6 directional derivatives indexed by acute prediction angle. The 45 degree entry is 64, one sample per row.
        /// </summary>
        private static ReadOnlySpan<int> DirectionalIntraDerivative =>
        [

            // Zero entries are angles that AV1 never signals. Direct indexing by angle needs no search or division in each directional block.
            // The values are the exact fixed-point derivatives of the normative AV1 table.
            0, 0, 0, 1023, 0, 0, 547, 0, 0, 372, 0, 0, 0, 0, 273, 0, 0, 215, 0, 0, 178, 0, 0,
            151, 0, 0, 132, 0, 0, 116, 0, 0, 102, 0, 0, 0, 90, 0, 0, 80, 0, 0, 71, 0, 0, 64, 0, 0,
            57, 0, 0, 51, 0, 0, 45, 0, 0, 0, 40, 0, 0, 35, 0, 0, 31, 0, 0, 27, 0, 0, 23, 0, 0,
            19, 0, 0, 15, 0, 0, 0, 0, 11, 0, 0, 7, 0, 0, 3, 0, 0,
        ];

        /// <summary>
        /// Predicts an 8-bit directional block using the widest available SIMD path.
        /// </summary>
        /// <param name="destination">The destination block origin.</param>
        /// <param name="destinationStride">The destination row stride in samples.</param>
        /// <param name="transformSize">The predicted block dimensions.</param>
        /// <param name="above">The prepared top reference, including any required extension.</param>
        /// <param name="left">The prepared left reference, including any required extension.</param>
        /// <param name="upsampleAbove">Whether the top edge contains half-sample positions.</param>
        /// <param name="upsampleLeft">Whether the left edge contains half-sample positions.</param>
        /// <param name="angle">The adjusted prediction angle.</param>
        /// <param name="transposedBlock">The caller-owned block transposition workspace.</param>
        public static void Predict(
            Span<byte> destination,
            int destinationStride,
            Av1TransformSize transformSize,
            ReadOnlySpan<byte> above,
            ReadOnlySpan<byte> left,
            bool upsampleAbove,
            bool upsampleLeft,
            int angle,
            Span<byte> transposedBlock)
        {
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();

            if (angle is > 0 and < 90)
            {
                PredictZone1(destination, destinationStride, above, upsampleAbove, GetDeltaX(angle), width, height);
            }
            else if (angle is > 90 and < 180)
            {
                PredictZone2(destination, destinationStride, above, left, upsampleAbove, upsampleLeft, GetDeltaX(angle), GetDeltaY(angle), width, height);
            }
            else if (angle is > 180 and < 270)
            {
                // Zone 3 uses a zone 1 prediction with swapped dimensions, followed by a transpose.
                // Thus both stages read edges and store rows contiguously, and no stage scatters columns.
                Span<byte> transposed = transposedBlock[..(width * height)];
                PredictZone1(transposed, height, left, upsampleLeft, GetDeltaY(angle), height, width);
                Transpose(transposed, destination, height, width, destinationStride);
            }
            else
            {
                Av1PredictionMode mode = angle == 90 ? Av1PredictionMode.Vertical : Av1PredictionMode.Horizontal;
                Av1NonDirectionalIntraPredictorBase.GetPredictor(mode).Predict(destination, destinationStride, above, left, width, height);
            }
        }

        /// <summary>
        /// Predicts a high-bit-depth directional block using the widest available SIMD path.
        /// </summary>
        /// <param name="destination">The destination block origin.</param>
        /// <param name="destinationStride">The destination row stride in samples.</param>
        /// <param name="transformSize">The predicted block dimensions.</param>
        /// <param name="above">The prepared top reference, including any required extension.</param>
        /// <param name="left">The prepared left reference, including any required extension.</param>
        /// <param name="upsampleAbove">Whether the top edge contains half-sample positions.</param>
        /// <param name="upsampleLeft">Whether the left edge contains half-sample positions.</param>
        /// <param name="angle">The adjusted prediction angle.</param>
        /// <param name="transposedBlock">The caller-owned block transposition workspace.</param>
        public static void Predict(
            Span<short> destination,
            int destinationStride,
            Av1TransformSize transformSize,
            ReadOnlySpan<short> above,
            ReadOnlySpan<short> left,
            bool upsampleAbove,
            bool upsampleLeft,
            int angle,
            Span<short> transposedBlock)
        {
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();

            if (angle is > 0 and < 90)
            {
                PredictZone1(destination, destinationStride, above, upsampleAbove, GetDeltaX(angle), width, height);
            }
            else if (angle is > 90 and < 180)
            {
                PredictZone2(destination, destinationStride, above, left, upsampleAbove, upsampleLeft, GetDeltaX(angle), GetDeltaY(angle), width, height);
            }
            else if (angle is > 180 and < 270)
            {
                Span<short> transposed = transposedBlock[..(width * height)];
                PredictZone1(transposed, height, left, upsampleLeft, GetDeltaY(angle), height, width);
                Transpose(transposed, destination, height, width, destinationStride);
            }
            else
            {
                Av1PredictionMode mode = angle == 90 ? Av1PredictionMode.Vertical : Av1PredictionMode.Horizontal;
                Av1NonDirectionalIntraPredictorBase.GetPredictor(mode).Predict(destination, destinationStride, above, left, width, height);
            }
        }

        /// <summary>
        /// Gets the horizontal Q6 projection derivative for an adjusted angle.
        /// </summary>
        /// <param name="angle">The adjusted prediction angle.</param>
        /// <returns>The horizontal derivative, or one when the selected zone does not consume it.</returns>
        public static int GetDeltaX(int angle)
            => angle switch
            {
                > 0 and < 90 => DirectionalIntraDerivative[angle],
                > 90 and < 180 => DirectionalIntraDerivative[180 - angle],
                _ => 1,
            };

        /// <summary>
        /// Gets the vertical Q6 projection derivative for an adjusted angle.
        /// </summary>
        /// <param name="angle">The adjusted prediction angle.</param>
        /// <returns>The vertical derivative, or one when the selected zone does not consume it.</returns>
        public static int GetDeltaY(int angle)
            => angle switch
            {
                > 90 and < 180 => DirectionalIntraDerivative[angle - 90],
                > 180 and < 270 => DirectionalIntraDerivative[270 - angle],
                _ => 1,
            };
    }
}
