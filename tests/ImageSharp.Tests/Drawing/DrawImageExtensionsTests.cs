// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Drawing;
using SixLabors.ImageSharp.Tests.Processing;

namespace SixLabors.ImageSharp.Tests.Drawing;

public class DrawImageExtensionsTests : BaseImageOperationsExtensionTest
{
    [Fact]
    public void DrawImage_OpacityOnly_VerifyGraphicOptionsTakenFromContext()
    {
        // non-default values as we cant easily defect usage otherwise
        this.options.AlphaCompositionMode = PixelAlphaCompositionMode.Xor;
        this.options.ColorBlendingMode = PixelColorBlendingMode.Screen;

        using Image<Rgba32> image = new(Configuration.Default, 1, 1);
        this.operations.DrawImage(image, 0.5f);
        DrawImageProcessor dip = this.Verify<DrawImageProcessor>();

        Assert.Equal(0.5, dip.Opacity);
        Assert.Equal(this.options.AlphaCompositionMode, dip.AlphaCompositionMode);
        Assert.Equal(this.options.ColorBlendingMode, dip.ColorBlendingMode);
    }

    [Fact]
    public void DrawImage_OpacityAndBlending_VerifyGraphicOptionsTakenFromContext()
    {
        // non-default values as we cant easily defect usage otherwise
        this.options.AlphaCompositionMode = PixelAlphaCompositionMode.Xor;
        this.options.ColorBlendingMode = PixelColorBlendingMode.Screen;

        using Image<Rgba32> image = new(Configuration.Default, 1, 1);
        this.operations.DrawImage(image, PixelColorBlendingMode.Multiply, 0.5f);
        DrawImageProcessor dip = this.Verify<DrawImageProcessor>();

        Assert.Equal(0.5, dip.Opacity);
        Assert.Equal(this.options.AlphaCompositionMode, dip.AlphaCompositionMode);
        Assert.Equal(PixelColorBlendingMode.Multiply, dip.ColorBlendingMode);
    }

    [Fact]
    public void DrawImage_LocationAndOpacity_VerifyGraphicOptionsTakenFromContext()
    {
        // non-default values as we cant easily defect usage otherwise
        this.options.AlphaCompositionMode = PixelAlphaCompositionMode.Xor;
        this.options.ColorBlendingMode = PixelColorBlendingMode.Screen;

        using Image<Rgba32> image = new(Configuration.Default, 1, 1);
        this.operations.DrawImage(image, Point.Empty, 0.5f);
        DrawImageProcessor dip = this.Verify<DrawImageProcessor>();

        Assert.Equal(0.5, dip.Opacity);
        Assert.Equal(this.options.AlphaCompositionMode, dip.AlphaCompositionMode);
        Assert.Equal(this.options.ColorBlendingMode, dip.ColorBlendingMode);
    }

    [Fact]
    public void DrawImage_LocationAndOpacityAndBlending_VerifyGraphicOptionsTakenFromContext()
    {
        // non-default values as we cant easily defect usage otherwise
        this.options.AlphaCompositionMode = PixelAlphaCompositionMode.Xor;
        this.options.ColorBlendingMode = PixelColorBlendingMode.Screen;

        using Image<Rgba32> image = new(Configuration.Default, 1, 1);
        this.operations.DrawImage(image, Point.Empty, PixelColorBlendingMode.Multiply, 0.5f);
        DrawImageProcessor dip = this.Verify<DrawImageProcessor>();

        Assert.Equal(0.5, dip.Opacity);
        Assert.Equal(this.options.AlphaCompositionMode, dip.AlphaCompositionMode);
        Assert.Equal(PixelColorBlendingMode.Multiply, dip.ColorBlendingMode);
    }
}
