// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats.Ani;
using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using static SixLabors.ImageSharp.Tests.TestImages.Ani;

namespace SixLabors.ImageSharp.Tests.Formats.Ani;

[Trait("Format", "Ani")]
[ValidateDisposedMemoryAllocations]
public class AniEncoderTests
{
    /// <summary>
    /// Verifies that ANI resources, including multi-resolution CUR resources, survive an encode/decode round trip.
    /// </summary>
    [Theory]
    [WithFile(Work, PixelTypes.Rgba32)]
    [WithFile(MultiFramesInEveryIconChunk, PixelTypes.Rgba32)]
    [WithFile(Help, PixelTypes.Rgba32)]
    public void AniEncoder_RoundTrips(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage(AniDecoder.Instance);
        using MemoryStream stream = new();

        image.Save(stream, new AniEncoder());

        // The RIFF size covers everything after its identifier and size field.
        Assert.Equal(stream.Length - 8, BinaryPrimitives.ReadUInt32LittleEndian(stream.GetBuffer().AsSpan(4, sizeof(uint))));

        stream.Position = 0;
        using Image<Rgba32> decoded = Image.Load<Rgba32>(stream);

        ImageComparer.Exact.VerifySimilarity(image, decoded);
        Assert.Equal(image.Frames.Count, decoded.Frames.Count);

        for (int i = 0; i < image.Frames.Count; i++)
        {
            AniFrameMetadata expected = image.Frames[i].Metadata.GetAniMetadata();
            AniFrameMetadata actual = decoded.Frames[i].Metadata.GetAniMetadata();

            Assert.Equal(expected.SequenceNumber, actual.SequenceNumber);
            Assert.Equal(expected.FrameDelay, actual.FrameDelay);
            Assert.Equal(expected.FrameFormat, actual.FrameFormat);
            Assert.Equal(expected.EncodingWidth, actual.EncodingWidth);
            Assert.Equal(expected.EncodingHeight, actual.EncodingHeight);
            Assert.Equal(expected.Compression, actual.Compression);
            Assert.Equal(expected.BmpBitsPerPixel, actual.BmpBitsPerPixel);
            Assert.Equal(expected.HotspotX, actual.HotspotX);
            Assert.Equal(expected.HotspotY, actual.HotspotY);
            Assert.Equal(expected.ColorTable?.ToArray(), actual.ColorTable?.ToArray());
        }
    }

    /// <summary>
    /// Verifies that per-step rates and RIFF information metadata are emitted and decoded.
    /// </summary>
    [Theory]
    [WithFile(Work, PixelTypes.Rgba32)]
    public void AniEncoder_WritesVariableRatesAndInformation(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage(AniDecoder.Instance);
        AniMetadata imageMetadata = image.Metadata.GetAniMetadata();
        imageMetadata.Name = "ImageSharp ANI";
        imageMetadata.Artist = "Six Labors";

        for (int i = 0; i < image.Frames.Count; i++)
        {
            image.Frames[i].Metadata.GetAniMetadata().FrameDelay = (uint)(i + 1);
        }

        using MemoryStream stream = new();
        image.Save(stream, new AniEncoder());

        Assert.Equal(stream.Length - 8, BinaryPrimitives.ReadUInt32LittleEndian(stream.GetBuffer().AsSpan(4, sizeof(uint))));

        stream.Position = 0;
        using Image<Rgba32> decoded = Image.Load<Rgba32>(stream);
        AniMetadata decodedMetadata = decoded.Metadata.GetAniMetadata();

        Assert.Equal(imageMetadata.Name, decodedMetadata.Name);
        Assert.Equal(imageMetadata.Artist, decodedMetadata.Artist);

        for (int i = 0; i < decoded.Frames.Count; i++)
        {
            Assert.Equal((uint)(i + 1), decoded.Frames[i].Metadata.GetAniMetadata().FrameDelay);
        }
    }

    /// <summary>
    /// Verifies the encoder and decoder paths for embedded ICO and BMP resources.
    /// </summary>
    [Theory]
    [InlineData(AniFrameFormat.Ico)]
    [InlineData(AniFrameFormat.Bmp)]
    public void AniEncoder_RoundTripsOtherFrameFormats(AniFrameFormat frameFormat)
    {
        using Image<Rgba32> image = new(16, 16, Color.Red.ToPixel<Rgba32>());
        AniMetadata imageMetadata = image.Metadata.GetAniMetadata();
        imageMetadata.DisplayRate = 6;
        imageMetadata.BitCount = 32;
        imageMetadata.Planes = 1;

        AniFrameMetadata frameMetadata = image.Frames.RootFrame.Metadata.GetAniMetadata();
        frameMetadata.FrameDelay = 6;
        frameMetadata.SequenceNumber = 1;
        frameMetadata.FrameFormat = frameFormat;
        frameMetadata.Compression = IconFrameCompression.Bmp;

        using MemoryStream stream = new();
        image.Save(stream, new AniEncoder());

        Assert.Equal(stream.Length - 8, BinaryPrimitives.ReadUInt32LittleEndian(stream.GetBuffer().AsSpan(4, sizeof(uint))));

        if (frameFormat is AniFrameFormat.Bmp)
        {
            ReadOnlySpan<byte> encoded = stream.GetBuffer().AsSpan(0, (int)stream.Length);
            int frameChunkOffset = encoded.IndexOf("icon"u8);
            Assert.True(frameChunkOffset >= 0);

            // AF_ICON-clear resources contain a headerless BMP DIB, not a standalone file beginning with BITMAPFILEHEADER.
            ReadOnlySpan<byte> frameData = encoded[(frameChunkOffset + AniConstants.ChunkHeaderSize)..];
            Assert.False(frameData.StartsWith("BM"u8));
        }

        stream.Position = 0;
        using Image<Rgba32> decoded = Image.Load<Rgba32>(stream);

        ImageComparer.Exact.VerifySimilarity(image, decoded);
        Assert.Equal(frameFormat, decoded.Frames.RootFrame.Metadata.GetAniMetadata().FrameFormat);
    }

    /// <summary>
    /// Verifies that an independent frame cannot collide with an explicit sequence group.
    /// </summary>
    [Fact]
    public void AniEncoder_NonPositiveSequenceDoesNotCollideWithExplicitGroup()
    {
        using Image<Rgba32> image = new(16, 16, Color.Red.ToPixel<Rgba32>());
        image.Frames.AddFrame(image.Frames.RootFrame);
        image.Frames[1].Metadata.GetAniMetadata().SequenceNumber = 1;

        using MemoryStream stream = new();
        image.Save(stream, new AniEncoder());

        stream.Position = 0;
        using Image<Rgba32> decoded = Image.Load<Rgba32>(stream);

        Assert.Equal(2, decoded.Frames.Count);
        Assert.Equal(1, decoded.Frames[0].Metadata.GetAniMetadata().SequenceNumber);
        Assert.Equal(2, decoded.Frames[1].Metadata.GetAniMetadata().SequenceNumber);
    }
}
