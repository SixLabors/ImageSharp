// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

public class GaborishTests
{
    private static JxlWeightsSymmetric3 GaborishKernel(float weight1, float weight2)
    {
        const float weight0 = 1.0f;

        // Normalize.
        float mul = 1.0f / (weight0 + (4.0f * (weight1 + weight2)));
        float w0 = weight0 * mul;
        float w1 = weight1 * mul;
        float w2 = weight2 * mul;

        JxlWeightsSymmetric3 wgt = new();
        wgt.SetC(Vector128.Create(w0));
        wgt.SetD(Vector128.Create(w1));
        wgt.SetR(Vector128.Create(w2));

        return wgt;
    }

    private static void ConvolveGaborish(
        JxlPlane<float> input,
        float weight1,
        float weight2,
        JxlPlane<float> output)
    {
        Assert.True(JxlImageOperations.SameSize(input, output));

        // The test will fail if this throws.
        JxlConvolve.SlowSymmetric3(
            input,
            input.GetRectangle(),
            GaborishKernel(weight1, weight2),
            output);
    }

    private static void TestRoundTrip(
        JxlImage3F input,
        float maxL1)
    {
        JxlImage3F fwd = new(TestEnvironment.Configuration, input.XSize, input.YSize);

        ConvolveGaborish(
            input.Plane(0),
            0,
            0,
            fwd.Plane(0));

        ConvolveGaborish(
            input.Plane(1),
            0,
            0,
            fwd.Plane(1));

        ConvolveGaborish(
            input.Plane(2),
            0,
            0,
            fwd.Plane(2));

        const float w = 0.92718927264540152f;

        InlineArray3<float> weights = default;
        weights[0] = w;
        weights[1] = w;
        weights[2] = w;

        // The test will fail if this throws.
        JxlGaborish.InverseGaborish(
            TestEnvironment.Configuration,
            fwd,
            fwd.GetRectangle(),
            weights);

        // TODO: VerifyRelativeError
        Assert.True(
            VerifyRelativeError(
                input,
                fwd,
                maxL1,
                1e-4f));
    }

    [Fact]
    public void TestZero()
    {
        JxlImage3F input = new(TestEnvironment.Configuration, 20, 20);
        JxlImageOperations.ZeroFillImage(input);
        TestRoundTrip(input, 0.0f);
    }

    [Fact]
    public void TestFlat()
    {
        JxlImage3F input = new(TestEnvironment.Configuration, 20, 20);
        JxlImageOperations.FillImage(1.0f, input);
        TestRoundTrip(input, 1e-5f);
    }
}
