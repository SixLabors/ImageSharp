// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Ani;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Ico;
using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Formats.Icon.Ico;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using static SixLabors.ImageSharp.Tests.TestImages.Ani;

namespace SixLabors.ImageSharp.Tests.Formats.Ani;

[Trait("Format", "Ani")]
[ValidateDisposedMemoryAllocations]
public class AniDecoderTests
{
    /// <summary>
    /// Verifies that ANI animation steps and embedded CUR resolution variants are flattened with their ANI metadata.
    /// The sequence number of each frame is the stored frame index plus 1. Help.ani plays its stored frames in the
    /// sequence 0, 1, 2, 1. The other files play each stored frame once, in order.
    /// </summary>
    [Theory]
    [WithFile(Work, PixelTypes.Rgba32, 17, 1, 6U, 6U, null)]
    [WithFile(MultiFramesInEveryIconChunk, PixelTypes.Rgba32, 54, 3, 3U, 3U, null)]
    [WithFile(Help, PixelTypes.Rgba32, 4, 1, 10U, 12U, new[] { 0, 1, 2, 1 })]
    public void AniDecoder_Decode(
        TestImageProvider<Rgba32> provider,
        int expectedFrameCount,
        int variantsPerStep,
        uint expectedDisplayRate,
        uint expectedFrameDelay,
        int[] sequence)
    {
        using Image<Rgba32> image = provider.GetImage(AniDecoder.Instance);

        Assert.Equal(expectedFrameCount, image.Frames.Count);
        Assert.Equal(expectedDisplayRate, image.Metadata.GetAniMetadata().DisplayRate);

        for (int i = 0; i < image.Frames.Count; i++)
        {
            AniFrameMetadata metadata = image.Frames[i].Metadata.GetAniMetadata();

            int step = i / variantsPerStep;
            Assert.Equal((sequence is null ? step : sequence[step]) + 1, metadata.SequenceNumber);
            Assert.Equal(expectedFrameDelay, metadata.FrameDelay);
            Assert.Equal(AniFrameFormat.Cur, metadata.FrameFormat);
            Assert.NotEqual(0, (int)metadata.BmpBitsPerPixel);
        }
    }

    /// <summary>
    /// Verifies that identification exposes the same flattened ANI frame structure without decoding pixels.
    /// </summary>
    [Theory]
    [InlineData(Work, 17)]
    [InlineData(MultiFramesInEveryIconChunk, 54)]
    [InlineData(Help, 4)]
    public void AniDecoder_Identify(string path, int expectedFrameCount)
    {
        TestFile file = TestFile.Create(path);
        using MemoryStream stream = new(file.Bytes, false);

        ImageInfo info = AniDecoder.Instance.Identify(DecoderOptions.Default, stream);

        Assert.Equal(expectedFrameCount, info.FrameMetadataCollection.Count);

        for (int i = 0; i < info.FrameMetadataCollection.Count; i++)
        {
            Assert.True(info.FrameMetadataCollection[i].GetAniMetadata().SequenceNumber > 0);
        }
    }

    /// <summary>
    /// Verifies that invalid sequence metadata follows the ancillary-segment integrity policy.
    /// </summary>
    [Fact]
    public void AniDecoder_InvalidSequenceReference_FollowsIntegrityHandling()
    {
        byte[] data = [.. TestFile.Create(Help).Bytes];
        int sequenceOffset = data.AsSpan().IndexOf("seq "u8);
        Assert.True(sequenceOffset >= 0);

        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(sequenceOffset + AniConstants.ChunkHeaderSize), uint.MaxValue);

