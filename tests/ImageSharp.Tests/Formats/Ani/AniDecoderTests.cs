// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Ani;
using SixLabors.ImageSharp.PixelFormats;
using static SixLabors.ImageSharp.Tests.TestImages.Ani;

namespace SixLabors.ImageSharp.Tests.Formats.Ani;

[Trait("Format", "Ani")]
[ValidateDisposedMemoryAllocations]
public class AniDecoderTests
{
    /// <summary>
    /// Verifies that ANI animation steps and embedded CUR resolution variants are flattened with their ANI metadata.
    /// </summary>
    [Theory]
    [WithFile(Work, PixelTypes.Rgba32, 17, 1, 6U, 6U)]
    [WithFile(MultiFramesInEveryIconChunk, PixelTypes.Rgba32, 54, 3, 3U, 3U)]
    [WithFile(Help, PixelTypes.Rgba32, 4, 1, 10U, 12U)]
    public void AniDecoder_Decode(
        TestImageProvider<Rgba32> provider,
        int expectedFrameCount,
        int variantsPerStep,
        uint expectedDisplayRate,
        uint expectedFrameDelay)
    {
        using Image<Rgba32> image = provider.GetImage(AniDecoder.Instance);

        Assert.Equal(expectedFrameCount, image.Frames.Count);
        Assert.Equal(expectedDisplayRate, image.Metadata.GetAniMetadata().DisplayRate);

        for (int i = 0; i < image.Frames.Count; i++)
        {
            AniFrameMetadata metadata = image.Frames[i].Metadata.GetAniMetadata();

            Assert.Equal((i / variantsPerStep) + 1, metadata.SequenceNumber);
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
        DecoderOptions strict = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() => AniDecoder.Instance.Decode<Rgba32>(strict, strictStream));

        using MemoryStream ignoreStream = new(data, false);
        DecoderOptions ignore = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.IgnoreAncillary };
        using Image<Rgba32> image = AniDecoder.Instance.Decode<Rgba32>(ignore, ignoreStream);

        Assert.Equal(3, image.Frames.Count);
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
        DecoderOptions strict = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() => AniDecoder.Instance.Decode<Rgba32>(strict, strictStream));

        using MemoryStream ignoreStream = new(data, false);
        DecoderOptions ignore = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.IgnoreImageData };
        using Image<Rgba32> image = AniDecoder.Instance.Decode<Rgba32>(ignore, ignoreStream);

        Assert.Equal(16, image.Frames.Count);
        Assert.Equal(2, image.Frames.RootFrame.Metadata.GetAniMetadata().SequenceNumber);
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
        DecoderOptions strict = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
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
        DecoderOptions strict = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
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
        DecoderOptions strict = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() => AniDecoder.Instance.Decode<Rgba32>(strict, strictStream));
    }
}
