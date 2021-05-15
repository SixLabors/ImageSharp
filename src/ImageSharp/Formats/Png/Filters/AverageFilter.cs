// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace SixLabors.ImageSharp.Formats.Png.Filters
{
    /// <summary>
    /// The Average filter uses the average of the two neighboring pixels (left and above) to predict
    /// the value of a pixel.
    /// <see href="https://www.w3.org/TR/PNG-Filters.html"/>
    /// </summary>
    internal static class AverageFilter
    {
        /// <summary>
        /// Decodes the scanline
        /// </summary>
        /// <param name="scanline">The scanline to decode</param>
        /// <param name="previousScanline">The previous scanline.</param>
        /// <param name="bytesPerPixel">The bytes per pixel.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Decode(Span<byte> scanline, Span<byte> previousScanline, int bytesPerPixel)
        {
            DebugGuard.MustBeSameSized(scanline, previousScanline, nameof(scanline));

            ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
            ref byte prevBaseRef = ref MemoryMarshal.GetReference(previousScanline);

            // Average(x) + floor((Raw(x-bpp)+Prior(x))/2)
            int x = 1;
            for (; x <= bytesPerPixel /* Note the <= because x starts at 1 */; ++x)
            {
                ref byte scan = ref Unsafe.Add(ref scanBaseRef, x);
                byte above = Unsafe.Add(ref prevBaseRef, x);
                scan = (byte)(scan + (above >> 1));
            }

            for (; x < scanline.Length; ++x)
            {
                ref byte scan = ref Unsafe.Add(ref scanBaseRef, x);
                byte left = Unsafe.Add(ref scanBaseRef, x - bytesPerPixel);
                byte above = Unsafe.Add(ref prevBaseRef, x);
                scan = (byte)(scan + Average(left, above));
            }
        }

        /// <summary>
        /// Encodes the scanline
        /// </summary>
        /// <param name="scanline">The scanline to encode</param>
        /// <param name="previousScanline">The previous scanline.</param>
        /// <param name="result">The filtered scanline result.</param>
        /// <param name="bytesPerPixel">The bytes per pixel.</param>
        /// <param name="sum">The sum of the total variance of the filtered row</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Encode(Span<byte> scanline, Span<byte> previousScanline, Span<byte> result, int bytesPerPixel, out int sum)
        {
            DebugGuard.MustBeSameSized(scanline, previousScanline, nameof(scanline));
            DebugGuard.MustBeSizedAtLeast(result, scanline, nameof(result));

            ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
            ref byte prevBaseRef = ref MemoryMarshal.GetReference(previousScanline);
            ref byte resultBaseRef = ref MemoryMarshal.GetReference(result);
            sum = 0;

            // Average(x) = Raw(x) - floor((Raw(x-bpp)+Prior(x))/2)
            resultBaseRef = 3;

            int x = 0;
            for (; x < bytesPerPixel; /* Note: ++x happens in the body to avoid one add operation */)
            {
                byte scan = Unsafe.Add(ref scanBaseRef, x);
                byte above = Unsafe.Add(ref prevBaseRef, x);
                ++x;
                ref byte res = ref Unsafe.Add(ref resultBaseRef, x);
                res = (byte)(scan - (above >> 1));
                sum += Numerics.Abs(unchecked((sbyte)res));
            }

#if SUPPORTS_RUNTIME_INTRINSICS
            if (Avx2.IsSupported)
            {
                Vector256<int> sumAccumulator = Vector256<int>.Zero;

                for (int xLeft = x - bytesPerPixel; x + Vector256<byte>.Count <= scanline.Length; xLeft += Vector256<byte>.Count)
                {
                    Vector256<byte> scan = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref scanBaseRef, x));
                    Vector256<byte> left = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref scanBaseRef, xLeft));
                    Vector256<byte> above = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref prevBaseRef, x));

                    Vector256<byte> res = Avx2.Subtract(scan, Average(left, above));
                    Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref resultBaseRef, x + 1)) = res; // +1 to skip filter type
                    x += Vector256<byte>.Count;

                    Vector256<sbyte> absRes = Avx2.Abs(res.AsSByte()).AsSByte();
                    Vector256<short> loRes16 = Avx2.UnpackLow(absRes, Vector256<sbyte>.Zero).AsInt16();
                    Vector256<short> hiRes16 = Avx2.UnpackHigh(absRes, Vector256<sbyte>.Zero).AsInt16();

                    Vector256<int> loRes32 = Avx2.UnpackLow(loRes16, Vector256<short>.Zero).AsInt32();
                    Vector256<int> hiRes32 = Avx2.UnpackHigh(loRes16, Vector256<short>.Zero).AsInt32();
                    sumAccumulator = Avx2.Add(sumAccumulator, loRes32);
                    sumAccumulator = Avx2.Add(sumAccumulator, hiRes32);

                    loRes32 = Avx2.UnpackLow(hiRes16, Vector256<short>.Zero).AsInt32();
                    hiRes32 = Avx2.UnpackHigh(hiRes16, Vector256<short>.Zero).AsInt32();
                    sumAccumulator = Avx2.Add(sumAccumulator, loRes32);
                    sumAccumulator = Avx2.Add(sumAccumulator, hiRes32);
                }

                for (int i = 0; i < Vector256<int>.Count; i++)
                {
                    sum += sumAccumulator.GetElement(i);
                }
            }
            else if (Sse2.IsSupported)
            {
                var allBitsSet = Vector128.Create((sbyte)-1);
                Vector128<int> sumAccumulator = Vector128<int>.Zero;

                for (int xLeft = x - bytesPerPixel; x + Vector128<byte>.Count <= scanline.Length; xLeft += Vector128<byte>.Count)
                {
                    Vector128<byte> scan = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref scanBaseRef, x));
                    Vector128<byte> left = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref scanBaseRef, xLeft));
                    Vector128<byte> above = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref prevBaseRef, x));

                    Vector128<byte> res = Sse2.Subtract(scan, Average(left, above));
                    Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref resultBaseRef, x + 1)) = res; // +1 to skip filter type
                    x += Vector128<byte>.Count;

                    Vector128<sbyte> absRes;
                    if (Ssse3.IsSupported)
                    {
                        absRes = Ssse3.Abs(res.AsSByte()).AsSByte();
                    }
                    else
                    {
                        Vector128<sbyte> mask = Sse2.CompareGreaterThan(res.AsSByte(), Vector128<sbyte>.Zero);
                        mask = Sse2.Xor(mask, allBitsSet);
                        absRes = Sse2.Xor(Sse2.Add(res.AsSByte(), mask), mask);
                    }

                    Vector128<short> loRes16 = Sse2.UnpackLow(absRes, Vector128<sbyte>.Zero).AsInt16();
                    Vector128<short> hiRes16 = Sse2.UnpackHigh(absRes, Vector128<sbyte>.Zero).AsInt16();

                    Vector128<int> loRes32 = Sse2.UnpackLow(loRes16, Vector128<short>.Zero).AsInt32();
                    Vector128<int> hiRes32 = Sse2.UnpackHigh(loRes16, Vector128<short>.Zero).AsInt32();
                    sumAccumulator = Sse2.Add(sumAccumulator, loRes32);
                    sumAccumulator = Sse2.Add(sumAccumulator, hiRes32);

                    loRes32 = Sse2.UnpackLow(hiRes16, Vector128<short>.Zero).AsInt32();
                    hiRes32 = Sse2.UnpackHigh(hiRes16, Vector128<short>.Zero).AsInt32();
                    sumAccumulator = Sse2.Add(sumAccumulator, loRes32);
                    sumAccumulator = Sse2.Add(sumAccumulator, hiRes32);
                }

                for (int i = 0; i < Vector128<int>.Count; i++)
                {
                    sum += sumAccumulator.GetElement(i);
                }
            }
