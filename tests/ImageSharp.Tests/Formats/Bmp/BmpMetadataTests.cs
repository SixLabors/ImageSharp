// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using static SixLabors.ImageSharp.Tests.TestImages.Bmp;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Bmp;

[Trait("Format", "Bmp")]
public class BmpMetadataTests
{
    [Fact]
    public void CloneIsDeep()
    {
        BmpMetadata meta = new()
        {
            BitsPerPixel = BmpBitsPerPixel.Bit24,
            ColorType = BmpColorType.Bgr565,
            InfoHeaderType = BmpInfoHeaderType.Os2Version2,
            Compression = BmpCompression.RunLength
        };

        BmpMetadata clone = meta.DeepClone();

        Assert.Equal(meta.ColorType, clone.ColorType);
        Assert.Equal(meta.Compression, clone.Compression);

        clone.BitsPerPixel = BmpBitsPerPixel.Bit32;
        clone.ColorType = BmpColorType.Bgr;
        clone.InfoHeaderType = BmpInfoHeaderType.WinVersion2;
        clone.Compression = BmpCompression.None;

        Assert.False(meta.BitsPerPixel.Equals(clone.BitsPerPixel));
        Assert.False(meta.ColorType.Equals(clone.ColorType));
        Assert.False(meta.InfoHeaderType.Equals(clone.InfoHeaderType));
        Assert.False(meta.Compression.Equals(clone.Compression));
    }

    [Theory]
    [InlineData(Bit8, BmpCompression.None)]
    [InlineData(Car, BmpCompression.None)]
    [InlineData(RLE8, BmpCompression.RunLength)]
    [InlineData(RLE4, BmpCompression.RunLength)]
    [InlineData(RLE24, BmpCompression.RunLength)]
    public void Identify_DetectsCompression(string imagePath, BmpCompression expectedCompression)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo imageInfo = Image.Identify(stream);

