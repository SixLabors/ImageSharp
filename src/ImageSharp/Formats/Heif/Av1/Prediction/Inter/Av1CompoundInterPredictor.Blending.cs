// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Provides distance-weighted and per-sample masked compound blending.
/// </content>
internal static partial class Av1CompoundInterPredictor
{
    private const int DistanceWeightBits = 4;
    private const int MaskWeightBits = 6;
    private const int MaximumMaskAlpha = 1 << MaskWeightBits;

    /// <summary>
    /// Combines two 8-bit predictors with AV1 display-distance weights.
    /// </summary>
    public static void DistanceWeighted(
        Span<byte> destination,
        int destinationStride,
        ReadOnlySpan<byte> second,
        int secondStride,
        int width,
        int height,
        int firstWeight,
        int secondWeight)
    {
        for (int row = 0; row < height; row++)
        {
            Span<byte> destinationRow = destination.Slice(row * destinationStride, width);
            ReadOnlySpan<byte> secondRow = second.Slice(row * secondStride, width);
            ref byte destinationReference = ref MemoryMarshal.GetReference(destinationRow);
            ref byte secondReference = ref MemoryMarshal.GetReference(secondRow);
            int column = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector512<byte>.Count;
                for (; column <= vectorEnd; column += Vector512<byte>.Count)
                {
                    Vector512<byte> firstVector = Vector512.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector512<byte> secondVector = Vector512.LoadUnsafe(ref secondReference, (nuint)column);
                    DistanceWeighted(firstVector, secondVector, firstWeight, secondWeight).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector256<byte>.Count;
                for (; column <= vectorEnd; column += Vector256<byte>.Count)
                {
                    Vector256<byte> firstVector = Vector256.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector256<byte> secondVector = Vector256.LoadUnsafe(ref secondReference, (nuint)column);
                    DistanceWeighted(firstVector, secondVector, firstWeight, secondWeight).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector128<byte>.Count;
                for (; column <= vectorEnd; column += Vector128<byte>.Count)
                {
                    Vector128<byte> firstVector = Vector128.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector128<byte> secondVector = Vector128.LoadUnsafe(ref secondReference, (nuint)column);
                    DistanceWeighted(firstVector, secondVector, firstWeight, secondWeight).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                destinationRow[column] = (byte)(((destinationRow[column] * firstWeight) + (secondRow[column] * secondWeight) + 8) >> DistanceWeightBits);
            }
        }
    }

    /// <summary>
    /// Combines two high-bit-depth predictors with AV1 display-distance weights.
    /// </summary>
    public static void DistanceWeighted(
        Span<ushort> destination,
        int destinationStride,
        ReadOnlySpan<ushort> second,
        int secondStride,
        int width,
        int height,
        int firstWeight,
        int secondWeight)
    {
        for (int row = 0; row < height; row++)
        {
            Span<ushort> destinationRow = destination.Slice(row * destinationStride, width);
            ReadOnlySpan<ushort> secondRow = second.Slice(row * secondStride, width);
            ref ushort destinationReference = ref MemoryMarshal.GetReference(destinationRow);
            ref ushort secondReference = ref MemoryMarshal.GetReference(secondRow);
            int column = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector512<ushort>.Count;
                for (; column <= vectorEnd; column += Vector512<ushort>.Count)
                {
                    Vector512<ushort> firstVector = Vector512.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector512<ushort> secondVector = Vector512.LoadUnsafe(ref secondReference, (nuint)column);
                    DistanceWeighted(firstVector, secondVector, firstWeight, secondWeight).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector256<ushort>.Count;
                for (; column <= vectorEnd; column += Vector256<ushort>.Count)
                {
                    Vector256<ushort> firstVector = Vector256.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector256<ushort> secondVector = Vector256.LoadUnsafe(ref secondReference, (nuint)column);
                    DistanceWeighted(firstVector, secondVector, firstWeight, secondWeight).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector128<ushort>.Count;
                for (; column <= vectorEnd; column += Vector128<ushort>.Count)
                {
                    Vector128<ushort> firstVector = Vector128.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector128<ushort> secondVector = Vector128.LoadUnsafe(ref secondReference, (nuint)column);
                    DistanceWeighted(firstVector, secondVector, firstWeight, secondWeight).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                destinationRow[column] = (ushort)(((destinationRow[column] * firstWeight) + (secondRow[column] * secondWeight) + 8) >> DistanceWeightBits);
            }
        }
    }

    /// <summary>
    /// Blends two 8-bit predictors through a contiguous AV1 alpha mask.
    /// </summary>
    public static void Blend(
        Span<byte> destination,
        int destinationStride,
        ReadOnlySpan<byte> second,
        int secondStride,
        ReadOnlySpan<byte> mask,
        int maskStride,
        int width,
        int height)
    {
        for (int row = 0; row < height; row++)
        {
            Span<byte> destinationRow = destination.Slice(row * destinationStride, width);
            ReadOnlySpan<byte> secondRow = second.Slice(row * secondStride, width);
            ReadOnlySpan<byte> maskRow = mask.Slice(row * maskStride, width);
            ref byte destinationReference = ref MemoryMarshal.GetReference(destinationRow);
            ref byte secondReference = ref MemoryMarshal.GetReference(secondRow);
            ref byte maskReference = ref MemoryMarshal.GetReference(maskRow);
            int column = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector512<byte>.Count;
                for (; column <= vectorEnd; column += Vector512<byte>.Count)
                {
                    Vector512<byte> firstVector = Vector512.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector512<byte> secondVector = Vector512.LoadUnsafe(ref secondReference, (nuint)column);
                    Vector512<byte> maskVector = Vector512.LoadUnsafe(ref maskReference, (nuint)column);
                    Blend(firstVector, secondVector, maskVector).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector256<byte>.Count;
                for (; column <= vectorEnd; column += Vector256<byte>.Count)
                {
                    Vector256<byte> firstVector = Vector256.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector256<byte> secondVector = Vector256.LoadUnsafe(ref secondReference, (nuint)column);
                    Vector256<byte> maskVector = Vector256.LoadUnsafe(ref maskReference, (nuint)column);
                    Blend(firstVector, secondVector, maskVector).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector128<byte>.Count;
                for (; column <= vectorEnd; column += Vector128<byte>.Count)
                {
                    Vector128<byte> firstVector = Vector128.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector128<byte> secondVector = Vector128.LoadUnsafe(ref secondReference, (nuint)column);
                    Vector128<byte> maskVector = Vector128.LoadUnsafe(ref maskReference, (nuint)column);
                    Blend(firstVector, secondVector, maskVector).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                int alpha = maskRow[column];
                destinationRow[column] = (byte)(((alpha * destinationRow[column]) + ((MaximumMaskAlpha - alpha) * secondRow[column]) + 32) >> MaskWeightBits);
            }
        }
    }

    /// <summary>
    /// Blends two high-bit-depth predictors through a contiguous AV1 alpha mask.
    /// </summary>
    public static void Blend(
        Span<ushort> destination,
        int destinationStride,
        ReadOnlySpan<ushort> second,
        int secondStride,
        ReadOnlySpan<byte> mask,
        int maskStride,
        int width,
        int height)
    {
        for (int row = 0; row < height; row++)
        {
            Span<ushort> destinationRow = destination.Slice(row * destinationStride, width);
            ReadOnlySpan<ushort> secondRow = second.Slice(row * secondStride, width);
            ReadOnlySpan<byte> maskRow = mask.Slice(row * maskStride, width);
            ref ushort destinationReference = ref MemoryMarshal.GetReference(destinationRow);
            ref ushort secondReference = ref MemoryMarshal.GetReference(secondRow);
            ref byte maskReference = ref MemoryMarshal.GetReference(maskRow);
            int column = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector512<ushort>.Count;
                for (; column <= vectorEnd; column += Vector512<ushort>.Count)
                {
                    Vector512<ushort> firstVector = Vector512.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector512<ushort> secondVector = Vector512.LoadUnsafe(ref secondReference, (nuint)column);
                    Vector512<ushort> maskVector = LoadMask512(ref maskReference, column);
                    Blend(firstVector, secondVector, maskVector).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector256<ushort>.Count;
                for (; column <= vectorEnd; column += Vector256<ushort>.Count)
                {
                    Vector256<ushort> firstVector = Vector256.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector256<ushort> secondVector = Vector256.LoadUnsafe(ref secondReference, (nuint)column);
                    Vector256<ushort> maskVector = LoadMask256(ref maskReference, column);
                    Blend(firstVector, secondVector, maskVector).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int vectorEnd = width - Vector128<ushort>.Count;
                for (; column <= vectorEnd; column += Vector128<ushort>.Count)
                {
                    Vector128<ushort> firstVector = Vector128.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector128<ushort> secondVector = Vector128.LoadUnsafe(ref secondReference, (nuint)column);
                    Vector128<ushort> maskVector = LoadMask128(ref maskReference, column);
                    Blend(firstVector, secondVector, maskVector).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                int alpha = maskRow[column];
                destinationRow[column] = (ushort)(((alpha * destinationRow[column]) + ((MaximumMaskAlpha - alpha) * secondRow[column]) + 32) >> MaskWeightBits);
            }
        }
    }

    /// <summary>
    /// Combines two 8-bit predictors with display-distance weights without explicit hardware intrinsics.
    /// </summary>
    public static void DistanceWeightedScalar(
        Span<byte> destination,
        int destinationStride,
        ReadOnlySpan<byte> second,
        int secondStride,
        int width,
        int height,
        int firstWeight,
        int secondWeight)
    {
        for (int row = 0; row < height; row++)
        {
            Span<byte> destinationRow = destination.Slice(row * destinationStride, width);
            ReadOnlySpan<byte> secondRow = second.Slice(row * secondStride, width);
            for (int column = 0; column < width; column++)
            {
                destinationRow[column] = (byte)(((destinationRow[column] * firstWeight) + (secondRow[column] * secondWeight) + 8) >> DistanceWeightBits);
            }
        }
    }

    /// <summary>
    /// Blends two 8-bit predictors through an alpha mask without explicit hardware intrinsics.
    /// </summary>
    public static void BlendScalar(
        Span<byte> destination,
        int destinationStride,
        ReadOnlySpan<byte> second,
        int secondStride,
        ReadOnlySpan<byte> mask,
        int maskStride,
        int width,
        int height)
    {
        for (int row = 0; row < height; row++)
        {
            Span<byte> destinationRow = destination.Slice(row * destinationStride, width);
            ReadOnlySpan<byte> secondRow = second.Slice(row * secondStride, width);
            ReadOnlySpan<byte> maskRow = mask.Slice(row * maskStride, width);
            for (int column = 0; column < width; column++)
            {
                int alpha = maskRow[column];
                destinationRow[column] = (byte)(((alpha * destinationRow[column]) + ((MaximumMaskAlpha - alpha) * secondRow[column]) + 32) >> MaskWeightBits);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> DistanceWeighted(Vector128<byte> first, Vector128<byte> second, int firstWeight, int secondWeight)
    {
        Av1IntraPredictorBase.Widen(first, out Vector128<int> first0, out Vector128<int> first1, out Vector128<int> first2, out Vector128<int> first3);
        Av1IntraPredictorBase.Widen(second, out Vector128<int> second0, out Vector128<int> second1, out Vector128<int> second2, out Vector128<int> second3);
        return Av1IntraPredictorBase.Narrow(
            DistanceWeighted(first0, second0, firstWeight, secondWeight),
            DistanceWeighted(first1, second1, firstWeight, secondWeight),
            DistanceWeighted(first2, second2, firstWeight, secondWeight),
            DistanceWeighted(first3, second3, firstWeight, secondWeight));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<byte> DistanceWeighted(Vector256<byte> first, Vector256<byte> second, int firstWeight, int secondWeight)
    {
        Av1IntraPredictorBase.Widen(first, out Vector256<int> first0, out Vector256<int> first1, out Vector256<int> first2, out Vector256<int> first3);
        Av1IntraPredictorBase.Widen(second, out Vector256<int> second0, out Vector256<int> second1, out Vector256<int> second2, out Vector256<int> second3);
        return Av1IntraPredictorBase.Narrow(
            DistanceWeighted(first0, second0, firstWeight, secondWeight),
            DistanceWeighted(first1, second1, firstWeight, secondWeight),
            DistanceWeighted(first2, second2, firstWeight, secondWeight),
            DistanceWeighted(first3, second3, firstWeight, secondWeight));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<byte> DistanceWeighted(Vector512<byte> first, Vector512<byte> second, int firstWeight, int secondWeight)
    {
        Av1IntraPredictorBase.Widen(first, out Vector512<int> first0, out Vector512<int> first1, out Vector512<int> first2, out Vector512<int> first3);
        Av1IntraPredictorBase.Widen(second, out Vector512<int> second0, out Vector512<int> second1, out Vector512<int> second2, out Vector512<int> second3);
        return Av1IntraPredictorBase.Narrow(
            DistanceWeighted(first0, second0, firstWeight, secondWeight),
            DistanceWeighted(first1, second1, firstWeight, secondWeight),
            DistanceWeighted(first2, second2, firstWeight, secondWeight),
            DistanceWeighted(first3, second3, firstWeight, secondWeight));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ushort> DistanceWeighted(Vector128<ushort> first, Vector128<ushort> second, int firstWeight, int secondWeight)
    {
        Av1IntraPredictorBase.Widen(first.AsInt16(), out Vector128<int> first0, out Vector128<int> first1);
        Av1IntraPredictorBase.Widen(second.AsInt16(), out Vector128<int> second0, out Vector128<int> second1);
        return Av1IntraPredictorBase.Narrow(
            DistanceWeighted(first0, second0, firstWeight, secondWeight),
            DistanceWeighted(first1, second1, firstWeight, secondWeight)).AsUInt16();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ushort> DistanceWeighted(Vector256<ushort> first, Vector256<ushort> second, int firstWeight, int secondWeight)
    {
        Av1IntraPredictorBase.Widen(first.AsInt16(), out Vector256<int> first0, out Vector256<int> first1);
        Av1IntraPredictorBase.Widen(second.AsInt16(), out Vector256<int> second0, out Vector256<int> second1);
        return Av1IntraPredictorBase.Narrow(
            DistanceWeighted(first0, second0, firstWeight, secondWeight),
            DistanceWeighted(first1, second1, firstWeight, secondWeight)).AsUInt16();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<ushort> DistanceWeighted(Vector512<ushort> first, Vector512<ushort> second, int firstWeight, int secondWeight)
    {
        Av1IntraPredictorBase.Widen(first.AsInt16(), out Vector512<int> first0, out Vector512<int> first1);
        Av1IntraPredictorBase.Widen(second.AsInt16(), out Vector512<int> second0, out Vector512<int> second1);
        return Av1IntraPredictorBase.Narrow(
            DistanceWeighted(first0, second0, firstWeight, secondWeight),
            DistanceWeighted(first1, second1, firstWeight, secondWeight)).AsUInt16();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> DistanceWeighted(Vector128<int> first, Vector128<int> second, int firstWeight, int secondWeight)
        => ((first * Vector128.Create(firstWeight)) + (second * Vector128.Create(secondWeight)) + Vector128.Create(8)) >> DistanceWeightBits;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> DistanceWeighted(Vector256<int> first, Vector256<int> second, int firstWeight, int secondWeight)
        => ((first * Vector256.Create(firstWeight)) + (second * Vector256.Create(secondWeight)) + Vector256.Create(8)) >> DistanceWeightBits;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<int> DistanceWeighted(Vector512<int> first, Vector512<int> second, int firstWeight, int secondWeight)
        => ((first * Vector512.Create(firstWeight)) + (second * Vector512.Create(secondWeight)) + Vector512.Create(8)) >> DistanceWeightBits;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> Blend(Vector128<byte> first, Vector128<byte> second, Vector128<byte> mask)
    {
        Av1IntraPredictorBase.Widen(first, out Vector128<int> first0, out Vector128<int> first1, out Vector128<int> first2, out Vector128<int> first3);
        Av1IntraPredictorBase.Widen(second, out Vector128<int> second0, out Vector128<int> second1, out Vector128<int> second2, out Vector128<int> second3);
        Av1IntraPredictorBase.Widen(mask, out Vector128<int> mask0, out Vector128<int> mask1, out Vector128<int> mask2, out Vector128<int> mask3);
        return Av1IntraPredictorBase.Narrow(
            Blend(first0, second0, mask0),
            Blend(first1, second1, mask1),
            Blend(first2, second2, mask2),
            Blend(first3, second3, mask3));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<byte> Blend(Vector256<byte> first, Vector256<byte> second, Vector256<byte> mask)
    {
        Av1IntraPredictorBase.Widen(first, out Vector256<int> first0, out Vector256<int> first1, out Vector256<int> first2, out Vector256<int> first3);
        Av1IntraPredictorBase.Widen(second, out Vector256<int> second0, out Vector256<int> second1, out Vector256<int> second2, out Vector256<int> second3);
        Av1IntraPredictorBase.Widen(mask, out Vector256<int> mask0, out Vector256<int> mask1, out Vector256<int> mask2, out Vector256<int> mask3);
        return Av1IntraPredictorBase.Narrow(
            Blend(first0, second0, mask0),
            Blend(first1, second1, mask1),
            Blend(first2, second2, mask2),
            Blend(first3, second3, mask3));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<byte> Blend(Vector512<byte> first, Vector512<byte> second, Vector512<byte> mask)
    {
        Av1IntraPredictorBase.Widen(first, out Vector512<int> first0, out Vector512<int> first1, out Vector512<int> first2, out Vector512<int> first3);
        Av1IntraPredictorBase.Widen(second, out Vector512<int> second0, out Vector512<int> second1, out Vector512<int> second2, out Vector512<int> second3);
        Av1IntraPredictorBase.Widen(mask, out Vector512<int> mask0, out Vector512<int> mask1, out Vector512<int> mask2, out Vector512<int> mask3);
        return Av1IntraPredictorBase.Narrow(
            Blend(first0, second0, mask0),
            Blend(first1, second1, mask1),
            Blend(first2, second2, mask2),
            Blend(first3, second3, mask3));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ushort> Blend(Vector128<ushort> first, Vector128<ushort> second, Vector128<ushort> mask)
    {
        Av1IntraPredictorBase.Widen(first.AsInt16(), out Vector128<int> first0, out Vector128<int> first1);
        Av1IntraPredictorBase.Widen(second.AsInt16(), out Vector128<int> second0, out Vector128<int> second1);
        Av1IntraPredictorBase.Widen(mask.AsInt16(), out Vector128<int> mask0, out Vector128<int> mask1);
        return Av1IntraPredictorBase.Narrow(Blend(first0, second0, mask0), Blend(first1, second1, mask1)).AsUInt16();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ushort> Blend(Vector256<ushort> first, Vector256<ushort> second, Vector256<ushort> mask)
    {
        Av1IntraPredictorBase.Widen(first.AsInt16(), out Vector256<int> first0, out Vector256<int> first1);
        Av1IntraPredictorBase.Widen(second.AsInt16(), out Vector256<int> second0, out Vector256<int> second1);
        Av1IntraPredictorBase.Widen(mask.AsInt16(), out Vector256<int> mask0, out Vector256<int> mask1);
        return Av1IntraPredictorBase.Narrow(Blend(first0, second0, mask0), Blend(first1, second1, mask1)).AsUInt16();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<ushort> Blend(Vector512<ushort> first, Vector512<ushort> second, Vector512<ushort> mask)
    {
        Av1IntraPredictorBase.Widen(first.AsInt16(), out Vector512<int> first0, out Vector512<int> first1);
        Av1IntraPredictorBase.Widen(second.AsInt16(), out Vector512<int> second0, out Vector512<int> second1);
        Av1IntraPredictorBase.Widen(mask.AsInt16(), out Vector512<int> mask0, out Vector512<int> mask1);
        return Av1IntraPredictorBase.Narrow(Blend(first0, second0, mask0), Blend(first1, second1, mask1)).AsUInt16();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> Blend(Vector128<int> first, Vector128<int> second, Vector128<int> mask)
        => ((mask * first) + ((Vector128.Create(MaximumMaskAlpha) - mask) * second) + Vector128.Create(32)) >> MaskWeightBits;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> Blend(Vector256<int> first, Vector256<int> second, Vector256<int> mask)
        => ((mask * first) + ((Vector256.Create(MaximumMaskAlpha) - mask) * second) + Vector256.Create(32)) >> MaskWeightBits;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<int> Blend(Vector512<int> first, Vector512<int> second, Vector512<int> mask)
        => ((mask * first) + ((Vector512.Create(MaximumMaskAlpha) - mask) * second) + Vector512.Create(32)) >> MaskWeightBits;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ushort> LoadMask128(ref byte source, int offset)
    {
        Vector64<byte> packed = Unsafe.As<byte, Vector64<byte>>(ref Unsafe.Add(ref source, offset));
        return Vector128.WidenLower(Vector128.Create(packed, Vector64<byte>.Zero));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ushort> LoadMask256(ref byte source, int offset)
    {
        Vector128<byte> packed = Vector128.LoadUnsafe(ref source, (nuint)offset);
        return Vector256.WidenLower(Vector256.Create(packed, Vector128<byte>.Zero));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<ushort> LoadMask512(ref byte source, int offset)
    {
        Vector256<byte> packed = Vector256.LoadUnsafe(ref source, (nuint)offset);
        return Vector512.WidenLower(Vector512.Create(packed, Vector256<byte>.Zero));
    }
}
