// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Blending;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Patch;

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

        JxlBlendInput inputBg = new([bgRgb.R], [bgRgb.G], [bgRgb.B], [bgA]);

        JxlBlendInput inputFg = new([fgRgb.R], [fgRgb.G], [fgRgb.B], [fgA]);

        JxlBlendOutput output = new(outR, outG, outB, outAlpha);

        JxlBlending.SourceOver(inputBg, inputFg, output, associated: false, clampWeight: false);

        Assert.Equal(
            Expected,
            [outR[0], outG[0], outB[0]],
            new ApproximateFloatComparer(0.05f));

        Assert.True(MathF.Abs(outAlpha[0] - (3174f / 4095)) <= 1e-5f);

        inputFg = new([fgRgb.R], [fgRgb.G], [fgRgb.B], [fgA2]);

        JxlBlending.SourceOver(inputBg, inputFg, output, associated: false, clampWeight: true);

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

        JxlBlendInput inputBg = new([bgRgb.R], [bgRgb.G], [bgRgb.B], [bgA]);

        JxlBlendInput inputFg = new([fgRgb.R], [fgRgb.G], [fgRgb.B], [fgA]);

        JxlBlendOutput output = new(outR, outG, outB, outAlpha);

        JxlBlending.SourceOver(inputBg, inputFg, output, associated: true, clampWeight: false);

        Assert.Equal(
            new float[] { 101.5f, 105.1f, 114.8f },
            [outR[0], outG[0], outB[0]],
            new ApproximateFloatComparer(0.05f));

        Assert.True(MathF.Abs(outAlpha[0] - (3174f / 4095)) <= 1e-5f);

        inputFg = new([fgRgb.R], [fgRgb.G], [fgRgb.B], [fgA2]);

        JxlBlending.SourceOver(inputBg, inputFg, output, associated: true, clampWeight: true);

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

        JxlBlending.Multiply(
            bg,
            fg,
            output,
            clampSource: false);

        Assert.True(MathF.Abs(output[0] - (fg[0] * bg[0])) <= 0.05f);

        JxlBlending.Multiply(
            bg,
            fg,
            output,
            clampSource: true);

        Assert.True(MathF.Abs(output[0] - bg[0]) <= 0.05f);
    }

    /// <summary>
    /// Checks the native alpha floor, including NaN, through complete SIMD blocks and the scalar tail.
    /// </summary>
    [Fact]
    public void AlphaRepresentation_PreservesColorsWithNativeFloor()
    {
        static void RunTest()
        {
            // The native floor is 2^-26. Its power-of-two factors make these expected products and reciprocals exact.
            ReadOnlySpan<float> pattern = [0F, float.NaN, 0.5F, 1F];
            ReadOnlySpan<float> factors = [1F / (1 << 26), 1F / (1 << 26), 0.5F, 1F];
            float[] alpha = new float[37];
            float[] red = new float[alpha.Length];
            float[] green = new float[alpha.Length];
            float[] blue = new float[alpha.Length];

            for (int i = 0; i < alpha.Length; i++)
            {
                alpha[i] = pattern[i % pattern.Length];
            }

            red.AsSpan().Fill(2F);
            green.AsSpan().Fill(3F);
            blue.AsSpan().Fill(4F);
            JxlAlphaHelper.PremultiplyAlpha(red, green, blue, alpha, alpha.Length);

            for (int i = 0; i < alpha.Length; i++)
            {
                float factor = factors[i % factors.Length];
                Assert.Equal(2F * factor, red[i]);
                Assert.Equal(3F * factor, green[i]);
                Assert.Equal(4F * factor, blue[i]);
            }

            JxlAlphaHelper.UnpremultiplyAlpha(red, green, blue, alpha, alpha.Length);

            for (int i = 0; i < alpha.Length; i++)
            {
                Assert.Equal(2F, red[i]);
                Assert.Equal(3F, green[i]);
                Assert.Equal(4F, blue[i]);
            }

            Assert.True(float.IsNaN(alpha[1]));
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
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

    /// <summary>
    /// Checks all planar traversal widths and scalar tails against fixed source-over and weighted-add values.
    /// </summary>
    [Fact]
    public void PlanarCompositionMatchesKnownValuesAcrossHardwareWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            VerifyPlanarComposition,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks known rational values, optional alpha clipping, alpha-only composition, and in-place channel writes.
    /// </summary>
    private static void VerifyPlanarComposition()
    {
        // Eight units of background at alpha 1/4 and two source units at alpha 1/2 produce 16/5 at alpha 5/8.
        // Counts straddle every float SIMD width. Each mode also runs with all hardware intrinsics disabled.
        foreach (int count in new[] { 0, 1, 3, 4, 5, 7, 8, 9, 15, 16, 17, 33 })
        {
            float[] background = new float[count];
            float[] source = new float[count];
            float[] backgroundAlpha = new float[count];
            float[] sourceAlpha = new float[count];
            float[] destination = new float[count];
            Array.Fill(background, 8F);
            Array.Fill(source, 2F);
            Array.Fill(backgroundAlpha, .25F);
            Array.Fill(sourceAlpha, .5F);

            JxlBlending.SourceOver(background, backgroundAlpha, source, sourceAlpha, destination, false, false);
            Assert.All(destination, value => Assert.Equal(3.2F, value));

            JxlBlending.SourceOver(background, backgroundAlpha, source, sourceAlpha, destination, true, false);
            Assert.All(destination, value => Assert.Equal(6F, value));

            JxlBlending.SourceOver(backgroundAlpha, backgroundAlpha, sourceAlpha, sourceAlpha, destination, false, false);
            Assert.All(destination, value => Assert.Equal(.625F, value));

            JxlBlending.AddWeighted(background, source, sourceAlpha, destination, false);
            Assert.All(destination, value => Assert.Equal(9F, value));

            // Adding an alpha channel weighted by itself must retain the background, including its first sample.
            JxlBlending.AddWeighted(background, sourceAlpha, sourceAlpha, destination, true);
            Assert.All(destination, value => Assert.Equal(8F, value));

            Array.Fill(sourceAlpha, 2F);
            JxlBlending.SourceOver(background, backgroundAlpha, source, sourceAlpha, destination, false, true);
            Assert.All(destination, value => Assert.Equal(2F, value));

            // With clipping disabled, alpha 2 is retained: the output is (4 - 2) / 1.75, not the clipped source value.
            JxlBlending.SourceOver(background, backgroundAlpha, source, sourceAlpha, destination, false, false);
            Assert.All(destination, value => Assert.Equal(8F / 7F, value));

            // The encoded clamp uses the shared finite rules: NaN and negative infinity map to zero; positive infinity maps to one.
            // Check all native operations so one mode cannot silently return to a different clamp implementation.
            foreach (float weight in new[] { float.NaN, float.NegativeInfinity, float.PositiveInfinity })
            {
                Array.Fill(sourceAlpha, weight);
                float boundedWeight = float.IsPositiveInfinity(weight) ? 1F : 0F;
                JxlBlending.SourceOver(background, backgroundAlpha, source, sourceAlpha, destination, false, true);
                Assert.All(destination, value => Assert.Equal(boundedWeight == 1F ? 2F : 8F, value));

                JxlBlending.AddWeighted(background, source, sourceAlpha, destination, true);
                Assert.All(destination, value => Assert.Equal(8F + (2F * boundedWeight), value));

                JxlBlending.Multiply(background, sourceAlpha, destination, true);
                Assert.All(destination, value => Assert.Equal(8F * boundedWeight, value));
            }

            Array.Fill(sourceAlpha, 2F);
            Array.Fill(backgroundAlpha, 2F);
            float[] result0 = new float[count];
            float[] result1 = new float[count];
            float[] result2 = new float[count];
            float[] resultWeight = new float[count];
            JxlBlendInput bottom = new(background, background, background, backgroundAlpha);
            JxlBlendInput top = new(source, source, source, sourceAlpha);
            JxlBlendOutput result = new(result0, result1, result2, resultWeight);

            // Native weights 2 and 2 cancel to output weight zero despite a nonzero associated numerator.
            // Reconstruction must use a zero reciprocal here. Valid pixel alpha remains bounded to [0,1].
            JxlBlending.SourceOver(bottom, top, result, false, false);
            Assert.All(result0, value => Assert.Equal(0F, value));
            Assert.All(result1, value => Assert.Equal(0F, value));
            Assert.All(result2, value => Assert.Equal(0F, value));
            Assert.All(resultWeight, value => Assert.Equal(0F, value));

            Array.Fill(backgroundAlpha, 3F);
            JxlBlending.SourceOver(bottom, top, result, false, false);
            Assert.All(result0, value => Assert.Equal(0F, value));
            Assert.All(resultWeight, value => Assert.Equal(-1F, value));

            // Load-before-store permits exact in-place writes without a row copy.
            JxlBlending.AddWeighted(background, source, sourceAlpha, background, false);
            Assert.All(background, value => Assert.Equal(12F, value));

            JxlBlending.Multiply(background, source, destination, true);
            Assert.All(destination, value => Assert.Equal(12F, value));
        }
    }

    /// <summary>
    /// Checks row offsets, reversed blends, selected alpha, and extra-channel writes through the codec's blend entry point.
    /// </summary>
    [Fact]
    public void RowBlendingPreservesOriginalAlphaAndAllChannels()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            VerifyRowBlending,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks native blend-mode results with aliased background/output rows and two independently selected alpha channels.
    /// </summary>
    private static void VerifyRowBlending()
    {
        const int startX = 2;
        const int count = 33;
        const int rowLength = startX + count + 2;
        List<JxlExtraChannelInfo> channels =
        [
            new() { Type = JxlExtraChannel.Depth },
            new() { Type = JxlExtraChannel.Alpha },
            new() { Type = JxlExtraChannel.SpotColor },
            new() { Type = JxlExtraChannel.Alpha }
        ];

        JxlPatchBlending[] extraBlending =
        [
            new() { Mode = JxlPatchBlendMode.Multiply },
            new() { Mode = JxlPatchBlendMode.None },
            new() { Mode = JxlPatchBlendMode.BlendBelow, AlphaChannel = 3 },
            new() { Mode = JxlPatchBlendMode.AlphaWeightedAddAbove, AlphaChannel = 3 }
        ];

        // These values come from the native blend equations, not from another call to the implementation under test.
        (JxlPatchBlendMode Mode, float Red, float Green, float Blue, float Alpha)[] expected =
        [
            (JxlPatchBlendMode.None, 8F, 16F, 24F, .25F),
            (JxlPatchBlendMode.Replace, 2F, 4F, 6F, .25F),
            (JxlPatchBlendMode.Add, 10F, 20F, 30F, .25F),
            (JxlPatchBlendMode.Multiply, 16F, 64F, 144F, .25F),
            (JxlPatchBlendMode.BlendAbove, 3.2F, 6.4F, 9.6F, .625F),
            (JxlPatchBlendMode.BlendBelow, 4.4F, 8.8F, 13.2F, .625F),
            (JxlPatchBlendMode.AlphaWeightedAddAbove, 9F, 18F, 27F, .25F),
            (JxlPatchBlendMode.AlphaWeightedAddBelow, 4F, 8F, 12F, .25F)
        ];

        using JxlPlane<float> scratch = new(Configuration.Default, count, 7);

        foreach (bool associated in new[] { false, true })
        {
            channels[1].AlphaAssociated = associated;

            foreach (var result in expected)
            {
                float[] backgroundValues = [8F, 16F, 24F, 12F, .25F, 20F, .75F];
                float[] foregroundValues = [2F, 4F, 6F, .5F, .5F, 4F, .5F];
                Memory<float>[] background = new Memory<float>[7];
                Memory<float>[] foreground = new Memory<float>[7];

                for (int channel = 0; channel < background.Length; channel++)
                {
                    float[] backgroundRow = new float[rowLength];
                    float[] foregroundRow = new float[count];
                    Array.Fill(backgroundRow, backgroundValues[channel]);
                    Array.Fill(foregroundRow, foregroundValues[channel]);
                    background[channel] = backgroundRow;
                    foreground[channel] = foregroundRow;
                }

                JxlBlending.PerformBlending(
                    background,
                    foreground,
                    background,
                    startX,
                    count,
                    new JxlPatchBlending { Mode = result.Mode, AlphaChannel = 1 },
                    extraBlending,
                    channels,
                    scratch);

                float[] expectedChannels = [result.Red, result.Green, result.Blue, 6F, result.Alpha, 124F / 7F, .75F];

                if (associated && result.Mode == JxlPatchBlendMode.BlendAbove)
                {
                    // Associated source-over is source + background * (1 - sourceWeight).
                    expectedChannels[0] = 6F;
                    expectedChannels[1] = 12F;
                    expectedChannels[2] = 18F;
                }
                else if (associated && result.Mode == JxlPatchBlendMode.BlendBelow)
                {
                    expectedChannels[0] = 9.5F;
                    expectedChannels[1] = 19F;
                    expectedChannels[2] = 28.5F;
                }

                for (int channel = 0; channel < background.Length; channel++)
                {
                    Span<float> row = background[channel].Span;

                    for (int x = 0; x < rowLength; x++)
                    {
                        float expectedValue = x >= startX && x < startX + count ? expectedChannels[channel] : backgroundValues[channel];

                        // The native equation multiplies by reciprocal alpha. Its rounding can differ from direct rational division by one float step.
                        Assert.True(MathF.Abs(expectedValue - row[x]) <= 1e-5F, $"Mode {result.Mode}, channel {channel}, column {x}.");
                    }
                }
            }
        }
    }
}
