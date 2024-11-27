// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
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

    [Fact]
    public void Encode_WithTransparentColorBehaviorClear_Works()
    {
        // arrange
        using Image<Rgba32> image = new(50, 50);
        CurEncoder encoder = new()
        {
            TransparentColorMode = TransparentColorMode.Clear,

        };
        Rgba32 rgba32 = Color.Blue.ToPixel<Rgba32>();
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < image.Height; y++)
            {
                Span<Rgba32> rowSpan = accessor.GetRowSpan(y);

                // Half of the test image should be transparent.
                if (y > 25)
                {
                    rgba32.A = 0;
                }

                for (int x = 0; x < image.Width; x++)
                {
                    rowSpan[x] = Rgba32.FromRgba32(rgba32);
                }
            }
        });

        // act
        using MemoryStream memStream = new();
        image.Save(memStream, encoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> actual = Image.Load<Rgba32>(memStream);
        Rgba32 expectedColor = Color.Blue.ToPixel<Rgba32>();

        actual.ProcessPixelRows(accessor =>
        {
            Rgba32 transparent = Color.Transparent.ToPixel<Rgba32>();
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<Rgba32> rowSpan = accessor.GetRowSpan(y);
                Span<Rgba32> rowSpanOpp = accessor.GetRowSpan(accessor.Height - y - 1);

                if (y > 25)
                {
                    expectedColor = transparent;
                }

                for (int x = 0; x < accessor.Width; x++)
                {
                    if (expectedColor != rowSpan[x])
                    {
                        var xx = 0;
                    }


                    Assert.Equal(expectedColor, rowSpan[x]);
                }
            }
        });
    }
}
