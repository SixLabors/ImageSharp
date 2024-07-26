// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Png.Filters;

/// <summary>
/// The Up filter is just like the Sub filter except that the pixel immediately above the current pixel,
/// rather than just to its left, is used as the predictor.
/// <see href="https://www.w3.org/TR/PNG-Filters.html"/>
/// </summary>
internal static class UpFilter
{
    /// <summary>
    /// Decodes a scanline, which was filtered with the up filter.
    /// </summary>
    /// <param name="scanline">The scanline to decode</param>
    /// <param name="previousScanline">The previous scanline.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Decode(Span<byte> scanline, Span<byte> previousScanline)
    {
        DebugGuard.MustBeSameSized<byte>(scanline, previousScanline, nameof(scanline));

        if (Avx2.IsSupported)
        {
            DecodeAvx2(scanline, previousScanline);
        }
        else if (Sse2.IsSupported)
        {
            DecodeSse2(scanline, previousScanline);
        }
        else if (AdvSimd.IsSupported)
        {
            DecodeArm(scanline, previousScanline);
        }
        else
        {
            DecodeScalar(scanline, previousScanline);
        }
    }

    private static void DecodeAvx2(Span<byte> scanline, Span<byte> previousScanline)
    {
        ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
        ref byte prevBaseRef = ref MemoryMarshal.GetReference(previousScanline);

        // Up(x) + Prior(x)
        int rb = scanline.Length;
        nuint offset = 1;
        while (rb >= Vector256<byte>.Count)
        {
            ref byte scanRef = ref Unsafe.Add(ref scanBaseRef, offset);
            Vector256<byte> prior = Unsafe.As<byte, Vector256<byte>>(ref scanRef);
            Vector256<byte> up = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref prevBaseRef, offset));

            Unsafe.As<byte, Vector256<byte>>(ref scanRef) = Avx2.Add(up, prior);

