// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Tests.ColorProfiles;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg;

[Trait("Format", "Jpg")]
public class JpegColorConverterTests
{
    private const float MaxColorChannelValue = 255F;
    private const float ColorProfileTolerance = 0.1F / MaxColorChannelValue;
    private const float ToRgbTolerance = 0.0001F;
    private const float FromRgbTolerance = 0.01F;

    // Independent model checks compare normalized colors at one tenth of a byte-domain sample.
    private static readonly ApproximateColorProfileComparer ColorSpaceComparer = new(epsilon: ColorProfileTolerance);

    /// <summary>
    /// Verifies that unsupported color spaces are rejected by the converter factory.
    /// </summary>
    [Fact]
    public void GetConverterThrowsExceptionOnInvalidColorSpace()
    {
        const JpegColorSpace invalidColorSpace = (JpegColorSpace)(-1);

        Assert.Throws<InvalidImageContentException>(() => JpegColorConverterBase.GetConverter(invalidColorSpace, 8));
    }

    /// <summary>
    /// Verifies that unsupported JPEG sample precisions are rejected by the converter factory.
    /// </summary>
    [Fact]
    public void GetConverterThrowsExceptionOnInvalidPrecision()
    {
        const int invalidPrecision = 9;

        Assert.Throws<InvalidImageContentException>(() => JpegColorConverterBase.GetConverter(JpegColorSpace.YCbCr, invalidPrecision));
    }

    /// <summary>
    /// Verifies that every supported color-space and precision pair resolves to the shared operator converter.
    /// </summary>
    /// <param name="colorSpace">The JPEG color space.</param>
    /// <param name="precision">The JPEG sample precision.</param>
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
    [InlineData(JpegColorSpace.TiffCmyk, 8)]
    [InlineData(JpegColorSpace.TiffCmyk, 12)]
    [InlineData(JpegColorSpace.TiffYccK, 8)]
    [InlineData(JpegColorSpace.TiffYccK, 12)]
    internal void GetConverterReturnsValidConverter(JpegColorSpace colorSpace, int precision)
    {
        JpegColorConverterBase converter = JpegColorConverterBase.GetConverter(colorSpace, precision);

        Assert.True(converter.IsAvailable);
        Assert.Equal(colorSpace, converter.ColorSpace);
        Assert.Equal(precision, converter.Precision);
    }

    /// <summary>
    /// Verifies that the converter factory closes the shared traversal over the matching color-model operator.
    /// </summary>
    /// <param name="colorSpace">The JPEG color space.</param>
    /// <param name="expectedType">The expected closed converter type.</param>
    [Theory]
    [InlineData(JpegColorSpace.Grayscale, typeof(JpegColorConverterBase.JpegColorConverter<JpegColorConverterBase.GrayScaleOperator>))]
    [InlineData(JpegColorSpace.RGB, typeof(JpegColorConverterBase.JpegColorConverter<JpegColorConverterBase.RgbOperator>))]
    [InlineData(JpegColorSpace.Cmyk, typeof(JpegColorConverterBase.JpegColorConverter<JpegColorConverterBase.CmykOperator>))]
    [InlineData(JpegColorSpace.YCbCr, typeof(JpegColorConverterBase.JpegColorConverter<JpegColorConverterBase.YCbCrOperator>))]
    [InlineData(JpegColorSpace.Ycck, typeof(JpegColorConverterBase.JpegColorConverter<JpegColorConverterBase.YccKOperator>))]
    [InlineData(JpegColorSpace.TiffCmyk, typeof(JpegColorConverterBase.JpegColorConverter<JpegColorConverterBase.TiffCmykOperator>))]
    [InlineData(JpegColorSpace.TiffYccK, typeof(JpegColorConverterBase.JpegColorConverter<JpegColorConverterBase.TiffYccKOperator>))]
    internal void GetConverterReturnsClosedOperatorConverter(JpegColorSpace colorSpace, Type expectedType)
    {
        JpegColorConverterBase converter = JpegColorConverterBase.GetConverter(colorSpace, 8);

        Assert.Equal(expectedType, converter.GetType());
    }

