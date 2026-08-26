// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <content>
/// Provides packed projection kernels for AV1 directional intra prediction. Contiguous zone-one references map one
/// output sample to each lane. Upsampled references use native byte or 16-bit table shuffles to select alternating
/// half-sample positions, while zone-two left projections construct four independent coordinates per vector because
/// their bases advance by the directional derivative rather than by a fixed memory stride. Every interpolation uses
/// Q5 weights and the scalar continuation preserves the same rounding and endpoint-extension rules.
/// </content>
internal static partial class Av1DirectionalIntraPredictor
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
    /// <param name="derivative">The Q8 projection derivative.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    private static void PredictZone1(Span<byte> destination, int destinationStride, ReadOnlySpan<byte> above, bool upsample, int derivative, int width, int height)
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
    /// Predicts one high-bit-depth zone 1 block using contiguous SIMD projection rows.
    /// </summary>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <param name="above">The projected top reference.</param>
    /// <param name="upsample">Whether the reference contains half-sample positions.</param>
    /// <param name="derivative">The Q8 projection derivative.</param>
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
    /// <param name="dx">The horizontal Q8 derivative.</param>
    /// <param name="dy">The vertical Q8 derivative.</param>
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
    /// <param name="dx">The horizontal Q8 derivative.</param>
    /// <param name="dy">The vertical Q8 derivative.</param>
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
        int validCount = maximumBasis == int.MaxValue || basis >= maximumBasis
            ? maximumBasis == int.MaxValue ? destination.Length : 0
            : Math.Min(destination.Length, ((maximumBasis - 1 - basis) / basisIncrement) + 1);
        int index = 0;

        if (!upsample)
        {
            // A single index is advanced through all supported widths. A narrower path consumes only the remainder
            // left by the wider path, so the row is written once without requiring padded destination storage.
            if (Vector512.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = validCount - Vector512<byte>.Count;
                for (; index <= oneVectorFromEnd; index += Vector512<byte>.Count)
                {
                    Vector512<byte> left = Vector512.LoadUnsafe(ref referenceBase, (nuint)(basis + index));
                    Vector512<byte> right = Vector512.LoadUnsafe(ref referenceBase, (nuint)(basis + index + 1));
                    Interpolate(left, right, weight).StoreUnsafe(ref destinationBase, (nuint)index);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = validCount - Vector256<byte>.Count;
                for (; index <= oneVectorFromEnd; index += Vector256<byte>.Count)
                {
                    Vector256<byte> left = Vector256.LoadUnsafe(ref referenceBase, (nuint)(basis + index));
                    Vector256<byte> right = Vector256.LoadUnsafe(ref referenceBase, (nuint)(basis + index + 1));
                    Interpolate(left, right, weight).StoreUnsafe(ref destinationBase, (nuint)index);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = validCount - Vector128<byte>.Count;
                for (; index <= oneVectorFromEnd; index += Vector128<byte>.Count)
                {
                    Vector128<byte> left = Vector128.LoadUnsafe(ref referenceBase, (nuint)(basis + index));
                    Vector128<byte> right = Vector128.LoadUnsafe(ref referenceBase, (nuint)(basis + index + 1));
                    Interpolate(left, right, weight).StoreUnsafe(ref destinationBase, (nuint)index);
                }
            }
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = validCount - 8;
            for (; index <= oneVectorFromEnd; index += 8)
            {
                Vector128<byte> source = Vector128.LoadUnsafe(ref referenceBase, (nuint)(basis + (index * 2)));

                // ShuffleNative maps to byte-table lookup on AdvSimd and PSHUFB on x86. Eight output samples are
                // gathered from sixteen half-sample positions without scalar lane construction.
                Vector128<byte> left = Vector128.ShuffleNative(source, EvenByteIndices);
                Vector128<byte> right = Vector128.ShuffleNative(source, OddByteIndices);
                Vector128<byte> prediction = Interpolate(left, right, weight);
                Unsafe.As<byte, ulong>(ref Unsafe.Add(ref destinationBase, index)) = prediction.AsUInt64().GetElement(0);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            // Four-lane construction covers both the final non-upsampled remainder and targets without a native
            // gather. Each lane carries an independently projected coordinate but shares the row's interpolation weight.
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

                StoreFourBytes(Interpolate(left, right, Vector128.Create(weight)), ref Unsafe.Add(ref destinationBase, index));
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
        int validCount = maximumBasis == int.MaxValue || basis >= maximumBasis
            ? maximumBasis == int.MaxValue ? destination.Length : 0
            : Math.Min(destination.Length, ((maximumBasis - 1 - basis) / basisIncrement) + 1);
        int index = 0;

        if (!upsample)
        {
            // High-bit-depth samples stay in signed 16-bit storage, but interpolation widens to Int32 before the Q5
            // weighted sum. The largest supported 12-bit sample therefore cannot overflow an intermediate lane.
            if (Vector512.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = validCount - Vector512<short>.Count;
                for (; index <= oneVectorFromEnd; index += Vector512<short>.Count)
                {
                    Vector512<short> left = Vector512.LoadUnsafe(ref referenceBase, (nuint)(basis + index));
                    Vector512<short> right = Vector512.LoadUnsafe(ref referenceBase, (nuint)(basis + index + 1));
                    Interpolate(left, right, weight).StoreUnsafe(ref destinationBase, (nuint)index);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = validCount - Vector256<short>.Count;
                for (; index <= oneVectorFromEnd; index += Vector256<short>.Count)
                {
                    Vector256<short> left = Vector256.LoadUnsafe(ref referenceBase, (nuint)(basis + index));
                    Vector256<short> right = Vector256.LoadUnsafe(ref referenceBase, (nuint)(basis + index + 1));
                    Interpolate(left, right, weight).StoreUnsafe(ref destinationBase, (nuint)index);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = validCount - Vector128<short>.Count;
                for (; index <= oneVectorFromEnd; index += Vector128<short>.Count)
                {
                    Vector128<short> left = Vector128.LoadUnsafe(ref referenceBase, (nuint)(basis + index));
                    Vector128<short> right = Vector128.LoadUnsafe(ref referenceBase, (nuint)(basis + index + 1));
                    Interpolate(left, right, weight).StoreUnsafe(ref destinationBase, (nuint)index);
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
                Vector128<short> prediction = Interpolate(left, right, weight);
                Unsafe.As<short, ulong>(ref Unsafe.Add(ref destinationBase, index)) = prediction.AsUInt64().GetElement(0);
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

                StoreFourShorts(Interpolate(left, right, Vector128.Create(weight)), ref Unsafe.Add(ref destinationBase, index));
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
    /// <param name="derivative">The Q8 derivative subtracted between columns.</param>
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
            // Zone-two left references are not contiguous across output columns. Constructing the four source pairs
            // directly avoids a temporary gather-index buffer and keeps the scalar continuation at the same offset.
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

                StoreFourBytes(Interpolate(source0, source1, weights), ref Unsafe.Add(ref destinationBase, index));
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
    /// <param name="derivative">The Q8 derivative subtracted between columns.</param>
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

                StoreFourShorts(Interpolate(source0, source1, weights), ref Unsafe.Add(ref destinationBase, index));
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
    /// Interpolates sixty-four pairs of 8-bit references.
    /// </summary>
    /// <param name="left">The left interpolation samples.</param>
    /// <param name="right">The right interpolation samples.</param>
    /// <param name="weight">The right-sample weight.</param>
    /// <returns>The rounded interpolated samples.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<byte> Interpolate(Vector512<byte> left, Vector512<byte> right, int weight)
    {
        (Vector512<ushort> leftLow, Vector512<ushort> leftHigh) = Vector512.Widen(left);
        (Vector512<ushort> rightLow, Vector512<ushort> rightHigh) = Vector512.Widen(right);
        Vector512<ushort> rounding = Vector512.Create((ushort)16);
        Vector512<ushort> low = ((leftLow * (ushort)(32 - weight)) + (rightLow * (ushort)weight) + rounding) >> 5;
        Vector512<ushort> high = ((leftHigh * (ushort)(32 - weight)) + (rightHigh * (ushort)weight) + rounding) >> 5;
        return Vector512.Narrow(low, high);
    }

    /// <summary>
    /// Interpolates thirty-two pairs of 8-bit references.
    /// </summary>
    /// <param name="left">The left interpolation samples.</param>
    /// <param name="right">The right interpolation samples.</param>
    /// <param name="weight">The right-sample weight.</param>
    /// <returns>The rounded interpolated samples.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<byte> Interpolate(Vector256<byte> left, Vector256<byte> right, int weight)
    {
        (Vector256<ushort> leftLow, Vector256<ushort> leftHigh) = Vector256.Widen(left);
        (Vector256<ushort> rightLow, Vector256<ushort> rightHigh) = Vector256.Widen(right);
        Vector256<ushort> rounding = Vector256.Create((ushort)16);
        Vector256<ushort> low = ((leftLow * (ushort)(32 - weight)) + (rightLow * (ushort)weight) + rounding) >> 5;
        Vector256<ushort> high = ((leftHigh * (ushort)(32 - weight)) + (rightHigh * (ushort)weight) + rounding) >> 5;
        return Vector256.Narrow(low, high);
    }

    /// <summary>
    /// Interpolates sixteen pairs of 8-bit references.
    /// </summary>
    /// <param name="left">The left interpolation samples.</param>
    /// <param name="right">The right interpolation samples.</param>
    /// <param name="weight">The right-sample weight.</param>
    /// <returns>The rounded interpolated samples.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> Interpolate(Vector128<byte> left, Vector128<byte> right, int weight)
    {
        (Vector128<ushort> leftLow, Vector128<ushort> leftHigh) = Vector128.Widen(left);
        (Vector128<ushort> rightLow, Vector128<ushort> rightHigh) = Vector128.Widen(right);
        Vector128<ushort> rounding = Vector128.Create((ushort)16);
        Vector128<ushort> low = ((leftLow * (ushort)(32 - weight)) + (rightLow * (ushort)weight) + rounding) >> 5;
        Vector128<ushort> high = ((leftHigh * (ushort)(32 - weight)) + (rightHigh * (ushort)weight) + rounding) >> 5;
        return Vector128.Narrow(low, high);
    }

    /// <summary>
    /// Interpolates thirty-two pairs of high-bit-depth references.
    /// </summary>
    /// <param name="left">The left interpolation samples.</param>
    /// <param name="right">The right interpolation samples.</param>
    /// <param name="weight">The right-sample weight.</param>
    /// <returns>The rounded interpolated samples.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<short> Interpolate(Vector512<short> left, Vector512<short> right, int weight)
    {
        (Vector512<int> leftLow, Vector512<int> leftHigh) = Vector512.Widen(left);
        (Vector512<int> rightLow, Vector512<int> rightHigh) = Vector512.Widen(right);
        Vector512<int> rounding = Vector512.Create(16);
        Vector512<int> low = ((leftLow * (32 - weight)) + (rightLow * weight) + rounding) >> 5;
        Vector512<int> high = ((leftHigh * (32 - weight)) + (rightHigh * weight) + rounding) >> 5;
        return Vector512.Narrow(low, high);
    }

    /// <summary>
    /// Interpolates sixteen pairs of high-bit-depth references.
    /// </summary>
    /// <param name="left">The left interpolation samples.</param>
    /// <param name="right">The right interpolation samples.</param>
    /// <param name="weight">The right-sample weight.</param>
    /// <returns>The rounded interpolated samples.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<short> Interpolate(Vector256<short> left, Vector256<short> right, int weight)
    {
        (Vector256<int> leftLow, Vector256<int> leftHigh) = Vector256.Widen(left);
        (Vector256<int> rightLow, Vector256<int> rightHigh) = Vector256.Widen(right);
        Vector256<int> rounding = Vector256.Create(16);
        Vector256<int> low = ((leftLow * (32 - weight)) + (rightLow * weight) + rounding) >> 5;
        Vector256<int> high = ((leftHigh * (32 - weight)) + (rightHigh * weight) + rounding) >> 5;
        return Vector256.Narrow(low, high);
    }

    /// <summary>
    /// Interpolates eight pairs of high-bit-depth references.
    /// </summary>
    /// <param name="left">The left interpolation samples.</param>
    /// <param name="right">The right interpolation samples.</param>
    /// <param name="weight">The right-sample weight.</param>
    /// <returns>The rounded interpolated samples.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<short> Interpolate(Vector128<short> left, Vector128<short> right, int weight)
    {
        (Vector128<int> leftLow, Vector128<int> leftHigh) = Vector128.Widen(left);
        (Vector128<int> rightLow, Vector128<int> rightHigh) = Vector128.Widen(right);
        Vector128<int> rounding = Vector128.Create(16);
        Vector128<int> low = ((leftLow * (32 - weight)) + (rightLow * weight) + rounding) >> 5;
        Vector128<int> high = ((leftHigh * (32 - weight)) + (rightHigh * weight) + rounding) >> 5;
        return Vector128.Narrow(low, high);
    }

    /// <summary>
    /// Interpolates four widened reference pairs with independent weights.
    /// </summary>
    /// <param name="left">The left interpolation samples.</param>
    /// <param name="right">The right interpolation samples.</param>
    /// <param name="weights">The right-sample weights.</param>
    /// <returns>The rounded interpolated samples.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> Interpolate(Vector128<int> left, Vector128<int> right, Vector128<int> weights)
        => ((left * (Vector128.Create(32) - weights)) + (right * weights) + Vector128.Create(16)) >> 5;

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
        Unsafe.As<short, ulong>(ref destination) = narrowed.AsUInt64().GetElement(0);
    }
}
