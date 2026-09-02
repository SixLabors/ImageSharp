// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;

/// <content>
/// Defines the closed scalar/SIMD operator contract and traversal for AV1 chroma-from-luma prediction.
/// </content>
internal static partial class Av1ChromaFromLumaPredictor
{
    /// <summary>
    /// The fixed row stride of the AV1 chroma-from-luma scratch buffer.
    /// </summary>
    private const int BufferLine = 32;

    /// <summary>
    /// Defines scalar and SIMD signed Q3 arithmetic for AV1 chroma-from-luma prediction.
    /// </summary>
    private interface IChromaFromLumaOperator
    {
        /// <summary>
        /// Predicts one chroma sample.
        /// </summary>
        /// <param name="lumaQ3">The zero-mean Q3 luma sample.</param>
        /// <param name="dc">The chroma DC prediction.</param>
        /// <param name="alphaQ3">The signed Q3 chroma scaling factor.</param>
        /// <param name="maximum">The maximum sample value.</param>
        /// <returns>The predicted chroma sample.</returns>
        public static abstract short Predict(short lumaQ3, short dc, int alphaQ3, short maximum);

        /// <summary>
        /// Predicts eight chroma samples.
        /// </summary>
        /// <param name="lumaQ3">The zero-mean Q3 luma samples.</param>
        /// <param name="dc">The chroma DC prediction.</param>
        /// <param name="alphaQ3">The signed Q3 chroma scaling factor.</param>
        /// <param name="maximum">The maximum sample value.</param>
        /// <returns>The predicted chroma samples.</returns>
        public static abstract Vector128<short> Predict(Vector128<short> lumaQ3, short dc, int alphaQ3, short maximum);

        /// <summary>
        /// Predicts sixteen chroma samples.
        /// </summary>
        /// <param name="lumaQ3">The zero-mean Q3 luma samples.</param>
        /// <param name="dc">The chroma DC prediction.</param>
        /// <param name="alphaQ3">The signed Q3 chroma scaling factor.</param>
        /// <param name="maximum">The maximum sample value.</param>
        /// <returns>The predicted chroma samples.</returns>
        public static abstract Vector256<short> Predict(Vector256<short> lumaQ3, short dc, int alphaQ3, short maximum);

        /// <summary>
        /// Predicts thirty-two chroma samples.
        /// </summary>
        /// <param name="lumaQ3">The zero-mean Q3 luma samples.</param>
        /// <param name="dc">The chroma DC prediction.</param>
        /// <param name="alphaQ3">The signed Q3 chroma scaling factor.</param>
        /// <param name="maximum">The maximum sample value.</param>
        /// <returns>The predicted chroma samples.</returns>
        public static abstract Vector512<short> Predict(Vector512<short> lumaQ3, short dc, int alphaQ3, short maximum);
    }

    /// <summary>
    /// Applies chroma-from-luma prediction to an 8-bit chroma block.
    /// </summary>
    /// <param name="lumaQ3">The zero-mean Q3 luma surface.</param>
    /// <param name="destination">The DC-predicted chroma block that receives the luma adjustment.</param>
    /// <param name="destinationStride">The distance, in samples, between destination rows.</param>
    /// <param name="alphaQ3">The signed Q3 chroma scaling factor.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    public static void Predict(ReadOnlySpan<short> lumaQ3, Span<byte> destination, int destinationStride, int alphaQ3, int width, int height)
        => Predictor<ChromaFromLumaOperator>.Predict(lumaQ3, destination, destinationStride, alphaQ3, width, height);

    /// <summary>
    /// Applies chroma-from-luma prediction to a high-bit-depth chroma block.
    /// </summary>
    /// <param name="lumaQ3">The zero-mean Q3 luma surface.</param>
    /// <param name="destination">The DC-predicted chroma block that receives the luma adjustment.</param>
    /// <param name="destinationStride">The distance, in samples, between destination rows.</param>
    /// <param name="alphaQ3">The signed Q3 chroma scaling factor.</param>
    /// <param name="bitDepth">The number of bits used to represent each sample.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    public static void Predict(ReadOnlySpan<short> lumaQ3, Span<short> destination, int destinationStride, int alphaQ3, int bitDepth, int width, int height)
        => Predictor<ChromaFromLumaOperator>.Predict(lumaQ3, destination, destinationStride, alphaQ3, bitDepth, width, height);

