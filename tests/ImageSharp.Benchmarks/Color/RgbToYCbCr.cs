// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Formats.Jpeg.Components;

namespace SixLabors.ImageSharp.Benchmarks
{
    public partial class RgbToYCbCr
    {
        private const int InputColorCount = 64;

        private const int InputByteCount = InputColorCount * 3;

        private static readonly Vector3 VectorY = new Vector3(0.299F, 0.587F, 0.114F);

        private static readonly Vector3 VectorCb = new Vector3(-0.168736F, 0.331264F, 0.5F);

        private static readonly Vector3 VectorCr = new Vector3(0.5F, 0.418688F, 0.081312F);

        private static class ScaledCoeffs
        {
            public static readonly int[] Y =
                {
                    306, 601, 117, 0,
                    306, 601, 117, 0,
                };

            public static readonly int[] Cb =
                {
                    -172, 339, 512, 0,
                    -172, 339, 512, 0,
                };

            public static readonly int[] Cr =
                {
                    512, 429, 83, 0,
                    512, 429, 83, 0,
                };

            public static class SelectLeft
            {
                public static readonly int[] Y =
                {
                    1, 1, 1, 0,
                    0, 0, 0, 0,
                };

                public static readonly int[] Cb =
                {
                    1, -1, 1, 0,
                    0, 0, 0, 0,
                };

                public static readonly int[] Cr =
                {
                    1, -1, -1, 0,
                    0, 0, 0, 0,
                };
            }

            public static class SelectRight
            {
                public static readonly int[] Y =
                {
                    0, 0, 0, 0,
                    1, 1, 1, 0,
                };

                public static readonly int[] Cb =
                {
                    0, 0, 0, 0,
                    1, -1, 1, 0,
                };

                public static readonly int[] Cr =
                {
                    0, 0, 0, 0,
                    1, -1, -1, 0,
                };
            }
        }

        // Waiting for C# 7 stackalloc keyword patiently ...
        private static class OnStackInputCache
        {
            public unsafe struct Byte
            {
                public fixed byte Data[InputByteCount * 3];

                public static Byte Create(byte[] data)
                {
                    Byte result = default(Byte);
                    for (int i = 0; i < data.Length; i++)
                    {
                        result.Data[i] = data[i];
                    }
                    return result;
                }
            }
        }

        public struct Result
        {
            internal Block8x8F Y;
            internal Block8x8F Cb;
            internal Block8x8F Cr;
        }

        // The operation is defined as "RGBA -> YCbCr Transform a stream of bytes into a stream of floats"
        // We need to benchmark the whole operation, to get true results, not missing any side effects!
        private byte[] inputSourceRGB = null;

        private int[] inputSourceRGBAsInteger = null;

        [GlobalSetup]
        public void Setup()
        {
            // Console.WriteLine("Vector<int>.Count: " + Vector<int>.Count);
            this.inputSourceRGB = new byte[InputByteCount];
            for (int i = 0; i < this.inputSourceRGB.Length; i++)
            {
                this.inputSourceRGB[i] = (byte)(42 + i);
            }
            this.inputSourceRGBAsInteger = new int[InputByteCount + Vector<int>.Count]; // Filling this should be part of the measured operation
        }

        [Benchmark(Baseline = true, Description = "Floating Point Conversion")]
        public unsafe void RgbaToYcbCrScalarFloat()
        {
            // Copy the input to the stack:
            OnStackInputCache.Byte input = OnStackInputCache.Byte.Create(this.inputSourceRGB);

            // On-stack output:
            Result result = default(Result);
            float* yPtr = (float*)&result.Y;
            float* cbPtr = (float*)&result.Cb;
            float* crPtr = (float*)&result.Cr;
            // end of code-bloat block :)

            for (int i = 0; i < InputColorCount; i++)
            {
                int i3 = i * 3;
                float r = input.Data[i3 + 0];
                float g = input.Data[i3 + 1];
                float b = input.Data[i3 + 2];

                *yPtr++ = (0.299F * r) + (0.587F * g) + (0.114F * b);
                *cbPtr++ = 128 + ((-0.168736F * r) - (0.331264F * g) + (0.5F * b));
                *crPtr++ = 128 + ((0.5F * r) - (0.418688F * g) - (0.081312F * b));
            }
        }

