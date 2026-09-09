// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Color;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestDataIcc;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

namespace SixLabors.ImageSharp.Tests.Formats.Heif;

[Trait("Format", "Heif")]
[ValidateDisposedMemoryAllocations]
public class HeifEncoderTests
{
    private const int Av1EightBit = (int)Av1BitDepth.EightBit;
    private const int Av1TenBit = (int)Av1BitDepth.TenBit;
    private const int Av1TwelveBit = (int)Av1BitDepth.TwelveBit;
    private const int Yuv400 = (int)Av1ColorFormat.Yuv400;
    private const int Yuv420 = (int)Av1ColorFormat.Yuv420;
    private const int Yuv422 = (int)Av1ColorFormat.Yuv422;
    private const int Yuv444 = (int)Av1ColorFormat.Yuv444;

    [Fact]
    public void OptionsHaveExpectedDefaults()
    {
        HeifEncoder encoder = new();

        Assert.Null(encoder.Quality);
        Assert.Null(encoder.AlphaQuality);
        Assert.Equal(5, encoder.Effort);
        Assert.False(encoder.Lossless);
        Assert.Null(encoder.BitDepth);
        Assert.Null(encoder.ChromaSubsampling);
        Assert.Null(encoder.RepeatCount);
        Assert.True(encoder.AnimateRootFrame);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void QualityOutsideRangeThrows(int quality)
        => Assert.Throws<ArgumentException>(() => new HeifEncoder { Quality = quality });

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void AlphaQualityOutsideRangeThrows(int quality)
        => Assert.Throws<ArgumentException>(() => new HeifEncoder { AlphaQuality = quality });

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void EffortOutsideRangeThrows(int effort)
        => Assert.Throws<ArgumentException>(() => new HeifEncoder { Effort = effort });

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(100, 100, 10)]
    public void OptionRangeBoundariesAreAccepted(int quality, int alphaQuality, int effort)
    {
        HeifEncoder encoder = new()
        {
            Quality = quality,
            AlphaQuality = alphaQuality,
            Effort = effort
        };

        Assert.Equal(quality, encoder.Quality);
        Assert.Equal(alphaQuality, encoder.AlphaQuality);
        Assert.Equal(effort, encoder.Effort);
    }

    [Fact]
    public void Av1ImageSequencePreservesSeparateRootFrame()
    {
        const int width = 8;
        const int height = 8;
        using Image<Rgb24> image = new(width, height);
        for (int row = 0; row < height; row++)
        {
            image.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(row).Fill(new Rgb24(255, 255, 255));
        }

        image.Frames.AddFrame(image.Frames.RootFrame);
        for (int row = 0; row < height; row++)
        {
            image.Frames[1].PixelBuffer.DangerousGetRowSpan(row).Fill(new Rgb24(0, 0, 0));
        }

        image.Frames[1].Metadata.GetHeifMetadata().FrameDelay = new Rational(1, 20);
        using MemoryStream stream = new();
        HeifEncoder encoder = new()
        {
            AnimateRootFrame = false,
            Lossless = true,
            Effort = 0
        };

        image.Save(stream, encoder);
        byte[] file = stream.ToArray();
        Span<byte> fileType = GetTopLevelBox(file, Heif4CharCode.Ftyp);
        Assert.Equal(Heif4CharCode.Miaf, (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(fileType[^sizeof(uint)..]));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(GetMetadataChild(file, Heif4CharCode.Pitm)[12..]));

        stream.Position = 0;
        using Image<Rgb24> decoded = Image.Load<Rgb24>(stream);
        Assert.Equal(2, decoded.Frames.Count);
        Assert.False(decoded.Metadata.GetHeifMetadata().AnimateRootFrame);
        Assert.Empty(ImageComparer.Exact.CompareImages(image, decoded));
        Assert.Equal(
            image.Frames[1].Metadata.GetHeifMetadata().FrameDelay,
            decoded.Frames[1].Metadata.GetHeifMetadata().FrameDelay);
    }

    [Fact]
    public void GridDecoderAcceptsSmallerRightAndBottomColorAndAlphaCells()
    {
        const int tileWidth = 64;
        const int tileHeight = 64;
        const int outputWidth = 96;
        const int outputHeight = 96;
        Size[] tileSizes =
        [
            new(tileWidth, tileHeight),
            new(outputWidth - tileWidth, tileHeight),
            new(tileWidth, outputHeight - tileHeight),
            new(outputWidth - tileWidth, outputHeight - tileHeight)
        ];

        Rgba32[] tileColors =
        [
            new(32, 32, 32),
            new(64, 64, 64),
            new(96, 96, 96),
            new(128, 128, 128)
        ];

        ObuColorConfig colorConfig = new()
        {
            IsColorDescriptionPresent = true,
            ColorPrimaries = ObuColorPrimaries.Bt709,
            TransferCharacteristics = ObuTransferCharacteristics.Srgb,
            MatrixCoefficients = ObuMatrixCoefficients.Bt709,
            ColorRange = true,
            IsMonochrome = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.EightBit
        };

        List<HeifItem> items = [];
        Dictionary<uint, byte[]> payloads = [];
        HeifItem gridItem = new(Heif4CharCode.Grid, 1);
        gridItem.SetExtent(new Size(outputWidth, outputHeight));
        items.Add(gridItem);
        HeifItemLink gridLink = new(Heif4CharCode.Dimg, gridItem.Id);
        for (int tileIndex = 0; tileIndex < tileSizes.Length; tileIndex++)
        {
            Size tileSize = tileSizes[tileIndex];
            using Image<Rgba32> tile = new(tileSize.Width, tileSize.Height, tileColors[tileIndex]);
            using MemoryStream payload = new();
            ObuSequenceHeader header = Av1FrameEncoder.Encode(
                Configuration.Default,
                tile.Frames.RootFrame,
                payload,
                colorConfig,
                qIndex: 0,
                effort: 0);

            uint itemId = (uint)tileIndex + 2;
            HeifItem tileItem = new(Heif4CharCode.Av01, itemId)
            {
                Av1CodecConfiguration = new Av1CodecConfiguration(header)
            };

            tileItem.SetExtent(tileSize);
            items.Add(tileItem);
            gridLink.DestinationIds.Add(itemId);
            payloads.Add(itemId, payload.ToArray());
        }

        List<HeifItemLink> links = [gridLink];
        GridHeifItemDecoder<Rgba32> decoder = new(items, links, ReadItem);
        Span<byte> descriptor = [0, 0, 1, 1, 0, outputWidth, 0, outputHeight];
        using Image<Rgba32> result = new(outputWidth, outputHeight);
        decoder.DecodeItemData(
            new DecoderOptions { Configuration = Configuration.Default },
            HeifChromaUpsampling.Auto,
            gridItem,
            descriptor,
            null,
            null,
            null,
            default,
            default,
            false,
            result.Bounds,
            default,
            result.Frames.RootFrame.PixelBuffer.GetRegion(result.Bounds),
            result.Metadata,
            TestContext.Current.CancellationToken);

        for (int y = 0; y < outputHeight; y++)
        {
            for (int x = 0; x < outputWidth; x++)
            {
                int tileIndex = (y < tileHeight ? 0 : 2) + (x < tileWidth ? 0 : 1);
                Assert.Equal(tileColors[tileIndex], result[x, y]);
            }
        }

        Rgba32 opaqueColor = new(7, 11, 13);
        using Image<Rgba32> alphaResult = new(outputWidth, outputHeight, opaqueColor);
        using Av1FrameBuffer<byte> alphaFrame = decoder.DecodeAlphaItemData(
            new DecoderOptions { Configuration = Configuration.Default },
            gridItem,
            descriptor,
            TestContext.Current.CancellationToken);

        Av1YuvConverter.ComposeAlpha(
            Configuration.Default,
            alphaFrame,
            alphaResult.Frames.RootFrame.PixelBuffer.GetRegion(alphaResult.Bounds),
            alphaResult.Size,
            new Rectangle(Point.Empty, alphaResult.Size),
            false,
            default);

        for (int y = 0; y < outputHeight; y++)
        {
            for (int x = 0; x < outputWidth; x++)
            {
                int tileIndex = (y < tileHeight ? 0 : 2) + (x < tileWidth ? 0 : 1);
                Rgba32 expected = opaqueColor;
                expected.A = tileColors[tileIndex].R;
                Assert.Equal(expected, alphaResult[x, y]);
            }
        }

        IMemoryOwner<byte> ReadItem(HeifItem item)
        {
            byte[] payload = payloads[item.Id];
            IMemoryOwner<byte> owner = Configuration.Default.MemoryAllocator.Allocate<byte>(payload.Length);
            payload.CopyTo(owner.Memory.Span);
            return owner;
        }
    }

