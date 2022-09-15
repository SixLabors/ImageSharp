// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Filters;

[Trait("Category", "Processors")]
[GroupOutput("Filters")]
public class BlackWhiteTest
{
    [Theory]
    [WithTestPatternImages(48, 48, PixelTypes.Rgba32)]
    public void ApplyBlackWhiteFilter<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.RunValidatingProcessorTest(ctx => ctx.BlackWhite(), comparer: ImageComparer.TolerantPercentage(0.002f));
    }
}