            offset += (uint)Vector256<byte>.Count;
            rb -= Vector256<byte>.Count;
        }

        // Handle left over.
        for (nuint i = offset; i < (uint)scanline.Length; i++)
        {
            ref byte scan = ref Unsafe.Add(ref scanBaseRef, offset);
            byte above = Unsafe.Add(ref prevBaseRef, offset);
            scan = (byte)(scan + above);
            offset++;
        }
    }

    private static void DecodeSse2(Span<byte> scanline, Span<byte> previousScanline)
    {
        ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
        ref byte prevBaseRef = ref MemoryMarshal.GetReference(previousScanline);

        // Up(x) + Prior(x)
        int rb = scanline.Length;
        nuint offset = 1;
        while (rb >= Vector128<byte>.Count)
        {
            ref byte scanRef = ref Unsafe.Add(ref scanBaseRef, offset);
            Vector128<byte> prior = Unsafe.As<byte, Vector128<byte>>(ref scanRef);
            Vector128<byte> up = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref prevBaseRef, offset));

            Unsafe.As<byte, Vector128<byte>>(ref scanRef) = Sse2.Add(up, prior);

            offset += (uint)Vector128<byte>.Count;
            rb -= Vector128<byte>.Count;
        }

        // Handle left over.
        for (nuint i = offset; i < (uint)scanline.Length; i++)
        {
            ref byte scan = ref Unsafe.Add(ref scanBaseRef, offset);
            byte above = Unsafe.Add(ref prevBaseRef, offset);
            scan = (byte)(scan + above);
            offset++;
        }
    }

    private static void DecodeArm(Span<byte> scanline, Span<byte> previousScanline)
    {
        ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
        ref byte prevBaseRef = ref MemoryMarshal.GetReference(previousScanline);

        // Up(x) + Prior(x)
        int rb = scanline.Length;
        nuint offset = 1;
        const int bytesPerBatch = 16;
        while (rb >= bytesPerBatch)
        {
            ref byte scanRef = ref Unsafe.Add(ref scanBaseRef, offset);
            Vector128<byte> prior = Unsafe.As<byte, Vector128<byte>>(ref scanRef);
            Vector128<byte> up = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref prevBaseRef, offset));

            Unsafe.As<byte, Vector128<byte>>(ref scanRef) = AdvSimd.Add(prior, up);

            offset += bytesPerBatch;
            rb -= bytesPerBatch;
        }

        // Handle left over.
        for (nuint i = offset; i < (uint)scanline.Length; i++)
        {
            ref byte scan = ref Unsafe.Add(ref scanBaseRef, offset);
            byte above = Unsafe.Add(ref prevBaseRef, offset);
            scan = (byte)(scan + above);
            offset++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DecodeScalar(Span<byte> scanline, Span<byte> previousScanline)
    {
        ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
        ref byte prevBaseRef = ref MemoryMarshal.GetReference(previousScanline);

        // Up(x) + Prior(x)
        for (nuint x = 1; x < (uint)scanline.Length; x++)
        {
            ref byte scan = ref Unsafe.Add(ref scanBaseRef, x);
            byte above = Unsafe.Add(ref prevBaseRef, x);
            scan = (byte)(scan + above);
        }
    }

    /// <summary>
    /// Encodes a scanline with the up filter applied.
    /// </summary>
    /// <param name="scanline">The scanline to encode.</param>
    /// <param name="previousScanline">The previous scanline.</param>
    /// <param name="result">The filtered scanline result.</param>
    /// <param name="sum">The sum of the total variance of the filtered row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Encode(ReadOnlySpan<byte> scanline, ReadOnlySpan<byte> previousScanline, Span<byte> result, out int sum)
    {
        DebugGuard.MustBeSameSized(scanline, previousScanline, nameof(scanline));
        DebugGuard.MustBeSizedAtLeast(result, scanline, nameof(result));

        ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
        ref byte prevBaseRef = ref MemoryMarshal.GetReference(previousScanline);
        ref byte resultBaseRef = ref MemoryMarshal.GetReference(result);
        sum = 0;

        // Up(x) = Raw(x) - Prior(x)
        resultBaseRef = (byte)FilterType.Up;

        nuint x = 0;

        if (Avx2.IsSupported)
        {
            Vector256<byte> zero = Vector256<byte>.Zero;
            Vector256<int> sumAccumulator = Vector256<int>.Zero;

            for (; (int)x <= scanline.Length - Vector256<byte>.Count;)
            {
                Vector256<byte> scan = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref scanBaseRef, x));
                Vector256<byte> above = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref prevBaseRef, x));

                Vector256<byte> res = Avx2.Subtract(scan, above);
                Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref resultBaseRef, x + 1)) = res; // +1 to skip filter type
                x += (uint)Vector256<byte>.Count;

                sumAccumulator = Avx2.Add(sumAccumulator, Avx2.SumAbsoluteDifferences(Avx2.Abs(res.AsSByte()), zero).AsInt32());
            }

            sum += Numerics.EvenReduceSum(sumAccumulator);
        }
        else if (Vector.IsHardwareAccelerated)
        {
            Vector<uint> sumAccumulator = Vector<uint>.Zero;

            for (; (int)x <= scanline.Length - Vector<byte>.Count;)
            {
                Vector<byte> scan = Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref scanBaseRef, x));
                Vector<byte> above = Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref prevBaseRef, x));

                Vector<byte> res = scan - above;
                Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref resultBaseRef, x + 1)) = res; // +1 to skip filter type
                x += (uint)Vector<byte>.Count;

                Numerics.Accumulate(ref sumAccumulator, Vector.AsVectorByte(Vector.Abs(Vector.AsVectorSByte(res))));
            }

            for (int i = 0; i < Vector<uint>.Count; i++)
            {
                sum += (int)sumAccumulator[i];
            }
        }

        for (; x < (uint)scanline.Length; /* Note: ++x happens in the body to avoid one add operation */)
        {
            byte scan = Unsafe.Add(ref scanBaseRef, x);
            byte above = Unsafe.Add(ref prevBaseRef, x);
            ++x;
            ref byte res = ref Unsafe.Add(ref resultBaseRef, x);
            res = (byte)(scan - above);
            sum += Numerics.Abs(unchecked((sbyte)res));
        }
    }
}