    /// <summary>
    /// Verifies the replacement converter against independent definitions of the established JPEG color models.
    /// </summary>
    /// <param name="colorSpace">The JPEG color space.</param>
    /// <param name="componentCount">The number of component planes owned by the color model.</param>
    [Theory]
    [InlineData(JpegColorSpace.Grayscale, 1)]
    [InlineData(JpegColorSpace.RGB, 3)]
    [InlineData(JpegColorSpace.Cmyk, 4)]
    [InlineData(JpegColorSpace.YCbCr, 3)]
    [InlineData(JpegColorSpace.Ycck, 4)]
    internal void ConvertToRgbMatchesColorModelDefinition(JpegColorSpace colorSpace, int componentCount)
    {
        const int length = 128;
        JpegColorConverterBase.ComponentValues source = CreateRandomValues(length, componentCount, 8);
        JpegColorConverterBase.ComponentValues actual = CreateRandomValues(length, componentCount, 8);
        JpegColorConverterBase converter = JpegColorConverterBase.GetConverter(colorSpace, 8);

        converter.ConvertToRgbInPlace(actual);

        for (int i = 0; i < length; i++)
        {
            AssertColorModelDefinition(colorSpace, source, actual, i);
        }
    }

    /// <summary>
    /// Verifies the adaptive traversal against each operator's scalar definition around every SIMD boundary.
    /// </summary>
    /// <param name="colorSpace">The JPEG color space.</param>
    /// <param name="componentCount">The number of component planes owned by the color model.</param>
    /// <param name="precision">The JPEG sample precision.</param>
    [Theory]
    [InlineData(JpegColorSpace.Grayscale, 1, 8)]
    [InlineData(JpegColorSpace.Grayscale, 1, 12)]
    [InlineData(JpegColorSpace.RGB, 3, 8)]
    [InlineData(JpegColorSpace.RGB, 3, 12)]
    [InlineData(JpegColorSpace.Cmyk, 4, 8)]
    [InlineData(JpegColorSpace.Cmyk, 4, 12)]
    [InlineData(JpegColorSpace.YCbCr, 3, 8)]
    [InlineData(JpegColorSpace.YCbCr, 3, 12)]
    [InlineData(JpegColorSpace.Ycck, 4, 8)]
    [InlineData(JpegColorSpace.Ycck, 4, 12)]
    [InlineData(JpegColorSpace.TiffCmyk, 4, 8)]
    [InlineData(JpegColorSpace.TiffCmyk, 4, 12)]
    [InlineData(JpegColorSpace.TiffYccK, 4, 8)]
    [InlineData(JpegColorSpace.TiffYccK, 4, 12)]
    internal void OperatorTraversalMatchesScalarDefinition(JpegColorSpace colorSpace, int componentCount, int precision)
    {
        switch (colorSpace)
        {
            case JpegColorSpace.Grayscale:
                ValidateOperator<JpegColorConverterBase.GrayScaleOperator>(componentCount, precision);
                break;
            case JpegColorSpace.RGB:
                ValidateOperator<JpegColorConverterBase.RgbOperator>(componentCount, precision);
                break;
            case JpegColorSpace.Cmyk:
                ValidateOperator<JpegColorConverterBase.CmykOperator>(componentCount, precision);
                break;
            case JpegColorSpace.YCbCr:
                ValidateOperator<JpegColorConverterBase.YCbCrOperator>(componentCount, precision);
                break;
            case JpegColorSpace.Ycck:
                ValidateOperator<JpegColorConverterBase.YccKOperator>(componentCount, precision);
                break;
            case JpegColorSpace.TiffCmyk:
                ValidateOperator<JpegColorConverterBase.TiffCmykOperator>(componentCount, precision);
                break;
            case JpegColorSpace.TiffYccK:
                ValidateOperator<JpegColorConverterBase.TiffYccKOperator>(componentCount, precision);
                break;
            default:
                Assert.Fail($"Unexpected JPEG color space: {colorSpace}.");
                break;
        }
    }

