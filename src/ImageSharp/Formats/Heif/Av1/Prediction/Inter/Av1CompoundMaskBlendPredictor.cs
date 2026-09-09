// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1CompoundInterPredictor;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1TranslationalInterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Reconstructs alpha-masked compound prediction.
/// </content>
internal static partial class Av1CompoundMaskBlendPredictor
{
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
        => Blend<CompoundMaskBlendOperator>(
            destination,
            destinationStride,
            second,
            secondStride,
            mask,
            maskStride,
            width,
            height);

    /// <summary>
    /// Executes one closed 8-bit masked compound operator.
    /// </summary>
    /// <typeparam name="TOperator">The compound arithmetic operator.</typeparam>
    private static void Blend<TOperator>(
        Span<byte> destination,
        int destinationStride,
        ReadOnlySpan<byte> second,
        int secondStride,
        ReadOnlySpan<byte> mask,
        int maskStride,
        int width,
        int height)
        where TOperator : struct, IAv1CompoundMaskBlendOperator
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
                nuint vectorCount = Numerics.Vector512Count<byte>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector512<byte>.Count)
                {
                    Vector512<byte> firstVector = Vector512.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector512<byte> secondVector = Vector512.LoadUnsafe(ref secondReference, (nuint)column);
                    Vector512<byte> maskVector = Vector512.LoadUnsafe(ref maskReference, (nuint)column);
                    TOperator.Blend(firstVector, secondVector, maskVector).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint vectorCount = Numerics.Vector256Count<byte>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector256<byte>.Count)
                {
                    Vector256<byte> firstVector = Vector256.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector256<byte> secondVector = Vector256.LoadUnsafe(ref secondReference, (nuint)column);
                    Vector256<byte> maskVector = Vector256.LoadUnsafe(ref maskReference, (nuint)column);
                    TOperator.Blend(firstVector, secondVector, maskVector).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint vectorCount = Numerics.Vector128Count<byte>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector128<byte>.Count)
                {
                    Vector128<byte> firstVector = Vector128.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector128<byte> secondVector = Vector128.LoadUnsafe(ref secondReference, (nuint)column);
                    Vector128<byte> maskVector = Vector128.LoadUnsafe(ref maskReference, (nuint)column);
                    TOperator.Blend(firstVector, secondVector, maskVector).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                destinationRow[column] = TOperator.Blend(destinationRow[column], secondRow[column], maskRow[column]);
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
        => Blend<CompoundMaskBlendOperator>(
            destination,
            destinationStride,
            second,
            secondStride,
            mask,
            maskStride,
            width,
            height);

    /// <summary>
    /// Executes one closed high-bit-depth masked compound operator.
    /// </summary>
    /// <typeparam name="TOperator">The compound arithmetic operator.</typeparam>
    private static void Blend<TOperator>(
        Span<ushort> destination,
        int destinationStride,
        ReadOnlySpan<ushort> second,
        int secondStride,
        ReadOnlySpan<byte> mask,
        int maskStride,
        int width,
        int height)
        where TOperator : struct, IAv1CompoundMaskBlendOperator
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
                nuint vectorCount = Numerics.Vector512Count<ushort>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector512<ushort>.Count)
                {
                    Vector512<ushort> firstVector = Vector512.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector512<ushort> secondVector = Vector512.LoadUnsafe(ref secondReference, (nuint)column);
                    Vector512<ushort> maskVector = LoadMask512(ref maskReference, column);
                    TOperator.Blend(firstVector, secondVector, maskVector).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint vectorCount = Numerics.Vector256Count<ushort>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector256<ushort>.Count)
                {
                    Vector256<ushort> firstVector = Vector256.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector256<ushort> secondVector = Vector256.LoadUnsafe(ref secondReference, (nuint)column);
                    Vector256<ushort> maskVector = LoadMask256(ref maskReference, column);
                    TOperator.Blend(firstVector, secondVector, maskVector).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint vectorCount = Numerics.Vector128Count<ushort>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector128<ushort>.Count)
                {
                    Vector128<ushort> firstVector = Vector128.LoadUnsafe(ref destinationReference, (nuint)column);
                    Vector128<ushort> secondVector = Vector128.LoadUnsafe(ref secondReference, (nuint)column);
                    Vector128<ushort> maskVector = LoadMask128(ref maskReference, column);
                    TOperator.Blend(firstVector, secondVector, maskVector).StoreUnsafe(ref destinationReference, (nuint)column);
                }
            }

            for (; column < width; column++)
            {
                destinationRow[column] = TOperator.Blend(destinationRow[column], secondRow[column], maskRow[column]);
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
        => BlendScalar<CompoundMaskBlendOperator>(
            destination,
            destinationStride,
            second,
            secondStride,
            mask,
            maskStride,
            width,
            height);

    /// <summary>
    /// Executes one closed 8-bit masked compound operator without explicit hardware intrinsics.
    /// </summary>
    /// <typeparam name="TOperator">The compound arithmetic operator.</typeparam>
    private static void BlendScalar<TOperator>(
        Span<byte> destination,
        int destinationStride,
        ReadOnlySpan<byte> second,
        int secondStride,
        ReadOnlySpan<byte> mask,
        int maskStride,
        int width,
        int height)
        where TOperator : struct, IAv1CompoundMaskBlendOperator
    {
        for (int row = 0; row < height; row++)
        {
            Span<byte> destinationRow = destination.Slice(row * destinationStride, width);
            ReadOnlySpan<byte> secondRow = second.Slice(row * secondStride, width);
            ReadOnlySpan<byte> maskRow = mask.Slice(row * maskStride, width);
            for (int column = 0; column < width; column++)
            {
                destinationRow[column] = TOperator.Blend(destinationRow[column], secondRow[column], maskRow[column]);
            }
        }
    }

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
