// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Tests.Quantization;

public class WuQuantizerTests
{
    [Fact]
    public void SinglePixelOpaque()
    {
        Configuration config = Configuration.Default;
        WuQuantizer quantizer = new(new QuantizerOptions { Dither = null });

        using Image<Rgba32> image = new(config, 1, 1, Color.Black.ToPixel<Rgba32>());
        ImageFrame<Rgba32> frame = image.Frames.RootFrame;

        using IQuantizer<Rgba32> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<Rgba32>(config);
        using IndexedImageFrame<Rgba32> result = frameQuantizer.BuildPaletteAndQuantizeFrame(frame, frame.Bounds);

        Assert.Equal(1, result.Palette.Length);
        Assert.Equal(1, result.Width);
        Assert.Equal(1, result.Height);

        Assert.Equal(Color.Black, Color.FromPixel(result.Palette.Span[0]));
        Assert.Equal(0, result.DangerousGetRowSpan(0)[0]);
    }

    [Fact]
    public void SinglePixelTransparent()
    {
        Configuration config = Configuration.Default;
        WuQuantizer quantizer = new(new QuantizerOptions { Dither = null });

        using Image<Rgba32> image = new(config, 1, 1, default(Rgba32));
        ImageFrame<Rgba32> frame = image.Frames.RootFrame;

        using IQuantizer<Rgba32> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<Rgba32>(config);
        using IndexedImageFrame<Rgba32> result = frameQuantizer.BuildPaletteAndQuantizeFrame(frame, frame.Bounds);

        Assert.Equal(1, result.Palette.Length);
        Assert.Equal(1, result.Width);
        Assert.Equal(1, result.Height);

        Assert.Equal(default, result.Palette.Span[0]);
        Assert.Equal(0, result.DangerousGetRowSpan(0)[0]);
    }

    [Fact]
    public void GrayScale() => TestScale(c => new Rgba32(c, c, c, 128));

    [Fact]
    public void RedScale() => TestScale(c => new Rgba32(c, 0, 0, 128));

    [Fact]
    public void GreenScale() => TestScale(c => new Rgba32(0, c, 0, 128));

    [Fact]
    public void BlueScale() => TestScale(c => new Rgba32(0, 0, c, 128));

    [Fact]
    public void AlphaScale() => TestScale(c => new Rgba32(0, 0, 0, c));

    [Fact]
    public void Palette256()
    {
        using Image<Rgba32> image = new(1, 256);

        for (int i = 0; i < 256; i++)
        {
            byte r = (byte)((i % 4) * 85);
            byte g = (byte)(((i / 4) % 4) * 85);
            byte b = (byte)(((i / 16) % 4) * 85);
            byte a = (byte)((i / 64) * 85);

            image[0, i] = new Rgba32(r, g, b, a);
        }

        Configuration config = Configuration.Default;
        WuQuantizer quantizer = new(new QuantizerOptions { Dither = null });

        ImageFrame<Rgba32> frame = image.Frames.RootFrame;

        using IQuantizer<Rgba32> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<Rgba32>(config);
        using IndexedImageFrame<Rgba32> result = frameQuantizer.BuildPaletteAndQuantizeFrame(frame, frame.Bounds);

        Assert.Equal(256, result.Palette.Length);
        Assert.Equal(1, result.Width);
        Assert.Equal(256, result.Height);

        using Image<Rgba32> actualImage = new(1, 256);

        actualImage.ProcessPixelRows(accessor =>
        {
            ReadOnlySpan<Rgba32> paletteSpan = result.Palette.Span;
            int paletteCount = paletteSpan.Length - 1;
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                ReadOnlySpan<byte> quantizedPixelSpan = result.DangerousGetRowSpan(y);

                for (int x = 0; x < accessor.Width; x++)
                {
                    row[x] = paletteSpan[Math.Min(paletteCount, quantizedPixelSpan[x])];
                }
            }
        });

        image.ProcessPixelRows(actualImage, static (imageAccessor, actualImageAccessor) =>
        {
            for (int y = 0; y < imageAccessor.Height; y++)
            {
                Assert.True(imageAccessor.GetRowSpan(y).SequenceEqual(actualImageAccessor.GetRowSpan(y)));
            }
        });
    }

    [Theory]
    [WithFile(TestImages.Png.LowColorVariance, PixelTypes.Rgba32)]
    public void LowVariance<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // See https://github.com/SixLabors/ImageSharp/issues/866
        using Image<TPixel> image = provider.GetImage();
        Configuration config = Configuration.Default;
        WuQuantizer quantizer = new(new QuantizerOptions { Dither = null });
        ImageFrame<TPixel> frame = image.Frames.RootFrame;

        using IQuantizer<TPixel> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<TPixel>(config);
        using IndexedImageFrame<TPixel> result = frameQuantizer.BuildPaletteAndQuantizeFrame(frame, frame.Bounds);

        Assert.Equal(48, result.Palette.Length);
    }

    private static void TestScale(Func<byte, Rgba32> pixelBuilder)
    {
        using Image<Rgba32> image = new(1, 256);
        using Image<Rgba32> expectedImage = new(1, 256);
        using Image<Rgba32> actualImage = new(1, 256);
        for (int i = 0; i < 256; i++)
        {
            byte c = (byte)i;
            image[0, i] = pixelBuilder.Invoke(c);
        }

        for (int i = 0; i < 256; i++)
        {
            byte c = (byte)((i & ~7) + 4);
            expectedImage[0, i] = pixelBuilder.Invoke(c);
        }

        Configuration config = Configuration.Default;
        WuQuantizer quantizer = new(new QuantizerOptions { Dither = null });

        ImageFrame<Rgba32> frame = image.Frames.RootFrame;
        using (IQuantizer<Rgba32> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<Rgba32>(config))
        using (IndexedImageFrame<Rgba32> result = frameQuantizer.BuildPaletteAndQuantizeFrame(frame, frame.Bounds))
        {
            Assert.Equal(4 * 8, result.Palette.Length);
            Assert.Equal(1, result.Width);
            Assert.Equal(256, result.Height);

            actualImage.ProcessPixelRows(accessor =>
            {
                ReadOnlySpan<Rgba32> paletteSpan = result.Palette.Span;
                int paletteCount = paletteSpan.Length - 1;
                for (int y = 0; y < accessor.Height; y++)
                {
                    Span<Rgba32> row = accessor.GetRowSpan(y);
                    ReadOnlySpan<byte> quantizedPixelSpan = result.DangerousGetRowSpan(y);

                    for (int x = 0; x < accessor.Width; x++)
                    {
                        row[x] = paletteSpan[Math.Min(paletteCount, quantizedPixelSpan[x])];
                    }
                }
            });
        }

        expectedImage.ProcessPixelRows(actualImage, static (expectedAccessor, actualAccessor) =>
        {
            for (int y = 0; y < expectedAccessor.Height; y++)
            {
                Assert.True(expectedAccessor.GetRowSpan(y).SequenceEqual(actualAccessor.GetRowSpan(y)));
            }
        });
    }
}
