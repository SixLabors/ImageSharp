// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Tests;

public class QuantizedImageTests
{
    private Configuration Configuration => Configuration.Default;

    [Fact]
    public void QuantizersDitherByDefault()
    {
        WernerPaletteQuantizer werner = new();
        WebSafePaletteQuantizer webSafe = new();
        OctreeQuantizer octree = new();
        WuQuantizer wu = new();

        Assert.NotNull(werner.Options.Dither);
        Assert.NotNull(webSafe.Options.Dither);
        Assert.NotNull(octree.Options.Dither);
        Assert.NotNull(wu.Options.Dither);

        using (IQuantizer<Rgba32> quantizer = werner.CreatePixelSpecificQuantizer<Rgba32>(this.Configuration))
        {
            Assert.NotNull(quantizer.Options.Dither);
        }

        using (IQuantizer<Rgba32> quantizer = webSafe.CreatePixelSpecificQuantizer<Rgba32>(this.Configuration))
        {
            Assert.NotNull(quantizer.Options.Dither);
        }

        using (IQuantizer<Rgba32> quantizer = octree.CreatePixelSpecificQuantizer<Rgba32>(this.Configuration))
        {
            Assert.NotNull(quantizer.Options.Dither);
        }

        using (IQuantizer<Rgba32> quantizer = wu.CreatePixelSpecificQuantizer<Rgba32>(this.Configuration))
        {
            Assert.NotNull(quantizer.Options.Dither);
        }
    }

    [Theory]
    [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32, true)]
    [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32, false)]
    public void OctreeQuantizerYieldsCorrectTransparentPixel<TPixel>(
        TestImageProvider<TPixel> provider,
        bool dither)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        Assert.True(image[0, 0].Equals(default));

        QuantizerOptions options = new();
        if (!dither)
        {
            options.Dither = null;
        }

        OctreeQuantizer quantizer = new(options);

        foreach (ImageFrame<TPixel> frame in image.Frames)
        {
            using IQuantizer<TPixel> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<TPixel>(this.Configuration);
            using IndexedImageFrame<TPixel> quantized = frameQuantizer.BuildPaletteAndQuantizeFrame(frame, frame.Bounds);
            int index = this.GetTransparentIndex(quantized);
            Assert.Equal(index, quantized.DangerousGetRowSpan(0)[0]);
        }
    }

    [Theory]
    [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32, true)]
    [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32, false)]
    public void WuQuantizerYieldsCorrectTransparentPixel<TPixel>(TestImageProvider<TPixel> provider, bool dither)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        Assert.True(image[0, 0].Equals(default));

        QuantizerOptions options = new();
        if (!dither)
        {
            options.Dither = null;
        }

        WuQuantizer quantizer = new(options);

        foreach (ImageFrame<TPixel> frame in image.Frames)
        {
            using IQuantizer<TPixel> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<TPixel>(this.Configuration);
            using IndexedImageFrame<TPixel> quantized = frameQuantizer.BuildPaletteAndQuantizeFrame(frame, frame.Bounds);
            int index = this.GetTransparentIndex(quantized);
            Assert.Equal(index, quantized.DangerousGetRowSpan(0)[0]);
        }
    }

    // Test case for issue: https://github.com/SixLabors/ImageSharp/issues/1505
    [Theory]
    [WithFile(TestImages.Gif.Issues.Issue1505, PixelTypes.Rgba32)]
    public void Issue1505<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        OctreeQuantizer octreeQuantizer = new();
        IQuantizer<TPixel> quantizer = octreeQuantizer.CreatePixelSpecificQuantizer<TPixel>(Configuration.Default, new() { MaxColors = 128 });
        ImageFrame<TPixel> frame = image.Frames[0];
        quantizer.BuildPaletteAndQuantizeFrame(frame, frame.Bounds);
    }

    private int GetTransparentIndex<TPixel>(IndexedImageFrame<TPixel> quantized)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Transparent pixels are much more likely to be found at the end of a palette
        int index = -1;
        ReadOnlySpan<TPixel> paletteSpan = quantized.Palette.Span;
        Span<Rgba32> colorSpan = stackalloc Rgba32[QuantizerConstants.MaxColors].Slice(0, paletteSpan.Length);

        PixelOperations<TPixel>.Instance.ToRgba32(quantized.Configuration, paletteSpan, colorSpan);
        for (int i = colorSpan.Length - 1; i >= 0; i--)
        {
            if (colorSpan[i].Equals(default))
            {
                index = i;
            }
        }

        return index;
    }
}
