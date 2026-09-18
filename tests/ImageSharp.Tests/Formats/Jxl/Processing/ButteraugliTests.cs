// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using Microsoft.Diagnostics.Runtime.Interop;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Butteraugli;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

/// <summary>
/// Tests for Google Butteraugli (C# implementation), a tool
/// used for comparing images in a more sophisticated way
/// (the way humans may notice differences). For example,
/// if every pixel was off by 1, PSNR, which operates on a
/// pixel-by-pixel basis, would report a large difference,
/// while Butteraugli would report barely any differences,
/// as us humans wouldn't notice anything if every pixel
/// was just off by one.
/// </summary>
public class ButteraugliTests
{
    private static JxlImage3F SinglePixelImage(float r, float g, float b)
    {
        JxlImage3F img = new(TestEnvironment.Configuration, 1, 1);
        img.PlaneRow(0, 0)[0] = r;
        img.PlaneRow(1, 0)[0] = r;
        img.PlaneRow(2, 0)[0] = r;
        return img;
    }

    private static void AddUniformNoise(JxlImage3F img, float d, ulong seed)
    {
        Rng generator = new(seed);

        for (int y = 0; y < img.YSize; ++y)
        {
            for (int c = 0; c < 3; ++c)
            {
                Span<float> planeRow = img.PlaneRow(c, y);

                for (int x = 0; x < img.XSize; ++x)
                {
                    planeRow[x] += generator.UniformF(-d, d);
                }
            }
        }
    }

    private static void AddEdge(JxlImage3F image, float d, int x0, int y0)
    {
        int h = Math.Min(image.YSize - y0, 100);
        int w = Math.Min(image.XSize - x0, 5);

        for (int dy = 0; dy < h; ++dy)
        {
            Span<float> planeRow = image.PlaneRow(1, y0 + dy);

            for (int dx = 0; dx < w; ++dx)
            {
                planeRow[x0 + dx] += d;
            }
        }
    }

    [Fact]
    public void TestSinglePixel()
    {
        JxlImage3F rgb0 = SinglePixelImage(0.5f, 0.5f, 0.5f);
        JxlImage3F rgb1 = SinglePixelImage(0.5f, 0.49f, 0.5f);

        ButteraugliParameters butteraugliParameters = new();
        JxlImageF diffmap = new();

        Assert.True(
            Butteraugli.ButteraugliInterface(TestEnvironment.Configuration, rgb0, rgb1, butteraugliParameters, diffmap, out double diffval),
            "Butteraugli initialization failed");

        Assert.True(new TolerantMath(0.5).AreEqual(diffval, 2.5), $"Diff value isn't even close to 2.5 (it's {diffval})");

        JxlImageF diffmap2 = new();
        Assert.True(Butteraugli.ButteraugliInterfaceInPlace(
            TestEnvironment.Configuration,
            rgb0,
            rgb1,
            butteraugliParameters,
            diffmap2,
            out double diffval2));

        Assert.True(new TolerantMath(1e-10).AreEqual(diffval, diffval2), $"Diff value isn't even close to diffval2 (diffval={diffval}, diffval2={diffval2})");
    }

    // TODO: we need to port test image stuff from libjxl
    [Fact]
    public void TestLargeImage()
    {
        const int xSize = 1024;
        const int ySize = 1024;

        JxlTestImage img = new();
        img.SetDimensions(xSize, ySize);

        JxlTestFrame frame = img.AddFrame();
        frame.RandomFill(777);

        JxlImage3F rgb0 = GetColorImage(img.Ppf);
        JxlImage3F rgb1 = new(TestEnvironment.Configuration, xSize, ySize);
        JxlImageOperations.CopyImage(rgb0, rgb1);

        AddUniformNoise(rgb1, 0.02f, 7777uL);
        AddEdge(rgb1, 0.1f, xSize / 2, xSize / 2);

        ButteraugliParameters butteraugliParameters = new();
        JxlImageF diffmap = new();
        Assert.True(
            Butteraugli.ButteraugliInterface(TestEnvironment.Configuration,, rgb0, rgb1, butteraugliParameters, diffmap, out double diffval),
            "Couldn't initialize Butteraugli");

        double distp = Butteraugli.ComputeDistanceP(diffmap, butteraugliParameters, 3.0);
        Assert.True(new TolerantMath(0.5).AreEqual(diffval, 4.0), $"Diff isn't even close to 4.0 (diffval={diffval})");
        Assert.True(new TolerantMath(0.5).AreEqual(distp, 1.5), $"Distance isn't even close to 4.0 (distp={distp})");

        JxlImageF diffmap2 = new();
        Assert.True(
            Butteraugli.ButteraugliInterfaceInPlace(
                TestEnvironment.Configuration,
                rgb0,
                rgb1,
                butteraugliParameters,
                diffmap2,
                out double diffval2),
            "Butteraugli in-place interface initialization failed");

        double distp2 = Butteraugli.ComputeDistanceP(diffmap2, butteraugliParameters, 3.0);

        Assert.True(new TolerantMath(5e-7).AreEqual(diffval, diffval2), $"Diffval != diffval2 (diffval={diffval}, diffval2={diffval2})");
        Assert.True(new TolerantMath(1e-7).AreEqual(distp, distp2), $"Distp != distp2 (distp={distp}, distp2={distp2})");
    }
}
