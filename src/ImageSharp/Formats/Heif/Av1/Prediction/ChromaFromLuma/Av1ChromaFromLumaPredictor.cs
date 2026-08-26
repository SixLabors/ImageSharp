// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;

/// <summary>
/// Applies an AV1 chroma-from-luma residual to a DC-predicted chroma block.
/// </summary>
/// <remarks>
/// Each signed 16-bit lane contains one zero-mean Q3 luma value. Packed rounded-high multiplication converts the product
/// with the Q3 alpha parameter directly to a signed integer adjustment; the alpha/luma sign mask restores the product
/// sign after the magnitude operation. The common DC prediction is broadcast, then results are clipped and narrowed to
/// the destination sample representation.
/// </remarks>
internal static class Av1ChromaFromLumaPredictor
{
    /// <summary>
    /// The fixed row stride of the AV1 chroma-from-luma scratch buffer.
    /// </summary>
    private const int BufferLine = 32;

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
    {
        ref short lumaBase = ref MemoryMarshal.GetReference(lumaQ3);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        short dc = destinationBase;

        // CfL always follows DC prediction, so the first sample is the common base value for every lane. This
        // mirrors libaom and avoids loading a block that is known to contain a single repeated prediction value.
        if (Avx2.IsSupported && width >= Vector256<short>.Count)
        {
            Vector256<short> alphaSign = Vector256.Create((short)alphaQ3);
            Vector256<short> alphaQ12 = Vector256.Create((short)(Math.Abs(alphaQ3) << 9));
            Vector256<short> dcVector = Vector256.Create(dc);
            Vector256<short> maximum = Vector256.Create((short)byte.MaxValue);

            for (int row = 0; row < height; row++)
            {
                int lumaRowOffset = row * BufferLine;
                int destinationRowOffset = row * destinationStride;

                for (int column = 0; column < width; column += Vector256<short>.Count)
                {
                    Vector256<short> prediction = Predict(Vector256.LoadUnsafe(ref lumaBase, (nuint)(lumaRowOffset + column)), alphaSign, alphaQ12, dcVector);
                    prediction = Vector256.Clamp(prediction, Vector256<short>.Zero, maximum);
                    Vector256.Narrow(prediction.AsUInt16(), Vector256<ushort>.Zero).GetLower().StoreUnsafe(ref destinationBase, (nuint)(destinationRowOffset + column));
                }
            }

            return;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<short> alphaSign = Vector128.Create((short)alphaQ3);
            Vector128<short> alphaQ12 = Vector128.Create((short)(Math.Abs(alphaQ3) << 9));
            Vector128<short> dcVector = Vector128.Create(dc);
            Vector128<short> maximum = Vector128.Create((short)byte.MaxValue);

            for (int row = 0; row < height; row++)
            {
                int lumaRowOffset = row * BufferLine;
                int destinationRowOffset = row * destinationStride;
                int column = 0;

                for (; column <= width - Vector128<short>.Count; column += Vector128<short>.Count)
                {
                    Vector128<short> prediction = Predict(Vector128.LoadUnsafe(ref lumaBase, (nuint)(lumaRowOffset + column)), alphaSign, alphaQ12, dcVector, alphaQ3);
                    prediction = Vector128.Clamp(prediction, Vector128<short>.Zero, maximum);
                    Vector128.Narrow(prediction.AsUInt16(), Vector128<ushort>.Zero).GetLower().StoreUnsafe(ref destinationBase, (nuint)(destinationRowOffset + column));
                }

                // Four-wide CfL blocks still have a complete padded scratch row, so reading eight residuals is
                // valid. Only the four active predictions are stored to the image buffer.
                if (column < width)
                {
                    Vector128<short> prediction = Predict(Vector128.LoadUnsafe(ref lumaBase, (nuint)(lumaRowOffset + column)), alphaSign, alphaQ12, dcVector, alphaQ3);
                    prediction = Vector128.Clamp(prediction, Vector128<short>.Zero, maximum);
                    Vector64<byte> packed = Vector128.Narrow(prediction.AsUInt16(), Vector128<ushort>.Zero).GetLower();
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref destinationBase, destinationRowOffset + column), packed.AsUInt32().ToScalar());
                }
            }

