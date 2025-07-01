// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Effects;

[Trait("Category", "Processors")]
[GroupOutput("Effects")]
public class PixelateTest
{
    public static readonly TheoryData<int> PixelateValues = new() { 4, 8 };

    [Theory]
    [WithFile(TestImages.Png.Ducky, nameof(PixelateValues), PixelTypes.Rgba32)]
    public void FullImage<TPixel>(TestImageProvider<TPixel> provider, int value)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.RunValidatingProcessorTest(x => x.Pixelate(value), value, appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithTestPatternImages(nameof(PixelateValues), 320, 240, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.CalliphoraPartial, nameof(PixelateValues), PixelTypes.Rgba32)]
    public void InBox<TPixel>(TestImageProvider<TPixel> provider, int value)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.RunRectangleConstrainedValidatingProcessorTest((x, rect) => x.Pixelate(value, rect), value);
    }
}
