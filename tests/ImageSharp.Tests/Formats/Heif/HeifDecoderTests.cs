// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Heif;

[Trait("Format", "Heif")]
[ValidateDisposedMemoryAllocations]
public class HeifDecoderTests
{
    private const uint UnknownBoxType = 0x74657374U;

    [Theory]
    [InlineData(TestImages.Heif.Image1, HeifCompressionMethod.Hevc, 3992, 2992)]
    [InlineData(TestImages.Heif.Sample640x427, HeifCompressionMethod.Hevc, 640, 428)]
    [InlineData(TestImages.Heif.FujiFilmHif, HeifCompressionMethod.LegacyJpeg, 7728, 5152)]
    [InlineData(TestImages.Heif.IrvineAvif, HeifCompressionMethod.Av1, 480, 640)]
    public void Identify(string imagePath, HeifCompressionMethod compressionMethod, int width, int height)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);

        ImageInfo imageInfo = Image.Identify(stream);
        HeifMetadata heicMetadata = imageInfo.Metadata.GetHeifMetadata();

        Assert.NotNull(imageInfo);
        Assert.Equal(HeifFormat.Instance, imageInfo.Metadata.DecodedImageFormat);
        Assert.Equal(compressionMethod, heicMetadata.CompressionMethod);
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

    [Fact]
    public void DecodeIgnoresUnknownTopLevelBox()
    {
        byte[] data = CreateEncodedContainer();
        data = InsertBytes(data, data.Length, CreateUnknownBox());

        using Image<Rgba32> image = Image.Load<Rgba32>(data);

        Assert.Equal(new Size(2, 3), image.Size);
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
    [InlineData(Heif4CharCode.Hevm)]
    [InlineData(Heif4CharCode.Hevs)]
    [InlineData(Heif4CharCode.Avis)]
    [InlineData(Heif4CharCode.Jpgs)]
    public void DetectorRejectsSequenceMajorBrand(Heif4CharCode brand)
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

    private static byte[] CreateEncodedContainer()
    {
        using Image<Rgba32> image = new(2, 3);
        using MemoryStream stream = new();
        image.Save(stream, new HeifEncoder());
        return stream.ToArray();
    }

    private static byte[] CreateContainerWithUnknownProperty(bool essential)
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
        data = InsertBytes(data, ipcoOffset + ipcoSize, CreateUnknownBox());
        IncrementBoxSize(data, metaOffset, 8);
        IncrementBoxSize(data, iprpOffset, 8);
        IncrementBoxSize(data, ipcoOffset, 8);
        ipmaOffset += 8;

        // The generated container has one item with one property association; append the unknown property to that entry.
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

    private static byte[] CreateUnknownBox()
    {
        byte[] box = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(box, (uint)box.Length);
        BinaryPrimitives.WriteUInt32BigEndian(box.AsSpan(4), UnknownBoxType);
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
}