    /// <summary>
    /// Verifies that the shared converter retains its scalar behavior when hardware intrinsics are disabled.
    /// </summary>
    [Fact]
    public void OperatorTraversalMatchesScalarWithoutHardwareIntrinsics()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunWithoutHardwareIntrinsics, HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Verifies TIFF YccK encoding against the canonical normalized color-profile conversion.
    /// </summary>
    [Fact]
    public void TiffYccKOperatorFromRgbMatchesColorProfileDefinition()
    {
        const int maximumLength = 40;
        const float maximumValue = 255F;
        const float halfValue = 128F;
        float[] rSeed = [0, 255, 255, 0, 0, 127, 32, 240, 0, 255, 255, 0, 0, 127, 32, 240, 0, 255, 64, 192];
        float[] gSeed = [0, 255, 0, 255, 0, 127, 160, 16, 0, 255, 0, 255, 0, 127, 160, 16, 255, 0, 128, 96];
        float[] bSeed = [0, 255, 0, 0, 255, 127, 224, 80, 255, 0, 255, 0, 0, 127, 224, 80, 0, 255, 192, 32];
        float[] r = new float[maximumLength];
        float[] g = new float[maximumLength];
        float[] b = new float[maximumLength];
        JpegColorConverterBase converter = JpegColorConverterBase.GetConverter(JpegColorSpace.TiffYccK, 8);

        // Repeating the color set provides enough lanes to exercise every SIMD width and mixed-width tail.
        rSeed.CopyTo(r, 0);
        rSeed.CopyTo(r, rSeed.Length);
        gSeed.CopyTo(g, 0);
        gSeed.CopyTo(g, gSeed.Length);
        bSeed.CopyTo(b, 0);
        bSeed.CopyTo(b, bSeed.Length);

        ColorProfileConverter reference = new();
        int[] lengths = [1, 3, 4, 7, 8, 15, 16, 31, 32, 40];

        foreach (int length in lengths)
        {
            float[] y = new float[length];
            float[] cb = new float[length];
            float[] cr = new float[length];
            float[] k = new float[length];
            JpegColorConverterBase.ComponentValues values = new(4, y, cb, cr, k);

            converter.ConvertFromRgb(values, r.AsSpan(0, length), g.AsSpan(0, length), b.AsSpan(0, length));

            for (int i = 0; i < length; i++)
            {
                Rgb rgb = new(r[i] / maximumValue, g[i] / maximumValue, b[i] / maximumValue);
                YccK expected = reference.Convert<Rgb, YccK>(rgb);

                Assert.Equal(expected.Y * maximumValue, y[i], ToRgbTolerance);

                // JPEG centers chroma on the integer midpoint, while the color-profile definition uses exactly 0.5.
                Assert.Equal(halfValue + ((expected.Cb - 0.5F) * maximumValue), cb[i], ToRgbTolerance);
                Assert.Equal(halfValue + ((expected.Cr - 0.5F) * maximumValue), cr[i], ToRgbTolerance);
                Assert.Equal(expected.K * maximumValue, k[i], ToRgbTolerance);
            }
        }
    }

    /// <summary>
    /// Runs the disabled-intrinsics scalar comparison inside the feature-test process.
    /// </summary>
    /// <param name="arg">The unused feature-test argument.</param>
    private static void RunWithoutHardwareIntrinsics(string arg)
        => ValidateOperator<JpegColorConverterBase.YCbCrOperator>(3, 8);

    /// <summary>
    /// Checks one closed operator converter at every scalar and SIMD transition length.
    /// </summary>
    /// <typeparam name="TOperator">The color-model operator under test.</typeparam>
    /// <param name="componentCount">The number of component planes owned by the operator.</param>
    /// <param name="precision">The JPEG sample precision.</param>
    private static void ValidateOperator<TOperator>(int componentCount, int precision)
        where TOperator : struct, JpegColorConverterBase.IJpegColorConverterOperator
    {
        JpegColorConverterBase converter = new JpegColorConverterBase.JpegColorConverter<TOperator>(precision);
        int[] lengths = [1, 3, 4, 7, 8, 15, 16, 31, 32, 40, 64, 128];

        // Adjacent values around 4/8/16 lanes verify every prefix, mixed-width tail, and scalar remainder.
        foreach (int length in lengths)
        {
            ValidateConversionToRgb<TOperator>(converter, length, componentCount, precision);
            ValidateConversionFromRgb<TOperator>(converter, length, componentCount, precision);
        }
    }

    /// <summary>
    /// Compares the adaptive component-to-RGB traversal with repeated scalar operator calls.
    /// </summary>
    /// <typeparam name="TOperator">The color-model operator under test.</typeparam>
    /// <param name="converter">The adaptive converter.</param>
    /// <param name="length">The number of samples to convert.</param>
    /// <param name="componentCount">The number of source component planes.</param>
    /// <param name="precision">The JPEG sample precision.</param>
    private static void ValidateConversionToRgb<TOperator>(JpegColorConverterBase converter, int length, int componentCount, int precision)
        where TOperator : struct, JpegColorConverterBase.IJpegColorConverterOperator
    {
        JpegColorConverterBase.ComponentValues expected = CreateRandomValues(length, componentCount, precision);
        JpegColorConverterBase.ComponentValues actual = CreateRandomValues(length, componentCount, precision);
        float maximumValue = MathF.Pow(2, precision) - 1;
        float halfValue = MathF.Ceiling(maximumValue * 0.5F);
        float scale = 1F / maximumValue;

        for (int i = 0; i < length; i++)
        {
            ref float c0 = ref expected.Component0[i];
            ref float c1 = ref expected.Component1[i];
            ref float c2 = ref expected.Component2[i];
            float c3 = componentCount == 4 ? expected.Component3[i] : 0;

            TOperator.ConvertToRgb(ref c0, ref c1, ref c2, c3, maximumValue, halfValue, scale);
        }

        converter.ConvertToRgbInPlace(actual);

        // YCbCr conversion rounds in the integer sample domain before normalization. Fused SIMD arithmetic can
        // cross a half-way boundary differently from the scalar expression, so allow one source-sample quantum
        // in addition to the ordinary floating-point tolerance at both supported sample precisions.
        float tolerance = scale + ToRgbTolerance;
        CompareSequence(expected.Component0, actual.Component0, tolerance);
        CompareSequence(expected.Component1, actual.Component1, tolerance);
        CompareSequence(expected.Component2, actual.Component2, tolerance);
    }

