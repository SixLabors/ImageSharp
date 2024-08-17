// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Cur;
using SixLabors.ImageSharp.Formats.Ico;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using static SixLabors.ImageSharp.Tests.TestImages.Cur;
using static SixLabors.ImageSharp.Tests.TestImages.Ico;

namespace SixLabors.ImageSharp.Tests.Formats.Icon.Cur;

[Trait("Format", "Cur")]
public class CurEncoderTests
{
    private static CurEncoder Encoder => new();

    [Theory]
    [WithFile(CurReal, PixelTypes.Rgba32)]
    [WithFile(WindowsMouse, PixelTypes.Rgba32)]
    public void CanRoundTripEncoder<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(CurDecoder.Instance);
        using MemoryStream memStream = new();
        image.DebugSaveMultiFrame(provider);

        image.Save(memStream, Encoder);
        memStream.Seek(0, SeekOrigin.Begin);

        using Image<TPixel> encoded = Image.Load<TPixel>(memStream);
        encoded.DebugSaveMultiFrame(provider, appendPixelTypeToFileName: false);

        encoded.CompareToOriginalMultiFrame(provider, ImageComparer.Exact, CurDecoder.Instance);
    }

    [Theory]
    [WithFile(Flutter, PixelTypes.Rgba32)]
    public void CanConvertFromIco<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(IcoDecoder.Instance);
        using MemoryStream memStream = new();

        image.Save(memStream, Encoder);
        memStream.Seek(0, SeekOrigin.Begin);

        using Image<TPixel> encoded = Image.Load<TPixel>(memStream);
        encoded.DebugSaveMultiFrame(provider);

        // Despite preservation of the palette. The process can still be lossy
        encoded.CompareToOriginalMultiFrame(provider, ImageComparer.TolerantPercentage(.23f), IcoDecoder.Instance);

        for (int i = 0; i < image.Frames.Count; i++)
        {
            IcoFrameMetadata icoFrame = image.Frames[i].Metadata.GetIcoMetadata();
            CurFrameMetadata curFrame = encoded.Frames[i].Metadata.GetCurMetadata();

            // Compression may differ as we cannot convert that.
            // Color table may differ.
            Assert.Equal(icoFrame.BmpBitsPerPixel, curFrame.BmpBitsPerPixel);
            Assert.Equal(icoFrame.EncodingWidth, curFrame.EncodingWidth);
            Assert.Equal(icoFrame.EncodingHeight, curFrame.EncodingHeight);
        }
    }
}
