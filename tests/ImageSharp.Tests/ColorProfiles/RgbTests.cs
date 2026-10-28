// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests the <see cref="Rgb"/> struct.
/// </summary>
[Trait("Color", "Conversion")]
public class RgbTests
{
    [Fact]
    public void RgbConstructorAssignsFields()
    {
        const float r = .75F;
        const float g = .64F;
        const float b = .87F;
        Rgb rgb = new(r, g, b);

        Assert.Equal(r, rgb.R);
        Assert.Equal(g, rgb.G);
        Assert.Equal(b, rgb.B);
    }

    [Fact]
    public void RgbEquality()
    {
        Rgb x = default;
        Rgb y = new(Vector3.One);

        Assert.True(default == default(Rgb));
        Assert.False(default != default(Rgb));
        Assert.Equal(default, default(Rgb));
        Assert.Equal(new Rgb(1, 0, 1), new Rgb(1, 0, 1));
        Assert.Equal(new Rgb(Vector3.One), new Rgb(Vector3.One));
        Assert.False(x.Equals(y));
        Assert.False(x.Equals((object)y));
        Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
    }

    [Fact]
    public void RgbAndRgb24Interop()
    {
        const byte r = 64;
        const byte g = 128;
        const byte b = 255;

        Rgb24 rgb24 = Rgb24.FromScaledVector4(new Rgb(r / 255F, g / 255F, b / 255F).ToScaledVector4());
        Rgb rgb2 = Rgb.FromScaledVector4(rgb24.ToScaledVector4());

        Assert.Equal(r, rgb24.R);
        Assert.Equal(g, rgb24.G);
        Assert.Equal(b, rgb24.B);

        Assert.Equal(r / 255F, rgb2.R);
        Assert.Equal(g / 255F, rgb2.G);
        Assert.Equal(b / 255F, rgb2.B);
    }

    [Fact]
    public void RgbAndRgba32Interop()
    {
        const byte r = 64;
        const byte g = 128;
        const byte b = 255;

        Rgba32 rgba32 = Rgba32.FromScaledVector4(new Rgb(r / 255F, g / 255F, b / 255F).ToScaledVector4());
        Rgb rgb2 = Rgb.FromScaledVector4(rgba32.ToScaledVector4());

        Assert.Equal(r, rgba32.R);
        Assert.Equal(g, rgba32.G);
        Assert.Equal(b, rgba32.B);

        Assert.Equal(r / 255F, rgb2.R);
        Assert.Equal(g / 255F, rgb2.G);
        Assert.Equal(b / 255F, rgb2.B);
    }

    /// <summary>
    /// Checks complete SIMD groups, tails and destination boundaries against the single-color conversion contract.
    /// </summary>
    [Fact]
    public void BulkScaledVectors_MatchSingleConversions() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckBulkScaledVectors,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Verifies exact component bits and opaque expansion without clipping floating-point RGB.
    /// </summary>
    private static void CheckBulkScaledVectors()
    {
        ReadOnlySpan<int> lengths = [0, 1, 3, 4, 7, 8, 15, 16, 17, 31, 32, 33, 127, 128, 129];
        Random random = new(42);

        foreach (int length in lengths)
        {
            // Negative and HDR values detect clipping. Exceptional components must also pass through without arithmetic.
            Vector4[] source = random.GenerateRandomVectorArray(length, -16F, 16F);
            if (length > 0)
            {
                source[0] = new Vector4(-0F, float.PositiveInfinity, float.NaN, -0.5F);
            }

            Rgb[] expectedRgb = new Rgb[length];
            Vector4[] expectedExpanded = new Vector4[length];
            for (int i = 0; i < length; i++)
            {
                expectedRgb[i] = Rgb.FromScaledVector4(source[i]);
                expectedExpanded[i] = expectedRgb[i].ToScaledVector4();
            }

            // Sentinels outside the generated range expose stores beyond the caller's converted prefix.
            Rgb sentinelRgb = new(float.MinValue, float.MinValue, float.MinValue);
            Vector4 sentinelVector = new(float.MinValue);
            Rgb[] actualRgb = new Rgb[length + 1];
            Vector4[] actualExpanded = new Vector4[length + 1];
            Array.Fill(actualRgb, sentinelRgb);
            Array.Fill(actualExpanded, sentinelVector);

            Rgb.FromScaledVector4(source, actualRgb);

            Assert.True(
                MemoryMarshal.AsBytes(expectedRgb.AsSpan()).SequenceEqual(MemoryMarshal.AsBytes(actualRgb.AsSpan(0, length))),
                $"RGB compaction changed component bits at length {length}.");

            Assert.Equal(sentinelRgb, actualRgb[length]);

            Rgb.ToScaledVector4(actualRgb.AsSpan(0, length), actualExpanded);

            Assert.True(
                MemoryMarshal.AsBytes(expectedExpanded.AsSpan()).SequenceEqual(MemoryMarshal.AsBytes(actualExpanded.AsSpan(0, length))),
                $"RGB expansion changed component bits at length {length}.");

            Assert.Equal(sentinelVector, actualExpanded[length]);
        }
    }
}