    /// <summary>
    /// Compares the adaptive RGB-to-component traversal with repeated scalar operator calls.
    /// </summary>
    /// <typeparam name="TOperator">The color-model operator under test.</typeparam>
    /// <param name="converter">The adaptive converter.</param>
    /// <param name="length">The number of samples to convert.</param>
    /// <param name="componentCount">The number of destination component planes.</param>
    /// <param name="precision">The JPEG sample precision.</param>
    private static void ValidateConversionFromRgb<TOperator>(JpegColorConverterBase converter, int length, int componentCount, int precision)
        where TOperator : struct, JpegColorConverterBase.IJpegColorConverterOperator
    {
        JpegColorConverterBase.ComponentValues expected = CreateRandomValues(length, componentCount, precision);
        JpegColorConverterBase.ComponentValues actual = CreateRandomValues(length, componentCount, precision);
        Random random = new(precision);
        float[] r = CreateRandomValues(length, random);
        float[] g = CreateRandomValues(length, random);
        float[] b = CreateRandomValues(length, random);
        float maximumValue = MathF.Pow(2, precision) - 1;
        float halfValue = MathF.Ceiling(maximumValue * 0.5F);
        float scale = 1F / maximumValue;

        for (int i = 0; i < length; i++)
        {
            TOperator.ConvertFromRgb(r[i], g[i], b[i], maximumValue, halfValue, scale, out expected.Component0[i], out float c1, out float c2, out float c3);

            if (componentCount >= 2)
            {
                expected.Component1[i] = c1;
            }

            if (componentCount >= 3)
            {
                expected.Component2[i] = c2;
            }

            if (componentCount == 4)
            {
                expected.Component3[i] = c3;
            }
        }

        converter.ConvertFromRgb(actual, r, g, b);
        CompareSequence(expected.Component0, actual.Component0, FromRgbTolerance);

        if (componentCount >= 2)
        {
            CompareSequence(expected.Component1, actual.Component1, FromRgbTolerance);
        }

        if (componentCount >= 3)
        {
            CompareSequence(expected.Component2, actual.Component2, FromRgbTolerance);
        }

        if (componentCount == 4)
        {
            CompareSequence(expected.Component3, actual.Component3, FromRgbTolerance);
        }
    }

    /// <summary>
    /// Creates deterministic component planes in the configured JPEG sample domain.
    /// </summary>
    /// <param name="length">The number of samples in each plane.</param>
    /// <param name="componentCount">The number of independent component planes.</param>
    /// <param name="precision">The JPEG sample precision and deterministic random seed.</param>
    /// <returns>The generated component planes.</returns>
    private static JpegColorConverterBase.ComponentValues CreateRandomValues(int length, int componentCount, int precision)
    {
        Random random = new(precision);
        float maximumValue = MathF.Pow(2, precision) - 1;
        float[] c0 = CreateRandomValues(length, random, maximumValue);
        float[] c1 = componentCount >= 2 ? CreateRandomValues(length, random, maximumValue) : c0;
        float[] c2 = componentCount >= 3 ? CreateRandomValues(length, random, maximumValue) : c0;
        float[] c3 = componentCount == 4 ? CreateRandomValues(length, random, maximumValue) : [];

        return new JpegColorConverterBase.ComponentValues(componentCount, c0, c1, c2, c3);
    }

    /// <summary>
    /// Creates deterministic RGB samples in ImageSharp's byte-scaled encoder domain.
    /// </summary>
    /// <param name="length">The number of samples.</param>
    /// <param name="random">The deterministic random source.</param>
    /// <returns>The generated samples.</returns>
    private static float[] CreateRandomValues(int length, Random random)
        => CreateRandomValues(length, random, 255F);

