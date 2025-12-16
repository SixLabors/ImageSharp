// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Png.Chunks;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Png;

[Trait("Format", "Png")]
public class PngMetadataTests
{
    public static readonly TheoryData<string, int, int, PixelResolutionUnit> RatioFiles =
        new()
        {
            { TestImages.Png.Splash, 11810, 11810, PixelResolutionUnit.PixelsPerMeter },
            { TestImages.Png.Ratio1x4, 1, 4, PixelResolutionUnit.AspectRatio },
            { TestImages.Png.Ratio4x1, 4, 1, PixelResolutionUnit.AspectRatio }
        };

    [Fact]
    public void CloneIsDeep()
    {
        PngMetadata meta = new()
        {
            BitDepth = PngBitDepth.Bit16,
            ColorType = PngColorType.GrayscaleWithAlpha,
            InterlaceMethod = PngInterlaceMode.Adam7,
            Gamma = 2,
            TextData = new List<PngTextData> { new("name", "value", "foo", "bar") },
            RepeatCount = 123,
            AnimateRootFrame = false
        };

        PngMetadata clone = (PngMetadata)meta.DeepClone();

        Assert.True(meta.BitDepth == clone.BitDepth);
        Assert.True(meta.ColorType == clone.ColorType);
        Assert.True(meta.InterlaceMethod == clone.InterlaceMethod);
        Assert.True(meta.Gamma.Equals(clone.Gamma));
        Assert.False(meta.TextData.Equals(clone.TextData));
        Assert.True(meta.TextData.SequenceEqual(clone.TextData));
        Assert.True(meta.RepeatCount == clone.RepeatCount);
        Assert.True(meta.AnimateRootFrame == clone.AnimateRootFrame);

        clone.BitDepth = PngBitDepth.Bit2;
        clone.ColorType = PngColorType.Palette;
        clone.InterlaceMethod = PngInterlaceMode.None;
        clone.Gamma = 1;
        clone.RepeatCount = 321;

        Assert.False(meta.BitDepth == clone.BitDepth);
        Assert.False(meta.ColorType == clone.ColorType);
        Assert.False(meta.InterlaceMethod == clone.InterlaceMethod);
        Assert.False(meta.Gamma.Equals(clone.Gamma));
        Assert.False(meta.TextData.Equals(clone.TextData));
        Assert.True(meta.TextData.SequenceEqual(clone.TextData));
        Assert.False(meta.RepeatCount == clone.RepeatCount);
    }

