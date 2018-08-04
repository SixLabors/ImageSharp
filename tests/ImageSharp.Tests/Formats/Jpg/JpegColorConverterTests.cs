// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters;
using SixLabors.ImageSharp.Memory;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    public class JpegColorConverterTests
    {
        private const float Precision = 0.1f / 255;

        public static readonly TheoryData<int, int, int> CommonConversionData =
            new TheoryData<int, int, int>
                {
                    { 40, 40, 1 },
                    { 42, 40, 2 },
                    { 42, 39, 3 }
                };

        private static readonly ColorSpaceConverter ColorSpaceConverter = new ColorSpaceConverter();

        public JpegColorConverterTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void ConvertFromYCbCrBasic(int inputBufferLength, int resultBufferLength, int seed)
        {
            ValidateRgbToYCbCrConversion(
                new JpegColorConverter.FromYCbCrBasic(),
                3,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        private static void ValidateYCbCr(JpegColorConverter.ComponentValues values, Vector4[] result, int i)
        {
            float y = values.Component0[i];
            float cb = values.Component1[i];
            float cr = values.Component2[i];
            var ycbcr = new YCbCr(y, cb, cr);

            Vector4 rgba = result[i];
            var actual = new Rgb(rgba.X, rgba.Y, rgba.Z);
            var expected = ColorSpaceConverter.ToRgb(ycbcr);

            Assert.True(actual.AlmostEquals(expected, Precision), $"{actual} != {expected}");
            Assert.Equal(1, rgba.W);
        }

        [Theory]
        [InlineData(64, 1)]
        [InlineData(16, 2)]
        [InlineData(8, 3)]
        public void FromYCbCrSimd_ConvertCore(int size, int seed)
        {
            JpegColorConverter.ComponentValues values = CreateRandomValues(3, size, seed);
            var result = new Vector4[size];

            JpegColorConverter.FromYCbCrSimd.ConvertCore(values, result);

            for (int i = 0; i < size; i++)
            {
                ValidateYCbCr(values, result, i);
            }
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void FromYCbCrSimd(int inputBufferLength, int resultBufferLength, int seed)
        {
            ValidateRgbToYCbCrConversion(
                new JpegColorConverter.FromYCbCrSimd(),
                3,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void FromYCbCrSimdAvx2(int inputBufferLength, int resultBufferLength, int seed)
        {
            if (!SimdUtils.IsAvx2CompatibleArchitecture)
            {
                this.Output.WriteLine("No AVX2 present, skipping test!");
                return;
            }

            //JpegColorConverter.FromYCbCrSimdAvx2.LogPlz = s => this.Output.WriteLine(s);

            ValidateRgbToYCbCrConversion(
                new JpegColorConverter.FromYCbCrSimdAvx2(),
                3,
                inputBufferLength,
                resultBufferLength,
                seed);
        }


        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void ConvertFromYCbCr_WithDefaultConverter(int inputBufferLength, int resultBufferLength, int seed)
        {
            ValidateConversion(
                JpegColorSpace.YCbCr,
                3,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        // Benchmark, for local execution only
        //[Theory]
        //[InlineData(false)]
        //[InlineData(true)]
        public void BenchmarkYCbCr(bool simd)
        {
            int count = 2053;
            int times = 50000;

            JpegColorConverter.ComponentValues values = CreateRandomValues(3, count, 1);
            var result = new Vector4[count];

            JpegColorConverter converter = simd ? (JpegColorConverter)new JpegColorConverter.FromYCbCrSimd() : new JpegColorConverter.FromYCbCrBasic();

            // Warm up:
            converter.ConvertToRgba(values, result);

            using (new MeasureGuard(this.Output, $"{converter.GetType().Name} x {times}"))
            {
                for (int i = 0; i < times; i++)
                {
                    converter.ConvertToRgba(values, result);
                }
            }
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void ConvertFromCmyk(int inputBufferLength, int resultBufferLength, int seed)
        {
            var v = new Vector4(0, 0, 0, 1F);
            var scale = new Vector4(1 / 255F, 1 / 255F, 1 / 255F, 1F);

            var converter = JpegColorConverter.GetConverter(JpegColorSpace.Cmyk);
            JpegColorConverter.ComponentValues values = CreateRandomValues(4, inputBufferLength, seed);
            var result = new Vector4[resultBufferLength];

            converter.ConvertToRgba(values, result);

            for (int i = 0; i < resultBufferLength; i++)
            {
                float c = values.Component0[i];
                float m = values.Component1[i];
                float y = values.Component2[i];
                float k = values.Component3[i] / 255F;

                v.X = c * k;
                v.Y = m * k;
                v.Z = y * k;
                v.W = 1F;

                v *= scale;

                Vector4 rgba = result[i];
                var actual = new Rgb(rgba.X, rgba.Y, rgba.Z);
                var expected = new Rgb(v.X, v.Y, v.Z);

                Assert.True(actual.AlmostEquals(expected, Precision));
                Assert.Equal(1, rgba.W);
            }
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void ConvertFromGrayScale(int inputBufferLength, int resultBufferLength, int seed)
        {
            var converter = JpegColorConverter.GetConverter(JpegColorSpace.Grayscale);
            JpegColorConverter.ComponentValues values = CreateRandomValues(1, inputBufferLength, seed);
            var result = new Vector4[resultBufferLength];

            converter.ConvertToRgba(values, result);

            for (int i = 0; i < resultBufferLength; i++)
            {
                float y = values.Component0[i];
                Vector4 rgba = result[i];
                var actual = new Rgb(rgba.X, rgba.Y, rgba.Z);
                var expected = new Rgb(y / 255F, y / 255F, y / 255F);

                Assert.True(actual.AlmostEquals(expected, Precision));
                Assert.Equal(1, rgba.W);
            }
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void ConvertFromRgb(int inputBufferLength, int resultBufferLength, int seed)
        {
            var converter = JpegColorConverter.GetConverter(JpegColorSpace.RGB);
            JpegColorConverter.ComponentValues values = CreateRandomValues(3, inputBufferLength, seed);
            var result = new Vector4[resultBufferLength];

            converter.ConvertToRgba(values, result);

            for (int i = 0; i < resultBufferLength; i++)
            {
                float r = values.Component0[i];
                float g = values.Component1[i];
                float b = values.Component2[i];
                Vector4 rgba = result[i];
                var actual = new Rgb(rgba.X, rgba.Y, rgba.Z);
                var expected = new Rgb(r / 255F, g / 255F, b / 255F);

                Assert.True(actual.AlmostEquals(expected, Precision));
                Assert.Equal(1, rgba.W);
            }
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void ConvertFromYcck(int inputBufferLength, int resultBufferLength, int seed)
        {
            var v = new Vector4(0, 0, 0, 1F);
            var scale = new Vector4(1 / 255F, 1 / 255F, 1 / 255F, 1F);

            var converter = JpegColorConverter.GetConverter(JpegColorSpace.Ycck);
            JpegColorConverter.ComponentValues values = CreateRandomValues(4, inputBufferLength, seed);
            var result = new Vector4[resultBufferLength];

            converter.ConvertToRgba(values, result);

            for (int i = 0; i < resultBufferLength; i++)
            {
                float y = values.Component0[i];
                float cb = values.Component1[i] - 128F;
                float cr = values.Component2[i] - 128F;
                float k = values.Component3[i] / 255F;

                v.X = (255F - (float)Math.Round(y + (1.402F * cr), MidpointRounding.AwayFromZero)) * k;
                v.Y = (255F - (float)Math.Round(
                           y - (0.344136F * cb) - (0.714136F * cr),
                           MidpointRounding.AwayFromZero)) * k;
                v.Z = (255F - (float)Math.Round(y + (1.772F * cb), MidpointRounding.AwayFromZero)) * k;
                v.W = 1F;

                v *= scale;

                Vector4 rgba = result[i];
                var actual = new Rgb(rgba.X, rgba.Y, rgba.Z);
                var expected = new Rgb(v.X, v.Y, v.Z);

                Assert.True(actual.AlmostEquals(expected, Precision));
                Assert.Equal(1, rgba.W);
            }
        }

        private static JpegColorConverter.ComponentValues CreateRandomValues(
            int componentCount,
            int inputBufferLength,
            int seed,
            float minVal = 0f,
            float maxVal = 255f)
        {
            var rnd = new Random(seed);
            var buffers = new Buffer2D<float>[componentCount];
            for (int i = 0; i < componentCount; i++)
            {
                float[] values = new float[inputBufferLength];

                for (int j = 0; j < inputBufferLength; j++)
                {
                    values[j] = (float)rnd.NextDouble() * (maxVal - minVal) + minVal;
                }

                // no need to dispose when buffer is not array owner
                var memory = new Memory<float>(values);
                var source = new MemorySource<float>(memory);
                buffers[i] = new Buffer2D<float>(source, values.Length, 1);
            }
            return new JpegColorConverter.ComponentValues(buffers, 0);
        }

        private static void ValidateConversion(
            JpegColorSpace colorSpace,
            int componentCount,
            int inputBufferLength,
            int resultBufferLength,
            int seed)
        {
            ValidateRgbToYCbCrConversion(
                JpegColorConverter.GetConverter(colorSpace),
                componentCount,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        private static void ValidateRgbToYCbCrConversion(
            JpegColorConverter converter,
            int componentCount,
            int inputBufferLength,
            int resultBufferLength,
            int seed)
        {
            JpegColorConverter.ComponentValues values = CreateRandomValues(componentCount, inputBufferLength, seed);
            var result = new Vector4[resultBufferLength];

            converter.ConvertToRgba(values, result);

            for (int i = 0; i < resultBufferLength; i++)
            {
                ValidateYCbCr(values, result, i);
            }
        }
    }
}