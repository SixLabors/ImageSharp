// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.PixelFormats;

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
        Assert.Equal(new(1, 0, 1), new Rgb(1, 0, 1));
        Assert.Equal(new(Vector3.One), new Rgb(Vector3.One));
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
}
