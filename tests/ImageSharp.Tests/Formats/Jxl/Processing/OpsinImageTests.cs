// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

/// <summary>
/// Tests the sRGB to XYB and vice versa color conversion.
/// </summary>
public class OpsinImageTests
{
    /// <summary>
    /// Converts the linear sRGB color space to JPEG XL Opsin XYB.
    /// </summary>
    /// <param name="rgbR">Input Red channel</param>
    /// <param name="rgbG">Input Green channel</param>
    /// <param name="rgbB">Input Blue channel</param>
    /// <param name="xybX">Output X channel</param>
    /// <param name="xybY">Output Y channel</param>
    /// <param name="xybB">Output B channel</param>
    private static void LinearSRgbToOpsin(float rgbR, float rgbG, float rgbB, out float xybX, out float xybY, out float xybB)
    {
        using JxlImage3<float> linear = new(TestEnvironment.Configuration, 1, 1);
        linear.PlaneRow(0, 0)[0] = rgbR;
        linear.PlaneRow(1, 0)[0] = rgbG;
        linear.PlaneRow(2, 0)[0] = rgbB;
        Assert.True(JxlXybEncoder.ToXyb(
            TestEnvironment.Configuration,
            JxlColorEncoding.LinearSRgb,
            JxlShared.DefaultIntensityTarget,
            null,
            new JxlImage3<float>(),
            JxlCmsInterface.CreateDefaultCms(),
            null));
        xybX = linear.PlaneRow(0, 0)[0];
        xybY = linear.PlaneRow(1, 0)[0];
        xybB = linear.PlaneRow(2, 0)[0];
    }

    /// <summary>
    /// Converts JPEG XL Opsin XYB color space to linear sRGB. (Opposite
    /// of <see cref="LinearSRgbToOpsin(float, float, float, out float, out float, out float)"/>).
    /// </summary>
    /// <param name="xybX">Input X channel</param>
    /// <param name="xybY">Input Y channel</param>
    /// <param name="xybB">Input B channel</param>
    /// <param name="rgbR">Output Red channel</param>
    /// <param name="rgbG">Output Green channel</param>
    /// <param name="rgbB">Output Blue channel</param>
    private static void OpsinToLinearSRgb(float xybX, float xybY, float xybB, out float rgbR, out float rgbG, out float rgbB)
    {
        using JxlImage3<float> opsin = new(TestEnvironment.Configuration, 1, 1);
        opsin.PlaneRow(0, 0)[0] = xybX;
        opsin.PlaneRow(1, 0)[0] = xybY;
        opsin.PlaneRow(2, 0)[0] = xybB;

        using JxlImage3<float> linear = new(TestEnvironment.Configuration, 1, 1);

        JxlOpsinParameters opsinParameters = new();
        JxlOpsinInverseMatrix inverse = new();
        opsinParameters.Initialize(inverse.InverseMatrix, inverse.OpsinBiases, 255F);

        Assert.True(JxlXybDecoder.OpsinToLinear(
            opsin,
            opsin.GetRectangle(),
            linear,
            opsinParameters));

        rgbR = linear.PlaneRow(0, 0)[0];
        rgbG = linear.PlaneRow(1, 0)[0];
        rgbB = linear.PlaneRow(2, 0)[0];
    }

    private static void OpsinRoundtripTestRgb(float r, float g, float b)
    {
        // There will be a tiny floating-point gap. This is normal.
        const float tolerance = 1e-3f;

        LinearSRgbToOpsin(r, g, b, out float xybX, out float xybY, out float xybB);
        OpsinToLinearSRgb(xybX, xybY, xybB, out float r2, out float g2, out float b2);

        Assert.Equal(r, r2, tolerance);
        Assert.Equal(g, g2, tolerance);
        Assert.Equal(b, b2, tolerance);
    }

    [Fact]
    public void VerifyOpsinAbsorbanceInverseMatrix()
    {
        const float tolerance = 1e-6f;

        JxlMatrix3x3F matrix = JxlOpsinConstants.GetOpsinAbsorbanceInverseMatrix();

        Assert.True(JxlMatrix3x3F.Invert(ref matrix));

        for (int j = 0; j < 3; j++)
        {
            for (int i = 0; i < 3; i++)
            {
                Assert.Equal(
                    matrix.DangerousGetReferenceTo(j, i),
                    JxlOpsinConstants.OpsinAbsorbanceMatrix[j][i],
                    tolerance);
            }
        }
    }

