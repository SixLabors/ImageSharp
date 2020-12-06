// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Tests.Colorspaces.Conversion;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    [Trait("Format", "Jpg")]
    public class JpegColorConverterTests
    {
        private const float Precision = 0.1F / 255;

        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(Precision);

        // int inputBufferLength, int resultBufferLength, int seed
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
        public void FromYCbCrBasic(int inputBufferLength, int resultBufferLength, int seed)
        {
            ValidateConversion(
                new JpegColorConverter.FromYCbCrBasic(8),
                3,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void FromYCbCrVector4(int inputBufferLength, int resultBufferLength, int seed)
        {
            if (!SimdUtils.HasVector4)
            {
                this.Output.WriteLine("No SSE present, skipping test!");
                return;
            }

            ValidateConversion(
                new JpegColorConverter.FromYCbCrVector4(8),
                3,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void FromYCbCrVector8(int inputBufferLength, int resultBufferLength, int seed)
        {
            if (!SimdUtils.HasVector8)
            {
                this.Output.WriteLine("No AVX2 present, skipping test!");
                return;
            }

            ValidateConversion(
                new JpegColorConverter.FromYCbCrVector8(8),
                3,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void FromYCbCrAvx2(int inputBufferLength, int resultBufferLength, int seed)
        {
            if (!SimdUtils.HasAvx2)
            {
                this.Output.WriteLine("No AVX2 present, skipping test!");
                return;
            }

            ValidateConversion(
                new JpegColorConverter.FromYCbCrAvx2(8),
                3,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void FromYCbCr_WithDefaultConverter(int inputBufferLength, int resultBufferLength, int seed)
        {
            ValidateConversion(
                JpegColorSpace.YCbCr,
                3,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void FromCmykBasic(int inputBufferLength, int resultBufferLength, int seed)
        {
            ValidateConversion(
                new JpegColorConverter.FromCmykBasic(8),
                4,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void FromCmykVector8(int inputBufferLength, int resultBufferLength, int seed)
        {
            if (!SimdUtils.HasVector8)
            {
                this.Output.WriteLine("No AVX2 present, skipping test!");
                return;
            }

            ValidateConversion(
                new JpegColorConverter.FromCmykVector8(8),
                4,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void FromCmykAvx2(int inputBufferLength, int resultBufferLength, int seed)
        {
            if (!SimdUtils.HasAvx2)
            {
                this.Output.WriteLine("No AVX2 present, skipping test!");
                return;
            }

            ValidateConversion(
                new JpegColorConverter.FromCmykAvx2(8),
                4,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void FromCmyk_WithDefaultConverter(int inputBufferLength, int resultBufferLength, int seed)
        {
            ValidateConversion(
                JpegColorSpace.Cmyk,
                4,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void FromGrayscaleBasic(int inputBufferLength, int resultBufferLength, int seed)
        {
            ValidateConversion(
                new JpegColorConverter.FromGrayscaleBasic(8),
                1,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void FromGrayscaleAvx2(int inputBufferLength, int resultBufferLength, int seed)
        {
            if (!SimdUtils.HasAvx2)
            {
                this.Output.WriteLine("No AVX2 present, skipping test!");
                return;
            }

            ValidateConversion(
                new JpegColorConverter.FromGrayscaleAvx2(8),
                1,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void FromGraysacle_WithDefaultConverter(int inputBufferLength, int resultBufferLength, int seed)
        {
            ValidateConversion(
                JpegColorSpace.Grayscale,
                1,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void FromRgbBasic(int inputBufferLength, int resultBufferLength, int seed)
        {
            ValidateConversion(
                new JpegColorConverter.FromRgbBasic(8),
                3,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void FromRgbVector8(int inputBufferLength, int resultBufferLength, int seed)
        {
            if (!SimdUtils.HasVector8)
            {
                this.Output.WriteLine("No AVX2 present, skipping test!");
                return;
            }

            ValidateConversion(
                new JpegColorConverter.FromRgbVector8(8),
                3,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void FromRgbAvx2(int inputBufferLength, int resultBufferLength, int seed)
        {
            if (!SimdUtils.HasAvx2)
            {
                this.Output.WriteLine("No AVX2 present, skipping test!");
                return;
            }

            ValidateConversion(
                new JpegColorConverter.FromRgbAvx2(8),
                3,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void FromRgb_WithDefaultConverter(int inputBufferLength, int resultBufferLength, int seed)
        {
            ValidateConversion(
                JpegColorSpace.RGB,
                3,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void FromYccKBasic(int inputBufferLength, int resultBufferLength, int seed)
        {
            ValidateConversion(
                new JpegColorConverter.FromYccKBasic(8),
                4,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void FromYccKVector8(int inputBufferLength, int resultBufferLength, int seed)
        {
            if (!SimdUtils.HasVector8)
            {
                this.Output.WriteLine("No AVX2 present, skipping test!");
                return;
            }

            ValidateConversion(
                new JpegColorConverter.FromYccKVector8(8),
                4,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void FromYccKAvx2(int inputBufferLength, int resultBufferLength, int seed)
        {
            if (!SimdUtils.HasAvx2)
            {
                this.Output.WriteLine("No AVX2 present, skipping test!");
                return;
            }

            ValidateConversion(
                new JpegColorConverter.FromYccKAvx2(8),
                4,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void FromYcck_WithDefaultConverter(int inputBufferLength, int resultBufferLength, int seed)
        {
            ValidateConversion(
                JpegColorSpace.Ycck,
                4,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        // Benchmark, for local execution only
        // [Theory]
        // [InlineData(false)]
        // [InlineData(true)]
        public void BenchmarkYCbCr(bool simd)
        {
            int count = 2053;
            int times = 50000;

            JpegColorConverter.ComponentValues values = CreateRandomValues(3, count, 1);
            var result = new Vector4[count];

            JpegColorConverter converter = simd ? (JpegColorConverter)new JpegColorConverter.FromYCbCrVector4(8) : new JpegColorConverter.FromYCbCrBasic(8);

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
                var values = new float[inputBufferLength];

                for (int j = 0; j < inputBufferLength; j++)
                {
                    values[j] = ((float)rnd.NextDouble() * (maxVal - minVal)) + minVal;
                }

                // no need to dispose when buffer is not array owner
                var memory = new Memory<float>(values);
                var source = MemoryGroup<float>.Wrap(memory);
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
            ValidateConversion(
                JpegColorConverter.GetConverter(colorSpace, 8),
                componentCount,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        private static void ValidateConversion(
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
                Validate(converter.ColorSpace, values, result, i);
            }
        }

        private static void Validate(
            JpegColorSpace colorSpace,
            in JpegColorConverter.ComponentValues values,
            Vector4[] result,
            int i)
        {
            switch (colorSpace)
            {
                case JpegColorSpace.Grayscale:
                    ValidateGrayScale(values, result, i);
                    break;
                case JpegColorSpace.Ycck:
                    ValidateCyyK(values, result, i);
                    break;
                case JpegColorSpace.Cmyk:
                    ValidateCmyk(values, result, i);
                    break;
                case JpegColorSpace.RGB:
                    ValidateRgb(values, result, i);
                    break;
                case JpegColorSpace.YCbCr:
                    ValidateYCbCr(values, result, i);
                    break;
                default:
                    Assert.True(false, $"Colorspace {colorSpace} not supported!");
                    break;
            }
        }

        private static void ValidateYCbCr(in JpegColorConverter.ComponentValues values, Vector4[] result, int i)
        {
            float y = values.Component0[i];
            float cb = values.Component1[i];
            float cr = values.Component2[i];
            var ycbcr = new YCbCr(y, cb, cr);

            Vector4 rgba = result[i];
            var actual = new Rgb(rgba.X, rgba.Y, rgba.Z);
            var expected = ColorSpaceConverter.ToRgb(ycbcr);

            Assert.Equal(expected, actual, ColorSpaceComparer);
            Assert.Equal(1, rgba.W);
        }

        private static void ValidateCyyK(in JpegColorConverter.ComponentValues values, Vector4[] result, int i)
        {
            var v = new Vector4(0, 0, 0, 1F);
            var scale = new Vector4(1 / 255F, 1 / 255F, 1 / 255F, 1F);

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

            Assert.Equal(expected, actual, ColorSpaceComparer);
            Assert.Equal(1, rgba.W);
        }

        private static void ValidateRgb(in JpegColorConverter.ComponentValues values, Vector4[] result, int i)
        {
            float r = values.Component0[i];
            float g = values.Component1[i];
            float b = values.Component2[i];
            Vector4 rgba = result[i];
            var actual = new Rgb(rgba.X, rgba.Y, rgba.Z);
            var expected = new Rgb(r / 255F, g / 255F, b / 255F);

            Assert.Equal(expected, actual, ColorSpaceComparer);
            Assert.Equal(1, rgba.W);
        }

        private static void ValidateGrayScale(in JpegColorConverter.ComponentValues values, Vector4[] result, int i)
        {
            float y = values.Component0[i];
            Vector4 rgba = result[i];
            var actual = new Rgb(rgba.X, rgba.Y, rgba.Z);
            var expected = new Rgb(y / 255F, y / 255F, y / 255F);

            Assert.Equal(expected, actual, ColorSpaceComparer);
            Assert.Equal(1, rgba.W);
        }

        private static void ValidateCmyk(in JpegColorConverter.ComponentValues values, Vector4[] result, int i)
        {
            var v = new Vector4(0, 0, 0, 1F);
            var scale = new Vector4(1 / 255F, 1 / 255F, 1 / 255F, 1F);

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

            Assert.Equal(expected, actual, ColorSpaceComparer);
            Assert.Equal(1, rgba.W);
        }
    }
}
