// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Quantization
{
    public class WuQuantizerTests
    {
        [Fact]
        public void SinglePixelOpaque()
        {
            Configuration config = Configuration.Default;
            var quantizer = new WuQuantizer(new QuantizerOptions { Dither = null });

            using var image = new Image<Rgba32>(config, 1, 1, Color.Black);
            ImageFrame<Rgba32> frame = image.Frames.RootFrame;

            using IFrameQuantizer<Rgba32> frameQuantizer = quantizer.CreateFrameQuantizer<Rgba32>(config);
            using IndexedImageFrame<Rgba32> result = frameQuantizer.QuantizeFrame(frame, frame.Bounds());

            Assert.Equal(1, result.Palette.Length);
            Assert.Equal(1, result.GetPixelBufferSpan().Length);

            Assert.Equal(Color.Black, (Color)result.Palette.Span[0]);
            Assert.Equal(0, result.GetPixelBufferSpan()[0]);
        }

        [Fact]
        public void SinglePixelTransparent()
        {
            Configuration config = Configuration.Default;
            var quantizer = new WuQuantizer(new QuantizerOptions { Dither = null });

            using var image = new Image<Rgba32>(config, 1, 1, default(Rgba32));
            ImageFrame<Rgba32> frame = image.Frames.RootFrame;

            using IFrameQuantizer<Rgba32> frameQuantizer = quantizer.CreateFrameQuantizer<Rgba32>(config);
            using IndexedImageFrame<Rgba32> result = frameQuantizer.QuantizeFrame(frame, frame.Bounds());

            Assert.Equal(1, result.Palette.Length);
            Assert.Equal(1, result.GetPixelBufferSpan().Length);

            Assert.Equal(default, result.Palette.Span[0]);
            Assert.Equal(0, result.GetPixelBufferSpan()[0]);
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
            using var image = new Image<Rgba32>(1, 256);

            for (int i = 0; i < 256; i++)
            {
                byte r = (byte)((i % 4) * 85);
                byte g = (byte)(((i / 4) % 4) * 85);
                byte b = (byte)(((i / 16) % 4) * 85);
                byte a = (byte)((i / 64) * 85);

                image[0, i] = new Rgba32(r, g, b, a);
            }

            Configuration config = Configuration.Default;
            var quantizer = new WuQuantizer(new QuantizerOptions { Dither = null });

            ImageFrame<Rgba32> frame = image.Frames.RootFrame;

            using IFrameQuantizer<Rgba32> frameQuantizer = quantizer.CreateFrameQuantizer<Rgba32>(config);
            using IndexedImageFrame<Rgba32> result = frameQuantizer.QuantizeFrame(frame, frame.Bounds());

            Assert.Equal(256, result.Palette.Length);
            Assert.Equal(256, result.GetPixelBufferSpan().Length);

            var actualImage = new Image<Rgba32>(1, 256);

            ReadOnlySpan<Rgba32> paletteSpan = result.Palette.Span;
            int paletteCount = paletteSpan.Length - 1;
            for (int y = 0; y < actualImage.Height; y++)
            {
                Span<Rgba32> row = actualImage.GetPixelRowSpan(y);
                ReadOnlySpan<byte> quantizedPixelSpan = result.GetPixelBufferSpan();
                int yy = y * actualImage.Width;

                for (int x = 0; x < actualImage.Width; x++)
                {
                    int i = x + yy;
                    row[x] = paletteSpan[Math.Min(paletteCount, quantizedPixelSpan[i])];
                }
            }

            Assert.True(image.GetPixelSpan().SequenceEqual(actualImage.GetPixelSpan()));
        }

        [Theory]
        [WithFile(TestImages.Png.LowColorVariance, PixelTypes.Rgba32)]
        public void LowVariance<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // See https://github.com/SixLabors/ImageSharp/issues/866
            using (Image<TPixel> image = provider.GetImage())
            {
                Configuration config = Configuration.Default;
                var quantizer = new WuQuantizer(new QuantizerOptions { Dither = null });
                ImageFrame<TPixel> frame = image.Frames.RootFrame;

                using IFrameQuantizer<TPixel> frameQuantizer = quantizer.CreateFrameQuantizer<TPixel>(config);
                using IndexedImageFrame<TPixel> result = frameQuantizer.QuantizeFrame(frame, frame.Bounds());

                Assert.Equal(48, result.Palette.Length);
            }
        }

        private static void TestScale(Func<byte, Rgba32> pixelBuilder)
        {
            using (var image = new Image<Rgba32>(1, 256))
            using (var expectedImage = new Image<Rgba32>(1, 256))
            using (var actualImage = new Image<Rgba32>(1, 256))
            {
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
                var quantizer = new WuQuantizer(new QuantizerOptions { Dither = null });

                ImageFrame<Rgba32> frame = image.Frames.RootFrame;
                using (IFrameQuantizer<Rgba32> frameQuantizer = quantizer.CreateFrameQuantizer<Rgba32>(config))
                using (IndexedImageFrame<Rgba32> result = frameQuantizer.QuantizeFrame(frame, frame.Bounds()))
                {
                    Assert.Equal(4 * 8, result.Palette.Length);
                    Assert.Equal(256, result.GetPixelBufferSpan().Length);

                    ReadOnlySpan<Rgba32> paletteSpan = result.Palette.Span;
                    int paletteCount = paletteSpan.Length - 1;
                    for (int y = 0; y < actualImage.Height; y++)
                    {
                        Span<Rgba32> row = actualImage.GetPixelRowSpan(y);
                        ReadOnlySpan<byte> quantizedPixelSpan = result.GetPixelBufferSpan();
                        int yy = y * actualImage.Width;

                        for (int x = 0; x < actualImage.Width; x++)
                        {
                            int i = x + yy;
                            row[x] = paletteSpan[Math.Min(paletteCount, quantizedPixelSpan[i])];
                        }
                    }
                }

                Assert.True(expectedImage.GetPixelSpan().SequenceEqual(actualImage.GetPixelSpan()));
            }
        }
    }
}
