// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats.Heif;

[Trait("Format", "Heif")]
[ValidateDisposedMemoryAllocations]
public class HeifDecoderTests
{
    private const uint UnknownBoxType = 0x74657374U;

    /// <summary>
    /// Decodes the AVIF corpus through the public decoder and compares every frame with the independent decoder.
    /// </summary>
    [Theory]
    [WithFile(TestImages.Heif.IrvineAvif, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.Animated8BitWithAudio, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.Animated8BitWithAlphaExifXmp, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.Av1Deblocking8BitAvif, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.Av1Progressive8BitAvif, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.Av1ScaledReferenceSelectedLayerAvif, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.Av1AverageCompoundSequenceAvif, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.Av1WedgeCompoundSequenceAvif, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.Av1ObmcSequenceAvif, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.Av1LocalWarpSequenceAvif, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.Av1GlobalWarpSequenceAvif, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.Av1Cdef8BitAvif, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.Av1Profile8BitMonochromeAvif, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.Av1SuperResolution8BitAvif, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.Av1Restoration8BitAvif, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.Animated12BitWithKeyframes, PixelTypes.Rgba64)]
    [WithFile(TestImages.Heif.Av1Deblocking10BitAvif, PixelTypes.Rgba64)]
    [WithFile(TestImages.Heif.Av1Deblocking12BitAvif, PixelTypes.Rgba64)]
    [WithFile(TestImages.Heif.Av1Cdef10BitAvif, PixelTypes.Rgba64)]
    [WithFile(TestImages.Heif.Av1Cdef12BitAvif, PixelTypes.Rgba64)]
    [WithFile(TestImages.Heif.Av1Profile10Bit420Avif, PixelTypes.Rgba64)]
    [WithFile(TestImages.Heif.Av1Profile12Bit444Avif, PixelTypes.Rgba64)]
    [WithFile(TestImages.Heif.Av1Lossless10BitAvif, PixelTypes.Rgba64)]
    [WithFile(TestImages.Heif.Av1SuperResolution10BitAvif, PixelTypes.Rgba64)]
    [WithFile(TestImages.Heif.Av1SuperResolution12BitAvif, PixelTypes.Rgba64)]
    [WithFile(TestImages.Heif.Av1Restoration10BitAvif, PixelTypes.Rgba64)]
    [WithFile(TestImages.Heif.Av1Restoration12BitAvif, PixelTypes.Rgba64)]
    public void Decode<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new() { ColorProfileHandling = ColorProfileHandling.Preserve };
        using Image<TPixel> image = provider.GetImage(HeifDecoder.Instance, options);

