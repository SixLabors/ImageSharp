// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests the <see cref="Cmyk"/> struct.
/// </summary>
[Trait("Color", "Conversion")]
public class CmykTests
{
    [Fact]
    public void CmykConstructorAssignsFields()
    {
        const float c = .75F;
        const float m = .64F;
        const float y = .87F;
        const float k = .334F;
        Cmyk cmyk = new(c, m, y, k);

        Assert.Equal(c, cmyk.C);
        Assert.Equal(m, cmyk.M);
        Assert.Equal(y, cmyk.Y);
        Assert.Equal(k, cmyk.K);
    }

    [Fact]
    public void CmykEquality()
    {
        Cmyk x = default;
        Cmyk y = new(Vector4.One);

        Assert.True(default == default(Cmyk));
        Assert.False(default != default(Cmyk));
        Assert.Equal(default, default(Cmyk));
        Assert.Equal(new Cmyk(1, 0, 1, 0), new Cmyk(1, 0, 1, 0));
        Assert.Equal(new Cmyk(Vector4.One), new Cmyk(Vector4.One));
        Assert.False(x.Equals(y));
        Assert.False(x.Equals((object)y));
        Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
    }

    /// <summary>
    /// Verifies that bulk conversion rejects a destination shorter than the source.
    /// </summary>
    [Fact]
    public void BulkConversionRejectsShortDestination()
    {
        Cmyk[] source = new Cmyk[4];
        Rgb[] destination = new Rgb[3];
        ColorConversionOptions options = new();

        Assert.Throws<ArgumentException>(() => Cmyk.ToProfileConnectionSpace(options, source, destination));
    }

    /// <summary>
    /// Checks the shared RGB-to-CMYK model at all SIMD widths, including HDR bounds and its near-black rule.
    /// </summary>
    [Fact]
    public void FromRgbBulkMatchesScalar() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        AssertRgbToCmyk,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Compares bulk ink values with the single-color contract, including nonfinite input and destination padding.
    /// </summary>
    private static void AssertRgbToCmyk()
    {
        ColorConversionOptions options = new();
        foreach (int count in new[] { 0, 1, 4, 8, 16, 17, 33, 65, 1025 })
        {
            Random random = new(42);
            Rgb[] source = new Rgb[count];
            Cmyk sentinel = new(.1F, .2F, .3F, .4F);
            Cmyk[] expected = Enumerable.Repeat(sentinel, count + 1).ToArray();
            Cmyk[] actual = expected.ToArray();

            for (int i = 0; i < count; i++)
            {
                source[i] = Rgb.FromScaledVector3(new Vector3(random.NextSingle(), random.NextSingle(), random.NextSingle()));
            }

            if (count >= 4)
            {
                // These values exercise full black, the scalar near-black cutoff, and bounds on extended RGB.
                source[0] = Rgb.FromScaledVector3(Vector3.Zero);
                source[1] = Rgb.FromScaledVector3(new Vector3(Constants.Epsilon / 2F));
                source[2] = Rgb.FromScaledVector3(new Vector3(-1F, .5F, 2F));
                source[3] = Rgb.FromScaledVector3(Vector3.One);
            }

            if (count >= 8)
            {
                // Each RGB channel can contain NaN. The bounded ink model must treat that channel as zero at every SIMD width.
                source[4] = Rgb.FromScaledVector3(new Vector3(float.NaN, .5F, 1F));
                source[5] = Rgb.FromScaledVector3(new Vector3(.5F, float.NaN, 1F));
                source[6] = Rgb.FromScaledVector3(new Vector3(.5F, 1F, float.NaN));
                source[7] = Rgb.FromScaledVector3(new Vector3(float.NegativeInfinity, .5F, float.PositiveInfinity));
            }

            for (int i = 0; i < count; i++)
            {
                expected[i] = Cmyk.FromProfileConnectingSpace(options, in source[i]);
            }

            Cmyk.FromProfileConnectionSpace(options, source, actual);

            Assert.Equal(expected, actual);
        }
    }
}