    [Fact]
    public void GridDecoderRejectsCellSmallerThanMiafMinimum()
    {
        ObuColorConfig colorConfig = new()
        {
            IsColorDescriptionPresent = true,
            ColorPrimaries = ObuColorPrimaries.Bt709,
            TransferCharacteristics = ObuTransferCharacteristics.Srgb,
            MatrixCoefficients = ObuMatrixCoefficients.Bt709,
            ColorRange = true,
            IsMonochrome = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.EightBit
        };

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(
            () =>
            {
                using Image<Rgba32> decoded = DecodeSingleCellGrid(63, 64, colorConfig);
            });

        Assert.Contains("grid cells must be at least 64 samples", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies grid dimensions preserve chroma alignment for AV1's 4:2:2 and 4:2:0 layouts.
    /// </summary>
    [Theory]
    [InlineData(65, 64, true, false)]
    [InlineData(65, 64, true, true)]
    [InlineData(64, 65, true, true)]
    public void GridDecoderRejectsOddSubsampledDimension(
        int width,
        int height,
        bool subsamplingX,
        bool subsamplingY)
    {
        ObuColorConfig colorConfig = new()
        {
            IsColorDescriptionPresent = true,
            ColorPrimaries = ObuColorPrimaries.Bt709,
            TransferCharacteristics = ObuTransferCharacteristics.Srgb,
            MatrixCoefficients = ObuMatrixCoefficients.Bt709,
            ColorRange = true,
            BitDepth = Av1BitDepth.EightBit,
            SubSamplingX = subsamplingX,
            SubSamplingY = subsamplingY
        };

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(
            () =>
            {
                using Image<Rgba32> decoded = DecodeSingleCellGrid(width, height, colorConfig);
            });

        Assert.Contains("must be even", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(HeifChromaSubsampling.Yuv420)]
    [InlineData(HeifChromaSubsampling.Yuv422)]
    [InlineData(HeifChromaSubsampling.Yuv444)]
    public void Av1OversizedStillImageWritesAndDecodesGrid(HeifChromaSubsampling? chromaSubsampling)
    {
        const int width = 65537;
        using Image<Rgb24> image = new(width, 1);
        image[0, 0] = new Rgb24(1, 2, 3);
        image[32768, 0] = new Rgb24(11, 13, 17);
        image[32769, 0] = new Rgb24(19, 23, 29);
        image[width - 1, 0] = new Rgb24(31, 37, 41);

        // Identity-matrix 4:4:4 makes the lossless AV1 cells preserve the packed RGB channels exactly.
        CicpProfile sourceProfile = new(1, 13, 0, true);
        image.Metadata.CicpProfile = sourceProfile;
        using MemoryStream stream = new();
        HeifEncoder encoder = new()
        {
            Lossless = true,
            ChromaSubsampling = chromaSubsampling,
            Effort = 0
        };

        image.Save(stream, encoder);
        byte[] file = stream.ToArray();
        Assert.Equal(
            [0, 1, 0, 1, 0, 1, 0, 1, 0, 0, 0, 1],
            GetItemPayload(file, 1).ToArray());

        using Av1Decoder firstCellDecoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> firstCellPlanes = firstCellDecoder.DecodeFrameBuffer(GetItemPayload(file, 2), null, null, out _);
        using Image<Rgb24> firstCell = new(Configuration.Default, firstCellPlanes.Width, firstCellPlanes.Height);
        Av1YuvConverter.ConvertToRgb(
            Configuration.Default,
            firstCellPlanes,
            firstCell.Bounds,
            firstCell.Frames.RootFrame.PixelBuffer.GetRegion(firstCell.Bounds),
            firstCell.Size,
            default,
            null,
            null,
            default,
            default,
            false,
            HeifChromaUpsampling.Auto,
            firstCellPlanes.ColorConfig.ColorRange);

        using Av1Decoder secondCellDecoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> secondCellPlanes = secondCellDecoder.DecodeFrameBuffer(GetItemPayload(file, 3), null, null, out _);
        using Image<Rgb24> secondCell = new(Configuration.Default, secondCellPlanes.Width, secondCellPlanes.Height);
        Av1YuvConverter.ConvertToRgb(
            Configuration.Default,
            secondCellPlanes,
            secondCell.Bounds,
            secondCell.Frames.RootFrame.PixelBuffer.GetRegion(secondCell.Bounds),
            secondCell.Size,
            default,
            null,
            null,
            default,
            default,
            false,
            HeifChromaUpsampling.Auto,
            secondCellPlanes.ColorConfig.ColorRange);

        // Odd grid dimensions require full-resolution chroma, including when subsampling was explicitly requested.
        // The conversion must retain the source profile and signal the resolved sampling on every coded cell.
        ObuSequenceHeader firstHeader = Assert.IsType<ObuSequenceHeader>(firstCellDecoder.SequenceHeader);
        ObuSequenceHeader secondHeader = Assert.IsType<ObuSequenceHeader>(secondCellDecoder.SequenceHeader);
        Assert.False(firstHeader.ColorConfig.SubSamplingX);
        Assert.False(firstHeader.ColorConfig.SubSamplingY);
        Assert.False(secondHeader.ColorConfig.SubSamplingX);
        Assert.False(secondHeader.ColorConfig.SubSamplingY);
        Assert.Equal(ObuMatrixCoefficients.Identity, firstHeader.ColorConfig.MatrixCoefficients);
        Assert.Equal(ObuMatrixCoefficients.Identity, secondHeader.ColorConfig.MatrixCoefficients);
        Assert.Same(sourceProfile, image.Metadata.CicpProfile);

        // AVIF requires the first grid cell to be at least 64 samples on both axes. The derived grid trims
        // the replicated right and bottom edges back to the presentation encoded in its descriptor.
        Assert.Equal(new Size(32769, 64), firstCell.Size);
        Assert.Equal(new Size(32769, 64), secondCell.Size);
        Assert.Equal(image[32768, 0], firstCell[32768, 0]);
        Assert.Equal(image[32769, 0], secondCell[0, 0]);
        Assert.Equal(image[width - 1, 0], secondCell[32767, 0]);
        Assert.Equal(secondCell[32767, 0], secondCell[32768, 0]);
        Assert.Equal(secondCell[0, 0], secondCell[0, 63]);
        Assert.Equal(1U, GetItemInfoFlags(file, 2));
        Assert.Equal(1U, GetItemInfoFlags(file, 3));

        ReadOnlySpan<byte> references = GetMetadataChild(file, Heif4CharCode.Iref);
        const int FirstReferenceOffset = 12;
        Assert.Equal(
            Heif4CharCode.Dimg,
            (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(references[(FirstReferenceOffset + 4)..]));

        Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(references[(FirstReferenceOffset + 8)..]));
        Assert.Equal(2, BinaryPrimitives.ReadUInt16BigEndian(references[(FirstReferenceOffset + 10)..]));
        Assert.Equal(2, BinaryPrimitives.ReadUInt16BigEndian(references[(FirstReferenceOffset + 12)..]));
        Assert.Equal(3, BinaryPrimitives.ReadUInt16BigEndian(references[(FirstReferenceOffset + 14)..]));

        stream.Position = 0;
        using Image<Rgb24> decoded = Image.Load<Rgb24>(stream);
        Assert.Empty(ImageComparer.Exact.CompareImages(image, decoded));
    }

    [Theory]
    [WithFile(TestImages.Png.Ducky, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.Bike, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.Splash, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.Transparency, PixelTypes.Rgba32)]
    public void LosslessRgba(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage();

        // Identity 4:4:4 preserves the source RGB samples without matrix or chroma-subsampling losses.
        image.Metadata.CicpProfile = new CicpProfile(1, 13, 0, true);
        HeifEncoder encoder = new()
        {
            Lossless = true,
            Effort = 0
        };

        string outputFile = image.VerifyEncoder(
            provider,
            "avif",
            null,
            encoder,
            ImageComparer.Exact,
            referenceDecoder: MagickReferenceDecoder.Heif);

        using FileStream stream = File.OpenRead(outputFile);
        using Image<Rgba32> reference = MagickReferenceDecoder.Heif.Decode<Rgba32>(DecoderOptions.Default, stream);
        reference.DebugSave(provider, extension: "png", encoder: new PngEncoder());
    }

    [Theory]
    [WithFile(TestImages.Tiff.Rgba10BitUnassociatedAlphaBigEndian, PixelTypes.Rgba64, HeifBitDepth.Bit10)]
    [WithFile(TestImages.Tiff.Rgba12BitUnassociatedAlphaBigEndian, PixelTypes.Rgba64, HeifBitDepth.Bit12)]
    public void Av1LosslessRoundTripPreservesHighBitDepthSourcePixels(TestImageProvider<Rgba64> provider, HeifBitDepth bitDepth)
    {
        using Image<Rgba64> image = provider.GetImage();

        // Identity 4:4:4 retains the source's native 10/12-bit RGB sample precision without a color matrix.
        image.Metadata.CicpProfile = new CicpProfile(1, 13, 0, true);
        HeifEncoder encoder = new()
        {
            BitDepth = bitDepth,
            ChromaSubsampling = HeifChromaSubsampling.Yuv444,
            Lossless = true,
            Effort = 0
        };

        string outputFile = image.VerifyEncoder(
            provider,
            "avif",
            bitDepth,
            encoder,
            ImageComparer.Exact,
            referenceDecoder: MagickReferenceDecoder.Heif);

        using FileStream stream = File.OpenRead(outputFile);
        using Image<Rgba64> reference = MagickReferenceDecoder.Heif.Decode<Rgba64>(DecoderOptions.Default, stream);
        reference.DebugSave(provider, bitDepth, encoder: new PngEncoder
        {
            BitDepth = PngBitDepth.Bit16
        });
    }

    [Theory]
    [InlineData((ushort)0, null, (ushort)0)]
    [InlineData((ushort)3, null, (ushort)3)]
    [InlineData((ushort)3, (ushort)7, (ushort)7)]
    public void Av1LosslessImageSequencePreservesFramesTimingAndAlpha(
        ushort metadataRepeatCount,
        ushort? encoderRepeatCount,
        ushort expectedRepeatCount)
    {
        const int width = 8;
        const int height = 8;
        const int frameCount = 3;
        using Image<Rgba32> image = new(width, height);
        image.Frames.AddFrame(image.Frames.RootFrame);
        image.Frames.AddFrame(image.Frames.RootFrame);
        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            ImageFrame<Rgba32> frame = image.Frames[frameIndex];
            frame.Metadata.GetHeifMetadata().FrameDelay = frameIndex switch
            {
                0 => new Rational(1, 24),
                1 => new Rational(1, 25),
                _ => new Rational(1, 30)
            };

            for (int row = 0; row < height; row++)
            {
                Span<Rgba32> pixels = frame.PixelBuffer.DangerousGetRowSpan(row);
                for (int column = 0; column < width; column++)
                {
                    pixels[column] = new Rgba32(
                        (byte)((frameIndex * 53) + (column * 19) + row),
                        (byte)((frameIndex * 31) + (row * 23) + column),
                        (byte)((frameIndex * 71) + (column * 7) + (row * 13)),
                        (byte)((frameIndex * 47) + (column * 17) + (row * 11)));
                }
            }
        }

        image.Metadata.CicpProfile = new CicpProfile(1, 13, 0, true);
        image.Metadata.IccProfile = new IccProfile(IccTestDataProfiles.ProfileRandomArray);
        ExifProfile exifProfile = new();
        exifProfile.SetValue(ExifTag.Software, "ImageSharp AV1 sequence");
        image.Metadata.ExifProfile = exifProfile;
        byte[] xmpData = Encoding.UTF8.GetBytes("<xmp>ImageSharp AV1 sequence</xmp>");
        image.Metadata.XmpProfile = new XmpProfile(xmpData);
        image.Metadata.GetHeifMetadata().RepeatCount = metadataRepeatCount;
        using MemoryStream stream = new();
        HeifEncoder encoder = new()
        {
            Lossless = true,
            Effort = 0,
            RepeatCount = encoderRepeatCount
        };

        image.Save(stream, encoder);
        byte[] file = stream.ToArray();
        Assert.Equal((uint)Heif4CharCode.Avis, BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(8)));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(GetMetadataChild(file, Heif4CharCode.Pitm)[12..]));

        using (Av1Decoder sampleDecoder = new(Configuration.Default))
        {
            using Av1FrameBuffer<byte> decodedSamplePlanes = sampleDecoder.DecodeFrameBuffer(GetItemPayload(file, 1), null, null, out _);
            using Image<Rgba32> decodedSample = new(Configuration.Default, decodedSamplePlanes.Width, decodedSamplePlanes.Height);
            Av1YuvConverter.ConvertToRgb(
                Configuration.Default,
                decodedSamplePlanes,
                decodedSample.Bounds,
                decodedSample.Frames.RootFrame.PixelBuffer.GetRegion(decodedSample.Bounds),
                decodedSample.Size,
                default,
                null,
                null,
                default,
                default,
                false,
                HeifChromaUpsampling.Auto,
                decodedSamplePlanes.ColorConfig.ColorRange);

            ObuSequenceHeader sampleHeader = sampleDecoder.SequenceHeader;
            Assert.NotNull(sampleHeader);
            Assert.False(sampleHeader.IsStillPicture);
            Assert.False(sampleHeader.IsReducedStillPictureHeader);
        }

        stream.Position = 0;
        DecoderOptions preserveOptions = new() { ColorProfileHandling = ColorProfileHandling.Preserve };
        using Image<Rgba32> decoded = Image.Load<Rgba32>(preserveOptions, stream);
        Assert.Equal(frameCount, decoded.Frames.Count);
        Assert.Equal(expectedRepeatCount, decoded.Metadata.GetHeifMetadata().RepeatCount);
        Assert.True(decoded.Metadata.GetHeifMetadata().AnimateRootFrame);
        Assert.Empty(ImageComparer.Exact.CompareImages(image, decoded));
        Assert.Equal(
            IccTestDataProfiles.ProfileRandomArray,
            Assert.IsType<IccProfile>(decoded.Metadata.IccProfile).ToByteArray());

        ExifProfile decodedExif = Assert.IsType<ExifProfile>(decoded.Metadata.ExifProfile);
        Assert.True(decodedExif.TryGetValue(ExifTag.Software, out IExifValue<string> software));
        Assert.Equal("ImageSharp AV1 sequence", software.Value);
        Assert.Equal(xmpData, Assert.IsType<XmpProfile>(decoded.Metadata.XmpProfile).ToByteArray());
        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            Assert.Equal(
                image.Frames[frameIndex].Metadata.GetHeifMetadata().FrameDelay,
                decoded.Frames[frameIndex].Metadata.GetHeifMetadata().FrameDelay);
        }
    }

    [Fact]
    public void Av1ImageSequenceWritesToPrefixedNonSeekableStream()
    {
        using Image<Rgb24> image = new(8, 8);
        image.Frames.AddFrame(image.Frames.RootFrame);
        image.Frames.RootFrame.Metadata.GetHeifMetadata().FrameDelay = new Rational(1, 10);
        image.Frames[1].Metadata.GetHeifMetadata().FrameDelay = new Rational(1, 20);
        image.Metadata.IccProfile = new IccProfile(IccTestDataProfiles.ProfileRandomArray);
        image.Metadata.ExifProfile = new ExifProfile();
        image.Metadata.ExifProfile.SetValue(ExifTag.Software, "suppressed");
        image.Metadata.XmpProfile = new XmpProfile(Encoding.UTF8.GetBytes("<xmp>suppressed</xmp>"));
        using MemoryStream storage = new();
        storage.Write([1, 2, 3, 4]);
        long fileStart = storage.Position;
        using NonSeekableStream destination = new(storage);
        HeifEncoder encoder = new()
        {
            Effort = 0,
            SkipMetadata = true
        };

        image.Save(destination, encoder);
        storage.Position = fileStart;
        using Image<Rgb24> decoded = Image.Load<Rgb24>(storage);
        Assert.Equal(image.Size, decoded.Size);
        Assert.Equal(image.Frames.Count, decoded.Frames.Count);
        Assert.Null(decoded.Metadata.IccProfile);
        Assert.Null(decoded.Metadata.ExifProfile);
        Assert.Null(decoded.Metadata.XmpProfile);
    }

    [Theory]
    [InlineData(0, 255)]
    [InlineData(1, 249)]
    [InlineData(2, 249)]
    [InlineData(50, 128)]
    [InlineData(60, 100)]
    [InlineData(75, 64)]
    [InlineData(99, 4)]
    [InlineData(100, 4)]
    public void Av1QualityMapsThroughLibaomQuantizers(int quality, int expectedQIndex)
        => Assert.Equal(expectedQIndex, HeifEncoderCore.GetAv1QuantizerIndex(quality));

    [Theory]
    [WithFile(TestImages.Png.Bike, PixelTypes.Rgb24)]
    public void Av1WritesStillImageWithRequiredBrandsAndColorDescription(TestImageProvider<Rgb24> provider)
    {
        using Image<Rgb24> image = provider.GetImage();
        using MemoryStream stream = new();
        HeifEncoder encoder = new()
        {
            Quality = 75,
            Effort = 0
        };

        image.Save(stream, encoder);
        byte[] file = stream.ToArray();
        ReadOnlySpan<byte> fileType = GetTopLevelBox(file, Heif4CharCode.Ftyp);
        Assert.Equal(28, BinaryPrimitives.ReadInt32BigEndian(fileType));
        Assert.Equal(Heif4CharCode.Ftyp, (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(fileType[4..]));
        Assert.Equal(Heif4CharCode.Avif, (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(fileType[8..]));
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(fileType[12..]));
        Assert.Equal(Heif4CharCode.Avif, (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(fileType[16..]));
        Assert.Equal(Heif4CharCode.Mif1, (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(fileType[20..]));
        Assert.Equal(Heif4CharCode.Miaf, (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(fileType[24..]));

        stream.Position = 0;
        ImageInfo info = Image.Identify(stream);
        HeifMetadata metadata = info.Metadata.GetHeifMetadata();
        Assert.Equal(image.Size, info.Size);
        Assert.Equal(HeifBitDepth.Bit8, metadata.BitDepth);
        Assert.False(metadata.IsMonochrome);
        Assert.False(metadata.HasAlpha);
        CicpProfile colorProfile = Assert.IsType<CicpProfile>(info.Metadata.CicpProfile);
        Assert.Equal(CicpMatrixCoefficients.ItuRBt601_7_525, colorProfile.MatrixCoefficients);
        Assert.False(colorProfile.FullRange);
    }

    [Fact]
    public void Av1WritesAuxiliaryAlphaFromSourcePixelType()
    {
        const int width = 16;
        const int height = 8;
        using Image<Rgba32> image = new(width, height);
        for (int row = 0; row < height; row++)
        {
            Span<Rgba32> pixels = image.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(row);
            for (int column = 0; column < width; column++)
            {
                pixels[column] = column < 8
                    ? new Rgba32(40, 80, 120, 0)
                    : new Rgba32(40, 80, 120, 255);
            }
        }

        using MemoryStream stream = new();
        HeifEncoder encoder = new()
        {
            Quality = 75,
            AlphaQuality = 100,
            Effort = 0
        };

        image.Save(stream, encoder);
        byte[] file = stream.ToArray();
        Span<byte> colorPayload = GetItemPayload(file, 1);
        Span<byte> alphaPayload = GetItemPayload(file, 2);
        using Av1Decoder colorDecoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> colorImagePlanes = colorDecoder.DecodeFrameBuffer(colorPayload, null, null, out _);
        using Image<Rgba32> colorImage = new(Configuration.Default, colorImagePlanes.Width, colorImagePlanes.Height);
        Av1YuvConverter.ConvertToRgb(
            Configuration.Default,
            colorImagePlanes,
            colorImage.Bounds,
            colorImage.Frames.RootFrame.PixelBuffer.GetRegion(colorImage.Bounds),
            colorImage.Size,
            default,
            null,
            null,
            default,
            default,
            false,
            HeifChromaUpsampling.Auto,
            colorImagePlanes.ColorConfig.ColorRange);

        ObuFrameHeader colorFrameHeader = Assert.IsType<ObuFrameHeader>(colorDecoder.FrameHeader);
        Assert.Equal(64, colorFrameHeader.QuantizationParameters.BaseQIndex);

        using Av1Decoder alphaDecoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> alphaImagePlanes = alphaDecoder.DecodeFrameBuffer(alphaPayload, null, null, out _);
        using Image<L8> alphaImage = new(Configuration.Default, alphaImagePlanes.Width, alphaImagePlanes.Height);
        Av1YuvConverter.ConvertToRgb(
            Configuration.Default,
            alphaImagePlanes,
            alphaImage.Bounds,
            alphaImage.Frames.RootFrame.PixelBuffer.GetRegion(alphaImage.Bounds),
            alphaImage.Size,
            default,
            null,
            null,
            default,
            default,
            false,
            HeifChromaUpsampling.Auto,
            alphaImagePlanes.ColorConfig.ColorRange);

        ObuSequenceHeader alphaSequenceHeader = Assert.IsType<ObuSequenceHeader>(alphaDecoder.SequenceHeader);
        ObuFrameHeader alphaFrameHeader = Assert.IsType<ObuFrameHeader>(alphaDecoder.FrameHeader);
        Assert.True(alphaSequenceHeader.ColorConfig.IsMonochrome);
        Assert.Equal(4, alphaFrameHeader.QuantizationParameters.BaseQIndex);

        stream.Position = 0;
        using Image<Rgba32> decoded = Image.Load<Rgba32>(stream);
        HeifMetadata metadata = decoded.Metadata.GetHeifMetadata();
        Assert.True(metadata.HasAlpha);
        Assert.InRange(decoded[0, 0].A, (byte)0, (byte)8);
        Assert.InRange(decoded[width - 1, 0].A, (byte)247, byte.MaxValue);
    }

    [Theory]
    [InlineData(HeifBitDepth.Bit8, HeifChromaSubsampling.Monochrome, Av1EightBit, Yuv400)]
    [InlineData(HeifBitDepth.Bit10, HeifChromaSubsampling.Yuv420, Av1TenBit, Yuv420)]
    [InlineData(HeifBitDepth.Bit10, HeifChromaSubsampling.Yuv422, Av1TenBit, Yuv422)]
    [InlineData(HeifBitDepth.Bit12, HeifChromaSubsampling.Yuv444, Av1TwelveBit, Yuv444)]
    public void Av1ExplicitPrecisionAndSamplingReachPayload(
        HeifBitDepth bitDepth,
        HeifChromaSubsampling chromaSubsampling,
        int expectedAv1BitDepthValue,
        int expectedColorFormatValue)
    {
        Av1BitDepth expectedAv1BitDepth = (Av1BitDepth)expectedAv1BitDepthValue;
        Av1ColorFormat expectedColorFormat = (Av1ColorFormat)expectedColorFormatValue;
        using Image<Rgb24> image = new(8, 8);
        for (int row = 0; row < image.Height; row++)
        {
            Span<Rgb24> pixels = image.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(row);
            for (int column = 0; column < image.Width; column++)
            {
                pixels[column] = new Rgb24(
                    (byte)(column * 29),
                    (byte)(row * 29),
                    (byte)((column + row) * 13));
            }
        }

        image.Metadata.GetHeifMetadata().BitDepth = HeifBitDepth.Bit10;
        using MemoryStream stream = new();
        HeifEncoder encoder = new()
        {
            BitDepth = bitDepth,
            ChromaSubsampling = chromaSubsampling,
            Effort = 0
        };

        image.Save(stream, encoder);
        byte[] file = stream.ToArray();
        Span<byte> payload = GetItemPayload(file, 1);
        using Av1Decoder payloadDecoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> payloadImagePlanes = payloadDecoder.DecodeFrameBuffer(payload, null, null, out _);
        using Image<Rgb48> payloadImage = new(Configuration.Default, payloadImagePlanes.Width, payloadImagePlanes.Height);
        Av1YuvConverter.ConvertToRgb(
            Configuration.Default,
            payloadImagePlanes,
            payloadImage.Bounds,
            payloadImage.Frames.RootFrame.PixelBuffer.GetRegion(payloadImage.Bounds),
            payloadImage.Size,
            default,
            null,
            null,
            default,
            default,
            false,
            HeifChromaUpsampling.Auto,
            payloadImagePlanes.ColorConfig.ColorRange);

        ObuSequenceHeader sequenceHeader = Assert.IsType<ObuSequenceHeader>(payloadDecoder.SequenceHeader);
        Assert.Equal(expectedAv1BitDepth, sequenceHeader.ColorConfig.BitDepth);
        Assert.Equal(expectedColorFormat, sequenceHeader.ColorConfig.GetColorFormat());
        Assert.Equal(image.Size, payloadImage.Size);

        stream.Position = 0;
        using Image<Rgb48> decoded = Image.Load<Rgb48>(stream);
        HeifMetadata metadata = decoded.Metadata.GetHeifMetadata();
        Assert.Equal(bitDepth, metadata.BitDepth);
        Assert.Equal(chromaSubsampling == HeifChromaSubsampling.Monochrome, metadata.IsMonochrome);
    }

    [Fact]
    public void Av1UsesMetadataBitDepthWhenOptionIsNull()
    {
        using Image<Rgb24> image = new(8, 8);
        image.Metadata.GetHeifMetadata().BitDepth = HeifBitDepth.Bit10;
        using MemoryStream stream = new();
        HeifEncoder encoder = new()
        {
            ChromaSubsampling = HeifChromaSubsampling.Yuv444,
            Effort = 0
        };

        image.Save(stream, encoder);
        byte[] file = stream.ToArray();
        Span<byte> payload = GetItemPayload(file, 1);
        using Av1Decoder payloadDecoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> payloadImagePlanes = payloadDecoder.DecodeFrameBuffer(payload, null, null, out _);
        using Image<Rgb48> payloadImage = new(Configuration.Default, payloadImagePlanes.Width, payloadImagePlanes.Height);
        Av1YuvConverter.ConvertToRgb(
            Configuration.Default,
            payloadImagePlanes,
            payloadImage.Bounds,
            payloadImage.Frames.RootFrame.PixelBuffer.GetRegion(payloadImage.Bounds),
            payloadImage.Size,
            default,
            null,
            null,
            default,
            default,
            false,
            HeifChromaUpsampling.Auto,
            payloadImagePlanes.ColorConfig.ColorRange);

        ObuSequenceHeader sequenceHeader = Assert.IsType<ObuSequenceHeader>(payloadDecoder.SequenceHeader);
        Assert.Equal(Av1BitDepth.TenBit, sequenceHeader.ColorConfig.BitDepth);
    }

    [Theory]
    [InlineData(HeifBitDepth.Bit8, false, false, false)]
    [InlineData(HeifBitDepth.Bit8, true, false, false)]
    [InlineData(HeifBitDepth.Bit10, false, false, false)]
    [InlineData(HeifBitDepth.Bit10, true, false, false)]
    [InlineData(HeifBitDepth.Bit12, false, false, false)]
    [InlineData(HeifBitDepth.Bit12, true, false, false)]
    [InlineData(HeifBitDepth.Bit8, false, true, false)]
    [InlineData(HeifBitDepth.Bit8, true, true, false)]
    [InlineData(HeifBitDepth.Bit10, false, true, false)]
    [InlineData(HeifBitDepth.Bit10, true, true, false)]
    [InlineData(HeifBitDepth.Bit12, false, true, false)]
    [InlineData(HeifBitDepth.Bit12, true, true, false)]
    [InlineData(HeifBitDepth.Bit8, false, false, true)]
    [InlineData(HeifBitDepth.Bit12, false, true, true)]
    public void Av1PreservesIdentityMatrixColorDescription(
        HeifBitDepth bitDepth,
        bool fullRange,
        bool sequence,
        bool srgb)
    {
        const int Width = 8;
        const int Height = 8;
        using Image<Rgb24> image = new(Width, Height);
        if (sequence)
        {
            image.Frames.AddFrame(image.Frames.RootFrame);
        }

        for (int frameIndex = 0; frameIndex < image.Frames.Count; frameIndex++)
        {
            ImageFrame<Rgb24> frame = image.Frames[frameIndex];
            frame.Metadata.GetHeifMetadata().FrameDelay = new Rational(1, 25);
            for (int y = 0; y < Height; y++)
            {
                Span<Rgb24> row = frame.PixelBuffer.DangerousGetRowSpan(y);
                for (int x = 0; x < Width; x++)
                {
                    row[x] = new Rgb24(
                        (byte)((x * 31) + y + frameIndex),
                        (byte)((y * 29) + x + frameIndex),
                        (byte)((x * 17) + (y * 11) + frameIndex));
                }
            }
        }

        // BT.2020/PQ identity uses explicit range syntax. Only the BT.709/sRGB identity combination
        // infers full range, so its limited-range metadata must be normalized before pixel conversion.
        CicpProfile profile = srgb ? new(1, 13, 0, fullRange) : new(9, 16, 0, fullRange);
        image.Metadata.CicpProfile = profile;
        bool expectedFullRange = fullRange || srgb;
        using MemoryStream stream = new();
        image.Save(stream, new HeifEncoder
        {
            BitDepth = bitDepth,
            ChromaSubsampling = HeifChromaSubsampling.Yuv444,
            Lossless = true,
            Effort = 0
        });

        Assert.Same(profile, image.Metadata.CicpProfile);
        Assert.Equal(fullRange, profile.FullRange);
        byte[] file = stream.ToArray();
        using Av1Decoder sampleDecoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> samplePlanes = sampleDecoder.DecodeFrameBuffer(GetItemPayload(file, 1), null, null, out _);
        using Image<Rgb24> sample = new(Configuration.Default, samplePlanes.Width, samplePlanes.Height);
        Av1YuvConverter.ConvertToRgb(
            Configuration.Default,
            samplePlanes,
            sample.Bounds,
            sample.Frames.RootFrame.PixelBuffer.GetRegion(sample.Bounds),
            sample.Size,
            default,
            null,
            null,
            default,
            default,
            false,
            HeifChromaUpsampling.Auto,
            samplePlanes.ColorConfig.ColorRange);

        ObuSequenceHeader header = Assert.IsType<ObuSequenceHeader>(sampleDecoder.SequenceHeader);
        Assert.Equal(ObuMatrixCoefficients.Identity, header.ColorConfig.MatrixCoefficients);
        Assert.Equal((byte)profile.ColorPrimaries, (byte)header.ColorConfig.ColorPrimaries);
        Assert.Equal((byte)profile.TransferCharacteristics, (byte)header.ColorConfig.TransferCharacteristics);
        Assert.Equal(expectedFullRange, header.ColorConfig.ColorRange);
        Assert.Equal(Av1ColorFormat.Yuv444, header.ColorConfig.GetColorFormat());

        stream.Position = 0;
        DecoderOptions options = new() { ColorProfileHandling = ColorProfileHandling.Preserve };
        using Image<Rgb24> decoded = Image.Load<Rgb24>(options, stream);
        CicpProfile decodedProfile = Assert.IsType<CicpProfile>(decoded.Metadata.CicpProfile);
        Assert.Equal(profile.ColorPrimaries, decodedProfile.ColorPrimaries);
        Assert.Equal(profile.TransferCharacteristics, decodedProfile.TransferCharacteristics);
        Assert.Equal(CicpMatrixCoefficients.Identity, decodedProfile.MatrixCoefficients);
        Assert.True(decodedProfile.FullRange);
        Assert.Equal(image.Frames.Count, decoded.Frames.Count);
        for (int frameIndex = 0; frameIndex < image.Frames.Count; frameIndex++)
        {
            for (int y = 0; y < Height; y++)
            {
                ReadOnlySpan<Rgb24> expectedRow = image.Frames[frameIndex].PixelBuffer.DangerousGetRowSpan(y);
                ReadOnlySpan<Rgb24> actualRow = decoded.Frames[frameIndex].PixelBuffer.DangerousGetRowSpan(y);
                for (int x = 0; x < Width; x++)
                {
                    // Limited-range conversion rounds onto 219 codes before the lossless codec stage.
                    Assert.InRange((int)actualRow[x].R - expectedRow[x].R, -1, 1);
                    Assert.InRange((int)actualRow[x].G - expectedRow[x].G, -1, 1);
                    Assert.InRange((int)actualRow[x].B - expectedRow[x].B, -1, 1);
                }
            }
        }
    }

    [Theory]
    [InlineData(CicpMatrixCoefficients.Identity, HeifChromaSubsampling.Yuv420)]
    [InlineData(CicpMatrixCoefficients.YCgCoRe, null)]
    [InlineData(CicpMatrixCoefficients.YCgCoRe, HeifChromaSubsampling.Yuv420)]
    [InlineData(CicpMatrixCoefficients.YCgCoRe, HeifChromaSubsampling.Yuv422)]
    [InlineData(CicpMatrixCoefficients.YCgCoRo, null)]
    [InlineData(CicpMatrixCoefficients.YCgCoRo, HeifChromaSubsampling.Yuv420)]
    [InlineData(CicpMatrixCoefficients.YCgCoRo, HeifChromaSubsampling.Yuv422)]
    public void Av1SanitizesIncompatibleMatrixWithoutMutatingSourceMetadata(
        CicpMatrixCoefficients matrix,
        HeifChromaSubsampling? subsampling)
    {
        using Image<Rgb24> image = new(8, 8, new Rgb24(32, 96, 192));
        CicpProfile sourceProfile = new(1, 13, (byte)matrix, false);
        image.Metadata.CicpProfile = sourceProfile;
        using MemoryStream stream = new();
        HeifEncoder encoder = new()
        {
            ChromaSubsampling = subsampling,
            Effort = 0
        };

        image.Save(stream, encoder);
        Assert.Same(sourceProfile, image.Metadata.CicpProfile);
        Assert.Equal(matrix, sourceProfile.MatrixCoefficients);
        Assert.False(sourceProfile.FullRange);

        stream.Position = 0;
        ImageInfo encoded = Image.Identify(stream);
        CicpProfile encodedProfile = Assert.IsType<CicpProfile>(encoded.Metadata.CicpProfile);
        Assert.Equal(CicpMatrixCoefficients.ItuRBt601_7_525, encodedProfile.MatrixCoefficients);
        Assert.False(encodedProfile.FullRange);

        // A fallback must change the actual encoded conversion as well as its metadata. Compare with the
        // same packed pixels explicitly encoded using that fallback matrix and the requested sampling.
        using Image<Rgb24> explicitConversion = image.Clone();
        explicitConversion.Metadata.CicpProfile = new CicpProfile(1, 13, (byte)CicpMatrixCoefficients.ItuRBt601_7_525, false);
        using MemoryStream expected = new();
        explicitConversion.Save(expected, encoder);
        Assert.Equal(expected.ToArray(), stream.ToArray());
    }

    [Fact]
    public void Av1PreservesIccExifAndXmpMetadata()
    {
        using Image<Rgb24> image = new(8, 8);
        image.Metadata.IccProfile = new IccProfile(IccTestDataProfiles.ProfileRandomArray);

        ExifProfile generatedExif = new();
        generatedExif.SetValue(ExifTag.Software, "ImageSharp HEIF");
        byte[] exifData = generatedExif.ToByteArray();
        image.Metadata.ExifProfile = generatedExif;

        byte[] xmpData = Encoding.UTF8.GetBytes("<xmp>ImageSharp HEIF</xmp>");
        image.Metadata.XmpProfile = new XmpProfile(xmpData);

        using MemoryStream stream = new();
        HeifEncoder encoder = new()
        {
            Effort = 0
        };

        image.Save(stream, encoder);
        byte[] file = stream.ToArray();

        Span<byte> itemInfo = GetMetadataChild(file, Heif4CharCode.Iinf);
        Assert.Equal(3, BinaryPrimitives.ReadUInt16BigEndian(itemInfo[12..]));
        int entryOffset = 14;

        int colorEntryLength = BinaryPrimitives.ReadInt32BigEndian(itemInfo[entryOffset..]);
        Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(itemInfo[(entryOffset + 12)..]));
        Assert.Equal(Heif4CharCode.Av01, (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(itemInfo[(entryOffset + 16)..]));
        Assert.Equal([0], itemInfo.Slice(entryOffset + 20, colorEntryLength - 20).ToArray());
        entryOffset += colorEntryLength;

        int exifEntryLength = BinaryPrimitives.ReadInt32BigEndian(itemInfo[entryOffset..]);
        Assert.Equal(2, BinaryPrimitives.ReadUInt16BigEndian(itemInfo[(entryOffset + 12)..]));
        Assert.Equal(Heif4CharCode.Exif, (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(itemInfo[(entryOffset + 16)..]));
        Assert.Equal("Exif\0", Encoding.UTF8.GetString(itemInfo.Slice(entryOffset + 20, exifEntryLength - 20)));
        entryOffset += exifEntryLength;

        int xmpEntryLength = BinaryPrimitives.ReadInt32BigEndian(itemInfo[entryOffset..]);
        Assert.Equal(3, BinaryPrimitives.ReadUInt16BigEndian(itemInfo[(entryOffset + 12)..]));
        Assert.Equal(Heif4CharCode.Mime, (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(itemInfo[(entryOffset + 16)..]));
        Assert.Equal(
            "XMP\0application/rdf+xml\0",
            Encoding.UTF8.GetString(itemInfo.Slice(entryOffset + 20, xmpEntryLength - 20)));

        entryOffset += xmpEntryLength;
        Assert.Equal(itemInfo.Length, entryOffset);

        Span<byte> itemReferences = GetMetadataChild(file, Heif4CharCode.Iref);
        int referenceOffset = 12;
        for (ushort sourceId = 2; sourceId <= 3; sourceId++)
        {
            int referenceLength = BinaryPrimitives.ReadInt32BigEndian(itemReferences[referenceOffset..]);
            Assert.Equal(14, referenceLength);
            Assert.Equal(
                Heif4CharCode.Cdsc,
                (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(itemReferences[(referenceOffset + 4)..]));

            Assert.Equal(sourceId, BinaryPrimitives.ReadUInt16BigEndian(itemReferences[(referenceOffset + 8)..]));
            Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(itemReferences[(referenceOffset + 10)..]));
            Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(itemReferences[(referenceOffset + 12)..]));
            referenceOffset += referenceLength;
        }

        Assert.Equal(itemReferences.Length, referenceOffset);

        Span<byte> encodedExif = GetItemPayload(file, 2);
        Assert.Equal(0U, BinaryPrimitives.ReadUInt32BigEndian(encodedExif));
        Assert.Equal(exifData, encodedExif[4..].ToArray());
        Assert.Equal(xmpData, GetItemPayload(file, 3).ToArray());

        DecoderOptions preserveOptions = new() { ColorProfileHandling = ColorProfileHandling.Preserve };
        using Image<Rgb24> decoded = Image.Load<Rgb24>(preserveOptions, file);
        Assert.Equal(
            IccTestDataProfiles.ProfileRandomArray,
            Assert.IsType<IccProfile>(decoded.Metadata.IccProfile).ToByteArray());

        ExifProfile decodedExif = Assert.IsType<ExifProfile>(decoded.Metadata.ExifProfile);
        Assert.True(decodedExif.TryGetValue(ExifTag.Software, out IExifValue<string> software));
        Assert.Equal("ImageSharp HEIF", software.Value);
        Assert.Equal(xmpData, Assert.IsType<XmpProfile>(decoded.Metadata.XmpProfile).ToByteArray());
    }

    [Fact]
    public void Av1SkipMetadataSuppressesIccExifAndXmp()
    {
        using Image<Rgb24> image = new(8, 8);
        image.Metadata.IccProfile = new IccProfile(IccTestDataProfiles.ProfileRandomArray);
        image.Metadata.ExifProfile = new ExifProfile();
        image.Metadata.ExifProfile.SetValue(ExifTag.Software, "ImageSharp HEIF");
        image.Metadata.XmpProfile = new XmpProfile(Encoding.UTF8.GetBytes("<xmp>ImageSharp HEIF</xmp>"));

        using MemoryStream stream = new();
        HeifEncoder encoder = new()
        {
            Effort = 0,
            SkipMetadata = true
        };

        image.Save(stream, encoder);
        byte[] file = stream.ToArray();
        Span<byte> itemInfo = GetMetadataChild(file, Heif4CharCode.Iinf);
        Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(itemInfo[12..]));

        Span<byte> itemProperties = GetMetadataChild(file, Heif4CharCode.Iprp);
        const int IpcoOffset = 8;
        int ipcoEnd = IpcoOffset + BinaryPrimitives.ReadInt32BigEndian(itemProperties[IpcoOffset..]);
        int propertyOffset = IpcoOffset + 8;
        while (propertyOffset < ipcoEnd)
        {
            int propertyLength = BinaryPrimitives.ReadInt32BigEndian(itemProperties[propertyOffset..]);
            Heif4CharCode propertyType = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(itemProperties[(propertyOffset + 4)..]);
            if (propertyType == Heif4CharCode.Colr)
            {
                Assert.Equal(
                    Heif4CharCode.Nclx,
                    (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(itemProperties[(propertyOffset + 8)..]));
            }

            propertyOffset += propertyLength;
        }

        Assert.Equal(ipcoEnd, propertyOffset);

        DecoderOptions preserveOptions = new() { ColorProfileHandling = ColorProfileHandling.Preserve };
        using Image<Rgb24> decoded = Image.Load<Rgb24>(preserveOptions, file);
        Assert.Null(decoded.Metadata.IccProfile);
        Assert.Null(decoded.Metadata.ExifProfile);
        Assert.Null(decoded.Metadata.XmpProfile);
    }

    [Fact]
    public void Av1WritesNonSeekableStream()
    {
        using Image<Rgb24> image = new(8, 8);
        using MemoryStream storage = new();
        using NonSeekableStream destination = new(storage);
        HeifEncoder encoder = new()
        {
            Effort = 0
        };

        image.Save(destination, encoder);
        Assert.NotEqual(0, storage.Length);
        storage.Position = 0;
        using Image<Rgb24> decoded = Image.Load<Rgb24>(storage);
        Assert.Equal(image.Size, decoded.Size);
    }

    [Fact]
    public void Av1WritesAtCurrentStreamPosition()
    {
        using Image<Rgb24> image = new(8, 8);
        using MemoryStream stream = new();
        stream.Write([1, 2, 3, 4]);
        long fileStart = stream.Position;
        HeifEncoder encoder = new()
        {
            Effort = 0
        };

        image.Save(stream, encoder);
        stream.Position = fileStart;
        using Image<Rgb24> decoded = Image.Load<Rgb24>(stream);
        Assert.Equal(image.Size, decoded.Size);
    }

    [Fact]
    public void Av1ItemPropertiesWriteRequiredTypesAndEssentialConfiguration()
    {
        ObuSequenceHeader colorHeader = new()
        {
            SequenceProfile = ObuSequenceProfile.Main,
            OperatingPoint = [new ObuOperatingPoint { SequenceLevelIndex = 31 }],
            ColorConfig = new ObuColorConfig
            {
                BitDepth = Av1BitDepth.TenBit,
                SubSamplingX = true,
                SubSamplingY = true
            }
        };

        ObuSequenceHeader alphaHeader = new()
        {
            SequenceProfile = ObuSequenceProfile.Main,
            OperatingPoint = [new ObuOperatingPoint { SequenceLevelIndex = 31 }],
            ColorConfig = new ObuColorConfig
            {
                BitDepth = Av1BitDepth.TenBit,
                IsMonochrome = true,
                SubSamplingX = true,
                SubSamplingY = true
            }
        };

        HeifItem colorItem = new(Heif4CharCode.Av01, 1)
        {
            ChannelBitDepths = [10, 10, 10],
            Av1CodecConfiguration = new Av1CodecConfiguration(colorHeader),
            CicpProfile = new CicpProfile(1, 13, 6, true)
        };

        colorItem.SetExtent(new Size(64, 48));
        HeifItem alphaItem = new(Heif4CharCode.Av01, 2)
        {
            ChannelBitDepths = [10],
            Av1CodecConfiguration = new Av1CodecConfiguration(alphaHeader),
            AuxiliaryType = HeifConstants.AlphaAuxiliaryType
        };

        alphaItem.SetExtent(new Size(64, 48));
        List<HeifItem> items = [colorItem, alphaItem];
        int expectedLength = HeifEncoderCore.GetItemPropertiesBoxLength(items);
        using IMemoryOwner<byte> owner = Configuration.Default.MemoryAllocator.Allocate<byte>(expectedLength);
        Span<byte> propertyBox = owner.Memory.Span[..expectedLength];
        int length = HeifEncoderCore.WriteItemPropertiesBox(propertyBox, 0, items);

        Assert.Equal(length, BinaryPrimitives.ReadInt32BigEndian(propertyBox));
        Assert.Equal(Heif4CharCode.Iprp, (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(propertyBox[4..]));

        const int IpcoOffset = 8;
        int ipcoSize = BinaryPrimitives.ReadInt32BigEndian(propertyBox[IpcoOffset..]);
        Assert.Equal(Heif4CharCode.Ipco, (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(propertyBox[(IpcoOffset + 4)..]));

        Heif4CharCode[] expectedTypes =
        [
            Heif4CharCode.Ispe,
            Heif4CharCode.Pixi,
            Heif4CharCode.Av1C,
            Heif4CharCode.Colr,
            Heif4CharCode.Ispe,
            Heif4CharCode.Pixi,
            Heif4CharCode.Av1C,
            Heif4CharCode.AuxC
        ];

        int propertyOffset = IpcoOffset + 8;
        int ipcoEnd = IpcoOffset + ipcoSize;
        int propertyIndex = 0;
        while (propertyOffset < ipcoEnd)
        {
            int propertySize = BinaryPrimitives.ReadInt32BigEndian(propertyBox[propertyOffset..]);
            Heif4CharCode propertyType = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(propertyBox[(propertyOffset + 4)..]);
            ReadOnlySpan<byte> payload = propertyBox.Slice(propertyOffset + 8, propertySize - 8);
            Assert.Equal(expectedTypes[propertyIndex], propertyType);
            switch (propertyIndex)
            {
                case 0:
                case 4:
                    Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(payload));
                    Assert.Equal(64, BinaryPrimitives.ReadInt32BigEndian(payload[4..]));
                    Assert.Equal(48, BinaryPrimitives.ReadInt32BigEndian(payload[8..]));
                    break;
                case 1:
                    Assert.Equal([0, 0, 0, 0, 3, 10, 10, 10], payload.ToArray());
                    break;
                case 2:
                    Assert.Equal([0x81, 0x1F, 0x4C, 0], payload.ToArray());
                    break;
                case 3:
                    Assert.Equal(Heif4CharCode.Nclx, (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(payload));
                    Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(payload[4..]));
                    Assert.Equal(13, BinaryPrimitives.ReadUInt16BigEndian(payload[6..]));
                    Assert.Equal(6, BinaryPrimitives.ReadUInt16BigEndian(payload[8..]));
                    Assert.Equal(0x80, payload[10]);
                    break;
                case 5:
                    Assert.Equal([0, 0, 0, 0, 1, 10], payload.ToArray());
                    break;
                case 6:
                    Assert.Equal([0x81, 0x1F, 0x5C, 0], payload.ToArray());
                    break;
                case 7:
                    Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(payload));
                    Assert.Equal(HeifConstants.AlphaAuxiliaryType, Encoding.UTF8.GetString(payload[4..^1]));
                    Assert.Equal(0, payload[^1]);
                    break;
            }

            propertyOffset += propertySize;
            propertyIndex++;
        }

        Assert.Equal(expectedTypes.Length, propertyIndex);
        Assert.Equal(ipcoEnd, propertyOffset);

        int ipmaOffset = ipcoEnd;
        int ipmaSize = BinaryPrimitives.ReadInt32BigEndian(propertyBox[ipmaOffset..]);
        Assert.Equal(length - ipmaOffset, ipmaSize);
        Assert.Equal(Heif4CharCode.Ipma, (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(propertyBox[(ipmaOffset + 4)..]));
        ReadOnlySpan<byte> ipmaPayload = propertyBox.Slice(ipmaOffset + 8, ipmaSize - 8);
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(ipmaPayload));
        Assert.Equal(2, BinaryPrimitives.ReadInt32BigEndian(ipmaPayload[4..]));
        Assert.Equal(
            [0, 1, 4, 1, 2, 0x83, 4, 0, 2, 4, 5, 6, 0x87, 8],
            ipmaPayload[8..].ToArray());
    }

    [Fact]
    public void ItemPropertiesReuseAnEarlierIdenticalCellPropertySet()
    {
        ObuSequenceHeader sequenceHeader = new()
        {
            SequenceProfile = ObuSequenceProfile.Main,
            OperatingPoint = [new ObuOperatingPoint { SequenceLevelIndex = 31 }],
            ColorConfig = new ObuColorConfig
            {
                BitDepth = Av1BitDepth.EightBit,
                SubSamplingX = false,
                SubSamplingY = false
            }
        };

        HeifItem firstCell = new(Heif4CharCode.Av01, 1)
        {
            ChannelBitDepths = [8, 8, 8],
            Av1CodecConfiguration = new Av1CodecConfiguration(sequenceHeader)
        };

        firstCell.SetExtent(new Size(64, 64));
        HeifItem secondCell = new(Heif4CharCode.Av01, 2)
        {
            PropertySource = firstCell
        };

        secondCell.SetExtent(firstCell.Extent);
        List<HeifItem> items = [firstCell, secondCell];
        int expectedLength = HeifEncoderCore.GetItemPropertiesBoxLength(items);
        using IMemoryOwner<byte> owner = Configuration.Default.MemoryAllocator.Allocate<byte>(expectedLength);
        Span<byte> propertyBox = owner.Memory.Span[..expectedLength];
        int length = HeifEncoderCore.WriteItemPropertiesBox(propertyBox, 0, items);

        Assert.Equal(92, length);
        const int IpcoOffset = 8;
        int ipmaOffset = IpcoOffset + BinaryPrimitives.ReadInt32BigEndian(propertyBox[IpcoOffset..]);
        ReadOnlySpan<byte> ipmaPayload = propertyBox[(ipmaOffset + 8)..];
        Assert.Equal(
            [0, 0, 0, 0, 0, 0, 0, 2, 0, 1, 3, 1, 2, 0x83, 0, 2, 3, 1, 2, 0x83],
            ipmaPayload.ToArray());
    }

    [Fact]
    public void Av1ItemPropertiesWriteIccBeforeCicpAndExcludeMetadataItemsFromAssociations()
    {
        IccProfile iccProfile = new(IccTestDataProfiles.ProfileRandomArray);
        HeifItem colorItem = new(Heif4CharCode.Av01, 1)
        {
            IccProfile = iccProfile,
            CicpProfile = new CicpProfile(1, 13, 6, true)
        };

        colorItem.SetExtent(new Size(64, 48));
        List<HeifItem> items =
        [
            colorItem,
            new HeifItem(Heif4CharCode.Exif, 2),
            new HeifItem(Heif4CharCode.Mime, 3)
        ];

        int expectedLength = HeifEncoderCore.GetItemPropertiesBoxLength(items);
        using IMemoryOwner<byte> owner = Configuration.Default.MemoryAllocator.Allocate<byte>(expectedLength);
        Span<byte> propertyBox = owner.Memory.Span[..expectedLength];
        int length = HeifEncoderCore.WriteItemPropertiesBox(propertyBox, 0, items);
        const int IpcoOffset = 8;
        int ipcoEnd = IpcoOffset + BinaryPrimitives.ReadInt32BigEndian(propertyBox[IpcoOffset..]);
        int propertyOffset = IpcoOffset + 8;

        Assert.Equal(Heif4CharCode.Ispe, (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(propertyBox[(propertyOffset + 4)..]));
        propertyOffset += BinaryPrimitives.ReadInt32BigEndian(propertyBox[propertyOffset..]);

        int iccPropertyLength = BinaryPrimitives.ReadInt32BigEndian(propertyBox[propertyOffset..]);
        Assert.Equal(Heif4CharCode.Colr, (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(propertyBox[(propertyOffset + 4)..]));
        Assert.Equal(Heif4CharCode.Prof, (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(propertyBox[(propertyOffset + 8)..]));
        Assert.Equal(
            IccTestDataProfiles.ProfileRandomArray,
            propertyBox.Slice(propertyOffset + 12, iccPropertyLength - 12).ToArray());

        propertyOffset += iccPropertyLength;

        int cicpPropertyLength = BinaryPrimitives.ReadInt32BigEndian(propertyBox[propertyOffset..]);
        Assert.Equal(Heif4CharCode.Colr, (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(propertyBox[(propertyOffset + 4)..]));
        Assert.Equal(Heif4CharCode.Nclx, (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(propertyBox[(propertyOffset + 8)..]));
        propertyOffset += cicpPropertyLength;
        Assert.Equal(ipcoEnd, propertyOffset);

        int ipmaOffset = ipcoEnd;
        int ipmaLength = BinaryPrimitives.ReadInt32BigEndian(propertyBox[ipmaOffset..]);
        ReadOnlySpan<byte> ipmaPayload = propertyBox.Slice(ipmaOffset + 8, ipmaLength - 8);
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(ipmaPayload));
        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(ipmaPayload[4..]));
        Assert.Equal([0, 1, 3, 1, 2, 3], ipmaPayload[8..].ToArray());
    }

    [Fact]
    public void ItemPropertiesUseLargeAssociationsWhenPropertyCountExceedsCompactRange()
    {
        const int ItemCount = 43;
        ObuSequenceHeader sequenceHeader = new()
        {
            SequenceProfile = ObuSequenceProfile.Main,
            OperatingPoint = [new ObuOperatingPoint { SequenceLevelIndex = 31 }],
            ColorConfig = new ObuColorConfig
            {
                BitDepth = Av1BitDepth.EightBit,
                SubSamplingX = true,
                SubSamplingY = true
            }
        };

        Av1CodecConfiguration codecConfiguration = new(sequenceHeader);
        byte[] channelBitDepths = [8, 8, 8];
        List<HeifItem> items = new(ItemCount);
        for (uint itemId = 1; itemId <= ItemCount; itemId++)
        {
            HeifItem item = new(Heif4CharCode.Av01, itemId)
            {
                ChannelBitDepths = channelBitDepths,
                Av1CodecConfiguration = codecConfiguration
            };

            item.SetExtent(new Size(1, 1));
            items.Add(item);
        }

        int expectedLength = HeifEncoderCore.GetItemPropertiesBoxLength(items);
        using IMemoryOwner<byte> owner = Configuration.Default.MemoryAllocator.Allocate<byte>(expectedLength);
        Span<byte> propertyBox = owner.Memory.Span[..expectedLength];
        int length = HeifEncoderCore.WriteItemPropertiesBox(propertyBox, 0, items);
        const int IpcoOffset = 8;
        int ipcoSize = BinaryPrimitives.ReadInt32BigEndian(propertyBox[IpcoOffset..]);
        int ipmaOffset = IpcoOffset + ipcoSize;

        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(propertyBox[(ipmaOffset + 8)..]));
        Assert.Equal(ItemCount, BinaryPrimitives.ReadInt32BigEndian(propertyBox[(ipmaOffset + 12)..]));

        const int AssociationEntrySize = 9;
        int finalEntryOffset = ipmaOffset + 16 + ((ItemCount - 1) * AssociationEntrySize);
        Assert.Equal(ItemCount, BinaryPrimitives.ReadUInt16BigEndian(propertyBox[finalEntryOffset..]));
        Assert.Equal(3, propertyBox[finalEntryOffset + 2]);
        Assert.Equal(127, BinaryPrimitives.ReadUInt16BigEndian(propertyBox[(finalEntryOffset + 3)..]));
        Assert.Equal(128, BinaryPrimitives.ReadUInt16BigEndian(propertyBox[(finalEntryOffset + 5)..]));
        Assert.Equal(0x8081, BinaryPrimitives.ReadUInt16BigEndian(propertyBox[(finalEntryOffset + 7)..]));
    }

    private static Span<byte> GetItemPayload(Span<byte> file, ushort itemId)
    {
        int offset = 0;
        while (offset < file.Length)
        {
            int boxSize = BinaryPrimitives.ReadInt32BigEndian(file[offset..]);
            Heif4CharCode boxType = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(file[(offset + 4)..]);
            if (boxType == Heif4CharCode.Meta)
            {
                int childOffset = offset + 12;
                int boxEnd = offset + boxSize;
                while (childOffset < boxEnd)
                {
                    int childSize = BinaryPrimitives.ReadInt32BigEndian(file[childOffset..]);
                    Heif4CharCode childType = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(file[(childOffset + 4)..]);
                    if (childType == Heif4CharCode.Iloc)
                    {
                        int locationOffset = childOffset + 14;
                        int itemCount = BinaryPrimitives.ReadUInt16BigEndian(file[locationOffset..]);
                        locationOffset += 2;
                        for (int itemIndex = 0; itemIndex < itemCount; itemIndex++)
                        {
                            ushort currentItemId = BinaryPrimitives.ReadUInt16BigEndian(file[locationOffset..]);
                            locationOffset += 6;
                            int extentCount = BinaryPrimitives.ReadUInt16BigEndian(file[locationOffset..]);
                            locationOffset += 2;
                            for (int extentIndex = 0; extentIndex < extentCount; extentIndex++)
                            {
                                int itemOffset = checked((int)BinaryPrimitives.ReadUInt64BigEndian(file[locationOffset..]));
                                locationOffset += 8;
                                int itemLength = BinaryPrimitives.ReadInt32BigEndian(file[locationOffset..]);
                                locationOffset += 4;
                                if (currentItemId == itemId)
                                {
                                    return file.Slice(itemOffset, itemLength);
                                }
                            }
                        }
                    }

                    childOffset += childSize;
                }
            }

            offset += boxSize;
        }

        throw new InvalidImageContentException($"The encoded file has no payload for item {itemId}.");
    }

    private static uint GetItemInfoFlags(Span<byte> file, ushort itemId)
    {
        ReadOnlySpan<byte> itemInformation = GetMetadataChild(file, Heif4CharCode.Iinf);
        int entryOffset = 14;
        while (entryOffset < itemInformation.Length)
        {
            int entrySize = BinaryPrimitives.ReadInt32BigEndian(itemInformation[entryOffset..]);
            ReadOnlySpan<byte> entry = itemInformation.Slice(entryOffset, entrySize);
            ushort currentItemId = BinaryPrimitives.ReadUInt16BigEndian(entry[12..]);
            if (currentItemId == itemId)
            {
                return (uint)((entry[9] << 16) | (entry[10] << 8) | entry[11]);
            }

            entryOffset += entrySize;
        }

        throw new InvalidImageContentException($"The encoded file has no item-information entry for item {itemId}.");
    }

    private static Image<Rgba32> DecodeSingleCellGrid(int width, int height, ObuColorConfig colorConfig)
    {
        using Image<Rgba32> tile = new(width, height, new Rgba32(127, 127, 127));
        using MemoryStream payloadStream = new();
        ObuSequenceHeader header = Av1FrameEncoder.Encode(
            Configuration.Default,
            tile.Frames.RootFrame,
            payloadStream,
            colorConfig,
            qIndex: 0,
            effort: 0);

        byte[] payload = payloadStream.ToArray();
        HeifItem gridItem = new(Heif4CharCode.Grid, 1);
        gridItem.SetExtent(new Size(width, height));
        HeifItem tileItem = new(Heif4CharCode.Av01, 2)
        {
            Av1CodecConfiguration = new Av1CodecConfiguration(header)
        };

        tileItem.SetExtent(new Size(width, height));
        HeifItemLink gridLink = new(Heif4CharCode.Dimg, gridItem.Id);
        gridLink.DestinationIds.Add(tileItem.Id);
        GridHeifItemDecoder<Rgba32> decoder = new([gridItem, tileItem], [gridLink], ReadItem);
        byte[] descriptor = new byte[8];
        BinaryPrimitives.WriteUInt16BigEndian(descriptor.AsSpan(4), (ushort)width);
        BinaryPrimitives.WriteUInt16BigEndian(descriptor.AsSpan(6), (ushort)height);
        Image<Rgba32> result = new(width, height);
        try
        {
            decoder.DecodeItemData(
                new DecoderOptions { Configuration = Configuration.Default },
                HeifChromaUpsampling.Auto,
                gridItem,
                descriptor,
                null,
                null,
                null,
                default,
                default,
                false,
                result.Bounds,
                default,
                result.Frames.RootFrame.PixelBuffer.GetRegion(result.Bounds),
                result.Metadata,
                TestContext.Current.CancellationToken);

            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }

        IMemoryOwner<byte> ReadItem(HeifItem item)
        {
            Assert.Equal(tileItem.Id, item.Id);
            IMemoryOwner<byte> owner = Configuration.Default.MemoryAllocator.Allocate<byte>(payload.Length);
            payload.CopyTo(owner.Memory.Span);
            return owner;
        }
    }

    private static Span<byte> GetTopLevelBox(Span<byte> file, Heif4CharCode requestedType)
    {
        int offset = 0;
        while (offset < file.Length)
        {
            int boxSize = BinaryPrimitives.ReadInt32BigEndian(file[offset..]);
            Heif4CharCode boxType = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(file[(offset + sizeof(uint))..]);
            if (boxType == requestedType)
            {
                return file.Slice(offset, boxSize);
            }

            offset += boxSize;
        }

        throw new InvalidImageContentException($"The encoded file has no {requestedType} top-level box.");
    }

    private static Span<byte> GetMetadataChild(Span<byte> file, Heif4CharCode childType)
    {
        int offset = 0;
        while (offset < file.Length)
        {
            int boxSize = BinaryPrimitives.ReadInt32BigEndian(file[offset..]);
            Heif4CharCode boxType = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(file[(offset + 4)..]);
            if (boxType == Heif4CharCode.Meta)
            {
                int childOffset = offset + 12;
                int boxEnd = offset + boxSize;
                while (childOffset < boxEnd)
                {
                    int childSize = BinaryPrimitives.ReadInt32BigEndian(file[childOffset..]);
                    Heif4CharCode currentChildType =
                        (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(file[(childOffset + 4)..]);

                    if (currentChildType == childType)
                    {
                        return file.Slice(childOffset, childSize);
                    }

                    childOffset += childSize;
                }
            }

            offset += boxSize;
        }

        throw new InvalidImageContentException($"The encoded file has no {childType} metadata child.");
    }
}
