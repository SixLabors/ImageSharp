// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Blending;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

public class AlphaBlendingTests
{
    private static float[] RgbToArray(Rgb rgb) => [rgb.R, rgb.G, rgb.B];

    private static readonly float[] Expected = [77.2f, 83.0f, 90.6f];

    [Fact]
    public void BlendingWithNonPremultiplied()
    {
        Rgb bgRgb = new(100, 110, 120);
        float bgA = 180f / 255;

        Rgb fgRgb = new(25, 21, 23);
        float fgA = 15420f / 65535;
        float fgA2 = 2.0f;

        float[] outR = [0];
        float[] outG = [0];
        float[] outB = [0];
        float[] outAlpha = [0];

        JxlAlphaBlendingInputLayer inputBg = new()
        {
            R = [bgRgb.R],
            G = [bgRgb.G],
            B = [bgRgb.B],
            A = [bgA],
        };

        JxlAlphaBlendingInputLayer inputFg = new()
        {
            R = [fgRgb.R],
            G = [fgRgb.G],
            B = [fgRgb.B],
            A = [fgA],
        };

        JxlAlphaBlendingOutput output = new()
        {
            R = outR,
            G = outG,
            B = outB,
            A = outAlpha,
        };

        JxlAlphaHelper.PerformAlphaBlending(
            inputBg,
            inputFg,
            output,
            1,
            alphaIsPremultiplied: false,
            clamp: false);

        Assert.Equal(
            Expected,
            [outR[0], outG[0], outB[0]],
            new ApproximateFloatComparer(0.05f));

        Assert.True(MathF.Abs(outAlpha[0] - (3174f / 4095)) <= 1e-5f);

        inputFg = new()
        {
            R = [fgRgb.R],
            G = [fgRgb.G],
            B = [fgRgb.B],
            A = [fgA2],
        };

        JxlAlphaHelper.PerformAlphaBlending(
            inputBg,
            inputFg,
            output,
            1,
            alphaIsPremultiplied: false,
            clamp: true);

        Assert.Equal(
            RgbToArray(fgRgb),
            [outR[0], outG[0], outB[0]],
            new ApproximateFloatComparer(0.05f));

        Assert.True(MathF.Abs(outAlpha[0] - 1.0f) <= 1e-5f);
    }

    [Fact]
    public void BlendingWithPremultiplied()
    {
        Rgb bgRgb = new(100, 110, 120);
        float bgA = 180f / 255;

        Rgb fgRgb = new(25, 21, 23);
        float fgA = 15420f / 65535;
        float fgA2 = 2.0f;

        float[] outR = [0];
        float[] outG = [0];
        float[] outB = [0];
        float[] outAlpha = [0];

        JxlAlphaBlendingInputLayer inputBg = new()
        {
            R = [bgRgb.R],
            G = [bgRgb.G],
            B = [bgRgb.B],
            A = [bgA],
        };

        JxlAlphaBlendingInputLayer inputFg = new()
        {
            R = [fgRgb.R],
            G = [fgRgb.G],
            B = [fgRgb.B],
            A = [fgA],
        };

        JxlAlphaBlendingOutput output = new()
        {
            R = outR,
            G = outG,
            B = outB,
            A = outAlpha,
        };

        JxlAlphaHelper.PerformAlphaBlending(
            inputBg,
            inputFg,
            output,
            1,
            alphaIsPremultiplied: true,
            clamp: false);

        Assert.Equal(
            new float[] { 101.5f, 105.1f, 114.8f },
            [outR[0], outG[0], outB[0]],
            new ApproximateFloatComparer(0.05f));

        Assert.True(MathF.Abs(outAlpha[0] - (3174f / 4095)) <= 1e-5f);

        inputFg = new()
        {
            R = [fgRgb.R],
            G = [fgRgb.G],
            B = [fgRgb.B],
            A = [fgA2],
        };

        JxlAlphaHelper.PerformAlphaBlending(
            inputBg,
            inputFg,
            output,
            1,
            alphaIsPremultiplied: true,
            clamp: true);

        Assert.Equal(
            RgbToArray(fgRgb),
            [outR[0], outG[0], outB[0]],
            new ApproximateFloatComparer(0.05f));

        Assert.True(MathF.Abs(outAlpha[0] - 1.0f) <= 1e-5f);
    }