        [Benchmark(Description = "Simd Floating Point Conversion")]
        public unsafe void RgbaToYcbCrSimdFloat()
        {
            // Copy the input to the stack:
            OnStackInputCache.Byte input = OnStackInputCache.Byte.Create(this.inputSourceRGB);

            // On-stack output:
            Result result = default(Result);
            float* yPtr = (float*)&result.Y;
            float* cbPtr = (float*)&result.Cb;
            float* crPtr = (float*)&result.Cr;
            // end of code-bloat block :)

            for (int i = 0; i < InputColorCount; i++)
            {
                int i3 = i * 3;

                Vector3 vectorRgb = new Vector3(
                    input.Data[i3 + 0],
                    input.Data[i3 + 1],
                    input.Data[i3 + 2]
                    );

                Vector3 vectorY = VectorY * vectorRgb;
                Vector3 vectorCb = VectorCb * vectorRgb;
                Vector3 vectorCr = VectorCr * vectorRgb;

                *yPtr++ = vectorY.X + vectorY.Y + vectorY.Z;
                *cbPtr++ = 128 + (vectorCb.X - vectorCb.Y + vectorCb.Z);
                *crPtr++ = 128 + (vectorCr.X - vectorCr.Y - vectorCr.Z);
            }
        }

        [Benchmark(Description = "Scaled Integer Conversion + Vector<int>")]
        public unsafe void RgbaToYcbCrScaledIntegerSimd()
        {
            // Copy the input to the stack:

            // On-stack output:
            Result result = default(Result);
            float* yPtr = (float*)&result.Y;
            float* cbPtr = (float*)&result.Cb;
            float* crPtr = (float*)&result.Cr;
            // end of code-bloat block :)

            Vector<int> yCoeffs = new Vector<int>(ScaledCoeffs.Y);
            Vector<int> cbCoeffs = new Vector<int>(ScaledCoeffs.Cb);
            Vector<int> crCoeffs = new Vector<int>(ScaledCoeffs.Cr);

            for (int i = 0; i < this.inputSourceRGB.Length; i++)
            {
                this.inputSourceRGBAsInteger[i] = this.inputSourceRGB[i];
            }

            for (int i = 0; i < InputColorCount; i += 2)
            {
                Vector<int> rgb = new Vector<int>(this.inputSourceRGBAsInteger, i * 3);

                Vector<int> y = yCoeffs * rgb;
                Vector<int> cb = cbCoeffs * rgb;
                Vector<int> cr = crCoeffs * rgb;

                *yPtr++ = (y[0] + y[1] + y[2]) >> 10;
                *cbPtr++ = 128 + ((cb[0] - cb[1] + cb[2]) >> 10);
                *crPtr++ = 128 + ((cr[0] - cr[1] - cr[2]) >> 10);

                *yPtr++ = (y[4] + y[5] + y[6]) >> 10;
                *cbPtr++ = 128 + ((cb[4] - cb[5] + cb[6]) >> 10);
                *crPtr++ = 128 + ((cr[4] - cr[5] - cr[6]) >> 10);
            }
        }

