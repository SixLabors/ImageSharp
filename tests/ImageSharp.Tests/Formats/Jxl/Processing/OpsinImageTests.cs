// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

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
        using JxlImage3F linear = new(TestEnvironment.Configuration, 1, 1);
        linear.PlaneRow(0, 0)[0] = rgbR;
        linear.PlaneRow(1, 0)[0] = rgbG;
        linear.PlaneRow(2, 0)[0] = rgbB;
        Assert.True(JxlXybEncoder.ToXyb(
            TestEnvironment.Configuration,
            JxlColorEncoding.LinearSRgb,
            JxlShared.DefaultIntensityTarget,
            null,
            new JxlImage3F(),
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
        using JxlImage3F opsin = new(TestEnvironment.Configuration, 1, 1);
        opsin.PlaneRow(0, 0)[0] = xybX;
        opsin.PlaneRow(1, 0)[0] = xybY;
        opsin.PlaneRow(2, 0)[0] = xybB;

        using JxlImage3F linear = new(TestEnvironment.Configuration, 1, 1);

        JxlOpsinParameters opsinParameters = new();
        opsinParameters.Initialize(intensityTarget: 255.0f);

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
}
