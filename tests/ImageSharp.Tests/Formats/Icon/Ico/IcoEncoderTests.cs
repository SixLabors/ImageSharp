// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Ico;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using static SixLabors.ImageSharp.Tests.TestImages.Ico;

namespace SixLabors.ImageSharp.Tests.Formats.Icon.Ico;

[Trait("Format", "Icon")]
public class IcoEncoderTests
{
    private static IcoEncoder Encoder => new();

    [Theory]
    [WithFile(Flutter, PixelTypes.Rgba32)]
    public void CanRoundTripEncoder<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(IcoDecoder.Instance);
        using MemoryStream memStream = new();
        image.DebugSaveMultiFrame(provider);

        image.Save(memStream, Encoder);
        memStream.Seek(0, SeekOrigin.Begin);

        using Image<TPixel> encoded = Image.Load<TPixel>(memStream);
        encoded.DebugSaveMultiFrame(provider, appendPixelTypeToFileName: false);

        // Despite preservation of the palette. The process can still be lossy
        encoded.CompareToOriginalMultiFrame(provider, ImageComparer.TolerantPercentage(.23f), IcoDecoder.Instance);
    }
}
