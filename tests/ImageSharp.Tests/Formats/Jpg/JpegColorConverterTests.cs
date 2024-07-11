// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Tests.ColorProfiles;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg;

[Trait("Format", "Jpg")]
public class JpegColorConverterTests
{
    private const float MaxColorChannelValue = 255f;

    private const float Precision = 0.1F / 255;

    private const int TestBufferLength = 40;

    private const HwIntrinsics IntrinsicsConfig = HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2;

    private static readonly ApproximateColorProfileComparer ColorSpaceComparer = new(epsilon: Precision);

    private static readonly ColorProfileConverter ColorSpaceConverter = new();

    public static readonly TheoryData<int> Seeds = new() { 1, 2, 3 };

    public JpegColorConverterTests(ITestOutputHelper output)
        => this.Output = output;

    private ITestOutputHelper Output { get; }

    [Fact]
    public void GetConverterThrowsExceptionOnInvalidColorSpace()
    {
        const JpegColorSpace invalidColorSpace = (JpegColorSpace)(-1);
        Assert.Throws<InvalidImageContentException>(() => JpegColorConverterBase.GetConverter(invalidColorSpace, 8));
    }

    [Fact]
    public void GetConverterThrowsExceptionOnInvalidPrecision()
    {
        // Valid precisions: 8 & 12 bit
        const int invalidPrecision = 9;
        Assert.Throws<InvalidImageContentException>(() => JpegColorConverterBase.GetConverter(JpegColorSpace.YCbCr, invalidPrecision));
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
        JpegColorConverterBase converter = JpegColorConverterBase.GetConverter(colorSpace, precision);

        Assert.NotNull(converter);
        Assert.True(converter.IsAvailable);
        Assert.Equal(colorSpace, converter.ColorSpace);
        Assert.Equal(precision, converter.Precision);
    }

    [Fact]
    public void GetConverterReturnsCorrectConverterWithRgbColorSpace()
    {
        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableSSE2 | HwIntrinsics.DisableHWIntrinsic);

