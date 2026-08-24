// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;
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
        { BitsPerPixel = BmpBitsPerPixel.Bit24, InfoHeaderType = BmpInfoHeaderType.Os2Version2 };
        BmpMetadata clone = meta.DeepClone();

        clone.BitsPerPixel = BmpBitsPerPixel.Bit32;
        clone.InfoHeaderType = BmpInfoHeaderType.WinVersion2;

        Assert.False(meta.BitsPerPixel.Equals(clone.BitsPerPixel));
        Assert.False(meta.InfoHeaderType.Equals(clone.InfoHeaderType));
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
    [WithFile(TestImages.Bmp.IccProfile, PixelTypes.Rgba32)]
    public void Decoder_CanReadColorProfile<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(BmpDecoder.Instance);
        ImageSharp.Metadata.ImageMetadata metaData = image.Metadata;
        Assert.NotNull(metaData);
        Assert.NotNull(metaData.IccProfile);
        Assert.Equal(16, metaData.IccProfile.Entries.Length);
    }

    [Fact]
    public void Identify_MalformedIccProfile_IgnoresNonCriticalErrorsByDefault()
    {
        ImageInfo info = Image.Identify(CreateBmpWithMalformedIccProfile());
        Assert.Equal(1, info.Width);
        Assert.Equal(1, info.Height);
    }

    [Fact]
    public void Decode_MalformedIccProfile_IgnoresNonCriticalErrorsByDefault()
    {
        using Image<Rgba32> image = Image.Load<Rgba32>(CreateBmpWithMalformedIccProfile());
        Assert.Equal(1, image.Width);
        Assert.Equal(1, image.Height);
    }

    [Fact]
    public void Identify_MalformedIccProfile_ThrowsWithStrict()
    {
        DecoderOptions options = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidIccProfileException>(() => Image.Identify(options, CreateBmpWithMalformedIccProfile()));
    }

    [Fact]
    public void Decode_MalformedIccProfile_ThrowsWithStrict()
    {
        DecoderOptions options = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidIccProfileException>(() =>
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(options, CreateBmpWithMalformedIccProfile());
        });
    }

    private static byte[] CreateBmpWithMalformedIccProfile()
        => CorruptedMetadataImageFactory.CreateImageWithMalformedIccProfile(new BmpEncoder());
}
