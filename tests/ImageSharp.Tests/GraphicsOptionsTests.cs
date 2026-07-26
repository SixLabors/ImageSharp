// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests;

public class GraphicsOptionsTests
{
    private static readonly GraphicsOptionsComparer GraphicsOptionsComparer = new();
    private readonly GraphicsOptions newGraphicsOptions = new();
    private readonly GraphicsOptions cloneGraphicsOptions = new GraphicsOptions().DeepClone();

    [Fact]
    public void CloneGraphicsOptionsIsNotNull() => Assert.NotNull(this.cloneGraphicsOptions);

    [Fact]
    public void DefaultGraphicsOptionsAntialias()
    {
        Assert.True(this.newGraphicsOptions.Antialias);
        Assert.True(this.cloneGraphicsOptions.Antialias);
    }

    [Fact]
    public void DefaultGraphicsOptionsAntialiasThreshold()
    {
        const float expected = .5F;
        Assert.Equal(expected, this.newGraphicsOptions.AntialiasThreshold);
        Assert.Equal(expected, this.cloneGraphicsOptions.AntialiasThreshold);
    }

    [Fact]
    public void DefaultGraphicsOptionsBlendPercentage()
    {
        const float expected = 1F;
        Assert.Equal(expected, this.newGraphicsOptions.BlendPercentage);
        Assert.Equal(expected, this.cloneGraphicsOptions.BlendPercentage);
    }

    [Fact]
    public void DefaultGraphicsOptionsColorBlendingMode()
    {
        const PixelColorBlendingMode expected = PixelColorBlendingMode.Normal;
        Assert.Equal(expected, this.newGraphicsOptions.ColorBlendingMode);
        Assert.Equal(expected, this.cloneGraphicsOptions.ColorBlendingMode);
    }

    [Fact]
    public void DefaultGraphicsOptionsAlphaCompositionMode()
    {
        const PixelAlphaCompositionMode expected = PixelAlphaCompositionMode.SrcOver;
        Assert.Equal(expected, this.newGraphicsOptions.AlphaCompositionMode);
        Assert.Equal(expected, this.cloneGraphicsOptions.AlphaCompositionMode);
    }

    [Fact]
    public void NonDefaultClone()
    {
        GraphicsOptions expected = new()
        {
            AlphaCompositionMode = PixelAlphaCompositionMode.DestAtop,
            Antialias = false,
            AntialiasThreshold = .33F,
            BlendPercentage = .25F,
            ColorBlendingMode = PixelColorBlendingMode.HardLight,
        };

        GraphicsOptions actual = expected.DeepClone();

        Assert.Equal(expected, actual, GraphicsOptionsComparer);
    }

    [Fact]
    public void CloneIsDeep()
    {
        GraphicsOptions expected = new();
        GraphicsOptions actual = expected.DeepClone();

        actual.AlphaCompositionMode = PixelAlphaCompositionMode.DestAtop;
        actual.Antialias = false;
        actual.AntialiasThreshold = .67F;
        actual.BlendPercentage = .25F;
        actual.ColorBlendingMode = PixelColorBlendingMode.HardLight;

        Assert.NotEqual(expected, actual, GraphicsOptionsComparer);
    }
}
