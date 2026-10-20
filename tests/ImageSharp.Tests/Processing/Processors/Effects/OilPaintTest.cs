// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Effects;

[Trait("Category", "Processors")]
[GroupOutput("Effects")]
public class OilPaintTest
{
    public static readonly TheoryData<int, int> OilPaintValues = new()
    {
                                                                         { 15, 10 },
                                                                         { 6, 5 }
                                                                     };

    public static readonly string[] InputImages =
    [
        TestImages.Png.CalliphoraPartial,
            TestImages.Bmp.Car
    ];

    [Theory]
    [WithFileCollection(nameof(InputImages), nameof(OilPaintValues), PixelTypes.Rgba32)]
    public void FullImage<TPixel>(TestImageProvider<TPixel> provider, int levels, int brushSize)
        where TPixel : unmanaged, IPixel<TPixel>
        => provider.RunValidatingProcessorTest(
            x =>
            {
                x.OilPaint(levels, brushSize);
                return $"{levels}-{brushSize}";
            },
            ImageComparer.TolerantPercentage(0.01F),
            appendPixelTypeToFileName: false);

    [Theory]
    [WithFileCollection(nameof(InputImages), nameof(OilPaintValues), PixelTypes.Rgba32)]
    [WithTestPatternImages(nameof(OilPaintValues), 100, 100, PixelTypes.Rgba32)]
    public void InBox<TPixel>(TestImageProvider<TPixel> provider, int levels, int brushSize)
        where TPixel : unmanaged, IPixel<TPixel>
        => provider.RunRectangleConstrainedValidatingProcessorTest(
            (x, rect) => x.OilPaint(levels, brushSize, rect),
            $"{levels}-{brushSize}",
            ImageComparer.TolerantPercentage(0.01F));

    /// <summary>
    /// A uniform image retains finite HDR components. Nonfinite color input uses the pixel format's finite endpoints.
    /// </summary>
    /// <param name="component">The unbounded component.</param>
    /// <param name="expected">The finite color-conversion result required by RgbaVector.</param>
    [Theory]
    [InlineData(2F, 2F)]
    [InlineData(100F, 100F)]
    [InlineData(float.PositiveInfinity, float.MaxValue)]
    [InlineData(float.NegativeInfinity, float.MinValue)]
    [InlineData(float.NaN, float.MinValue)]
    public void Issue2518_PixelComponentOutsideOfRange_UsesBoundedIntensityBin(float component, float expected)
    {
        using Image<RgbaVector> image = new(10, 10, new RgbaVector(1, 1, component));

        image.Mutate(ctx => ctx.OilPaint());

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                float actual = image[x, y].ToVector4().Z;
                Assert.Equal(expected, actual);
            }
        }
    }

    /// <summary>
    /// Distinct HDR magnitudes occupy separate bins; an equal-count tie retains the first selected bin.
    /// </summary>
    [Fact]
    public void OilPaint_HdrSamplesUseDistinctMagnitudeBins()
    {
        using Image<RgbaVector> image = new(2, 2);
        image[0, 0] = new RgbaVector(2F, 2F, 2F);
        image[1, 0] = new RgbaVector(4F, 4F, 4F);
        image[0, 1] = new RgbaVector(2F, 2F, 2F);
        image[1, 1] = new RgbaVector(4F, 4F, 4F);

        image.Mutate(ctx => ctx.OilPaint(2, 2));

        // The neighborhood contains two samples of each value. The 2.0 bin reaches each
        // new maximum first, so the existing strict-greater tie rule selects its own mean.
        Assert.Equal(new RgbaVector(2F, 2F, 2F), image[1, 1]);
    }
}