    [Fact]
    public void OpsinRoundtrip()
    {
        OpsinRoundtripTestRgb(0, 0, 0);
        OpsinRoundtripTestRgb(1.0f / 255, 1.0f / 255, 1.0f / 255);
        OpsinRoundtripTestRgb(128.0f / 255, 128.0f / 255, 128.0f / 255);
        OpsinRoundtripTestRgb(1, 1, 1);

        OpsinRoundtripTestRgb(0, 0, 1.0f / 255);
        OpsinRoundtripTestRgb(0, 0, 128.0f / 255);
        OpsinRoundtripTestRgb(0, 0, 1);

        OpsinRoundtripTestRgb(0, 1.0f / 255, 0);
        OpsinRoundtripTestRgb(0, 128.0f / 255, 0);
        OpsinRoundtripTestRgb(0, 1, 0);

        OpsinRoundtripTestRgb(1.0f / 255, 0, 0);
        OpsinRoundtripTestRgb(128.0f / 255, 0, 0);
        OpsinRoundtripTestRgb(1, 0, 0);
    }

    /// <summary>
    /// Tests that Linear sRGB { 0, 0, 0 } to Opsin produces
    /// values equal to or extremely close to { 0, 0, 0 }.
    /// </summary>
    [Fact]
    public void VerifyZero()
    {
        LinearSRgbToOpsin(0, 0, 0, out float x, out float y, out float b);
        Assert.Equal(0, x, 1e-9f);
        Assert.Equal(0, y, 1e-7f);
        Assert.Equal(0, b, 1e-7f);
    }

    /// <summary>
    /// Tests that grayscale colors have a fixed Y to B ratio and x == 0.
    /// </summary>
    [Fact]
    public void VerifyGray()
    {
        for (int i = 1; i < 255; i++)
        {
            LinearSRgbToOpsin(i / 255.0f, i / 255.0f, i / 255.0f, out float x, out float y, out float b);
            Assert.Equal(0, x, 1e-6f);
            Assert.Equal(JxlOpsinConstants.YToBRatio, b / y, 3e-5f);
        }
    }

