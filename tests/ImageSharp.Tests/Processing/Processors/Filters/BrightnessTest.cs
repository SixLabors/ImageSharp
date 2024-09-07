// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Filters;

[Trait("Category", "Processors")]
[GroupOutput("Filters")]
public class BrightnessTest
{
    private readonly ImageComparer imageComparer = ImageComparer.Tolerant(0.007F);

    public static readonly TheoryData<float> BrightnessValues
    = new()
    {
        .5F,
        1.5F
    };

    [Theory]
    [WithTestPatternImages(nameof(BrightnessValues), 48, 48, PixelTypes.Rgba32)]
    public void ApplyBrightnessFilter<TPixel>(TestImageProvider<TPixel> provider, float value)
        where TPixel : unmanaged, IPixel<TPixel> => provider.RunValidatingProcessorTest(ctx => ctx.Brightness(value), value, this.imageComparer);
}
