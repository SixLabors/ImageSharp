// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Tests.Quantization;

public class AlphaAssociationQuantizerTests
{
    public static TheoryData<IDither?> Dithers { get; } = new()
    {
        null,
        KnownDitherings.Bayer4x4,
        KnownDitherings.FloydSteinberg
    };

    [Theory]
    [MemberData(nameof(Dithers))]
    public void WuQuantizerProducesEquivalentResultsForAssociatedPixels(IDither? dither)
    {
        byte[] alphaValues = [0, 1, 63, 64, 65, 127, 128, 254, 255];
        Rgba32[] source = new Rgba32[alphaValues.Length * 4];

        for (int i = 0; i < source.Length; i++)
        {
            byte alpha = alphaValues[i % alphaValues.Length];
            source[i] = alpha == 0 ? default : new Rgba32(255, 255, 255, alpha);
        }

        QuantizerOptions options = new()
        {
            Dither = dither,
            MaxColors = 8,
            TransparencyThreshold = .5F
        };

        Rgba32[] expected = Quantize<Rgba32>(source, options);
        Rgba32[] actual = Quantize<Rgba32P>(source, options);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PaletteQuantizerAppliesTransparencyThresholdToAssociatedPixels()
    {
        Configuration configuration = Configuration.Default;
        Rgba32P source = new(127, 0, 0, 127);
        Color[] palette = [Color.Transparent, Color.FromPixel(source)];
        QuantizerOptions options = new() { Dither = null, TransparencyThreshold = .5F };
        PaletteQuantizer quantizer = new(palette, options);

        using Image<Rgba32P> image = new(configuration, 1, 1, source);
        using IQuantizer<Rgba32P> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<Rgba32P>(configuration);
        using IndexedImageFrame<Rgba32P> result = frameQuantizer.BuildPaletteAndQuantizeFrame(image.Frames.RootFrame, image.Bounds);

        Assert.Equal(0, result.DangerousGetRowSpan(0)[0]);
        Assert.Equal(default, result.Palette.Span[0]);
    }

    [Theory]
    [InlineData(64 / 255F, (byte)63, (byte)64)]
    [InlineData(.5F, (byte)127, (byte)128)]
    [InlineData(1F, (byte)254, (byte)255)]
    public void WuQuantizerAppliesNormalizedTransparencyThresholdToAssociatedPixels(float threshold, byte below, byte retained)
    {
        Rgba32[] source =
        [
            new Rgba32(255, 255, 255, below),
            new Rgba32(255, 255, 255, retained)
        ];

        QuantizerOptions options = new()
        {
            Dither = null,
            MaxColors = 2,
            TransparencyThreshold = threshold
        };

        Rgba32[] expected = Quantize<Rgba32>(source, options);
        Rgba32[] actual = Quantize<Rgba32P>(source, options);

        Assert.Equal(expected, actual);
        Assert.Equal(default, actual[0]);
        Assert.NotEqual(0, actual[1].A);
    }

    private static Rgba32[] Quantize<TPixel>(Rgba32[] source, QuantizerOptions options)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Configuration configuration = Configuration.Default;
        using Image<TPixel> image = new(configuration, source.Length, 1);

        for (int x = 0; x < source.Length; x++)
        {
            image[x, 0] = TPixel.FromRgba32(source[x]);
        }

        WuQuantizer quantizer = new(options);
        ImageFrame<TPixel> frame = image.Frames.RootFrame;

        using IQuantizer<TPixel> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<TPixel>(configuration);
        using IndexedImageFrame<TPixel> result = frameQuantizer.BuildPaletteAndQuantizeFrame(frame, frame.Bounds);

        ReadOnlySpan<TPixel> palette = result.Palette.Span;
        ReadOnlySpan<byte> indices = result.DangerousGetRowSpan(0);
        Rgba32[] colors = new Rgba32[source.Length];

        for (int x = 0; x < colors.Length; x++)
        {
            colors[x] = palette[indices[x]].ToRgba32();
        }

        return colors;
    }
}
