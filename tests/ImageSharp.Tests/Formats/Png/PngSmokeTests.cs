// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats.Png;

[Trait("Format", "Png")]
public class PngSmokeTests
{
    [Theory]
    [WithTestPatternImages(300, 300, PixelTypes.Rgba32)]
    public void GeneralTest<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        using MemoryStream ms = new();

        image.Save(ms, new PngEncoder());
        ms.Position = 0;
        using Image<Rgba32> img2 = PngDecoder.Instance.Decode<Rgba32>(DecoderOptions.Default, ms);
        ImageComparer.Exact.VerifySimilarity(image, img2);
    }

    [Theory]
    [WithTestPatternImages(300, 300, PixelTypes.Rgba32)]
    public void Resize<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // does saving a file then reopening mean both files are identical???
        using Image<TPixel> image = provider.GetImage();
        using MemoryStream ms = new();

        image.Mutate(x => x.Resize(100, 100));

        image.Save(ms, new PngEncoder());
        ms.Position = 0;
        using Image<Rgba32> img2 = PngDecoder.Instance.Decode<Rgba32>(DecoderOptions.Default, ms);
        ImageComparer.Tolerant().VerifySimilarity(image, img2);
    }
}
