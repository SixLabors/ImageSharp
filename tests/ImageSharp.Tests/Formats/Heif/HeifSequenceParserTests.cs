// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Text;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Heif;

[Trait("Format", "Heif")]
[ValidateDisposedMemoryAllocations]
public class HeifSequenceParserTests
{
    private const int TrackExifOffset = 1800;
    private const int TrackXmpOffset = 1840;

    private static ReadOnlySpan<byte> TrackExifData => [0, 0, 0, 0, 0x49, 0x49, 0x2A, 0, 8, 0, 0, 0];

    private static ReadOnlySpan<byte> TrackXmpData => "<x:xmpmeta/>"u8;

    [Fact]
    public void IdentifyReturnsBoundedSequenceAndFrameMetadata()
    {
        byte[] data = CreateSequenceContainer(trackProperties: true, trackMetadata: true);

        ImageInfo info = Image.Identify(data);
        HeifMetadata metadata = info.Metadata.GetHeifMetadata();

        Assert.Equal(new Size(240, 320), info.Size);
        Assert.Equal(2, info.FrameCount);
        Assert.Equal(HeifCompressionMethod.Av1, metadata.CompressionMethod);
        Assert.Equal(HeifBitDepth.Bit8, metadata.BitDepth);
        Assert.Equal(3, metadata.RepeatCount);
        Assert.False(metadata.HasAlpha);
        Assert.NotNull(info.Metadata.CicpProfile);
        Assert.NotNull(info.Metadata.ExifProfile);
        Assert.NotNull(info.Metadata.XmpProfile);
        Assert.NotNull(metadata.ContentLightLevel);
        Assert.Equal(4D, info.Metadata.HorizontalResolution);
        Assert.Equal(3D, info.Metadata.VerticalResolution);
        Assert.All(
            info.FrameMetadataCollection,
            frame => Assert.Equal(new Rational(1, 10), frame.GetHeifMetadata().FrameDelay));
    }

    [Fact]
    public void IdentifySkipsAncillarySequenceMetadataWithoutDroppingImageMetadata()
    {
        byte[] data = CreateSequenceContainer(trackProperties: true, trackMetadata: true);
        using MemoryStream stream = new(data, false);
        DecoderOptions options = new() { SkipMetadata = true };

        ImageInfo info = Image.Identify(options, stream);
        HeifMetadata metadata = info.Metadata.GetHeifMetadata();

        Assert.Equal(HeifCompressionMethod.Av1, metadata.CompressionMethod);
        Assert.Equal(HeifBitDepth.Bit8, metadata.BitDepth);
        Assert.Equal(3, metadata.RepeatCount);
        Assert.Null(info.Metadata.CicpProfile);
        Assert.Null(info.Metadata.IccProfile);
        Assert.Null(info.Metadata.ExifProfile);
        Assert.Null(info.Metadata.XmpProfile);
        Assert.Null(metadata.ContentLightLevel);
        Assert.All(
            info.FrameMetadataCollection,
            frame => Assert.Equal(new Rational(1, 10), frame.GetHeifMetadata().FrameDelay));
    }

