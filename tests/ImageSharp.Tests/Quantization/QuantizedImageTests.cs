// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class QuantizedImageTests
    {
        private Configuration Configuration => Configuration.Default;

        [Fact]
        public void QuantizersDitherByDefault()
        {
            var werner = new WernerPaletteQuantizer();
            var webSafe = new WebSafePaletteQuantizer();
            var octree = new OctreeQuantizer();
            var wu = new WuQuantizer();

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
            using (Image<TPixel> image = provider.GetImage())
            {
                Assert.True(image[0, 0].Equals(default));

                var options = new QuantizerOptions();
                if (!dither)
                {
                    options.Dither = null;
                }

                var quantizer = new OctreeQuantizer(options);

                foreach (ImageFrame<TPixel> frame in image.Frames)
                {
                    using (IQuantizer<TPixel> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<TPixel>(this.Configuration))
                    using (IndexedImageFrame<TPixel> quantized = frameQuantizer.BuildPaletteAndQuantizeFrame(frame, frame.Bounds()))
                    {
                        int index = this.GetTransparentIndex(quantized);
                        Assert.Equal(index, quantized.GetPixelRowSpan(0)[0]);
                    }
                }
            }
        }

        [Theory]
        [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32, true)]
        [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32, false)]
        public void WuQuantizerYieldsCorrectTransparentPixel<TPixel>(TestImageProvider<TPixel> provider, bool dither)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                Assert.True(image[0, 0].Equals(default));

                var options = new QuantizerOptions();
                if (!dither)
                {
                    options.Dither = null;
                }

                var quantizer = new WuQuantizer(options);

                foreach (ImageFrame<TPixel> frame in image.Frames)
                {
                    using (IQuantizer<TPixel> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<TPixel>(this.Configuration))
                    using (IndexedImageFrame<TPixel> quantized = frameQuantizer.BuildPaletteAndQuantizeFrame(frame, frame.Bounds()))
                    {
                        int index = this.GetTransparentIndex(quantized);
                        Assert.Equal(index, quantized.GetPixelRowSpan(0)[0]);
                    }
                }
            }
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
}
