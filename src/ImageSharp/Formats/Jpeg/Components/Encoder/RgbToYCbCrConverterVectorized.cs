// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder
{
    internal static class RgbToYCbCrConverterVectorized
    {
        public static bool IsSupported => Avx2.IsSupported;

        public static int AvxCompatibilityPadding
        {
            // rgb byte matrices contain 8 strides by 8 pixels each, thus 64 pixels total
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
            //
            // 8 byte padding to rgb byte span will solve this problem without extra code in converters
            get
            {
                if (IsSupported)
                {
                    return 8;
                }

                return 0;
            }
        }

        internal static ReadOnlySpan<byte> MoveFirst24BytesToSeparateLanes => new byte[]
        {
            0, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 6, 0, 0, 0,
            3, 0, 0, 0, 4, 0, 0, 0, 5, 0, 0, 0, 7, 0, 0, 0
        };

        internal static ReadOnlySpan<byte> ExtractRgb => new byte[]
        {
            0, 3, 6, 9, 1, 4, 7, 10, 2, 5, 8, 11, 0xFF, 0xFF, 0xFF, 0xFF,
            0, 3, 6, 9, 1, 4, 7, 10, 2, 5, 8, 11, 0xFF, 0xFF, 0xFF, 0xFF
        };

        /// <summary>
        /// Converts 8x8 Rgb24 pixel matrix to YCbCr pixel matrices with 4:4:4 subsampling
        /// </summary>
        /// <remarks>Total size of rgb span must be 200 bytes</remarks>
        /// <param name="rgbSpan">Span of rgb pixels with size of 64</param>
        /// <param name="yBlock">8x8 destination matrix of Luminance(Y) converted data</param>
        /// <param name="cbBlock">8x8 destination matrix of Chrominance(Cb) converted data</param>
        /// <param name="crBlock">8x8 destination matrix of Chrominance(Cr) converted data</param>
        public static void Convert444(ReadOnlySpan<Rgb24> rgbSpan, ref Block8x8F yBlock, ref Block8x8F cbBlock, ref Block8x8F crBlock)
        {
            Debug.Assert(IsSupported, "AVX2 is required to run this converter");

            var f0299 = Vector256.Create(0.299f);
            var f0587 = Vector256.Create(0.587f);
            var f0114 = Vector256.Create(0.114f);
            var fn0168736 = Vector256.Create(-0.168736f);
            var fn0331264 = Vector256.Create(-0.331264f);
            var f128 = Vector256.Create(128f);
            var fn0418688 = Vector256.Create(-0.418688f);
            var fn0081312F = Vector256.Create(-0.081312F);
            var f05 = Vector256.Create(0.5f);
            Vector256<byte> zero = Vector256.Create(0).AsByte();

            ref Vector256<byte> rgbByteSpan = ref Unsafe.As<Rgb24, Vector256<byte>>(ref MemoryMarshal.GetReference(rgbSpan));
            ref Vector256<float> destYRef = ref yBlock.V0;
            ref Vector256<float> destCbRef = ref cbBlock.V0;
            ref Vector256<float> destCrRef = ref crBlock.V0;

            Vector256<uint> extractToLanesMask = Unsafe.As<byte, Vector256<uint>>(ref MemoryMarshal.GetReference(MoveFirst24BytesToSeparateLanes));
            Vector256<byte> extractRgbMask = Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(ExtractRgb));
            Vector256<byte> rgb, rg, bx;
            Vector256<float> r, g, b;

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
        }

        /// <summary>
        /// Converts 16x8 Rgb24 pixels matrix to 2 Y 8x8 matrices with 4:2:0 subsampling
        /// </summary>
        public static void Convert420(ReadOnlySpan<Rgb24> rgbSpan, ref Block8x8F yBlockLeft, ref Block8x8F yBlockRight, ref Block8x8F cbBlock, ref Block8x8F crBlock, int row)
        {
            Debug.Assert(IsSupported, "AVX2 is required to run this converter");

            var f0299 = Vector256.Create(0.299f);
            var f0587 = Vector256.Create(0.587f);
            var f0114 = Vector256.Create(0.114f);
            var fn0168736 = Vector256.Create(-0.168736f);
            var fn0331264 = Vector256.Create(-0.331264f);
            var f128 = Vector256.Create(128f);
            var fn0418688 = Vector256.Create(-0.418688f);
            var fn0081312F = Vector256.Create(-0.081312F);
            var f05 = Vector256.Create(0.5f);
            Vector256<byte> zero = Vector256.Create(0).AsByte();

            ref Vector256<byte> rgbByteSpan = ref Unsafe.As<Rgb24, Vector256<byte>>(ref MemoryMarshal.GetReference(rgbSpan));

            int destOffset = row * 4;

            ref Vector256<float> destCbRef = ref Unsafe.Add(ref Unsafe.As<Block8x8F, Vector256<float>>(ref cbBlock), destOffset);
            ref Vector256<float> destCrRef = ref Unsafe.Add(ref Unsafe.As<Block8x8F, Vector256<float>>(ref crBlock), destOffset);

            Vector256<uint> extractToLanesMask = Unsafe.As<byte, Vector256<uint>>(ref MemoryMarshal.GetReference(MoveFirst24BytesToSeparateLanes));
            Vector256<byte> extractRgbMask = Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(ExtractRgb));
            Vector256<byte> rgb, rg, bx;
            Vector256<float> r, g, b;

            Span<Vector256<float>> rDataLanes = stackalloc Vector256<float>[4];
            Span<Vector256<float>> gDataLanes = stackalloc Vector256<float>[4];
            Span<Vector256<float>> bDataLanes = stackalloc Vector256<float>[4];

            const int bytesPerRgbStride = 24;
            for (int i = 0; i < 4; i++)
            {
                // 16x2 => 8x1
                // left 8x8 column conversions
                for (int j = 0; j < 4; j += 2)
                {
                    rgb = Avx2.PermuteVar8x32(Unsafe.AddByteOffset(ref rgbByteSpan, (IntPtr)(bytesPerRgbStride * ((i * 4) + j))).AsUInt32(), extractToLanesMask).AsByte();

                    rgb = Avx2.Shuffle(rgb, extractRgbMask);

                    rg = Avx2.UnpackLow(rgb, zero);
                    bx = Avx2.UnpackHigh(rgb, zero);

                    r = Avx.ConvertToVector256Single(Avx2.UnpackLow(rg, zero).AsInt32());
                    g = Avx.ConvertToVector256Single(Avx2.UnpackHigh(rg, zero).AsInt32());
                    b = Avx.ConvertToVector256Single(Avx2.UnpackLow(bx, zero).AsInt32());

                    int yBlockVerticalOffset = (i * 2) + ((j & 2) >> 1);

                    // (0.299F * r) + (0.587F * g) + (0.114F * b);
                    Unsafe.Add(ref yBlockLeft.V0, yBlockVerticalOffset) = SimdUtils.HwIntrinsics.MultiplyAdd(SimdUtils.HwIntrinsics.MultiplyAdd(Avx.Multiply(f0114, b), f0587, g), f0299, r);

                    rDataLanes[j] = r;
                    gDataLanes[j] = g;
                    bDataLanes[j] = b;
                }

                // 16x2 => 8x1
                // right 8x8 column conversions
                for (int j = 1; j < 4; j += 2)
                {
                    rgb = Avx2.PermuteVar8x32(Unsafe.AddByteOffset(ref rgbByteSpan, (IntPtr)(bytesPerRgbStride * ((i * 4) + j))).AsUInt32(), extractToLanesMask).AsByte();

                    rgb = Avx2.Shuffle(rgb, extractRgbMask);

                    rg = Avx2.UnpackLow(rgb, zero);
                    bx = Avx2.UnpackHigh(rgb, zero);

                    r = Avx.ConvertToVector256Single(Avx2.UnpackLow(rg, zero).AsInt32());
                    g = Avx.ConvertToVector256Single(Avx2.UnpackHigh(rg, zero).AsInt32());
                    b = Avx.ConvertToVector256Single(Avx2.UnpackLow(bx, zero).AsInt32());

                    int yBlockVerticalOffset = (i * 2) + ((j & 2) >> 1);

                    // (0.299F * r) + (0.587F * g) + (0.114F * b);
                    Unsafe.Add(ref yBlockRight.V0, yBlockVerticalOffset) = SimdUtils.HwIntrinsics.MultiplyAdd(SimdUtils.HwIntrinsics.MultiplyAdd(Avx.Multiply(f0114, b), f0587, g), f0299, r);

                    rDataLanes[j] = r;
                    gDataLanes[j] = g;
                    bDataLanes[j] = b;
                }

                r = Scale16x2_8x1(rDataLanes);
                g = Scale16x2_8x1(gDataLanes);
                b = Scale16x2_8x1(bDataLanes);

                // 128F + ((-0.168736F * r) - (0.331264F * g) + (0.5F * b))
                Unsafe.Add(ref destCbRef, i) = Avx.Add(f128, SimdUtils.HwIntrinsics.MultiplyAdd(SimdUtils.HwIntrinsics.MultiplyAdd(Avx.Multiply(f05, b), fn0331264, g), fn0168736, r));

                // 128F + ((0.5F * r) - (0.418688F * g) - (0.081312F * b))
                Unsafe.Add(ref destCrRef, i) = Avx.Add(f128, SimdUtils.HwIntrinsics.MultiplyAdd(SimdUtils.HwIntrinsics.MultiplyAdd(Avx.Multiply(fn0081312F, b), fn0418688, g), f05, r));
            }
        }

        /// <summary>
        /// Scales 16x2 matrix to 8x1 using 2x2 average
        /// </summary>
        /// <param name="v">Input matrix consisting of 4 256bit vectors</param>
        /// <returns>256bit vector containing upper and lower scaled parts of the input matrix</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<float> Scale16x2_8x1(ReadOnlySpan<Vector256<float>> v)
        {
            Debug.Assert(Avx2.IsSupported, "AVX2 is required to run this converter");
            DebugGuard.IsTrue(v.Length == 4, "Input span must consist of 4 elements");

            var f025 = Vector256.Create(0.25f);

            Vector256<float> left = Avx.Add(v[0], v[2]);
            Vector256<float> right = Avx.Add(v[1], v[3]);
            Vector256<float> avg2x2 = Avx.Multiply(Avx.HorizontalAdd(left, right), f025);

            return Avx2.Permute4x64(avg2x2.AsDouble(), 0b11_01_10_00).AsSingle();
        }
    }
}