            return;
        }

        for (int row = 0; row < height; row++)
        {
            int lumaRowOffset = row * BufferLine;
            int destinationRowOffset = row * destinationStride;
            for (int column = 0; column < width; column++)
            {
                int scaledLumaQ0 = Av1Math.RoundPowerOf2Signed(alphaQ3 * Unsafe.Add(ref lumaBase, lumaRowOffset + column), 6);
                Unsafe.Add(ref destinationBase, destinationRowOffset + column) = (byte)Math.Clamp(dc + scaledLumaQ0, byte.MinValue, byte.MaxValue);
            }
        }
    }

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
    {
        ref short lumaBase = ref MemoryMarshal.GetReference(lumaQ3);
        ref short destinationBase = ref MemoryMarshal.GetReference(destination);
        short dc = destinationBase;
        short maximum = (short)((1 << bitDepth) - 1);

        if (Avx2.IsSupported && width >= Vector256<short>.Count)
        {
            Vector256<short> alphaSign = Vector256.Create((short)alphaQ3);
            Vector256<short> alphaQ12 = Vector256.Create((short)(Math.Abs(alphaQ3) << 9));
            Vector256<short> dcVector = Vector256.Create(dc);
            Vector256<short> maximumVector = Vector256.Create(maximum);

            for (int row = 0; row < height; row++)
            {
                int lumaRowOffset = row * BufferLine;
                int destinationRowOffset = row * destinationStride;

                for (int column = 0; column < width; column += Vector256<short>.Count)
                {
                    Vector256<short> prediction = Predict(Vector256.LoadUnsafe(ref lumaBase, (nuint)(lumaRowOffset + column)), alphaSign, alphaQ12, dcVector);
                    Vector256.Clamp(prediction, Vector256<short>.Zero, maximumVector).StoreUnsafe(ref destinationBase, (nuint)(destinationRowOffset + column));
                }
            }

            return;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<short> alphaSign = Vector128.Create((short)alphaQ3);
            Vector128<short> alphaQ12 = Vector128.Create((short)(Math.Abs(alphaQ3) << 9));
            Vector128<short> dcVector = Vector128.Create(dc);
            Vector128<short> maximumVector = Vector128.Create(maximum);

            for (int row = 0; row < height; row++)
            {
                int lumaRowOffset = row * BufferLine;
                int destinationRowOffset = row * destinationStride;
                int column = 0;

                for (; column <= width - Vector128<short>.Count; column += Vector128<short>.Count)
                {
                    Vector128<short> prediction = Predict(Vector128.LoadUnsafe(ref lumaBase, (nuint)(lumaRowOffset + column)), alphaSign, alphaQ12, dcVector, alphaQ3);
                    Vector128.Clamp(prediction, Vector128<short>.Zero, maximumVector).StoreUnsafe(ref destinationBase, (nuint)(destinationRowOffset + column));
                }

                if (column < width)
                {
                    Vector128<short> prediction = Predict(Vector128.LoadUnsafe(ref lumaBase, (nuint)(lumaRowOffset + column)), alphaSign, alphaQ12, dcVector, alphaQ3);
                    prediction = Vector128.Clamp(prediction, Vector128<short>.Zero, maximumVector);
                    Unsafe.WriteUnaligned(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref destinationBase, destinationRowOffset + column)), prediction.AsUInt64().ToScalar());
                }
            }

            return;
        }

        for (int row = 0; row < height; row++)
        {
            int lumaRowOffset = row * BufferLine;
            int destinationRowOffset = row * destinationStride;
            for (int column = 0; column < width; column++)
            {
                int scaledLumaQ0 = Av1Math.RoundPowerOf2Signed(alphaQ3 * Unsafe.Add(ref lumaBase, lumaRowOffset + column), 6);
                Unsafe.Add(ref destinationBase, destinationRowOffset + column) = (short)Math.Clamp(dc + scaledLumaQ0, 0, maximum);
            }
        }
    }

    /// <summary>
    /// Calculates sixteen chroma predictions using the packed Q3 arithmetic defined by AV1.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<short> Predict(Vector256<short> lumaQ3, Vector256<short> alphaSign, Vector256<short> alphaQ12, Vector256<short> dc)
    {
        Vector256<short> scaledLumaQ0 = Avx2.MultiplyHighRoundScale(Avx2.Abs(lumaQ3).AsInt16(), alphaQ12);
        Vector256<short> signMask = (lumaQ3 ^ alphaSign) >> 15;
        return ((scaledLumaQ0 ^ signMask) - signMask) + dc;
    }

    /// <summary>
    /// Calculates eight chroma predictions using the packed Q3 arithmetic defined by AV1.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<short> Predict(Vector128<short> lumaQ3, Vector128<short> alphaSign, Vector128<short> alphaQ12, Vector128<short> dc, int alphaQ3)
    {
        Vector128<short> scaledLumaQ0;
        if (Ssse3.IsSupported)
        {
            scaledLumaQ0 = Ssse3.MultiplyHighRoundScale(Ssse3.Abs(lumaQ3).AsInt16(), alphaQ12);
        }
        else if (AdvSimd.IsSupported)
        {
            scaledLumaQ0 = AdvSimd.MultiplyRoundedDoublingSaturateHigh(Vector128.Abs(lumaQ3), alphaQ12);
        }
        else
        {
            // WebAssembly and other Vector128 targets do not expose packed rounded-high multiply. Widening retains
            // SIMD traversal while reproducing the same signed nearest-integer result in ordinary integer lanes.
            (Vector128<int> lower, Vector128<int> upper) = Vector128.Widen(lumaQ3);
            Vector128<int> alpha = Vector128.Create(alphaQ3);
            lower *= alpha;
            upper *= alpha;
            lower = (lower + Vector128.Create(32) + (lower >> 31)) >> 6;
            upper = (upper + Vector128.Create(32) + (upper >> 31)) >> 6;
            return Vector128.Narrow(lower, upper) + dc;
        }

        Vector128<short> signMask = (lumaQ3 ^ alphaSign) >> 15;
        return ((scaledLumaQ0 ^ signMask) - signMask) + dc;
    }
}
