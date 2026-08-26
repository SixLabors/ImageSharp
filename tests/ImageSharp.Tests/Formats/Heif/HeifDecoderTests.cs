// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.ColorProfiles.Icc;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats.Heif;

[Trait("Format", "Heif")]
[ValidateDisposedMemoryAllocations]
public class HeifDecoderTests
{
    private const uint UnknownBoxType = 0x74657374U;

    private static ReadOnlySpan<byte> MalformedJpegApp13 =>
    [
        0xFF, 0xED,
        0x00, 0x1D,
        (byte)'P', (byte)'h', (byte)'o', (byte)'t', (byte)'o', (byte)'s', (byte)'h', (byte)'o', (byte)'p', (byte)' ', (byte)'3', (byte)'.',
        (byte)'0', 0x00,
        (byte)'B', (byte)'a', (byte)'d', (byte)'R', (byte)'e', (byte)'s', (byte)'o', (byte)'u', (byte)'r', (byte)'c', (byte)'e', (byte)'!',
        (byte)'!'
    ];

    [Theory]
    [InlineData(TestImages.Heif.Image1, HeifCompressionMethod.Hevc, HeifBitDepth.Bit8, 3992, 2992)]
    [InlineData(TestImages.Heif.Sample640x427, HeifCompressionMethod.Hevc, HeifBitDepth.Bit8, 640, 428)]
    [InlineData(TestImages.Heif.FujiFilmHif, HeifCompressionMethod.LegacyJpeg, HeifBitDepth.Bit8, 7728, 5152)]
    [InlineData(TestImages.Heif.IrvineAvif, HeifCompressionMethod.Av1, HeifBitDepth.Bit8, 480, 640)]
    public void Identify(string imagePath, HeifCompressionMethod compressionMethod, HeifBitDepth bitDepth, int width, int height)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);

        ImageInfo imageInfo = Image.Identify(stream);
        HeifMetadata heicMetadata = imageInfo.Metadata.GetHeifMetadata();

        Assert.NotNull(imageInfo);
        Assert.Equal(HeifFormat.Instance, imageInfo.Metadata.DecodedImageFormat);
        Assert.Equal(compressionMethod, heicMetadata.CompressionMethod);
        Assert.Equal(bitDepth, heicMetadata.BitDepth);
        Assert.Equal(width, imageInfo.Width);
        Assert.Equal(height, imageInfo.Height);
    }

    [Theory]
    [WithFile(TestImages.Heif.FujiFilmHif, PixelTypes.Rgba32)]
    public void Decode<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        HeifMetadata heicMetadata = image.Metadata.GetHeifMetadata();
        image.DebugSave(provider);

        image.CompareToReferenceOutput(provider);
        Assert.Equal(HeifCompressionMethod.LegacyJpeg, heicMetadata.CompressionMethod);
    }

    /// <summary>
    /// Verifies complete HEVC still-image decoding for real grid, auxiliary-alpha, 4:2:0, and 4:4:4 HEIC inputs.
    /// </summary>
    /// <param name="provider">The real HEIC input and matching output naming context.</param>
    /// <param name="width">The independently reported presented width.</param>
    /// <param name="height">The independently reported presented height.</param>
    /// <param name="hasIccProfile">Whether the presented image carries an ICC profile.</param>
    [Theory]
    [WithFile(TestImages.Heif.Image1, PixelTypes.Rgba32, 3992, 2992, true)]
    [WithFile(TestImages.Heif.Image2, PixelTypes.Rgba32, 3464, 2130, true)]
    [WithFile(TestImages.Heif.Image3, PixelTypes.Rgba32, 4242, 2828, true)]
    [WithFile(TestImages.Heif.Image4, PixelTypes.Rgba32, 700, 476, true)]
    [WithFile(TestImages.Heif.Sample640x427, PixelTypes.Rgba32, 640, 428, false)]
    public void DecodeHevcStillImage<TPixel>(TestImageProvider<TPixel> provider, int width, int height, bool hasIccProfile)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        HeifMetadata metadata = image.Metadata.GetHeifMetadata();
        image.DebugSave(provider);

        image.CompareToReferenceOutput(ImageComparer.Exact, provider);

        Assert.Equal(new Size(width, height), image.Size);
        Assert.Equal(HeifCompressionMethod.Hevc, metadata.CompressionMethod);
        Assert.Equal(HeifBitDepth.Bit8, metadata.BitDepth);
        Assert.Equal(hasIccProfile, image.Metadata.IccProfile is not null);
    }

    /// <summary>
    /// Verifies that AVIF decoding preserves the exact embedded ICC profile bytes.
    /// </summary>
    [Theory]
    [WithFile(TestImages.Heif.ParisIccExifXmpAvif, PixelTypes.Rgba32)]
    public void DecodeAvifPreservesEmbeddedIccProfile<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions preserveOptions = new() { ColorProfileHandling = ColorProfileHandling.Preserve };

        using Image<TPixel> preserved = provider.GetImage(HeifDecoder.Instance, preserveOptions);
        using Image<TPixel> expectedPreserved = Image.Load<TPixel>(preserveOptions, TestFile.Create(TestImages.Heif.ParisIccExifXmpPng).Bytes);

        Assert.NotNull(preserved.Metadata.IccProfile);
        Assert.NotNull(expectedPreserved.Metadata.IccProfile);
        Assert.Equal(expectedPreserved.Metadata.IccProfile.ToByteArray(), preserved.Metadata.IccProfile.ToByteArray());
    }

    /// <summary>
    /// Verifies that AVIF decoding converts pixels from an embedded non-sRGB ICC profile to sRGB.
    /// </summary>
    [Theory]
    [WithFile(TestImages.Heif.PerceptualIccAvif, PixelTypes.Rgba32)]
    public void DecodeAvifConvertsEmbeddedNonSrgbIccProfile<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions preserveOptions = new() { ColorProfileHandling = ColorProfileHandling.Preserve };
        DecoderOptions convertOptions = new() { ColorProfileHandling = ColorProfileHandling.Convert };

        using Image<TPixel> preserved = provider.GetImage(HeifDecoder.Instance, preserveOptions);
        using Image<TPixel> converted = provider.GetImage(HeifDecoder.Instance, convertOptions);
        using Image<TPixel> expected = Image.Load<TPixel>(convertOptions, TestFile.Create(TestImages.Png.Icc.Perceptual).Bytes);

        Assert.NotNull(preserved.Metadata.IccProfile);
        Assert.Null(converted.Metadata.IccProfile);
        Assert.NotEmpty(ImageComparer.Exact.CompareImages(preserved, converted));

        converted.DebugSave(provider, testOutputDetails: "IccConverted");

        // The PNG is the independent RGB source used by libavif's avifenc. A tolerant comparison accounts for the
        // AV1 loss while proving the AVIF ICC stage produces the same target-profile interpretation.
        ImageComparer.TolerantPercentage(1F, 20).VerifySimilarity(expected, converted);
    }

    /// <summary>
    /// Verifies that AVIF grid composition retains and converts the presented image's non-sRGB ICC profile.
    /// </summary>
    [Theory]
    [WithFile(TestImages.Heif.PerceptualIccGridAvif, PixelTypes.Rgba32)]
    public void DecodeAvifGridConvertsEmbeddedNonSrgbIccProfile<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions preserveOptions = new() { ColorProfileHandling = ColorProfileHandling.Preserve };
        DecoderOptions convertOptions = new() { ColorProfileHandling = ColorProfileHandling.Convert };

        using Image<TPixel> preserved = provider.GetImage(HeifDecoder.Instance, preserveOptions);
        using Image<TPixel> converted = provider.GetImage(HeifDecoder.Instance, convertOptions);
        using Image<TPixel> expectedPreserved = Image.Load<TPixel>(preserveOptions, TestFile.Create(TestImages.Png.Icc.Perceptual).Bytes);
        using Image<TPixel> expected = Image.Load<TPixel>(convertOptions, TestFile.Create(TestImages.Png.Icc.Perceptual).Bytes);

        Assert.NotNull(preserved.Metadata.IccProfile);
        Assert.Null(converted.Metadata.IccProfile);
        Assert.Equal(expectedPreserved.Metadata.IccProfile!.ToByteArray(), preserved.Metadata.IccProfile.ToByteArray());
        Assert.NotEmpty(ImageComparer.Exact.CompareImages(preserved, converted));
        ImageComparer.TolerantPercentage(1F, 20).VerifySimilarity(expected, converted);
    }

    /// <summary>
    /// Verifies that AVIF sequence ICC conversion is applied to every presented frame.
    /// </summary>
    [Theory]
    [WithFile(TestImages.Heif.PerceptualIccSequenceAvif, PixelTypes.Rgba32)]
    public void DecodeAvifSequenceConvertsEveryFrameWithEmbeddedNonSrgbIccProfile<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions preserveOptions = new() { ColorProfileHandling = ColorProfileHandling.Preserve };
        DecoderOptions convertOptions = new() { ColorProfileHandling = ColorProfileHandling.Convert };

        using Image<TPixel> preserved = provider.GetImage(HeifDecoder.Instance, preserveOptions);
        using Image<TPixel> converted = provider.GetImage(HeifDecoder.Instance, convertOptions);
        using Image<TPixel> expectedPreserved = Image.Load<TPixel>(preserveOptions, TestFile.Create(TestImages.Png.Icc.Perceptual).Bytes);
        using Image<TPixel> expected = Image.Load<TPixel>(convertOptions, TestFile.Create(TestImages.Png.Icc.Perceptual).Bytes);

        Assert.Equal(2, preserved.Frames.Count);
        Assert.Equal(preserved.Frames.Count, converted.Frames.Count);
        Assert.NotNull(preserved.Metadata.IccProfile);
        Assert.Null(converted.Metadata.IccProfile);
        Assert.Equal(expectedPreserved.Metadata.IccProfile!.ToByteArray(), preserved.Metadata.IccProfile.ToByteArray());

        for (int i = 0; i < converted.Frames.Count; i++)
        {
            Assert.False(ImageComparer.Exact.CompareImagesOrFrames(i, preserved.Frames[i], converted.Frames[i]).IsEmpty);
            Assert.True(ImageComparer.TolerantPercentage(1F, 20).CompareImagesOrFrames(i, expected.Frames.RootFrame, converted.Frames[i]).IsEmpty);
        }
    }

    /// <summary>
    /// Verifies that non-sRGB ICC conversion follows auxiliary-alpha composition and preserves the composed alpha values.
    /// </summary>
    [Fact]
    public void DecodeAvifAlphaImageConvertsEmbeddedIccProfileWithoutChangingAlpha()
    {
        DecoderOptions preserveOptions = new() { ColorProfileHandling = ColorProfileHandling.Preserve };
        DecoderOptions convertOptions = new() { ColorProfileHandling = ColorProfileHandling.Convert };
        byte[] encoded = TestFile.Create(TestImages.Heif.DuckyRommIccAlphaAvif).Bytes;

        using Image<Rgba32> preserved = Image.Load<Rgba32>(preserveOptions, encoded);
        using Image<Rgba32> converted = Image.Load<Rgba32>(convertOptions, encoded);
        using Image<Rgba32> expected = preserved.Clone();

        ColorProfileConverter converter = new(new ColorConversionOptions
        {
            SourceIccProfile = expected.Metadata.IccProfile,
            TargetIccProfile = CompactSrgbV4Profile.Profile,
            MemoryAllocator = expected.Configuration.MemoryAllocator,
        });

        // Build the oracle from the fully composed preserved decode so that only ICC ordering and alpha retention
        // are under test; the independently encoded AV1 color and alpha payloads remain identical in both paths.
        converter.Convert(expected);

        Assert.NotNull(preserved.Metadata.IccProfile);
        Assert.Null(converted.Metadata.IccProfile);
        Assert.Equal(TestIccProfiles.GetProfile(TestIccProfiles.RommRgb).ToByteArray(), preserved.Metadata.IccProfile.ToByteArray());
        Assert.NotEmpty(ImageComparer.Exact.CompareImages(preserved, converted));

        for (int y = 0; y < converted.Height; y++)
        {
            Span<Rgba32> preservedRow = preserved.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            Span<Rgba32> convertedRow = converted.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);

            for (int x = 0; x < convertedRow.Length; x++)
            {
                Assert.Equal(preservedRow[x].A, convertedRow[x].A);
            }
        }

        ImageComparer.Exact.VerifySimilarity(expected, converted);
    }

    /// <summary>
    /// Verifies that compact profile handling retains non-sRGB ICC profiles and leaves their pixels unconverted.
    /// </summary>
    [Theory]
    [WithFile(TestImages.Heif.PerceptualIccAvif, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.PerceptualIccGridAvif, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.PerceptualIccSequenceAvif, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.DuckyRommIccAlphaAvif, PixelTypes.Rgba32)]
    public void DecodeAvifRetainsNonSrgbIccProfileWhenCompacting<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions preserveOptions = new() { ColorProfileHandling = ColorProfileHandling.Preserve };
        DecoderOptions compactOptions = new() { ColorProfileHandling = ColorProfileHandling.Compact };

        using Image<TPixel> preserved = provider.GetImage(HeifDecoder.Instance, preserveOptions);
        using Image<TPixel> compact = provider.GetImage(HeifDecoder.Instance, compactOptions);

        Assert.NotNull(preserved.Metadata.IccProfile);
        Assert.NotNull(compact.Metadata.IccProfile);
        Assert.Equal(preserved.Metadata.IccProfile.ToByteArray(), compact.Metadata.IccProfile.ToByteArray());
        Assert.Empty(ImageComparer.Exact.CompareImages(preserved, compact));
    }

    /// <summary>
    /// Verifies that compact profile handling removes a canonical sRGB ICC profile without changing pixels.
    /// </summary>
    [Theory]
    [WithFile(TestImages.Heif.ParisIccExifXmpAvif, PixelTypes.Rgba32)]
    public void DecodeAvifCompactsCanonicalSrgbIccProfile<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions preserveOptions = new() { ColorProfileHandling = ColorProfileHandling.Preserve };
        DecoderOptions compactOptions = new() { ColorProfileHandling = ColorProfileHandling.Compact };

        using Image<TPixel> preserved = provider.GetImage(HeifDecoder.Instance, preserveOptions);
        using Image<TPixel> compact = provider.GetImage(HeifDecoder.Instance, compactOptions);

        Assert.NotNull(preserved.Metadata.IccProfile);
        Assert.Null(compact.Metadata.IccProfile);
        Assert.Empty(ImageComparer.Exact.CompareImages(preserved, compact));
    }

    /// <summary>
    /// Verifies that metadata skipping omits the embedded AVIF ICC profile.
    /// </summary>
    [Theory]
    [WithFile(TestImages.Heif.ParisIccExifXmpAvif, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.PerceptualIccGridAvif, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.PerceptualIccSequenceAvif, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.DuckyRommIccAlphaAvif, PixelTypes.Rgba32)]
    public void DecodeAvifSkipsEmbeddedIccProfileWithMetadata<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new()
        {
            ColorProfileHandling = ColorProfileHandling.Preserve,
            SkipMetadata = true
        };

        using Image<TPixel> image = provider.GetImage(HeifDecoder.Instance, options);

        Assert.Null(image.Metadata.IccProfile);
    }

    [Fact]
    public void DecodeIgnoresUnknownTopLevelBox()
    {
        byte[] data = CreateEncodedContainer();
        data = InsertBytes(data, data.Length, CreateUnknownBox());

        using Image<Rgba32> image = Image.Load<Rgba32>(data);

        Assert.Equal(new Size(2, 3), image.Size);
    }

    [Fact]
    public void DecodeAppliesTargetSizeOnceToThePresentedHeifImage()
    {
        using Image<Rgba32> source = new(64, 48);
        for (int y = 0; y < source.Height; y++)
        {
            Span<Rgba32> row = source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < row.Length; x++)
            {
                row[x] = new Rgba32((byte)(x * 3), (byte)(y * 5), (byte)((x * 7) + (y * 11)));
            }
        }

        using MemoryStream stream = new();
        source.Save(stream, new HeifEncoder());
        byte[] data = stream.ToArray();
        Size targetSize = new(17, 17);
        DecoderOptions options = new() { TargetSize = targetSize };

        using Image<Rgba32> expected = Image.Load<Rgba32>(data);
        expected.Mutate(context => context.Resize(new ResizeOptions { Size = targetSize, Mode = ResizeMode.Max, Sampler = options.Sampler }));

        using Image<Rgba32> image = Image.Load<Rgba32>(options, data);

        Assert.Equal(expected.Size, image.Size);
        for (int y = 0; y < image.Height; y++)
        {
            Assert.True(image.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y).SequenceEqual(
                expected.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y)));
        }
    }

    [Fact]
    public void DecodePropagatesStrictValidationToLegacyJpegItems()
    {
        byte[] data = CreateContainerWithMalformedJpegMetadata();
        DecoderOptions options = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };

        Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(options, data);
        });
    }

    [Theory]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(SegmentIntegrityHandling.IgnoreImageData)]
    public void DecodePropagatesRecoverableMetadataValidationToLegacyJpegItems(SegmentIntegrityHandling handling)
    {
        byte[] data = CreateContainerWithMalformedJpegMetadata();
        DecoderOptions options = new() { SegmentIntegrityHandling = handling };

        using Image<Rgba32> image = Image.Load<Rgba32>(options, data);

        Assert.Equal(new Size(2, 3), image.Size);
    }

    [Fact]
    public void DecodePropagatesSkipMetadataToLegacyJpegItems()
    {
        byte[] data = CreateContainerWithMalformedJpegMetadata();
        DecoderOptions options = new()
        {
            SkipMetadata = true,
            SegmentIntegrityHandling = SegmentIntegrityHandling.Strict
        };

        using Image<Rgba32> image = Image.Load<Rgba32>(options, data);

        Assert.Equal(new Size(2, 3), image.Size);
    }

    [Fact]
    public void DecodePropagatesConfigurationToLegacyJpegItems()
    {
        byte[] data = CreateEncodedContainer();
        Configuration configuration = Configuration.CreateDefaultInstance();
        DecoderOptions options = new() { Configuration = configuration };

        using Image<Rgba32> image = Image.Load<Rgba32>(options, data);

        Assert.Same(configuration, image.Configuration);
    }

    /// <summary>
    /// Verifies that invalid optional alpha payloads remain fatal when image-data errors cannot be ignored.
    /// </summary>
    [Theory]
    [InlineData(SegmentIntegrityHandling.Strict)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary)]
    public void DecodeRejectsInvalidAlphaPayloadUnlessImageDataErrorsAreIgnored(SegmentIntegrityHandling handling)
    {
        byte[] data = [.. TestFile.Create(TestImages.Heif.DuckyRommIccAlphaAvif).Bytes];
        uint alphaItemId = FindFirstItemReferenceSourceId(data, Heif4CharCode.Auxl);
        ClearItemPayload(data, alphaItemId);
        DecoderOptions options = new() { SegmentIntegrityHandling = handling };

        Assert.ThrowsAny<InvalidImageContentException>(() =>
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(options, data);
        });
    }

    /// <summary>
    /// Verifies that <see cref="SegmentIntegrityHandling.IgnoreImageData"/> omits a corrupt optional alpha item while
    /// retaining the independently decodable color item.
    /// </summary>
    [Fact]
    public void DecodeOmitsInvalidAlphaPayloadWhenImageDataErrorsAreIgnored()
    {
        byte[] source = TestFile.Create(TestImages.Heif.DuckyRommIccAlphaAvif).Bytes;
        byte[] data = [.. source];
        uint alphaItemId = FindFirstItemReferenceSourceId(data, Heif4CharCode.Auxl);
        ClearItemPayload(data, alphaItemId);
        DecoderOptions options = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.IgnoreImageData };

        using Image<Rgba32> expected = Image.Load<Rgba32>(source);
        using Image<Rgba32> actual = Image.Load<Rgba32>(options, data);

        AssertOpaqueRgbMatches(expected, actual);
    }

    /// <summary>
    /// Verifies that a malformed alpha relationship remains fatal when image-data errors cannot be ignored.
    /// </summary>
    [Theory]
    [InlineData(SegmentIntegrityHandling.Strict)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary)]
    public void DecodeRejectsMalformedAlphaReferenceUnlessImageDataErrorsAreIgnored(SegmentIntegrityHandling handling)
    {
        byte[] data = [.. TestFile.Create(TestImages.Heif.DuckyRommIccAlphaAvif).Bytes];
        InvalidateFirstItemReferenceSource(data, Heif4CharCode.Auxl);
        DecoderOptions options = new() { SegmentIntegrityHandling = handling };

        Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(options, data);
        });
    }

    /// <summary>
    /// Verifies that <see cref="SegmentIntegrityHandling.IgnoreImageData"/> omits a malformed optional alpha
    /// relationship while retaining the independently decodable color item.
    /// </summary>
    [Fact]
    public void DecodeOmitsMalformedAlphaReferenceWhenImageDataErrorsAreIgnored()
    {
        byte[] source = TestFile.Create(TestImages.Heif.DuckyRommIccAlphaAvif).Bytes;
        byte[] data = [.. source];
        InvalidateFirstItemReferenceSource(data, Heif4CharCode.Auxl);
        DecoderOptions options = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.IgnoreImageData };

        using Image<Rgba32> expected = Image.Load<Rgba32>(source);
        using Image<Rgba32> actual = Image.Load<Rgba32>(options, data);

        AssertOpaqueRgbMatches(expected, actual);
    }

    /// <summary>
    /// Verifies that strict validation rejects a malformed descriptive metadata relationship.
    /// </summary>
    [Fact]
    public void DecodeRejectsMalformedMetadataReferenceInStrictMode()
    {
        byte[] data = [.. TestFile.Create(TestImages.Heif.ParisIccExifXmpAvif).Bytes];
        InvalidateFirstItemReferenceSource(data, Heif4CharCode.Cdsc);
        DecoderOptions options = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };

        Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(options, data);
        });
    }

    /// <summary>
    /// Verifies that non-strict validation omits a malformed descriptive relationship without weakening image-data
    /// validation.
    /// </summary>
    [Theory]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(SegmentIntegrityHandling.IgnoreImageData)]
    public void DecodeOmitsMalformedMetadataReferenceWhenAncillaryErrorsAreIgnored(SegmentIntegrityHandling handling)
    {
        byte[] data = [.. TestFile.Create(TestImages.Heif.ParisIccExifXmpAvif).Bytes];
        InvalidateFirstItemReferenceSource(data, Heif4CharCode.Cdsc);
        DecoderOptions options = new() { SegmentIntegrityHandling = handling };

        using Image<Rgba32> image = Image.Load<Rgba32>(options, data);

        Assert.Null(image.Metadata.ExifProfile);
        Assert.NotNull(image.Metadata.XmpProfile);
        Assert.NotNull(image.Metadata.IccProfile);
    }

    /// <summary>
    /// Verifies that skipped metadata is neither retained nor validated through its optional descriptive links.
    /// </summary>
    [Fact]
    public void DecodeDoesNotValidateSkippedMetadataReference()
    {
        byte[] data = [.. TestFile.Create(TestImages.Heif.ParisIccExifXmpAvif).Bytes];
        InvalidateFirstItemReferenceSource(data, Heif4CharCode.Cdsc);
        DecoderOptions options = new()
        {
            SkipMetadata = true,
            SegmentIntegrityHandling = SegmentIntegrityHandling.Strict
        };

        using Image<Rgba32> image = Image.Load<Rgba32>(options, data);

        Assert.Null(image.Metadata.ExifProfile);
        Assert.Null(image.Metadata.XmpProfile);
        Assert.Null(image.Metadata.IccProfile);
    }

    [Fact]
    public void IdentifyIgnoresUnknownMetadataBox()
    {
        byte[] data = CreateEncodedContainer();
        int metaOffset = FindBoxOffset(data, Heif4CharCode.Meta, 0, data.Length);
        int metaSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(metaOffset));
        data = InsertBytes(data, metaOffset + metaSize, CreateUnknownBox());
        IncrementBoxSize(data, metaOffset, 8);

        ImageInfo imageInfo = Image.Identify(data);

        Assert.Equal(new Size(2, 3), imageInfo.Size);
    }

    [Fact]
    public void IdentifyIgnoresUnknownNonEssentialProperty()
    {
        byte[] data = CreateContainerWithUnknownProperty(false);

        ImageInfo imageInfo = Image.Identify(data);

        Assert.Equal(new Size(2, 3), imageInfo.Size);
    }

    [Fact]
    public void IdentifyRejectsUnknownEssentialProperty()
    {
        byte[] data = CreateContainerWithUnknownProperty(true);

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Image.Identify(data));

        Assert.Contains("essential", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IdentifyRejectsMalformedAncillaryPropertyInStrictMode()
    {
        byte[] data = CreateContainerWithProperty(CreateEmptyBox(Heif4CharCode.Pasp), false);
        DecoderOptions options = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };

        Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, data));
    }

    [Theory]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(SegmentIntegrityHandling.IgnoreImageData)]
    public void IdentifyIgnoresMalformedAncillaryPropertyWhenPermitted(SegmentIntegrityHandling handling)
    {
        byte[] data = CreateContainerWithProperty(CreateEmptyBox(Heif4CharCode.Pasp), false);
        DecoderOptions options = new() { SegmentIntegrityHandling = handling };

        ImageInfo imageInfo = Image.Identify(options, data);

        Assert.Equal(new Size(2, 3), imageInfo.Size);
        Assert.Equal(PixelResolutionUnit.PixelsPerInch, imageInfo.Metadata.ResolutionUnits);
        Assert.Equal(96D, imageInfo.Metadata.HorizontalResolution);
        Assert.Equal(96D, imageInfo.Metadata.VerticalResolution);
    }

    [Fact]
    public void IdentifyDoesNotValidateSkippedAncillaryPropertyMetadata()
    {
        byte[] data = CreateContainerWithProperty(CreateEmptyBox(Heif4CharCode.Pasp), false);
        DecoderOptions options = new()
        {
            SkipMetadata = true,
            SegmentIntegrityHandling = SegmentIntegrityHandling.Strict
        };

        ImageInfo imageInfo = Image.Identify(options, data);

        Assert.Equal(new Size(2, 3), imageInfo.Size);
    }

    [Theory]
    [InlineData(SegmentIntegrityHandling.Strict)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary)]
    public void IdentifyRejectsMalformedImagePropertyUnlessImageDataErrorsAreIgnored(SegmentIntegrityHandling handling)
    {
        byte[] data = CreateContainerWithProperty(CreateEmptyBox(Heif4CharCode.Irot), true);
        DecoderOptions options = new() { SegmentIntegrityHandling = handling };

        Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, data));
    }

    [Fact]
    public void IdentifyIgnoresMalformedImagePropertyWhenImageDataErrorsAreIgnored()
    {
        byte[] data = CreateContainerWithProperty(CreateEmptyBox(Heif4CharCode.Irot), true);
        DecoderOptions options = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.IgnoreImageData };

        ImageInfo imageInfo = Image.Identify(options, data);

        Assert.Equal(new Size(2, 3), imageInfo.Size);
    }

    [Fact]
    public void IdentifyRejectsDuplicateAncillaryPropertyAssociationInStrictMode()
    {
        byte[] data = CreateContainerWithDuplicatePropertyAssociation(CreatePixelAspectRatioBox(), false);
        DecoderOptions options = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };

        Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, data));
    }

    [Theory]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(SegmentIntegrityHandling.IgnoreImageData)]
    public void IdentifyIgnoresDuplicateAncillaryPropertyAssociationWhenPermitted(SegmentIntegrityHandling handling)
    {
        byte[] data = CreateContainerWithDuplicatePropertyAssociation(CreatePixelAspectRatioBox(), false);
        DecoderOptions options = new() { SegmentIntegrityHandling = handling };

        ImageInfo imageInfo = Image.Identify(options, data);

        Assert.Equal(new Size(2, 3), imageInfo.Size);
        Assert.Equal(PixelResolutionUnit.AspectRatio, imageInfo.Metadata.ResolutionUnits);
        Assert.Equal(1D, imageInfo.Metadata.HorizontalResolution);
        Assert.Equal(2D, imageInfo.Metadata.VerticalResolution);
    }

    [Theory]
    [InlineData(SegmentIntegrityHandling.Strict)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary)]
    public void IdentifyRejectsDuplicateImagePropertyAssociationUnlessImageDataErrorsAreIgnored(SegmentIntegrityHandling handling)
    {
        byte[] data = CreateContainerWithDuplicatePropertyAssociation(CreateRotationBox(), true);
        DecoderOptions options = new() { SegmentIntegrityHandling = handling };

        Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, data));
    }

    [Fact]
    public void IdentifyIgnoresDuplicateImagePropertyAssociationWhenImageDataErrorsAreIgnored()
    {
        byte[] data = CreateContainerWithDuplicatePropertyAssociation(CreateRotationBox(), true);
        DecoderOptions options = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.IgnoreImageData };

        ImageInfo imageInfo = Image.Identify(options, data);

        Assert.Equal(new Size(3, 2), imageInfo.Size);
    }

    [Theory]
    [InlineData(Heif4CharCode.Heic)]
    [InlineData(Heif4CharCode.Heix)]
    [InlineData(Heif4CharCode.Mif1)]
    [InlineData(Heif4CharCode.Avif)]
    [InlineData(Heif4CharCode.Jpeg)]
    public void DetectorRecognizesSupportedStillImageMajorBrand(Heif4CharCode brand)
    {
        byte[] data = CreateEncodedContainer();
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), (uint)brand);
        HeifImageFormatDetector detector = new();

        bool detected = detector.TryDetectFormat(data.AsSpan(0, detector.HeaderSize), out IImageFormat format);

        Assert.True(detected);
        Assert.Same(HeifFormat.Instance, format);
    }

    [Fact]
    public void IdentifyAcceptsSupportedCompatibleBrand()
    {
        byte[] data = CreateEncodedContainer();
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), UnknownBoxType);

        ImageInfo imageInfo = Image.Identify(data);

        Assert.Equal(new Size(2, 3), imageInfo.Size);
    }

    [Theory]
    [InlineData(Heif4CharCode.Hevc)]
    [InlineData(Heif4CharCode.Hevx)]
    [InlineData(Heif4CharCode.Avis)]
    public void DetectorRecognizesSupportedSequenceMajorBrand(Heif4CharCode brand)
    {
        byte[] data = CreateEncodedContainer();
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), (uint)brand);
        HeifImageFormatDetector detector = new();

        bool detected = detector.TryDetectFormat(data.AsSpan(0, detector.HeaderSize), out IImageFormat format);

        Assert.True(detected);
        Assert.Same(HeifFormat.Instance, format);
    }

    [Theory]
    [InlineData(Heif4CharCode.Hevm)]
    [InlineData(Heif4CharCode.Hevs)]
    [InlineData(Heif4CharCode.Jpgs)]
    public void DetectorRejectsUnsupportedSequenceMajorBrand(Heif4CharCode brand)
    {
        byte[] data = CreateEncodedContainer();
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), (uint)brand);
        HeifImageFormatDetector detector = new();

        Assert.False(detector.TryDetectFormat(data.AsSpan(0, detector.HeaderSize), out _));
    }

    [Fact]
    public void IdentifyRejectsUnsupportedBrands()
    {
        byte[] data = CreateEncodedContainer();
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), UnknownBoxType);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(16), UnknownBoxType);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(20), UnknownBoxType);
        using MemoryStream stream = new(data, false);

        Assert.Throws<ImageFormatException>(() => HeifDecoder.Instance.Identify(DecoderOptions.Default, stream));
    }

    [Fact]
    public void IdentifyAcceptsExtendedSizeTopLevelBox()
    {
        byte[] data = CreateEncodedContainer();
        byte[] box = new byte[16];
        BinaryPrimitives.WriteUInt32BigEndian(box, 1);
        BinaryPrimitives.WriteUInt32BigEndian(box.AsSpan(4), UnknownBoxType);
        BinaryPrimitives.WriteUInt64BigEndian(box.AsSpan(8), (ulong)box.Length);
        data = InsertBytes(data, data.Length, box);

        ImageInfo imageInfo = Image.Identify(data);

        Assert.Equal(new Size(2, 3), imageInfo.Size);
    }

    [Fact]
    public void IdentifyAcceptsUuidTopLevelBox()
    {
        byte[] data = CreateEncodedContainer();
        byte[] box = new byte[24];
        BinaryPrimitives.WriteUInt32BigEndian(box, (uint)box.Length);
        BinaryPrimitives.WriteUInt32BigEndian(box.AsSpan(4), (uint)Heif4CharCode.Uuid);
        data = InsertBytes(data, data.Length, box);

        ImageInfo imageInfo = Image.Identify(data);

        Assert.Equal(new Size(2, 3), imageInfo.Size);
    }

    [Fact]
    public void IdentifyAcceptsSizeZeroTopLevelBox()
    {
        byte[] data = CreateEncodedContainer();
        byte[] box = CreateUnknownBox();
        BinaryPrimitives.WriteUInt32BigEndian(box, 0);
        data = InsertBytes(data, data.Length, box);

        ImageInfo imageInfo = Image.Identify(data);

        Assert.Equal(new Size(2, 3), imageInfo.Size);
    }

    [Fact]
    public void IdentifyAcceptsExtendedSizeItemInfoEntry()
    {
        byte[] data = CreateEncodedContainer();
        int metaOffset = FindBoxOffset(data, Heif4CharCode.Meta, 0, data.Length);
        int metaSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(metaOffset));
        int iinfOffset = FindBoxOffset(data, Heif4CharCode.Iinf, metaOffset + 12, metaSize - 12);
        int infeOffset = iinfOffset + 14;
        uint infeSize = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(infeOffset));
        data = InsertBytes(data, infeOffset + 8, new byte[8]);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(infeOffset), 1);
        BinaryPrimitives.WriteUInt64BigEndian(data.AsSpan(infeOffset + 8), infeSize + 8);
        IncrementBoxSize(data, metaOffset, 8);
        IncrementBoxSize(data, iinfOffset, 8);

        ImageInfo imageInfo = Image.Identify(data);

        Assert.Equal(new Size(2, 3), imageInfo.Size);
    }

    [Fact]
    public void IdentifyRejectsSizeZeroMetadataChild()
    {
        byte[] data = CreateEncodedContainer();
        int metaOffset = FindBoxOffset(data, Heif4CharCode.Meta, 0, data.Length);
        int metaSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(metaOffset));
        byte[] box = CreateUnknownBox();
        BinaryPrimitives.WriteUInt32BigEndian(box, 0);
        data = InsertBytes(data, metaOffset + metaSize, box);
        IncrementBoxSize(data, metaOffset, box.Length);

        Assert.Throws<InvalidImageContentException>(() => Image.Identify(data));
    }

    [Fact]
    public void IdentifyRejectsMetadataChildBeyondParent()
    {
        byte[] data = CreateEncodedContainer();
        int metaOffset = FindBoxOffset(data, Heif4CharCode.Meta, 0, data.Length);
        int metaSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(metaOffset));
        byte[] box = CreateUnknownBox();
        BinaryPrimitives.WriteUInt32BigEndian(box, 16);
        data = InsertBytes(data, metaOffset + metaSize, box);
        IncrementBoxSize(data, metaOffset, box.Length);

        Assert.Throws<InvalidImageContentException>(() => Image.Identify(data));
    }

    [Fact]
    public void IdentifyRejectsItemInfoEntryBeyondParent()
    {
        byte[] data = CreateEncodedContainer();
        int metaOffset = FindBoxOffset(data, Heif4CharCode.Meta, 0, data.Length);
        int metaSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(metaOffset));
        int iinfOffset = FindBoxOffset(data, Heif4CharCode.Iinf, metaOffset + 12, metaSize - 12);
        uint iinfSize = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(iinfOffset));
        int infeOffset = iinfOffset + 14;
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(infeOffset), iinfSize);

        Assert.Throws<InvalidImageContentException>(() => Image.Identify(data));
    }

    [Fact]
    public void IdentifyRejectsBoxSmallerThanHeader()
    {
        byte[] data = CreateEncodedContainer();
        byte[] box = CreateUnknownBox();
        BinaryPrimitives.WriteUInt32BigEndian(box, 4);
        data = InsertBytes(data, data.Length, box);

        Assert.Throws<InvalidImageContentException>(() => Image.Identify(data));
    }

    [Fact]
    public void IdentifyRejectsTruncatedExtendedSizeHeader()
    {
        byte[] data = CreateEncodedContainer();
        byte[] box = new byte[12];
        BinaryPrimitives.WriteUInt32BigEndian(box, 1);
        BinaryPrimitives.WriteUInt32BigEndian(box.AsSpan(4), UnknownBoxType);
        data = InsertBytes(data, data.Length, box);

        Assert.Throws<InvalidImageContentException>(() => Image.Identify(data));
    }

    [Fact]
    public void IdentifyRejectsTruncatedUuidHeader()
    {
        byte[] data = CreateEncodedContainer();
        byte[] box = new byte[16];
        BinaryPrimitives.WriteUInt32BigEndian(box, 24);
        BinaryPrimitives.WriteUInt32BigEndian(box.AsSpan(4), (uint)Heif4CharCode.Uuid);
        data = InsertBytes(data, data.Length, box);

        Assert.Throws<InvalidImageContentException>(() => Image.Identify(data));
    }

    [Fact]
    public void IdentifyAcceptsItemPropertiesBeforeItemInfo()
    {
        byte[] data = CreateEncodedContainer();
        int metaOffset = FindBoxOffset(data, Heif4CharCode.Meta, 0, data.Length);
        int metaSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(metaOffset));
        int iinfOffset = FindBoxOffset(data, Heif4CharCode.Iinf, metaOffset + 12, metaSize - 12);
        int iprpOffset = FindBoxOffset(data, Heif4CharCode.Iprp, metaOffset + 12, metaSize - 12);
        int iprpSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(iprpOffset));
        data = MoveBoxBefore(data, iprpOffset, iprpSize, iinfOffset);

        ImageInfo imageInfo = Image.Identify(data);

        Assert.Equal(new Size(2, 3), imageInfo.Size);
    }

    [Fact]
    public void IdentifyAcceptsItemLocationBeforeItemInfo()
    {
        byte[] data = CreateEncodedContainer();
        int metaOffset = FindBoxOffset(data, Heif4CharCode.Meta, 0, data.Length);
        int metaSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(metaOffset));
        int iinfOffset = FindBoxOffset(data, Heif4CharCode.Iinf, metaOffset + 12, metaSize - 12);
        int ilocOffset = FindBoxOffset(data, Heif4CharCode.Iloc, metaOffset + 12, metaSize - 12);
        int ilocSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(ilocOffset));
        data = MoveBoxBefore(data, ilocOffset, ilocSize, iinfOffset);

        ImageInfo imageInfo = Image.Identify(data);

        Assert.Equal(new Size(2, 3), imageInfo.Size);
    }

    [Fact]
    public void IdentifyRejectsDuplicateUniqueMetadataBox()
    {
        byte[] data = CreateEncodedContainer();
        int metaOffset = FindBoxOffset(data, Heif4CharCode.Meta, 0, data.Length);
        int metaSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(metaOffset));
        int pitmOffset = FindBoxOffset(data, Heif4CharCode.Pitm, metaOffset + 12, metaSize - 12);
        int pitmSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(pitmOffset));
        data = InsertBytes(data, metaOffset + metaSize, data.AsSpan(pitmOffset, pitmSize));
        IncrementBoxSize(data, metaOffset, pitmSize);

        Assert.Throws<InvalidImageContentException>(() => Image.Identify(data));
    }

    private static byte[] CreateEncodedContainer()
    {
        using Image<Rgba32> image = new(2, 3);
        using MemoryStream stream = new();
        image.Save(stream, new HeifEncoder());
        return stream.ToArray();
    }

    private static byte[] CreateContainerWithUnknownProperty(bool essential)
        => CreateContainerWithProperty(CreateUnknownBox(), essential);

    private static byte[] CreateContainerWithProperty(ReadOnlySpan<byte> property, bool essential)
    {
        byte[] data = CreateEncodedContainer();
        int metaOffset = FindBoxOffset(data, Heif4CharCode.Meta, 0, data.Length);
        int metaSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(metaOffset));
        int iprpOffset = FindBoxOffset(data, Heif4CharCode.Iprp, metaOffset + 12, metaSize - 12);
        int iprpSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(iprpOffset));
        int ipcoOffset = FindBoxOffset(data, Heif4CharCode.Ipco, iprpOffset + 8, iprpSize - 8);
        int ipcoSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(ipcoOffset));
        int ipmaOffset = FindBoxOffset(data, Heif4CharCode.Ipma, iprpOffset + 8, iprpSize - 8);

        // Insert the property before ipma so its one-based index is 2 and all parent box sizes remain explicit.
        data = InsertBytes(data, ipcoOffset + ipcoSize, property);
        IncrementBoxSize(data, metaOffset, property.Length);
        IncrementBoxSize(data, iprpOffset, property.Length);
        IncrementBoxSize(data, ipcoOffset, property.Length);
        ipmaOffset += property.Length;

        // The generated container has one item with one property association; append the inserted property to that entry.
        int associationCountOffset = ipmaOffset + 18;
        data[associationCountOffset]++;
        int associationOffset = ipmaOffset + (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(ipmaOffset));
        byte association = (byte)(2 | (essential ? 0x80 : 0));
        data = InsertBytes(data, associationOffset, new byte[] { association });
        IncrementBoxSize(data, metaOffset, 1);
        IncrementBoxSize(data, iprpOffset, 1);
        IncrementBoxSize(data, ipmaOffset, 1);
        return data;
    }

    private static byte[] CreateContainerWithMalformedJpegMetadata()
    {
        byte[] data = CreateEncodedContainer();
        int metaOffset = FindBoxOffset(data, Heif4CharCode.Meta, 0, data.Length);
        int metaSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(metaOffset));
        int itemLocationOffset = FindBoxOffset(data, Heif4CharCode.Iloc, metaOffset + 12, metaSize - 12);
        int mediaDataOffset = FindBoxOffset(data, Heif4CharCode.Mdat, 0, data.Length);

        // The generated item uses one file-relative extent. Insert the malformed JPEG application segment after its
        // start-of-image marker, then update the enclosing media-data size and the exact declared extent length.
        data = InsertBytes(data, mediaDataOffset + 10, MalformedJpegApp13);
        IncrementBoxSize(data, mediaDataOffset, MalformedJpegApp13.Length);
        uint extentLength = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(itemLocationOffset + 32));
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(itemLocationOffset + 32), extentLength + (uint)MalformedJpegApp13.Length);
        return data;
    }

    private static byte[] CreateContainerWithDuplicatePropertyAssociation(ReadOnlySpan<byte> property, bool essential)
    {
        byte[] data = CreateContainerWithProperty(property, essential);
        int metaOffset = FindBoxOffset(data, Heif4CharCode.Meta, 0, data.Length);
        int metaSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(metaOffset));
        int iprpOffset = FindBoxOffset(data, Heif4CharCode.Iprp, metaOffset + 12, metaSize - 12);
        int iprpSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(iprpOffset));
        int ipmaOffset = FindBoxOffset(data, Heif4CharCode.Ipma, iprpOffset + 8, iprpSize - 8);
        int associationCountOffset = ipmaOffset + 18;

        // Repeat the inserted property's one-based index in the existing item entry without changing box structure.
        data[associationCountOffset]++;
        int associationOffset = ipmaOffset + (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(ipmaOffset));
        byte association = (byte)(2 | (essential ? 0x80 : 0));
        data = InsertBytes(data, associationOffset, new byte[] { association });
        IncrementBoxSize(data, metaOffset, 1);
        IncrementBoxSize(data, iprpOffset, 1);
        IncrementBoxSize(data, ipmaOffset, 1);
        return data;
    }

    private static byte[] CreateUnknownBox()
        => CreateEmptyBox((Heif4CharCode)UnknownBoxType);

    private static byte[] CreateEmptyBox(Heif4CharCode type)
        => CreateBox(type, []);

    private static byte[] CreatePixelAspectRatioBox()
    {
        byte[] payload = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(payload, 2);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4), 1);
        return CreateBox(Heif4CharCode.Pasp, payload);
    }

    private static byte[] CreateRotationBox() => CreateBox(Heif4CharCode.Irot, [1]);

    private static byte[] CreateBox(Heif4CharCode type, ReadOnlySpan<byte> payload)
    {
        byte[] box = new byte[8 + payload.Length];
        BinaryPrimitives.WriteUInt32BigEndian(box, (uint)box.Length);
        BinaryPrimitives.WriteUInt32BigEndian(box.AsSpan(4), (uint)type);
        payload.CopyTo(box.AsSpan(8));
        return box;
    }

    private static int FindBoxOffset(ReadOnlySpan<byte> data, Heif4CharCode type, int offset, int length)
    {
        int endOffset = offset + length;
        while (offset < endOffset)
        {
            int boxSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
            Heif4CharCode boxType = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(data[(offset + 4)..]);
            if (boxType == type)
            {
                return offset;
            }

            offset += boxSize;
        }

        return -1;
    }

    /// <summary>
    /// Reads the source item identifier from the first registered relationship of the requested type.
    /// </summary>
    /// <param name="data">The complete HEIF container.</param>
    /// <param name="referenceType">The item-reference child type.</param>
    /// <returns>The source item identifier.</returns>
    private static uint FindFirstItemReferenceSourceId(ReadOnlySpan<byte> data, Heif4CharCode referenceType)
    {
        int metaOffset = FindBoxOffset(data, Heif4CharCode.Meta, 0, data.Length);
        Assert.True(metaOffset >= 0);

        int metaSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data[metaOffset..]);
        int itemReferenceOffset = FindBoxOffset(data, Heif4CharCode.Iref, metaOffset + 12, metaSize - 12);
        Assert.True(itemReferenceOffset >= 0);

        int itemReferenceSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data[itemReferenceOffset..]);
        int relationshipOffset = FindBoxOffset(data, referenceType, itemReferenceOffset + 12, itemReferenceSize - 12);
        Assert.True(relationshipOffset >= 0);

        byte version = data[itemReferenceOffset + 8];

        Assert.InRange(version, (byte)0, (byte)1);

        return version == 0
            ? BinaryPrimitives.ReadUInt16BigEndian(data[(relationshipOffset + 8)..])
            : BinaryPrimitives.ReadUInt32BigEndian(data[(relationshipOffset + 8)..]);
    }

    /// <summary>
    /// Replaces the source item identifier of the first requested relationship with an undeclared value.
    /// </summary>
    /// <param name="data">The complete mutable HEIF container.</param>
    /// <param name="referenceType">The item-reference child type.</param>
    private static void InvalidateFirstItemReferenceSource(Span<byte> data, Heif4CharCode referenceType)
    {
        int metaOffset = FindBoxOffset(data, Heif4CharCode.Meta, 0, data.Length);
        Assert.True(metaOffset >= 0);

        int metaSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data[metaOffset..]);
        int itemReferenceOffset = FindBoxOffset(data, Heif4CharCode.Iref, metaOffset + 12, metaSize - 12);
        Assert.True(itemReferenceOffset >= 0);

        int itemReferenceSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data[itemReferenceOffset..]);
        int relationshipOffset = FindBoxOffset(data, referenceType, itemReferenceOffset + 12, itemReferenceSize - 12);
        Assert.True(relationshipOffset >= 0);

        byte version = data[itemReferenceOffset + 8];

        Assert.InRange(version, (byte)0, (byte)1);

        if (version == 0)
        {
            BinaryPrimitives.WriteUInt16BigEndian(data[(relationshipOffset + 8)..], ushort.MaxValue);
        }
        else
        {
            BinaryPrimitives.WriteUInt32BigEndian(data[(relationshipOffset + 8)..], uint.MaxValue);
        }
    }

    /// <summary>
    /// Clears every file-relative extent belonging to the requested item while retaining the container structure.
    /// </summary>
    /// <param name="data">The complete mutable HEIF container.</param>
    /// <param name="itemId">The item whose coded payload is cleared.</param>
    private static void ClearItemPayload(Span<byte> data, uint itemId)
    {
        int metaOffset = FindBoxOffset(data, Heif4CharCode.Meta, 0, data.Length);
        Assert.True(metaOffset >= 0);

        int metaSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data[metaOffset..]);
        int itemLocationOffset = FindBoxOffset(data, Heif4CharCode.Iloc, metaOffset + 12, metaSize - 12);
        Assert.True(itemLocationOffset >= 0);

        int offset = itemLocationOffset + 8;
        byte version = data[offset];
        offset += 4;

        int extentOffsetSize = data[offset] >> 4;
        int extentLengthSize = data[offset] & 0x0F;
        offset++;
        int baseOffsetSize = data[offset] >> 4;
        int extentIndexSize = version is 1 or 2 ? data[offset] & 0x0F : 0;
        offset++;

        uint itemCount = version == 2
            ? BinaryPrimitives.ReadUInt32BigEndian(data[offset..])
            : BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);

        offset += version == 2 ? 4 : 2;
        bool found = false;
        for (uint itemIndex = 0; itemIndex < itemCount; itemIndex++)
        {
            uint currentItemId = version == 2
                ? BinaryPrimitives.ReadUInt32BigEndian(data[offset..])
                : BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);

            offset += version == 2 ? 4 : 2;
            if (version is 1 or 2)
            {
                ushort constructionMethod = BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);
                Assert.Equal(0, constructionMethod & 0x0F);
                offset += 2;
            }

            // The data-reference index is zero for the self-contained image items used by the fixture.
            Assert.Equal(0, BinaryPrimitives.ReadUInt16BigEndian(data[offset..]));
            offset += 2;
            ulong baseOffset = ReadVariableUnsigned(data, baseOffsetSize, ref offset);
            int extentCount = BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);
            offset += 2;
            for (int extentIndex = 0; extentIndex < extentCount; extentIndex++)
            {
                _ = ReadVariableUnsigned(data, extentIndexSize, ref offset);
                ulong extentOffset = ReadVariableUnsigned(data, extentOffsetSize, ref offset);
                ulong extentLength = ReadVariableUnsigned(data, extentLengthSize, ref offset);
                if (currentItemId == itemId)
                {
                    data.Slice(checked((int)(baseOffset + extentOffset)), checked((int)extentLength)).Clear();
                    found = true;
                }
            }
        }

        Assert.True(found);
    }

    /// <summary>
    /// Reads one zero-width, 32-bit, or 64-bit unsigned item-location field.
    /// </summary>
    /// <param name="data">The complete HEIF container.</param>
    /// <param name="size">The field width in bytes.</param>
    /// <param name="offset">The current read offset, advanced past the field.</param>
    /// <returns>The decoded field value.</returns>
    private static ulong ReadVariableUnsigned(ReadOnlySpan<byte> data, int size, ref int offset)
    {
        ulong value = size switch
        {
            0 => 0,
            4 => BinaryPrimitives.ReadUInt32BigEndian(data[offset..]),
            8 => BinaryPrimitives.ReadUInt64BigEndian(data[offset..]),
            _ => throw new InvalidOperationException($"Unexpected item-location field width {size} in the test fixture.")
        };

        offset += size;
        return value;
    }

    /// <summary>
    /// Verifies that omitting an invalid alpha item preserves color channels and produces opaque output.
    /// </summary>
    /// <param name="expected">The image decoded with its valid alpha item.</param>
    /// <param name="actual">The image decoded after the alpha item or relationship was invalidated.</param>
    private static void AssertOpaqueRgbMatches(Image<Rgba32> expected, Image<Rgba32> actual)
    {
        Assert.False(actual.Metadata.GetHeifMetadata().HasAlpha);
        Assert.Equal(expected.Size, actual.Size);
        for (int y = 0; y < actual.Height; y++)
        {
            Span<Rgba32> expectedRow = expected.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            Span<Rgba32> actualRow = actual.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < actualRow.Length; x++)
            {
                Assert.Equal(expectedRow[x].R, actualRow[x].R);
                Assert.Equal(expectedRow[x].G, actualRow[x].G);
                Assert.Equal(expectedRow[x].B, actualRow[x].B);
                Assert.Equal(byte.MaxValue, actualRow[x].A);
            }
        }
    }

    private static byte[] InsertBytes(byte[] data, int offset, ReadOnlySpan<byte> inserted)
    {
        byte[] result = new byte[data.Length + inserted.Length];
        data.AsSpan(0, offset).CopyTo(result);
        inserted.CopyTo(result.AsSpan(offset));
        data.AsSpan(offset).CopyTo(result.AsSpan(offset + inserted.Length));
        return result;
    }

    private static byte[] MoveBoxBefore(byte[] data, int boxOffset, int boxSize, int beforeOffset)
    {
        byte[] result = new byte[data.Length];
        data.AsSpan(0, beforeOffset).CopyTo(result);
        data.AsSpan(boxOffset, boxSize).CopyTo(result.AsSpan(beforeOffset));
        data.AsSpan(beforeOffset, boxOffset - beforeOffset).CopyTo(result.AsSpan(beforeOffset + boxSize));
        data.AsSpan(boxOffset + boxSize).CopyTo(result.AsSpan(boxOffset + boxSize));
        return result;
    }

    private static void IncrementBoxSize(byte[] data, int offset, int increment)
    {
        uint size = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset));
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(offset), size + (uint)increment);
    }
}