#endif

            for (int xLeft = x - bytesPerPixel; x < scanline.Length; ++xLeft /* Note: ++x happens in the body to avoid one add operation */)
            {
                byte scan = Unsafe.Add(ref scanBaseRef, x);
                byte left = Unsafe.Add(ref scanBaseRef, xLeft);
                byte above = Unsafe.Add(ref prevBaseRef, x);
                ++x;
                ref byte res = ref Unsafe.Add(ref resultBaseRef, x);
                res = (byte)(scan - Average(left, above));
                sum += Numerics.Abs(unchecked((sbyte)res));
            }

            sum -= 3;
        }

        /// <summary>
        /// Calculates the average value of two bytes
        /// </summary>
        /// <param name="left">The left byte</param>
        /// <param name="above">The above byte</param>
        /// <returns>The <see cref="int"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Average(byte left, byte above) => (left + above) >> 1;

#if SUPPORTS_RUNTIME_INTRINSICS
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<byte> Average(Vector128<byte> left, Vector128<byte> above)
        {
            Vector128<ushort> loLeft16 = Sse2.UnpackLow(left, Vector128<byte>.Zero).AsUInt16();
            Vector128<ushort> hiLeft16 = Sse2.UnpackHigh(left, Vector128<byte>.Zero).AsUInt16();

            Vector128<ushort> loAbove16 = Sse2.UnpackLow(above, Vector128<byte>.Zero).AsUInt16();
            Vector128<ushort> hiAbove16 = Sse2.UnpackHigh(above, Vector128<byte>.Zero).AsUInt16();

            Vector128<ushort> div1 = Sse2.ShiftRightLogical(Sse2.Add(loLeft16, loAbove16), 1);
            Vector128<ushort> div2 = Sse2.ShiftRightLogical(Sse2.Add(hiLeft16, hiAbove16), 1);

            return Sse2.PackUnsignedSaturate(div1.AsInt16(), div2.AsInt16());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<byte> Average(Vector256<byte> left, Vector256<byte> above)
        {
            Vector256<ushort> loLeft16 = Avx2.UnpackLow(left, Vector256<byte>.Zero).AsUInt16();
            Vector256<ushort> hiLeft16 = Avx2.UnpackHigh(left, Vector256<byte>.Zero).AsUInt16();

            Vector256<ushort> loAbove16 = Avx2.UnpackLow(above, Vector256<byte>.Zero).AsUInt16();
            Vector256<ushort> hiAbove16 = Avx2.UnpackHigh(above, Vector256<byte>.Zero).AsUInt16();

            Vector256<ushort> div1 = Avx2.ShiftRightLogical(Avx2.Add(loLeft16, loAbove16), 1);
            Vector256<ushort> div2 = Avx2.ShiftRightLogical(Avx2.Add(hiLeft16, hiAbove16), 1);

            return Avx2.PackUnsignedSaturate(div1.AsInt16(), div2.AsInt16());
        }
#endif
    }
}