    /// <summary>
    /// Creates deterministic samples between zero and the supplied inclusive domain maximum.
    /// </summary>
    /// <param name="length">The number of samples.</param>
    /// <param name="random">The deterministic random source.</param>
    /// <param name="maximumValue">The upper bound of the sample domain.</param>
    /// <returns>The generated samples.</returns>
    private static float[] CreateRandomValues(int length, Random random, float maximumValue)
    {
        float[] values = new float[length];

        for (int i = 0; i < values.Length; i++)
        {
            values[i] = random.NextSingle() * maximumValue;
        }

        return values;
    }

    /// <summary>
    /// Compares one converted sample with an independent definition of its JPEG color model.
    /// </summary>
    /// <param name="colorSpace">The JPEG color space.</param>
    /// <param name="source">The unmodified source component planes.</param>
    /// <param name="actual">The converted RGB planes.</param>
    /// <param name="index">The sample index.</param>
    private static void AssertColorModelDefinition(JpegColorSpace colorSpace, in JpegColorConverterBase.ComponentValues source, in JpegColorConverterBase.ComponentValues actual, int index)
    {
        float c0 = source.Component0[index];
        float c1 = source.Component1[index];
        float c2 = source.Component2[index];
        float c3 = 0;
        Rgb expected;

        switch (colorSpace)
        {
            case JpegColorSpace.Grayscale:
                float luminance = c0 / MaxColorChannelValue;
                expected = new Rgb(luminance, luminance, luminance);
                break;
            case JpegColorSpace.RGB:
                expected = new Rgb(c0 / MaxColorChannelValue, c1 / MaxColorChannelValue, c2 / MaxColorChannelValue);

                break;
            case JpegColorSpace.Cmyk:
                c3 = source.Component3[index] / MaxColorChannelValue;
                expected = new Rgb(c0 * c3 / MaxColorChannelValue, c1 * c3 / MaxColorChannelValue, c2 * c3 / MaxColorChannelValue);

                break;
            case JpegColorSpace.YCbCr:
                c1 -= 128F;
                c2 -= 128F;

                // JPEG applies the BT.601 matrix in the integer sample domain and rounds before normalization.
                expected = new Rgb(MathF.Round(c0 + (1.402F * c2), MidpointRounding.AwayFromZero) / MaxColorChannelValue, MathF.Round(c0 - (0.344136F * c1) - (0.714136F * c2), MidpointRounding.AwayFromZero) / MaxColorChannelValue, MathF.Round(c0 + (1.772F * c1), MidpointRounding.AwayFromZero) / MaxColorChannelValue);

                break;
            case JpegColorSpace.Ycck:
                c1 -= 128F;
                c2 -= 128F;
                c3 = source.Component3[index] / MaxColorChannelValue;

                // Adobe YccK reconstructs inverted RGB first, then applies the normalized black component.
                expected = new Rgb((MaxColorChannelValue - MathF.Round(c0 + (1.402F * c2), MidpointRounding.AwayFromZero)) * c3 / MaxColorChannelValue, (MaxColorChannelValue - MathF.Round(c0 - (0.344136F * c1) - (0.714136F * c2), MidpointRounding.AwayFromZero)) * c3 / MaxColorChannelValue, (MaxColorChannelValue - MathF.Round(c0 + (1.772F * c1), MidpointRounding.AwayFromZero)) * c3 / MaxColorChannelValue);

                break;
            default:
                Assert.Fail($"Unexpected JPEG color space: {colorSpace}.");
                return;
        }

        // Color-space comparison intentionally clamps both sides because JPEG reconstruction can overshoot
        // the normalized RGB gamut and saturation belongs to the eventual pixel conversion.
        Rgb clampedExpected = Rgb.Clamp(expected);
        Rgb clampedActual = Rgb.Clamp(new Rgb(actual.Component0[index], actual.Component1[index], actual.Component2[index]));

        Assert.True(ColorSpaceComparer.Equals(clampedExpected, clampedActual), $"Colors {clampedExpected} and {clampedActual} are not equal at index {index}.");
    }

    /// <summary>
    /// Compares two component planes using an absolute floating-point tolerance.
    /// </summary>
    /// <param name="expected">The scalar reference values.</param>
    /// <param name="actual">The adaptive traversal values.</param>
    /// <param name="tolerance">The maximum permitted absolute difference.</param>
    private static void CompareSequence(Span<float> expected, Span<float> actual, float tolerance)
    {
        Assert.Equal(expected.Length, actual.Length);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i], tolerance);
        }
    }
}
