// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg;

[Trait("Format", "Jpg")]
public partial class JpegDecoderTests
{
    // TODO: A JPEGsnoop & metadata expert should review if the Exif/Icc expectations are correct.
    // I'm seeing several entries with Exif-related names in images where we do not decode an exif profile. (- Anton)
    public static readonly TheoryData<bool, string, int, bool, bool> MetadataTestData =
    new()
    {
        { false, TestImages.Jpeg.Progressive.Progress, 24, false, false },
        { false, TestImages.Jpeg.Progressive.Fb, 24, false, true },
        { false, TestImages.Jpeg.Baseline.Cmyk, 32, false, true },
        { false, TestImages.Jpeg.Baseline.Ycck, 32, true, true },
        { false, TestImages.Jpeg.Baseline.Jpeg400, 8, false, false },
        { false, TestImages.Jpeg.Baseline.Snake, 24, true, true },
        { false, TestImages.Jpeg.Baseline.Jpeg420Exif, 24, true, false },
        { true, TestImages.Jpeg.Progressive.Progress, 24, false, false },
        { true, TestImages.Jpeg.Progressive.Fb, 24, false, true },
        { true, TestImages.Jpeg.Baseline.Cmyk, 32, false, true },
        { true, TestImages.Jpeg.Baseline.Ycck, 32, true, true },
        { true, TestImages.Jpeg.Baseline.Jpeg400, 8, false, false },
        { true, TestImages.Jpeg.Baseline.Snake, 24, true, true },
        { true, TestImages.Jpeg.Baseline.Jpeg420Exif, 24, true, false },
        { true, TestImages.Jpeg.Issues.IdentifyMultiFrame1211, 24, true, true },
    };

    public static readonly TheoryData<string, int, int, PixelResolutionUnit> RatioFiles =
    new()
    {
        { TestImages.Jpeg.Baseline.Ratio1x1, 1, 1, PixelResolutionUnit.AspectRatio },
        { TestImages.Jpeg.Baseline.Snake, 300, 300, PixelResolutionUnit.PixelsPerInch },
        { TestImages.Jpeg.Baseline.GammaDalaiLamaGray, 72, 72, PixelResolutionUnit.PixelsPerInch },
        { TestImages.Jpeg.Issues.MultipleApp01932, 400, 400, PixelResolutionUnit.PixelsPerInch }
    };

    public static readonly TheoryData<string, int> QualityFiles =
    new()
    {
        { TestImages.Jpeg.Baseline.Calliphora, 80 },
        { TestImages.Jpeg.Progressive.Fb, 75 },
        { TestImages.Jpeg.Issues.IncorrectQuality845, 98 },
        { TestImages.Jpeg.Baseline.ForestBridgeDifferentComponentsQuality, 89 },
        { TestImages.Jpeg.Progressive.Winter420_NonInterleaved, 80 }
    };

    [Theory]
    [MemberData(nameof(MetadataTestData))]
    public void MetadataIsParsedCorrectly(
        bool useIdentify,
        string imagePath,
        int expectedPixelSize,
        bool exifProfilePresent,
        bool iccProfilePresent) => TestMetadataImpl(
            useIdentify,
            JpegDecoder.Instance,
            imagePath,
            expectedPixelSize,
            exifProfilePresent,
            iccProfilePresent);

