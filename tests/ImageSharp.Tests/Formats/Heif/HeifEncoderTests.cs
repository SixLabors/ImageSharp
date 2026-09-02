// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Text;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

namespace SixLabors.ImageSharp.Tests.Formats.Heif;

[Trait("Format", "Heif")]
[ValidateDisposedMemoryAllocations]
public class HeifEncoderTests
{
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
    public void Av1RejectsEncodingBeforeWritingOutput()
    {
        using Image<Rgba32> image = new(1, 1);
        using MemoryStream stream = new();
        HeifEncoder encoder = new() { CompressionMethod = HeifCompressionMethod.Av1 };

        Assert.Throws<NotSupportedException>(() => image.Save(stream, encoder));
        Assert.Equal(0, stream.Length);
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
}
