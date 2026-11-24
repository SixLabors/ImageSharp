// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats.Ani;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.ColorProfiles.Icc;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using static SixLabors.ImageSharp.Tests.TestImages.Ani;

namespace SixLabors.ImageSharp.Tests.Formats.Ani;

[Trait("Format", "Ani")]
[ValidateDisposedMemoryAllocations]
public partial class AniEncoderTests
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
    /// Verifies the encoder and decoder paths for embedded ICO and CUR bitmap resources. The ACON specification defines
    /// each frame as an embedded .CUR file, so the encoder sets AF_ICON.
    /// </summary>
    /// <param name="frameFormat">The frame format of the metadata.</param>
    [Theory]
    [InlineData(AniFrameFormat.Ico)]
    [InlineData(AniFrameFormat.Cur)]
    public void AniEncoder_RoundTripsOtherFrameFormats(AniFrameFormat frameFormat)
    {
        using Image<Rgba32> image = new(16, 16, Color.Red.ToPixel<Rgba32>());
        AniMetadata imageMetadata = image.Metadata.GetAniMetadata();
        imageMetadata.DisplayRate = 6;

        AniFrameMetadata frameMetadata = image.Frames.RootFrame.Metadata.GetAniMetadata();
        frameMetadata.FrameDelay = 6;
        frameMetadata.SequenceNumber = 1;
        frameMetadata.FrameFormat = frameFormat;
        frameMetadata.Compression = IconFrameCompression.Bmp;

        using MemoryStream stream = new();
        image.Save(stream, new AniEncoder());

        Assert.Equal(stream.Length - 8, BinaryPrimitives.ReadUInt32LittleEndian(stream.GetBuffer().AsSpan(4, sizeof(uint))));

        // The flags are the last field of the 36-byte anih header. Each frame is an ICO or CUR file, so AF_ICON is set.
        ReadOnlySpan<byte> encoded = stream.GetBuffer().AsSpan(0, (int)stream.Length);
        int headerOffset = encoded.IndexOf("anih"u8) + AniConstants.ChunkHeaderSize;
        Assert.Equal(AniHeaderFlags.IsIcon, (AniHeaderFlags)BinaryPrimitives.ReadUInt32LittleEndian(encoded[(headerOffset + 32)..]));

        stream.Position = 0;
        using Image<Rgba32> decoded = Image.Load<Rgba32>(stream);

        ImageComparer.Exact.VerifySimilarity(image, decoded);
        Assert.Equal(frameFormat, decoded.Frames.RootFrame.Metadata.GetAniMetadata().FrameFormat);
    }

    /// <summary>
    /// Verifies that embedded resource encoders omit resolution metadata but retain the ICC profile.
    /// </summary>
    /// <param name="provider">The existing bitmap fixture.</param>
    /// <param name="frameFormat">The embedded resource format.</param>
    [Theory]
    [WithFile(TestImages.Bmp.Bit32Rgba, PixelTypes.Rgba32, AniFrameFormat.Ico)]
    [WithFile(TestImages.Bmp.Bit32Rgba, PixelTypes.Rgba32, AniFrameFormat.Cur)]
    public void AniEncoder_SkipMetadataPropagatesToEmbeddedEncoder(TestImageProvider<Rgba32> provider, AniFrameFormat frameFormat)
    {
        using Image<Rgba32> image = provider.GetImage();
        IccProfile profile = TestIccProfiles.GetProfile(TestIccProfiles.StandardRgbV2);
        image.Metadata.IccProfile = profile;
        image.Metadata.ResolutionUnits = PixelResolutionUnit.PixelsPerInch;
        image.Metadata.HorizontalResolution = 96;
        image.Metadata.VerticalResolution = 96;

        AniFrameMetadata frameMetadata = image.Frames.RootFrame.Metadata.GetAniMetadata();
        frameMetadata.FrameFormat = frameFormat;
        frameMetadata.Compression = IconFrameCompression.Bmp;
        frameMetadata.BmpBitsPerPixel = BmpBitsPerPixel.Bit32;

        using MemoryStream stream = new();
        image.Save(stream, new AniEncoder { SkipMetadata = true });

        ReadOnlySpan<byte> encoded = stream.GetBuffer().AsSpan(0, (int)stream.Length);
        int frameChunkOffset = encoded.IndexOf("icon"u8);
        Assert.True(frameChunkOffset >= 0);

        // Each frame is an ICO or CUR file. The directory entry ends with the offset of the image data.
        ReadOnlySpan<byte> resource = encoded[(frameChunkOffset + AniConstants.ChunkHeaderSize)..];
        int dibOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(resource[(IconDir.Size + IconDirEntry.Size - sizeof(uint))..]));

        // ICC requires a V5 header even when optional metadata is omitted. ProfileData is relative to this DIB header.
        ReadOnlySpan<byte> dib = resource[dibOffset..];
        Assert.Equal(BmpInfoHeader.SizeV5, BinaryPrimitives.ReadInt32LittleEndian(dib));

        BmpInfoHeader header = BmpInfoHeader.ParseV5(dib);
        Assert.Equal(0, header.XPelsPerMeter);
        Assert.Equal(0, header.YPelsPerMeter);
        Assert.Equal(BmpColorSpace.PROFILE_EMBEDDED, header.CsType);
        Assert.Equal(profile.ToByteArray(), dib.Slice((int)header.ProfileData, (int)header.ProfileSize).ToArray());
    }

    /// <summary>
    /// Verifies that a stored frame that plays in two steps is stored once. Help.ani stores 3 frames and plays them in the
    /// sequence 0, 1, 2, 1.
    /// </summary>
    /// <param name="provider">The test image provider.</param>
    [Theory]
    [WithFile(Help, PixelTypes.Rgba32)]
    public void AniEncoder_ReusedFrame_IsStoredOnce(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage(AniDecoder.Instance);
        Assert.Equal(4, image.Frames.Count);

        using MemoryStream stream = new();
        image.Save(stream, new AniEncoder());

        // The anih header holds cFrames at byte 4 and cSteps at byte 8. The 'seq ' chunk holds one index for each step.
        ReadOnlySpan<byte> encoded = stream.GetBuffer().AsSpan(0, (int)stream.Length);
        int headerOffset = encoded.IndexOf("anih"u8) + AniConstants.ChunkHeaderSize;
        Assert.Equal(3U, BinaryPrimitives.ReadUInt32LittleEndian(encoded[(headerOffset + 4)..]));
        Assert.Equal(4U, BinaryPrimitives.ReadUInt32LittleEndian(encoded[(headerOffset + 8)..]));

        int sequenceOffset = encoded.IndexOf("seq "u8) + AniConstants.ChunkHeaderSize;
        uint[] sequence = new uint[4];
        for (int i = 0; i < sequence.Length; i++)
        {
            sequence[i] = BinaryPrimitives.ReadUInt32LittleEndian(encoded[(sequenceOffset + (i * sizeof(uint)))..]);
        }

        Assert.Equal([0U, 1U, 2U, 1U], sequence);

        stream.Position = 0;
        using Image<Rgba32> decoded = Image.Load<Rgba32>(stream);
        ImageComparer.Exact.VerifySimilarity(image, decoded);
    }

    /// <summary>
    /// Verifies that a stored frame that plays in two adjacent steps is stored once. All four frames have sequence number
    /// 1. A stored frame cannot hold two images with the same size and depth, so the repeated 32-pixel image starts the
    /// second step.
    /// </summary>
    [Fact]
    public void AniEncoder_AdjacentReusedFrame_IsStoredOnce()
    {
        using Image<Rgba32> image = new(32, 32, Color.Red.ToPixel<Rgba32>());
        for (int i = 1; i < 4; i++)
        {
            using ImageFrame<Rgba32> copy = image.Frames.RootFrame.Clone();
            image.Frames.AddFrame(copy);
        }

        for (int i = 0; i < image.Frames.Count; i++)
        {
            AniFrameMetadata metadata = image.Frames[i].Metadata.GetAniMetadata();
            metadata.FrameFormat = AniFrameFormat.Cur;
            metadata.SequenceNumber = 1;
            metadata.EncodingWidth = (byte)((i & 1) == 0 ? 32 : 16);
            metadata.EncodingHeight = metadata.EncodingWidth;
        }

        using MemoryStream stream = new();
        image.Save(stream, new AniEncoder());

        // The anih header holds cFrames at byte 4 and cSteps at byte 8. The 'seq ' chunk holds one index for each step.
        ReadOnlySpan<byte> encoded = stream.GetBuffer().AsSpan(0, (int)stream.Length);
        int headerOffset = encoded.IndexOf("anih"u8) + AniConstants.ChunkHeaderSize;
        Assert.Equal(1U, BinaryPrimitives.ReadUInt32LittleEndian(encoded[(headerOffset + 4)..]));
        Assert.Equal(2U, BinaryPrimitives.ReadUInt32LittleEndian(encoded[(headerOffset + 8)..]));

        int sequenceOffset = encoded.IndexOf("seq "u8) + AniConstants.ChunkHeaderSize;
        Assert.Equal(0U, BinaryPrimitives.ReadUInt32LittleEndian(encoded[sequenceOffset..]));
        Assert.Equal(0U, BinaryPrimitives.ReadUInt32LittleEndian(encoded[(sequenceOffset + sizeof(uint))..]));

        // Both steps play the same stored frame, so the second step decodes to the same pixels as the first.
        stream.Position = 0;
        using Image<Rgba32> decoded = Image.Load<Rgba32>(stream);
        Assert.Equal(4, decoded.Frames.Count);

        using Image<Rgba32> firstStep = decoded.Frames.CloneFrame(0);
        using Image<Rgba32> secondStep = decoded.Frames.CloneFrame(2);
        ImageComparer.Exact.VerifySimilarity(firstStep, secondStep);
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

    /// <summary>
    /// Each bitmap frame keeps its own palette, in its own order, when the encoder writes it.
    /// </summary>
    [Fact]
    public void AniEncoder_BitmapFrames_KeepEachFramePalette()
    {
        // Each frame has its own palette in its own order, with one entry that no pixel uses. A new palette drops that
        // entry, and a shared palette gives both frames the same table. The test finds both errors.
        Color first = NearColorPalette.Palette.Span[0];
        Color second = NearColorPalette.Palette.Span[1];
        Color[][] palettes =
        [
            [first, second, Color.FromPixel(new Rgba32(200, 100, 50, 255))],
            [second, first, Color.FromPixel(new Rgba32(50, 100, 200, 255))]
        ];

        using Image<Rgba32> image = NearColorPalette.CreateImage();
        using (ImageFrame<Rgba32> copy = image.Frames.RootFrame.Clone())
        {
            image.Frames.AddFrame(copy);
        }

        image.Metadata.GetAniMetadata().DisplayRate = 6;

        for (int i = 0; i < image.Frames.Count; i++)
        {
            AniFrameMetadata frameMetadata = image.Frames[i].Metadata.GetAniMetadata();
            frameMetadata.FrameDelay = 6;
            frameMetadata.SequenceNumber = i + 1;
            frameMetadata.FrameFormat = AniFrameFormat.Cur;
            frameMetadata.Compression = IconFrameCompression.Bmp;
            frameMetadata.BmpBitsPerPixel = BmpBitsPerPixel.Bit8;
            frameMetadata.ColorTable = palettes[i];
        }

        using MemoryStream stream = new();
        image.Save(stream, new AniEncoder());
        stream.Position = 0;
        using Image<Rgba32> decoded = Image.Load<Rgba32>(stream);

        Assert.Equal(image.Frames.Count, decoded.Frames.Count);
        for (int i = 0; i < decoded.Frames.Count; i++)
        {
            Color[] colorTable = decoded.Frames[i].Metadata.GetAniMetadata().ColorTable!.Value.ToArray();
            Assert.Equal(palettes[i], colorTable[..palettes[i].Length]);
        }

        ImageComparer.Exact.VerifySimilarity(image, decoded);
    }

    /// <summary>
    /// Verifies that a bitmap frame is not compressed when the BMP metadata of the image selects RLE. ICONIMAGE uses only
    /// six BITMAPINFOHEADER members, and "All other members must be 0", so biCompression is 0 (BI_RGB): https://learn.microsoft.com/en-us/previous-versions/ms997538(v=msdn.10)
    /// </summary>
    [Fact]
    public void AniEncoder_BitmapFrames_IgnoreBmpMetadataCompression()
    {
        using Image<Rgba32> image = NearColorPalette.CreateImage();
        image.Metadata.GetBmpMetadata().Compression = BmpCompression.RunLength;

        AniFrameMetadata frameMetadata = image.Frames.RootFrame.Metadata.GetAniMetadata();
        frameMetadata.FrameFormat = AniFrameFormat.Cur;
        frameMetadata.Compression = IconFrameCompression.Bmp;
        frameMetadata.BmpBitsPerPixel = BmpBitsPerPixel.Bit8;
        frameMetadata.ColorTable = NearColorPalette.Palette;

        using MemoryStream stream = new();
        image.Save(stream, new AniEncoder());

        ReadOnlySpan<byte> encoded = stream.GetBuffer().AsSpan(0, (int)stream.Length);
        int frameChunkOffset = encoded.IndexOf("icon"u8);
        Assert.True(frameChunkOffset >= 0);

        // The frame is a CUR file. The directory entry ends with the offset of the DIB, and the compression field of the
        // info header starts at byte 16 of the DIB.
        ReadOnlySpan<byte> resource = encoded[(frameChunkOffset + AniConstants.ChunkHeaderSize)..];
        int dibOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(resource[(IconDir.Size + IconDirEntry.Size - sizeof(uint))..]));
        Assert.Equal((uint)BmpInfoCompression.Rgb, BinaryPrimitives.ReadUInt32LittleEndian(resource[(dibOffset + 16)..]));
    }
}
