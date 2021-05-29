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

        private static ReadOnlySpan<byte> ExtractRgb => new byte[]
        {
            0, 3, 6, 9, 1, 4, 7, 10, 2, 5, 8, 11, 0xFF, 0xFF, 0xFF, 0xFF,
            0, 3, 6, 9, 1, 4, 7, 10, 2, 5, 8, 11, 0xFF, 0xFF, 0xFF, 0xFF
        };
#endif

        /// <summary>
        /// Converts 8x8 Rgb24 pixel matrix to YCbCr pixel matrices with 4:4:4 subsampling
        /// </summary>
        /// <remarks>Total size of rgb span must be 200 bytes</remarks>
        /// <param name="rgbSpan">Span of rgb pixels with size of 64</param>
        /// <param name="yBlock">8x8 destination matrix of Luminance(Y) converted data</param>
        /// <param name="cbBlock">8x8 destination matrix of Chrominance(Cb) converted data</param>
        /// <param name="crBlock">8x8 destination matrix of Chrominance(Cr) converted data</param>
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

            ref Vector256<byte> rgbByteSpan = ref Unsafe.As<Rgb24, Vector256<byte>>(ref MemoryMarshal.GetReference(rgbSpan));
            ref Vector256<float> destYRef = ref yBlock.V0;
            ref Vector256<float> destCbRef = ref cbBlock.V0;
            ref Vector256<float> destCrRef = ref crBlock.V0;

            var extractToLanesMask = Unsafe.As<byte, Vector256<uint>>(ref MemoryMarshal.GetReference(MoveFirst24BytesToSeparateLanes));
            var extractRgbMask = Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(ExtractRgb));
            Vector256<byte> rgb, rg, bx;
            Vector256<float> r, g, b;

            // TODO: probably remove this after the draft
            // rgbByteSpan contains 8 strides by 8 pixels each, thus 64 pixels total
            // Strides are stored sequentially - one big span of 64 * 3 = 192 bytes
            // Each stride has exactly 3 * 8 = 24 bytes or 3 * 8 * 8 = 192 bits
            // Avx registers are 256 bits so rgb span will be loaded with extra 64 bits from the next stride:
            // stride 0    0    - 192  -(+64bits)-> 256
            // stride 1    192  - 384  -(+64bits)-> 448
            // stride 2    384  - 576  -(+64bits)-> 640
            // stride 3    576  - 768  -(+64bits)-> 832
            // stride 4    768  - 960  -(+64bits)-> 1024
            // stride 5    960  - 1152 -(+64bits)-> 1216
            // stride 6    1152 - 1344 -(+64bits)-> 1408
            // stride 7    1344 - 1536 -(+64bits)-> 1600 <-- READ ACCESS VIOLATION
            //
            // Total size of the 64 pixel rgb span: 64 * 3 * 8 = 1536 bits, avx operations require 1600 bits
            // This is not permitted - we are reading foreign memory
            // That's why last stride is calculated outside of the for-loop loop with special extract shuffle mask involved
            //
            // Extra mask & separate stride:7 calculations can be eliminated by simply providing rgb pixel span of slightly bigger size than pixels data need:
            // Total pixel data size is 192 bytes, avx registers need it to be 200 bytes
            const int bytesPerRgbStride = 24;
            for (int i = 0; i < 8; i++)
            {
                rgb = Avx2.PermuteVar8x32(Unsafe.AddByteOffset(ref rgbByteSpan, (IntPtr)(bytesPerRgbStride * i)).AsUInt32(), extractToLanesMask).AsByte();

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
#endif
        }

        /// <summary>
        /// Converts 8x8 Rgb24 pixel matrix to YCbCr pixel matrices with 4:2:0 subsampling
        /// </summary>
        /// <remarks>Total size of rgb span must be 200 bytes</remarks>
        /// <param name="rgbSpan">Span of rgb pixels with size of 64</param>
        /// <param name="yBlock">8x8 destination matrix of Luminance(Y) converted data</param>ф
        public static void Convert420(ReadOnlySpan<Rgb24> rgbSpan, ref Block8x8F yBlock, ref Block8x8F cbBlock, ref Block8x8F crBlock, int idx)
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

            ref Vector256<byte> rgbByteSpan = ref Unsafe.As<Rgb24, Vector256<byte>>(ref MemoryMarshal.GetReference(rgbSpan));
            ref Vector256<float> destYRef = ref yBlock.V0;

            int destOffset = (idx & 2) * 4 + (idx & 1);

            ref Vector128<float> destCbRef = ref Unsafe.Add(ref Unsafe.As<Block8x8F, Vector128<float>>(ref cbBlock), destOffset);
            ref Vector128<float> destCrRef = ref Unsafe.Add(ref Unsafe.As<Block8x8F, Vector128<float>>(ref crBlock), destOffset);

            var extractToLanesMask = Unsafe.As<byte, Vector256<uint>>(ref MemoryMarshal.GetReference(MoveFirst24BytesToSeparateLanes));
            var extractRgbMask = Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(ExtractRgb));
            Vector256<byte> rgb, rg, bx;
            Vector256<float> r, g, b;

            Span<Vector256<float>> rDataLanes = stackalloc Vector256<float>[4];
            Span<Vector256<float>> gDataLanes = stackalloc Vector256<float>[4];
            Span<Vector256<float>> bDataLanes = stackalloc Vector256<float>[4];

            const int bytesPerRgbStride = 24;
            for (int i = 0; i < 2; i++)
            {
                // each 4 lanes - [0, 1, 2, 3] & [4, 5, 6, 7]
                for (int j = 0; j < 4; j++)
                {
                    rgb = Avx2.PermuteVar8x32(Unsafe.AddByteOffset(ref rgbByteSpan, (IntPtr)(bytesPerRgbStride * (i * 4 + j))).AsUInt32(), extractToLanesMask).AsByte();

                    rgb = Avx2.Shuffle(rgb, extractRgbMask);

                    rg = Avx2.UnpackLow(rgb, zero);
                    bx = Avx2.UnpackHigh(rgb, zero);

                    r = Avx.ConvertToVector256Single(Avx2.UnpackLow(rg, zero).AsInt32());
                    g = Avx.ConvertToVector256Single(Avx2.UnpackHigh(rg, zero).AsInt32());
                    b = Avx.ConvertToVector256Single(Avx2.UnpackLow(bx, zero).AsInt32());

                    // (0.299F * r) + (0.587F * g) + (0.114F * b);
                    Unsafe.Add(ref destYRef, i * 4 + j) = SimdUtils.HwIntrinsics.MultiplyAdd(SimdUtils.HwIntrinsics.MultiplyAdd(Avx.Multiply(f0114, b), f0587, g), f0299, r);

                    rDataLanes[j] = r;
                    gDataLanes[j] = g;
                    bDataLanes[j] = b;
                }

                int localDestOffset = (i & 1) * 4;

                r = Scale_8x4_4x2(rDataLanes);
                g = Scale_8x4_4x2(gDataLanes);
                b = Scale_8x4_4x2(bDataLanes);

                // 128F + ((-0.168736F * r) - (0.331264F * g) + (0.5F * b))
                Vector256<float> cb = Avx.Add(f128, SimdUtils.HwIntrinsics.MultiplyAdd(SimdUtils.HwIntrinsics.MultiplyAdd(Avx.Multiply(f05, b), fn0331264, g), fn0168736, r));
                Unsafe.Add(ref destCbRef, localDestOffset) = cb.GetLower();
                Unsafe.Add(ref destCbRef, localDestOffset + 2) = cb.GetUpper();

                // 128F + ((0.5F * r) - (0.418688F * g) - (0.081312F * b))
                Vector256<float> cr = Avx.Add(f128, SimdUtils.HwIntrinsics.MultiplyAdd(SimdUtils.HwIntrinsics.MultiplyAdd(Avx.Multiply(fn0081312F, b), fn0418688, g), f05, r));
                Unsafe.Add(ref destCrRef, localDestOffset) = cr.GetLower();
                Unsafe.Add(ref destCrRef, localDestOffset + 2) = cr.GetUpper();
            }
