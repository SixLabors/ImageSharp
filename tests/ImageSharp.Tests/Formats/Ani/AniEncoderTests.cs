// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats.Ani;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestDataIcc;
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
    /// Verifies that metadata suppression is propagated to every embedded resource encoder.
    /// </summary>
    [Theory]
    [InlineData(AniFrameFormat.Ico)]
    [InlineData(AniFrameFormat.Cur)]
    [InlineData(AniFrameFormat.Bmp)]
    public void AniEncoder_SkipMetadataPropagatesToEmbeddedEncoder(AniFrameFormat frameFormat)
    {
        using Image<Rgba32> image = new(16, 16, Color.Red.ToPixel<Rgba32>());
        image.Metadata.IccProfile = new IccProfile(IccTestDataProfiles.ProfileRandomArray);

        AniMetadata imageMetadata = image.Metadata.GetAniMetadata();
        imageMetadata.BitCount = 32;
        imageMetadata.Planes = 1;

        AniFrameMetadata frameMetadata = image.Frames.RootFrame.Metadata.GetAniMetadata();
        frameMetadata.FrameFormat = frameFormat;
        frameMetadata.Compression = IconFrameCompression.Bmp;
        frameMetadata.BmpBitsPerPixel = BmpBitsPerPixel.Bit32;

        using MemoryStream stream = new();
        image.Save(stream, new AniEncoder { SkipMetadata = true });

        ReadOnlySpan<byte> encoded = stream.GetBuffer().AsSpan(0, (int)stream.Length);
        int frameChunkOffset = encoded.IndexOf("icon"u8);
        Assert.True(frameChunkOffset >= 0);

        ReadOnlySpan<byte> resource = encoded[(frameChunkOffset + AniConstants.ChunkHeaderSize)..];
        int dibOffset = 0;

        if (frameFormat is not AniFrameFormat.Bmp)
        {
            dibOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(resource[(IconDir.Size + IconDirEntry.Size - sizeof(uint))..]));
        }

        // Metadata-free ICO/CUR bitmaps use BITMAPINFOHEADER, while raw transparent ANI bitmaps require BITMAPV4HEADER.
        int expectedHeaderSize = frameFormat is AniFrameFormat.Bmp ? BmpInfoHeader.SizeV4 : BmpInfoHeader.SizeV3;
        Assert.Equal(expectedHeaderSize, BinaryPrimitives.ReadInt32LittleEndian(resource[dibOffset..]));
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

    /// <summary>
    /// Verifies that an explicit source sequence is preserved as an identity table after playback-order expansion.
    /// </summary>
    [Fact]
    public void AniEncoder_PreservesExplicitSequence()
    {
        using Image<Rgba32> image = new(16, 16, Color.Red.ToPixel<Rgba32>());
        image.Frames.AddFrame(image.Frames.RootFrame);
        image.Metadata.GetAniMetadata().Flags = AniHeaderFlags.IsIcon | AniHeaderFlags.ContainsSequence;

        using MemoryStream stream = new();
        image.Save(stream, new AniEncoder());

        ReadOnlySpan<byte> data = stream.GetBuffer().AsSpan(0, (int)stream.Length);
        int sequenceOffset = data.IndexOf("seq "u8);

        Assert.True(sequenceOffset >= 0);
        Assert.Equal((uint)(2 * sizeof(uint)), BinaryPrimitives.ReadUInt32LittleEndian(data[(sequenceOffset + sizeof(uint))..]));
        Assert.Equal(0U, BinaryPrimitives.ReadUInt32LittleEndian(data[(sequenceOffset + AniConstants.ChunkHeaderSize)..]));
        Assert.Equal(1U, BinaryPrimitives.ReadUInt32LittleEndian(data[(sequenceOffset + AniConstants.ChunkHeaderSize + sizeof(uint))..]));

        stream.Position = 0;
        using Image<Rgba32> decoded = Image.Load<Rgba32>(stream);

        Assert.True(decoded.Metadata.GetAniMetadata().Flags.HasFlag(AniHeaderFlags.ContainsSequence));
    }

    /// <summary>
    /// Verifies that unsupported public frame metadata is rejected before any container data is written.
    /// </summary>
    [Fact]
    public void AniEncoder_UnsupportedFrameFormatThrowsBeforeWriting()
    {
        using Image<Rgba32> image = new(16, 16);
        image.Frames.RootFrame.Metadata.GetAniMetadata().FrameFormat = (AniFrameFormat)byte.MaxValue;

        using MemoryStream stream = new();

        Assert.Throws<ImageFormatException>(() => image.Save(stream, new AniEncoder()));
        Assert.Equal(0, stream.Length);
    }
}
