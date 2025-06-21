// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Quantization;

[Trait("Category", "Processors")]
public class PaletteQuantizerTests
{
    private static readonly Color[] Palette = { Color.Red, Color.Green, Color.Blue };

    [Fact]
    public void PaletteQuantizerConstructor()
    {
        QuantizerOptions expected = new() { MaxColors = 128 };
        PaletteQuantizer quantizer = new(Palette, expected);

        Assert.Equal(expected.MaxColors, quantizer.Options.MaxColors);
        Assert.Equal(QuantizerConstants.DefaultDither, quantizer.Options.Dither);

        expected = new() { Dither = null };
        quantizer = new(Palette, expected);
        Assert.Equal(QuantizerConstants.MaxColors, quantizer.Options.MaxColors);
        Assert.Null(quantizer.Options.Dither);

        expected = new() { Dither = KnownDitherings.Atkinson };
        quantizer = new(Palette, expected);
        Assert.Equal(QuantizerConstants.MaxColors, quantizer.Options.MaxColors);
        Assert.Equal(KnownDitherings.Atkinson, quantizer.Options.Dither);

        expected = new() { Dither = KnownDitherings.Atkinson, MaxColors = 0 };
        quantizer = new(Palette, expected);
        Assert.Equal(QuantizerConstants.MinColors, quantizer.Options.MaxColors);
        Assert.Equal(KnownDitherings.Atkinson, quantizer.Options.Dither);
    }

    [Fact]
    public void PaletteQuantizerCanCreateFrameQuantizer()
    {
        PaletteQuantizer quantizer = new(Palette);
        IQuantizer<Rgba32> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<Rgba32>(Configuration.Default);

        Assert.NotNull(frameQuantizer);
        Assert.NotNull(frameQuantizer.Options);
        Assert.Equal(QuantizerConstants.DefaultDither, frameQuantizer.Options.Dither);
        frameQuantizer.Dispose();

        quantizer = new(Palette, new() { Dither = null });
        frameQuantizer = quantizer.CreatePixelSpecificQuantizer<Rgba32>(Configuration.Default);

        Assert.NotNull(frameQuantizer);
        Assert.Null(frameQuantizer.Options.Dither);
        frameQuantizer.Dispose();

        quantizer = new(Palette, new() { Dither = KnownDitherings.Atkinson });
        frameQuantizer = quantizer.CreatePixelSpecificQuantizer<Rgba32>(Configuration.Default);
        Assert.NotNull(frameQuantizer);
        Assert.Equal(KnownDitherings.Atkinson, frameQuantizer.Options.Dither);
        frameQuantizer.Dispose();
    }

    [Fact]
    public void KnownQuantizersWebSafeTests()
    {
        IQuantizer quantizer = KnownQuantizers.WebSafe;
        Assert.Equal(QuantizerConstants.DefaultDither, quantizer.Options.Dither);
    }

    [Fact]
    public void KnownQuantizersWernerTests()
    {
        IQuantizer quantizer = KnownQuantizers.Werner;
        Assert.Equal(QuantizerConstants.DefaultDither, quantizer.Options.Dither);
    }
}
