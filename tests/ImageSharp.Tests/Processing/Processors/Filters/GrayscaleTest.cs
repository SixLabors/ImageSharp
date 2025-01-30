// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Filters;

[Trait("Category", "Processors")]
[GroupOutput("Filters")]
public class GrayscaleTest
{
    public static readonly TheoryData<GrayscaleMode> GrayscaleModeTypes
        = new()
        {
            GrayscaleMode.Bt601,
            GrayscaleMode.Bt709
        };

    /// <summary>
    /// Use test patterns over loaded images to save decode time.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type of the image.</typeparam>
    [Theory]
    [WithTestPatternImages(nameof(GrayscaleModeTypes), 48, 48, PixelTypes.Rgba32)]
    public void ApplyGrayscaleFilter<TPixel>(TestImageProvider<TPixel> provider, GrayscaleMode value)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.RunValidatingProcessorTest(x => x.Grayscale(value), value);
    }
}