#endif
        }

        /// <summary>
        /// Converts 16x8 Rgb24 pixels matrix to 2 Y 8x8 matrices with 4:2:0 subsampling
        /// </summary>
        /// <param name="rgbSpan"></param>
        /// <param name="yBlock0"></param>
        /// <param name="yBlock1"></param>
        /// <param name="cbBlock"></param>
        /// <param name="crBlock"></param>
        /// <param name="row"></param>
        public static void Convert420_16x8(ReadOnlySpan<Rgb24> rgbSpan, Span<Block8x8F> yBlocks, ref Block8x8F cbBlock, ref Block8x8F crBlock, int row)
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

            ref Vector256<byte> rgbByteSpan = ref Unsafe.As<Rgb24, Vector256<byte>>(ref MemoryMarshal.GetReference(rgbSpan));

            int destOffset = row * 4;

            ref Vector256<float> destCbRef = ref Unsafe.Add(ref Unsafe.As<Block8x8F, Vector256<float>>(ref cbBlock), destOffset);
            ref Vector256<float> destCrRef = ref Unsafe.Add(ref Unsafe.As<Block8x8F, Vector256<float>>(ref crBlock), destOffset);

            var extractToLanesMask = Unsafe.As<byte, Vector256<uint>>(ref MemoryMarshal.GetReference(MoveFirst24BytesToSeparateLanes));
            var extractRgbMask = Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(ExtractRgb));
            Vector256<byte> rgb, rg, bx;
            Vector256<float> r, g, b;

            Span<Vector256<float>> rDataLanes = stackalloc Vector256<float>[4];
            Span<Vector256<float>> gDataLanes = stackalloc Vector256<float>[4];
            Span<Vector256<float>> bDataLanes = stackalloc Vector256<float>[4];

            const int bytesPerRgbStride = 24;
            for (int i = 0; i < 4; i++)
            {
                // 16x2 => 8x1
                for (int j = 0; j < 4; j++)
                {
                    rgb = Avx2.PermuteVar8x32(Unsafe.AddByteOffset(ref rgbByteSpan, (IntPtr)(bytesPerRgbStride * (i * 4 + j))).AsUInt32(), extractToLanesMask).AsByte();

                    rgb = Avx2.Shuffle(rgb, extractRgbMask);

                    rg = Avx2.UnpackLow(rgb, zero);
                    bx = Avx2.UnpackHigh(rgb, zero);

                    r = Avx.ConvertToVector256Single(Avx2.UnpackLow(rg, zero).AsInt32());
                    g = Avx.ConvertToVector256Single(Avx2.UnpackHigh(rg, zero).AsInt32());
                    b = Avx.ConvertToVector256Single(Avx2.UnpackLow(bx, zero).AsInt32());

                    int yBlockVerticalOffset = (i * 2) + ((j & 2) >> 1);

                    // (0.299F * r) + (0.587F * g) + (0.114F * b);
                    Unsafe.Add(ref yBlocks[j & 1].V0, yBlockVerticalOffset) = SimdUtils.HwIntrinsics.MultiplyAdd(SimdUtils.HwIntrinsics.MultiplyAdd(Avx.Multiply(f0114, b), f0587, g), f0299, r);

                    rDataLanes[j] = r;
                    gDataLanes[j] = g;
                    bDataLanes[j] = b;
                }

                r = Scale_8x4_4x2(rDataLanes);
                g = Scale_8x4_4x2(gDataLanes);
                b = Scale_8x4_4x2(bDataLanes);

                // 128F + ((-0.168736F * r) - (0.331264F * g) + (0.5F * b))
                Unsafe.Add(ref destCbRef, i) = Avx.Add(f128, SimdUtils.HwIntrinsics.MultiplyAdd(SimdUtils.HwIntrinsics.MultiplyAdd(Avx.Multiply(f05, b), fn0331264, g), fn0168736, r));

                // 128F + ((0.5F * r) - (0.418688F * g) - (0.081312F * b))
                Unsafe.Add(ref destCrRef, i) = Avx.Add(f128, SimdUtils.HwIntrinsics.MultiplyAdd(SimdUtils.HwIntrinsics.MultiplyAdd(Avx.Multiply(fn0081312F, b), fn0418688, g), f05, r));
            }
