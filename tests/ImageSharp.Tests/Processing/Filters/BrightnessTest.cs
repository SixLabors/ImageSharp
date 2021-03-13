// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Effects
{
    public class BrightnessTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Brightness_amount_BrightnessProcessorDefaultsSet()
        {
            this.operations.Brightness(1.5F);
            BrightnessProcessor processor = this.Verify<BrightnessProcessor>();

            Assert.Equal(1.5F, processor.Amount);
        }

        [Fact]
        public void Brightness_amount_rect_BrightnessProcessorDefaultsSet()
        {
            this.operations.Brightness(1.5F, this.rect);
            BrightnessProcessor processor = this.Verify<BrightnessProcessor>(this.rect);

            Assert.Equal(1.5F, processor.Amount);
        }

        [Fact]
        public void Brightness_scaled_vector()
        {
            var rgbImage = new Image<Rgb24>(Configuration.Default, 100, 100, new Rgb24(0, 0, 0));

            rgbImage.Mutate(x => x.ApplyProcessor(new BrightnessProcessor(2)));

            Assert.Equal(new Rgb24(0, 0, 0), rgbImage[0, 0]);

            rgbImage = new Image<Rgb24>(Configuration.Default, 100, 100, new Rgb24(10, 10, 10));

            rgbImage.Mutate(x => x.ApplyProcessor(new BrightnessProcessor(2)));

            Assert.Equal(new Rgb24(20, 20, 20), rgbImage[0, 0]);

            var halfSingleImage = new Image<HalfSingle>(Configuration.Default, 100, 100, new HalfSingle(-1));

            halfSingleImage.Mutate(x => x.ApplyProcessor(new BrightnessProcessor(2)));

            Assert.Equal(new HalfSingle(-1), halfSingleImage[0, 0]);

            halfSingleImage = new Image<HalfSingle>(Configuration.Default, 100, 100, new HalfSingle(-0.5f));

            halfSingleImage.Mutate(x => x.ApplyProcessor(new BrightnessProcessor(2)));

            Assert.Equal(new HalfSingle(0), halfSingleImage[0, 0]);
        }
    }
}
