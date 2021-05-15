// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Png.Filters
{
    /// <summary>
    /// The Paeth filter computes a simple linear function of the three neighboring pixels (left, above, upper left),
    /// then chooses as predictor the neighboring pixel closest to the computed value.
    /// This technique is due to Alan W. Paeth.
    /// <see href="https://www.w3.org/TR/PNG-Filters.html"/>
    /// </summary>
    internal static class PaethFilter
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

            // Paeth(x) + PaethPredictor(Raw(x-bpp), Prior(x), Prior(x-bpp))
            int offset = bytesPerPixel + 1; // Add one because x starts at one.
            int x = 1;
            for (; x < offset; x++)
            {
                ref byte scan = ref Unsafe.Add(ref scanBaseRef, x);
                byte above = Unsafe.Add(ref prevBaseRef, x);
                scan = (byte)(scan + above);
            }

            for (; x < scanline.Length; x++)
            {
                ref byte scan = ref Unsafe.Add(ref scanBaseRef, x);
                byte left = Unsafe.Add(ref scanBaseRef, x - bytesPerPixel);
                byte above = Unsafe.Add(ref prevBaseRef, x);
                byte upperLeft = Unsafe.Add(ref prevBaseRef, x - bytesPerPixel);
                scan = (byte)(scan + PaethPredictor(left, above, upperLeft));
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

            // Paeth(x) = Raw(x) - PaethPredictor(Raw(x-bpp), Prior(x), Prior(x - bpp))
            resultBaseRef = 4;

            int x = 0;
            for (; x < bytesPerPixel; /* Note: ++x happens in the body to avoid one add operation */)
            {
                byte scan = Unsafe.Add(ref scanBaseRef, x);
                byte above = Unsafe.Add(ref prevBaseRef, x);
                ++x;
                ref byte res = ref Unsafe.Add(ref resultBaseRef, x);
                res = (byte)(scan - PaethPredictor(0, above, 0));
                sum += Numerics.Abs(unchecked((sbyte)res));
            }

#if SUPPORTS_RUNTIME_INTRINSICS
            if (Vector.IsHardwareAccelerated)
            {
                Vector<uint> sumAccumulator = Vector<uint>.Zero;

                for (int xLeft = x - bytesPerPixel; x + Vector<byte>.Count <= scanline.Length; xLeft += Vector<byte>.Count)
                {
                    var scan = new Vector<byte>(scanline.Slice(x));
                    var left = new Vector<byte>(scanline.Slice(xLeft));
                    var above = new Vector<byte>(previousScanline.Slice(x));
                    var upperLeft = new Vector<byte>(previousScanline.Slice(xLeft));

                    Vector<byte> res = scan - PaethPredictor(left, above, upperLeft);
                    res.CopyTo(result.Slice(x + 1)); // + 1 to skip filter type
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
                byte left = Unsafe.Add(ref scanBaseRef, xLeft);
                byte above = Unsafe.Add(ref prevBaseRef, x);
                byte upperLeft = Unsafe.Add(ref prevBaseRef, xLeft);
                ++x;
                ref byte res = ref Unsafe.Add(ref resultBaseRef, x);
                res = (byte)(scan - PaethPredictor(left, above, upperLeft));
                sum += Numerics.Abs(unchecked((sbyte)res));
            }

            sum -= 4;
        }

        /// <summary>
        /// Computes a simple linear function of the three neighboring pixels (left, above, upper left), then chooses
        /// as predictor the neighboring pixel closest to the computed value.
        /// </summary>
        /// <param name="left">The left neighbor pixel.</param>
        /// <param name="above">The above neighbor pixel.</param>
        /// <param name="upperLeft">The upper left neighbor pixel.</param>
        /// <returns>
        /// The <see cref="byte"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte PaethPredictor(byte left, byte above, byte upperLeft)
        {
            int p = left + above - upperLeft;
            int pa = Numerics.Abs(p - left);
            int pb = Numerics.Abs(p - above);
            int pc = Numerics.Abs(p - upperLeft);

            if (pa <= pb && pa <= pc)
            {
                return left;
            }

            if (pb <= pc)
            {
                return above;
            }

            return upperLeft;
        }

        private static Vector<byte> PaethPredictor(Vector<byte> left, Vector<byte> above, Vector<byte> upperLeft)
        {
            Vector.Widen(left, out Vector<ushort> a1, out Vector<ushort> a2);
            Vector.Widen(above, out Vector<ushort> b1, out Vector<ushort> b2);
            Vector.Widen(upperLeft, out Vector<ushort> c1, out Vector<ushort> c2);

            Vector<short> p1 = PaethPredictor(Vector.AsVectorInt16(a1), Vector.AsVectorInt16(b1), Vector.AsVectorInt16(c1));
            Vector<short> p2 = PaethPredictor(Vector.AsVectorInt16(a2), Vector.AsVectorInt16(b2), Vector.AsVectorInt16(c2));
            return Vector.AsVectorByte(Vector.Narrow(p1, p2));
        }

        private static Vector<short> PaethPredictor(Vector<short> left, Vector<short> above, Vector<short> upperLeft)
        {
            Vector<short> p = left + above - upperLeft;
            var pa = Vector.Abs(p - left);
            var pb = Vector.Abs(p - above);
            var pc = Vector.Abs(p - upperLeft);

            var pa_pb = Vector.LessThanOrEqual(pa, pb);
            var pa_pc = Vector.LessThanOrEqual(pa, pc);
            var pb_pc = Vector.LessThanOrEqual(pb, pc);

            return Vector.ConditionalSelect(
                condition: Vector.BitwiseAnd(pa_pb, pa_pc),
                left: left,
                right: Vector.ConditionalSelect(
                    condition: pb_pc,
                    left: above,
                    right: upperLeft));
        }
    }
}
