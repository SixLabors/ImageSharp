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

    private const HwIntrinsics IntrinsicsConfig = HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX2;

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
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableSSE2 | HwIntrinsics.DisableHWIntrinsic);

        static void RunTest(string arg)
        {
            // arrange
            Type expectedType = typeof(JpegColorConverterBase.RgbScalar);
            if (JpegColorConverterBase.JpegColorConverterVector512.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.RgbVector512);
            }
            else if (JpegColorConverterBase.JpegColorConverterVector256.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.RgbVector256);
            }
            else if (JpegColorConverterBase.JpegColorConverterVector128.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.RgbVector128);
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
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableSSE2 | HwIntrinsics.DisableHWIntrinsic);

        static void RunTest(string arg)
        {
            // arrange
            Type expectedType = typeof(JpegColorConverterBase.GrayScaleScalar);
            if (JpegColorConverterBase.JpegColorConverterVector512.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.GrayScaleVector512);
            }
            else if (JpegColorConverterBase.JpegColorConverterVector256.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.GrayScaleVector256);
            }
            else if (JpegColorConverterBase.JpegColorConverterVector128.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.GrayScaleVector128);
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
            if (JpegColorConverterBase.JpegColorConverterVector512.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.CmykVector512);
            }
            else if (JpegColorConverterBase.JpegColorConverterVector256.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.CmykVector256);
            }
            else if (JpegColorConverterBase.JpegColorConverterVector128.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.CmykVector128);
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
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableSSE2 | HwIntrinsics.DisableHWIntrinsic);

        static void RunTest(string arg)
        {
            // arrange
            Type expectedType = typeof(JpegColorConverterBase.YCbCrScalar);
            if (JpegColorConverterBase.JpegColorConverterVector512.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.YCbCrVector512);
            }
            else if (JpegColorConverterBase.JpegColorConverterVector256.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.YCbCrVector256);
            }
            else if (JpegColorConverterBase.JpegColorConverterVector128.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.YCbCrVector128);
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
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableSSE2 | HwIntrinsics.DisableHWIntrinsic);

        static void RunTest(string arg)
        {
            // arrange
            Type expectedType = typeof(JpegColorConverterBase.YccKScalar);
            if (JpegColorConverterBase.JpegColorConverterVector512.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.YccKVector512);
            }
            else if (JpegColorConverterBase.JpegColorConverterVector256.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.YccKVector256);
            }
            else if (JpegColorConverterBase.JpegColorConverterVector128.IsSupported)
            {
                expectedType = typeof(JpegColorConverterBase.YccKVector128);
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
    public void FromYCbCrVector512(int seed) =>
        this.TestConversionToRgb(
            new JpegColorConverterBase.YCbCrVector512(8),
            3,
            seed,
            new JpegColorConverterBase.YCbCrScalar(8));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromYCbCrVector256(int seed) =>
        this.TestConversionToRgb(
            new JpegColorConverterBase.YCbCrVector256(8),
            3,
            seed,
            new JpegColorConverterBase.YCbCrScalar(8));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromYCbCrVector128(int seed) =>
        this.TestConversionToRgb(
            new JpegColorConverterBase.YCbCrVector128(8),
            3,
            seed,
            new JpegColorConverterBase.YCbCrScalar(8));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbToYCbCrVector512(int seed) =>
        this.TestConversionFromRgb(
            new JpegColorConverterBase.YCbCrVector512(8),
            3,
            seed,
            new JpegColorConverterBase.YCbCrScalar(8),
            precision: 2);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbToYCbCrVector256(int seed) =>
    this.TestConversionFromRgb(
        new JpegColorConverterBase.YCbCrVector256(8),
        3,
        seed,
        new JpegColorConverterBase.YCbCrScalar(8),
        precision: 2);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbToYCbCrVector128(int seed) =>
    this.TestConversionFromRgb(
        new JpegColorConverterBase.YCbCrVector128(8),
        3,
        seed,
        new JpegColorConverterBase.YCbCrScalar(8),
        precision: 2);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromCmykBasic(int seed) =>
        this.TestConversionToRgb(new JpegColorConverterBase.CmykScalar(8), 4, seed);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromCmykVector512(int seed) =>
        this.TestConversionToRgb(
            new JpegColorConverterBase.CmykVector512(8),
            4,
            seed,
            new JpegColorConverterBase.CmykScalar(8));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromCmykVector256(int seed) =>
        this.TestConversionToRgb(
            new JpegColorConverterBase.CmykVector256(8),
            4,
            seed,
            new JpegColorConverterBase.CmykScalar(8));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromCmykVector128(int seed) =>
        this.TestConversionToRgb(
            new JpegColorConverterBase.CmykVector128(8),
            4,
            seed,
            new JpegColorConverterBase.CmykScalar(8));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbToCmykVector512(int seed) =>
        this.TestConversionFromRgb(
            new JpegColorConverterBase.CmykVector512(8),
            4,
            seed,
            new JpegColorConverterBase.CmykScalar(8),
            precision: 2);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbToCmykVector256(int seed) =>
    this.TestConversionFromRgb(
        new JpegColorConverterBase.CmykVector256(8),
        4,
        seed,
        new JpegColorConverterBase.CmykScalar(8),
        precision: 2);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbToCmykVector128(int seed) =>
    this.TestConversionFromRgb(
        new JpegColorConverterBase.CmykVector128(8),
        4,
        seed,
        new JpegColorConverterBase.CmykScalar(8),
        precision: 2);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromGrayScaleBasic(int seed) =>
        this.TestConversionToRgb(new JpegColorConverterBase.GrayScaleScalar(8), 1, seed);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromGrayScaleVector512(int seed) =>
        this.TestConversionToRgb(
            new JpegColorConverterBase.GrayScaleVector512(8),
            1,
            seed,
            new JpegColorConverterBase.GrayScaleScalar(8));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromGrayScaleVector256(int seed) =>
        this.TestConversionToRgb(
            new JpegColorConverterBase.GrayScaleVector256(8),
            1,
            seed,
            new JpegColorConverterBase.GrayScaleScalar(8));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromGrayScaleVector128(int seed) =>
        this.TestConversionToRgb(
            new JpegColorConverterBase.GrayScaleVector128(8),
            1,
            seed,
            new JpegColorConverterBase.GrayScaleScalar(8));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbToGrayScaleVector512(int seed) =>
        this.TestConversionFromRgb(
            new JpegColorConverterBase.GrayScaleVector512(8),
            1,
            seed,
            new JpegColorConverterBase.GrayScaleScalar(8),
            precision: 2);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbToGrayScaleVector256(int seed) =>
    this.TestConversionFromRgb(
        new JpegColorConverterBase.GrayScaleVector256(8),
        1,
        seed,
        new JpegColorConverterBase.GrayScaleScalar(8),
        precision: 2);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbToGrayScaleVector128(int seed) =>
    this.TestConversionFromRgb(
        new JpegColorConverterBase.GrayScaleVector128(8),
        1,
        seed,
        new JpegColorConverterBase.GrayScaleScalar(8),
        precision: 2);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbBasic(int seed) =>
        this.TestConversionToRgb(new JpegColorConverterBase.RgbScalar(8), 3, seed);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbVector512(int seed) =>
        this.TestConversionToRgb(
            new JpegColorConverterBase.RgbVector512(8),
            3,
            seed,
            new JpegColorConverterBase.RgbScalar(8));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbVector256(int seed) =>
        this.TestConversionToRgb(
            new JpegColorConverterBase.RgbVector256(8),
            3,
            seed,
            new JpegColorConverterBase.RgbScalar(8));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbVector128(int seed) =>
        this.TestConversionToRgb(
            new JpegColorConverterBase.RgbVector128(8),
            3,
            seed,
            new JpegColorConverterBase.RgbScalar(8));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbToRgbVector512(int seed) =>
        this.TestConversionFromRgb(
            new JpegColorConverterBase.RgbVector512(8),
            3,
            seed,
            new JpegColorConverterBase.RgbScalar(8),
            precision: 2);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbToRgbVector256(int seed) =>
    this.TestConversionFromRgb(
        new JpegColorConverterBase.RgbVector256(8),
        3,
        seed,
        new JpegColorConverterBase.RgbScalar(8),
        precision: 2);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbToRgbVector128(int seed) =>
    this.TestConversionFromRgb(
        new JpegColorConverterBase.RgbVector128(8),
        3,
        seed,
        new JpegColorConverterBase.RgbScalar(8),
        precision: 2);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromYccKBasic(int seed) =>
        this.TestConversionToRgb(new JpegColorConverterBase.YccKScalar(8), 4, seed);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromYccKVector512(int seed) =>
        this.TestConversionToRgb(
            new JpegColorConverterBase.YccKVector512(8),
            4,
            seed,
            new JpegColorConverterBase.YccKScalar(8));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromYccKVector256(int seed) =>
        this.TestConversionToRgb(
            new JpegColorConverterBase.YccKVector256(8),
            4,
            seed,
            new JpegColorConverterBase.YccKScalar(8));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromYccKVector128(int seed) =>
        this.TestConversionToRgb(
            new JpegColorConverterBase.YccKVector128(8),
            4,
            seed,
            new JpegColorConverterBase.YccKScalar(8));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbToYccKVector512(int seed) =>
        this.TestConversionFromRgb(
            new JpegColorConverterBase.YccKVector512(8),
            4,
            seed,
            new JpegColorConverterBase.YccKScalar(8),
            precision: 2);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbToYccKVector256(int seed) =>
    this.TestConversionFromRgb(
        new JpegColorConverterBase.YccKVector256(8),
        4,
        seed,
        new JpegColorConverterBase.YccKScalar(8),
        precision: 2);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void FromRgbToYccKVector128(int seed) =>
    this.TestConversionFromRgb(
        new JpegColorConverterBase.YccKVector128(8),
        4,
        seed,
        new JpegColorConverterBase.YccKScalar(8),
        precision: 2);

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
        int precision)
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
            precision);
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

        converter.ConvertToRgbInPlace(actual);

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
            baseLineConverter.ConvertToRgbInPlace(expected);
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
                Assert.Fail($"Invalid Colorspace enum value: {colorSpace}.");
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
