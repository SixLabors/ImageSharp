// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Cur;
using SixLabors.ImageSharp.Formats.Ico;
using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using static SixLabors.ImageSharp.Tests.TestImages.Cur;
using static SixLabors.ImageSharp.Tests.TestImages.Ico;

namespace SixLabors.ImageSharp.Tests.Formats.Icon.Ico;

[Trait("Format", "Icon")]
public partial class IcoEncoderTests
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
        encoded.DebugSaveMultiFrame(provider);

        // Despite preservation of the palette. The process can still be lossy
        encoded.CompareToOriginalMultiFrame(provider, ImageComparer.TolerantPercentage(.23f), IcoDecoder.Instance);
    }

    [Theory]
    [WithFile(WindowsMouse, PixelTypes.Rgba32)]
    public void CanConvertFromCur<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(CurDecoder.Instance);
        using MemoryStream memStream = new();

        image.Save(memStream, Encoder);
        memStream.Seek(0, SeekOrigin.Begin);

        using Image<TPixel> encoded = Image.Load<TPixel>(memStream);
        encoded.DebugSaveMultiFrame(provider);

        encoded.CompareToOriginalMultiFrame(provider, ImageComparer.Exact, CurDecoder.Instance);

        for (int i = 0; i < image.Frames.Count; i++)
        {
            CurFrameMetadata curFrame = image.Frames[i].Metadata.GetCurMetadata();
            IcoFrameMetadata icoFrame = encoded.Frames[i].Metadata.GetIcoMetadata();

            // Compression may differ as we cannot convert that.
            Assert.Equal(curFrame.BmpBitsPerPixel, icoFrame.BmpBitsPerPixel);
            Assert.Equal(curFrame.EncodingWidth, icoFrame.EncodingWidth);
            Assert.Equal(curFrame.EncodingHeight, icoFrame.EncodingHeight);
            Assert.Equal(curFrame.ColorTable, icoFrame.ColorTable);
        }
    }

    [Fact]
    public void Encode_WithTransparentColorBehaviorClear_Works()
    {
        // arrange
        using Image<Rgba32> image = new(50, 50);
        IcoEncoder encoder = new()
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

                if (y > 25)
                {
                    expectedColor = transparent;
                }

                for (int x = 0; x < accessor.Width; x++)
                {
                    Assert.Equal(expectedColor, rowSpan[x]);
                }
            }
        });
    }

    [Fact]
    public void PngEntry_PreservesExifProfile()
    {
        using Image<Rgba32> image = new(16, 16);
        ExifProfile exifProfile = new();
        exifProfile.SetValue(ExifTag.Software, "ImageSharp");
        image.Metadata.ExifProfile = exifProfile;
        image.Frames.RootFrame.Metadata.GetIcoMetadata().Compression = IconFrameCompression.Png;

        using MemoryStream stream = new();
        image.Save(stream, Encoder);

        stream.Position = 0;
        using Image<Rgba32> decoded = Image.Load<Rgba32>(stream);

        Assert.NotNull(decoded.Metadata.ExifProfile);
        Assert.Equal(image.Metadata.ExifProfile.Values, decoded.Metadata.ExifProfile.Values);
    }

    /// <summary>
    /// Verifies that the final partial AND-mask byte contains every pixel when the bitmap width is not byte-aligned.
    /// </summary>
    /// <param name="width">The bitmap width to encode.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(15)]
    public void BmpEntry_WritesPartialAlphaMaskByte(int width)
    {
        using Image<Rgba32> image = new(width, 1, Color.Red.ToPixel<Rgba32>());
        image[width - 1, 0] = Color.Transparent.ToPixel<Rgba32>();

        IcoFrameMetadata metadata = image.Frames.RootFrame.Metadata.GetIcoMetadata();
        metadata.Compression = IconFrameCompression.Bmp;
        metadata.BmpBitsPerPixel = BmpBitsPerPixel.Bit32;

        using MemoryStream stream = new();
        image.Save(stream, Encoder);

        // These widths produce one DWORD-aligned mask row at the end of the bitmap resource.
        ReadOnlySpan<byte> mask = stream.GetBuffer().AsSpan(checked((int)stream.Length) - sizeof(uint), sizeof(uint));
        int pixelIndex = width - 1;
        int byteIndex = pixelIndex / 8;
        int bitIndex = pixelIndex % 8;

        Assert.Equal((byte)(0b10000000 >> bitIndex), mask[byteIndex]);
    }

    [Fact]
    public void Encode_PaletteFromMetadata_KeepsExactColors()
    {
        using Image<Rgba32> image = NearColorPalette.CreateImage();
        IcoFrameMetadata metadata = image.Frames.RootFrame.Metadata.GetIcoMetadata();
        metadata.Compression = IconFrameCompression.Bmp;
        metadata.BmpBitsPerPixel = BmpBitsPerPixel.Bit8;
        metadata.ColorTable = NearColorPalette.Palette;

        using MemoryStream stream = new();
        image.Save(stream, new IcoEncoder());
        stream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(stream);

        ImageComparer.Exact.VerifySimilarity(image, output);
    }

    [Fact]
    public void Encode_DecodedBgr565Bitmap_WritesBgr()
    {
        // The icon format permits only BI_RGB. A decoded 5-6-5 entry keeps that color type in the metadata, so the
        // encoder must set the color type of the bitmap and write 5-5-5 without masks.
        using Image<Rgba32> source = IcoDecoderTests.CreateGradient();
        using Image<Rgba32> decoded = Image.Load<Rgba32>(IcoDecoderTests.CreateIconWithRgb565Bitmap(1, source));
        Assert.Equal(BmpColorType.Bgr565, decoded.Frames.RootFrame.Metadata.GetIcoMetadata().BmpColorType);

        using MemoryStream stream = new();
        decoded.Save(stream, new IcoEncoder());
        stream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(stream);
        IcoFrameMetadata metadata = output.Frames.RootFrame.Metadata.GetIcoMetadata();

        Assert.Equal(BmpBitsPerPixel.Bit16, metadata.BmpBitsPerPixel);
        Assert.Equal(BmpColorType.Bgr, metadata.BmpColorType);
    }

    [Fact]
    public void Encode_BmpMetadataRunLength_WritesUncompressedEntry()
    {
        // The bitmap of an icon image must set biCompression to 0 (BI_RGB), so the BMP metadata cannot select RLE.
        using Image<Rgba32> image = NearColorPalette.CreateImage();
        image.Metadata.GetBmpMetadata().Compression = BmpCompression.RunLength;

        IcoFrameMetadata metadata = image.Frames.RootFrame.Metadata.GetIcoMetadata();
        metadata.Compression = IconFrameCompression.Bmp;
        metadata.BmpBitsPerPixel = BmpBitsPerPixel.Bit8;
        metadata.ColorTable = NearColorPalette.Palette;

        using MemoryStream stream = new();
        image.Save(stream, new IcoEncoder());

        // The directory entry ends with the offset of the image data. The compression field starts at byte 16 of the
        // info header.
        ReadOnlySpan<byte> encoded = stream.GetBuffer().AsSpan(0, (int)stream.Length);
        int dibOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(encoded[(IconDir.Size + IconDirEntry.Size - sizeof(uint))..]));
        Assert.Equal((uint)BmpInfoCompression.RGB, BinaryPrimitives.ReadUInt32LittleEndian(encoded[(dibOffset + 16)..]));
    }
}
