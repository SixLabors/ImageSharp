// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestDataIcc;
using SixLabors.ImageSharp.Tests.TestUtilities;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Webp;

[Trait("Format", "Webp")]
public class WebpMetaDataTests
{
    public static IEnumerable<object[]> IccMetadataOptions()
    {
        foreach (SegmentIntegrityHandling integrity in new[] { SegmentIntegrityHandling.Strict, SegmentIntegrityHandling.IgnoreAncillary, SegmentIntegrityHandling.IgnoreImageData })
        {
            foreach (bool skipMetadata in new[] { false, true })
            {
                foreach (bool animated in new[] { false, true })
                {
                    yield return new object[] { integrity, skipMetadata, animated };
                }
            }
        }
    }

    public static IEnumerable<object[]> TruncatedMetadataOptions()
    {
        foreach (string chunkType in new[] { "EXIF", "XMP " })
        {
            foreach (uint length in new[] { 0x40000000U, 0xFFFFFFFEU, uint.MaxValue })
            {
                foreach (SegmentIntegrityHandling integrity in new[] { SegmentIntegrityHandling.Strict, SegmentIntegrityHandling.IgnoreAncillary, SegmentIntegrityHandling.IgnoreImageData })
                {
                    string fileName = $"{TestImages.Webp.ContainerDirectory}{chunkType.Trim().ToLowerInvariant()}-trailing-{length:x8}.webp";
                    yield return new object[] { fileName, integrity, false };
                    yield return new object[] { fileName, integrity, true };
                }
            }
        }
    }

