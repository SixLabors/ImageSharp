// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder
{
    internal static class RgbToYCbCrConverterVectorized
    {
        private static ReadOnlySpan<byte> ExtractionMasks => new byte[]
        {
            0x0, 0xFF, 0xFF, 0xFF, 0x1, 0xFF, 0xFF, 0xFF, 0x2, 0xFF, 0xFF, 0xFF, 0x3, 0xFF, 0xFF, 0xFF,   0x10, 0xFF, 0xFF, 0xFF, 0x11, 0xFF, 0xFF, 0xFF, 0x12, 0xFF, 0xFF, 0xFF, 0x13, 0xFF, 0xFF, 0xFF,
            0x4, 0xFF, 0xFF, 0xFF, 0x5, 0xFF, 0xFF, 0xFF, 0x6, 0xFF, 0xFF, 0xFF, 0x7, 0xFF, 0xFF, 0xFF,   0x14, 0xFF, 0xFF, 0xFF, 0x15, 0xFF, 0xFF, 0xFF, 0x16, 0xFF, 0xFF, 0xFF, 0x17, 0xFF, 0xFF, 0xFF,
            0x8, 0xFF, 0xFF, 0xFF, 0x9, 0xFF, 0xFF, 0xFF, 0xA, 0xFF, 0xFF, 0xFF, 0xB, 0xFF, 0xFF, 0xFF,   0x18, 0xFF, 0xFF, 0xFF, 0x19, 0xFF, 0xFF, 0xFF, 0x1A, 0xFF, 0xFF, 0xFF, 0x1B, 0xFF, 0xFF, 0xFF,
            0xC, 0xFF, 0xFF, 0xFF, 0xD, 0xFF, 0xFF, 0xFF, 0xE, 0xFF, 0xFF, 0xFF, 0xF, 0xFF, 0xFF, 0xFF,   0x1C, 0xFF, 0xFF, 0xFF, 0x1D, 0xFF, 0xFF, 0xFF, 0x1E, 0xFF, 0xFF, 0xFF, 0x1F, 0xFF, 0xFF, 0xFF,
        };

        public static bool IsSupported
        {
            get
            {
#if SUPPORTS_RUNTIME_INTRINSICS
                return Avx2.IsSupported && Fma.IsSupported;
#else
                return false;
#endif
            }
        }

        public static void Convert(ReadOnlySpan<Rgb24> rgbSpan, ref Block8x8F yBlock, ref Block8x8F cbBlock, ref Block8x8F crBlock)
        {
            Debug.Assert(IsSupported, "AVX2 and FMA are required to run this converter");

#if SUPPORTS_RUNTIME_INTRINSICS
            SeparateRgb(rgbSpan);
            ConvertInternal(rgbSpan, ref yBlock, ref cbBlock, ref crBlock);
#endif
        }

#if SUPPORTS_RUNTIME_INTRINSICS
        /// <summary>
        /// Rearranges the provided <paramref name="rgbSpan"/> in-place
        /// from { r00, g00, b00, ..., r63, g63, b63 }
        /// to { r00, ... r31, g00, ..., g31, b00, ..., b31,
        ///      r32, ... r63, g32, ..., g63, b31, ..., b63 }
        /// </summary>
        /// <remarks>
        /// SSE is used for this operation as it is significantly faster than AVX in this specific case.
        /// Solving this problem with AVX requires too many instructions that cross the 128-bit lanes of YMM registers.
        /// </remarks>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static void SeparateRgb(ReadOnlySpan<Rgb24> rgbSpan)
        {
            var selectRed0 = Vector128.Create(0x00, 0x03, 0x06, 0x09, 0x0C, 0x0F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF);
            var selectRed1 = Vector128.Create(0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x02, 0x05, 0x08, 0x0B, 0x0E, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF);
            var selectRed2 = Vector128.Create(0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01, 0x04, 0x07, 0x0A, 0x0D);

            var selectGreen0 = Vector128.Create(0x01, 0x04, 0x07, 0x0A, 0x0D, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF);
            var selectGreen1 = Vector128.Create(0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x03, 0x06, 0x09, 0x0C, 0x0F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF);
            var selectGreen2 = Vector128.Create(0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x02, 0x05, 0x08, 0x0B, 0x0E);

            var selectBlue0 = Vector128.Create(0x02, 0x05, 0x08, 0x0B, 0x0E, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF);
            var selectBlue1 = Vector128.Create(0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01, 0x04, 0x07, 0x0A, 0x0D, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF);
            var selectBlue2 = Vector128.Create(0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x03, 0x06, 0x09, 0x0C, 0x0F);

            for (int i = 0; i < 2; i++)
            {
                ref Vector128<byte> inRef = ref Unsafe.Add(ref Unsafe.As<Rgb24, Vector128<byte>>(ref MemoryMarshal.GetReference(rgbSpan)), i * 6);

                Vector128<byte> in0 = inRef;
                Vector128<byte> in1 = Unsafe.Add(ref inRef, 1);
                Vector128<byte> in2 = Unsafe.Add(ref inRef, 2);

                Vector128<byte> r0 = Sse2.Or(Sse2.Or(Ssse3.Shuffle(in0, selectRed0), Ssse3.Shuffle(in1, selectRed1)), Ssse3.Shuffle(in2, selectRed2));
                Vector128<byte> g0 = Sse2.Or(Sse2.Or(Ssse3.Shuffle(in0, selectGreen0), Ssse3.Shuffle(in1, selectGreen1)), Ssse3.Shuffle(in2, selectGreen2));
                Vector128<byte> b0 = Sse2.Or(Sse2.Or(Ssse3.Shuffle(in0, selectBlue0), Ssse3.Shuffle(in1, selectBlue1)), Ssse3.Shuffle(in2, selectBlue2));

                in0 = Unsafe.Add(ref inRef, 3);
                in1 = Unsafe.Add(ref inRef, 4);
                in2 = Unsafe.Add(ref inRef, 5);

                Vector128<byte> r1 = Sse2.Or(Sse2.Or(Ssse3.Shuffle(in0, selectRed0), Ssse3.Shuffle(in1, selectRed1)), Ssse3.Shuffle(in2, selectRed2));
                Vector128<byte> g1 = Sse2.Or(Sse2.Or(Ssse3.Shuffle(in0, selectGreen0), Ssse3.Shuffle(in1, selectGreen1)), Ssse3.Shuffle(in2, selectGreen2));
                Vector128<byte> b1 = Sse2.Or(Sse2.Or(Ssse3.Shuffle(in0, selectBlue0), Ssse3.Shuffle(in1, selectBlue1)), Ssse3.Shuffle(in2, selectBlue2));

                inRef = r0;
                Unsafe.Add(ref inRef, 1) = r1;
                Unsafe.Add(ref inRef, 2) = g0;
                Unsafe.Add(ref inRef, 3) = g1;
                Unsafe.Add(ref inRef, 4) = b0;
                Unsafe.Add(ref inRef, 5) = b1;
            }
        }

        /// <summary>
        /// Converts the previously separated (see <see cref="SeparateRgb"/>) RGB values to YCbCr using AVX2 and FMA.
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static void ConvertInternal(ReadOnlySpan<Rgb24> rgbSpan, ref Block8x8F yBlock, ref Block8x8F cbBlock, ref Block8x8F crBlock)
        {
            var f0299 = Vector256.Create(0.299f);
            var f0587 = Vector256.Create(0.587f);
            var f0114 = Vector256.Create(0.114f);
            var fn0168736 = Vector256.Create(-0.168736f);
            var fn0331264 = Vector256.Create(-0.331264f);
            var f128 = Vector256.Create(128f);
            var fn0418688 = Vector256.Create(-0.418688f);
            var fn0081312F = Vector256.Create(-0.081312F);
            var f05 = Vector256.Create(0.5f);

            ref Vector256<byte> inRef = ref Unsafe.As<Rgb24, Vector256<byte>>(ref MemoryMarshal.GetReference(rgbSpan));

            for (int i = 0; i < 2; i++)
            {
                ref Vector256<float> destYRef = ref Unsafe.Add(ref Unsafe.As<Block8x8F, Vector256<float>>(ref yBlock), i * 4);
                ref Vector256<float> destCbRef = ref Unsafe.Add(ref Unsafe.As<Block8x8F, Vector256<float>>(ref cbBlock), i * 4);
                ref Vector256<float> destCrRef = ref Unsafe.Add(ref Unsafe.As<Block8x8F, Vector256<float>>(ref crBlock), i * 4);

                Vector256<byte> red = Unsafe.Add(ref inRef, i * 3);
                Vector256<byte> green = Unsafe.Add(ref inRef, (i * 3) + 1);
                Vector256<byte> blue = Unsafe.Add(ref inRef, (i * 3) + 2);

                for (int j = 0; j < 2; j++)
                {
                    // 1st part of unrolled loop
                    Vector256<byte> mask = Unsafe.Add(ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(ExtractionMasks)), j * 2);

                    Vector256<float> r = Avx.ConvertToVector256Single(Avx2.Shuffle(red, mask).AsInt32());
                    Vector256<float> g = Avx.ConvertToVector256Single(Avx2.Shuffle(green, mask).AsInt32());
                    Vector256<float> b = Avx.ConvertToVector256Single(Avx2.Shuffle(blue, mask).AsInt32());

                    // (0.299F * r) + (0.587F * g) + (0.114F * b);
                    Vector256<float> yy0 = Fma.MultiplyAdd(f0299, r, Fma.MultiplyAdd(f0587, g, Avx.Multiply(f0114, b)));

                    // 128F + ((-0.168736F * r) - (0.331264F * g) + (0.5F * b))
                    Vector256<float> cb0 = Avx.Add(f128, Fma.MultiplyAdd(fn0168736, r, Fma.MultiplyAdd(fn0331264, g, Avx.Multiply(f05, b))));

                    // 128F + ((0.5F * r) - (0.418688F * g) - (0.081312F * b))
                    Vector256<float> cr0 = Avx.Add(f128, Fma.MultiplyAdd(f05, r, Fma.MultiplyAdd(fn0418688, g, Avx.Multiply(fn0081312F, b))));

                    // 2nd part of unrolled loop
                    mask = Unsafe.Add(ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(ExtractionMasks)), (j * 2) + 1);

                    r = Avx.ConvertToVector256Single(Avx2.Shuffle(red, mask).AsInt32());
                    g = Avx.ConvertToVector256Single(Avx2.Shuffle(green, mask).AsInt32());
                    b = Avx.ConvertToVector256Single(Avx2.Shuffle(blue, mask).AsInt32());

                    // (0.299F * r) + (0.587F * g) + (0.114F * b);
                    Vector256<float> yy1 = Fma.MultiplyAdd(f0299, r, Fma.MultiplyAdd(f0587, g, Avx.Multiply(f0114, b)));

                    // 128F + ((-0.168736F * r) - (0.331264F * g) + (0.5F * b))
                    Vector256<float> cb1 = Avx.Add(f128, Fma.MultiplyAdd(fn0168736, r, Fma.MultiplyAdd(fn0331264, g, Avx.Multiply(f05, b))));

                    // 128F + ((0.5F * r) - (0.418688F * g) - (0.081312F * b))
                    Vector256<float> cr1 = Avx.Add(f128, Fma.MultiplyAdd(f05, r, Fma.MultiplyAdd(fn0418688, g, Avx.Multiply(fn0081312F, b))));

                    // store results from 1st and 2nd part
                    Vector256<float> tmpY = Avx.Permute2x128(yy0, yy1, 0b0010_0001);
                    Unsafe.Add(ref destYRef, j) = Avx.Blend(yy0, tmpY, 0b1111_0000);
                    Unsafe.Add(ref destYRef, j + 2) = Avx.Blend(yy1, tmpY, 0b0000_1111);

                    Vector256<float> tmpCb = Avx.Permute2x128(cb0, cb1, 0b0010_0001);
                    Unsafe.Add(ref destCbRef, j) = Avx.Blend(cb0, tmpCb, 0b1111_0000);
                    Unsafe.Add(ref destCbRef, j + 2) = Avx.Blend(cb1, tmpCb, 0b0000_1111);

                    Vector256<float> tmpCr = Avx.Permute2x128(cr0, cr1, 0b0010_0001);
                    Unsafe.Add(ref destCrRef, j) = Avx.Blend(cr0, tmpCr, 0b1111_0000);
                    Unsafe.Add(ref destCrRef, j + 2) = Avx.Blend(cr1, tmpCr, 0b0000_1111);
                }
            }
        }
#endif
    }
}
