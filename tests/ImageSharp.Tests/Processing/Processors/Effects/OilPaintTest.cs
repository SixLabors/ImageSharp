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
        {
            TestImages.Png.CalliphoraPartial,
            TestImages.Bmp.Car
        };

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

    [Fact]
    public void Issue2518_PixelComponentOutsideOfRange_ThrowsImageProcessingException()
    {
        using Image<RgbaVector> image = new(10, 10, new RgbaVector(1, 1, 100));
        Assert.Throws<ImageProcessingException>(() => image.Mutate(ctx => ctx.OilPaint()));
    }
}