    [Theory]
    [WithFile(TestImages.Png.PngWithMetadata, PixelTypes.Rgba32)]
    public void Decoder_CanReadTextData<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        PngMetadata meta = image.Metadata.GetFormatMetadata(PngFormat.Instance);
        VerifyTextDataIsPresent(meta);
    }

    [Theory]
    [WithFile(TestImages.Png.PngWithMetadata, PixelTypes.Rgba32)]
    public void Encoder_PreservesTextData<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> input = provider.GetImage(PngDecoder.Instance);
        using MemoryStream memoryStream = new();
        input.Save(memoryStream, new PngEncoder());

        memoryStream.Position = 0;
        using Image<Rgba32> image = PngDecoder.Instance.Decode<Rgba32>(DecoderOptions.Default, memoryStream);
        PngMetadata meta = image.Metadata.GetFormatMetadata(PngFormat.Instance);
        VerifyTextDataIsPresent(meta);
    }

    [Theory]
    [WithFile(TestImages.Png.InvalidTextData, PixelTypes.Rgba32)]
    public void Decoder_IgnoresInvalidTextData<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        PngMetadata meta = image.Metadata.GetFormatMetadata(PngFormat.Instance);
        Assert.DoesNotContain(meta.TextData, m => m.Value is "leading space");
        Assert.DoesNotContain(meta.TextData, m => m.Value is "trailing space");
        Assert.DoesNotContain(meta.TextData, m => m.Value is "space");
        Assert.DoesNotContain(meta.TextData, m => m.Value is "empty");
        Assert.DoesNotContain(meta.TextData, m => m.Value is "invalid characters");
        Assert.DoesNotContain(meta.TextData, m => m.Value is "too large");
    }

    [Theory]
    [WithFile(TestImages.Png.PngWithMetadata, PixelTypes.Rgba32)]
    public void Encode_UseCompression_WhenTextIsGreaterThenThreshold_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> input = provider.GetImage(PngDecoder.Instance);
        using MemoryStream memoryStream = new();

        // This will be a zTXt chunk.
        PngTextData expectedText = new("large-text", new string('c', 100), string.Empty, string.Empty);

        // This will be a iTXt chunk.
        PngTextData expectedTextNoneLatin = new("large-text-non-latin", new string('Ф', 100), "language-tag", "translated-keyword");
        PngMetadata inputMetadata = input.Metadata.GetFormatMetadata(PngFormat.Instance);
        inputMetadata.TextData.Add(expectedText);
        inputMetadata.TextData.Add(expectedTextNoneLatin);
        input.Save(memoryStream, new PngEncoder
        {
            TextCompressionThreshold = 50
        });

        memoryStream.Position = 0;
        using Image<Rgba32> image = PngDecoder.Instance.Decode<Rgba32>(DecoderOptions.Default, memoryStream);
        PngMetadata meta = image.Metadata.GetFormatMetadata(PngFormat.Instance);
        Assert.Contains(meta.TextData, m => m.Equals(expectedText));
        Assert.Contains(meta.TextData, m => m.Equals(expectedTextNoneLatin));
    }

    [Theory]
    [WithFile(TestImages.Png.PngWithMetadata, PixelTypes.Rgba32)]
    public void Decode_ReadsExifData<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new()
        {
            SkipMetadata = false
        };

        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance, options);
        Assert.NotNull(image.Metadata.ExifProfile);
        ExifProfile exif = image.Metadata.ExifProfile;
        VerifyExifDataIsPresent(exif);
    }

    [Theory]
    [WithFile(TestImages.Png.DefaultNotAnimated, PixelTypes.Rgba32)]
    public void Decode_IdentifiesDefaultFrameNotAnimated<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        PngMetadata meta = image.Metadata.GetFormatMetadata(PngFormat.Instance);
        Assert.False(meta.AnimateRootFrame);
    }

    [Theory]
    [WithFile(TestImages.Png.APng, PixelTypes.Rgba32)]
    public void Decode_IdentifiesDefaultFrameAnimated<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        PngMetadata meta = image.Metadata.GetFormatMetadata(PngFormat.Instance);
        Assert.True(meta.AnimateRootFrame);
    }

    [Theory]
    [WithFile(TestImages.Png.PngWithMetadata, PixelTypes.Rgba32)]
    public void Decode_IgnoresExifData_WhenIgnoreMetadataIsTrue<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new()
        {
            SkipMetadata = true
        };

        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance, options);
        Assert.Null(image.Metadata.ExifProfile);
    }

    [Fact]
    public void Decode_IgnoreMetadataIsFalse_TextChunkIsRead()
    {
        DecoderOptions options = new()
        {
            SkipMetadata = false
        };

        TestFile testFile = TestFile.Create(TestImages.Png.Blur);

        using Image<Rgba32> image = testFile.CreateRgba32Image(PngDecoder.Instance, options);
        PngMetadata meta = image.Metadata.GetFormatMetadata(PngFormat.Instance);

        Assert.Equal(1, meta.TextData.Count);
        Assert.Equal("Software", meta.TextData[0].Keyword);
        Assert.Equal("paint.net 4.0.6", meta.TextData[0].Value);
        Assert.Equal(0.4545d, meta.Gamma, precision: 4);
    }

    [Fact]
    public void Decode_IgnoreMetadataIsTrue_TextChunksAreIgnored()
    {
        DecoderOptions options = new()
        {
            SkipMetadata = true
        };

        TestFile testFile = TestFile.Create(TestImages.Png.PngWithMetadata);

        using Image<Rgba32> image = testFile.CreateRgba32Image(PngDecoder.Instance, options);
        PngMetadata meta = image.Metadata.GetFormatMetadata(PngFormat.Instance);
        Assert.Equal(0, meta.TextData.Count);
    }

    [Theory]
    [MemberData(nameof(RatioFiles))]
    public void Decode_VerifyRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        using Image<Rgba32> image = PngDecoder.Instance.Decode<Rgba32>(DecoderOptions.Default, stream);
        ImageMetadata meta = image.Metadata;
        Assert.Equal(xResolution, meta.HorizontalResolution);
        Assert.Equal(yResolution, meta.VerticalResolution);
        Assert.Equal(resolutionUnit, meta.ResolutionUnits);
    }

    [Theory]
    [WithFile(TestImages.Png.PngWithMetadata, PixelTypes.Rgba32)]
    public void Encode_PreservesColorProfile<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> input = provider.GetImage(PngDecoder.Instance);
        ImageSharp.Metadata.Profiles.Icc.IccProfile expectedProfile = input.Metadata.IccProfile;
        byte[] expectedProfileBytes = expectedProfile.ToByteArray();

        using MemoryStream memStream = new();
        input.Save(memStream, new PngEncoder());

        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        ImageSharp.Metadata.Profiles.Icc.IccProfile actualProfile = output.Metadata.IccProfile;
        byte[] actualProfileBytes = actualProfile.ToByteArray();

        Assert.NotNull(actualProfile);
        Assert.Equal(expectedProfileBytes, actualProfileBytes);
    }

    [Theory]
    [MemberData(nameof(RatioFiles))]
    public void Identify_VerifyRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo image = PngDecoder.Instance.Identify(DecoderOptions.Default, stream);
        ImageMetadata meta = image.Metadata;
        Assert.Equal(xResolution, meta.HorizontalResolution);
        Assert.Equal(yResolution, meta.VerticalResolution);
        Assert.Equal(resolutionUnit, meta.ResolutionUnits);
    }

    [Theory]
    [InlineData(TestImages.Png.PngWithMetadata)]
    public void Identify_ReadsTextData(string imagePath)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo imageInfo = Image.Identify(stream);
        Assert.NotNull(imageInfo);
        PngMetadata meta = imageInfo.Metadata.GetFormatMetadata(PngFormat.Instance);
        VerifyTextDataIsPresent(meta);
    }

    [Theory]
    [InlineData(TestImages.Png.PngWithMetadata)]
    public void Identify_ReadsExifData(string imagePath)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo imageInfo = Image.Identify(stream);
        Assert.NotNull(imageInfo);
        Assert.NotNull(imageInfo.Metadata.ExifProfile);
        ExifProfile exif = imageInfo.Metadata.ExifProfile;
        VerifyExifDataIsPresent(exif);
    }

    private static void VerifyExifDataIsPresent(ExifProfile exif)
    {
        Assert.Equal(1, exif.Values.Count);
        IExifValue<string> software = exif.GetValue(ExifTag.Software);
        Assert.NotNull(software);
        Assert.Equal("ImageSharp", software.Value);
    }

    private static void VerifyTextDataIsPresent(PngMetadata meta)
    {
        Assert.NotNull(meta);
        Assert.Contains(meta.TextData, m => m.Keyword is "Comment" && m.Value is "comment");
        Assert.Contains(meta.TextData, m => m.Keyword is "Author" && m.Value is "ImageSharp");
        Assert.Contains(meta.TextData, m => m.Keyword is "Copyright" && m.Value is "ImageSharp");
        Assert.Contains(meta.TextData, m => m.Keyword is "Title" && m.Value is "unittest");
        Assert.Contains(meta.TextData, m => m.Keyword is "Description" && m.Value is "compressed-text");
        Assert.Contains(meta.TextData, m => m.Keyword is "International" && m.Value is "'e', mu'tlheghvam, ghaH yu'" && m.LanguageTag is "x-klingon" && m.TranslatedKeyword is "warning");
        Assert.Contains(meta.TextData, m => m.Keyword is "International2" && m.Value is "ИМАГЕШАРП" && m.LanguageTag is "rus");
        Assert.Contains(meta.TextData, m => m.Keyword is "CompressedInternational" && m.Value is "la plume de la mante" && m.LanguageTag is "fra" && m.TranslatedKeyword is "foobar");
        Assert.Contains(meta.TextData, m => m.Keyword is "CompressedInternational2" && m.Value is "這是一個考驗" && m.LanguageTag is "chinese");
        Assert.Contains(meta.TextData, m => m.Keyword is "NoLang" && m.Value is "this text chunk is missing a language tag");
        Assert.Contains(meta.TextData, m => m.Keyword is "NoTranslatedKeyword" && m.Value is "dieser chunk hat kein übersetztes Schlüßelwort");
    }

    [Theory]
    [InlineData(TestImages.Png.Issue1875)]
    public void Identify_ReadsLegacyExifData(string imagePath)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo imageInfo = Image.Identify(stream);
        Assert.NotNull(imageInfo);
        Assert.NotNull(imageInfo.Metadata.ExifProfile);

        PngMetadata meta = imageInfo.Metadata.GetFormatMetadata(PngFormat.Instance);
        Assert.DoesNotContain(meta.TextData, t => t.Keyword.Equals("Raw profile type exif", StringComparison.OrdinalIgnoreCase));

        ExifProfile exif = imageInfo.Metadata.ExifProfile;
        Assert.Equal(0, exif.InvalidTags.Count);
        Assert.Equal(3, exif.Values.Count);

        Assert.Equal(
            "A colorful tiling of blue, red, yellow, and green 4x4 pixel blocks.",
            exif.GetValue(ExifTag.ImageDescription).Value);
        Assert.Equal(
            "Duplicated from basn3p02.png, then image metadata modified with exiv2",
            exif.GetValue(ExifTag.ImageHistory).Value);

        Assert.Equal(42, (int)exif.GetValue(ExifTag.ImageNumber).Value);
    }


    [Theory]
    [InlineData(PixelColorType.Binary, PngColorType.Palette)]
    [InlineData(PixelColorType.Indexed, PngColorType.Palette)]
    [InlineData(PixelColorType.Luminance, PngColorType.Grayscale)]
    [InlineData(PixelColorType.RGB, PngColorType.Rgb)]
    [InlineData(PixelColorType.BGR, PngColorType.Rgb)]
    [InlineData(PixelColorType.YCbCr, PngColorType.RgbWithAlpha)]
    [InlineData(PixelColorType.CMYK, PngColorType.RgbWithAlpha)]
    [InlineData(PixelColorType.YCCK, PngColorType.RgbWithAlpha)]
    public void FromFormatConnectingMetadata_ConvertColorTypeAsExpected(PixelColorType pixelColorType, PngColorType expectedPngColorType)
    {
        FormatConnectingMetadata formatConnectingMetadata = new()
        {
            PixelTypeInfo = new PixelTypeInfo(24)
            {
                ColorType = pixelColorType,
            },
        };

        PngMetadata actual = PngMetadata.FromFormatConnectingMetadata(formatConnectingMetadata);

        Assert.Equal(expectedPngColorType, actual.ColorType);
    }
}
