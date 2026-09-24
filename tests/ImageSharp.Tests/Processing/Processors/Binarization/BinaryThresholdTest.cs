// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Binarization;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Binarization;

[Trait("Category", "Processors")]
public class BinaryThresholdTest
{
    [Theory]
    [InlineData(2F, 5F, 6F)]
    [InlineData(20F, 50F, 60F)]
    public void LuminanceThreshold_UsesObservedHdrRange(float minimum, float middle, float maximum)
    {
        using Image<RgbaVector> image = new(65, 1);
        for (int x = 0; x < image.Width; x++)
        {
            image[x, 0] = new RgbaVector(minimum, minimum, minimum);
        }

        image[32, 0] = new RgbaVector(middle, middle, middle);
        image[64, 0] = new RgbaVector(maximum, maximum, maximum);

        image.Mutate(x => x.BinaryThreshold(.5F));

        Assert.Equal(RgbaVector.FromRgba32(Color.Black.ToPixel<Rgba32>()), image[0, 0]);
        Assert.Equal(RgbaVector.FromRgba32(Color.White.ToPixel<Rgba32>()), image[32, 0]);
        Assert.Equal(RgbaVector.FromRgba32(Color.White.ToPixel<Rgba32>()), image[64, 0]);
    }

    [Fact]
    public void LuminanceThreshold_NonfiniteMetricsSelectLower()
    {
        using Image<RgbaVector> image = new(65, 1);
        for (int x = 0; x < image.Width; x++)
        {
            image[x, 0] = new RgbaVector(2F, 2F, 2F);
        }

        image[30, 0] = new RgbaVector(4F, 4F, 4F);
        image[31, 0] = new RgbaVector(float.NaN, 2F, 2F);
        image[32, 0] = new RgbaVector(float.PositiveInfinity, 2F, 2F);
        image[33, 0] = new RgbaVector(float.NegativeInfinity, 2F, 2F);
        image[34, 0] = new RgbaVector(4F, 4F, 4F, float.NaN);

        image.Mutate(x => x.BinaryThreshold(.5F));

        Assert.Equal(0F, image[0, 0].R);
        Assert.Equal(1F, image[30, 0].R);
        Assert.Equal(0F, image[31, 0].R);
        Assert.Equal(0F, image[32, 0].R);
        Assert.Equal(0F, image[33, 0].R);
        Assert.Equal(0F, image[34, 0].R);
    }

    [Fact]
    public void LuminanceThreshold_UsesOnlySelectedRegionForRange()
    {
        using Image<RgbaVector> image = new(65, 1);
        for (int x = 0; x < image.Width; x++)
        {
            image[x, 0] = new RgbaVector(2F, 2F, 2F);
        }

        image[32, 0] = new RgbaVector(4F, 4F, 4F);
        image[64, 0] = new RgbaVector(100F, 100F, 100F);

        image.Mutate(x => x.BinaryThreshold(.5F, new Rectangle(0, 0, 64, 1)));

        Assert.Equal(0F, image[0, 0].R);
        Assert.Equal(1F, image[32, 0].R);
        Assert.Equal(100F, image[64, 0].R);
    }

    [Theory]
    [InlineData(BinaryThresholdMode.Saturation)]
    [InlineData(BinaryThresholdMode.MaxChroma)]
    public void ColorMetricThreshold_UsesObservedRange(BinaryThresholdMode mode)
    {
        using Image<RgbaVector> image = new(65, 1);
        for (int x = 0; x < image.Width; x++)
        {
            image[x, 0] = new RgbaVector(.5F, .5F, .5F);
        }

        image[32, 0] = new RgbaVector(.8F, 0F, 0F);
        image[64, 0] = new RgbaVector(1F, 0F, 0F);

        image.Mutate(x => x.BinaryThreshold(.5F, mode));

        Assert.Equal(0F, image[0, 0].R);
        Assert.Equal(1F, image[32, 0].R);
        Assert.Equal(1F, image[64, 0].R);
    }

    [Fact]
    public void LuminanceThreshold_ConstantFiniteRangeSelectsUpper()
    {
        using Image<RgbaVector> image = new(65, 1, new RgbaVector(2F, 2F, 2F));

        image.Mutate(x => x.BinaryThreshold(.5F));

        Assert.Equal(1F, image[0, 0].R);
        Assert.Equal(1F, image[64, 0].R);
    }