        using MemoryStream strictStream = new(data, false);
        DecoderOptions strict = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() => AniDecoder.Instance.Decode<Rgba32>(strict, strictStream));

        using MemoryStream ignoreStream = new(data, false);
        DecoderOptions ignore = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.IgnoreAncillary };
        using Image<Rgba32> image = AniDecoder.Instance.Decode<Rgba32>(ignore, ignoreStream);

        Assert.Equal(3, image.Frames.Count);
    }

    /// <summary>
    /// Verifies that a file without AF_ICON decodes its raw frames as icon bitmaps: the color data, then the AND mask. The height covers both.
    /// The decoder reports each raw frame as a CUR frame that holds a bitmap of the same depth.
    /// </summary>
    [Fact]
    public void AniDecoder_RawBitmapFrame_DecodesIconBitmap()
    {
        using Image<Rgba32> source = NearColorPalette.CreateImage();
        IcoFrameMetadata icoMetadata = source.Frames.RootFrame.Metadata.GetIcoMetadata();
        icoMetadata.Compression = IconFrameCompression.Bmp;
        icoMetadata.BmpBitsPerPixel = BmpBitsPerPixel.Bit8;
        icoMetadata.ColorTable = NearColorPalette.Palette;

        // The bitmap of an ICO entry is an icon bitmap. The directory entry gives its size and offset.
        using MemoryStream icoStream = new();
        source.Save(icoStream, new IcoEncoder());
        ReadOnlySpan<byte> ico = icoStream.GetBuffer().AsSpan(0, (int)icoStream.Length);
        int bitmapSize = BinaryPrimitives.ReadInt32LittleEndian(ico[(IconDir.Size + IconDirEntry.Size - (2 * sizeof(uint)))..]);
        int bitmapOffset = BinaryPrimitives.ReadInt32LittleEndian(ico[(IconDir.Size + IconDirEntry.Size - sizeof(uint))..]);
        ReadOnlySpan<byte> bitmap = ico.Slice(bitmapOffset, bitmapSize);

        // RIFF('ACON' anih(header) LIST('fram' icon(bitmap))). The anih flags are 0, so AF_ICON is clear.
        int frameListSize = 4 + AniConstants.ChunkHeaderSize + bitmap.Length + (bitmap.Length & 1);
        int riffSize = 4 + AniConstants.ChunkHeaderSize + AniHeader.Size + AniConstants.ChunkHeaderSize + frameListSize;
        using MemoryStream ani = new();
        byte[] value = new byte[4];
        void WriteUInt32(uint v)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(value, v);
            ani.Write(value);
        }

        ani.Write("RIFF"u8);
        WriteUInt32((uint)riffSize);
        ani.Write("ACONanih"u8);
        WriteUInt32(AniHeader.Size);
        foreach (uint field in new uint[] { AniHeader.Size, 1, 1, (uint)source.Width, (uint)source.Height, 8, 1, 6, 0 })
        {
            WriteUInt32(field);
        }

        ani.Write("LIST"u8);
        WriteUInt32((uint)frameListSize);
        ani.Write("framicon"u8);
        WriteUInt32((uint)bitmap.Length);
        ani.Write(bitmap);
        if ((bitmap.Length & 1) != 0)
        {
            ani.WriteByte(0);
        }

        ani.Position = 0;
        using Image<Rgba32> decoded = Image.Load<Rgba32>(ani);
        AniFrameMetadata metadata = decoded.Frames.RootFrame.Metadata.GetAniMetadata();

        Assert.Equal(AniFrameFormat.Cur, metadata.FrameFormat);
        Assert.Equal(IconFrameCompression.Bmp, metadata.Compression);
        Assert.Equal(BmpBitsPerPixel.Bit8, metadata.BmpBitsPerPixel);
        ImageComparer.Exact.VerifySimilarity(source, decoded);

        using MemoryStream encoded = new();
        decoded.Save(encoded, new AniEncoder());
        encoded.Position = 0;
        using Image<Rgba32> roundTrip = Image.Load<Rgba32>(encoded);
        AniFrameMetadata roundTripMetadata = roundTrip.Frames.RootFrame.Metadata.GetAniMetadata();

        Assert.Equal(AniFrameFormat.Cur, roundTripMetadata.FrameFormat);
        Assert.Equal(IconFrameCompression.Bmp, roundTripMetadata.Compression);
        Assert.Equal(BmpBitsPerPixel.Bit8, roundTripMetadata.BmpBitsPerPixel);
        ImageComparer.Exact.VerifySimilarity(source, roundTrip);
    }

    /// <summary>
    /// Verifies that ignored corrupt resources retain their sequence-table slot.
    /// </summary>
    [Fact]
    public void AniDecoder_UnsupportedResource_FollowsIntegrityHandling()
    {
        byte[] data = [.. TestFile.Create(Work).Bytes];
        int resourceOffset = data.AsSpan().IndexOf("icon"u8);
        Assert.True(resourceOffset >= 0);

        int iconTypeOffset = resourceOffset + AniConstants.ChunkHeaderSize + sizeof(ushort);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(iconTypeOffset), ushort.MaxValue);

        using MemoryStream strictStream = new(data, false);
        DecoderOptions strict = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() => AniDecoder.Instance.Decode<Rgba32>(strict, strictStream));

        using MemoryStream ignoreStream = new(data, false);
        DecoderOptions ignore = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.IgnoreImageData };
        using Image<Rgba32> image = AniDecoder.Instance.Decode<Rgba32>(ignore, ignoreStream);

        Assert.Equal(16, image.Frames.Count);
        Assert.Equal(2, image.Frames.RootFrame.Metadata.GetAniMetadata().SequenceNumber);
    }

    /// <summary>
    /// Verifies that a file that ends inside its last frame resource decodes with the default options. Every animation
    /// step is kept. The steps with complete resources match the decode of the full file, and the last step keeps the
    /// rows that the data holds. In strict mode the decoder throws.
    /// </summary>
    [Fact]
    public void AniDecoder_TruncatedLastResource_KeepsEveryStep()
    {
        byte[] full = TestFile.Create(Work).Bytes;

        // The frame list starts with its 'fram' list type. Walk its chunks to find the last frame resource.
        int chunkOffset = full.AsSpan().IndexOf("fram"u8) + sizeof(uint);
        Assert.True(chunkOffset > sizeof(uint));

        int lastResourceOffset = chunkOffset;
        int lastResourceSize = 0;
        while (chunkOffset + AniConstants.ChunkHeaderSize <= full.Length)
        {
            int size = (int)BinaryPrimitives.ReadUInt32LittleEndian(full.AsSpan(chunkOffset + sizeof(uint)));
            lastResourceOffset = chunkOffset;
            lastResourceSize = size;
            chunkOffset += AniConstants.ChunkHeaderSize + size + (size & 1);
        }

        // End the file in the middle of the payload of the last frame resource.
        byte[] data = full[..(lastResourceOffset + AniConstants.ChunkHeaderSize + (lastResourceSize / 2))];

        using MemoryStream strictStream = new(data, false);
        DecoderOptions strict = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() => AniDecoder.Instance.Decode<Rgba32>(strict, strictStream));

        // Each step of the file plays one stored frame in order, so only the last output frame uses the truncated
        // resource. That resource is an icon file with one entry. Its directory entry stores BytesInRes at byte 8 and
        // ImageOffset at byte 12. The last frame keeps the rows of the entry bitmap that the data holds.
        using Image<Rgba32> expected = Image.Load<Rgba32>(full);
        int resourceStart = lastResourceOffset + AniConstants.ChunkHeaderSize;
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(full.AsSpan(resourceStart + 4)));
        int bitmapLength = BinaryPrimitives.ReadInt32LittleEndian(full.AsSpan(resourceStart + IconDir.Size + 8));
        int bitmapStart = resourceStart + BinaryPrimitives.ReadInt32LittleEndian(full.AsSpan(resourceStart + IconDir.Size + 12));
        IcoDecoderTests.ClearPixelsAfterResourceEnd(
            expected.Frames[expected.Frames.Count - 1],
            full.AsSpan(bitmapStart, bitmapLength),
            data.Length - bitmapStart);

        using Image<Rgba32> actual = Image.Load<Rgba32>(data);
        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }

    /// <summary>
    /// Verifies that a frame resource that extends past its frame list is damaged image data. In the Strict and IgnoreAncillary modes, the decoder throws. In the
    /// IgnoreImageData mode, the decoder keeps the steps that play the resources before the damaged resource.
    /// </summary>
    [Fact]
    public void AniDecoder_ResourcePastFrameListEnd_KeepsEarlierSteps()
    {
        byte[] full = TestFile.Create(Work).Bytes;

        // Bytes after the RIFF container make sure that the frame list does not end at the end of the stream. Thus the decoder cannot read the damaged chunk as
        // a truncated resource.
        byte[] data = [.. full, 0, 0, 0, 0, 0, 0, 0, 0];

        // The frame list starts with its 'fram' list type. The list size is at 8 bytes before the list type.
        int listTypeOffset = data.AsSpan().IndexOf("fram"u8);
        Assert.True(listTypeOffset > sizeof(uint));
        int listEnd = listTypeOffset + (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(listTypeOffset - sizeof(uint)));

        // Walk the resources of the list to the third resource. Then set its size so that it ends 2 bytes past the list end.
        const int damagedResource = 2;
        int chunkOffset = listTypeOffset + sizeof(uint);
        for (int i = 0; i < damagedResource; i++)
        {
            int size = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(chunkOffset + sizeof(uint)));
            chunkOffset += AniConstants.ChunkHeaderSize + size + (size & 1);
        }

        Assert.True(data.AsSpan(chunkOffset, 4).SequenceEqual("icon"u8));
        int damagedSize = listEnd - (chunkOffset + AniConstants.ChunkHeaderSize) + 2;
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(chunkOffset + sizeof(uint)), (uint)damagedSize);

        foreach (SegmentIntegrityHandling handling in new[] { SegmentIntegrityHandling.Strict, SegmentIntegrityHandling.IgnoreAncillary })
        {
            using MemoryStream stream = new(data, false);
            DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = handling };
            Assert.Throws<InvalidImageContentException>(() => AniDecoder.Instance.Decode<Rgba32>(options, stream));
        }

        // The sequence number of each step is the 1-based index of its resource. The steps that play the resources before the damaged resource keep their
        // pixels. The decoder drops the other steps.
        using Image<Rgba32> expected = Image.Load<Rgba32>(full);
        for (int i = expected.Frames.Count - 1; i >= 0; i--)
        {
            if (expected.Frames[i].Metadata.GetAniMetadata().SequenceNumber > damagedResource)
            {
                expected.Frames.RemoveFrame(i);
            }
        }

        Assert.True(expected.Frames.Count > 0);

        using MemoryStream ignoreStream = new(data, false);
        DecoderOptions ignore = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.IgnoreImageData };
        using Image<Rgba32> actual = AniDecoder.Instance.Decode<Rgba32>(ignore, ignoreStream);
        Assert.Equal(expected.Frames.Count, actual.Frames.Count);
        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }

    /// <summary>
    /// Verifies that a malformed rate chunk follows the ancillary-segment integrity policy.
    /// </summary>
    [Fact]
    public void AniDecoder_InvalidRateChunk_FollowsIntegrityHandling()
    {
        byte[] source = [.. TestFile.Create(Help).Bytes];
        int rateOffset = source.AsSpan().IndexOf("rate"u8);
        Assert.True(rateOffset >= 0);

        int rateSizeOffset = rateOffset + sizeof(uint);
        int rateSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(rateSizeOffset));
        int rateEnd = rateOffset + AniConstants.ChunkHeaderSize + rateSize;
        byte[] data = new byte[source.Length + 2];
        source.AsSpan(0, rateEnd).CopyTo(data);
        source.AsSpan(rateEnd).CopyTo(data.AsSpan(rateEnd + 2));
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(rateSizeOffset), (uint)rateSize + 2);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(sizeof(uint)), (uint)data.Length - 8);

        using MemoryStream defaultStream = new(data, false);
        using Image<Rgba32> image = AniDecoder.Instance.Decode<Rgba32>(DecoderOptions.Default, defaultStream);

        Assert.Equal(4, image.Frames.Count);
        foreach (ImageFrame<Rgba32> frame in image.Frames)
        {
            Assert.Equal(10U, frame.Metadata.GetAniMetadata().FrameDelay);
        }

        using MemoryStream strictStream = new(data, false);
        DecoderOptions strict = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() => AniDecoder.Instance.Decode<Rgba32>(strict, strictStream));
    }

    /// <summary>
    /// Verifies that oversized control arrays are rejected before allocation and follow ancillary integrity handling.
    /// </summary>
    /// <param name="sequence"><see langword="true"/> to append a sequence chunk; otherwise, a rate chunk.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AniDecoder_OversizedControlChunk_FollowsIntegrityHandling(bool sequence)
    {
        byte[] source = [.. TestFile.Create(Help).Bytes];
        int chunkOffset = (source.Length + 1) & ~1;
        int payloadSize = AniConstants.MaxAncillaryChunkSize + sizeof(uint);
        byte[] data = new byte[chunkOffset + AniConstants.ChunkHeaderSize + payloadSize];
        source.CopyTo(data, 0);

        ReadOnlySpan<byte> identifier = sequence ? "seq "u8 : "rate"u8;
        identifier.CopyTo(data.AsSpan(chunkOffset));
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(chunkOffset + sizeof(uint)), (uint)payloadSize);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(sizeof(uint)), (uint)data.Length - AniConstants.ChunkHeaderSize);

        using MemoryStream defaultStream = new(data, false);
        using Image<Rgba32> image = AniDecoder.Instance.Decode<Rgba32>(DecoderOptions.Default, defaultStream);

        Assert.Equal(4, image.Frames.Count);

        using MemoryStream strictStream = new(data, false);
        DecoderOptions strict = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() => AniDecoder.Instance.Decode<Rgba32>(strict, strictStream));
    }

    /// <summary>
    /// Verifies that oversized information text is rejected before allocation and follows ancillary integrity handling.
    /// </summary>
    [Fact]
    public void AniDecoder_OversizedInformationText_FollowsIntegrityHandling()
    {
        byte[] source = [.. TestFile.Create(Help).Bytes];
        int listOffset = (source.Length + 1) & ~1;
        int textSize = AniConstants.MaxAncillaryChunkSize + 1;
        int paddedTextSize = textSize + (textSize & 1);
        int listSize = sizeof(uint) + AniConstants.ChunkHeaderSize + paddedTextSize;
        byte[] data = new byte[listOffset + AniConstants.ChunkHeaderSize + listSize];
        source.CopyTo(data, 0);

        "LIST"u8.CopyTo(data.AsSpan(listOffset));
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(listOffset + sizeof(uint)), (uint)listSize);
        "INFO"u8.CopyTo(data.AsSpan(listOffset + AniConstants.ChunkHeaderSize));
        int textOffset = listOffset + AniConstants.ChunkHeaderSize + sizeof(uint);
        "INAM"u8.CopyTo(data.AsSpan(textOffset));
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(textOffset + sizeof(uint)), (uint)textSize);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(sizeof(uint)), (uint)data.Length - AniConstants.ChunkHeaderSize);

        using MemoryStream defaultStream = new(data, false);
        using Image<Rgba32> image = AniDecoder.Instance.Decode<Rgba32>(DecoderOptions.Default, defaultStream);

        Assert.Equal(4, image.Frames.Count);

        using MemoryStream strictStream = new(data, false);
        DecoderOptions strict = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() => AniDecoder.Instance.Decode<Rgba32>(strict, strictStream));
    }
}