    [Fact]
    public void Mul()
    {
        Span<float> bg = [100];
        Span<float> fg = [25];
        Span<float> output = [0];

        JxlAlphaHelper.PerformMultiplyBlending(
            bg,
            fg,
            output,
            1,
            clamp: false);

        Assert.True(MathF.Abs(output[0] - (fg[0] * bg[0])) <= 0.05f);

        JxlAlphaHelper.PerformMultiplyBlending(
            bg,
            fg,
            output,
            1,
            clamp: true);

        Assert.True(MathF.Abs(output[0] - bg[0]) <= 0.05f);
    }

    [Fact]
    public void PremultiplyAndUnpremultiply()
    {
        float[] alpha =
        {
            0f,
            63f / 255,
            127f / 255,
            1f
        };

        float[] r = { 120, 130, 140, 150 };
        float[] g = { 124, 134, 144, 154 };
        float[] b = { 127, 137, 147, 157 };

        JxlAlphaHelper.PremultiplyAlpha(r, g, b, alpha, alpha.Length);

        Assert.Equal(
            new[]
            {
                0f,
                130 * 63f / 255,
                140 * 127f / 255,
                150f
            },
            r,
            new ApproximateFloatComparer(1e-5f));

        Assert.Equal(
            new[]
            {
                0f,
                134 * 63f / 255,
                144 * 127f / 255,
                154f
            },
            g,
            new ApproximateFloatComparer(1e-5f));

        Assert.Equal(
            new[]
            {
                0f,
                137 * 63f / 255,
                147 * 127f / 255,
                157f
            },
            b,
            new ApproximateFloatComparer(1e-5f));

        JxlAlphaHelper.UnpremultiplyAlpha(r, g, b, alpha, alpha.Length);

        Assert.Equal(
            new[] { 120f, 130f, 140f, 150f },
            r,
            new ApproximateFloatComparer(1e-4f));

        Assert.Equal(
            new[] { 124f, 134f, 144f, 154f },
            g,
            new ApproximateFloatComparer(1e-4f));

        Assert.Equal(
            new[] { 127f, 137f, 147f, 157f },
            b,
            new ApproximateFloatComparer(1e-4f));
    }

    [Fact]
    public void UnpremultiplyAndPremultiply()
    {
        float[] alpha =
        {
            0f,
            63f / 255,
            127f / 255,
            1f
        };

        float[] r = { 50, 60, 70, 80 };
        float[] g = { 54, 64, 74, 84 };
        float[] b = { 57, 67, 77, 87 };

        JxlAlphaHelper.UnpremultiplyAlpha(r, g, b, alpha, alpha.Length);

        Assert.Equal(
            new[]
            {
                50f * (1 << 26),
                60 * 255f / 63,
                70 * 255f / 127,
                80f
            },
            r,
            new ApproximateFloatComparer(1e-4f));

        Assert.Equal(
            new[]
            {
                54f * (1 << 26),
                64 * 255f / 63,
                74 * 255f / 127,
                84f
            },
            g,
            new ApproximateFloatComparer(1e-4f));

        Assert.Equal(
            new[]
            {
                57f * (1 << 26),
                67 * 255f / 63,
                77 * 255f / 127,
                87f
            },
            b,
            new ApproximateFloatComparer(1e-4f));

        JxlAlphaHelper.PremultiplyAlpha(r, g, b, alpha, alpha.Length);

        Assert.Equal(
            new[] { 50f, 60f, 70f, 80f },
            r,
            new ApproximateFloatComparer(1e-4f));

        Assert.Equal(
            new[] { 54f, 64f, 74f, 84f },
            g,
            new ApproximateFloatComparer(1e-4f));

        Assert.Equal(
            new[] { 57f, 67f, 77f, 87f },
            b,
            new ApproximateFloatComparer(1e-4f));
    }
}