    [Fact]
    public void LuminanceThreshold_WithoutFiniteMetricsSelectsLower()
    {
        using Image<RgbaVector> image = new(65, 1, new RgbaVector(float.NaN, float.NaN, float.NaN));

        image.Mutate(x => x.BinaryThreshold(.5F));

        Assert.Equal(0F, image[0, 0].R);
        Assert.Equal(0F, image[64, 0].R);
    }

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
        using (Image<TPixel> source = provider.GetImage())
        using (Image<TPixel> image = source.Clone())
        {
            image.Mutate(x => x.BinaryThreshold(value, BinaryThresholdMode.Saturation));
            image.DebugSave(provider, value);
            AssertObservedRangeThreshold(source, image, source.Bounds, value, BinaryThresholdMode.Saturation);
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
            AssertObservedRangeThreshold(source, image, bounds, value, BinaryThresholdMode.Saturation);
        }
    }

    [Theory]
    [WithFileCollection(nameof(CommonTestImages), nameof(BinaryThresholdValues), PixelTypes.Rgba32)]
    public void ImageShouldApplyBinaryMaxChromaThresholdFilter<TPixel>(TestImageProvider<TPixel> provider, float value)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> source = provider.GetImage())
        using (Image<TPixel> image = source.Clone())
        {
            image.Mutate(x => x.BinaryThreshold(value, BinaryThresholdMode.MaxChroma));
            image.DebugSave(provider, value);
            AssertObservedRangeThreshold(source, image, source.Bounds, value, BinaryThresholdMode.MaxChroma);
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
            AssertObservedRangeThreshold(source, image, bounds, value, BinaryThresholdMode.MaxChroma);
        }
    }

    /// <summary>
    /// Checks every output pixel against the observed range of the original image.
    /// </summary>
    private static void AssertObservedRangeThreshold<TPixel>(
        Image<TPixel> source,
        Image<TPixel> actual,
        Rectangle bounds,
        float fraction,
        BinaryThresholdMode mode)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Vector4[] vectors = new Vector4[source.Width];
        PixelOperations<TPixel> operations = PixelOperations<TPixel>.Instance;
        PixelConversionModifiers modifiers = PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply;
        float minimum = float.PositiveInfinity;
        float maximum = float.NegativeInfinity;

        // Use the same pixel conversion contract for the source values, then calculate
        // the range and expected mask independently from the unmodified image.
        for (int y = bounds.Top; y < bounds.Bottom; y++)
        {
            operations.ToVector4(source.Configuration, source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y), vectors, modifiers);

            for (int x = bounds.Left; x < bounds.Right; x++)
            {
                float metric = GetReferenceMetric(vectors[x], mode);
                minimum = MathF.Min(minimum, metric);
                maximum = MathF.Max(maximum, metric);
            }
        }

        float threshold = (float)(minimum + (fraction * ((double)maximum - minimum)));
        TPixel upper = Color.White.ToPixel<TPixel>();
        TPixel lower = Color.Black.ToPixel<TPixel>();

        for (int y = 0; y < source.Height; y++)
        {
            operations.ToVector4(source.Configuration, source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y), vectors, modifiers);

            for (int x = 0; x < source.Width; x++)
            {
                TPixel expected = bounds.Contains(x, y)
                    ? GetReferenceMetric(vectors[x], mode) >= threshold ? upper : lower
                    : source[x, y];

                TPixel actualPixel = actual[x, y];
                if (!expected.Equals(actualPixel))
                {
                    Assert.Fail($"({x}, {y}) source={source[x, y]} metric={GetReferenceMetric(vectors[x], mode)} threshold={threshold} expected={expected} actual={actualPixel}");
                }
            }
        }
    }

    /// <summary>
    /// Calculates the selected color metric from one straight source pixel.
    /// </summary>
    private static float GetReferenceMetric(Vector4 vector, BinaryThresholdMode mode)
    {
        float max = MathF.Max(vector.X, MathF.Max(vector.Y, vector.Z));
        float min = MathF.Min(vector.X, MathF.Min(vector.Y, vector.Z));

        if (mode == BinaryThresholdMode.Saturation)
        {
            float chroma = max - min;
            if (MathF.Abs(chroma) < Constants.Epsilon)
            {
                return 0F;
            }

            float lightness = (max + min) * .5F;
            return lightness <= .5F ? chroma / (max + min) : chroma / (2F - max - min);
        }

        float cb = (-0.168736F * vector.X) - (0.331264F * vector.Y) + (0.5F * vector.Z);
        float cr = (0.5F * vector.X) - (0.418688F * vector.Y) - (0.081312F * vector.Z);
        return MathF.Max(MathF.Abs(cb), MathF.Abs(cr));
    }
}