    [Theory]
    [MemberData(nameof(RatioFiles))]
    public void Decode_VerifyRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        using Image image = JpegDecoder.Instance.Decode(DecoderOptions.Default, stream);
        ImageMetadata meta = image.Metadata;
        Assert.Equal(xResolution, meta.HorizontalResolution);
        Assert.Equal(yResolution, meta.VerticalResolution);
        Assert.Equal(resolutionUnit, meta.ResolutionUnits);
    }

    [Theory]
    [MemberData(nameof(RatioFiles))]
    public void Identify_VerifyRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo image = JpegDecoder.Instance.Identify(DecoderOptions.Default, stream);
        ImageMetadata meta = image.Metadata;
        Assert.Equal(xResolution, meta.HorizontalResolution);
        Assert.Equal(yResolution, meta.VerticalResolution);
        Assert.Equal(resolutionUnit, meta.ResolutionUnits);
    }

    [Theory]
    [MemberData(nameof(RatioFiles))]
    public async Task Identify_VerifyRatioAsync(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo image = await JpegDecoder.Instance.IdentifyAsync(DecoderOptions.Default, stream);
        ImageMetadata meta = image.Metadata;
        Assert.Equal(xResolution, meta.HorizontalResolution);
        Assert.Equal(yResolution, meta.VerticalResolution);
        Assert.Equal(resolutionUnit, meta.ResolutionUnits);
    }

    [Theory]
    [MemberData(nameof(QualityFiles))]
    public void Identify_VerifyQuality(string imagePath, int quality)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo image = JpegDecoder.Instance.Identify(DecoderOptions.Default, stream);
        JpegMetadata meta = image.Metadata.GetJpegMetadata();
        Assert.Equal(quality, meta.Quality);
    }

    [Theory]
    [MemberData(nameof(QualityFiles))]
    public void Decode_VerifyQuality(string imagePath, int quality)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        using Image image = JpegDecoder.Instance.Decode(DecoderOptions.Default, stream);
        JpegMetadata meta = image.Metadata.GetJpegMetadata();
        Assert.Equal(quality, meta.Quality);
    }

    [Theory]
    [MemberData(nameof(QualityFiles))]
    public async Task Decode_VerifyQualityAsync(string imagePath, int quality)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        using Image image = await JpegDecoder.Instance.DecodeAsync(DecoderOptions.Default, stream);
        JpegMetadata meta = image.Metadata.GetJpegMetadata();
        Assert.Equal(quality, meta.Quality);
    }

    [Theory]
    [InlineData(TestImages.Jpeg.Baseline.Floorplan, JpegEncodingColor.Luminance)]
    [InlineData(TestImages.Jpeg.Baseline.Jpeg420Small, JpegEncodingColor.YCbCrRatio420)]
    [InlineData(TestImages.Jpeg.Baseline.Jpeg444, JpegEncodingColor.YCbCrRatio444)]
    [InlineData(TestImages.Jpeg.Baseline.JpegRgb, JpegEncodingColor.Rgb)]
    [InlineData(TestImages.Jpeg.Baseline.Cmyk, JpegEncodingColor.Cmyk)]
    [InlineData(TestImages.Jpeg.Baseline.Jpeg410, JpegEncodingColor.YCbCrRatio410)]
    [InlineData(TestImages.Jpeg.Baseline.Jpeg411, JpegEncodingColor.YCbCrRatio411)]
    public void Identify_DetectsCorrectColorType(string imagePath, JpegEncodingColor expectedColorType)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo image = JpegDecoder.Instance.Identify(DecoderOptions.Default, stream);
        JpegMetadata meta = image.Metadata.GetJpegMetadata();
        Assert.Equal(expectedColorType, meta.ColorType);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.Floorplan, PixelTypes.Rgb24, JpegEncodingColor.Luminance)]
    [WithFile(TestImages.Jpeg.Baseline.Jpeg420Small, PixelTypes.Rgb24, JpegEncodingColor.YCbCrRatio420)]
    [WithFile(TestImages.Jpeg.Baseline.Jpeg444, PixelTypes.Rgb24, JpegEncodingColor.YCbCrRatio444)]
    [WithFile(TestImages.Jpeg.Baseline.JpegRgb, PixelTypes.Rgb24, JpegEncodingColor.Rgb)]
    [WithFile(TestImages.Jpeg.Baseline.Cmyk, PixelTypes.Rgb24, JpegEncodingColor.Cmyk)]
    public void Decode_DetectsCorrectColorType<TPixel>(TestImageProvider<TPixel> provider, JpegEncodingColor expectedColorType)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance);
        JpegMetadata meta = image.Metadata.GetJpegMetadata();
        Assert.Equal(expectedColorType, meta.ColorType);
    }

    private static void TestImageInfo(string imagePath, IImageDecoder decoder, Action<ImageInfo> test)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo imageInfo = decoder.Identify(DecoderOptions.Default, stream);
        test(imageInfo);
    }

    private static void TestImageDecode(string imagePath, IImageDecoder decoder, Action<Image> test)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        using Image<Rgba32> img = decoder.Decode<Rgba32>(DecoderOptions.Default, stream);
        test(img);
    }

    private static void TestMetadataImpl(
        bool useIdentify,
        IImageDecoder decoder,
        string imagePath,
        int expectedPixelSize,
        bool exifProfilePresent,
        bool iccProfilePresent)
    {
        if (useIdentify)
        {
            TestImageInfo(
                imagePath,
                decoder,
                imageInfo =>
                {
                    Assert.NotNull(imageInfo);
                    Assert.NotNull(imageInfo.PixelType);
                    Assert.Equal(expectedPixelSize, imageInfo.PixelType.BitsPerPixel);

                    ExifProfile exifProfile = imageInfo.Metadata.ExifProfile;

                    if (exifProfilePresent)
                    {
                        Assert.NotNull(exifProfile);
                        Assert.NotEmpty(exifProfile.Values);
                    }
                    else
                    {
                        Assert.Null(exifProfile);
                    }

                    IccProfile iccProfile = imageInfo.Metadata.IccProfile;

                    if (iccProfilePresent)
                    {
                        Assert.NotNull(iccProfile);
                        Assert.NotEmpty(iccProfile.Entries);
                    }
                    else
                    {
                        Assert.Null(iccProfile);
                    }
                });
        }
        else
        {
            TestImageDecode(
                imagePath,
                decoder,
                imageInfo =>
                {
                    Assert.NotNull(imageInfo);
                    Assert.NotNull(imageInfo.PixelType);

                    // When full Image<TPixel> decoding is performed, BitsPerPixel will match TPixel
                    int bpp32 = Unsafe.SizeOf<Rgba32>() * 8;
                    Assert.Equal(bpp32, imageInfo.PixelType.BitsPerPixel);

                    ExifProfile exifProfile = imageInfo.Metadata.ExifProfile;

                    if (exifProfilePresent)
                    {
                        Assert.NotNull(exifProfile);
                        Assert.NotEmpty(exifProfile.Values);
                    }
                    else
                    {
                        Assert.Null(exifProfile);
                    }

                    IccProfile iccProfile = imageInfo.Metadata.IccProfile;

                    if (iccProfilePresent)
                    {
                        Assert.NotNull(iccProfile);
                        Assert.NotEmpty(iccProfile.Entries);
                    }
                    else
                    {
                        Assert.Null(iccProfile);
                    }
                });
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IgnoreMetadata_ControlsWhetherMetadataIsParsed(bool ignoreMetadata)
    {
        DecoderOptions options = new() { SkipMetadata = ignoreMetadata };

        // Snake.jpg has both Exif and ICC profiles defined:
        TestFile testFile = TestFile.Create(TestImages.Jpeg.Baseline.Snake);

        using Image<Rgba32> image = testFile.CreateRgba32Image(JpegDecoder.Instance, options);
        if (ignoreMetadata)
        {
            Assert.Null(image.Metadata.ExifProfile);
            Assert.Null(image.Metadata.IccProfile);
        }
        else
        {
            Assert.NotNull(image.Metadata.ExifProfile);
            Assert.NotNull(image.Metadata.IccProfile);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Decoder_Reads_Correct_Resolution_From_Jfif(bool useIdentify)
    {
        if (useIdentify)
        {
            TestImageInfo(
                TestImages.Jpeg.Baseline.Floorplan,
                JpegDecoder.Instance,
                imageInfo =>
                {
                    Assert.Equal(300, imageInfo.Metadata.HorizontalResolution);
                    Assert.Equal(300, imageInfo.Metadata.VerticalResolution);
                });
        }
        else
        {
            TestImageDecode(
                TestImages.Jpeg.Baseline.Floorplan,
                JpegDecoder.Instance,
                image =>
                {
                    Assert.Equal(300, image.Metadata.HorizontalResolution);
                    Assert.Equal(300, image.Metadata.VerticalResolution);
                });
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Decoder_Reads_Correct_Resolution_From_Exif(bool useIdentify)
    {
        if (useIdentify)
        {
            TestImageInfo(
                TestImages.Jpeg.Baseline.Jpeg420Exif,
                JpegDecoder.Instance,
                imageInfo =>
                {
                    Assert.Equal(72, imageInfo.Metadata.HorizontalResolution);
                    Assert.Equal(72, imageInfo.Metadata.VerticalResolution);
                });
        }
        else
        {
            TestImageDecode(
                TestImages.Jpeg.Baseline.Jpeg420Exif,
                JpegDecoder.Instance,
                imageInfo =>
                {
                    Assert.Equal(72, imageInfo.Metadata.HorizontalResolution);
                    Assert.Equal(72, imageInfo.Metadata.VerticalResolution);
                });
        }
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Issues.InvalidIptcTag, PixelTypes.Rgba32)]
    public void Decode_WithInvalidIptcTag_DoesNotThrowException<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Exception ex = Record.Exception(() =>
        {
            using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance);
        });
        Assert.Null(ex);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Issues.ExifNullArrayTag, PixelTypes.Rgba32)]
    public void Clone_WithNullRationalArrayTag_DoesNotThrowException<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Exception ex = Record.Exception(() =>
        {
            using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance);
            ExifProfile clone = image.Metadata.ExifProfile.DeepClone();
        });
        Assert.Null(ex);
    }

    [Fact]
    public void EncodedStringTags_WriteAndRead()
    {
        using MemoryStream memoryStream = new();
        using (Image image = Image.Load(TestFile.GetInputFileFullPath(TestImages.Jpeg.Baseline.Calliphora)))
        {
            ExifProfile exif = new();

            exif.SetValue(ExifTag.GPSDateStamp, "2022-01-06");

            exif.SetValue(ExifTag.XPTitle, "A bit of test metadata for image title");
            exif.SetValue(ExifTag.XPComment, "A bit of test metadata for image comment");
            exif.SetValue(ExifTag.XPAuthor, "Dan Petitt");
            exif.SetValue(ExifTag.XPKeywords, "Keyword1;Keyword2");
            exif.SetValue(ExifTag.XPSubject, "This is a subject");

            // exif.SetValue(ExifTag.UserComment, new EncodedString(EncodedString.CharacterCode.JIS, "ビッ"));
            exif.SetValue(ExifTag.UserComment, new EncodedString(EncodedString.CharacterCode.JIS, "eng comment text (JIS)"));

            exif.SetValue(ExifTag.GPSProcessingMethod, new EncodedString(EncodedString.CharacterCode.ASCII, "GPS processing method (ASCII)"));
            exif.SetValue(ExifTag.GPSAreaInformation, new EncodedString(EncodedString.CharacterCode.Unicode, "GPS area info (Unicode)"));

            image.Metadata.ExifProfile = exif;

            image.Save(memoryStream, new JpegEncoder());
        }

        memoryStream.Seek(0, SeekOrigin.Begin);
        using (Image image = Image.Load(memoryStream))
        {
            ExifProfile exif = image.Metadata.ExifProfile;
            VerifyEncodedStrings(exif);
        }
    }

    [Fact]
    public void EncodedStringTags_Read()
    {
        using Image image = Image.Load(TestFile.GetInputFileFullPath(TestImages.Jpeg.Baseline.Calliphora_EncodedStrings));
        ExifProfile exif = image.Metadata.ExifProfile;
        VerifyEncodedStrings(exif);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Issues.Issue2067_CommentMarker, PixelTypes.Rgba32)]
    public void JpegDecoder_DecodeMetadataComment<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        string expectedComment = "TEST COMMENT";
        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance);
        JpegMetadata metadata = image.Metadata.GetJpegMetadata();

        Assert.Equal(1, metadata.Comments.Count);
        Assert.Equal(expectedComment, metadata.Comments.ElementAtOrDefault(0).ToString());
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    private static void VerifyEncodedStrings(ExifProfile exif)
    {
        Assert.NotNull(exif);

        Assert.Equal("2022-01-06", exif.GetValue(ExifTag.GPSDateStamp).Value);

        Assert.Equal("A bit of test metadata for image title", exif.GetValue(ExifTag.XPTitle).Value);
        Assert.Equal("A bit of test metadata for image comment", exif.GetValue(ExifTag.XPComment).Value);
        Assert.Equal("Dan Petitt", exif.GetValue(ExifTag.XPAuthor).Value);
        Assert.Equal("Keyword1;Keyword2", exif.GetValue(ExifTag.XPKeywords).Value);
        Assert.Equal("This is a subject", exif.GetValue(ExifTag.XPSubject).Value);

        Assert.Equal("eng comment text (JIS)", exif.GetValue(ExifTag.UserComment).Value.Text);
        Assert.Equal(EncodedString.CharacterCode.JIS, exif.GetValue(ExifTag.UserComment).Value.Code);

        Assert.Equal("GPS processing method (ASCII)", exif.GetValue(ExifTag.GPSProcessingMethod).Value.Text);
        Assert.Equal(EncodedString.CharacterCode.ASCII, exif.GetValue(ExifTag.GPSProcessingMethod).Value.Code);

        Assert.Equal("GPS area info (Unicode)", (string)exif.GetValue(ExifTag.GPSAreaInformation).Value);
        Assert.Equal(EncodedString.CharacterCode.Unicode, exif.GetValue(ExifTag.GPSAreaInformation).Value.Code);
    }
}