    /// <summary>
    /// Applies the decoded chroma scaling factor to zero-mean luma samples.
    /// </summary>
    private readonly struct ChromaFromLumaOperator : IChromaFromLumaOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short Predict(short lumaQ3, short dc, int alphaQ3, short maximum)
        {
            int scaledLumaQ0 = Av1Math.RoundPowerOf2Signed(alphaQ3 * lumaQ3, 6);

            return (short)Math.Clamp(dc + scaledLumaQ0, (short)0, maximum);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> Predict(Vector128<short> lumaQ3, short dc, int alphaQ3, short maximum)
        {
            Vector128<short> dcVector = Vector128.Create(dc);
            Vector128<short> scaledLumaQ0;

            if (Ssse3.IsSupported)
            {
                Vector128<short> alphaSign = Vector128.Create((short)alphaQ3);
                Vector128<short> alphaQ12 = Vector128.Create((short)(Math.Abs(alphaQ3) << 9));
                scaledLumaQ0 = Ssse3.MultiplyHighRoundScale(Ssse3.Abs(lumaQ3).AsInt16(), alphaQ12);
                Vector128<short> signMask = (lumaQ3 ^ alphaSign) >> 15;
                scaledLumaQ0 = (scaledLumaQ0 ^ signMask) - signMask;
            }
            else if (AdvSimd.IsSupported)
            {
                Vector128<short> alphaSign = Vector128.Create((short)alphaQ3);
                Vector128<short> alphaQ12 = Vector128.Create((short)(Math.Abs(alphaQ3) << 9));
                scaledLumaQ0 = AdvSimd.MultiplyRoundedDoublingSaturateHigh(Vector128.Abs(lumaQ3), alphaQ12);
                Vector128<short> signMask = (lumaQ3 ^ alphaSign) >> 15;
                scaledLumaQ0 = (scaledLumaQ0 ^ signMask) - signMask;
            }
            else
            {
                // WebAssembly and other Vector128 targets do not expose packed rounded-high multiply. Widening keeps
                // the same signed rounding rule without introducing a second scalar traversal.
                (Vector128<int> lower, Vector128<int> upper) = Vector128.Widen(lumaQ3);
                Vector128<int> alpha = Vector128.Create(alphaQ3);
                lower *= alpha;
                upper *= alpha;
                lower = (lower + Vector128.Create(32) + (lower >> 31)) >> 6;
                upper = (upper + Vector128.Create(32) + (upper >> 31)) >> 6;
                scaledLumaQ0 = Vector128.Narrow(lower, upper);
            }

            return Vector128.Clamp(scaledLumaQ0 + dcVector, Vector128<short>.Zero, Vector128.Create(maximum));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<short> Predict(Vector256<short> lumaQ3, short dc, int alphaQ3, short maximum)
        {
            Vector256<short> dcVector = Vector256.Create(dc);
            Vector256<short> scaledLumaQ0;

            if (Avx2.IsSupported)
            {
                Vector256<short> alphaSign = Vector256.Create((short)alphaQ3);
                Vector256<short> alphaQ12 = Vector256.Create((short)(Math.Abs(alphaQ3) << 9));
                scaledLumaQ0 = Avx2.MultiplyHighRoundScale(Avx2.Abs(lumaQ3).AsInt16(), alphaQ12);
                Vector256<short> signMask = (lumaQ3 ^ alphaSign) >> 15;
                scaledLumaQ0 = (scaledLumaQ0 ^ signMask) - signMask;
            }
            else
            {
                (Vector256<int> lower, Vector256<int> upper) = Vector256.Widen(lumaQ3);
                Vector256<int> alpha = Vector256.Create(alphaQ3);
                lower *= alpha;
                upper *= alpha;
                lower = (lower + Vector256.Create(32) + (lower >> 31)) >> 6;
                upper = (upper + Vector256.Create(32) + (upper >> 31)) >> 6;
                scaledLumaQ0 = Vector256.Narrow(lower, upper);
            }

            return Vector256.Clamp(scaledLumaQ0 + dcVector, Vector256<short>.Zero, Vector256.Create(maximum));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<short> Predict(Vector512<short> lumaQ3, short dc, int alphaQ3, short maximum)
        {
            (Vector512<int> lower, Vector512<int> upper) = Vector512.Widen(lumaQ3);
            Vector512<int> alpha = Vector512.Create(alphaQ3);
            lower *= alpha;
            upper *= alpha;
            lower = (lower + Vector512.Create(32) + (lower >> 31)) >> 6;
            upper = (upper + Vector512.Create(32) + (upper >> 31)) >> 6;
            Vector512<short> scaledLumaQ0 = Vector512.Narrow(lower, upper);

            return Vector512.Clamp(scaledLumaQ0 + Vector512.Create(dc), Vector512<short>.Zero, Vector512.Create(maximum));
        }
    }

    /// <summary>
    /// Traverses a chroma block through one closed prediction operator.
    /// </summary>
    /// <typeparam name="TOperator">The signed Q3 prediction arithmetic.</typeparam>
    private static class Predictor<TOperator>
        where TOperator : struct, IChromaFromLumaOperator
    {
        /// <summary>
        /// Applies chroma-from-luma prediction to an 8-bit block.
        /// </summary>
        public static void Predict(ReadOnlySpan<short> lumaQ3, Span<byte> destination, int destinationStride, int alphaQ3, int width, int height)
        {
            ref short lumaBase = ref MemoryMarshal.GetReference(lumaQ3);
            ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
            short dc = destinationBase;

            // CfL follows DC prediction, so one sample supplies the base value for the complete block. The fixed
            // scratch stride also makes exact-width Vector128 loads safe for the four-sample AV1 tail.
            for (int row = 0; row < height; row++)
            {
                int lumaRowOffset = row * BufferLine;
                int destinationRowOffset = row * destinationStride;
                ref short lumaRow = ref Unsafe.Add(ref lumaBase, lumaRowOffset);
                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, destinationRowOffset);
                int column = 0;

                if (Vector512.IsHardwareAccelerated)
                {
                    nuint vectorCount = Numerics.Vector512Count<short>(width - column);
                    for (; vectorCount > 0; vectorCount--, column += Vector512<short>.Count)
                    {
                        Vector512<short> prediction = TOperator.Predict(Vector512.LoadUnsafe(ref lumaRow, (nuint)column), dc, alphaQ3, byte.MaxValue);
                        Vector256<byte> packed = Vector512.Narrow(prediction.AsUInt16(), Vector512<ushort>.Zero).GetLower();
                        packed.StoreUnsafe(ref destinationRow, (nuint)column);
                    }
                }

                if (Vector256.IsHardwareAccelerated)
                {
                    nuint vectorCount = Numerics.Vector256Count<short>(width - column);
                    for (; vectorCount > 0; vectorCount--, column += Vector256<short>.Count)
                    {
                        Vector256<short> prediction = TOperator.Predict(Vector256.LoadUnsafe(ref lumaRow, (nuint)column), dc, alphaQ3, byte.MaxValue);
                        Vector128<byte> packed = Vector256.Narrow(prediction.AsUInt16(), Vector256<ushort>.Zero).GetLower();
                        packed.StoreUnsafe(ref destinationRow, (nuint)column);
                    }
                }

                if (Vector128.IsHardwareAccelerated)
                {
                    nuint vectorCount = Numerics.Vector128Count<short>(width - column);
                    for (; vectorCount > 0; vectorCount--, column += Vector128<short>.Count)
                    {
                        Vector128<short> prediction = TOperator.Predict(Vector128.LoadUnsafe(ref lumaRow, (nuint)column), dc, alphaQ3, byte.MaxValue);
                        Vector64<byte> packed = Vector128.Narrow(prediction.AsUInt16(), Vector128<ushort>.Zero).GetLower();
                        packed.StoreUnsafe(ref destinationRow, (nuint)column);
                    }

                    if (width - column >= 4)
                    {
                        Vector128<short> prediction = TOperator.Predict(Vector128.LoadUnsafe(ref lumaRow, (nuint)column), dc, alphaQ3, byte.MaxValue);
                        Vector64<byte> packed = Vector128.Narrow(prediction.AsUInt16(), Vector128<ushort>.Zero).GetLower();
                        Unsafe.WriteUnaligned(ref Unsafe.Add(ref destinationRow, column), packed.AsUInt32().ToScalar());
                        column += 4;
                    }
                }

                for (; column < width; column++)
                {
                    Unsafe.Add(ref destinationRow, column) = (byte)TOperator.Predict(Unsafe.Add(ref lumaRow, column), dc, alphaQ3, byte.MaxValue);
                }
            }
        }

        /// <summary>
        /// Applies chroma-from-luma prediction to a high-bit-depth block.
        /// </summary>
        public static void Predict(ReadOnlySpan<short> lumaQ3, Span<short> destination, int destinationStride, int alphaQ3, int bitDepth, int width, int height)
        {
            ref short lumaBase = ref MemoryMarshal.GetReference(lumaQ3);
            ref short destinationBase = ref MemoryMarshal.GetReference(destination);
            short dc = destinationBase;
            short maximum = (short)((1 << bitDepth) - 1);

            for (int row = 0; row < height; row++)
            {
                int lumaRowOffset = row * BufferLine;
                int destinationRowOffset = row * destinationStride;
                ref short lumaRow = ref Unsafe.Add(ref lumaBase, lumaRowOffset);
                ref short destinationRow = ref Unsafe.Add(ref destinationBase, destinationRowOffset);
                int column = 0;

                if (Vector512.IsHardwareAccelerated)
                {
                    nuint vectorCount = Numerics.Vector512Count<short>(width - column);
                    for (; vectorCount > 0; vectorCount--, column += Vector512<short>.Count)
                    {
                        TOperator.Predict(Vector512.LoadUnsafe(ref lumaRow, (nuint)column), dc, alphaQ3, maximum).StoreUnsafe(ref destinationRow, (nuint)column);
                    }
                }

                if (Vector256.IsHardwareAccelerated)
                {
                    nuint vectorCount = Numerics.Vector256Count<short>(width - column);
                    for (; vectorCount > 0; vectorCount--, column += Vector256<short>.Count)
                    {
                        TOperator.Predict(Vector256.LoadUnsafe(ref lumaRow, (nuint)column), dc, alphaQ3, maximum).StoreUnsafe(ref destinationRow, (nuint)column);
                    }
                }

                if (Vector128.IsHardwareAccelerated)
                {
                    nuint vectorCount = Numerics.Vector128Count<short>(width - column);
                    for (; vectorCount > 0; vectorCount--, column += Vector128<short>.Count)
                    {
                        TOperator.Predict(Vector128.LoadUnsafe(ref lumaRow, (nuint)column), dc, alphaQ3, maximum).StoreUnsafe(ref destinationRow, (nuint)column);
                    }

                    if (width - column >= 4)
                    {
                        Vector128<short> prediction = TOperator.Predict(Vector128.LoadUnsafe(ref lumaRow, (nuint)column), dc, alphaQ3, maximum);
                        Unsafe.WriteUnaligned(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref destinationRow, column)), prediction.AsUInt64().ToScalar());
                        column += 4;
                    }
                }

                for (; column < width; column++)
                {
                    Unsafe.Add(ref destinationRow, column) = TOperator.Predict(Unsafe.Add(ref lumaRow, column), dc, alphaQ3, maximum);
                }
            }
        }
    }
}
