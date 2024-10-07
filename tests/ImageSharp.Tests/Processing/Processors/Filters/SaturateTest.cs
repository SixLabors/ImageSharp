// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Filters;

[Trait("Category", "Processors")]
[GroupOutput("Filters")]
public class SaturateTest
{
    public static readonly TheoryData<float> SaturationValues
    = new()
    {
        .5F,
        1.5F,
    };

    [Theory]
    [WithTestPatternImages(nameof(SaturationValues), 48, 48, PixelTypes.Rgba32)]
    public void ApplySaturationFilter<TPixel>(TestImageProvider<TPixel> provider, float value)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.RunValidatingProcessorTest(x => x.Saturate(value), value);
    }
}
