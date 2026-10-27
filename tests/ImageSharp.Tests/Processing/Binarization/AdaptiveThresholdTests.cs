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

    /// <summary>
    /// Checks every pixel, including the last row and column, in a narrow image.
    /// </summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
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

    /// <summary>
    /// Checks a narrow processing rectangle that does not start at the image origin.
    /// </summary>
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

    /// <summary>
    /// Checks centered windows against direct sums, including single-pixel dimensions and rectangle borders.
    /// </summary>
    /// <param name="width">The processing rectangle width.</param>
    /// <param name="height">The processing rectangle height.</param>
    /// <param name="radius">The expected window radius.</param>
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1, 8, 1)]
    [InlineData(8, 1, 1)]
    [InlineData(15, 7, 1)]
    [InlineData(16, 9, 1)]
    [InlineData(31, 11, 1)]
    [InlineData(32, 17, 2)]
    public void AdaptiveThreshold_Rectangle_MatchesLocalMean(int width, int height, int radius)
    {
        Rectangle rectangle = new(3, 4, width, height);
        using Image<L8> source = new(width + 6, height + 8);
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                // An asymmetric pattern detects windows shifted away from the current pixel.
                source[x, y] = new L8((byte)((17 + (37 * x) + (61 * y)) % 256));
            }
        }

        const float thresholdLimit = .85F;
        using Image<L8> actual = source.Clone(context => context.AdaptiveThreshold(Color.White, Color.Black, thresholdLimit, rectangle));

        AssertMatchesLocalMean(source, actual, rectangle, radius, thresholdLimit);
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

        for (int y = region.Top; y < region.Bottom; y++)
        {
            for (int x = region.Left; x < region.Right; x++)
            {
                Assert.Equal(x == dark.X && y == dark.Y ? black : white, image[x, y]);
            }
        }
    }

    /// <summary>
    /// Checks the local mean without using an integral image, and checks that pixels outside the rectangle are unchanged.
    /// </summary>
    /// <param name="source">The unchanged grayscale input.</param>
    /// <param name="actual">The processed image.</param>
    /// <param name="rectangle">The processing rectangle.</param>
    /// <param name="radius">The expected window radius.</param>
    /// <param name="thresholdLimit">The fraction of the local mean used as the threshold.</param>
    private static void AssertMatchesLocalMean(Image<L8> source, Image<L8> actual, Rectangle rectangle, int radius, float thresholdLimit)
    {
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                L8 expected = source[x, y];
                if (rectangle.Contains(x, y))
                {
                    int sum = 0;
                    int count = 0;
                    for (int sampleY = Math.Max(y - radius, rectangle.Top); sampleY <= Math.Min(y + radius, rectangle.Bottom - 1); sampleY++)
                    {
                        for (int sampleX = Math.Max(x - radius, rectangle.Left); sampleX <= Math.Min(x + radius, rectangle.Right - 1); sampleX++)
                        {
                            sum += source[sampleX, sampleY].PackedValue;
                            count++;
                        }
                    }

                    // Include both window endpoints. A one-pixel window therefore has count = 1.
                    expected = new L8(source[x, y].PackedValue * count <= sum * thresholdLimit ? (byte)0 : (byte)255);
                }

                Assert.Equal(expected, actual[x, y]);
            }
        }
    }
}
