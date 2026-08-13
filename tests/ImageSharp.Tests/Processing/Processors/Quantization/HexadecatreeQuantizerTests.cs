// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Quantization;

[Trait("Category", "Processors")]
public class HexadecatreeQuantizerTests
{
    [Fact]
    public void HexadecatreeQuantizerConstructor()
    {
        QuantizerOptions expected = new() { MaxColors = 128 };
        HexadecatreeQuantizer quantizer = new(expected);

        Assert.Equal(expected.MaxColors, quantizer.Options.MaxColors);
        Assert.Equal(QuantizerConstants.DefaultDither, quantizer.Options.Dither);

        expected = new QuantizerOptions { Dither = null };
        quantizer = new HexadecatreeQuantizer(expected);
        Assert.Equal(QuantizerConstants.MaxColors, quantizer.Options.MaxColors);
        Assert.Null(quantizer.Options.Dither);

        expected = new QuantizerOptions { Dither = KnownDitherings.Atkinson };
        quantizer = new HexadecatreeQuantizer(expected);
        Assert.Equal(QuantizerConstants.MaxColors, quantizer.Options.MaxColors);
        Assert.Equal(KnownDitherings.Atkinson, quantizer.Options.Dither);

        expected = new QuantizerOptions { Dither = KnownDitherings.Atkinson, MaxColors = 0 };
        quantizer = new HexadecatreeQuantizer(expected);
        Assert.Equal(QuantizerConstants.MinColors, quantizer.Options.MaxColors);
        Assert.Equal(KnownDitherings.Atkinson, quantizer.Options.Dither);
    }

    [Fact]
    public void HexadecatreeQuantizerCanCreateFrameQuantizer()
    {
        HexadecatreeQuantizer quantizer = new();
        IQuantizer<Rgba32> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<Rgba32>(Configuration.Default);

        Assert.NotNull(frameQuantizer);
        Assert.NotNull(frameQuantizer.Options);
        Assert.Equal(QuantizerConstants.DefaultDither, frameQuantizer.Options.Dither);
        frameQuantizer.Dispose();

        quantizer = new HexadecatreeQuantizer(new QuantizerOptions { Dither = null });
        frameQuantizer = quantizer.CreatePixelSpecificQuantizer<Rgba32>(Configuration.Default);

        Assert.NotNull(frameQuantizer);
        Assert.Null(frameQuantizer.Options.Dither);
        frameQuantizer.Dispose();

        quantizer = new HexadecatreeQuantizer(new QuantizerOptions { Dither = KnownDitherings.Atkinson });
        frameQuantizer = quantizer.CreatePixelSpecificQuantizer<Rgba32>(Configuration.Default);
        Assert.NotNull(frameQuantizer);
        Assert.Equal(KnownDitherings.Atkinson, frameQuantizer.Options.Dither);
        frameQuantizer.Dispose();
    }

    [Fact]
    public void PaletteUsesNormalizedTransparencyThreshold()
    {
        using HexadecatreeQuantizer<Rgba32>.Hexadecatree tree = new(Configuration.Default, 8, 2, .5F);
        tree.AddColors([new Rgba32(255, 255, 255, 127)]);

        Span<Rgba32> palette = stackalloc Rgba32[2];
        short paletteIndex = 0;
        tree.Palettize(palette, ref paletteIndex);

        Assert.Equal(1, paletteIndex);
        Assert.Equal(default, palette[0]);
    }
}