    [Fact]
    public void DecodeAdoptsIndependentAv1SamplesAsImageFrames()
    {
        byte[] source = TestFile.Create(TestImages.Heif.Orange4x4).Bytes;
        byte[] data = CreateDecodableAv1SequenceContainer(source.AsSpan(0x10E, 0x1D), source.AsSpan(0xC7, 4));

        using Image<Rgba32> expected = Image.Load<Rgba32>(source);
        using Image<Rgba32> actual = Image.Load<Rgba32>(data);

        Assert.Equal(new Size(4, 4), actual.Size);
        Assert.Equal(2, actual.Frames.Count);
        foreach (ImageFrame<Rgba32> frame in actual.Frames)
        {
            Assert.Equal(new Rational(1, 10), frame.Metadata.GetHeifMetadata().FrameDelay);

            for (int y = 0; y < frame.Height; y++)
            {
                Assert.True(frame.PixelBuffer.DangerousGetRowSpan(y).SequenceEqual(expected.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y)));
            }
        }
    }

    [Fact]
    public void DecodeComposesFrameAlignedAv1AlphaSamples()
    {
        byte[] source = TestFile.Create(TestImages.Heif.Orange4x4).Bytes;
        byte[] data = CreateDecodableAv1SequenceWithAlphaContainer(source.AsSpan(0x10E, 0x1D), source.AsSpan(0xC7, 4));

        using Image<Rgba32> expectedColor = Image.Load<Rgba32>(source);
        using Image<L16> expectedAlpha = Image.Load<L16>(source);
        using Image<Rgba32> actual = Image.Load<Rgba32>(data);

        Assert.Equal(2, actual.Frames.Count);
        Assert.True(actual.Metadata.GetHeifMetadata().HasAlpha);
        foreach (ImageFrame<Rgba32> frame in actual.Frames)
        {
            for (int y = 0; y < frame.Height; y++)
            {
                for (int x = 0; x < frame.Width; x++)
                {
                    Rgba64 expected = Rgba64.FromRgba32(expectedColor[x, y]);
                    expected.A = expectedAlpha[x, y].PackedValue;
                    Assert.Equal(expected.ToRgba32(), frame[x, y]);
                }
            }
        }
    }

    [Fact]
    public void ParseResolvesLibavifShapedSampleTable()
    {
        byte[] data = CreateSequenceFile(1024);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(2);
        stream.Position = 8;

        HeifSequence sequence = parser.Parse(stream, GetMoviePayloadLength(data));

        Assert.Equal(1000U, sequence.MovieTimescale);
        Assert.Null(sequence.AlphaTrack);
        Assert.Equal(1U, sequence.ColorTrack.Id);
        Assert.Equal(320, sequence.ColorTrack.Width);
        Assert.Equal(240, sequence.ColorTrack.Height);
        Assert.Equal(Heif4CharCode.Av01, sequence.ColorTrack.CodecType);
        Assert.NotNull(sequence.ColorTrack.Av1CodecConfiguration);
        Assert.Equal(2U, sequence.ColorTrack.TotalSampleCount);
        Assert.Equal(3, sequence.ColorTrack.RepeatCount);
        Assert.False(sequence.ColorTrack.AllReferencePicturesIntra);
        Assert.True(sequence.ColorTrack.IntraPicturePredictionUsed);
        Assert.Equal(15, sequence.ColorTrack.MaximumReferencesPerPicture);
        Assert.Collection(
            sequence.ColorTrack.Samples,
            sample =>
            {
                Assert.Equal(1024, sample.Offset);
                Assert.Equal(10, sample.Length);
                Assert.Equal(100U, sample.Duration);
                Assert.True(sample.IsSync);
            },
            sample =>
            {
                Assert.Equal(1034, sample.Offset);
                Assert.Equal(12, sample.Length);
                Assert.Equal(100U, sample.Duration);
                Assert.False(sample.IsSync);
            });
    }

    [Fact]
    public void ParseRetainsOnlyConfiguredFrameCount()
    {
        byte[] data = CreateSequenceFile(1024);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(1);
        stream.Position = 8;

        HeifSequence sequence = parser.Parse(stream, GetMoviePayloadLength(data));

        Assert.Equal(2U, sequence.ColorTrack.TotalSampleCount);
        HeifSequenceSample sample = Assert.Single(sequence.ColorTrack.Samples);
        Assert.Equal(1024, sample.Offset);
        Assert.Equal(10, sample.Length);
        Assert.Equal(100U, sample.Duration);
    }

    [Fact]
    public void ParseRejectsRetainedSampleBeyondFile()
    {
        byte[] data = CreateSequenceFile(2040);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(2);
        stream.Position = 8;

        Assert.Throws<InvalidImageContentException>(() => parser.Parse(stream, GetMoviePayloadLength(data)));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ParseRejectsNonIdentityMoviePresentationMatrix(bool nonIdentityMovieMatrix, bool nonIdentityTrackMatrix)
    {
        byte[] data = CreateSequenceFile(
            1024,
            nonIdentityMovieMatrix: nonIdentityMovieMatrix,
            nonIdentityTrackMatrix: nonIdentityTrackMatrix);

        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(2);
        stream.Position = 8;

        Assert.Throws<NotSupportedException>(() => parser.Parse(stream, GetMoviePayloadLength(data)));
    }

    [Fact]
    public void ParseMarksHiddenHevcSamples()
    {
        byte[] data = CreateSequenceFile(1024, hevc: true, compositionOffsets: true);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(2);
        stream.Position = 8;

        HeifSequence sequence = parser.Parse(stream, GetMoviePayloadLength(data));

        Assert.Equal(Heif4CharCode.Hvc1, sequence.ColorTrack.CodecType);
        Assert.NotNull(sequence.ColorTrack.HevcCodecConfiguration);
        Assert.True(sequence.ColorTrack.Samples[0].IsHidden);
        Assert.Equal(long.MinValue, sequence.ColorTrack.Samples[0].CompositionTime);
        Assert.False(sequence.ColorTrack.Samples[1].IsHidden);
        Assert.Equal(100, sequence.ColorTrack.Samples[1].CompositionTime);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ParseMatchesAlphaTrackAndPremultiplicationByTrackId(bool colorTransforms, bool alphaTransforms)
    {
        byte[] data = CreateSequenceFileWithAlpha(1024, 1000, 2, colorTransforms, alphaTransforms);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(2);
        stream.Position = 8;

        HeifSequence sequence = parser.Parse(stream, GetMoviePayloadLength(data));

        Assert.NotNull(sequence.AlphaTrack);
        Assert.Equal(2U, sequence.AlphaTrack.Id);
        Assert.True(sequence.AlphaTrack.IsAlpha);
        Assert.True(sequence.ColorTrack.IsPremultiplied);
    }

    [Theory]
    [InlineData(SegmentIntegrityHandling.Strict)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary)]
    public void ParseRejectsAlphaTrackWithDifferentDecodeTiming(SegmentIntegrityHandling handling)
    {
        byte[] data = CreateSequenceFileWithAlpha(1024, 2000, 0);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(2, segmentIntegrityHandling: handling);
        stream.Position = 8;

        Assert.Throws<InvalidImageContentException>(() => parser.Parse(stream, GetMoviePayloadLength(data)));
    }

    [Fact]
    public void ParseDropsAlphaTrackWithDifferentDecodeTimingWhenImageDataErrorsAreIgnored()
    {
        byte[] data = CreateSequenceFileWithAlpha(1024, 2000, 0);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(2, segmentIntegrityHandling: SegmentIntegrityHandling.IgnoreImageData);
        stream.Position = 8;

        HeifSequence sequence = parser.Parse(stream, GetMoviePayloadLength(data));

        Assert.Null(sequence.AlphaTrack);
        Assert.False(sequence.ColorTrack.IsPremultiplied);
    }

    [Fact]
    public void ParseRejectsPremultiplicationReferenceToUnrelatedTrack()
    {
        byte[] data = CreateSequenceFileWithAlpha(1024, 1000, 3);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(2);
        stream.Position = 8;

        Assert.Throws<InvalidImageContentException>(() => parser.Parse(stream, GetMoviePayloadLength(data)));
    }

    [Fact]
    public void ParseRejectsMismatchedAlphaPresentationTransforms()
    {
        byte[] data = CreateSequenceFileWithAlpha(1024, 1000, 0, false, true);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(2);
        stream.Position = 8;

        Assert.Throws<NotSupportedException>(() => parser.Parse(stream, GetMoviePayloadLength(data)));
    }

    [Fact]
    public void ParseRejectsCompositionOffsetsForAv1()
    {
        byte[] data = CreateSequenceFile(1024, compositionOffsets: true);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(2);
        stream.Position = 8;

        Assert.Throws<InvalidImageContentException>(() => parser.Parse(stream, GetMoviePayloadLength(data)));
    }

    [Fact]
    public void ParseResolvesDirectReferenceSamples()
    {
        byte[] data = CreateSequenceFile(1024, directReferences: true);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(2);
        stream.Position = 8;

        HeifSequence sequence = parser.Parse(stream, GetMoviePayloadLength(data));

        Assert.Equal(new[] { 0 }, sequence.ColorTrack.DirectReferenceSampleIndices);
        Assert.Equal(1U, sequence.ColorTrack.Samples[0].SampleId);
        Assert.Equal(0, sequence.ColorTrack.Samples[0].DirectReferenceCount);
        Assert.Equal(0U, sequence.ColorTrack.Samples[1].SampleId);
        Assert.Equal(0, sequence.ColorTrack.Samples[1].DirectReferenceOffset);
        Assert.Equal(1, sequence.ColorTrack.Samples[1].DirectReferenceCount);
    }

    [Fact]
    public void ParseRejectsUnknownDirectReferenceSampleId()
    {
        byte[] data = CreateSequenceFile(1024, directReferences: true, directReferenceSampleId: 2);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(2);
        stream.Position = 8;

        Assert.Throws<InvalidImageContentException>(() => parser.Parse(stream, GetMoviePayloadLength(data)));
    }

    [Fact]
    public void ParseRetainsTrackImageProperties()
    {
        byte[] data = CreateSequenceFile(1024, trackProperties: true);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(2);
        stream.Position = 8;

        HeifSequenceTrack track = parser.Parse(stream, GetMoviePayloadLength(data)).ColorTrack;

        Assert.NotNull(track.CicpProfile);
        Assert.Equal(4U, track.PixelAspectRatio!.HorizontalSpacing);
        Assert.Equal(3U, track.PixelAspectRatio.VerticalSpacing);
        Assert.Equal(new Rectangle(0, 0, 320, 240), track.CleanAperture!.Value.ToRectangle(new Size(320, 240)));
        Assert.Equal((byte)1, track.RotationAngle);
        Assert.Equal((byte)1, track.MirrorAxis);
        Assert.Equal((ushort)1000, track.ContentLightLevel!.Value.MaximumContentLightLevel);
        Assert.NotNull(track.MasteringDisplayColorVolume);
        Assert.NotNull(track.ContentColorVolume);
        Assert.NotNull(track.AmbientViewingEnvironment);
        Assert.NotNull(track.ReferenceViewingEnvironment);
        Assert.NotNull(track.NominalDiffuseWhite);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParseRetainsBoundedTrackMetadata(bool useItemData)
    {
        byte[] data = CreateSequenceFile(1024, trackMetadata: true, metadataInItemData: useItemData);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(2);
        stream.Position = 8;

        HeifSequenceTrack track = parser.Parse(stream, GetMoviePayloadLength(data)).ColorTrack;

        Assert.NotNull(track.Metadata);
        Assert.Equal(TrackExifData.ToArray(), track.Metadata.ExifData);
        Assert.Equal(TrackXmpData.ToArray(), track.Metadata.XmpData);
    }

    [Fact]
    public void ParseDoesNotValidateOrRetainSkippedTrackMetadata()
    {
        byte[] data = CreateSequenceFile(1024, trackMetadata: true, invalidTrackMetadata: true);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(2, skipMetadata: true);
        stream.Position = 8;

        HeifSequenceTrack track = parser.Parse(stream, GetMoviePayloadLength(data)).ColorTrack;

        Assert.Null(track.Metadata);
        Assert.Equal(2, track.Samples.Length);
    }

    [Fact]
    public void ParseUsesAncillaryIntegrityPolicyForTrackMetadata()
    {
        byte[] data = CreateSequenceFile(1024, trackMetadata: true, invalidTrackMetadata: true);
        using MemoryStream strictStream = new(data, false);
        HeifSequenceParser strictParser = CreateParser(2);
        strictStream.Position = 8;

        Assert.Throws<InvalidImageContentException>(() => strictParser.Parse(strictStream, GetMoviePayloadLength(data)));

        using MemoryStream tolerantStream = new(data, false);
        HeifSequenceParser tolerantParser = CreateParser(2, segmentIntegrityHandling: SegmentIntegrityHandling.IgnoreAncillary);
        tolerantStream.Position = 8;

        HeifSequenceTrack track = tolerantParser.Parse(tolerantStream, GetMoviePayloadLength(data)).ColorTrack;

        Assert.Null(track.Metadata);
        Assert.Equal(2, track.Samples.Length);
    }

    [Theory]
    [InlineData(SegmentIntegrityHandling.Strict)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary)]
    public void ParseRejectsInvalidPresentationPropertyUnlessImageDataErrorsAreIgnored(SegmentIntegrityHandling handling)
    {
        byte[] data = CreateSequenceFile(1024, trackProperties: true, invalidRotation: true);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(2, segmentIntegrityHandling: handling);
        stream.Position = 8;

        Assert.Throws<InvalidImageContentException>(() => parser.Parse(stream, GetMoviePayloadLength(data)));
    }

    [Fact]
    public void ParseOmitsInvalidPresentationPropertyWhenImageDataErrorsAreIgnored()
    {
        byte[] data = CreateSequenceFile(1024, trackProperties: true, invalidRotation: true);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(2, segmentIntegrityHandling: SegmentIntegrityHandling.IgnoreImageData);
        stream.Position = 8;

        HeifSequenceTrack track = parser.Parse(stream, GetMoviePayloadLength(data)).ColorTrack;

        Assert.Null(track.RotationAngle);
        Assert.NotNull(track.PixelAspectRatio);
        Assert.Equal(2, track.Samples.Length);
    }

    private static byte[] CreateSequenceFile(
        uint chunkOffset,
        bool hevc = false,
        bool compositionOffsets = false,
        bool directReferences = false,
        uint directReferenceSampleId = 1,
        bool trackProperties = false,
        bool trackMetadata = false,
        bool metadataInItemData = false,
        bool invalidTrackMetadata = false,
        bool invalidRotation = false,
        int width = 320,
        int height = 240,
        byte[] av1Configuration = null,
        int? sampleSize = null,
        bool allSamplesSync = false,
        uint premultipliedByTrackId = 0,
        bool nonIdentityMovieMatrix = false,
        bool nonIdentityTrackMatrix = false)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, true);
        long movie = BeginBox(writer, Heif4CharCode.Moov);

        long movieHeader = BeginBox(writer, Heif4CharCode.Mvhd);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 1000);
        WriteUInt32(writer, 600);
        WriteUInt32(writer, 0x00010000);
        WriteUInt16(writer, 0x0100);
        WriteUInt16(writer, 0);
        WriteZeros(writer, 8);
        WritePresentationMatrix(writer, nonIdentityMovieMatrix);
        WriteZeros(writer, 24);
        WriteUInt32(writer, 3);
        EndBox(writer, movieHeader);

        long track = BeginBox(writer, Heif4CharCode.Trak);
        WriteTrackHeader(writer, width, height, 1, nonIdentityTrackMatrix);
        if (premultipliedByTrackId != 0)
        {
            WriteTrackReference(writer, Heif4CharCode.Prem, premultipliedByTrackId);
        }

        WriteEditList(writer);
        if (trackMetadata)
        {
            WriteTrackMetadata(writer, metadataInItemData, invalidTrackMetadata);
        }

        long media = BeginBox(writer, Heif4CharCode.Mdia);
        WriteMediaHeader(writer);
        WriteHandler(writer, Heif4CharCode.Pict);

        long mediaInformation = BeginBox(writer, Heif4CharCode.Minf);
        WriteDataInformation(writer);
        WriteSampleTable(
            writer,
            chunkOffset,
            hevc,
            compositionOffsets,
            directReferences,
            directReferenceSampleId,
            trackProperties,
            invalidRotation,
            width,
            height,
            av1Configuration,
            sampleSize,
            allSamplesSync);

        EndBox(writer, mediaInformation);
        EndBox(writer, media);
        EndBox(writer, track);
        EndBox(writer, movie);

        byte[] movieBytes = stream.ToArray();
        byte[] file = new byte[2048];

        movieBytes.CopyTo(file, 0);
        if (trackMetadata && !metadataInItemData)
        {
            TrackExifData.CopyTo(file.AsSpan(TrackExifOffset));
            TrackXmpData.CopyTo(file.AsSpan(TrackXmpOffset));
        }

        return file;
    }

    private static byte[] CreateSequenceFileWithAlpha(
        uint chunkOffset,
        uint alphaTimescale,
        uint premultipliedByTrackId,
        bool colorTransforms = false,
        bool alphaTransforms = false,
        uint? alphaChunkOffset = null,
        int width = 320,
        int height = 240,
        byte[] av1Configuration = null,
        int? sampleSize = null,
        bool allSamplesSync = false)
    {
        byte[] colorFile = CreateSequenceFile(
            chunkOffset,
            trackProperties: colorTransforms,
            width: width,
            height: height,
            av1Configuration: av1Configuration,
            sampleSize: sampleSize,
            allSamplesSync: allSamplesSync,
            premultipliedByTrackId: premultipliedByTrackId);

        int movieLength = (int)BinaryPrimitives.ReadUInt32BigEndian(colorFile);
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, true);
        long track = BeginBox(writer, Heif4CharCode.Trak);
        WriteTrackHeader(writer, width, height, 2);
        WriteTrackReference(writer, Heif4CharCode.Auxl, 1);

        long media = BeginBox(writer, Heif4CharCode.Mdia);
        WriteMediaHeader(writer, alphaTimescale);
        WriteHandler(writer, Heif4CharCode.Auxv);

        long mediaInformation = BeginBox(writer, Heif4CharCode.Minf);
        WriteDataInformation(writer);
        WriteSampleTable(
            writer, alphaChunkOffset ?? chunkOffset, false, false, false, 1, alphaTransforms, false,
            width, height, av1Configuration, sampleSize, allSamplesSync, true);

        EndBox(writer, mediaInformation);
        EndBox(writer, media);
        EndBox(writer, track);

        byte[] alphaTrack = stream.ToArray();
        byte[] file = new byte[2048];
        colorFile.AsSpan(0, movieLength).CopyTo(file);
        alphaTrack.CopyTo(file, movieLength);
        BinaryPrimitives.WriteUInt32BigEndian(file, (uint)(movieLength + alphaTrack.Length));
        return file;
    }

    private static byte[] CreateSequenceContainer(bool trackProperties, bool trackMetadata)
    {
        byte[] movie = CreateSequenceFile(
            1024,
            trackProperties: trackProperties,
            trackMetadata: trackMetadata,
            metadataInItemData: true);

        byte[] data = new byte[movie.Length + 24];
        BinaryPrimitives.WriteUInt32BigEndian(data, 24);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(4), (uint)Heif4CharCode.Ftyp);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), (uint)Heif4CharCode.Avis);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(12), 0);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(16), (uint)Heif4CharCode.Avif);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(20), (uint)Heif4CharCode.Mif1);
        movie.CopyTo(data, 24);
        return data;
    }

    private static byte[] CreateDecodableAv1SequenceContainer(ReadOnlySpan<byte> sample, ReadOnlySpan<byte> configuration)
    {
        const int fileTypeLength = 24;
        const int movieStorageLength = 2048;
        uint chunkOffset = fileTypeLength + movieStorageLength;
        byte[] movie = CreateSequenceFile(
            chunkOffset,
            width: 4,
            height: 4,
            av1Configuration: configuration.ToArray(),
            sampleSize: sample.Length,
            allSamplesSync: true);

        byte[] data = new byte[chunkOffset + (sample.Length * 2)];
        BinaryPrimitives.WriteUInt32BigEndian(data, fileTypeLength);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(4), (uint)Heif4CharCode.Ftyp);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), (uint)Heif4CharCode.Avis);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(12), 0);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(16), (uint)Heif4CharCode.Avif);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(20), (uint)Heif4CharCode.Mif1);
        movie.CopyTo(data, fileTypeLength);
        sample.CopyTo(data.AsSpan((int)chunkOffset));
        sample.CopyTo(data.AsSpan((int)chunkOffset + sample.Length));
        return data;
    }

    private static byte[] CreateDecodableAv1SequenceWithAlphaContainer(ReadOnlySpan<byte> sample, ReadOnlySpan<byte> configuration)
    {
        const int fileTypeLength = 24;
        const int movieStorageLength = 2048;
        uint colorChunkOffset = fileTypeLength + movieStorageLength;
        uint alphaChunkOffset = colorChunkOffset + (uint)(sample.Length * 2);
        byte[] movie = CreateSequenceFileWithAlpha(
            colorChunkOffset,
            1000,
            0,
            alphaChunkOffset: alphaChunkOffset,
            width: 4,
            height: 4,
            av1Configuration: configuration.ToArray(),
            sampleSize: sample.Length,
            allSamplesSync: true);

        byte[] data = new byte[alphaChunkOffset + (sample.Length * 2)];
        BinaryPrimitives.WriteUInt32BigEndian(data, fileTypeLength);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(4), (uint)Heif4CharCode.Ftyp);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), (uint)Heif4CharCode.Avis);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(12), 0);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(16), (uint)Heif4CharCode.Avif);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(20), (uint)Heif4CharCode.Mif1);
        movie.CopyTo(data, fileTypeLength);
        sample.CopyTo(data.AsSpan((int)colorChunkOffset));
        sample.CopyTo(data.AsSpan((int)colorChunkOffset + sample.Length));
        sample.CopyTo(data.AsSpan((int)alphaChunkOffset));
        sample.CopyTo(data.AsSpan((int)alphaChunkOffset + sample.Length));
        return data;
    }

    private static void WriteTrackHeader(BinaryWriter writer, int width, int height, uint trackId = 1, bool nonIdentityMatrix = false)
    {
        long trackHeader = BeginBox(writer, Heif4CharCode.Tkhd);
        WriteFullBoxHeader(writer, 0, 3);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, trackId);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 600);
        WriteZeros(writer, 16);
        WritePresentationMatrix(writer, nonIdentityMatrix);
        WriteUInt32(writer, (uint)width << 16);
        WriteUInt32(writer, (uint)height << 16);
        EndBox(writer, trackHeader);
    }

    private static void WritePresentationMatrix(BinaryWriter writer, bool nonIdentityMatrix)
    {
        WriteUInt32(writer, nonIdentityMatrix ? 0x00020000U : 0x00010000U);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0x00010000);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0x40000000);
    }

    private static void WriteTrackReference(BinaryWriter writer, Heif4CharCode referenceType, uint trackId)
    {
        long references = BeginBox(writer, Heif4CharCode.Tref);
        long reference = BeginBox(writer, referenceType);
        WriteUInt32(writer, trackId);
        EndBox(writer, reference);
        EndBox(writer, references);
    }

    private static void WriteEditList(BinaryWriter writer)
    {
        long edit = BeginBox(writer, Heif4CharCode.Edts);
        long editList = BeginBox(writer, Heif4CharCode.Elst);
        WriteFullBoxHeader(writer, 0, 1);
        WriteUInt32(writer, 1);
        WriteUInt32(writer, 200);
        WriteUInt32(writer, 0);
        WriteUInt16(writer, 1);
        WriteUInt16(writer, 0);
        EndBox(writer, editList);
        EndBox(writer, edit);
    }

    private static void WriteMediaHeader(BinaryWriter writer, uint timescale = 1000)
    {
        long mediaHeader = BeginBox(writer, Heif4CharCode.Mdhd);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, timescale);
        WriteUInt32(writer, 200);
        WriteUInt16(writer, 21956);
        WriteUInt16(writer, 0);
        EndBox(writer, mediaHeader);
    }

    private static void WriteHandler(BinaryWriter writer, Heif4CharCode handlerType)
    {
        long handler = BeginBox(writer, Heif4CharCode.Hdlr);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, (uint)handlerType);
        WriteZeros(writer, 12);
        writer.Write((byte)0);
        EndBox(writer, handler);
    }

    private static void WriteDataInformation(BinaryWriter writer)
    {
        long dataInformation = BeginBox(writer, Heif4CharCode.Dinf);
        long dataReference = BeginBox(writer, Heif4CharCode.Dref);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, 1);
        long location = BeginBox(writer, Heif4CharCode.Url);
        WriteFullBoxHeader(writer, 0, 1);
        EndBox(writer, location);
        EndBox(writer, dataReference);
        EndBox(writer, dataInformation);
    }

    private static void WriteTrackMetadata(BinaryWriter writer, bool useItemData, bool invalidHandler)
    {
        long metadata = BeginBox(writer, Heif4CharCode.Meta);
        WriteFullBoxHeader(writer, 0, 0);
        WriteHandler(writer, invalidHandler ? Heif4CharCode.Vide : Heif4CharCode.Pict);

        long itemLocations = BeginBox(writer, Heif4CharCode.Iloc);
        WriteFullBoxHeader(writer, useItemData ? (byte)1 : (byte)0, 0);
        writer.Write((byte)0x44);
        writer.Write((byte)0);
        WriteUInt16(writer, 2);
        WriteTrackMetadataLocation(writer, 1, useItemData, useItemData ? 0U : TrackExifOffset, (uint)TrackExifData.Length);
        WriteTrackMetadataLocation(
            writer,
            2,
            useItemData,
            useItemData ? (uint)TrackExifData.Length : TrackXmpOffset,
            (uint)TrackXmpData.Length);

        EndBox(writer, itemLocations);

        long itemInformation = BeginBox(writer, Heif4CharCode.Iinf);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt16(writer, 2);
        WriteTrackMetadataItem(writer, 1, Heif4CharCode.Exif);
        WriteTrackMetadataItem(writer, 2, Heif4CharCode.Mime);
        EndBox(writer, itemInformation);

        if (useItemData)
        {
            long itemData = BeginBox(writer, Heif4CharCode.Idat);
            writer.Write(TrackExifData);
            writer.Write(TrackXmpData);
            EndBox(writer, itemData);
        }

        EndBox(writer, metadata);
    }

    private static void WriteTrackMetadataLocation(BinaryWriter writer, ushort itemId, bool useItemData, uint offset, uint length)
    {
        WriteUInt16(writer, itemId);
        if (useItemData)
        {
            WriteUInt16(writer, 1);
        }

        WriteUInt16(writer, 0);
        WriteUInt16(writer, 1);
        WriteUInt32(writer, offset);
        WriteUInt32(writer, length);
    }

    private static void WriteTrackMetadataItem(BinaryWriter writer, ushort itemId, Heif4CharCode itemType)
    {
        long itemInformationEntry = BeginBox(writer, Heif4CharCode.Infe);
        WriteFullBoxHeader(writer, 2, 0);
        WriteUInt16(writer, itemId);
        WriteUInt16(writer, 0);
        WriteUInt32(writer, (uint)itemType);
        writer.Write((byte)0);
        if (itemType == Heif4CharCode.Mime)
        {
            writer.Write("application/rdf+xml"u8);
            writer.Write((byte)0);
        }

        EndBox(writer, itemInformationEntry);
    }

    private static void WriteSampleTable(
        BinaryWriter writer,
        uint chunkOffset,
        bool hevc,
        bool compositionOffsets,
        bool directReferences,
        uint directReferenceSampleId,
        bool trackProperties,
        bool invalidRotation,
        int width,
        int height,
        byte[] av1Configuration,
        int? sampleSize,
        bool allSamplesSync,
        bool alpha = false)
    {
        long sampleTable = BeginBox(writer, Heif4CharCode.Stbl);
        WriteSampleDescription(writer, hevc, trackProperties, invalidRotation, width, height, av1Configuration, allSamplesSync, alpha);

        long timing = BeginBox(writer, Heif4CharCode.Stts);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, 1);
        WriteUInt32(writer, 2);
        WriteUInt32(writer, 100);
        EndBox(writer, timing);

        long sampleToChunk = BeginBox(writer, Heif4CharCode.Stsc);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, 1);
        WriteUInt32(writer, 1);
        WriteUInt32(writer, 2);
        WriteUInt32(writer, 1);
        EndBox(writer, sampleToChunk);

        long sampleSizes = BeginBox(writer, Heif4CharCode.Stsz);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 2);
        WriteUInt32(writer, (uint)(sampleSize ?? 10));
        WriteUInt32(writer, (uint)(sampleSize ?? 12));
        EndBox(writer, sampleSizes);

        long chunkOffsets = BeginBox(writer, Heif4CharCode.Stco);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, 1);
        WriteUInt32(writer, chunkOffset);
        EndBox(writer, chunkOffsets);

        long syncSamples = BeginBox(writer, Heif4CharCode.Stss);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, allSamplesSync ? 2U : 1U);
        WriteUInt32(writer, 1);
        if (allSamplesSync)
        {
            WriteUInt32(writer, 2);
        }

        EndBox(writer, syncSamples);

        if (compositionOffsets)
        {
            long offsets = BeginBox(writer, Heif4CharCode.Ctts);
            WriteFullBoxHeader(writer, 1, 0);
            WriteUInt32(writer, 2);
            WriteUInt32(writer, 1);
            WriteUInt32(writer, 0x80000000);
            WriteUInt32(writer, 1);
            WriteUInt32(writer, 0);
            EndBox(writer, offsets);

            long compositionToDecode = BeginBox(writer, Heif4CharCode.Cslg);
            WriteFullBoxHeader(writer, 0, 0);
            WriteUInt32(writer, 0);
            WriteUInt32(writer, 0);
            WriteUInt32(writer, 0);
            WriteUInt32(writer, 100);
            WriteUInt32(writer, 200);
            EndBox(writer, compositionToDecode);
        }

        if (directReferences)
        {
            WriteDirectReferenceSampleGroup(writer, directReferenceSampleId);
        }

        EndBox(writer, sampleTable);
    }

    private static void WriteDirectReferenceSampleGroup(BinaryWriter writer, uint directReferenceSampleId)
    {
        long descriptions = BeginBox(writer, Heif4CharCode.Sgpd);
        WriteFullBoxHeader(writer, 1, 0);
        WriteUInt32(writer, (uint)Heif4CharCode.Refs);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 2);
        WriteUInt32(writer, 5);
        WriteUInt32(writer, 1);
        writer.Write((byte)0);
        WriteUInt32(writer, 9);
        WriteUInt32(writer, 0);
        writer.Write((byte)1);
        WriteUInt32(writer, directReferenceSampleId);
        EndBox(writer, descriptions);

        long sampleMap = BeginBox(writer, Heif4CharCode.Sbgp);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, (uint)Heif4CharCode.Refs);
        WriteUInt32(writer, 2);
        WriteUInt32(writer, 1);
        WriteUInt32(writer, 1);
        WriteUInt32(writer, 1);
        WriteUInt32(writer, 2);
        EndBox(writer, sampleMap);
    }

    private static void WriteSampleDescription(
        BinaryWriter writer,
        bool hevc,
        bool trackProperties,
        bool invalidRotation,
        int width,
        int height,
        byte[] av1Configuration,
        bool allSamplesSync,
        bool alpha)
    {
        long description = BeginBox(writer, Heif4CharCode.Stsd);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, 1);
        long sampleEntry = BeginBox(writer, hevc ? Heif4CharCode.Hvc1 : Heif4CharCode.Av01);
        WriteZeros(writer, 6);
        WriteUInt16(writer, 1);
        WriteZeros(writer, 16);
        WriteUInt16(writer, (ushort)width);
        WriteUInt16(writer, (ushort)height);
        WriteUInt32(writer, 0x00480000);
        WriteUInt32(writer, 0x00480000);
        WriteUInt32(writer, 0);
        WriteUInt16(writer, 1);
        WriteZeros(writer, 32);
        WriteUInt16(writer, 0x18);
        WriteUInt16(writer, ushort.MaxValue);

        if (hevc)
        {
            WriteHevcConfiguration(writer);
        }
        else
        {
            long configuration = BeginBox(writer, Heif4CharCode.Av1C);
            writer.Write(av1Configuration ?? [0x81, 0, 0, 0]);
            EndBox(writer, configuration);
        }

        if (alpha)
        {
            long auxiliaryType = BeginBox(writer, Heif4CharCode.Auxi);
            WriteFullBoxHeader(writer, 0, 0);
            writer.Write(Encoding.UTF8.GetBytes(HeifConstants.AlphaAuxiliaryType));
            writer.Write((byte)0);
            EndBox(writer, auxiliaryType);
        }

        if (trackProperties)
        {
            WriteTrackImageProperties(writer, invalidRotation, width, height);
        }

        long codingConstraints = BeginBox(writer, Heif4CharCode.Ccst);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, allSamplesSync ? 0xFC000000U : 0x7C000000U);
        EndBox(writer, codingConstraints);
        EndBox(writer, sampleEntry);
        EndBox(writer, description);
    }

    private static void WriteTrackImageProperties(BinaryWriter writer, bool invalidRotation, int width, int height)
    {
        long color = BeginBox(writer, Heif4CharCode.Colr);
        WriteUInt32(writer, (uint)Heif4CharCode.Nclx);
        WriteUInt16(writer, 1);
        WriteUInt16(writer, 13);
        WriteUInt16(writer, 6);
        writer.Write((byte)0x80);
        EndBox(writer, color);

        long pixelAspectRatio = BeginBox(writer, Heif4CharCode.Pasp);
        WriteUInt32(writer, 4);
        WriteUInt32(writer, 3);
        EndBox(writer, pixelAspectRatio);

        long cleanAperture = BeginBox(writer, Heif4CharCode.Clap);
        WriteUInt32(writer, (uint)width);
        WriteUInt32(writer, 1);
        WriteUInt32(writer, (uint)height);
        WriteUInt32(writer, 1);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 1);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 1);
        EndBox(writer, cleanAperture);

        long rotation = BeginBox(writer, Heif4CharCode.Irot);
        writer.Write(invalidRotation ? (byte)0xFC : (byte)1);
        EndBox(writer, rotation);

        long mirror = BeginBox(writer, Heif4CharCode.Imir);
        writer.Write((byte)1);
        EndBox(writer, mirror);

        long contentLightLevel = BeginBox(writer, Heif4CharCode.Clli);
        WriteUInt16(writer, 1000);
        WriteUInt16(writer, 400);
        EndBox(writer, contentLightLevel);

        long masteringDisplay = BeginBox(writer, Heif4CharCode.Mdcv);
        WriteUInt16(writer, 15000);
        WriteUInt16(writer, 30000);
        WriteUInt16(writer, 7500);
        WriteUInt16(writer, 3000);
        WriteUInt16(writer, 34000);
        WriteUInt16(writer, 16000);
        WriteUInt16(writer, 15635);
        WriteUInt16(writer, 16450);
        WriteUInt32(writer, 10_000_000);
        WriteUInt32(writer, 50);
        EndBox(writer, masteringDisplay);

        long contentColorVolume = BeginBox(writer, Heif4CharCode.Cclv);
        writer.Write((byte)0x1C);
        WriteUInt32(writer, 1_000_000);
        WriteUInt32(writer, 10_000_000);
        WriteUInt32(writer, 5_000_000);
        EndBox(writer, contentColorVolume);

        long ambientViewing = BeginBox(writer, Heif4CharCode.Amve);
        WriteUInt32(writer, 10_000);
        WriteUInt16(writer, 15_635);
        WriteUInt16(writer, 16_450);
        EndBox(writer, ambientViewing);

        long referenceViewing = BeginBox(writer, Heif4CharCode.Reve);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, 10_000);
        WriteUInt16(writer, 3_127);
        WriteUInt16(writer, 3_290);
        WriteUInt32(writer, 5_000);
        WriteUInt16(writer, 3_127);
        WriteUInt16(writer, 3_290);
        EndBox(writer, referenceViewing);

        long nominalDiffuseWhite = BeginBox(writer, Heif4CharCode.Ndwt);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, 2_030_000);
        EndBox(writer, nominalDiffuseWhite);
    }

    private static void WriteHevcConfiguration(BinaryWriter writer)
    {
        long configuration = BeginBox(writer, Heif4CharCode.HvcC);
        writer.Write((byte)1);
        writer.Write((byte)1);
        WriteUInt32(writer, 0);
        WriteZeros(writer, 6);
        writer.Write((byte)0);
        WriteUInt16(writer, 0xF000);
        writer.Write((byte)0xFC);
        writer.Write((byte)0xFD);
        writer.Write((byte)0xF8);
        writer.Write((byte)0xF8);
        WriteUInt16(writer, 0);
        writer.Write((byte)3);
        writer.Write((byte)0);
        EndBox(writer, configuration);
    }

    private static long BeginBox(BinaryWriter writer, Heif4CharCode type)
    {
        long start = writer.BaseStream.Position;
        WriteUInt32(writer, 0);
        WriteUInt32(writer, (uint)type);
        return start;
    }

    private static void EndBox(BinaryWriter writer, long start)
    {
        long end = writer.BaseStream.Position;
        writer.BaseStream.Position = start;
        WriteUInt32(writer, checked((uint)(end - start)));
        writer.BaseStream.Position = end;
    }

    private static void WriteFullBoxHeader(BinaryWriter writer, byte version, uint flags)
        => WriteUInt32(writer, ((uint)version << 24) | flags);

    private static void WriteUInt16(BinaryWriter writer, ushort value)
        => writer.Write(BinaryPrimitives.ReverseEndianness(value));

    private static void WriteUInt32(BinaryWriter writer, uint value)
        => writer.Write(BinaryPrimitives.ReverseEndianness(value));

    private static void WriteZeros(BinaryWriter writer, int count) => writer.Write(new byte[count]);

    private static int GetMoviePayloadLength(byte[] data)
        => checked((int)BinaryPrimitives.ReadUInt32BigEndian(data) - 8);

    private static HeifSequenceParser CreateParser(
        uint maxFrames,
        bool skipMetadata = false,
        SegmentIntegrityHandling segmentIntegrityHandling = SegmentIntegrityHandling.Strict)
        => new(new DecoderOptions
        {
            MaxFrames = maxFrames,
            SkipMetadata = skipMetadata,
            SegmentIntegrityHandling = segmentIntegrityHandling
        });
}