        if (image.Frames.Count == 1)
        {
            image.DebugSave(provider, extension: "png", encoder: new PngEncoder());
            image.CompareToReferenceOutput(ImageComparer.Exact, provider);
        }
        else
        {
            image.DebugSaveMultiFrame(provider, encoder: new PngEncoder());
            image.CompareToReferenceOutputMultiFrame(provider, ImageComparer.Exact);
        }
    }

    [Fact]
    public void DecodeStillAndBoundedSequenceFromSupportedStream()
    {
        DecodeStillAndBoundedSequenceFromSupportedStreamCase(TestImages.Heif.Orange4x4, DecoderStreamKind.NonSeekable, 1, 4, 4);
        DecodeStillAndBoundedSequenceFromSupportedStreamCase(TestImages.Heif.Orange4x4, DecoderStreamKind.ShortRead, 1, 4, 4);
        DecodeStillAndBoundedSequenceFromSupportedStreamCase(TestImages.Heif.Animated8Bit, DecoderStreamKind.NonSeekable, 5, 150, 150);
        DecodeStillAndBoundedSequenceFromSupportedStreamCase(TestImages.Heif.Animated8Bit, DecoderStreamKind.ShortRead, 5, 150, 150);
    }

    private static void DecodeStillAndBoundedSequenceFromSupportedStreamCase(
        string imagePath,
        DecoderStreamKind streamKind,
        int expectedFrameCount,
        int expectedWidth,
        int expectedHeight)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using Image<Rgba32> expected = Image.Load<Rgba32>(testFile.Bytes);
        using Stream stream = streamKind switch
        {
            DecoderStreamKind.NonSeekable => new NonSeekableStream(new MemoryStream(testFile.Bytes, false)),
            DecoderStreamKind.ShortRead => new ShortReadMemoryStream(testFile.Bytes),
            _ => throw new InvalidOperationException()
        };

        using Image<Rgba32> actual = Image.Load<Rgba32>(stream);

        Assert.Equal(new Size(expectedWidth, expectedHeight), actual.Size);
        Assert.Equal(expectedFrameCount, actual.Frames.Count);
        Assert.Equal(expected.Frames.Count, actual.Frames.Count);
        for (int frameIndex = 0; frameIndex < actual.Frames.Count; frameIndex++)
        {
            for (int y = 0; y < actual.Height; y++)
            {
                Assert.True(
                    expected.Frames[frameIndex].PixelBuffer.DangerousGetRowSpan(y)
                        .SequenceEqual(actual.Frames[frameIndex].PixelBuffer.DangerousGetRowSpan(y)));
            }
        }
    }

    [Theory]
    [InlineData(TestImages.Heif.Animated8Bit, 5, 150, 150)]
    public void DecodeFromCurrentStreamPosition(
        string imagePath,
        int expectedFrameCount,
        int expectedWidth,
        int expectedHeight)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new();
        stream.Write([1, 2, 3, 4]);
        long fileStart = stream.Position;
        stream.Write(testFile.Bytes);
        stream.Position = fileStart;

        using Image<Rgba32> image = Image.Load<Rgba32>(stream);

        Assert.Equal(new Size(expectedWidth, expectedHeight), image.Size);
        Assert.Equal(expectedFrameCount, image.Frames.Count);
    }

    /// <summary>
    /// Verifies that AVIF decoding preserves the exact embedded ICC profile bytes.
    /// </summary>
    [Theory]
    [WithFile(TestImages.Heif.ParisIccExifXmpAvif, PixelTypes.Rgba32)]
    public void Decode_WhenColorProfileHandlingIsPreserve_PreservesIccProfileBytes<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions preserveOptions = new() { ColorProfileHandling = ColorProfileHandling.Preserve };

        using Image<TPixel> preserved = provider.GetImage(HeifDecoder.Instance, preserveOptions);
        using Image<TPixel> expectedPreserved = Image.Load<TPixel>(preserveOptions, TestFile.Create(TestImages.Heif.ParisIccExifXmpPng).Bytes);

        Assert.NotNull(preserved.Metadata.IccProfile);
        Assert.NotNull(expectedPreserved.Metadata.IccProfile);
        Assert.Equal(expectedPreserved.Metadata.IccProfile.ToByteArray(), preserved.Metadata.IccProfile.ToByteArray());
        preserved.DebugSave(provider, extension: "png", encoder: new PngEncoder());
        preserved.CompareToReferenceOutput(ImageComparer.Exact, provider);
    }

    /// <summary>
    /// Verifies that AVIF decoding converts pixels from an embedded non-sRGB ICC profile to sRGB.
    /// </summary>
    [Theory]
    [WithFile(TestImages.Heif.PerceptualIccAvif, PixelTypes.Rgba32)]
    public void Decode_WhenColorProfileHandlingIsConvert_ApplyIccProfile<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions preserveOptions = new() { ColorProfileHandling = ColorProfileHandling.Preserve };
        DecoderOptions convertOptions = new() { ColorProfileHandling = ColorProfileHandling.Convert };

        using Image<TPixel> preserved = provider.GetImage(HeifDecoder.Instance, preserveOptions);
        using Image<TPixel> converted = provider.GetImage(HeifDecoder.Instance, convertOptions);

        Assert.NotNull(preserved.Metadata.IccProfile);
        Assert.Null(converted.Metadata.IccProfile);
        Assert.NotEmpty(ImageComparer.Exact.CompareImages(preserved, converted));

        converted.DebugSave(provider, extension: "png", encoder: new PngEncoder());
        converted.CompareToReferenceOutput(ImageComparer.Exact, provider);
    }

    /// <summary>
    /// Verifies that AVIF grid composition retains and converts the presented image's non-sRGB ICC profile.
    /// </summary>
    [Theory]
    [WithFile(TestImages.Heif.PerceptualIccGridAvif, PixelTypes.Rgba32)]
    public void Decode_WhenColorProfileHandlingIsConvert_ApplyIccProfile_Grid<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions preserveOptions = new() { ColorProfileHandling = ColorProfileHandling.Preserve };
        DecoderOptions convertOptions = new() { ColorProfileHandling = ColorProfileHandling.Convert };

        using Image<TPixel> preserved = provider.GetImage(HeifDecoder.Instance, preserveOptions);
        using Image<TPixel> converted = provider.GetImage(HeifDecoder.Instance, convertOptions);
        using Image<TPixel> expectedPreserved = Image.Load<TPixel>(preserveOptions, TestFile.Create(TestImages.Png.Icc.Perceptual).Bytes);

        IccProfile preservedIccProfile = Assert.IsType<IccProfile>(preserved.Metadata.IccProfile);
        IccProfile expectedIccProfile = Assert.IsType<IccProfile>(expectedPreserved.Metadata.IccProfile);
        Assert.Null(converted.Metadata.IccProfile);
        Assert.Equal(expectedIccProfile.ToByteArray(), preservedIccProfile.ToByteArray());
        Assert.NotEmpty(ImageComparer.Exact.CompareImages(preserved, converted));
        converted.DebugSave(provider, extension: "png", encoder: new PngEncoder());
        converted.CompareToReferenceOutput(ImageComparer.Exact, provider);
    }

    /// <summary>
    /// Verifies that AVIF sequence ICC conversion is applied to every presented frame.
    /// </summary>
    [Theory]
    [WithFile(TestImages.Heif.PerceptualIccSequenceAvif, PixelTypes.Rgba32)]
    public void Decode_WhenColorProfileHandlingIsConvert_ApplyIccProfile_Sequence<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions preserveOptions = new() { ColorProfileHandling = ColorProfileHandling.Preserve };
        DecoderOptions convertOptions = new() { ColorProfileHandling = ColorProfileHandling.Convert };

        using Image<TPixel> preserved = provider.GetImage(HeifDecoder.Instance, preserveOptions);
        using Image<TPixel> converted = provider.GetImage(HeifDecoder.Instance, convertOptions);
        using Image<TPixel> expectedPreserved = Image.Load<TPixel>(preserveOptions, TestFile.Create(TestImages.Png.Icc.Perceptual).Bytes);

        Assert.Equal(2, preserved.Frames.Count);
        Assert.Equal(preserved.Frames.Count, converted.Frames.Count);
        IccProfile preservedIccProfile = Assert.IsType<IccProfile>(preserved.Metadata.IccProfile);
        IccProfile expectedIccProfile = Assert.IsType<IccProfile>(expectedPreserved.Metadata.IccProfile);
        Assert.Null(converted.Metadata.IccProfile);
        Assert.Equal(expectedIccProfile.ToByteArray(), preservedIccProfile.ToByteArray());

        converted.DebugSaveMultiFrame(provider, encoder: new PngEncoder());
        converted.CompareToReferenceOutputMultiFrame(provider, ImageComparer.Exact);
    }

    /// <summary>
    /// Verifies that compact profile handling removes a canonical sRGB ICC profile without changing pixels.
    /// </summary>
    [Theory]
    [WithFile(TestImages.Heif.ParisIccExifXmpAvif, PixelTypes.Rgba32)]
    public void Decode_WhenColorProfileHandlingIsCompact_RemovesSrgbIccProfile<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions preserveOptions = new() { ColorProfileHandling = ColorProfileHandling.Preserve };
        DecoderOptions compactOptions = new() { ColorProfileHandling = ColorProfileHandling.Compact };

        using Image<TPixel> preserved = provider.GetImage(HeifDecoder.Instance, preserveOptions);
        using Image<TPixel> compact = provider.GetImage(HeifDecoder.Instance, compactOptions);

        Assert.NotNull(preserved.Metadata.IccProfile);
        Assert.Null(compact.Metadata.IccProfile);
        Assert.Empty(ImageComparer.Exact.CompareImages(preserved, compact));
        compact.DebugSave(provider, extension: "png", encoder: new PngEncoder());
        compact.CompareToReferenceOutput(ImageComparer.Exact, provider);
    }

    /// <summary>
    /// Verifies that metadata skipping omits the embedded AVIF ICC profile.
    /// </summary>
    [Theory]
    [WithFile(TestImages.Heif.ParisIccExifXmpAvif, PixelTypes.Rgba32)]
    public void Decode_WhenSkipMetadataIsTrue_OmitsIccProfile<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new()
        {
            ColorProfileHandling = ColorProfileHandling.Preserve,
            SkipMetadata = true
        };

        using Image<TPixel> image = provider.GetImage(HeifDecoder.Instance, options);

        Assert.Null(image.Metadata.IccProfile);
        if (image.Frames.Count == 1)
        {
            image.DebugSave(provider, extension: "png", encoder: new PngEncoder());
            image.CompareToReferenceOutput(ImageComparer.Exact, provider);
        }
        else
        {
            image.DebugSaveMultiFrame(provider, encoder: new PngEncoder());
            image.CompareToReferenceOutputMultiFrame(provider, ImageComparer.Exact);
        }
    }

    [Fact]
    public void DecodeIgnoresUnknownTopLevelBox()
    {
        byte[] data = CreateAv1Container();
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
        source.Save(stream, new HeifEncoder { Speed = HeifEncodingSpeed.Level9 });
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

    /// <summary>
    /// Verifies that invalid optional alpha payloads remain fatal when image-data errors cannot be ignored.
    /// </summary>
    [Theory]
    [InlineData(SegmentIntegrityHandling.Strict)]
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
    [InlineData(Heif4CharCode.Avif)]
    [InlineData(Heif4CharCode.Avis)]
    public void DetectorRecognizesSupportedMajorBrand(Heif4CharCode brand)
    {
        byte[] data = CreateAv1Container();
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), (uint)brand);
        HeifImageFormatDetector detector = new();

        bool detected = detector.TryDetectFormat(data.AsSpan(0, detector.HeaderSize), out IImageFormat format);

        Assert.True(detected);
        Assert.Same(HeifFormat.Instance, format);
    }

    [Fact]
    public void IdentifyRejectsUnsupportedBrands()
    {
        byte[] data = CreateAv1Container();
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), UnknownBoxType);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(16), UnknownBoxType);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(20), UnknownBoxType);
        using MemoryStream stream = new(data, false);

        Assert.Throws<ImageFormatException>(() => HeifDecoder.Instance.Identify(DecoderOptions.Default, stream));
    }

    [Fact]
    public void IdentifyRejectsMetadataChildBeyondParent()
    {
        byte[] data = CreateAv1Container();
        int metaOffset = FindBoxOffset(data, Heif4CharCode.Meta, 0, data.Length);
        int metaSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(metaOffset));
        byte[] box = CreateUnknownBox();
        BinaryPrimitives.WriteUInt32BigEndian(box, 16);
        data = InsertBytes(data, metaOffset + metaSize, box);
        IncrementBoxSize(data, metaOffset, box.Length);

        Assert.Throws<InvalidImageContentException>(() => Image.Identify(data));
    }

    [Fact]
    public void IdentifyRejectsBoxSmallerThanHeader()
    {
        byte[] data = CreateAv1Container();
        byte[] box = CreateUnknownBox();
        BinaryPrimitives.WriteUInt32BigEndian(box, 4);
        data = InsertBytes(data, data.Length, box);

        Assert.Throws<InvalidImageContentException>(() => Image.Identify(data));
    }

    [Fact]
    public void IdentifyRejectsDuplicateUniqueMetadataBox()
    {
        byte[] data = CreateAv1Container();
        int metaOffset = FindBoxOffset(data, Heif4CharCode.Meta, 0, data.Length);
        int metaSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(metaOffset));
        int pitmOffset = FindBoxOffset(data, Heif4CharCode.Pitm, metaOffset + 12, metaSize - 12);
        int pitmSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(pitmOffset));
        data = InsertBytes(data, metaOffset + metaSize, data.AsSpan(pitmOffset, pitmSize));
        IncrementBoxSize(data, metaOffset, pitmSize);

        Assert.Throws<InvalidImageContentException>(() => Image.Identify(data));
    }

    private static byte[] CreateAv1Container()
    {
        using Image<Rgb24> image = new(2, 3);
        using MemoryStream stream = new();
        image.Save(stream, new HeifEncoder());
        return stream.ToArray();
    }

    private static byte[] CreateContainerWithUnknownProperty(bool essential)
        => CreateContainerWithProperty(CreateUnknownBox(), essential);

    private static byte[] CreateContainerWithProperty(ReadOnlySpan<byte> property, bool essential)
    {
        byte[] data = CreateAv1Container();
        int metaOffset = FindBoxOffset(data, Heif4CharCode.Meta, 0, data.Length);
        int metaSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(metaOffset));
        int iprpOffset = FindBoxOffset(data, Heif4CharCode.Iprp, metaOffset + 12, metaSize - 12);
        int iprpSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(iprpOffset));
        int ipcoOffset = FindBoxOffset(data, Heif4CharCode.Ipco, iprpOffset + 8, iprpSize - 8);
        int ipcoSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(ipcoOffset));
        int ipmaOffset = FindBoxOffset(data, Heif4CharCode.Ipma, iprpOffset + 8, iprpSize - 8);

        // Count existing AV1 properties before appending the test property; its association is one-based.
        int propertyIndex = 1;
        for (int offset = ipcoOffset + 8; offset < ipcoOffset + ipcoSize;)
        {
            offset += (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset));
            propertyIndex++;
        }

        // Insert before ipma and update each enclosing box size.
        data = InsertBytes(data, ipcoOffset + ipcoSize, property);
        IncrementBoxSize(data, metaOffset, property.Length);
        IncrementBoxSize(data, iprpOffset, property.Length);
        IncrementBoxSize(data, ipcoOffset, property.Length);
        ipmaOffset += property.Length;

        // Append the new property association to the sole opaque AV1 item's existing entry.
        int associationCountOffset = ipmaOffset + 18;
        data[associationCountOffset]++;
        int associationOffset = ipmaOffset + (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(ipmaOffset));
        byte association = (byte)(propertyIndex | (essential ? 0x80 : 0));
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
        foreach (Range extent in GetItemPayloadRanges(data, itemId))
        {
            data[extent].Clear();
        }
    }

    /// <summary>
    /// Locates every file-relative extent for an item without decoding its contents.
    /// </summary>
    /// <param name="data">The complete HEIF container.</param>
    /// <param name="itemId">The item whose extents are selected.</param>
    /// <returns>The item's extents in file order.</returns>
    private static List<Range> GetItemPayloadRanges(ReadOnlySpan<byte> data, uint itemId)
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
        List<Range> extents = new();

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
                    int start = checked((int)(baseOffset + extentOffset));
                    extents.Add(new Range(start, checked(start + (int)extentLength)));
                }
            }
        }

        Assert.NotEmpty(extents);
        return extents;
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

    private static void IncrementBoxSize(byte[] data, int offset, int increment)
    {
        uint size = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset));
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(offset), size + (uint)increment);
    }

    public enum DecoderStreamKind
    {
        NonSeekable,
        ShortRead
    }

    private sealed class ShortReadMemoryStream : MemoryStream
    {
        private const int MaximumReadLength = 3;

        public ShortReadMemoryStream(byte[] data)
            : base(data, false)
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
            => base.Read(buffer, offset, Math.Min(count, MaximumReadLength));

        public override int Read(Span<byte> buffer)
            => base.Read(buffer[..Math.Min(buffer.Length, MaximumReadLength)]);
    }
}
