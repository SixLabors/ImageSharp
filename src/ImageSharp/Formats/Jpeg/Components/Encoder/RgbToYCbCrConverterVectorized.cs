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
        public static bool IsSupported
        {
            get
            {
#if SUPPORTS_RUNTIME_INTRINSICS
                return Avx2.IsSupported;
#else
                return false;
#endif
            }
        }

#if SUPPORTS_RUNTIME_INTRINSICS
        private static ReadOnlySpan<byte> MoveFirst24BytesToSeparateLanes => new byte[]
        {
            0, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 6, 0, 0, 0,
            3, 0, 0, 0, 4, 0, 0, 0, 5, 0, 0, 0, 7, 0, 0, 0
        };

        private static ReadOnlySpan<byte> MoveLast24BytesToSeparateLanes => new byte[]
        {
            2, 0, 0, 0, 3, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0,
            5, 0, 0, 0, 6, 0, 0, 0, 7, 0, 0, 0, 1, 0, 0, 0
        };

        private static ReadOnlySpan<byte> ExtractRgb => new byte[]
        {
            0, 3, 6, 9, 1, 4, 7, 10, 2, 5, 8, 11, 0xFF, 0xFF, 0xFF, 0xFF,
            0, 3, 6, 9, 1, 4, 7, 10, 2, 5, 8, 11, 0xFF, 0xFF, 0xFF, 0xFF
        };
#endif

        public static void Convert(ReadOnlySpan<Rgb24> rgbSpan, ref Block8x8F yBlock, ref Block8x8F cbBlock, ref Block8x8F crBlock)
        {
            Debug.Assert(IsSupported, "AVX2 is required to run this converter");

#if SUPPORTS_RUNTIME_INTRINSICS
            var f0299 = Vector256.Create(0.299f);
            var f0587 = Vector256.Create(0.587f);
            var f0114 = Vector256.Create(0.114f);
            var fn0168736 = Vector256.Create(-0.168736f);
            var fn0331264 = Vector256.Create(-0.331264f);
            var f128 = Vector256.Create(128f);
            var fn0418688 = Vector256.Create(-0.418688f);
            var fn0081312F = Vector256.Create(-0.081312F);
            var f05 = Vector256.Create(0.5f);
            var zero = Vector256.Create(0).AsByte();

            ref Vector256<byte> inRef = ref Unsafe.As<Rgb24, Vector256<byte>>(ref MemoryMarshal.GetReference(rgbSpan));
            ref Vector256<float> destYRef = ref Unsafe.As<Block8x8F, Vector256<float>>(ref yBlock);
            ref Vector256<float> destCbRef = ref Unsafe.As<Block8x8F, Vector256<float>>(ref cbBlock);
            ref Vector256<float> destCrRef = ref Unsafe.As<Block8x8F, Vector256<float>>(ref crBlock);

            var extractToLanesMask = Unsafe.As<byte, Vector256<uint>>(ref MemoryMarshal.GetReference(MoveFirst24BytesToSeparateLanes));
            var extractRgbMask = Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(ExtractRgb));
            Vector256<byte> rgb, rg, bx;
            Vector256<float> r, g, b;
            for (int i = 0; i < 7; i++)
            {
                rgb = Avx2.PermuteVar8x32(Unsafe.AddByteOffset(ref inRef, (IntPtr)(24 * i)).AsUInt32(), extractToLanesMask).AsByte();

                rgb = Avx2.Shuffle(rgb, extractRgbMask);

                rg = Avx2.UnpackLow(rgb, zero);
                bx = Avx2.UnpackHigh(rgb, zero);

                r = Avx.ConvertToVector256Single(Avx2.UnpackLow(rg, zero).AsInt32());
                g = Avx.ConvertToVector256Single(Avx2.UnpackHigh(rg, zero).AsInt32());
                b = Avx.ConvertToVector256Single(Avx2.UnpackLow(bx, zero).AsInt32());

                // (0.299F * r) + (0.587F * g) + (0.114F * b);
                Unsafe.Add(ref destYRef, i) = SimdUtils.HwIntrinsics.MultiplyAdd(SimdUtils.HwIntrinsics.MultiplyAdd(Avx.Multiply(f0114, b), f0587, g), f0299, r);

                // 128F + ((-0.168736F * r) - (0.331264F * g) + (0.5F * b))
                Unsafe.Add(ref destCbRef, i) = Avx.Add(f128, SimdUtils.HwIntrinsics.MultiplyAdd(SimdUtils.HwIntrinsics.MultiplyAdd(Avx.Multiply(f05, b), fn0331264, g), fn0168736, r));

                // 128F + ((0.5F * r) - (0.418688F * g) - (0.081312F * b))
                Unsafe.Add(ref destCrRef, i) = Avx.Add(f128, SimdUtils.HwIntrinsics.MultiplyAdd(SimdUtils.HwIntrinsics.MultiplyAdd(Avx.Multiply(fn0081312F, b), fn0418688, g), f05, r));
            }

            extractToLanesMask = Unsafe.As<byte, Vector256<uint>>(ref MemoryMarshal.GetReference(MoveLast24BytesToSeparateLanes));
            rgb = Avx2.PermuteVar8x32(Unsafe.AddByteOffset(ref inRef, (IntPtr)160).AsUInt32(), extractToLanesMask).AsByte();
            rgb = Avx2.Shuffle(rgb, extractRgbMask);

            rg = Avx2.UnpackLow(rgb, zero);
            bx = Avx2.UnpackHigh(rgb, zero);

            r = Avx.ConvertToVector256Single(Avx2.UnpackLow(rg, zero).AsInt32());
            g = Avx.ConvertToVector256Single(Avx2.UnpackHigh(rg, zero).AsInt32());
            b = Avx.ConvertToVector256Single(Avx2.UnpackLow(bx, zero).AsInt32());

            // (0.299F * r) + (0.587F * g) + (0.114F * b);
            Unsafe.Add(ref destYRef, 7) = SimdUtils.HwIntrinsics.MultiplyAdd(SimdUtils.HwIntrinsics.MultiplyAdd(Avx.Multiply(f0114, b), f0587, g), f0299, r);

            // 128F + ((-0.168736F * r) - (0.331264F * g) + (0.5F * b))
            Unsafe.Add(ref destCbRef, 7) = Avx.Add(f128, SimdUtils.HwIntrinsics.MultiplyAdd(SimdUtils.HwIntrinsics.MultiplyAdd(Avx.Multiply(f05, b), fn0331264, g), fn0168736, r));

            // 128F + ((0.5F * r) - (0.418688F * g) - (0.081312F * b))
            Unsafe.Add(ref destCrRef, 7) = Avx.Add(f128, SimdUtils.HwIntrinsics.MultiplyAdd(SimdUtils.HwIntrinsics.MultiplyAdd(Avx.Multiply(fn0081312F, b), fn0418688, g), f05, r));
#endif
        }
    }
}