        Assert.Equal(expectedCompression, imageInfo.Metadata.GetBmpMetadata().Compression);
    }

    [Fact]
    public void FromFormatConnectingMetadata_SelectsColorType()
    {
        static BmpMetadata Create(PixelTypeInfo info) => BmpMetadata.FromFormatConnectingMetadata(new FormatConnectingMetadata { PixelTypeInfo = info });

        BmpMetadata bgr565 = Create(Bgr565.GetPixelTypeInfo());
        BmpMetadata bgra5551 = Create(Bgra5551.GetPixelTypeInfo());
        BmpMetadata bgr = Create(Rgb24.GetPixelTypeInfo());
        BmpMetadata bgra = Create(Rgba32.GetPixelTypeInfo());
        BmpMetadata palette = Create(L8.GetPixelTypeInfo());

        Assert.Equal((BmpBitsPerPixel.Bit16, BmpColorType.Bgr565), (bgr565.BitsPerPixel, bgr565.ColorType));
        Assert.Equal((BmpBitsPerPixel.Bit16, BmpColorType.Bgra5551), (bgra5551.BitsPerPixel, bgra5551.ColorType));
        Assert.Equal((BmpBitsPerPixel.Bit24, BmpColorType.Bgr), (bgr.BitsPerPixel, bgr.ColorType));
        Assert.Equal((BmpBitsPerPixel.Bit32, BmpColorType.Bgra), (bgra.BitsPerPixel, bgra.ColorType));
        Assert.Equal((BmpBitsPerPixel.Bit8, BmpColorType.Palette), (palette.BitsPerPixel, palette.ColorType));
    }

    [Theory]
    [InlineData(Bit8, BmpColorType.Palette)]
    [InlineData(Rgb16, BmpColorType.Bgr)]
    [InlineData(Rgb16bfdef, BmpColorType.Bgr)]
    [InlineData(Rgb16565, BmpColorType.Bgr565)]
    [InlineData(Rgb16565pal, BmpColorType.Bgr565)]
    [InlineData(Car, BmpColorType.Bgr)]
    [InlineData(Bit32Rgb, BmpColorType.Bgr)]
    [InlineData(Rgb32bf, BmpColorType.Bgr)]
    [InlineData(Bit32Rgba, BmpColorType.Bgra)]
    public void Identify_DetectsColorType(string imagePath, BmpColorType expectedColorType)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo imageInfo = Image.Identify(stream);
        BmpMetadata bitmapMetadata = imageInfo.Metadata.GetBmpMetadata();

        Assert.Equal(expectedColorType, bitmapMetadata.ColorType);
    }

    [Theory]
    [InlineData(BmpColorType.Palette, BmpBitsPerPixel.Bit8, PixelColorType.Indexed)]
    [InlineData(BmpColorType.Bgr, BmpBitsPerPixel.Bit16, PixelColorType.BGR)]
    [InlineData(BmpColorType.Bgr, BmpBitsPerPixel.Bit24, PixelColorType.BGR)]
    [InlineData(BmpColorType.Bgr, BmpBitsPerPixel.Bit32, PixelColorType.BGR)]
    [InlineData(BmpColorType.Bgra, BmpBitsPerPixel.Bit32, PixelColorType.BGR | PixelColorType.Alpha)]
    [InlineData(BmpColorType.Bgr565, BmpBitsPerPixel.Bit16, PixelColorType.BGR)]
    [InlineData(BmpColorType.Bgra5551, BmpBitsPerPixel.Bit16, PixelColorType.BGR | PixelColorType.Alpha)]
    public void GetPixelTypeInfo_ReportsStorageOrder(BmpColorType colorType, BmpBitsPerPixel bitsPerPixel, PixelColorType expected)
    {
        // A bitmap stores blue first, so each direct color type reports BGR.
        BmpMetadata metadata = new() { ColorType = colorType, BitsPerPixel = bitsPerPixel };

        Assert.Equal(expected, metadata.GetPixelTypeInfo().ColorType);
    }

    [Theory]
    [InlineData(WinBmpv2, BmpInfoHeaderType.WinVersion2)]
    [InlineData(WinBmpv3, BmpInfoHeaderType.WinVersion3)]
    [InlineData(WinBmpv4, BmpInfoHeaderType.WinVersion4)]
    [InlineData(WinBmpv5, BmpInfoHeaderType.WinVersion5)]
    [InlineData(Os2v2Short, BmpInfoHeaderType.Os2Version2Short)]
    [InlineData(Rgb32h52AdobeV3, BmpInfoHeaderType.AdobeVersion3)]
    [InlineData(Rgba32bf56AdobeV3, BmpInfoHeaderType.AdobeVersion3WithAlpha)]
    [InlineData(Os2v2, BmpInfoHeaderType.Os2Version2)]
    public void Identify_DetectsCorrectBitmapInfoHeaderType(string imagePath, BmpInfoHeaderType expectedInfoHeaderType)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo imageInfo = Image.Identify(stream);
        Assert.NotNull(imageInfo);
        BmpMetadata bitmapMetadata = imageInfo.Metadata.GetBmpMetadata();
        Assert.NotNull(bitmapMetadata);
        Assert.Equal(expectedInfoHeaderType, bitmapMetadata.InfoHeaderType);
    }

    [Theory]
    [WithFile(TestImages.Bmp.IccProfile, PixelTypes.Rgba32, false)]
    [WithFile(TestImages.Bmp.IccProfile, PixelTypes.Rgba32, true)]
    public void Decoder_CanReadColorProfile<TPixel>(TestImageProvider<TPixel> provider, bool skipMetadata)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new(Configuration.Default) { SkipMetadata = skipMetadata };
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance, options);
        ImageSharp.Metadata.ImageMetadata metaData = image.Metadata;

        Assert.NotNull(metaData);
        Assert.NotNull(metaData.IccProfile);
        Assert.Equal(16, metaData.IccProfile.Entries.Length);

        ImageInfo info = Image.Identify(options, TestFile.Create(provider.SourceFileOrDescription).Bytes);

        Assert.Equal(metaData.IccProfile.ToByteArray(), info.Metadata.IccProfile.ToByteArray());
    }

    /// <summary>
    /// Checks that Identify discards an invalid profile and retains the image dimensions by default.
    /// </summary>
    /// <param name="provider">The existing BMP with an embedded profile.</param>
    [Theory]
    [WithFile(TestImages.Bmp.IccProfile, PixelTypes.Rgba32)]
    public void Identify_MalformedIccProfile_IgnoresNonCriticalErrorsByDefault(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> expected = provider.GetImage();
        ImageInfo info = Image.Identify(CreateBmpWithMalformedIccProfile(provider));

        Assert.Equal(expected.Size, info.Size);
        Assert.Null(info.Metadata.IccProfile);
    }

    /// <summary>
    /// Checks that Decode discards an invalid profile and decodes the image by default.
    /// </summary>
    /// <param name="provider">The existing BMP with an embedded profile.</param>
    [Theory]
    [WithFile(TestImages.Bmp.IccProfile, PixelTypes.Rgba32)]
    public void Decode_MalformedIccProfile_IgnoresNonCriticalErrorsByDefault(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> expected = provider.GetImage();
        using Image<Rgba32> image = Image.Load<Rgba32>(CreateBmpWithMalformedIccProfile(provider));

        Assert.Equal(expected.Size, image.Size);
        Assert.Null(image.Metadata.IccProfile);
    }

    /// <summary>
    /// Checks that strict Identify rejects an invalid ICC profile.
    /// </summary>
    /// <param name="provider">The existing BMP with an embedded profile.</param>
    [Theory]
    [WithFile(TestImages.Bmp.IccProfile, PixelTypes.Rgba32)]
    public void Identify_MalformedIccProfile_ThrowsWithStrict(TestImageProvider<Rgba32> provider)
    {
        byte[] encoded = CreateBmpWithMalformedIccProfile(provider);
        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidIccProfileException>(() => Image.Identify(options, encoded));
    }

    /// <summary>
    /// Checks that strict Decode rejects an invalid ICC profile.
    /// </summary>
    /// <param name="provider">The existing BMP with an embedded profile.</param>
    [Theory]
    [WithFile(TestImages.Bmp.IccProfile, PixelTypes.Rgba32)]
    public void Decode_MalformedIccProfile_ThrowsWithStrict(TestImageProvider<Rgba32> provider)
    {
        byte[] encoded = CreateBmpWithMalformedIccProfile(provider);
        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidIccProfileException>(() =>
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(options, encoded);
        });
    }

    /// <summary>
    /// Checks that Identify and Decode do not read the profile data of a linked profile as ICC data.
    /// </summary>
    /// <param name="provider">The existing BMP with an embedded profile.</param>
    [Theory]
    [WithFile(TestImages.Bmp.IccProfile, PixelTypes.Rgba32)]
    public void DecodeAndIdentify_LinkedProfile_DoesNotReadIccProfile(TestImageProvider<Rgba32> provider)
    {
        byte[] encoded = TestFile.Create(provider.SourceFileOrDescription).Bytes.ToArray();
        Span<byte> infoHeaderData = encoded.AsSpan(BmpFileHeader.Size, BmpInfoHeader.SizeV5);
        BmpInfoHeader header = BmpInfoHeader.ParseV5(infoHeaderData);

        // A linked profile stores a null-terminated file name at the profile offset. Strict handling throws for any ICC
        // data that the decoder reads, so the file name must not be read as a profile.
        byte[] fileName = "profile.icc\0"u8.ToArray();
        fileName.CopyTo(encoded.AsSpan(BmpFileHeader.Size + header.ProfileData));
        header.CsType = BmpColorSpace.PROFILE_LINKED;
        header.ProfileSize = fileName.Length;
        header.WriteV5Header(infoHeaderData);

        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        using Image<Rgba32> expected = provider.GetImage();
        ImageInfo info = Image.Identify(options, encoded);
        using Image<Rgba32> image = Image.Load<Rgba32>(options, encoded);

        Assert.Equal(expected.Size, info.Size);
        Assert.Null(info.Metadata.IccProfile);
        Assert.Equal(expected.Size, image.Size);
        Assert.Null(image.Metadata.IccProfile);
    }

    /// <summary>
    /// Invalidates the ICC size in a copy of an existing BMP without changing its container or pixel data.
    /// </summary>
    /// <param name="provider">The existing BMP with an embedded profile.</param>
    /// <returns>The BMP bytes with an invalid ICC size.</returns>
    private static byte[] CreateBmpWithMalformedIccProfile(TestImageProvider<Rgba32> provider)
    {
        byte[] encoded = TestFile.Create(provider.SourceFileOrDescription).Bytes.ToArray();
        BmpInfoHeader header = BmpInfoHeader.ParseV5(encoded.AsSpan(BmpFileHeader.Size));

        // ICC starts with its big-endian declared size. A size of 127 cannot contain the required 128-byte header.
        BinaryPrimitives.WriteUInt32BigEndian(encoded.AsSpan(BmpFileHeader.Size + header.ProfileData), 127);
        return encoded;
    }
}
