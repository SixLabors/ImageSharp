// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Filters;

[Trait("Category", "Processors")]
[GroupOutput("Filters")]
public class HueTest
{
    public static readonly TheoryData<int> HueValues
    = new()
    {
        180,
        -180
    };

    [Theory]
    [WithTestPatternImages(nameof(HueValues), 48, 48, PixelTypes.Rgba32)]
    public void ApplyHueFilter<TPixel>(TestImageProvider<TPixel> provider, int value)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.RunValidatingProcessorTest(x => x.Hue(value), value);
    }
}
