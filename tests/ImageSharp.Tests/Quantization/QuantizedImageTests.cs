namespace SixLabors.ImageSharp.Tests
{
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing.Quantization;

    using Xunit;

    public class QuantizedImageTests
    {
        [Fact]
        public void QuantizersDitherByDefault()
        {
            var palette = new PaletteQuantizer();
            var octree = new OctreeQuantizer();
            var wu = new WuQuantizer();

            Assert.True(palette.Dither);
            Assert.True(octree.Dither);
            Assert.True(wu.Dither);
        }

        [Theory]
        [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32, true)]
        [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32, false)]
        public void PaletteQuantizerYieldsCorrectTransparentPixel<TPixel>(TestImageProvider<TPixel> provider, bool dither)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                Assert.True(image[0, 0].Equals(default(TPixel)));

                var quantizer = new PaletteQuantizer(dither);

                foreach (ImageFrame<TPixel> frame in image.Frames)
                {
                    QuantizedFrame<TPixel> quantized = quantizer.CreateFrameQuantizer<TPixel>().QuantizeFrame(frame);

                    int index = this.GetTransparentIndex(quantized);
                    Assert.Equal(index, quantized.Pixels[0]);
                }
            }
        }

        [Theory]
        [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32, true)]
        [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32, false)]
        public void OctreeQuantizerYieldsCorrectTransparentPixel<TPixel>(TestImageProvider<TPixel> provider, bool dither)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                Assert.True(image[0, 0].Equals(default(TPixel)));

                var quantizer = new OctreeQuantizer(dither);

                foreach (ImageFrame<TPixel> frame in image.Frames)
                {
                    QuantizedFrame<TPixel> quantized = quantizer.CreateFrameQuantizer<TPixel>().QuantizeFrame(frame);

                    int index = this.GetTransparentIndex(quantized);
                    Assert.Equal(index, quantized.Pixels[0]);
                }
            }
        }

        [Theory]
        [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32, true)]
        [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32, false)]
        public void WuQuantizerYieldsCorrectTransparentPixel<TPixel>(TestImageProvider<TPixel> provider, bool dither)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                Assert.True(image[0, 0].Equals(default(TPixel)));

                var quantizer = new WuQuantizer(dither);

                foreach (ImageFrame<TPixel> frame in image.Frames)
                {
                    QuantizedFrame<TPixel> quantized = quantizer.CreateFrameQuantizer<TPixel>().QuantizeFrame(frame);

                    int index = this.GetTransparentIndex(quantized);
                    Assert.Equal(index, quantized.Pixels[0]);
                }
            }
        }

        private int GetTransparentIndex<TPixel>(QuantizedFrame<TPixel> quantized)
            where TPixel : struct, IPixel<TPixel>
        {
            // Transparent pixels are much more likely to be found at the end of a palette
            int index = -1;
            var trans = default(Rgba32);
            for (int i = quantized.Palette.Length - 1; i >= 0; i--)
            {
                quantized.Palette[i].ToRgba32(ref trans);

                if (trans.Equals(default(Rgba32)))
                {
                    index = i;
                }
            }

            return index;
        }
    }
}