#endif
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> Scale_8x4_4x2(Span<Vector256<float>> v)
        {
            Vector256<int> switchInnerDoubleWords = Unsafe.As<byte, Vector256<int>>(ref MemoryMarshal.GetReference(SimdUtils.HwIntrinsics.PermuteMaskSwitchInnerDWords8x32));
            var f025 = Vector256.Create(0.25f);

            Vector256<float> topPairSum = SumHorizontalPairs(v[0], v[2]);
            Vector256<float> botPairSum = SumHorizontalPairs(v[1], v[3]);

            return Avx2.PermuteVar8x32(Avx.Multiply(SumVerticalPairs(topPairSum, botPairSum), f025), switchInnerDoubleWords);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> SumHorizontalPairs(Vector256<float> v0, Vector256<float> v1)
            => Avx.Add(Avx.Shuffle(v0, v1, 0b10_00_10_00), Avx.Shuffle(v0, v1, 0b11_01_11_01));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> SumVerticalPairs(Vector256<float> v0, Vector256<float> v1)
            => Avx.Add(Avx.Shuffle(v0, v1, 0b01_00_01_00), Avx.Shuffle(v0, v1, 0b11_10_11_10));

        public static void ConvertCbCr(ref Block8x8F rBlock, ref Block8x8F gBlock, ref Block8x8F bBlock, ref Block8x8F cbBlock, ref Block8x8F crBlock)
        {
            var fn0168736 = Vector256.Create(-0.168736f);
            var fn0331264 = Vector256.Create(-0.331264f);
            var f128 = Vector256.Create(128f);
            var fn0418688 = Vector256.Create(-0.418688f);
            var fn0081312F = Vector256.Create(-0.081312F);
            var f05 = Vector256.Create(0.5f);

            ref Vector256<float> destCbRef = ref cbBlock.V0;
            ref Vector256<float> destCrRef = ref crBlock.V0;

            ref Vector256<float> rRef = ref rBlock.V0;
            ref Vector256<float> gRef = ref gBlock.V0;
            ref Vector256<float> bRef = ref bBlock.V0;

            for (int i = 0; i < 8; i++)
            {
                ref Vector256<float> r = ref Unsafe.Add(ref rRef, i);
                ref Vector256<float> g = ref Unsafe.Add(ref gRef, i);
                ref Vector256<float> b = ref Unsafe.Add(ref bRef, i);

                // 128F + ((-0.168736F * r) - (0.331264F * g) + (0.5F * b))
                Unsafe.Add(ref destCbRef, i) = Avx.Add(f128, SimdUtils.HwIntrinsics.MultiplyAdd(SimdUtils.HwIntrinsics.MultiplyAdd(Avx.Multiply(f05, b), fn0331264, g), fn0168736, r));

                // 128F + ((0.5F * r) - (0.418688F * g) - (0.081312F * b))
                Unsafe.Add(ref destCrRef, i) = Avx.Add(f128, SimdUtils.HwIntrinsics.MultiplyAdd(SimdUtils.HwIntrinsics.MultiplyAdd(Avx.Multiply(fn0081312F, b), fn0418688, g), f05, r));
            }
        }
    }
}
