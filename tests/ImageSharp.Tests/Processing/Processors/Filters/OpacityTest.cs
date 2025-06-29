// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Filters;

[Trait("Category", "Processors")]
[GroupOutput("Filters")]
public class OpacityTest
{
    public static readonly TheoryData<float> AlphaValues
    = new()
    {
        20 / 100F,
        80 / 100F
    };

    [Theory]
    [WithTestPatternImages(nameof(AlphaValues), 48, 48, PixelTypes.Rgba32)]
    public void ApplyAlphaFilter<TPixel>(TestImageProvider<TPixel> provider, float value)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.RunValidatingProcessorTest(x => x.Opacity(value), value);
    }
}
