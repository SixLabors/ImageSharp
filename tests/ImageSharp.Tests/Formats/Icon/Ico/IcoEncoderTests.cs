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

    /// <summary>
    /// A 16-bit or 24-bit icon bitmap has no alpha. The AND mask after the color rows marks the transparent pixels, for
    /// each color depth. See "Icons", section "The ICO File":
    /// https://learn.microsoft.com/en-us/previous-versions/ms997538(v=msdn.10)
    /// The decoder reads the mask, so the transparent pixels stay transparent.
    /// </summary>
    /// <param name="bitsPerPixel">The bit depth of the bitmap.</param>
    [Theory]
    [InlineData(BmpBitsPerPixel.Bit16)]
    [InlineData(BmpBitsPerPixel.Bit24)]
    public void Encode_DirectColorBitmap_KeepsMaskTransparency(BmpBitsPerPixel bitsPerPixel)
    {
        // Each channel is 0 or 255, so the 5-bit channels of the 16-bit layout keep each value. The encoder writes the
        // color of a transparent pixel, and the decoder keeps it with alpha 0.
        Rgba32 red = new(255, 0, 0, 255);
        Rgba32 transparentBlue = new(0, 0, 255, 0);
        Rgba32[] pixels = [red, transparentBlue, transparentBlue, red];
        using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(pixels, 2, 2);
        IcoFrameMetadata metadata = image.Frames.RootFrame.Metadata.GetIcoMetadata();
        metadata.Compression = IconFrameCompression.Bmp;
        metadata.BmpBitsPerPixel = bitsPerPixel;

        using MemoryStream stream = new();
        image.Save(stream, new IcoEncoder());
        stream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(stream);

        Assert.Equal(bitsPerPixel, output.Frames.RootFrame.Metadata.GetIcoMetadata().BmpBitsPerPixel);
        ImageComparer.Exact.VerifySimilarity(image, output);
    }

    /// <summary>
    /// A palette from the metadata keeps the exact color of each pixel, also for two colors in one coarse cache bucket.
    /// </summary>
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

    /// <summary>
    /// A decoded 5-6-5 icon bitmap encodes and decodes to the same pixels.
    /// </summary>
    [Fact]
    public void Encode_DecodedBgr565Bitmap_KeepsEachBit()
    {
        // The icon format permits only BI_RGB, which has no 5-6-5 layout. The 24-bit BI_RGB layout holds each bit of a
        // decoded 5-6-5 entry, so the re-encoded icon decodes to the same pixels.
        using Image<Rgba32> source = IcoDecoderTests.CreateGradient();
        using Image<Rgba32> decoded = Image.Load<Rgba32>(IcoDecoderTests.CreateIconWithRgb565Bitmap(1, source));

        using MemoryStream stream = new();
        decoded.Save(stream, new IcoEncoder());
        stream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(stream);
        IcoFrameMetadata metadata = output.Frames.RootFrame.Metadata.GetIcoMetadata();

        Assert.Equal(BmpBitsPerPixel.Bit24, metadata.BmpBitsPerPixel);
        Assert.Equal(BmpColorType.Bgr, metadata.BmpColorType);
        ImageComparer.Exact.VerifySimilarity(decoded, output);
    }

    /// <summary>
    /// The icon encoder writes BI_RGB bitmaps, so a 16-bit entry is 5-5-5 for each color type. The metadata reports the
    /// layout that the encoder writes.
    /// </summary>
    [Fact]
    public void Encode_16BitBgr565Metadata_ReportsWrittenLayout()
    {
        using Image<Rgba32> image = IcoDecoderTests.CreateGradient();
        IcoFrameMetadata metadata = image.Frames.RootFrame.Metadata.GetIcoMetadata();
        metadata.Compression = IconFrameCompression.Bmp;
        metadata.BmpBitsPerPixel = BmpBitsPerPixel.Bit16;
        metadata.BmpColorType = BmpColorType.Bgr565;

        using MemoryStream stream = new();
        image.Save(stream, new IcoEncoder());
        stream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(stream);
        IcoFrameMetadata written = output.Frames.RootFrame.Metadata.GetIcoMetadata();

        // The green channel is the second component: 5 bits in the 5-5-5 layout, 6 bits in the 5-6-5 layout. A 16-bit
        // BI_RGB bitmap has the 5-5-5 layout, so the metadata before and after the encode reports 5 bits.
        Assert.Equal(5, metadata.ToFormatConnectingFrameMetadata().PixelTypeInfo!.Value.ComponentInfo!.Value.GetComponentPrecision(1));
        Assert.Equal(5, written.ToFormatConnectingFrameMetadata().PixelTypeInfo!.Value.ComponentInfo!.Value.GetComponentPrecision(1));
    }

    /// <summary>
    /// An icon bitmap is uncompressed, also when the BMP metadata of the image selects RLE.
    /// </summary>
    [Fact]
    public void Encode_BmpMetadataRunLength_WritesUncompressedEntry()
    {
        // ICONIMAGE uses only six BITMAPINFOHEADER members, and "All other members must be 0", so biCompression is 0
        // (BI_RGB), and the BMP metadata cannot select RLE: https://learn.microsoft.com/en-us/previous-versions/ms997538(v=msdn.10)
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
        Assert.Equal((uint)BmpInfoCompression.Rgb, BinaryPrimitives.ReadUInt32LittleEndian(encoded[(dibOffset + 16)..]));
    }
}
