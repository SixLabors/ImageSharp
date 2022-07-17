// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace SixLabors.ImageSharp.Formats.Png.Filters
{
    /// <summary>
    /// The Sub filter transmits the difference between each byte and the value of the corresponding byte
    /// of the prior pixel.
    /// <see href="https://www.w3.org/TR/PNG-Filters.html"/>
    /// </summary>
    internal static class SubFilter
    {
        /// <summary>
        /// Decodes a scanline, which was filtered with the sub filter.
        /// </summary>
        /// <param name="scanline">The scanline to decode.</param>
        /// <param name="bytesPerPixel">The bytes per pixel.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Decode(Span<byte> scanline, int bytesPerPixel)
        {
            // The Sub filter predicts each pixel as the previous pixel.
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Sse2.IsSupported && bytesPerPixel is 4)
            {
                DecodeSse2(scanline);
            }
            else
#endif
            {
                DecodeScalar(scanline, bytesPerPixel);
            }
        }

#if SUPPORTS_RUNTIME_INTRINSICS
        private static void DecodeSse2(Span<byte> scanline)
        {
            ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);

            Vector128<byte> d = Vector128<byte>.Zero;

            int rb = scanline.Length;
            nint offset = 1;
            while (rb >= 4)
            {
                ref byte scanRef = ref Unsafe.Add(ref scanBaseRef, offset);
                Vector128<byte> a = d;
                d = Sse2.ConvertScalarToVector128Int32(Unsafe.As<byte, int>(ref scanRef)).AsByte();

                d = Sse2.Add(d, a);

                Unsafe.As<byte, int>(ref scanRef) = Sse2.ConvertToInt32(d.AsInt32());

                rb -= 4;
                offset += 4;
            }
        }
#endif

        private static void DecodeScalar(Span<byte> scanline, int bytesPerPixel)
        {
            ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);

            // Sub(x) + Raw(x-bpp)
            nint x = bytesPerPixel + 1;
            Unsafe.Add(ref scanBaseRef, x);
            for (; x < scanline.Length; ++x)
            {
                ref byte scan = ref Unsafe.Add(ref scanBaseRef, x);
                byte prev = Unsafe.Add(ref scanBaseRef, x - bytesPerPixel);
                scan = (byte)(scan + prev);
            }
        }

        /// <summary>
        /// Encodes a scanline with the sup filter applied.
        /// </summary>
        /// <param name="scanline">The scanline to encode.</param>
        /// <param name="result">The filtered scanline result.</param>
        /// <param name="bytesPerPixel">The bytes per pixel.</param>
        /// <param name="sum">The sum of the total variance of the filtered row.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Encode(ReadOnlySpan<byte> scanline, ReadOnlySpan<byte> result, int bytesPerPixel, out int sum)
        {
            DebugGuard.MustBeSizedAtLeast(result, scanline, nameof(result));

            ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
            ref byte resultBaseRef = ref MemoryMarshal.GetReference(result);
            sum = 0;

            // Sub(x) = Raw(x) - Raw(x-bpp)
            resultBaseRef = 1;

            int x = 0;
            for (; x < bytesPerPixel; /* Note: ++x happens in the body to avoid one add operation */)
            {
                byte scan = Unsafe.Add(ref scanBaseRef, x);
                ++x;
                ref byte res = ref Unsafe.Add(ref resultBaseRef, x);
                res = scan;
                sum += Numerics.Abs(unchecked((sbyte)res));
            }

#if SUPPORTS_RUNTIME_INTRINSICS
            if (Avx2.IsSupported)
            {
                Vector256<byte> zero = Vector256<byte>.Zero;
                Vector256<int> sumAccumulator = Vector256<int>.Zero;

                for (int xLeft = x - bytesPerPixel; x + Vector256<byte>.Count <= scanline.Length; xLeft += Vector256<byte>.Count)
                {
                    Vector256<byte> scan = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref scanBaseRef, x));
                    Vector256<byte> prev = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref scanBaseRef, xLeft));

                    Vector256<byte> res = Avx2.Subtract(scan, prev);
                    Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref resultBaseRef, x + 1)) = res; // +1 to skip filter type
                    x += Vector256<byte>.Count;

                    sumAccumulator = Avx2.Add(sumAccumulator, Avx2.SumAbsoluteDifferences(Avx2.Abs(res.AsSByte()), zero).AsInt32());
                }

                sum += Numerics.EvenReduceSum(sumAccumulator);
            }
            else if (Vector.IsHardwareAccelerated)
            {
                Vector<uint> sumAccumulator = Vector<uint>.Zero;

                for (int xLeft = x - bytesPerPixel; x + Vector<byte>.Count <= scanline.Length; xLeft += Vector<byte>.Count)
                {
                    Vector<byte> scan = Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref scanBaseRef, x));
                    Vector<byte> prev = Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref scanBaseRef, xLeft));

                    Vector<byte> res = scan - prev;
                    Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref resultBaseRef, x + 1)) = res; // +1 to skip filter type
                    x += Vector<byte>.Count;

                    Numerics.Accumulate(ref sumAccumulator, Vector.AsVectorByte(Vector.Abs(Vector.AsVectorSByte(res))));
                }

                for (int i = 0; i < Vector<uint>.Count; i++)
                {
                    sum += (int)sumAccumulator[i];
                }
            }
#endif

            for (int xLeft = x - bytesPerPixel; x < scanline.Length; ++xLeft /* Note: ++x happens in the body to avoid one add operation */)
            {
                byte scan = Unsafe.Add(ref scanBaseRef, x);
                byte prev = Unsafe.Add(ref scanBaseRef, xLeft);
                ++x;
                ref byte res = ref Unsafe.Add(ref resultBaseRef, x);
                res = (byte)(scan - prev);
                sum += Numerics.Abs(unchecked((sbyte)res));
            }

            sum -= 1;
        }
    }
}
