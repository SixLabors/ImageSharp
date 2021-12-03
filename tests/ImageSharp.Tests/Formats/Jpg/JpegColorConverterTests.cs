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
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    [Trait("Format", "Jpg")]
    public class JpegColorConverterTests
    {
        private const float Precision = 0.1F / 255;

        private const int TestBufferLength = 40;

        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new(Precision);

        public static readonly TheoryData<int> Seeds = new() { 1, 2, 3 };

        private static readonly ColorSpaceConverter ColorSpaceConverter = new();

        public JpegColorConverterTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }

        [Fact]
        public void GetConverterThrowsExceptionOnInvalidColorSpace()
        {
            Assert.Throws<Exception>(() => JpegColorConverterBase.GetConverter(JpegColorSpace.Undefined, 8));
        }

        [Fact]
        public void GetConverterThrowsExceptionOnInvalidPrecision()
        {
            // Valid precisions: 8 & 12 bit
            Assert.Throws<Exception>(() => JpegColorConverterBase.GetConverter(JpegColorSpace.YCbCr, 9));
        }

        [Theory]
        [InlineData(JpegColorSpace.Grayscale, 8)]
        [InlineData(JpegColorSpace.Grayscale, 12)]
        [InlineData(JpegColorSpace.Ycck, 8)]
        [InlineData(JpegColorSpace.Ycck, 12)]
        [InlineData(JpegColorSpace.Cmyk, 8)]
        [InlineData(JpegColorSpace.Cmyk, 12)]
        [InlineData(JpegColorSpace.RGB, 8)]
        [InlineData(JpegColorSpace.RGB, 12)]
        [InlineData(JpegColorSpace.YCbCr, 8)]
        [InlineData(JpegColorSpace.YCbCr, 12)]
        internal void GetConverterReturnsValidConverter(JpegColorSpace colorSpace, int precision)
        {
            var converter = JpegColorConverterBase.GetConverter(colorSpace, precision);

            Assert.NotNull(converter);
            Assert.True(converter.IsAvailable);
            Assert.Equal(colorSpace, converter.ColorSpace);
            Assert.Equal(precision, converter.Precision);
        }

        [Theory]
        [InlineData(JpegColorSpace.Grayscale, 1)]
        [InlineData(JpegColorSpace.Ycck, 4)]
        [InlineData(JpegColorSpace.Cmyk, 4)]
        [InlineData(JpegColorSpace.RGB, 3)]
        [InlineData(JpegColorSpace.YCbCr, 3)]
        internal void ConvertWithSelectedConverter(JpegColorSpace colorSpace, int componentCount)
        {
            var converter = JpegColorConverterBase.GetConverter(colorSpace, 8);
            ValidateConversion(
                converter,
                componentCount,
                1);
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void FromYCbCrBasic(int seed)
        {
            ValidateConversion(
                new JpegColorConverterBase.FromYCbCrScalar(8),
                3,
                seed);
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void FromYCbCrVector(int seed)
        {
            var converter = new JpegColorConverterBase.FromYCbCrVector(8);

            if (!converter.IsAvailable)
            {
                this.Output.WriteLine(
                    $"Skipping test - {converter.GetType().Name} is not supported on current hardware.");
                return;
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                seed,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX);

            static void RunTest(string arg) =>
                ValidateConversion(
                    new JpegColorConverterBase.FromYCbCrVector(8),
                    3,
                    FeatureTestRunner.Deserialize<int>(arg));
        }

#if SUPPORTS_RUNTIME_INTRINSICS
        [Theory]
        [MemberData(nameof(Seeds))]
        public void FromYCbCrAvx2(int seed)
        {
            var converter = new JpegColorConverterBase.FromYCbCrAvx(8);

            if (!converter.IsAvailable)
            {
                this.Output.WriteLine(
                    $"Skipping test - {converter.GetType().Name} is not supported on current hardware.");
                return;
            }

            ValidateConversion(
                converter,
                3,
                seed);
        }
#endif

        [Theory]
        [MemberData(nameof(Seeds))]
        public void FromCmykBasic(int seed)
        {
            ValidateConversion(
                new JpegColorConverterBase.FromCmykScalar(8),
                4,
                seed);
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void FromCmykVector(int seed)
        {
            var converter = new JpegColorConverterBase.FromCmykVector(8);

            if (!converter.IsAvailable)
            {
                this.Output.WriteLine(
                    $"Skipping test - {converter.GetType().Name} is not supported on current hardware.");
                return;
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                seed,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX);

            static void RunTest(string arg) =>
                ValidateConversion(
                    new JpegColorConverterBase.FromCmykVector(8),
                    4,
                    FeatureTestRunner.Deserialize<int>(arg));
        }

#if SUPPORTS_RUNTIME_INTRINSICS
        [Theory]
        [MemberData(nameof(Seeds))]
        public void FromCmykAvx2(int seed)
        {
            var converter = new JpegColorConverterBase.FromCmykAvx(8);

            if (!converter.IsAvailable)
            {
                this.Output.WriteLine(
                    $"Skipping test - {converter.GetType().Name} is not supported on current hardware.");
                return;
            }

            ValidateConversion(
                converter,
                4,
                seed);
        }
#endif

        [Theory]
        [MemberData(nameof(Seeds))]
        public void FromGrayscaleBasic(int seed)
        {
            ValidateConversion(
                new JpegColorConverterBase.FromGrayscaleScalar(8),
                1,
                seed);
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void FromGrayscaleVector(int seed)
        {
            var converter = new JpegColorConverterBase.FromGrayScaleVector(8);

            if (!converter.IsAvailable)
            {
                this.Output.WriteLine(
                    $"Skipping test - {converter.GetType().Name} is not supported on current hardware.");
                return;
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                seed,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX);

            static void RunTest(string arg) =>
                ValidateConversion(
                    new JpegColorConverterBase.FromGrayScaleVector(8),
                    1,
                    FeatureTestRunner.Deserialize<int>(arg));
        }

#if SUPPORTS_RUNTIME_INTRINSICS
        [Theory]
        [MemberData(nameof(Seeds))]
        public void FromGrayscaleAvx2(int seed)
        {
            var converter = new JpegColorConverterBase.FromGrayscaleAvx(8);

            if (!converter.IsAvailable)
            {
                this.Output.WriteLine(
                    $"Skipping test - {converter.GetType().Name} is not supported on current hardware.");
                return;
            }

            ValidateConversion(
                converter,
                1,
                seed);
        }
#endif

        [Theory]
        [MemberData(nameof(Seeds))]
        public void FromRgbBasic(int seed)
        {
            ValidateConversion(
                new JpegColorConverterBase.FromRgbScalar(8),
                3,
                seed);
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void FromRgbVector(int seed)
        {
            var converter = new JpegColorConverterBase.FromRgbVector(8);

            if (!converter.IsAvailable)
            {
                this.Output.WriteLine(
                    $"Skipping test - {converter.GetType().Name} is not supported on current hardware.");
                return;
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                seed,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX);

            static void RunTest(string arg) =>
                ValidateConversion(
                    new JpegColorConverterBase.FromRgbVector(8),
                    3,
                    FeatureTestRunner.Deserialize<int>(arg));
        }

#if SUPPORTS_RUNTIME_INTRINSICS
        [Theory]
        [MemberData(nameof(Seeds))]
        public void FromRgbAvx2(int seed)
        {
            var converter = new JpegColorConverterBase.FromRgbAvx(8);

            if (!converter.IsAvailable)
            {
                this.Output.WriteLine(
                    $"Skipping test - {converter.GetType().Name} is not supported on current hardware.");
                return;
            }

            ValidateConversion(
                converter,
                3,
                seed);
        }
#endif

        [Theory]
        [MemberData(nameof(Seeds))]
        public void FromYccKBasic(int seed)
        {
            ValidateConversion(
                new JpegColorConverterBase.FromYccKScalar(8),
                4,
                seed);
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void FromYccKVector(int seed)
        {
            var converter = new JpegColorConverterBase.FromYccKVector(8);

            if (!converter.IsAvailable)
            {
                this.Output.WriteLine(
                    $"Skipping test - {converter.GetType().Name} is not supported on current hardware.");
                return;
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                seed,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX);

            static void RunTest(string arg) =>
                ValidateConversion(
                    new JpegColorConverterBase.FromYccKVector(8),
                    4,
                    FeatureTestRunner.Deserialize<int>(arg));
        }

#if SUPPORTS_RUNTIME_INTRINSICS
        [Theory]
        [MemberData(nameof(Seeds))]
        public void FromYccKAvx2(int seed)
        {
            var converter = new JpegColorConverterBase.FromYccKAvx(8);

            if (!converter.IsAvailable)
            {
                this.Output.WriteLine(
                    $"Skipping test - {converter.GetType().Name} is not supported on current hardware.");
                return;
            }

            ValidateConversion(
                converter,
                4,
                seed);
        }
#endif

        private static JpegColorConverterBase.ComponentValues CreateRandomValues(
            int length,
            int componentCount,
            int seed)
        {
            const float minVal = 0f;
            const float maxVal = Precision;

            var rnd = new Random(seed);

            var buffers = new Buffer2D<float>[componentCount];
            for (int i = 0; i < componentCount; i++)
            {
                float[] values = new float[length];

                for (int j = 0; j < values.Length; j++)
                {
                    values[j] = ((float)rnd.NextDouble() * (maxVal - minVal)) + minVal;
                }

                // no need to dispose when buffer is not array owner
                var memory = new Memory<float>(values);
                var source = MemoryGroup<float>.Wrap(memory);
                buffers[i] = new Buffer2D<float>(source, values.Length, 1);
            }

            return new JpegColorConverterBase.ComponentValues(buffers, 0);
        }

        private static void ValidateConversion(
            JpegColorConverterBase converter,
            int componentCount,
            int seed)
        {
            JpegColorConverterBase.ComponentValues original = CreateRandomValues(TestBufferLength, componentCount, seed);
            JpegColorConverterBase.ComponentValues values = new(
                    original.ComponentCount,
                    original.Component0,
                    original.Component1,
                    original.Component2,
                    original.Component3);

            converter.ConvertToRgbInplace(values);

            for (int i = 0; i < TestBufferLength; i++)
            {
                Validate(converter.ColorSpace, original, values, i);
            }
        }

        private static void Validate(
            JpegColorSpace colorSpace,
            in JpegColorConverterBase.ComponentValues original,
            in JpegColorConverterBase.ComponentValues result,
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
                case JpegColorSpace.Undefined:
                default:
                    Assert.True(false, $"Invalid Colorspace enum value: {colorSpace}.");
                    break;
            }
        }

        private static void ValidateYCbCr(in JpegColorConverterBase.ComponentValues values, in JpegColorConverterBase.ComponentValues result, int i)
        {
            float y = values.Component0[i];
            float cb = values.Component1[i];
            float cr = values.Component2[i];
            var ycbcr = new YCbCr(y, cb, cr);

            var actual = new Rgb(result.Component0[i], result.Component1[i], result.Component2[i]);
            var expected = ColorSpaceConverter.ToRgb(ycbcr);

            bool equal = ColorSpaceComparer.Equals(expected, actual);
            Assert.True(equal, $"Colors {expected} and {actual} are not equal at index {i}");
        }

        private static void ValidateCyyK(in JpegColorConverterBase.ComponentValues values, in JpegColorConverterBase.ComponentValues result, int i)
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

            bool equal = ColorSpaceComparer.Equals(expected, actual);
            Assert.True(equal, $"Colors {expected} and {actual} are not equal at index {i}");
        }

        private static void ValidateRgb(in JpegColorConverterBase.ComponentValues values, in JpegColorConverterBase.ComponentValues result, int i)
        {
            float r = values.Component0[i];
            float g = values.Component1[i];
            float b = values.Component2[i];

            var actual = new Rgb(result.Component0[i], result.Component1[i], result.Component2[i]);
            var expected = new Rgb(r / 255F, g / 255F, b / 255F);

            bool equal = ColorSpaceComparer.Equals(expected, actual);
            Assert.True(equal, $"Colors {expected} and {actual} are not equal at index {i}");
        }

        private static void ValidateGrayScale(in JpegColorConverterBase.ComponentValues values, in JpegColorConverterBase.ComponentValues result, int i)
        {
            float y = values.Component0[i];
            var actual = new Rgb(result.Component0[i], result.Component0[i], result.Component0[i]);
            var expected = new Rgb(y / 255F, y / 255F, y / 255F);

            bool equal = ColorSpaceComparer.Equals(expected, actual);
            Assert.True(equal, $"Colors {expected} and {actual} are not equal at index {i}");
        }

        private static void ValidateCmyk(in JpegColorConverterBase.ComponentValues values, in JpegColorConverterBase.ComponentValues result, int i)
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

            bool equal = ColorSpaceComparer.Equals(expected, actual);
            Assert.True(equal, $"Colors {expected} and {actual} are not equal at index {i}");
        }
    }
}
