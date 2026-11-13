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
        { BitsPerPixel = BmpBitsPerPixel.Bit24, Bit16Layout = BmpBit16Layout.Rgb565, InfoHeaderType = BmpInfoHeaderType.Os2Version2 };
        BmpMetadata clone = meta.DeepClone();

        Assert.Equal(meta.Bit16Layout, clone.Bit16Layout);

        clone.BitsPerPixel = BmpBitsPerPixel.Bit32;
        clone.Bit16Layout = BmpBit16Layout.Rgb555;
        clone.InfoHeaderType = BmpInfoHeaderType.WinVersion2;

        Assert.False(meta.BitsPerPixel.Equals(clone.BitsPerPixel));
        Assert.False(meta.Bit16Layout.Equals(clone.Bit16Layout));
        Assert.False(meta.InfoHeaderType.Equals(clone.InfoHeaderType));
    }

    [Fact]
    public void FromFormatConnectingMetadata_SelectsBit16LayoutFromGreenPrecision()
    {
        BmpMetadata rgb565 = BmpMetadata.FromFormatConnectingMetadata(new FormatConnectingMetadata { PixelTypeInfo = Bgr565.GetPixelTypeInfo() });
        BmpMetadata rgb555 = BmpMetadata.FromFormatConnectingMetadata(new FormatConnectingMetadata { PixelTypeInfo = Bgra5551.GetPixelTypeInfo() });

        Assert.Equal(BmpBitsPerPixel.Bit16, rgb565.BitsPerPixel);
        Assert.Equal(BmpBit16Layout.Rgb565, rgb565.Bit16Layout);
        Assert.Equal(BmpBitsPerPixel.Bit16, rgb555.BitsPerPixel);
        Assert.Equal(BmpBit16Layout.Rgb555, rgb555.Bit16Layout);
    }

    [Theory]
    [InlineData(Rgb16, BmpBit16Layout.Rgb555)]
    [InlineData(Rgb16bfdef, BmpBit16Layout.Rgb555)]
    [InlineData(Rgb16565, BmpBit16Layout.Rgb565)]
    [InlineData(Rgb16565pal, BmpBit16Layout.Rgb565)]
    public void Identify_DetectsBit16Layout(string imagePath, BmpBit16Layout expectedLayout)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo imageInfo = Image.Identify(stream);
        BmpMetadata bitmapMetadata = imageInfo.Metadata.GetBmpMetadata();

        Assert.Equal(expectedLayout, bitmapMetadata.Bit16Layout);
        Assert.Equal(
            expectedLayout is BmpBit16Layout.Rgb565 ? 6 : 5,
            imageInfo.PixelType.ComponentInfo!.Value.GetComponentPrecision(1));
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