        static void RunTest(string arg)
        {
            // arrange
            Type expectedType = typeof(JpegColorConverterBase.RgbScalar);
            if (Avx.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.RgbAvx);
            }
            else if (Sse2.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.RgbVector);
            }
            else if (AdvSimd.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.RgbArm);
            }

            // act
            JpegColorConverterBase converter = JpegColorConverterBase.GetConverter(JpegColorSpace.RGB, 8);
            Type actualType = converter.GetType();

            // assert
            Assert.Equal(expectedType, actualType);
        }
    }

    [Fact]
    public void GetConverterReturnsCorrectConverterWithGrayScaleColorSpace()
    {
        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableSSE2 | HwIntrinsics.DisableHWIntrinsic);

        static void RunTest(string arg)
        {
            // arrange
            Type expectedType = typeof(JpegColorConverterBase.GrayscaleScalar);
            if (Avx.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.GrayscaleAvx);
            }
            else if (Sse2.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.GrayScaleVector);
            }
            else if (AdvSimd.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.GrayscaleArm);
            }

            // act
            JpegColorConverterBase converter = JpegColorConverterBase.GetConverter(JpegColorSpace.Grayscale, 8);
            Type actualType = converter.GetType();

            // assert
            Assert.Equal(expectedType, actualType);
        }
    }

    [Fact]
    public void GetConverterReturnsCorrectConverterWithCmykColorSpace()
    {
        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableSSE2 | HwIntrinsics.DisableHWIntrinsic);

        static void RunTest(string arg)
        {
            // arrange
            Type expectedType = typeof(JpegColorConverterBase.CmykScalar);
            if (Avx.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.CmykAvx);
            }
            else if (Sse2.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.CmykVector);
            }
            else if (AdvSimd.Arm64.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.CmykArm64);
            }

            // act
            JpegColorConverterBase converter = JpegColorConverterBase.GetConverter(JpegColorSpace.Cmyk, 8);
            Type actualType = converter.GetType();

            // assert
            Assert.Equal(expectedType, actualType);
        }
    }

    [Fact]
    public void GetConverterReturnsCorrectConverterWithYCbCrColorSpace()
    {
        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableSSE2 | HwIntrinsics.DisableHWIntrinsic);

        static void RunTest(string arg)
        {
            // arrange
            Type expectedType = typeof(JpegColorConverterBase.YCbCrScalar);
            if (Avx.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.YCbCrAvx);
            }
            else if (Sse2.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.YCbCrVector);
            }
            else if (AdvSimd.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.YCbCrArm);
            }

            // act
            JpegColorConverterBase converter = JpegColorConverterBase.GetConverter(JpegColorSpace.YCbCr, 8);
            Type actualType = converter.GetType();

            // assert
            Assert.Equal(expectedType, actualType);
        }
    }

    [Fact]
    public void GetConverterReturnsCorrectConverterWithYcckColorSpace()
    {
        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableSSE2 | HwIntrinsics.DisableHWIntrinsic);

        static void RunTest(string arg)
        {
            // arrange
            Type expectedType = typeof(JpegColorConverterBase.YccKScalar);
            if (Avx.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.YccKAvx);
            }
            else if (Sse2.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.YccKVector);
            }
            else if (AdvSimd.Arm64.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.YccKArm64);
            }

            // act
            JpegColorConverterBase converter = JpegColorConverterBase.GetConverter(JpegColorSpace.Ycck, 8);
            Type actualType = converter.GetType();

            // assert
            Assert.Equal(expectedType, actualType);
        }
    }

    [Theory]
    [InlineData(JpegColorSpace.Grayscale, 1)]
    [InlineData(JpegColorSpace.Ycck, 4)]
    [InlineData(JpegColorSpace.Cmyk, 4)]
    [InlineData(JpegColorSpace.RGB, 3)]
    [InlineData(JpegColorSpace.YCbCr, 3)]
    internal void ConvertToRgbWithSelectedConverter(JpegColorSpace colorSpace, int componentCount)
    {
        JpegColorConverterBase converter = JpegColorConverterBase.GetConverter(colorSpace, 8);
        ValidateConversionToRgb(
            converter,
            componentCount,
            1);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromYCbCrBasic(int seed) =>
        this.TestConversionToRgb(new JpegColorConverterBase.YCbCrScalar(8), 3, seed);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromYCbCrVector(int seed)
    {
        JpegColorConverterBase.YCbCrVector converter = new(8);

        if (!converter.IsAvailable)
        {
            this.Output.WriteLine(
                $"Skipping test - {converter.GetType().Name} is not supported on current hardware.");
            return;
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            seed,
            IntrinsicsConfig);

        static void RunTest(string arg) =>
            ValidateConversionToRgb(
                new JpegColorConverterBase.YCbCrVector(8),
                3,
                FeatureTestRunner.Deserialize<int>(arg),
                new JpegColorConverterBase.YCbCrScalar(8));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromCmykBasic(int seed) =>
        this.TestConversionToRgb(new JpegColorConverterBase.CmykScalar(8), 4, seed);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromCmykVector(int seed)
    {
        JpegColorConverterBase.CmykVector converter = new(8);

        if (!converter.IsAvailable)
        {
            this.Output.WriteLine(
                $"Skipping test - {converter.GetType().Name} is not supported on current hardware.");
            return;
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            seed,
            IntrinsicsConfig);

        static void RunTest(string arg) =>
            ValidateConversionToRgb(
                new JpegColorConverterBase.CmykVector(8),
                4,
                FeatureTestRunner.Deserialize<int>(arg),
                new JpegColorConverterBase.CmykScalar(8));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromGrayscaleBasic(int seed) =>
        this.TestConversionToRgb(new JpegColorConverterBase.GrayscaleScalar(8), 1, seed);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromGrayscaleVector(int seed)
    {
        JpegColorConverterBase.GrayScaleVector converter = new(8);

        if (!converter.IsAvailable)
        {
            this.Output.WriteLine(
                $"Skipping test - {converter.GetType().Name} is not supported on current hardware.");
            return;
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            seed,
            IntrinsicsConfig);

        static void RunTest(string arg) =>
            ValidateConversionToRgb(
                new JpegColorConverterBase.GrayScaleVector(8),
                1,
                FeatureTestRunner.Deserialize<int>(arg),
                new JpegColorConverterBase.GrayscaleScalar(8));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbBasic(int seed) =>
        this.TestConversionToRgb(new JpegColorConverterBase.RgbScalar(8), 3, seed);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbVector(int seed)
    {
        JpegColorConverterBase.RgbVector converter = new(8);

        if (!converter.IsAvailable)
        {
            this.Output.WriteLine(
                $"Skipping test - {converter.GetType().Name} is not supported on current hardware.");
            return;
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            seed,
            IntrinsicsConfig);

        static void RunTest(string arg) =>
            ValidateConversionToRgb(
                new JpegColorConverterBase.RgbVector(8),
                3,
                FeatureTestRunner.Deserialize<int>(arg),
                new JpegColorConverterBase.RgbScalar(8));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromYccKBasic(int seed) =>
        this.TestConversionToRgb(new JpegColorConverterBase.YccKScalar(8), 4, seed);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromYccKVector(int seed)
    {
        JpegColorConverterBase.YccKVector converter = new(8);

        if (!converter.IsAvailable)
        {
            this.Output.WriteLine(
                $"Skipping test - {converter.GetType().Name} is not supported on current hardware.");
            return;
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            seed,
            IntrinsicsConfig);

        static void RunTest(string arg) =>
            ValidateConversionToRgb(
                new JpegColorConverterBase.YccKVector(8),
                4,
                FeatureTestRunner.Deserialize<int>(arg),
                new JpegColorConverterBase.YccKScalar(8));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromYCbCrAvx2(int seed) =>
        this.TestConversionToRgb(
            new JpegColorConverterBase.YCbCrAvx(8),
            3,
            seed,
            new JpegColorConverterBase.YCbCrScalar(8));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbToYCbCrAvx2(int seed) =>
        this.TestConversionFromRgb(
            new JpegColorConverterBase.YCbCrAvx(8),
            3,
            seed,
            new JpegColorConverterBase.YCbCrScalar(8),
            precísion: 2);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromYCbCrArm(int seed) =>
        this.TestConversionToRgb(
            new JpegColorConverterBase.YCbCrArm(8),
            3,
            seed,
            new JpegColorConverterBase.YCbCrScalar(8));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbToYCbCrArm(int seed) =>
        this.TestConversionFromRgb(
            new JpegColorConverterBase.YCbCrArm(8),
            3,
            seed,
            new JpegColorConverterBase.YCbCrScalar(8),
            precísion: 2);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromCmykAvx2(int seed) =>
        this.TestConversionToRgb(
            new JpegColorConverterBase.CmykAvx(8),
            4,
            seed,
            new JpegColorConverterBase.CmykScalar(8));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbToCmykAvx2(int seed) =>
        this.TestConversionFromRgb(
            new JpegColorConverterBase.CmykAvx(8),
            4,
            seed,
            new JpegColorConverterBase.CmykScalar(8),
            precísion: 4);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromCmykArm(int seed) =>
        this.TestConversionToRgb(
            new JpegColorConverterBase.CmykArm64(8),
            4,
            seed,
            new JpegColorConverterBase.CmykScalar(8));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbToCmykArm(int seed) =>
        this.TestConversionFromRgb(
            new JpegColorConverterBase.CmykArm64(8),
            4,
            seed,
            new JpegColorConverterBase.CmykScalar(8),
            precísion: 4);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromGrayscaleAvx2(int seed) =>
        this.TestConversionToRgb(
            new JpegColorConverterBase.GrayscaleAvx(8),
            1,
            seed,
            new JpegColorConverterBase.GrayscaleScalar(8));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbToGrayscaleAvx2(int seed) =>
        this.TestConversionFromRgb(
            new JpegColorConverterBase.GrayscaleAvx(8),
            1,
            seed,
            new JpegColorConverterBase.GrayscaleScalar(8),
            precísion: 3);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromGrayscaleArm(int seed) =>
        this.TestConversionToRgb(
            new JpegColorConverterBase.GrayscaleArm(8),
            1,
            seed,
            new JpegColorConverterBase.GrayscaleScalar(8));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbToGrayscaleArm(int seed) =>
        this.TestConversionFromRgb(
            new JpegColorConverterBase.GrayscaleArm(8),
            1,
            seed,
            new JpegColorConverterBase.GrayscaleScalar(8),
            precísion: 3);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbAvx2(int seed) =>
        this.TestConversionToRgb(
            new JpegColorConverterBase.RgbAvx(8),
            3,
            seed,
            new JpegColorConverterBase.RgbScalar(8));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbArm(int seed) =>
        this.TestConversionToRgb(
            new JpegColorConverterBase.RgbArm(8),
            3,
            seed,
            new JpegColorConverterBase.RgbScalar(8));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromYccKAvx2(int seed) =>
        this.TestConversionToRgb(
            new JpegColorConverterBase.YccKAvx(8),
            4,
            seed,
            new JpegColorConverterBase.YccKScalar(8));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbToYccKAvx2(int seed) =>
        this.TestConversionFromRgb(
            new JpegColorConverterBase.YccKAvx(8),
            4,
            seed,
            new JpegColorConverterBase.YccKScalar(8),
            precísion: 4);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromYccKArm64(int seed) =>
        this.TestConversionToRgb(
            new JpegColorConverterBase.YccKArm64(8),
            4,
            seed,
            new JpegColorConverterBase.YccKScalar(8));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbToYccKArm64(int seed) =>
        this.TestConversionFromRgb(
            new JpegColorConverterBase.YccKArm64(8),
            4,
            seed,
            new JpegColorConverterBase.YccKScalar(8),
            precísion: 4);

    private void TestConversionToRgb(
        JpegColorConverterBase converter,
        int componentCount,
        int seed,
        JpegColorConverterBase baseLineConverter = null)
    {
        if (!converter.IsAvailable)
        {
            this.Output.WriteLine(
                $"Skipping test - {converter.GetType().Name} is not supported on current hardware.");
            return;
        }

        ValidateConversionToRgb(
            converter,
            componentCount,
            seed,
            baseLineConverter);
    }

    private void TestConversionFromRgb(
        JpegColorConverterBase converter,
        int componentCount,
        int seed,
        JpegColorConverterBase baseLineConverter,
        int precísion)
    {
        if (!converter.IsAvailable)
        {
            this.Output.WriteLine(
                $"Skipping test - {converter.GetType().Name} is not supported on current hardware.");
            return;
        }

        ValidateConversionFromRgb(
            converter,
            componentCount,
            seed,
            baseLineConverter,
            precísion);
    }

    private static JpegColorConverterBase.ComponentValues CreateRandomValues(
        int length,
        int componentCount,
        int seed)
    {
        Random rnd = new(seed);

        Buffer2D<float>[] buffers = new Buffer2D<float>[componentCount];
        for (int i = 0; i < componentCount; i++)
        {
            float[] values = new float[length];

            for (int j = 0; j < values.Length; j++)
            {
                values[j] = (float)rnd.NextDouble() * MaxColorChannelValue;
            }

            // no need to dispose when buffer is not array owner
            Memory<float> memory = new(values);
            MemoryGroup<float> source = MemoryGroup<float>.Wrap(memory);
            buffers[i] = new Buffer2D<float>(source, values.Length, 1);
        }

        return new JpegColorConverterBase.ComponentValues(buffers, 0);
    }

    private static float[] CreateRandomValues(int length, Random rnd)
    {
        float[] values = new float[length];

        for (int j = 0; j < values.Length; j++)
        {
            values[j] = (float)rnd.NextDouble() * MaxColorChannelValue;
        }

        return values;
    }

    private static void ValidateConversionToRgb(
        JpegColorConverterBase converter,
        int componentCount,
        int seed,
        JpegColorConverterBase baseLineConverter = null)
    {
        JpegColorConverterBase.ComponentValues original = CreateRandomValues(TestBufferLength, componentCount, seed);
        JpegColorConverterBase.ComponentValues actual = new(
                original.ComponentCount,
                original.Component0.ToArray(),
                original.Component1.ToArray(),
                original.Component2.ToArray(),
                original.Component3.ToArray());

        converter.ConvertToRgbInplace(actual);

        for (int i = 0; i < TestBufferLength; i++)
        {
            Validate(converter.ColorSpace, original, actual, i);
        }

        // Compare conversion result to a baseline, should be the scalar version.
        if (baseLineConverter != null)
        {
            JpegColorConverterBase.ComponentValues expected = new(
                original.ComponentCount,
                original.Component0.ToArray(),
                original.Component1.ToArray(),
                original.Component2.ToArray(),
                original.Component3.ToArray());
            baseLineConverter.ConvertToRgbInplace(expected);
            if (componentCount == 1)
            {
                Assert.True(expected.Component0.SequenceEqual(actual.Component0));
            }

            if (componentCount == 2)
            {
                Assert.True(expected.Component1.SequenceEqual(actual.Component1));
            }

            if (componentCount == 3)
            {
                Assert.True(expected.Component2.SequenceEqual(actual.Component2));
            }

            if (componentCount == 4)
            {
                Assert.True(expected.Component3.SequenceEqual(actual.Component3));
            }
        }
    }

    private static void ValidateConversionFromRgb(
        JpegColorConverterBase converter,
        int componentCount,
        int seed,
        JpegColorConverterBase baseLineConverter,
        int precision = 4)
    {
        // arrange
        JpegColorConverterBase.ComponentValues actual = CreateRandomValues(TestBufferLength, componentCount, seed);
        JpegColorConverterBase.ComponentValues expected = CreateRandomValues(TestBufferLength, componentCount, seed);
        Random rnd = new(seed);
        float[] rLane = CreateRandomValues(TestBufferLength, rnd);
        float[] gLane = CreateRandomValues(TestBufferLength, rnd);
        float[] bLane = CreateRandomValues(TestBufferLength, rnd);

        // act
        converter.ConvertFromRgb(actual, rLane, gLane, bLane);
        baseLineConverter.ConvertFromRgb(expected, rLane, gLane, bLane);

        // assert
        if (componentCount == 1)
        {
            CompareSequenceWithTolerance(expected.Component0, actual.Component0, precision);
        }

        if (componentCount == 2)
        {
            CompareSequenceWithTolerance(expected.Component1, actual.Component1, precision);
        }

        if (componentCount == 3)
        {
            CompareSequenceWithTolerance(expected.Component2, actual.Component2, precision);
        }

        if (componentCount == 4)
        {
            CompareSequenceWithTolerance(expected.Component3, actual.Component3, precision);
        }
    }

    private static void CompareSequenceWithTolerance(Span<float> expected, Span<float> actual, int precision)
    {
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i], precision: precision);
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
        Rgb expected = ColorSpaceConverter.Convert<YCbCr, Rgb>(new YCbCr(y, cb, cr));

        Rgb actual = Rgb.Clamp(new(result.Component0[i], result.Component1[i], result.Component2[i]));

        bool equal = ColorSpaceComparer.Equals(expected, actual);
        Assert.True(equal, $"Colors {expected} and {actual} are not equal at index {i}");
    }

    private static void ValidateCyyK(in JpegColorConverterBase.ComponentValues values, in JpegColorConverterBase.ComponentValues result, int i)
    {
        float y = values.Component0[i];
        float cb = values.Component1[i] - 128F;
        float cr = values.Component2[i] - 128F;
        float k = values.Component3[i] / 255F;

        float r = (255F - (float)Math.Round(y + (1.402F * cr), MidpointRounding.AwayFromZero)) * k;
        float g = (255F - (float)Math.Round(
            y - (0.344136F * cb) - (0.714136F * cr),
            MidpointRounding.AwayFromZero)) * k;
        float b = (255F - (float)Math.Round(y + (1.772F * cb), MidpointRounding.AwayFromZero)) * k;

        r /= MaxColorChannelValue;
        g /= MaxColorChannelValue;
        b /= MaxColorChannelValue;
        Rgb expected = Rgb.Clamp(new(r, g, b));

        Rgb actual = Rgb.Clamp(new(result.Component0[i], result.Component1[i], result.Component2[i]));

        bool equal = ColorSpaceComparer.Equals(expected, actual);
        Assert.True(equal, $"Colors {expected} and {actual} are not equal at index {i}");
    }

    private static void ValidateRgb(in JpegColorConverterBase.ComponentValues values, in JpegColorConverterBase.ComponentValues result, int i)
    {
        float r = values.Component0[i] / MaxColorChannelValue;
        float g = values.Component1[i] / MaxColorChannelValue;
        float b = values.Component2[i] / MaxColorChannelValue;
        Rgb expected = Rgb.Clamp(new(r, g, b));

        Rgb actual = Rgb.Clamp(new(result.Component0[i], result.Component1[i], result.Component2[i]));

        bool equal = ColorSpaceComparer.Equals(expected, actual);
        Assert.True(equal, $"Colors {expected} and {actual} are not equal at index {i}");
    }

    private static void ValidateGrayScale(in JpegColorConverterBase.ComponentValues values, in JpegColorConverterBase.ComponentValues result, int i)
    {
        float y = values.Component0[i] / MaxColorChannelValue;
        Rgb expected = Rgb.Clamp(new(y, y, y));

        Rgb actual = Rgb.Clamp(new(result.Component0[i], result.Component0[i], result.Component0[i]));

        bool equal = ColorSpaceComparer.Equals(expected, actual);
        Assert.True(equal, $"Colors {expected} and {actual} are not equal at index {i}");
    }

    private static void ValidateCmyk(in JpegColorConverterBase.ComponentValues values, in JpegColorConverterBase.ComponentValues result, int i)
    {
        float c = values.Component0[i];
        float m = values.Component1[i];
        float y = values.Component2[i];
        float k = values.Component3[i] / MaxColorChannelValue;

        float r = c * k / MaxColorChannelValue;
        float g = m * k / MaxColorChannelValue;
        float b = y * k / MaxColorChannelValue;
        Rgb expected = Rgb.Clamp(new(r, g, b));

        Rgb actual = Rgb.Clamp(new(result.Component0[i], result.Component1[i], result.Component2[i]));

        bool equal = ColorSpaceComparer.Equals(expected, actual);
        Assert.True(equal, $"Colors {expected} and {actual} are not equal at index {i}");
    }
}
