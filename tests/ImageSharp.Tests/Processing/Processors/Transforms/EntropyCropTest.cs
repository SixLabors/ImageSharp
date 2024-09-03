// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms;

[Trait("Category", "Processors")]
[GroupOutput("Transforms")]
public class EntropyCropTest
{
    public static readonly TheoryData<float> EntropyCropValues = new TheoryData<float> { .25F, .75F };

    public static readonly string[] InputImages =
        {
            TestImages.Png.Ducky,
            TestImages.Jpeg.Baseline.Jpeg400,
            TestImages.Jpeg.Baseline.MultiScanBaselineCMYK
        };

    [Theory]
    [WithFileCollection(nameof(InputImages), nameof(EntropyCropValues), PixelTypes.Rgba32)]
    public void EntropyCrop<TPixel>(TestImageProvider<TPixel> provider, float value)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.RunValidatingProcessorTest(x => x.EntropyCrop(value), value, appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithBlankImages(40, 30, PixelTypes.Rgba32)]
    [WithBlankImages(30, 40, PixelTypes.Rgba32)]
    public void Entropy_WillNotCropWhiteImage<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // arrange
        using Image<TPixel> image = provider.GetImage();
        int expectedHeight = image.Height;
        int expectedWidth = image.Width;

        // act
        image.Mutate(img => img.EntropyCrop());

        // assert
        Assert.Equal(image.Width, expectedWidth);
        Assert.Equal(image.Height, expectedHeight);
    }
}
