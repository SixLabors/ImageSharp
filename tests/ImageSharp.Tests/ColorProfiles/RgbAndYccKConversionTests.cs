// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="Rgb"/>-<see cref="YccK"/> conversions.
/// </summary>
/// <remarks>
/// Test data generated mathematically
/// </remarks>
public class RgbAndYccKConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.001F);

    [Theory]
    [InlineData(1, .5F, .5F, 0, 1, 1, 1)]
    [InlineData(0, .5F, .5F, 1, 0, 0, 0)]
    [InlineData(.5F, .5F, .5F, 0, .5F, .5F, .5F)]
    public void Convert_YccK_To_Rgb(float y, float cb, float cr, float k, float r, float g, float b)
    {
        // Arrange
        YccK input = new(y, cb, cr, k);
        Rgb expected = new(r, g, b);
        ColorProfileConverter converter = new();

        Span<YccK> inputSpan = new YccK[5];
        inputSpan.Fill(input);

        Span<Rgb> actualSpan = new Rgb[5];

        // Act
        Rgb actual = converter.Convert<YccK, Rgb>(input);
        converter.Convert<YccK, Rgb>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(1, 1, 1, 1, .5F, .5F, 0)]
    [InlineData(0, 0, 0, 0, .5F, .5F, 1)]
    [InlineData(.5F, .5F, .5F, 1, .5F, .5F, .5F)]
    public void Convert_Rgb_To_YccK(float r, float g, float b, float y, float cb, float cr, float k)
    {
        // Multiple YccK representations can decode to the same RGB value.
        // For example, (Y=1.0, Cb=0.5, Cr=0.5, K=0.5) and (Y=0.5, Cb=0.5, Cr=0.5, K=0.0) both yield RGB (0.5, 0.5, 0.5).
        // This is expected because YccK is not a unique encoding — K modulates RGB after YCbCr decoding.
        // Round-tripping RGB -> YccK -> RGB is stable, but YccK -> RGB -> YccK is not injective.

        // Arrange
        Rgb input = new(r, g, b);
        YccK expected = new(y, cb, cr, k);
        ColorProfileConverter converter = new();

        Span<Rgb> inputSpan = new Rgb[5];
        inputSpan.Fill(input);

        Span<YccK> actualSpan = new YccK[5];

        // Act
        YccK actual = converter.Convert<Rgb, YccK>(input);
        converter.Convert<Rgb, YccK>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    /// <summary>
    /// Checks both public PCS directions across every SIMD width and the scalar remainder.
    /// </summary>
    [Fact]
    public void BulkMatrixConversionsMatchScalar()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            VerifyBulkMatrixConversions,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks near-black selection, constructor clamping and raw black components against scalar conversion.
    /// </summary>
    private static void VerifyBulkMatrixConversions()
    {
        ReadOnlySpan<Rgb> rgbSamples =
        [
            new(0F, -0F, 0F),
            new(Constants.Epsilon * .5F, 0F, 0F),
            new(.12F, .75F, .33F),
            new(1.5F, .25F, -.1F),
            new(-.25F, -.125F, -.5F),
            new(.0001F, .0002F, .0003F),
            new(float.NaN, .5F, .25F),
            new(float.PositiveInfinity, .5F, float.NegativeInfinity),
            new(1F, 1F, 1F)
        ];

        ReadOnlySpan<Vector4> ycckSamples =
        [
            new(0F, .5F, .5F, 1F),
            new(.4F, .2F, .8F, .3F),
            new(1.25F, -.5F, 1.5F, -.25F),
            new(-0F, -0F, 0F, -0F),
            new(.5F, .5F, .5F, .99999994F),
            new(.2F, .7F, .4F, 1.25F),
            new(float.NaN, .5F, .5F, 0F),
            new(1F, .5F, .5F, float.NegativeInfinity),
            new(.6F, .1F, .9F, .15F)
        ];

        ColorConversionOptions options = new();
        Rgb[] rgb = new Rgb[33];
        YccK[] ycck = new YccK[rgb.Length];
        YccK[] actualYcck = new YccK[rgb.Length];
        Rgb[] actualRgb = new Rgb[rgb.Length];
        for (int i = 0; i < rgb.Length; i++)
        {
            rgb[i] = rgbSamples[i % rgbSamples.Length];

            // The public normalized-component API retains raw values, while the normal constructor clamps.
            // This checks the existing inverse contract for extended components and K outside its usual range.
            ycck[i] = YccK.FromScaledVector4(ycckSamples[i % ycckSamples.Length]);
        }

        // Thirty-three mixed colors exercise 16+16+1 pixels at maximum width, plus all narrower remainders.
        // The two input periods are independent; this is not a round-trip test that could hide paired errors.
        YccK.FromProfileConnectionSpace(options, rgb, actualYcck);
        YccK.ToProfileConnectionSpace(options, ycck, actualRgb);
        for (int i = 0; i < rgb.Length; i++)
        {
            YccK expectedYcck = YccK.FromProfileConnectingSpace(options, in rgb[i]);
            Rgb expectedRgb = ycck[i].ToProfileConnectingSpace(options);
            if (float.IsFinite(expectedYcck.Y) && float.IsFinite(expectedYcck.Cb) && float.IsFinite(expectedYcck.Cr) && float.IsFinite(expectedYcck.K))
            {
                Assert.Equal(expectedYcck, actualYcck[i], Comparer);
            }
            else
            {
                // NaN and infinity follow the scalar contract. Do not route them through a finite-only comparer.
                Assert.Equal(expectedYcck.Y, actualYcck[i].Y);
                Assert.Equal(expectedYcck.Cb, actualYcck[i].Cb);
                Assert.Equal(expectedYcck.Cr, actualYcck[i].Cr);
                Assert.Equal(expectedYcck.K, actualYcck[i].K);
            }

            if (float.IsFinite(expectedRgb.R) && float.IsFinite(expectedRgb.G) && float.IsFinite(expectedRgb.B))
            {
                Assert.Equal(expectedRgb, actualRgb[i], Comparer);
            }
            else
            {
                Assert.Equal(expectedRgb.R, actualRgb[i].R);
                Assert.Equal(expectedRgb.G, actualRgb[i].G);
                Assert.Equal(expectedRgb.B, actualRgb[i].B);
            }

            if (i % rgbSamples.Length == 0)
            {
                // The black branch publishes exact raw constants, including positive-zero luminance.
                Assert.Equal(BitConverter.SingleToInt32Bits(expectedYcck.Y), BitConverter.SingleToInt32Bits(actualYcck[i].Y));
                Assert.Equal(expectedYcck.K, actualYcck[i].K);
            }
        }
    }
}
