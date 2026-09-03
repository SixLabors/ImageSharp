// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Text;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
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

        Assert.Equal(HeifCompressionMethod.LegacyJpeg, encoder.CompressionMethod);
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
    public void LegacyJpegAcceptsZeroQuality()
    {
        using Image<Rgba32> image = new(1, 1);
        image[0, 0] = new Rgba32(10, 20, 30);
        using MemoryStream stream = new();
        HeifEncoder encoder = new() { Quality = 0 };

        image.Save(stream, encoder);

        Assert.NotEqual(0, stream.Length);
        stream.Position = 0;
        using Image<Rgba32> decoded = Image.Load<Rgba32>(stream);
        Assert.Equal(image.Size, decoded.Size);
    }

    [Fact]
    public void LegacyJpegWritesNonSeekableStream()
    {
        using Image<Rgba32> image = new(1, 1);
        image[0, 0] = new Rgba32(10, 20, 30);
        using MemoryStream storage = new();
        using NonSeekableStream destination = new(storage);

        image.Save(destination, new HeifEncoder());

        Assert.NotEqual(0, storage.Length);
        storage.Position = 0;
        using Image<Rgba32> decoded = Image.Load<Rgba32>(storage);
        Assert.Equal(image.Size, decoded.Size);
    }

    [Fact]
    public void LegacyJpegWritesAtCurrentStreamPosition()
    {
        using Image<Rgba32> image = new(1, 1);
        image[0, 0] = new Rgba32(10, 20, 30);
        using MemoryStream stream = new();
        stream.Write([1, 2, 3, 4]);
        long fileStart = stream.Position;

        image.Save(stream, new HeifEncoder());

        stream.Position = fileStart;
        using Image<Rgba32> decoded = Image.Load<Rgba32>(stream);
        Assert.Equal(image.Size, decoded.Size);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void LegacyJpegHonorsSkipMetadataForEmbeddedProfiles(bool skipMetadata, bool expectedIccProfile)
    {
        using Image<Rgb24> image = new(8, 8);
        image.Metadata.IccProfile = new IccProfile(IccTestDataProfiles.ProfileRandomArray);
        using MemoryStream stream = new();
        HeifEncoder encoder = new() { SkipMetadata = skipMetadata };

        image.Save(stream, encoder);

        DecoderOptions preserveOptions = new() { ColorProfileHandling = ColorProfileHandling.Preserve };
        stream.Position = 0;
        using Image<Rgb24> decoded = Image.Load<Rgb24>(preserveOptions, stream);
        Assert.Equal(expectedIccProfile, decoded.Metadata.IccProfile is not null);
        if (expectedIccProfile)
        {
            Assert.Equal(
                IccTestDataProfiles.ProfileRandomArray,
                Assert.IsType<IccProfile>(decoded.Metadata.IccProfile).ToByteArray());
        }
    }

    [Fact]
    public void LegacyJpegRejectsLosslessEncoding()
    {
        using Image<Rgba32> image = new(1, 1);
        using MemoryStream stream = new();
        HeifEncoder encoder = new() { Lossless = true };

        Assert.Throws<NotSupportedException>(() => image.Save(stream, encoder));
        Assert.Equal(0, stream.Length);
    }

    [Theory]
    [InlineData(HeifBitDepth.Bit10)]
    [InlineData(HeifBitDepth.Bit12)]
    public void LegacyJpegRejectsHighBitDepth(HeifBitDepth bitDepth)
    {
        using Image<Rgba32> image = new(1, 1);
        using MemoryStream stream = new();
        HeifEncoder encoder = new() { BitDepth = bitDepth };

        Assert.Throws<NotSupportedException>(() => image.Save(stream, encoder));
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public void Av1LosslessRoundTripPreservesColorAndAlpha()
    {
        const int width = 8;
        const int height = 8;
        using Image<Rgba32> image = new(width, height);
        for (int row = 0; row < height; row++)
        {
            Span<Rgba32> pixels = image.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(row);
            for (int column = 0; column < width; column++)
            {
                pixels[column] = new Rgba32(
                    (byte)((column * 31) + row),
                    (byte)((row * 29) + column),
                    (byte)((column * 17) + (row * 11)),
                    (byte)((column * 23) + (row * 7)));
            }
        }

        // Identity-matrix 4:4:4 maps the packed RGB channels directly onto AV1 planes, so codec losslessness
        // can be asserted against the original pixels without a separate color-conversion tolerance.
        image.Metadata.CicpProfile = new CicpProfile(1, 13, 0, true);
        using MemoryStream stream = new();
        HeifEncoder encoder = new()
        {
            CompressionMethod = HeifCompressionMethod.Av1,
            Lossless = true,
            Effort = 0
        };

        image.Save(stream, encoder);
        byte[] file = stream.ToArray();
        Span<byte> colorPayload = GetItemPayload(file, 1);
        using Av1Decoder colorDecoder = new(Configuration.Default);
        using Image<Rgba32> colorImage = colorDecoder.Decode<Rgba32>(colorPayload);
        ObuSequenceHeader colorSequenceHeader = Assert.IsType<ObuSequenceHeader>(colorDecoder.SequenceHeader);
        ObuFrameHeader colorFrameHeader = Assert.IsType<ObuFrameHeader>(colorDecoder.FrameHeader);

        Assert.Equal(Av1ColorFormat.Yuv444, colorSequenceHeader.ColorConfig.GetColorFormat());
        Assert.Equal(0, colorFrameHeader.QuantizationParameters.BaseQIndex);
        Assert.True(colorFrameHeader.CodedLossless);
        Assert.True(colorFrameHeader.AllLossless);
        Assert.Equal(Av1TransformMode.Only4x4, colorFrameHeader.TransformMode);

        Span<byte> alphaPayload = GetItemPayload(file, 2);
        using Av1Decoder alphaDecoder = new(Configuration.Default);
        using Image<L8> alphaImage = alphaDecoder.Decode<L8>(alphaPayload);
        ObuFrameHeader alphaFrameHeader = Assert.IsType<ObuFrameHeader>(alphaDecoder.FrameHeader);
        Assert.Equal(0, alphaFrameHeader.QuantizationParameters.BaseQIndex);
        Assert.True(alphaFrameHeader.CodedLossless);

        stream.Position = 0;
        using Image<Rgba32> decoded = Image.Load<Rgba32>(stream);
        Assert.Empty(ImageComparer.Exact.CompareImages(image, decoded));

        string outputDirectory = Path.Combine(
            TestEnvironment.ActualOutputDirectoryFullPath,
            "Formats",
            "Heif",
            "Av1");

        Directory.CreateDirectory(outputDirectory);
        File.WriteAllBytes(Path.Combine(outputDirectory, "encoder-public-lossless-color.obu"), colorPayload.ToArray());
        File.WriteAllBytes(Path.Combine(outputDirectory, "encoder-public-lossless-alpha.obu"), alphaPayload.ToArray());
    }

    [Theory]
    [InlineData(HeifBitDepth.Bit10)]
    [InlineData(HeifBitDepth.Bit12)]
    public void Av1LosslessRoundTripPreservesHighBitDepthSourcePixels(HeifBitDepth bitDepth)
    {
        const int width = 8;
        const int height = 8;
        int codedMaximum = (1 << (int)bitDepth) - 1;
        using Image<Rgba64> image = new(width, height);
        for (int row = 0; row < height; row++)
        {
            Span<Rgba64> pixels = image.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(row);
            for (int column = 0; column < width; column++)
            {
                int red = ((column * 131) + (row * 37) + 1) & codedMaximum;
                int green = ((column * 61) + (row * 173) + 3) & codedMaximum;
                int blue = ((column * 211) + (row * 47) + 5) & codedMaximum;
                int alpha = ((column * 127) + (row * 89)) & codedMaximum;
                pixels[column] = new Rgba64(
                    ExpandToUShort(red, codedMaximum),
                    ExpandToUShort(green, codedMaximum),
                    ExpandToUShort(blue, codedMaximum),
                    ExpandToUShort(alpha, codedMaximum));
            }
        }

        // Full-range identity 4:4:4 preserves the requested sample lattice, isolating source precision from a
        // deliberately lossy color matrix or chroma subsampling step.
        image.Metadata.CicpProfile = new CicpProfile(1, 13, 0, true);
        using MemoryStream stream = new();
        HeifEncoder encoder = new()
        {
            CompressionMethod = HeifCompressionMethod.Av1,
            BitDepth = bitDepth,
            ChromaSubsampling = HeifChromaSubsampling.Yuv444,
            Lossless = true,
            Effort = 0
        };

        image.Save(stream, encoder);
        byte[] file = stream.ToArray();
        Span<byte> colorPayload = GetItemPayload(file, 1);
        Span<byte> alphaPayload = GetItemPayload(file, 2);
        stream.Position = 0;
        using Image<Rgba64> decoded = Image.Load<Rgba64>(stream);
        Assert.Empty(ImageComparer.Exact.CompareImages(image, decoded));

        string outputDirectory = Path.Combine(
            TestEnvironment.ActualOutputDirectoryFullPath,
            "Formats",
            "Heif",
            "Av1");

        Directory.CreateDirectory(outputDirectory);
        File.WriteAllBytes(
            Path.Combine(outputDirectory, $"encoder-public-lossless-{(int)bitDepth}b-color.obu"),
            colorPayload.ToArray());

        File.WriteAllBytes(
            Path.Combine(outputDirectory, $"encoder-public-lossless-{(int)bitDepth}b-alpha.obu"),
            alphaPayload.ToArray());
    }

    private static ushort ExpandToUShort(int sample, int maximum)
        => (ushort)(((sample * (long)ushort.MaxValue) + (maximum / 2)) / maximum);

    [Fact]
    public void Av1RejectsImageSequenceBeforeWritingOutput()
    {
        using Image<Rgb24> image = new(1, 1);
        image.Frames.AddFrame(image.Frames.RootFrame);
        using MemoryStream stream = new();
        HeifEncoder encoder = new() { CompressionMethod = HeifCompressionMethod.Av1 };

        Assert.Throws<NotSupportedException>(() => image.Save(stream, encoder));
        Assert.Equal(0, stream.Length);
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

    [Fact]
    public void Av1WritesStillImageWithRequiredBrandsAndProductionPayload()
    {
        const int width = 16;
        const int height = 16;
        using Image<Rgb24> image = new(width, height);
        for (int row = 0; row < height; row++)
        {
            Span<Rgb24> pixels = image.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(row);
            for (int column = 0; column < width; column++)
            {
                pixels[column] = new Rgb24(
                    (byte)(column * 11),
                    (byte)(row * 13),
                    (byte)((column + row) * 7));
            }
        }

        using MemoryStream stream = new();
        HeifEncoder encoder = new()
        {
            CompressionMethod = HeifCompressionMethod.Av1,
            Quality = 75,
            Effort = 0
        };

        image.Save(stream, encoder);
        byte[] file = stream.ToArray();
        ReadOnlySpan<byte> fileType = file.AsSpan(0, 28);
        Assert.Equal(28, BinaryPrimitives.ReadInt32BigEndian(fileType));
        Assert.Equal(Heif4CharCode.Ftyp, (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(fileType[4..]));
        Assert.Equal(Heif4CharCode.Avif, (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(fileType[8..]));
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(fileType[12..]));
        Assert.Equal(Heif4CharCode.Avif, (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(fileType[16..]));
        Assert.Equal(Heif4CharCode.Mif1, (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(fileType[20..]));
        Assert.Equal(Heif4CharCode.Miaf, (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(fileType[24..]));

        Span<byte> payload = GetItemPayload(file, 1);
        using Av1Decoder payloadDecoder = new(Configuration.Default);
        using Image<Rgb24> payloadImage = payloadDecoder.Decode<Rgb24>(payload);
        ObuFrameHeader frameHeader = Assert.IsType<ObuFrameHeader>(payloadDecoder.FrameHeader);
        Assert.Equal(64, frameHeader.QuantizationParameters.BaseQIndex);
        Assert.Equal(image.Size, payloadImage.Size);

        stream.Position = 0;
        using Image<Rgb24> decoded = Image.Load<Rgb24>(stream);
        HeifMetadata metadata = decoded.Metadata.GetHeifMetadata();
        Assert.Equal(image.Size, decoded.Size);
        Assert.Equal(HeifCompressionMethod.Av1, metadata.CompressionMethod);
        Assert.Equal(HeifBitDepth.Bit8, metadata.BitDepth);
        Assert.False(metadata.IsMonochrome);
        Assert.False(metadata.HasAlpha);
        CicpProfile colorProfile = Assert.IsType<CicpProfile>(decoded.Metadata.CicpProfile);
        Assert.Equal(CicpMatrixCoefficients.ItuRBt601_7_525, colorProfile.MatrixCoefficients);
        Assert.False(colorProfile.FullRange);

        string outputDirectory = Path.Combine(
            TestEnvironment.ActualOutputDirectoryFullPath,
            "Formats",
            "Heif",
            "Av1");

        Directory.CreateDirectory(outputDirectory);
        File.WriteAllBytes(Path.Combine(outputDirectory, "encoder-public-16x16-8b-420.avif"), file);
        File.WriteAllBytes(Path.Combine(outputDirectory, "encoder-public-16x16-8b-420.obu"), payload.ToArray());
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
            CompressionMethod = HeifCompressionMethod.Av1,
            Quality = 75,
            AlphaQuality = 100,
            Effort = 0
        };

        image.Save(stream, encoder);
        byte[] file = stream.ToArray();
        Span<byte> colorPayload = GetItemPayload(file, 1);
        Span<byte> alphaPayload = GetItemPayload(file, 2);
        using Av1Decoder colorDecoder = new(Configuration.Default);
        using Image<Rgba32> colorImage = colorDecoder.Decode<Rgba32>(colorPayload);
        ObuFrameHeader colorFrameHeader = Assert.IsType<ObuFrameHeader>(colorDecoder.FrameHeader);
        Assert.Equal(64, colorFrameHeader.QuantizationParameters.BaseQIndex);

        using Av1Decoder alphaDecoder = new(Configuration.Default);
        using Image<L8> alphaImage = alphaDecoder.Decode<L8>(alphaPayload);
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

        string outputDirectory = Path.Combine(
            TestEnvironment.ActualOutputDirectoryFullPath,
            "Formats",
            "Heif",
            "Av1");

        Directory.CreateDirectory(outputDirectory);
        File.WriteAllBytes(Path.Combine(outputDirectory, "encoder-public-alpha-color.obu"), colorPayload.ToArray());
        File.WriteAllBytes(Path.Combine(outputDirectory, "encoder-public-alpha-auxiliary.obu"), alphaPayload.ToArray());
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
            CompressionMethod = HeifCompressionMethod.Av1,
            BitDepth = bitDepth,
            ChromaSubsampling = chromaSubsampling,
            Effort = 0
        };

        image.Save(stream, encoder);
        byte[] file = stream.ToArray();
        Span<byte> payload = GetItemPayload(file, 1);
        using Av1Decoder payloadDecoder = new(Configuration.Default);
        using Image<Rgb48> payloadImage = payloadDecoder.Decode<Rgb48>(payload);
        ObuSequenceHeader sequenceHeader = Assert.IsType<ObuSequenceHeader>(payloadDecoder.SequenceHeader);
        Assert.Equal(expectedAv1BitDepth, sequenceHeader.ColorConfig.BitDepth);
        Assert.Equal(expectedColorFormat, sequenceHeader.ColorConfig.GetColorFormat());
        Assert.Equal(image.Size, payloadImage.Size);

        stream.Position = 0;
        using Image<Rgb48> decoded = Image.Load<Rgb48>(stream);
        HeifMetadata metadata = decoded.Metadata.GetHeifMetadata();
        Assert.Equal(bitDepth, metadata.BitDepth);
        Assert.Equal(chromaSubsampling == HeifChromaSubsampling.Monochrome, metadata.IsMonochrome);

        string outputDirectory = Path.Combine(
            TestEnvironment.ActualOutputDirectoryFullPath,
            "Formats",
            "Heif",
            "Av1");

        Directory.CreateDirectory(outputDirectory);
        File.WriteAllBytes(
            Path.Combine(outputDirectory, $"encoder-public-8x8-{(int)bitDepth}b-{chromaSubsampling}.obu"),
            payload.ToArray());
    }

    [Fact]
    public void Av1UsesMetadataBitDepthWhenOptionIsNull()
    {
        using Image<Rgb24> image = new(8, 8);
        image.Metadata.GetHeifMetadata().BitDepth = HeifBitDepth.Bit10;
        using MemoryStream stream = new();
        HeifEncoder encoder = new()
        {
            CompressionMethod = HeifCompressionMethod.Av1,
            ChromaSubsampling = HeifChromaSubsampling.Yuv444,
            Effort = 0
        };

        image.Save(stream, encoder);
        byte[] file = stream.ToArray();
        Span<byte> payload = GetItemPayload(file, 1);
        using Av1Decoder payloadDecoder = new(Configuration.Default);
        using Image<Rgb48> payloadImage = payloadDecoder.Decode<Rgb48>(payload);
        ObuSequenceHeader sequenceHeader = Assert.IsType<ObuSequenceHeader>(payloadDecoder.SequenceHeader);
        Assert.Equal(Av1BitDepth.TenBit, sequenceHeader.ColorConfig.BitDepth);
    }

    [Fact]
    public void Av1SanitizesIncompatibleIdentityMatrixWithoutMutatingSourceMetadata()
    {
        using Image<Rgb24> image = new(8, 8);
        CicpProfile sourceProfile = new(1, 13, 0, false);
        image.Metadata.CicpProfile = sourceProfile;
        using MemoryStream stream = new();
        HeifEncoder encoder = new()
        {
            CompressionMethod = HeifCompressionMethod.Av1,
            ChromaSubsampling = HeifChromaSubsampling.Yuv420,
            Effort = 0
        };

        image.Save(stream, encoder);
        Assert.Same(sourceProfile, image.Metadata.CicpProfile);
        Assert.Equal(CicpMatrixCoefficients.Identity, sourceProfile.MatrixCoefficients);
        Assert.False(sourceProfile.FullRange);

        stream.Position = 0;
        using Image<Rgb24> decoded = Image.Load<Rgb24>(stream);
        CicpProfile decodedProfile = Assert.IsType<CicpProfile>(decoded.Metadata.CicpProfile);
        Assert.Equal(CicpMatrixCoefficients.ItuRBt601_7_525, decodedProfile.MatrixCoefficients);
        Assert.False(decodedProfile.FullRange);
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
            CompressionMethod = HeifCompressionMethod.Av1,
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
            CompressionMethod = HeifCompressionMethod.Av1,
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
            CompressionMethod = HeifCompressionMethod.Av1,
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
            CompressionMethod = HeifCompressionMethod.Av1,
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
        using AutoExpandingMemory<byte> memory = new(Configuration.Default, 16);
        int length = HeifEncoderCore.WriteItemPropertiesBox(memory, 0, items);
        ReadOnlySpan<byte> propertyBox = memory.GetSpan(length);

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

        using AutoExpandingMemory<byte> memory = new(Configuration.Default, 16);
        int length = HeifEncoderCore.WriteItemPropertiesBox(memory, 0, items);
        ReadOnlySpan<byte> propertyBox = memory.GetSpan(length);
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

        using AutoExpandingMemory<byte> memory = new(Configuration.Default, 16);
        int length = HeifEncoderCore.WriteItemPropertiesBox(memory, 0, items);
        ReadOnlySpan<byte> propertyBox = memory.GetSpan(length);
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

    [Fact]
    public void LegacyJpegEncodingDoesNotMutateSourceHeifMetadata()
    {
        using Image<Rgba32> image = new(1, 1);
        image[0, 0] = new Rgba32(10, 20, 30, 255);
        HeifMetadata metadata = image.Metadata.GetHeifMetadata();
        metadata.CompressionMethod = HeifCompressionMethod.Av1;
        using MemoryStream stream = new();
        HeifEncoder encoder = new();

        image.Save(stream, encoder);

        Assert.Same(metadata, image.Metadata.GetHeifMetadata());
        Assert.Equal(HeifCompressionMethod.Av1, metadata.CompressionMethod);
    }

    [Theory]
    [WithFile(TestImages.Heif.IrvineAvif, PixelTypes.Rgba32, HeifCompressionMethod.LegacyJpeg)]
    public static void Encode<TPixel>(TestImageProvider<TPixel> provider, HeifCompressionMethod compressionMethod)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(new MagickReferenceDecoder(HeifFormat.Instance));
        using MemoryStream stream = new();
        HeifEncoder encoder = new();
        image.Save(stream, encoder);
        stream.Position = 0;

        ImageInfo imageInfo = Image.Identify(stream);
        Assert.Equal(image.Size, imageInfo.Size);

        stream.Position = 0;
        using Image<TPixel> encodedImage = Image.Load<TPixel>(stream);
        HeifMetadata heifMetadata = encodedImage.Metadata.GetHeifMetadata();

        ImageComparer.Exact.CompareImages(image, encodedImage);
        Assert.Equal(compressionMethod, heifMetadata.CompressionMethod);
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
