// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Binarization;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Processing.Binarization;

[Trait("Category", "Processors")]
public class AdaptiveThresholdTests : BaseImageOperationsExtensionTest
{
    [Fact]
    public void AdaptiveThreshold_UsesDefaults_Works()
    {
        // arrange
        float expectedThresholdLimit = .85f;
        Color expectedUpper = Color.White;
        Color expectedLower = Color.Black;

        // act
        this.operations.AdaptiveThreshold();

        // assert
        AdaptiveThresholdProcessor p = this.Verify<AdaptiveThresholdProcessor>();
        Assert.Equal(expectedThresholdLimit, p.ThresholdLimit);
        Assert.Equal(expectedUpper, p.Upper);
        Assert.Equal(expectedLower, p.Lower);
    }

    [Fact]
    public void AdaptiveThreshold_SettingThresholdLimit_Works()
    {
        // arrange
        float expectedThresholdLimit = .65f;

        // act
        this.operations.AdaptiveThreshold(expectedThresholdLimit);

        // assert
        AdaptiveThresholdProcessor p = this.Verify<AdaptiveThresholdProcessor>();
        Assert.Equal(expectedThresholdLimit, p.ThresholdLimit);
        Assert.Equal(Color.White, p.Upper);
        Assert.Equal(Color.Black, p.Lower);
    }

    [Fact]
    public void AdaptiveThreshold_SettingUpperLowerThresholds_Works()
    {
        // arrange
        Color expectedUpper = Color.HotPink;
        Color expectedLower = Color.Yellow;

        // act
        this.operations.AdaptiveThreshold(expectedUpper, expectedLower);

        // assert
        AdaptiveThresholdProcessor p = this.Verify<AdaptiveThresholdProcessor>();
        Assert.Equal(expectedUpper, p.Upper);
        Assert.Equal(expectedLower, p.Lower);
    }

    [Fact]
    public void AdaptiveThreshold_SettingUpperLowerWithThresholdLimit_Works()
    {
        // arrange
        float expectedThresholdLimit = .77f;
        Color expectedUpper = Color.HotPink;
        Color expectedLower = Color.Yellow;

        // act
        this.operations.AdaptiveThreshold(expectedUpper, expectedLower, expectedThresholdLimit);

        // assert
        AdaptiveThresholdProcessor p = this.Verify<AdaptiveThresholdProcessor>();
        Assert.Equal(expectedThresholdLimit, p.ThresholdLimit);
        Assert.Equal(expectedUpper, p.Upper);
        Assert.Equal(expectedLower, p.Lower);
    }

    [Fact]
    public void AdaptiveThreshold_SettingUpperLowerWithThresholdLimit_WithRectangle_Works()
    {
        // arrange
        float expectedThresholdLimit = .77f;
        Color expectedUpper = Color.HotPink;
        Color expectedLower = Color.Yellow;

        // act
        this.operations.AdaptiveThreshold(expectedUpper, expectedLower, expectedThresholdLimit, this.rect);

        // assert
        AdaptiveThresholdProcessor p = this.Verify<AdaptiveThresholdProcessor>(this.rect);
        Assert.Equal(expectedThresholdLimit, p.ThresholdLimit);
        Assert.Equal(expectedUpper, p.Upper);
        Assert.Equal(expectedLower, p.Lower);
    }

    [Theory]
    [WithFile(TestImages.Png.Bradley01, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.Bradley02, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.Ducky, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.Issue2217, PixelTypes.Rgba32)]
    public void AdaptiveThreshold_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage())
        {
            image.Mutate(img => img.AdaptiveThreshold());
            image.DebugSave(provider);
            image.CompareToReferenceOutput(ImageComparer.Exact, provider);
        }
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(8, 8)]
    [InlineData(15, 15)]
    [InlineData(15, 3)]
    public void AdaptiveThreshold_NarrowImage_SeparatesDarkPixel(int width, int height)
    {
        using Image<Rgba32> image = new(width, height, new Rgba32(200, 200, 200));
        Point dark = new(width / 2, height / 2);
        image[dark.X, dark.Y] = new Rgba32(10, 10, 10);

        image.Mutate(img => img.AdaptiveThreshold());

        AssertOnlyDarkPixelIsLower(image, new Rectangle(0, 0, width, height), dark);
    }

    [Fact]
    public void AdaptiveThreshold_NarrowRectangle_SeparatesDarkPixel()
    {
        using Image<Rgba32> image = new(100, 100, new Rgba32(200, 200, 200));
        Rectangle rectangle = new(10, 10, 8, 50);
        Point dark = new(14, 30);
        image[dark.X, dark.Y] = new Rgba32(10, 10, 10);

        image.Mutate(img => img.AdaptiveThreshold(Color.White, Color.Black, rectangle));

        AssertOnlyDarkPixelIsLower(image, rectangle, dark);
    }

    [Theory]
    [WithFile(TestImages.Png.Bradley02, PixelTypes.Rgba32)]
    public void AdaptiveThreshold_WithRectangle_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage())
        {
            image.Mutate(img => img.AdaptiveThreshold(Color.White, Color.Black, new Rectangle(60, 90, 200, 30)));
            image.DebugSave(provider);
            image.CompareToReferenceOutput(ImageComparer.Exact, provider);
        }
    }

    /// <summary>
    /// Asserts that a thresholded region of uniform light pixels with a single dark pixel
    /// maps the dark pixel to black and every other pixel to white.
    /// </summary>
    /// <param name="image">The thresholded image.</param>
    /// <param name="region">The region that was thresholded.</param>
    /// <param name="dark">The location of the dark pixel.</param>
    private static void AssertOnlyDarkPixelIsLower(Image<Rgba32> image, Rectangle region, Point dark)
    {
        Rgba32 black = Color.Black.ToPixel<Rgba32>();
        Rgba32 white = Color.White.ToPixel<Rgba32>();

        // The last column and row are skipped. With the smallest cluster size their averaging windows hold
        // no pixels, so they always take the lower color, matching regions 16 to 31 pixels wide.
        for (int y = region.Top; y < region.Bottom - 1; y++)
        {
            for (int x = region.Left; x < region.Right - 1; x++)
            {
                Assert.Equal(x == dark.X && y == dark.Y ? black : white, image[x, y]);
            }
        }
    }
}
