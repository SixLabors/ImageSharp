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
    /// A uniform image isolates bin selection; the output must retain the source component even at nonfinite endpoints.
    /// </summary>
    /// <param name="component">The unbounded component.</param>
    [Theory]
    [InlineData(2F)]
    [InlineData(100F)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(float.NaN)]
    public void Issue2518_PixelComponentOutsideOfRange_UsesBoundedIntensityBin(float component)
    {
        using Image<RgbaVector> image = new(10, 10, new RgbaVector(1, 1, component));

        image.Mutate(ctx => ctx.OilPaint());

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                float actual = image[x, y].ToVector4().Z;
                if (float.IsNaN(component))
                {
                    Assert.True(float.IsNaN(actual));
                }
                else
                {
                    Assert.Equal(component, actual);
                }
            }
        }
    }

    /// <summary>
    /// Values above the intensity range share its last bin while their color samples remain unbounded.
    /// </summary>
    [Fact]
    public void OilPaint_HdrSamplesShareEndpointBinWithoutClampingColor()
    {
        using Image<RgbaVector> image = new(2, 2);
        image[0, 0] = new RgbaVector(2F, 2F, 2F);
        image[1, 0] = new RgbaVector(4F, 4F, 4F);
        image[0, 1] = new RgbaVector(2F, 2F, 2F);
        image[1, 1] = new RgbaVector(4F, 4F, 4F);

        image.Mutate(ctx => ctx.OilPaint(2, 2));

        Assert.Equal(new RgbaVector(3F, 3F, 3F), image[1, 1]);
    }
}
