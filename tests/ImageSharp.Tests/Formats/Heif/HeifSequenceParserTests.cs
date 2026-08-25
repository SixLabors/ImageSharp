// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Text;
using SixLabors.ImageSharp.Formats.Heif;

namespace SixLabors.ImageSharp.Tests.Formats.Heif;

[Trait("Format", "Heif")]
[ValidateDisposedMemoryAllocations]
public class HeifSequenceParserTests
{
    [Fact]
    public void ParseResolvesLibavifShapedSampleTable()
    {
        byte[] data = CreateSequenceFile(1024);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = new(Configuration.Default.MemoryAllocator, 2);
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
        HeifSequenceParser parser = new(Configuration.Default.MemoryAllocator, 1);
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
        HeifSequenceParser parser = new(Configuration.Default.MemoryAllocator, 2);
        stream.Position = 8;

        Assert.Throws<InvalidImageContentException>(() => parser.Parse(stream, GetMoviePayloadLength(data)));
    }

    [Fact]
    public void ParseMarksHiddenHevcSamples()
    {
        byte[] data = CreateSequenceFile(1024, hevc: true, compositionOffsets: true);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = new(Configuration.Default.MemoryAllocator, 2);
        stream.Position = 8;

        HeifSequence sequence = parser.Parse(stream, GetMoviePayloadLength(data));

        Assert.Equal(Heif4CharCode.Hvc1, sequence.ColorTrack.CodecType);
        Assert.NotNull(sequence.ColorTrack.HevcCodecConfiguration);
        Assert.True(sequence.ColorTrack.Samples[0].IsHidden);
        Assert.Equal(long.MinValue, sequence.ColorTrack.Samples[0].CompositionTime);
        Assert.False(sequence.ColorTrack.Samples[1].IsHidden);
        Assert.Equal(100, sequence.ColorTrack.Samples[1].CompositionTime);
    }

    [Fact]
    public void ParseRejectsCompositionOffsetsForAv1()
    {
        byte[] data = CreateSequenceFile(1024, compositionOffsets: true);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = new(Configuration.Default.MemoryAllocator, 2);
        stream.Position = 8;

        Assert.Throws<InvalidImageContentException>(() => parser.Parse(stream, GetMoviePayloadLength(data)));
    }

    [Fact]
    public void ParseResolvesDirectReferenceSamples()
    {
        byte[] data = CreateSequenceFile(1024, directReferences: true);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = new(Configuration.Default.MemoryAllocator, 2);
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
        HeifSequenceParser parser = new(Configuration.Default.MemoryAllocator, 2);
        stream.Position = 8;

        Assert.Throws<InvalidImageContentException>(() => parser.Parse(stream, GetMoviePayloadLength(data)));
    }

    [Fact]
    public void ParseRetainsTrackImageProperties()
    {
        byte[] data = CreateSequenceFile(1024, trackProperties: true);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = new(Configuration.Default.MemoryAllocator, 2);
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

    private static byte[] CreateSequenceFile(
        uint chunkOffset,
        bool hevc = false,
        bool compositionOffsets = false,
        bool directReferences = false,
        uint directReferenceSampleId = 1,
        bool trackProperties = false)
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
        WriteZeros(writer, 80);
        EndBox(writer, movieHeader);

        long track = BeginBox(writer, Heif4CharCode.Trak);
        WriteTrackHeader(writer);
        WriteEditList(writer);

        long media = BeginBox(writer, Heif4CharCode.Mdia);
        WriteMediaHeader(writer);
        WriteHandler(writer, Heif4CharCode.Pict);

        long mediaInformation = BeginBox(writer, Heif4CharCode.Minf);
        WriteDataInformation(writer);
        WriteSampleTable(writer, chunkOffset, hevc, compositionOffsets, directReferences, directReferenceSampleId, trackProperties);
        EndBox(writer, mediaInformation);
        EndBox(writer, media);
        EndBox(writer, track);
        EndBox(writer, movie);

        byte[] movieBytes = stream.ToArray();
        byte[] file = new byte[2048];

        movieBytes.CopyTo(file, 0);
        return file;
    }

    private static void WriteTrackHeader(BinaryWriter writer)
    {
        long trackHeader = BeginBox(writer, Heif4CharCode.Tkhd);
        WriteFullBoxHeader(writer, 0, 3);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 1);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 600);
        WriteZeros(writer, 16);
        WriteUInt32(writer, 0x00010000);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0x00010000);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0x40000000);
        WriteUInt32(writer, 320U << 16);
        WriteUInt32(writer, 240U << 16);
        EndBox(writer, trackHeader);
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

    private static void WriteMediaHeader(BinaryWriter writer)
    {
        long mediaHeader = BeginBox(writer, Heif4CharCode.Mdhd);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 1000);
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

    private static void WriteSampleTable(
        BinaryWriter writer,
        uint chunkOffset,
        bool hevc,
        bool compositionOffsets,
        bool directReferences,
        uint directReferenceSampleId,
        bool trackProperties)
    {
        long sampleTable = BeginBox(writer, Heif4CharCode.Stbl);
        WriteSampleDescription(writer, hevc, trackProperties);

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
        WriteUInt32(writer, 10);
        WriteUInt32(writer, 12);
        EndBox(writer, sampleSizes);

        long chunkOffsets = BeginBox(writer, Heif4CharCode.Stco);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, 1);
        WriteUInt32(writer, chunkOffset);
        EndBox(writer, chunkOffsets);

        long syncSamples = BeginBox(writer, Heif4CharCode.Stss);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, 1);
        WriteUInt32(writer, 1);
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

    private static void WriteSampleDescription(BinaryWriter writer, bool hevc, bool trackProperties)
    {
        long description = BeginBox(writer, Heif4CharCode.Stsd);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, 1);
        long sampleEntry = BeginBox(writer, hevc ? Heif4CharCode.Hvc1 : Heif4CharCode.Av01);
        WriteZeros(writer, 6);
        WriteUInt16(writer, 1);
        WriteZeros(writer, 16);
        WriteUInt16(writer, 320);
        WriteUInt16(writer, 240);
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
            writer.Write(new byte[] { 0x81, 0, 0, 0 });
            EndBox(writer, configuration);
        }

        if (trackProperties)
        {
            WriteTrackImageProperties(writer);
        }

        long codingConstraints = BeginBox(writer, Heif4CharCode.Ccst);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, 0x7C000000);
        EndBox(writer, codingConstraints);
        EndBox(writer, sampleEntry);
        EndBox(writer, description);
    }

    private static void WriteTrackImageProperties(BinaryWriter writer)
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
        WriteUInt32(writer, 320);
        WriteUInt32(writer, 1);
        WriteUInt32(writer, 240);
        WriteUInt32(writer, 1);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 1);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 1);
        EndBox(writer, cleanAperture);

        long rotation = BeginBox(writer, Heif4CharCode.Irot);
        writer.Write((byte)1);
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
}
