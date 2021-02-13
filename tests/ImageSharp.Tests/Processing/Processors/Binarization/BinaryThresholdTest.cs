// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Binarization;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Binarization
{
    public class BinaryThresholdTest
    {
        public static readonly TheoryData<float> BinaryThresholdValues
            = new TheoryData<float>
        {
            .25F,
            .75F
        };

        public static readonly string[] CommonTestImages =
        {
            TestImages.Png.Rgb48Bpp,
            TestImages.Png.ColorsSaturationLightness,
        };

        public const PixelTypes TestPixelTypes = PixelTypes.Rgba32 | PixelTypes.Bgra32 | PixelTypes.Rgb24;

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), nameof(BinaryThresholdValues), PixelTypes.Rgba32)]
        public void ImageShouldApplyBinaryThresholdFilter<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.BinaryThreshold(value));
                image.DebugSave(provider, value);
            }
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), nameof(BinaryThresholdValues), PixelTypes.Rgba32)]
        public void ImageShouldApplyBinaryThresholdInBox<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (Image<TPixel> image = source.Clone())
            {
                var bounds = new Rectangle(image.Width / 8, image.Height / 8, 6 * image.Width / 8, 6 * image.Width / 8);

                image.Mutate(x => x.BinaryThreshold(value, bounds));
                image.DebugSave(provider, value);

                ImageComparer.Tolerant().VerifySimilarityIgnoreRegion(source, image, bounds);
            }
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), nameof(BinaryThresholdValues), PixelTypes.Rgba32)]
        public void ImageShouldApplyBinarySaturationThresholdFilter<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.BinaryThreshold(value, BinaryThresholdColorComponent.Saturation));
                image.DebugSave(provider, value);
                image.CompareToReferenceOutput(ImageComparer.Exact, provider, value.ToString("0.00", System.Globalization.NumberFormatInfo.InvariantInfo));
            }
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), nameof(BinaryThresholdValues), PixelTypes.Rgba32)]
        public void ImageShouldApplyBinarySaturationThresholdInBox<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (Image<TPixel> image = source.Clone())
            {
                var bounds = new Rectangle(image.Width / 8, image.Height / 8, 6 * image.Width / 8, 6 * image.Width / 8);

                image.Mutate(x => x.BinaryThreshold(value, BinaryThresholdColorComponent.Saturation, bounds));
                image.DebugSave(provider, value);
                image.CompareToReferenceOutput(ImageComparer.Exact, provider, value.ToString("0.00", System.Globalization.NumberFormatInfo.InvariantInfo));
            }
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), nameof(BinaryThresholdValues), PixelTypes.Rgba32)]
        public void ImageShouldApplyBinaryMaxChromaThresholdFilter<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.BinaryThreshold(value, BinaryThresholdColorComponent.MaxChroma));
                image.DebugSave(provider, value);
                image.CompareToReferenceOutput(ImageComparer.Exact, provider, value.ToString("0.00", System.Globalization.NumberFormatInfo.InvariantInfo));
            }
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), nameof(BinaryThresholdValues), PixelTypes.Rgba32)]
        public void ImageShouldApplyBinaryMaxChromaThresholdInBox<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (Image<TPixel> image = source.Clone())
            {
                var bounds = new Rectangle(image.Width / 8, image.Height / 8, 6 * image.Width / 8, 6 * image.Width / 8);

                image.Mutate(x => x.BinaryThreshold(value, BinaryThresholdColorComponent.MaxChroma, bounds));
                image.DebugSave(provider, value);
                image.CompareToReferenceOutput(ImageComparer.Exact, provider, value.ToString("0.00", System.Globalization.NumberFormatInfo.InvariantInfo));
            }
        }
    }
}
