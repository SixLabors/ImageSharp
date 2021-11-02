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
                    { 8, 8, 1 },
                    { 40, 40, 1 },
                    { 256, 256, 2 },
                    { 512, 512, 3 }
                };

        private static readonly ColorSpaceConverter ColorSpaceConverter = new ColorSpaceConverter();

        public JpegColorConverterTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void FromYCbCrScalar(int inputBufferLength, int resultBufferLength, int seed)
        {
            ValidateConversion(
                new JpegColorConverter.FromYCbCrScalar(8),
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
        public void FromYCbCrAvx(int inputBufferLength, int resultBufferLength, int seed)
        {
            if (!SimdUtils.HasAvx)
            {
                this.Output.WriteLine("No AVX2 present, skipping test!");
                return;
            }

            ValidateConversion(
                new JpegColorConverter.FromYCbCrAvx(8),
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
        public void FromCmykScalar(int inputBufferLength, int resultBufferLength, int seed)
        {
            ValidateConversion(
                new JpegColorConverter.FromCmykScalar(8),
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
        public void FromCmykAvx(int inputBufferLength, int resultBufferLength, int seed)
        {
            if (!SimdUtils.HasAvx)
            {
                this.Output.WriteLine("No AVX2 present, skipping test!");
                return;
            }

            ValidateConversion(
                new JpegColorConverter.FromCmykAvx(8),
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
        public void FromGrayscaleScalar(int inputBufferLength, int resultBufferLength, int seed)
        {
            ValidateConversion(
                new JpegColorConverter.FromGrayscaleScalar(8),
                1,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void FromGrayscaleVector8(int inputBufferLength, int resultBufferLength, int seed)
        {
            if (!SimdUtils.HasVector8)
            {
                this.Output.WriteLine("No AVX2 present, skipping test!");
                return;
            }

            ValidateConversion(
                new JpegColorConverter.FromGrayScaleVector8(8),
                1,
                inputBufferLength,
                resultBufferLength,
                seed);
        }

        [Theory]
        [MemberData(nameof(CommonConversionData))]
        public void FromGrayscaleAvx(int inputBufferLength, int resultBufferLength, int seed)
        {
            if (!SimdUtils.HasAvx)
            {
                this.Output.WriteLine("No AVX2 present, skipping test!");
                return;
            }

            ValidateConversion(
                new JpegColorConverter.FromGrayscaleAvx(8),
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
        public void FromRgbScalar(int inputBufferLength, int resultBufferLength, int seed)
        {
            ValidateConversion(
                new JpegColorConverter.FromRgbScalar(8),
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
        public void FromRgbAvx(int inputBufferLength, int resultBufferLength, int seed)
        {
            if (!SimdUtils.HasAvx)
            {
                this.Output.WriteLine("No AVX2 present, skipping test!");
                return;
            }

            ValidateConversion(
                new JpegColorConverter.FromRgbAvx(8),
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
        public void FromYccKScalar(int inputBufferLength, int resultBufferLength, int seed)
        {
            ValidateConversion(
                new JpegColorConverter.FromYccKScalar(8),
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
        public void FromYccKAvx(int inputBufferLength, int resultBufferLength, int seed)
        {
            if (!SimdUtils.HasAvx)
            {
                this.Output.WriteLine("No AVX2 present, skipping test!");
                return;
            }

            ValidateConversion(
                new JpegColorConverter.FromYccKAvx(8),
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
            JpegColorConverter.ComponentValues original = CreateRandomValues(componentCount, inputBufferLength, seed);
            JpegColorConverter.ComponentValues values = Copy(original);

            converter.ConvertToRgbInplace(values);

            for (int i = 0; i < resultBufferLength; i++)
            {
                Validate(converter.ColorSpace, original, values, i);
            }

            static JpegColorConverter.ComponentValues Copy(JpegColorConverter.ComponentValues values)
            {
                Span<float> c0 = values.Component0.ToArray();
                Span<float> c1 = values.ComponentCount > 1 ? values.Component1.ToArray().AsSpan() : c0;
                Span<float> c2 = values.ComponentCount > 2 ? values.Component2.ToArray().AsSpan() : c0;
                Span<float> c3 = values.ComponentCount > 3 ? values.Component3.ToArray().AsSpan() : Span<float>.Empty;
                return new JpegColorConverter.ComponentValues(values.ComponentCount, c0, c1, c2, c3);
            }
        }

        private static void Validate(
            JpegColorSpace colorSpace,
            in JpegColorConverter.ComponentValues original,
            in JpegColorConverter.ComponentValues result,
            int i)
        {
            switch (colorSpace)
            {
                case JpegColorSpace.Grayscale:
                    ValidateGrayScale(original, result, i);
                    break;
                case JpegColorSpace.Ycck:
                    ValidateCyyK(original, result, i);
                    break;
                case JpegColorSpace.Cmyk:
                    ValidateCmyk(original, result, i);
                    break;
                case JpegColorSpace.RGB:
                    ValidateRgb(original, result, i);
                    break;
                case JpegColorSpace.YCbCr:
                    ValidateYCbCr(original, result, i);
                    break;
                default:
                    Assert.True(false, $"Colorspace {colorSpace} not supported!");
                    break;
            }
        }

        private static void ValidateYCbCr(in JpegColorConverter.ComponentValues values, in JpegColorConverter.ComponentValues result, int i)
        {
            float y = values.Component0[i];
            float cb = values.Component1[i];
            float cr = values.Component2[i];
            var ycbcr = new YCbCr(y, cb, cr);

            var actual = new Rgb(result.Component0[i], result.Component1[i], result.Component2[i]);
            var expected = ColorSpaceConverter.ToRgb(ycbcr);

            Assert.Equal(expected, actual, ColorSpaceComparer);
        }

        private static void ValidateCyyK(in JpegColorConverter.ComponentValues values, in JpegColorConverter.ComponentValues result, int i)
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

            var actual = new Rgb(result.Component0[i], result.Component1[i], result.Component2[i]);
            var expected = new Rgb(v.X, v.Y, v.Z);

            Assert.Equal(expected, actual, ColorSpaceComparer);
        }

        private static void ValidateRgb(in JpegColorConverter.ComponentValues values, in JpegColorConverter.ComponentValues result, int i)
        {
            float r = values.Component0[i];
            float g = values.Component1[i];
            float b = values.Component2[i];

            var actual = new Rgb(result.Component0[i], result.Component1[i], result.Component2[i]);
            var expected = new Rgb(r / 255F, g / 255F, b / 255F);

            Assert.Equal(expected, actual, ColorSpaceComparer);
        }

        private static void ValidateGrayScale(in JpegColorConverter.ComponentValues values, in JpegColorConverter.ComponentValues result, int i)
        {
            float y = values.Component0[i];
            var actual = new Rgb(result.Component0[i], result.Component0[i], result.Component0[i]);
            var expected = new Rgb(y / 255F, y / 255F, y / 255F);

            Assert.Equal(expected, actual, ColorSpaceComparer);
        }

        private static void ValidateCmyk(in JpegColorConverter.ComponentValues values, in JpegColorConverter.ComponentValues result, int i)
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

            var actual = new Rgb(result.Component0[i], result.Component1[i], result.Component2[i]);
            var expected = new Rgb(v.X, v.Y, v.Z);

            Assert.Equal(expected, actual, ColorSpaceComparer);
        }
    }
}