    /// <summary>Checks bias subtraction, intensity scaling, rectangular origins and exact row guards at every SIMD capability.</summary>
    [Fact]
    public void InverseOpsin_ReconstructsDyadicSamplesWithinRectangle()
    {
        static void RunTest()
        {
            ReadOnlySpan<int> widths = [1, 3, 17, 33];
            ReadOnlySpan<float> intensities = [255F, 510F];
            ReadOnlySpan<float> xyb = [0F, 0F, 0F, 0.5F, 0.5F, 1F, -0.5F, 0.5F, -1F, 0.25F, 0.25F, -0.5F];

            // Bias -1 has cube root -1. Cubing (Y+X+1,Y-X+1,B+1), then subtracting one, gives exact dyadic LMS values.
            // The selected matrix gives these RGB triples independently: black; (21,-7,35)/4; (15,55,-18)/4; (83,-45,-9)/32.
            ReadOnlySpan<float> expected = [0F, 0F, 0F, 5.25F, -1.75F, 8.75F, 3.75F, 13.75F, -4.5F, 2.59375F, -1.40625F, -0.28125F];
            const float untouched = -777F;
            foreach (float intensity in intensities)
            {
                JxlOpsinParameters parameters = CreateDyadicOpsinParameters(intensity);
                foreach (int width in widths)
                {
                    using JxlImage3<float> source = new(TestEnvironment.Configuration, width + 19, 6);
                    using JxlImage3<float> destination = new(TestEnvironment.Configuration, width + 5, 5);
                    Assert.True(destination.ShrinkTo(width, 3));
                    Rectangle rectangle = new(2, 1, width, 3);
                    for (int c = 0; c < 3; c++)
                    {
                        Span<float> samples = source.PlaneRow(c, 0);
                        samples.Fill(untouched);
                        destination.PlaneRow(c, 0).Fill(untouched);
                        for (int y = 0; y < rectangle.Height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                samples[((y + 1) * source.PixelsPerRow) + x + 2] = xyb[(((x + (2 * y)) % 4) * 3) + c];
                            }
                        }
                    }

                    Assert.True(JxlXybDecoder.OpsinToLinear(source, rectangle, destination, parameters));
                    for (int c = 0; c < 3; c++)
                    {
                        ReadOnlySpan<float> samples = destination.PlaneRow(c, 0);
                        for (int i = 0; i < samples.Length; i++)
                        {
                            int y = i / destination.PixelsPerRow;
                            int x = i % destination.PixelsPerRow;
                            float value = y < 3 && x < width
                                ? expected[(((x + (2 * y)) % 4) * 3) + c] * (255F / intensity)
                                : untouched;

                            Assert.Equal(value, samples[i]);
                        }

                        // The out-of-place conversion must leave every source sample, its row prefix and its padding unchanged.
                        samples = source.PlaneRow(c, 0);
                        for (int i = 0; i < samples.Length; i++)
                        {
                            int y = (i / source.PixelsPerRow) - 1;
                            int x = (i % source.PixelsPerRow) - 2;
                            float value = y >= 0 && y < 3 && x >= 0 && x < width
                                ? xyb[(((x + (2 * y)) % 4) * 3) + c]
                                : untouched;

                            Assert.Equal(value, samples[i]);
                        }
                    }
                }
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>Checks in-place RGB and scaled-XYB stage output with zero halos, short rows and unchanged extra-channel storage.</summary>
    [Fact]
    public void XybStage_ConvertsLogicalRowsAndPreservesPadding()
    {
        static void RunTest()
        {
            ReadOnlySpan<int> widths = [1, 3, 17, 33];
            ReadOnlySpan<float> xyb = [0F, 0F, 0F, 0.5F, 0.5F, 1F, -0.5F, 0.5F, -1F, 0.25F, 0.25F, -0.5F];
            ReadOnlySpan<float> rgb = [0F, 0F, 0F, 5.25F, -1.75F, 8.75F, 3.75F, 13.75F, -4.5F, 2.59375F, -1.40625F, -0.28125F];
            const int origin = RenderPipelineStageBase.RenderPipelineXOffset;
            const float untouched = -777F;
            foreach (int width in widths)
            {
                for (int scaled = 0; scaled < 2; scaled++)
                {
                    int storageWidth = origin + width + 3;
                    using JxlPlane<float> storage = new(TestEnvironment.Configuration, storageWidth, 4);
                    Span<float> samples = storage.GetRow(0);
                    samples.Fill(untouched);
                    Memory<float>[] table = new Memory<float>[4];
                    for (int c = 0; c < table.Length; c++)
                    {
                        table[c] = storage.GetRowMemory(c)[..storageWidth];
                        if (c < 3)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                samples[(c * storage.PixelsPerRow) + origin + x] = xyb[((x % 4) * 3) + c];
                            }
                        }
                    }

                    // The row table borrows numeric storage. The stage reads no vertical or horizontal border samples and writes in place.
                    using Buffer2D<Memory<float>> rows = new(MemoryGroup<Memory<float>>.Wrap(table.AsMemory()), 4, 1);
                    JxlOutputEncodingInfo info = new()
                    {
                        OpsinParameters = CreateDyadicOpsinParameters(510F),
                        ColorEncoding = new() { ColorSpace = scaled == 0 ? JxlColorSpace.Rgb : JxlColorSpace.Xyb }
                    };

                    using XybStage stage = new(TestEnvironment.Configuration, info);
                    stage.ProcessRow(rows, rows, 0, 0, width, 0, 0);
                    for (int c = 0; c < 4; c++)
                    {
                        ReadOnlySpan<float> actual = samples.Slice(c * storage.PixelsPerRow, storage.PixelsPerRow);
                        for (int i = 0; i < actual.Length; i++)
                        {
                            int x = i - origin;
                            float expected = untouched;
                            if (c < 3 && x >= 0 && x < width)
                            {
                                int sample = (x % 4) * 3;
                                if (scaled == 0)
                                {
                                    expected = rgb[sample + c] * 0.5F;
                                }
                                else
                                {
                                    // Native scaled output uses (X+offset)*scale, Y*scale, and (B-Y+offset)*scale in this order.
                                    expected = c == 0 ? (xyb[sample] + 0.015386134F) * 22.995788804F
                                        : c == 1 ? xyb[sample + 1] * 1.183000077F
                                        : ((xyb[sample + 2] - xyb[sample + 1]) + 0.27770459F) * 1.502141333F;
                                }
                            }

                            Assert.Equal(expected, actual[i]);
                        }
                    }
                }
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>Prepares a nonsymmetric dyadic inverse matrix and exact cube-root biases for independent numerical checks.</summary>
    /// <param name="intensityTarget">The relative-output intensity target: 255 or 510 nits.</param>
    /// <returns>The prepared inverse transform.</returns>
    private static JxlOpsinParameters CreateDyadicOpsinParameters(float intensityTarget)
    {
        JxlMatrix3x3F matrix = default;
        ReadOnlySpan<float> coefficients = [1F, 0.5F, -0.25F, -0.5F, 2F, 0.25F, 0.25F, -0.5F, 1F];
        coefficients.CopyTo(matrix.AsSpan());
        InlineArray3<float> biases = default;
        biases[0] = biases[1] = biases[2] = -1F;
        JxlOpsinParameters parameters = new();
        parameters.Initialize(in matrix, biases, intensityTarget);
        return parameters;
    }
}
