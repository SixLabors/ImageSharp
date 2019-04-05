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
            var quantizer = new WuQuantizer(false);

            using (var image = new Image<Rgba32>(config, 1, 1, Rgba32.Black))
            using (QuantizedFrame<Rgba32> result = quantizer.CreateFrameQuantizer<Rgba32>(config).QuantizeFrame(image.Frames[0]))
            {
                Assert.Equal(1, result.Palette.Length);
                Assert.Equal(1, result.GetPixelSpan().Length);

                Assert.Equal(Rgba32.Black, result.Palette[0]);
                Assert.Equal(0, result.GetPixelSpan()[0]);
            }
        }

        [Fact]
        public void SinglePixelTransparent()
        {
            Configuration config = Configuration.Default;
            var quantizer = new WuQuantizer(false);

            using (var image = new Image<Rgba32>(config, 1, 1, default(Rgba32)))
            using (QuantizedFrame<Rgba32> result = quantizer.CreateFrameQuantizer<Rgba32>(config).QuantizeFrame(image.Frames[0]))
            {
                Assert.Equal(1, result.Palette.Length);
                Assert.Equal(1, result.GetPixelSpan().Length);

                Assert.Equal(default, result.Palette[0]);
                Assert.Equal(0, result.GetPixelSpan()[0]);
            }
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
            var image = new Image<Rgba32>(1, 256);

            for (int i = 0; i < 256; i++)
            {
                byte r = (byte)((i % 4) * 85);
                byte g = (byte)(((i / 4) % 4) * 85);
                byte b = (byte)(((i / 16) % 4) * 85);
                byte a = (byte)((i / 64) * 85);

                image[0, i] = new Rgba32(r, g, b, a);
            }

            Configuration config = Configuration.Default;
            var quantizer = new WuQuantizer(false);
            QuantizedFrame<Rgba32> result = quantizer.CreateFrameQuantizer<Rgba32>(config).QuantizeFrame(image.Frames[0]);

            Assert.Equal(256, result.Palette.Length);
            Assert.Equal(256, result.GetPixelSpan().Length);

            var actualImage = new Image<Rgba32>(1, 256);

            int paletteCount = result.Palette.Length - 1;
            for (int y = 0; y < actualImage.Height; y++)
            {
                Span<Rgba32> row = actualImage.GetPixelRowSpan(y);
                ReadOnlySpan<byte> quantizedPixelSpan = result.GetPixelSpan();
                int yy = y * actualImage.Width;

                for (int x = 0; x < actualImage.Width; x++)
                {
                    int i = x + yy;
                    row[x] = result.Palette[Math.Min(paletteCount, quantizedPixelSpan[i])];
                }
            }

            Assert.True(image.GetPixelSpan().SequenceEqual(actualImage.GetPixelSpan()));
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
                var quantizer = new WuQuantizer(false);
                using (QuantizedFrame<Rgba32> result = quantizer.CreateFrameQuantizer<Rgba32>(config).QuantizeFrame(image.Frames[0]))
                {
                    Assert.Equal(4 * 8, result.Palette.Length);
                    Assert.Equal(256, result.GetPixelSpan().Length);

                    int paletteCount = result.Palette.Length - 1;
                    for (int y = 0; y < actualImage.Height; y++)
                    {
                        Span<Rgba32> row = actualImage.GetPixelRowSpan(y);
                        ReadOnlySpan<byte> quantizedPixelSpan = result.GetPixelSpan();
                        int yy = y * actualImage.Width;

                        for (int x = 0; x < actualImage.Width; x++)
                        {
                            int i = x + yy;
                            row[x] = result.Palette[Math.Min(paletteCount, quantizedPixelSpan[i])];
                        }
                    }
                }

                Assert.True(expectedImage.GetPixelSpan().SequenceEqual(actualImage.GetPixelSpan()));
            }
        }
    }
}