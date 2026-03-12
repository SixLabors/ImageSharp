// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Quantization;

[Trait("Category", "Processors")]
public class OctreeQuantizerTests
{
    [Fact]
    public void OctreeQuantizerConstructor()
    {
        QuantizerOptions expected = new() { MaxColors = 128 };
        OctreeQuantizer quantizer = new(expected);

        Assert.Equal(expected.MaxColors, quantizer.Options.MaxColors);
        Assert.Equal(QuantizerConstants.DefaultDither, quantizer.Options.Dither);

        expected = new QuantizerOptions { Dither = null };
        quantizer = new OctreeQuantizer(expected);
        Assert.Equal(QuantizerConstants.MaxColors, quantizer.Options.MaxColors);
        Assert.Null(quantizer.Options.Dither);

        expected = new QuantizerOptions { Dither = KnownDitherings.Atkinson };
        quantizer = new OctreeQuantizer(expected);
        Assert.Equal(QuantizerConstants.MaxColors, quantizer.Options.MaxColors);
        Assert.Equal(KnownDitherings.Atkinson, quantizer.Options.Dither);

        expected = new QuantizerOptions { Dither = KnownDitherings.Atkinson, MaxColors = 0 };
        quantizer = new OctreeQuantizer(expected);
        Assert.Equal(QuantizerConstants.MinColors, quantizer.Options.MaxColors);
        Assert.Equal(KnownDitherings.Atkinson, quantizer.Options.Dither);
    }

    [Fact]
    public void OctreeQuantizerCanCreateFrameQuantizer()
    {
        OctreeQuantizer quantizer = new();
        IQuantizer<Rgba32> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<Rgba32>(Configuration.Default);

        Assert.NotNull(frameQuantizer);
        Assert.NotNull(frameQuantizer.Options);
        Assert.Equal(QuantizerConstants.DefaultDither, frameQuantizer.Options.Dither);
        frameQuantizer.Dispose();

        quantizer = new OctreeQuantizer(new QuantizerOptions { Dither = null });
        frameQuantizer = quantizer.CreatePixelSpecificQuantizer<Rgba32>(Configuration.Default);

        Assert.NotNull(frameQuantizer);
        Assert.Null(frameQuantizer.Options.Dither);
        frameQuantizer.Dispose();

        quantizer = new OctreeQuantizer(new QuantizerOptions { Dither = KnownDitherings.Atkinson });
        frameQuantizer = quantizer.CreatePixelSpecificQuantizer<Rgba32>(Configuration.Default);
        Assert.NotNull(frameQuantizer);
        Assert.Equal(KnownDitherings.Atkinson, frameQuantizer.Options.Dither);
        frameQuantizer.Dispose();
    }
}
