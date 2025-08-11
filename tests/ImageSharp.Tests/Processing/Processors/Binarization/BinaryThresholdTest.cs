// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Globalization;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Binarization;

[Trait("Category", "Processors")]
public class BinaryThresholdTest
{
    public static readonly TheoryData<float> BinaryThresholdValues
        = new()
        {
        .25F,
        .75F
    };

    public static readonly string[] CommonTestImages =
    [
        TestImages.Png.Rgb48Bpp,
        TestImages.Png.ColorsSaturationLightness
    ];

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
            Rectangle bounds = new(image.Width / 8, image.Height / 8, 6 * image.Width / 8, 6 * image.Width / 8);

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
            image.Mutate(x => x.BinaryThreshold(value, BinaryThresholdMode.Saturation));
            image.DebugSave(provider, value);
            image.CompareToReferenceOutput(ImageComparer.Exact, provider, value.ToString("0.00", NumberFormatInfo.InvariantInfo));
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
            Rectangle bounds = new(image.Width / 8, image.Height / 8, 6 * image.Width / 8, 6 * image.Width / 8);

            image.Mutate(x => x.BinaryThreshold(value, BinaryThresholdMode.Saturation, bounds));
            image.DebugSave(provider, value);
            image.CompareToReferenceOutput(ImageComparer.Exact, provider, value.ToString("0.00", NumberFormatInfo.InvariantInfo));
        }
    }

    [Theory]
    [WithFileCollection(nameof(CommonTestImages), nameof(BinaryThresholdValues), PixelTypes.Rgba32)]
    public void ImageShouldApplyBinaryMaxChromaThresholdFilter<TPixel>(TestImageProvider<TPixel> provider, float value)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage())
        {
            image.Mutate(x => x.BinaryThreshold(value, BinaryThresholdMode.MaxChroma));
            image.DebugSave(provider, value);

            if (!TestEnvironment.Is64BitProcess && TestEnvironment.IsFramework)
            {
                ImageComparer comparer = ImageComparer.TolerantPercentage(0.0004F);
                image.CompareToReferenceOutput(comparer, provider, value.ToString("0.00", NumberFormatInfo.InvariantInfo));
            }
            else
            {
                image.CompareToReferenceOutput(ImageComparer.Exact, provider, value.ToString("0.00", NumberFormatInfo.InvariantInfo));
            }
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
            Rectangle bounds = new(image.Width / 8, image.Height / 8, 6 * image.Width / 8, 6 * image.Width / 8);

            image.Mutate(x => x.BinaryThreshold(value, BinaryThresholdMode.MaxChroma, bounds));
            image.DebugSave(provider, value);

            if (!TestEnvironment.Is64BitProcess && TestEnvironment.IsFramework)
            {
                ImageComparer comparer = ImageComparer.TolerantPercentage(0.0004F);
                image.CompareToReferenceOutput(comparer, provider, value.ToString("0.00", NumberFormatInfo.InvariantInfo));
            }
            else
            {
                image.CompareToReferenceOutput(ImageComparer.Exact, provider, value.ToString("0.00", NumberFormatInfo.InvariantInfo));
            }
        }
    }
}