    [Theory]
    [WithFile(TestImages.Webp.Lossy.BikeWithExif, PixelTypes.Rgba32, false)]
    [WithFile(TestImages.Webp.Lossy.BikeWithExif, PixelTypes.Rgba32, true)]
    public void IgnoreMetadata_ControlsWhetherExifIsParsed_WithLossyImage<TPixel>(TestImageProvider<TPixel> provider, bool ignoreMetadata)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new() { SkipMetadata = ignoreMetadata };
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance, options);
        if (ignoreMetadata)
        {
            Assert.Null(image.Metadata.ExifProfile);
        }
        else
        {
            ExifProfile exifProfile = image.Metadata.ExifProfile;
            Assert.NotNull(exifProfile);
            Assert.NotEmpty(exifProfile.Values);
            Assert.Contains(exifProfile.Values, m => m.Tag.Equals(ExifTag.Software) && m.GetValue().Equals("GIMP 2.10.2"));
        }
    }

    [Theory]
    [WithFile(TestImages.Webp.Lossless.WithExif, PixelTypes.Rgba32, false)]
    [WithFile(TestImages.Webp.Lossless.WithExif, PixelTypes.Rgba32, true)]
    public void IgnoreMetadata_ControlsWhetherExifIsParsed_WithLosslessImage<TPixel>(TestImageProvider<TPixel> provider, bool ignoreMetadata)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new() { SkipMetadata = ignoreMetadata };
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance, options);
        if (ignoreMetadata)
        {
            Assert.Null(image.Metadata.ExifProfile);
        }
        else
        {
            ExifProfile exifProfile = image.Metadata.ExifProfile;
            Assert.NotNull(exifProfile);
            Assert.NotEmpty(exifProfile.Values);
            Assert.Contains(exifProfile.Values, m => m.Tag.Equals(ExifTag.Make) && m.GetValue().Equals("Canon"));
            Assert.Contains(exifProfile.Values, m => m.Tag.Equals(ExifTag.Model) && m.GetValue().Equals("Canon PowerShot S40"));
            Assert.Contains(exifProfile.Values, m => m.Tag.Equals(ExifTag.Software) && m.GetValue().Equals("GIMP 2.10.2"));
        }
    }

    [Theory]
    [WithFile(TestImages.Webp.Lossy.WithIccp, PixelTypes.Rgba32, false)]
    [WithFile(TestImages.Webp.Lossy.WithIccp, PixelTypes.Rgba32, true)]
    [WithFile(TestImages.Webp.Lossless.WithIccp, PixelTypes.Rgba32, false)]
    [WithFile(TestImages.Webp.Lossless.WithIccp, PixelTypes.Rgba32, true)]
    public void IgnoreMetadata_ControlsWhetherIccpIsParsed<TPixel>(TestImageProvider<TPixel> provider, bool ignoreMetadata)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new() { SkipMetadata = ignoreMetadata };
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance, options);
        if (ignoreMetadata)
        {
            Assert.Null(image.Metadata.IccProfile);
        }
        else
        {
            Assert.NotNull(image.Metadata.IccProfile);
            Assert.NotEmpty(image.Metadata.IccProfile.Entries);
        }
    }

    [Theory]
    [WithFile(TestImages.Webp.Lossy.WithXmp, PixelTypes.Rgba32, false)]
    [WithFile(TestImages.Webp.Lossy.WithXmp, PixelTypes.Rgba32, true)]
    public async Task IgnoreMetadata_ControlsWhetherXmpIsParsed<TPixel>(TestImageProvider<TPixel> provider, bool ignoreMetadata)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new() { SkipMetadata = ignoreMetadata };
        using Image<TPixel> image = await provider.GetImageAsync(WebpDecoder.Instance, options);
        if (ignoreMetadata)
        {
            Assert.Null(image.Metadata.XmpProfile);
        }
        else
        {
            Assert.NotNull(image.Metadata.XmpProfile);
            Assert.NotEmpty(image.Metadata.XmpProfile.Data);
        }
    }

    [Theory]
    [InlineData(WebpFileFormatType.Lossy)]
    [InlineData(WebpFileFormatType.Lossless)]
    public void Encode_WritesExifWithPadding(WebpFileFormatType fileFormatType)
    {
        // arrange
        using Image<Rgba32> input = new(25, 25);
        using MemoryStream memoryStream = new();
        ExifProfile expectedExif = new();
        string expectedSoftware = "ImageSharp";
        expectedExif.SetValue(ExifTag.Software, expectedSoftware);
        input.Metadata.ExifProfile = expectedExif;

        // act
        input.Save(memoryStream, new WebpEncoder() { FileFormat = fileFormatType });
        memoryStream.Position = 0;

        // assert
        using Image<Rgba32> image = Image.Load<Rgba32>(memoryStream);
        ExifProfile actualExif = image.Metadata.ExifProfile;
        Assert.NotNull(actualExif);
        Assert.Equal(expectedExif.Values.Count, actualExif.Values.Count);
        Assert.Equal(expectedSoftware, actualExif.GetValue(ExifTag.Software).Value);
    }

    [Theory]
    [WithFile(TestImages.Webp.Lossy.BikeWithExif, PixelTypes.Rgba32)]
    public void EncodeLossyWebp_PreservesExif<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // arrange
        using Image<TPixel> input = provider.GetImage(WebpDecoder.Instance);
        using MemoryStream memoryStream = new();
        ExifProfile expectedExif = input.Metadata.ExifProfile;

        // act
        input.Save(memoryStream, new WebpEncoder() { FileFormat = WebpFileFormatType.Lossy });
        memoryStream.Position = 0;

        // assert
        using Image<Rgba32> image = Image.Load<Rgba32>(memoryStream);
        ExifProfile actualExif = image.Metadata.ExifProfile;
        Assert.NotNull(actualExif);
        Assert.Equal(expectedExif.Values.Count, actualExif.Values.Count);
    }

    [Theory]
    [WithFile(TestImages.Webp.Lossless.WithExif, PixelTypes.Rgba32)]
    public void EncodeLosslessWebp_PreservesExif<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // arrange
        using Image<TPixel> input = provider.GetImage(WebpDecoder.Instance);
        using MemoryStream memoryStream = new();
        ExifProfile expectedExif = input.Metadata.ExifProfile;

        // act
        input.Save(memoryStream, new WebpEncoder() { FileFormat = WebpFileFormatType.Lossless });
        memoryStream.Position = 0;

        // assert
        using Image<Rgba32> image = Image.Load<Rgba32>(memoryStream);
        ExifProfile actualExif = image.Metadata.ExifProfile;
        Assert.NotNull(actualExif);
        Assert.Equal(expectedExif.Values.Count, actualExif.Values.Count);
    }

    [Theory]
    [WithFile(TestImages.Webp.Lossy.WithIccp, PixelTypes.Rgba32, WebpFileFormatType.Lossless)]
    [WithFile(TestImages.Webp.Lossy.WithIccp, PixelTypes.Rgba32, WebpFileFormatType.Lossy)]
    public void Encode_PreservesColorProfile<TPixel>(TestImageProvider<TPixel> provider, WebpFileFormatType fileFormat)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> input = provider.GetImage(WebpDecoder.Instance);
        ImageSharp.Metadata.Profiles.Icc.IccProfile expectedProfile = input.Metadata.IccProfile;
        byte[] expectedProfileBytes = expectedProfile.ToByteArray();

        using MemoryStream memStream = new();
        input.Save(memStream, new WebpEncoder()
        {
            FileFormat = fileFormat
        });

        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        ImageSharp.Metadata.Profiles.Icc.IccProfile actualProfile = output.Metadata.IccProfile;
        byte[] actualProfileBytes = actualProfile.ToByteArray();

        Assert.NotNull(actualProfile);
        Assert.Equal(expectedProfileBytes, actualProfileBytes);
    }

    [Theory]
    [WithFile(TestImages.Webp.Lossy.WithExifNotEnoughData, PixelTypes.Rgba32)]
    public void WebpDecoder_IgnoresInvalidExifChunk<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Exception ex = Record.Exception(() =>
        {
            using Image<TPixel> image = provider.GetImage();
        });
        Assert.Null(ex);
    }

    [Fact]
    public void Identify_InvalidExifChunk_IgnoresNonCriticalErrorsByDefault()
    {
        using MemoryStream stream = new(TestFile.Create(TestImages.Webp.Lossy.WithExifNotEnoughData).Bytes, false);
        ImageInfo info = Image.Identify(stream);
        Assert.True(info.Width > 0);
        Assert.True(info.Height > 0);
    }

    [Fact]
    public void Identify_InvalidExifChunk_ThrowsWithStrict()
    {
        DecoderOptions options = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        using MemoryStream stream = new(TestFile.Create(TestImages.Webp.Lossy.WithExifNotEnoughData).Bytes, false);
        Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, stream));
    }

    [Fact]
    public void Decode_InvalidExifChunk_ThrowsWithStrict()
    {
        DecoderOptions options = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        using MemoryStream stream = new(TestFile.Create(TestImages.Webp.Lossy.WithExifNotEnoughData).Bytes, false);
        Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(options, stream);
        });
    }

    [Theory]
    [InlineData("iccp", 0x40000000U)]
    [InlineData("exif", 0x40000000U)]
    [InlineData("xmp", 0x40000000U)]
    [InlineData("iccp", 0xFFFFFFFEU)]
    [InlineData("exif", 0xFFFFFFFEU)]
    [InlineData("xmp", 0xFFFFFFFEU)]
    [InlineData("iccp", uint.MaxValue)]
    [InlineData("exif", uint.MaxValue)]
    [InlineData("xmp", uint.MaxValue)]
    public void Decode_WithOversizedMetadataChunk_ThrowsInvalidImageContentException(string chunkType, uint length)
    {
        // The stored RIFF size covers the file. The metadata declaration itself exceeds that boundary.
        byte[] payload = TestFile.Create($"{TestImages.Webp.ContainerDirectory}{chunkType}-oversized-{length:x8}.webp").Bytes;
        DecoderOptions options = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };

        Assert.Throws<InvalidImageContentException>(() => Image.Load(options, payload));
        Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, payload));
    }

    [Theory]
    [MemberData(nameof(TruncatedMetadataOptions))]
    public void Decode_TruncatedTrailingMetadata_RespectsOptions(string fileName, SegmentIntegrityHandling integrity, bool skipMetadata)
    {
        byte[] payload = TestFile.Create(fileName).Bytes;
        DecoderOptions options = new() { SegmentIntegrityHandling = integrity, SkipMetadata = skipMetadata };

        if (integrity is SegmentIntegrityHandling.Strict && !skipMetadata)
        {
            Assert.Throws<InvalidImageContentException>(() => Image.Load(options, payload));
            Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, payload));
        }
        else
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(options, payload);
            Assert.Equal(new Size(2, 2), image.Size);
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Assert.Equal(new Rgba32(17, 34, 51), image[x, y]);
                }
            }

            Assert.Null(image.Metadata.ExifProfile);
            Assert.Null(image.Metadata.XmpProfile);

            ImageInfo info = Image.Identify(options, payload);
            Assert.Equal(image.Size, info.Size);
            Assert.Null(info.Metadata.ExifProfile);
            Assert.Null(info.Metadata.XmpProfile);
        }
    }

    [Theory]
    [MemberData(nameof(IccMetadataOptions))]
    public void Decode_InvalidIccPayload_RespectsOptionsAndReadsFollowingImage(SegmentIntegrityHandling integrity, bool skipMetadata, bool animated)
    {
        byte[] payload = TestFile.Create(TestImages.Webp.ContainerDirectory + (animated ? "invalid-icc-animated.webp" : "invalid-icc.webp")).Bytes;
        DecoderOptions options = new() { SegmentIntegrityHandling = integrity, SkipMetadata = skipMetadata };

        if (integrity is SegmentIntegrityHandling.Strict && !skipMetadata)
        {
            Assert.Throws<InvalidIccProfileException>(() => Image.Load(options, payload));
            Assert.Throws<InvalidIccProfileException>(() => Image.Identify(options, payload));
        }
        else
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(options, payload);
            Assert.Equal(new Size(2, 2), image.Size);
            Assert.Equal(animated ? 2 : 1, image.Frames.Count);
            Assert.Equal(new Rgba32(17, 34, 51), image[0, 0]);
            Assert.Null(image.Metadata.IccProfile);

            ImageInfo info = Image.Identify(options, payload);
            Assert.Equal(image.Size, info.Size);
            Assert.Null(info.Metadata.IccProfile);
        }
    }

    [Theory]
    [MemberData(nameof(IccMetadataOptions))]
    public void Decode_TruncatedIccFraming_RemainsFatal(SegmentIntegrityHandling integrity, bool skipMetadata, bool animated)
    {
        byte[] payload = TestFile.Create(TestImages.Webp.ContainerDirectory + (animated ? "truncated-icc-animated.webp" : "truncated-icc.webp")).Bytes;
        DecoderOptions options = new() { SegmentIntegrityHandling = integrity, SkipMetadata = skipMetadata };

        Assert.Throws<InvalidImageContentException>(() => Image.Load(options, payload));
        Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, payload));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Decode_OddLengthMetadataChunks_ReadsPayloadsWithoutPaddingByte(bool skipMetadata, bool animated)
    {
        // Local libwebp extracts these exact odd-length payloads and decodes the same solid 2x2 image.
        byte[] payload = TestFile.Create(TestImages.Webp.ContainerDirectory + (animated ? "odd-metadata-animated.webp" : "odd-metadata.webp")).Bytes;
        byte[] icc = TestFile.Create(TestImages.Webp.ContainerDirectory + "odd.icc").Bytes;
        byte[] xmp = TestFile.Create(TestImages.Webp.ContainerDirectory + "odd.xmp").Bytes;

        DecoderOptions options = new() { SkipMetadata = skipMetadata };
        using Image<Rgba32> image = Image.Load<Rgba32>(options, payload);

        // ICCP precedes ANIM. Entering the frame decoder must retain that profile, not replace its metadata owner.
        Assert.Equal(animated ? 2 : 1, image.Frames.Count);
        Assert.Equal(new Rgba32(17, 34, 51), image[0, 0]);
        if (skipMetadata)
        {
            Assert.Null(image.Metadata.IccProfile);
            Assert.Null(image.Metadata.ExifProfile);
            Assert.Null(image.Metadata.XmpProfile);
        }
        else
        {
            Assert.Equal(icc, image.Metadata.IccProfile.ToByteArray());
            Assert.Equal("ImageSharp", image.Metadata.ExifProfile.GetValue(ExifTag.Software).Value);
            Assert.Equal(xmp, image.Metadata.XmpProfile.ToByteArray());
        }
    }

    /// <summary>
    /// Applies ancillary recovery only within RIFF and keeps incomplete ICC framing fatal.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Decode_MetadataOutsideRiff_RespectsIntegrity(bool skipMetadata)
    {
        // Complete metadata bytes exist in the physical file, but the RIFF declaration excludes part of them.
        byte[] exif = TestFile.Create(TestImages.Webp.ContainerDirectory + "exif-crosses-riff.webp").Bytes;
        byte[] icc = TestFile.Create(TestImages.Webp.ContainerDirectory + "icc-crosses-riff.webp").Bytes;
        foreach (SegmentIntegrityHandling integrity in Enum.GetValues<SegmentIntegrityHandling>())
        {
            DecoderOptions options = new() { SegmentIntegrityHandling = integrity, SkipMetadata = skipMetadata };
            Assert.Throws<InvalidImageContentException>(() => Image.Load(options, icc));
            Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, icc));

            if (integrity == SegmentIntegrityHandling.Strict && !skipMetadata)
            {
                Assert.Throws<InvalidImageContentException>(() => Image.Load(options, exif));
                Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, exif));
            }
            else
            {
                using Image<Rgba32> image = Image.Load<Rgba32>(options, exif);
                Assert.Equal(new Rgba32(17, 34, 51), image[0, 0]);
                Assert.Null(image.Metadata.ExifProfile);
                Assert.Null(Image.Identify(options, exif).Metadata.ExifProfile);
            }
        }
    }
}