        /// <summary>
        /// This should perform better. Coreclr emmitted Vector.Dot() code lacks the vectorization even with IsHardwareAccelerated == true.
        /// Kept this benchmark because maybe it will be improved in a future CLR release.
        /// <see>
        ///     <cref>https://www.gamedev.net/topic/673396-c-systemnumericsvectors-slow/</cref>
        /// </see>
        /// </summary>
        [Benchmark(Description = "Scaled Integer Conversion + Vector<int> + Dot Product")]
        public unsafe void RgbaToYcbCrScaledIntegerSimdWithDotProduct()
        {
            // Copy the input to the stack:

            // On-stack output:
            Result result = default(Result);
            float* yPtr = (float*)&result.Y;
            float* cbPtr = (float*)&result.Cb;
            float* crPtr = (float*)&result.Cr;
            // end of code-bloat block :)

            Vector<int> yCoeffs = new Vector<int>(ScaledCoeffs.Y);
            Vector<int> cbCoeffs = new Vector<int>(ScaledCoeffs.Cb);
            Vector<int> crCoeffs = new Vector<int>(ScaledCoeffs.Cr);

            Vector<int> leftY = new Vector<int>(ScaledCoeffs.SelectLeft.Y);
            Vector<int> leftCb = new Vector<int>(ScaledCoeffs.SelectLeft.Cb);
            Vector<int> leftCr = new Vector<int>(ScaledCoeffs.SelectLeft.Cr);

            Vector<int> rightY = new Vector<int>(ScaledCoeffs.SelectRight.Y);
            Vector<int> rightCb = new Vector<int>(ScaledCoeffs.SelectRight.Cb);
            Vector<int> rightCr = new Vector<int>(ScaledCoeffs.SelectRight.Cr);

            for (int i = 0; i < this.inputSourceRGB.Length; i++)
            {
                this.inputSourceRGBAsInteger[i] = this.inputSourceRGB[i];
            }

            for (int i = 0; i < InputColorCount; i += 2)
            {
                Vector<int> rgb = new Vector<int>(this.inputSourceRGBAsInteger, i * 3);

                Vector<int> y = yCoeffs * rgb;
                Vector<int> cb = cbCoeffs * rgb;
                Vector<int> cr = crCoeffs * rgb;

                VectorizedConvertImpl(ref yPtr, ref cbPtr, ref crPtr, y, cb, cr, leftY, leftCb, leftCr);
                VectorizedConvertImpl(ref yPtr, ref cbPtr, ref crPtr, y, cb, cr, rightY, rightCb, rightCr);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void VectorizedConvertImpl(
            ref float* yPtr,
            ref float* cbPtr,
            ref float* crPtr,
            Vector<int> y,
            Vector<int> cb,
            Vector<int> cr,
            Vector<int> yAgg,
            Vector<int> cbAgg,
            Vector<int> crAgg)
        {
            int ySum = Vector.Dot(y, yAgg);
            int cbSum = Vector.Dot(cb, cbAgg);
            int crSum = Vector.Dot(cr, crAgg);
            *yPtr++ = ySum >> 10;
            *cbPtr++ = 128 + (cbSum >> 10);
            *crPtr++ = 128 + (crSum >> 10);
        }

        [Benchmark(Description = "Scaled Integer Conversion")]
        public unsafe void RgbaToYcbCrScaledInteger()
        {
            // Copy the input to the stack:
            OnStackInputCache.Byte input = OnStackInputCache.Byte.Create(this.inputSourceRGB);

            // On-stack output:
            Result result = default(Result);
            float* yPtr = (float*)&result.Y;
            float* cbPtr = (float*)&result.Cb;
            float* crPtr = (float*)&result.Cr;
            // end of code-bloat block :)

            for (int i = 0; i < InputColorCount; i++)
            {
                int i3 = i * 3;
                int r = input.Data[i3 + 0];
                int g = input.Data[i3 + 1];
                int b = input.Data[i3 + 2];

                // Scale by 1024, add .5F and truncate value
                int y0 = 306 * r; // (0.299F * 1024) + .5F
                int y1 = 601 * g; // (0.587F * 1024) + .5F
                int y2 = 117 * b; // (0.114F * 1024) + .5F

                int cb0 = -172 * r; // (-0.168736F * 1024) + .5F
                int cb1 = 339 * g; // (0.331264F * 1024) + .5F
                int cb2 = 512 * b; // (0.5F * 1024) + .5F

                int cr0 = 512 * r; // (0.5F * 1024) + .5F
                int cr1 = 429 * g; // (0.418688F * 1024) + .5F
                int cr2 = 83 * b; // (0.081312F * 1024) + .5F

                *yPtr++ = (y0 + y1 + y2) >> 10;
                *cbPtr++ = 128 + ((cb0 - cb1 + cb2) >> 10);
                *crPtr++ = 128 + ((cr0 - cr1 - cr2) >> 10);
            }
        }

        [Benchmark(Description = "Scaled Integer LUT Conversion")]
        public unsafe void RgbaToYcbCrScaledIntegerLut()
        {
            // Copy the input to the stack:
            OnStackInputCache.Byte input = OnStackInputCache.Byte.Create(this.inputSourceRGB);

            // On-stack output:
            Result result = default(Result);
            float* yPtr = (float*)&result.Y;
            float* cbPtr = (float*)&result.Cb;
            float* crPtr = (float*)&result.Cr;
            // end of code-bloat block :)

            for (int i = 0; i < InputColorCount; i++)
            {
                int i3 = i * 3;

                int r = input.Data[i3 + 0];
                int g = input.Data[i3 + 1];
                int b = input.Data[i3 + 2];

                // TODO: Maybe concatenating all the arrays in LookupTables to a flat one can improve this!
                *yPtr++ = (LookupTables.Y0[r] + LookupTables.Y1[g] + LookupTables.Y2[b]) >> 10;
                *cbPtr++ = 128 + ((LookupTables.Cb0[r] - LookupTables.Cb1[g] + LookupTables.Cb2Cr0[b]) >> 10);
                *crPtr++ = 128 + ((LookupTables.Cb2Cr0[r] - LookupTables.Cr1[g] - LookupTables.Cr2[b]) >> 10);
            }
        }
    }
}
