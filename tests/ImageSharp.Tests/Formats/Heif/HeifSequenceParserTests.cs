// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Text;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Tests.Formats.Heif;

/// <summary>
/// Verifies HEIF image-sequence parsing with upstream libavif files and narrowly constructed invalid containers.
/// </summary>
[Trait("Format", "Heif")]
[ValidateDisposedMemoryAllocations]
public class HeifSequenceParserTests
{
    private const int BoxHeaderLength = 8;
    private const int FileTypeBoxLength = 24;
    private const int SyntheticFileLength = 2048;
    private const uint SyntheticChunkOffset = 1024;
    private const uint SyntheticMovieTimescale = 1000;
    private const uint SyntheticTrackDuration = 600;
    private const uint SyntheticMediaDuration = 200;
    private const uint SyntheticSampleDuration = 100;
    private const uint SyntheticSampleCount = 2;
    private const int SyntheticWidth = 320;
    private const int SyntheticHeight = 240;
    private const int FirstSyntheticSampleLength = 10;
    private const int SecondSyntheticSampleLength = 12;
    private const uint ColorTrackId = 1;
    private const uint AlphaTrackId = 2;
    private const uint UnrelatedTrackId = 3;
    private const uint MismatchedAlphaTimescale = 2000;
    private const uint UnityFixed16Point16 = 1U << 16;
    private const uint DoubleFixed16Point16 = 2U << 16;
    private const uint UnityFixed2Point30 = 1U << 30;
    private const ushort UnityFixed8Point8 = 1 << 8;
    private const ushort PackedUndeterminedLanguage = 0x55C4;
    private const uint SyntheticHorizontalResolution = 72U << 16;
    private const ushort SyntheticPixelDepth = 24;
    private const ushort SyntheticMaximumContentLightLevel = 1000;
    private const ushort SyntheticMaximumFrameAverageLightLevel = 400;
    private const uint SyntheticHorizontalPixelSpacing = 4;
    private const uint SyntheticVerticalPixelSpacing = 3;
    private const ushort TrackExifItemId = 1;
    private const ushort TrackXmpItemId = 2;
    private const int OrangeAv1ConfigurationOffset = 0xC7;
    private const int OrangeAv1ConfigurationLength = 4;
    private const int OrangeAv1SampleOffset = 0x10E;
    private const int OrangeAv1SampleLength = 0x1D;
    private const int TrackExifOffset = 1800;
    private const int TrackXmpOffset = 1840;
    private const int LibavifAnimationFrameCount = 5;
    private const int LibavifAnimationSize = 150;
    private const int LibavifKeyframeAnimationSize = 64;
    private const int FinitePlayCount = 1;
    private const int InfinitePlayCount = 0;
    private const byte InvalidAv1SampleByte = 0x80;

    /// <summary>
    /// Gets the minimal little-endian TIFF payload stored in the synthetic track-level Exif item.
    /// </summary>
    private static ReadOnlySpan<byte> TrackExifData => [0, 0, 0, 0, 0x49, 0x49, 0x2A, 0, 8, 0, 0, 0];

    /// <summary>
    /// Gets the minimal XMP packet stored in the synthetic track-level MIME item.
    /// </summary>
    private static ReadOnlySpan<byte> TrackXmpData => "<x:xmpmeta/>"u8;

    /// <summary>
    /// Gets the minimal AV1CodecConfigurationBox payload for profile zero, level zero, and an absent initial
    /// presentation-delay field. The high marker and version bits encode marker one and configuration version one.
    /// </summary>
    private static ReadOnlySpan<byte> DefaultAv1Configuration => [0x81, 0, 0, 0];

    /// <summary>
    /// Verifies that genuine libavif animation files are identified from their image-sequence tracks rather than
    /// from the fallback primary item. The audio variant must produce the same image description because non-image
    /// tracks are deliberately outside the decoder's retained ISOBMFF surface.
    /// </summary>
    /// <param name="imagePath">The libavif animation fixture to identify.</param>
    [Theory]
    [InlineData(TestImages.Heif.Animated8Bit)]
    [InlineData(TestImages.Heif.Animated8BitWithAudio)]
    public void IdentifyReadsRealLibavifSequence(string imagePath)
    {
        TestFile file = TestFile.Create(imagePath);

        ImageInfo info = Image.Identify(file.Bytes);
        HeifMetadata metadata = info.Metadata.GetHeifMetadata();

        Assert.Equal(new Size(LibavifAnimationSize, LibavifAnimationSize), info.Size);
        Assert.Equal(LibavifAnimationFrameCount, info.FrameCount);
        Assert.Equal(HeifCompressionMethod.Av1, metadata.CompressionMethod);
        Assert.Equal(HeifBitDepth.Bit8, metadata.BitDepth);
        Assert.Equal(FinitePlayCount, metadata.RepeatCount);
        Assert.False(metadata.HasAlpha);
    }

    /// <summary>
    /// Verifies that a genuine libavif animation carries its linked alpha track, infinite repetition, and metadata
    /// items through the public sequence metadata boundary.
    /// </summary>
    [Fact]
    public void IdentifyReadsRealLibavifSequenceWithAlphaAndMetadata()
    {
        TestFile file = TestFile.Create(TestImages.Heif.Animated8BitWithAlphaExifXmp);

        ImageInfo info = Image.Identify(file.Bytes);
        HeifMetadata metadata = info.Metadata.GetHeifMetadata();

        Assert.Equal(new Size(LibavifAnimationSize, LibavifAnimationSize), info.Size);
        Assert.Equal(LibavifAnimationFrameCount, info.FrameCount);
        Assert.Equal(HeifCompressionMethod.Av1, metadata.CompressionMethod);
        Assert.Equal(HeifBitDepth.Bit8, metadata.BitDepth);
        Assert.Equal(InfinitePlayCount, metadata.RepeatCount);
        Assert.True(metadata.HasAlpha);
        Assert.NotNull(info.Metadata.ExifProfile);
        Assert.NotNull(info.Metadata.XmpProfile);
    }

    /// <summary>
    /// Verifies that a genuine 12-bit libavif sequence with inter-frame dependencies is identified as all five frames
    /// instead of falling back to its primary image item.
    /// </summary>
    [Fact]
    public void IdentifyReadsReal12BitLibavifSequence()
    {
        TestFile file = TestFile.Create(TestImages.Heif.Animated12BitWithKeyframes);

        ImageInfo info = Image.Identify(file.Bytes);
        HeifMetadata metadata = info.Metadata.GetHeifMetadata();

        Assert.Equal(new Size(LibavifKeyframeAnimationSize, LibavifKeyframeAnimationSize), info.Size);
        Assert.Equal(LibavifAnimationFrameCount, info.FrameCount);
        Assert.Equal(HeifCompressionMethod.Av1, metadata.CompressionMethod);
        Assert.Equal(HeifBitDepth.Bit12, metadata.BitDepth);
    }

    /// <summary>
    /// Verifies that identification transfers bounded sequence, track property, metadata-item, and frame-timing
    /// information from the parser into the public image metadata model.
    /// </summary>
    [Fact]
    public void IdentifyReturnsBoundedSequenceAndFrameMetadata()
    {
        byte[] data = CreateSequenceContainer(trackProperties: true, trackMetadata: true);

        ImageInfo info = Image.Identify(data);
        HeifMetadata metadata = info.Metadata.GetHeifMetadata();

        Assert.Equal(new Size(SyntheticHeight, SyntheticWidth), info.Size);
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
            frame => Assert.Equal(new Rational(SyntheticSampleDuration, SyntheticMovieTimescale), frame.GetHeifMetadata().FrameDelay));
    }

    /// <summary>
    /// Verifies that <see cref="DecoderOptions.SkipMetadata"/> omits ancillary sequence profiles and item metadata
    /// without discarding structural codec, repetition, or frame-timing information required to describe the image.
    /// </summary>
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
            frame => Assert.Equal(new Rational(SyntheticSampleDuration, SyntheticMovieTimescale), frame.GetHeifMetadata().FrameDelay));
    }

    /// <summary>
    /// Verifies that two independently addressable AV1 samples become two complete ImageSharp frames with the
    /// expected pixels and per-frame duration.
    /// </summary>
    [Fact]
    public void DecodeAdoptsIndependentAv1SamplesAsImageFrames()
    {
        byte[] source = TestFile.Create(TestImages.Heif.Orange4x4).Bytes;
        byte[] data = CreateDecodableAv1SequenceContainer(
            source.AsSpan(OrangeAv1SampleOffset, OrangeAv1SampleLength),
            source.AsSpan(OrangeAv1ConfigurationOffset, OrangeAv1ConfigurationLength));

        using Image<Rgba32> expected = Image.Load<Rgba32>(source);
        using Image<Rgba32> actual = Image.Load<Rgba32>(data);

        Assert.Equal(new Size(4, 4), actual.Size);
        Assert.Equal(2, actual.Frames.Count);
        foreach (ImageFrame<Rgba32> frame in actual.Frames)
        {
            Assert.Equal(
                new Rational(SyntheticSampleDuration, SyntheticMovieTimescale),
                frame.Metadata.GetHeifMetadata().FrameDelay);

            for (int y = 0; y < frame.Height; y++)
            {
                Assert.True(
                    frame.PixelBuffer.DangerousGetRowSpan(y)
                        .SequenceEqual(expected.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y)));
            }
        }
    }

    [Fact]
    public void DecodeAppliesTrackPresentationPropertiesToEveryFrame()
    {
        byte[] source = TestFile.Create(TestImages.Heif.Orange4x4).Bytes;
        byte[] data = CreateDecodableAv1SequenceContainer(
            source.AsSpan(OrangeAv1SampleOffset, OrangeAv1SampleLength),
            source.AsSpan(OrangeAv1ConfigurationOffset, OrangeAv1ConfigurationLength),
            trackProperties: true);

        int cleanApertureTypeOffset = data.AsSpan().IndexOf("clap"u8);
        Assert.True(cleanApertureTypeOffset >= 0);

        // Narrow the synthetic full-frame aperture to its centered 2x2 region without changing the coded AV1 sample.
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(cleanApertureTypeOffset + 4), 2);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(cleanApertureTypeOffset + 12), 2);

        using Image<Rgba32> expected = Image.Load<Rgba32>(source);
        expected.Mutate(context => context
            .Crop(new Rectangle(1, 1, 2, 2))
            .Rotate(RotateMode.Rotate270)
            .Flip(FlipMode.Horizontal));

        using Image<Rgba32> actual = Image.Load<Rgba32>(data);

        Assert.Equal(expected.Size, actual.Size);
        Assert.Equal(2, actual.Frames.Count);
        Assert.Equal(4D, actual.Metadata.HorizontalResolution);
        Assert.Equal(3D, actual.Metadata.VerticalResolution);
        Assert.Equal(PixelResolutionUnit.AspectRatio, actual.Metadata.ResolutionUnits);
        Assert.NotNull(actual.Metadata.CicpProfile);
        for (int frameIndex = 0; frameIndex < actual.Frames.Count; frameIndex++)
        {
            Assert.NotNull(actual.Frames[frameIndex].Metadata.CicpProfile);
            for (int y = 0; y < actual.Height; y++)
            {
                Assert.True(
                    expected.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y)
                        .SequenceEqual(actual.Frames[frameIndex].PixelBuffer.DangerousGetRowSpan(y)));
            }
        }
    }

    /// <summary>
    /// Verifies that strict and ancillary-tolerant decoding both reject corrupt coded image data because neither
    /// integrity mode permits recovery from errors in a retained AV1 sample.
    /// </summary>
    /// <param name="handling">The segment-integrity policy applied at the decoder boundary.</param>
    [Theory]
    [InlineData(SegmentIntegrityHandling.Strict)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary)]
    public void DecodeRejectsInvalidAv1SampleUnlessImageDataErrorsAreIgnored(SegmentIntegrityHandling handling)
    {
        byte[] source = TestFile.Create(TestImages.Heif.Orange4x4).Bytes;
        byte[] data = CreateDecodableAv1SequenceContainer(
            source.AsSpan(OrangeAv1SampleOffset, OrangeAv1SampleLength),
            [InvalidAv1SampleByte],
            source.AsSpan(OrangeAv1ConfigurationOffset, OrangeAv1ConfigurationLength),
            false);

        DecoderOptions options = new() { SegmentIntegrityHandling = handling };

        Assert.Throws<InvalidImageContentException>(() => Image.Load<Rgba32>(options, data));
    }

    /// <summary>
    /// Verifies that image-data tolerance drops an invalid non-root AV1 sample while preserving the valid frame.
    /// </summary>
    [Fact]
    public void DecodeSkipsInvalidAv1SampleWhenImageDataErrorsAreIgnored()
    {
        byte[] source = TestFile.Create(TestImages.Heif.Orange4x4).Bytes;
        byte[] data = CreateDecodableAv1SequenceContainer(
            source.AsSpan(OrangeAv1SampleOffset, OrangeAv1SampleLength),
            [InvalidAv1SampleByte],
            source.AsSpan(OrangeAv1ConfigurationOffset, OrangeAv1ConfigurationLength),
            false);

        DecoderOptions options = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.IgnoreImageData };

        using Image<Rgba32> image = Image.Load<Rgba32>(options, data);

        Assert.Equal(new Size(4, 4), image.Size);
        Assert.Single(image.Frames);
    }

    /// <summary>
    /// Verifies that an AV1 alpha track whose sequence header is not monochrome is rejected at the codec boundary.
    /// </summary>
    [Fact]
    public void DecodeRejectsNonMonochromeAv1AlphaSamples()
    {
        byte[] source = TestFile.Create(TestImages.Heif.Orange4x4).Bytes;
        byte[] data = CreateAv1SequenceWithNonMonochromeAlphaContainer(
            source.AsSpan(OrangeAv1SampleOffset, OrangeAv1SampleLength),
            source.AsSpan(OrangeAv1ConfigurationOffset, OrangeAv1ConfigurationLength));

        Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(data);
        });
    }

    /// <summary>
    /// Verifies that a genuine libavif alpha sequence composes every retained frame from the linked monochrome
    /// auxiliary track instead of returning any color frame as opaque.
    /// </summary>
    [Fact]
    public void DecodeComposesEveryRealLibavifAlphaSequenceFrame()
    {
        TestFile file = TestFile.Create(TestImages.Heif.Animated8BitWithAlphaExifXmp);

        using Image<Rgba32> image = Image.Load<Rgba32>(file.Bytes);

        Assert.Equal(LibavifAnimationFrameCount, image.Frames.Count);
        Assert.True(image.Metadata.GetHeifMetadata().HasAlpha);
        Assert.NotNull(image.Metadata.ExifProfile);
        Assert.NotNull(image.Metadata.XmpProfile);
        foreach (ImageFrame<Rgba32> frame in image.Frames)
        {
            bool hasNonOpaqueSample = false;
            for (int y = 0; y < frame.Height && !hasNonOpaqueSample; y++)
            {
                foreach (Rgba32 pixel in frame.PixelBuffer.DangerousGetRowSpan(y))
                {
                    if (pixel.A != byte.MaxValue)
                    {
                        hasNonOpaqueSample = true;
                        break;
                    }
                }
            }

            Assert.True(hasNonOpaqueSample);
            Assert.True(frame.Metadata.GetHeifMetadata().FrameDelay.Numerator > 0);
            Assert.True(frame.Metadata.GetHeifMetadata().FrameDelay.Denominator > 0);
        }
    }

    /// <summary>
    /// Verifies the complete libavif-shaped sample-table mapping, including timing, chunk offsets, sync status,
    /// coding constraints, and the normalized sequence play count.
    /// </summary>
    [Fact]
    public void ParseResolvesLibavifShapedSampleTable()
    {
        byte[] data = CreateSequenceFile(SyntheticChunkOffset);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(SyntheticSampleCount);
        stream.Position = BoxHeaderLength;

        HeifSequence sequence = parser.Parse(stream, GetMoviePayloadLength(data));

        Assert.Equal(SyntheticMovieTimescale, sequence.MovieTimescale);
        Assert.Null(sequence.AlphaTrack);
        Assert.Equal(ColorTrackId, sequence.ColorTrack.Id);
        Assert.Equal(SyntheticWidth, sequence.ColorTrack.Width);
        Assert.Equal(SyntheticHeight, sequence.ColorTrack.Height);
        Assert.Equal(Heif4CharCode.Av01, sequence.ColorTrack.CodecType);
        Assert.NotNull(sequence.ColorTrack.Av1CodecConfiguration);
        Assert.Equal(SyntheticSampleCount, sequence.ColorTrack.TotalSampleCount);
        Assert.Equal(3, sequence.ColorTrack.RepeatCount);
        Assert.False(sequence.ColorTrack.AllReferencePicturesIntra);
        Assert.True(sequence.ColorTrack.IntraPicturePredictionUsed);
        Assert.Equal(15, sequence.ColorTrack.MaximumReferencesPerPicture);
        Assert.Collection(
            sequence.ColorTrack.Samples,
            sample =>
            {
                Assert.Equal(SyntheticChunkOffset, sample.Offset);
                Assert.Equal(FirstSyntheticSampleLength, sample.Length);
                Assert.Equal(SyntheticSampleDuration, sample.Duration);
                Assert.True(sample.IsSync);
            },
            sample =>
            {
                Assert.Equal((long)SyntheticChunkOffset + FirstSyntheticSampleLength, sample.Offset);
                Assert.Equal(SecondSyntheticSampleLength, sample.Length);
                Assert.Equal(SyntheticSampleDuration, sample.Duration);
                Assert.False(sample.IsSync);
            });
    }

    /// <summary>
    /// Verifies that the parser does not select a picture track whose TrackHeaderBox clears the enabled flag.
    /// </summary>
    [Fact]
    public void ParseRejectsDisabledPictureTrack()
    {
        byte[] data = CreateSequenceFile(SyntheticChunkOffset, trackEnabled: false);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(SyntheticSampleCount);
        stream.Position = BoxHeaderLength;

        Assert.Throws<InvalidImageContentException>(() => parser.Parse(stream, GetMoviePayloadLength(data)));
    }

    /// <summary>
    /// Verifies that the parser retains only the configured maximum number of frames while preserving the track's
    /// declared total sample count.
    /// </summary>
    [Fact]
    public void ParseRetainsOnlyConfiguredFrameCount()
    {
        const uint retainedFrameLimit = 1;

        byte[] data = CreateSequenceFile(SyntheticChunkOffset);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(retainedFrameLimit);
        stream.Position = BoxHeaderLength;

        HeifSequence sequence = parser.Parse(stream, GetMoviePayloadLength(data));

        Assert.Equal(SyntheticSampleCount, sequence.ColorTrack.TotalSampleCount);
        HeifSequenceSample sample = Assert.Single(sequence.ColorTrack.Samples);
        Assert.Equal(SyntheticChunkOffset, sample.Offset);
        Assert.Equal(FirstSyntheticSampleLength, sample.Length);
        Assert.Equal(SyntheticSampleDuration, sample.Duration);
    }

    /// <summary>
    /// Verifies that a retained sample whose declared byte range extends beyond the source stream is rejected.
    /// </summary>
    [Fact]
    public void ParseRejectsRetainedSampleBeyondFile()
    {
        uint truncatedChunkOffset = SyntheticFileLength - BoxHeaderLength;
        byte[] data = CreateSequenceFile(truncatedChunkOffset);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(SyntheticSampleCount);
        stream.Position = BoxHeaderLength;

        Assert.Throws<InvalidImageContentException>(() => parser.Parse(stream, GetMoviePayloadLength(data)));
    }

    /// <summary>
    /// Verifies that presentation transforms at either the movie or track level are rejected until the decoder can
    /// apply those matrices to the emitted raster.
    /// </summary>
    /// <param name="nonIdentityMovieMatrix">Whether the movie header contains a non-unity matrix.</param>
    /// <param name="nonIdentityTrackMatrix">Whether the track header contains a non-unity matrix.</param>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ParseRejectsNonIdentityMoviePresentationMatrix(bool nonIdentityMovieMatrix, bool nonIdentityTrackMatrix)
    {
        byte[] data = CreateSequenceFile(
            SyntheticChunkOffset,
            nonIdentityMovieMatrix: nonIdentityMovieMatrix,
            nonIdentityTrackMatrix: nonIdentityTrackMatrix);

        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(SyntheticSampleCount);
        stream.Position = BoxHeaderLength;

        Assert.Throws<NotSupportedException>(() => parser.Parse(stream, GetMoviePayloadLength(data)));
    }

    /// <summary>
    /// Verifies that auxiliary-track and premultiplication references are resolved by track identifier and remain
    /// valid when matching presentation properties are present on either track.
    /// </summary>
    /// <param name="colorTransforms">Whether the color sample entry carries presentation properties.</param>
    /// <param name="alphaTransforms">Whether the alpha sample entry carries matching presentation properties.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ParseMatchesAlphaTrackAndPremultiplicationByTrackId(bool colorTransforms, bool alphaTransforms)
    {
        byte[] data = CreateSequenceFileWithAlpha(
            SyntheticChunkOffset,
            SyntheticMovieTimescale,
            AlphaTrackId,
            colorTransforms,
            alphaTransforms);

        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(SyntheticSampleCount);
        stream.Position = BoxHeaderLength;

        HeifSequence sequence = parser.Parse(stream, GetMoviePayloadLength(data));

        Assert.NotNull(sequence.AlphaTrack);
        Assert.Equal(AlphaTrackId, sequence.AlphaTrack.Id);
        Assert.True(sequence.AlphaTrack.IsAlpha);
        Assert.True(sequence.ColorTrack.IsPremultiplied);
    }

    /// <summary>
    /// Verifies that an alpha track with a different media timescale is rejected under policies that do not permit
    /// recovery from image-data inconsistencies.
    /// </summary>
    /// <param name="handling">The segment-integrity policy applied at the parser boundary.</param>
    [Theory]
    [InlineData(SegmentIntegrityHandling.Strict)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary)]
    public void ParseRejectsAlphaTrackWithDifferentDecodeTiming(SegmentIntegrityHandling handling)
    {
        byte[] data = CreateSequenceFileWithAlpha(SyntheticChunkOffset, MismatchedAlphaTimescale, 0);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(SyntheticSampleCount, segmentIntegrityHandling: handling);
        stream.Position = BoxHeaderLength;

        Assert.Throws<InvalidImageContentException>(() => parser.Parse(stream, GetMoviePayloadLength(data)));
    }

    /// <summary>
    /// Verifies that image-data tolerance drops a timing-incompatible alpha track and clears the color track's
    /// premultiplication state.
    /// </summary>
    [Fact]
    public void ParseDropsAlphaTrackWithDifferentDecodeTimingWhenImageDataErrorsAreIgnored()
    {
        byte[] data = CreateSequenceFileWithAlpha(SyntheticChunkOffset, MismatchedAlphaTimescale, 0);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(
            SyntheticSampleCount,
            segmentIntegrityHandling: SegmentIntegrityHandling.IgnoreImageData);

        stream.Position = BoxHeaderLength;

        HeifSequence sequence = parser.Parse(stream, GetMoviePayloadLength(data));

        Assert.Null(sequence.AlphaTrack);
        Assert.False(sequence.ColorTrack.IsPremultiplied);
    }

    /// <summary>
    /// Verifies that a premultiplication reference cannot name a track other than the selected linked alpha track.
    /// </summary>
    [Fact]
    public void ParseRejectsPremultiplicationReferenceToUnrelatedTrack()
    {
        byte[] data = CreateSequenceFileWithAlpha(SyntheticChunkOffset, SyntheticMovieTimescale, UnrelatedTrackId);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(SyntheticSampleCount);
        stream.Position = BoxHeaderLength;

        Assert.Throws<InvalidImageContentException>(() => parser.Parse(stream, GetMoviePayloadLength(data)));
    }

    /// <summary>
    /// Verifies that linked color and alpha tracks with different presentation properties are rejected because they
    /// cannot be composed frame-for-frame into one image sequence.
    /// </summary>
    [Fact]
    public void ParseRejectsMismatchedAlphaPresentationTransforms()
    {
        byte[] data = CreateSequenceFileWithAlpha(SyntheticChunkOffset, SyntheticMovieTimescale, 0, false, true);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(SyntheticSampleCount);
        stream.Position = BoxHeaderLength;

        Assert.Throws<NotSupportedException>(() => parser.Parse(stream, GetMoviePayloadLength(data)));
    }

    /// <summary>
    /// Verifies that AV1 image-sequence tracks reject prohibited composition timing boxes.
    /// </summary>
    [Fact]
    public void ParseRejectsCompositionOffsetsForAv1()
    {
        byte[] data = CreateSequenceFile(SyntheticChunkOffset, compositionOffsets: true);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(SyntheticSampleCount);
        stream.Position = BoxHeaderLength;

        Assert.Throws<InvalidImageContentException>(() => parser.Parse(stream, GetMoviePayloadLength(data)));
    }

    /// <summary>
    /// Verifies that AV1 direct-reference sample groups resolve file-defined sample identifiers into compact
    /// zero-based indices retained by each dependent sample.
    /// </summary>
    [Fact]
    public void ParseResolvesDirectReferenceSamples()
    {
        byte[] data = CreateSequenceFile(SyntheticChunkOffset, directReferences: true);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(SyntheticSampleCount);
        stream.Position = BoxHeaderLength;

        HeifSequence sequence = parser.Parse(stream, GetMoviePayloadLength(data));

        Assert.Equal(new[] { 0 }, sequence.ColorTrack.DirectReferenceSampleIndices);
        Assert.Equal(1U, sequence.ColorTrack.Samples[0].SampleId);
        Assert.Equal(0, sequence.ColorTrack.Samples[0].DirectReferenceCount);
        Assert.Equal(0U, sequence.ColorTrack.Samples[1].SampleId);
        Assert.Equal(0, sequence.ColorTrack.Samples[1].DirectReferenceOffset);
        Assert.Equal(1, sequence.ColorTrack.Samples[1].DirectReferenceCount);
    }

    /// <summary>
    /// Verifies that a direct-reference group cannot name a sample identifier absent from the retained description
    /// table.
    /// </summary>
    [Fact]
    public void ParseRejectsUnknownDirectReferenceSampleId()
    {
        byte[] data = CreateSequenceFile(
            SyntheticChunkOffset,
            directReferences: true,
            directReferenceSampleId: AlphaTrackId);

        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(SyntheticSampleCount);
        stream.Position = BoxHeaderLength;

        Assert.Throws<InvalidImageContentException>(() => parser.Parse(stream, GetMoviePayloadLength(data)));
    }

    /// <summary>
    /// Verifies that image properties nested in the visual sample entry are retained with their specified color,
    /// geometry, orientation, light-level, and viewing-environment values.
    /// </summary>
    [Fact]
    public void ParseRetainsTrackImageProperties()
    {
        byte[] data = CreateSequenceFile(SyntheticChunkOffset, trackProperties: true);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(SyntheticSampleCount);
        stream.Position = BoxHeaderLength;

        HeifSequenceTrack track = parser.Parse(stream, GetMoviePayloadLength(data)).ColorTrack;

        Assert.NotNull(track.CicpProfile);
        HeifPixelAspectRatio pixelAspectRatio = Assert.IsType<HeifPixelAspectRatio>(track.PixelAspectRatio);
        Assert.Equal(SyntheticHorizontalPixelSpacing, pixelAspectRatio.HorizontalSpacing);
        Assert.Equal(SyntheticVerticalPixelSpacing, pixelAspectRatio.VerticalSpacing);
        Size codedSize = new(SyntheticWidth, SyntheticHeight);

        Assert.True(track.CleanAperture.HasValue);
        HeifCleanAperture cleanAperture = track.CleanAperture.GetValueOrDefault();
        Assert.Equal(new Rectangle(Point.Empty, codedSize), cleanAperture.ToRectangle(codedSize));
        Assert.Equal((byte)1, track.RotationAngle);
        Assert.Equal((byte)1, track.MirrorAxis);
        Assert.True(track.ContentLightLevel.HasValue);
        HeifContentLightLevel contentLightLevel = track.ContentLightLevel.GetValueOrDefault();
        Assert.Equal(SyntheticMaximumContentLightLevel, contentLightLevel.MaximumContentLightLevel);
        Assert.NotNull(track.MasteringDisplayColorVolume);
        Assert.NotNull(track.ContentColorVolume);
        Assert.NotNull(track.AmbientViewingEnvironment);
        Assert.NotNull(track.ReferenceViewingEnvironment);
        Assert.NotNull(track.NominalDiffuseWhite);
    }

    /// <summary>
    /// Verifies that track-level Exif and XMP items are bounded and retained whether their extents address the file
    /// or the metadata box's item-data payload.
    /// </summary>
    /// <param name="useItemData">Whether metadata extents use construction method one and address the item-data box.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParseRetainsBoundedTrackMetadata(bool useItemData)
    {
        byte[] data = CreateSequenceFile(SyntheticChunkOffset, trackMetadata: true, metadataInItemData: useItemData);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(SyntheticSampleCount);
        stream.Position = BoxHeaderLength;

        HeifSequenceTrack track = parser.Parse(stream, GetMoviePayloadLength(data)).ColorTrack;

        Assert.NotNull(track.Metadata);
        Assert.Equal(TrackExifData.ToArray(), track.Metadata.ExifData);
        Assert.Equal(TrackXmpData.ToArray(), track.Metadata.XmpData);
    }

    /// <summary>
    /// Verifies that metadata skipping avoids both validation and retention of malformed optional track metadata
    /// while leaving image samples available.
    /// </summary>
    [Fact]
    public void ParseDoesNotValidateOrRetainSkippedTrackMetadata()
    {
        byte[] data = CreateSequenceFile(SyntheticChunkOffset, trackMetadata: true, invalidTrackMetadata: true);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(SyntheticSampleCount, skipMetadata: true);
        stream.Position = BoxHeaderLength;

        HeifSequenceTrack track = parser.Parse(stream, GetMoviePayloadLength(data)).ColorTrack;

        Assert.Null(track.Metadata);
        Assert.Equal(SyntheticSampleCount, (uint)track.Samples.Length);
    }

    /// <summary>
    /// Verifies that malformed track metadata is fatal under strict validation but is omitted under ancillary-error
    /// tolerance without affecting the retained image samples.
    /// </summary>
    [Fact]
    public void ParseUsesAncillaryIntegrityPolicyForTrackMetadata()
    {
        byte[] data = CreateSequenceFile(SyntheticChunkOffset, trackMetadata: true, invalidTrackMetadata: true);
        using MemoryStream strictStream = new(data, false);
        HeifSequenceParser strictParser = CreateParser(SyntheticSampleCount);
        strictStream.Position = BoxHeaderLength;

        Assert.Throws<InvalidImageContentException>(() => strictParser.Parse(strictStream, GetMoviePayloadLength(data)));

        using MemoryStream tolerantStream = new(data, false);
        HeifSequenceParser tolerantParser = CreateParser(
            SyntheticSampleCount,
            segmentIntegrityHandling: SegmentIntegrityHandling.IgnoreAncillary);

        tolerantStream.Position = BoxHeaderLength;

        HeifSequenceTrack track = tolerantParser.Parse(tolerantStream, GetMoviePayloadLength(data)).ColorTrack;

        Assert.Null(track.Metadata);
        Assert.Equal(SyntheticSampleCount, (uint)track.Samples.Length);
    }

    /// <summary>
    /// Verifies that malformed presentation properties remain image-data errors under strict and ancillary-tolerant
    /// policies because they affect the rendered image geometry.
    /// </summary>
    /// <param name="handling">The segment-integrity policy applied at the parser boundary.</param>
    [Theory]
    [InlineData(SegmentIntegrityHandling.Strict)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary)]
    public void ParseRejectsInvalidPresentationPropertyUnlessImageDataErrorsAreIgnored(SegmentIntegrityHandling handling)
    {
        byte[] data = CreateSequenceFile(SyntheticChunkOffset, trackProperties: true, invalidRotation: true);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(SyntheticSampleCount, segmentIntegrityHandling: handling);
        stream.Position = BoxHeaderLength;

        Assert.Throws<InvalidImageContentException>(() => parser.Parse(stream, GetMoviePayloadLength(data)));
    }

    /// <summary>
    /// Verifies that image-data tolerance omits only the malformed presentation property while retaining independent
    /// valid properties and all image samples.
    /// </summary>
    [Fact]
    public void ParseOmitsInvalidPresentationPropertyWhenImageDataErrorsAreIgnored()
    {
        byte[] data = CreateSequenceFile(SyntheticChunkOffset, trackProperties: true, invalidRotation: true);
        using MemoryStream stream = new(data, false);
        HeifSequenceParser parser = CreateParser(
            SyntheticSampleCount,
            segmentIntegrityHandling: SegmentIntegrityHandling.IgnoreImageData);

        stream.Position = BoxHeaderLength;

        HeifSequenceTrack track = parser.Parse(stream, GetMoviePayloadLength(data)).ColorTrack;

        Assert.Null(track.RotationAngle);
        Assert.NotNull(track.PixelAspectRatio);
        Assert.Equal(SyntheticSampleCount, (uint)track.Samples.Length);
    }

    /// <summary>
    /// Builds a bounded movie box containing one configurable image-sequence track and leaves sample payload space
    /// outside the movie so tests can independently control sample offsets and source-length validation.
    /// </summary>
    /// <param name="chunkOffset">The absolute file offset of the track's single sample chunk.</param>
    /// <param name="compositionOffsets">Whether to write prohibited composition timing boxes.</param>
    /// <param name="directReferences">Whether to write AV1 direct-reference sample groups.</param>
    /// <param name="directReferenceSampleId">The sample identifier named by the dependent sample.</param>
    /// <param name="trackProperties">Whether to write image presentation properties in the sample entry.</param>
    /// <param name="trackMetadata">Whether to write track-level Exif and XMP metadata items.</param>
    /// <param name="metadataInItemData">Whether metadata extents address an item-data box instead of file offsets.</param>
    /// <param name="invalidTrackMetadata">Whether the metadata handler is intentionally invalid.</param>
    /// <param name="invalidRotation">Whether the rotation property contains reserved high bits.</param>
    /// <param name="width">The displayed and coded sample width in pixels.</param>
    /// <param name="height">The displayed and coded sample height in pixels.</param>
    /// <param name="av1Configuration">The AV1CodecConfigurationBox payload, or the valid default payload.</param>
    /// <param name="sampleSize">The first sample size, or the synthetic default size.</param>
    /// <param name="secondSampleSize">The second sample size, or the first/default sample size.</param>
    /// <param name="allSamplesSync">Whether both samples are declared as sync samples.</param>
    /// <param name="premultipliedByTrackId">The alpha track identifier named by the premultiplication reference.</param>
    /// <param name="nonIdentityMovieMatrix">Whether the movie header matrix contains horizontal scaling.</param>
    /// <param name="nonIdentityTrackMatrix">Whether the track header matrix contains horizontal scaling.</param>
    /// <param name="trackEnabled">Whether the picture track is eligible for sequence presentation.</param>
    /// <returns>The fixed-length synthetic file containing the serialized movie box.</returns>
    private static byte[] CreateSequenceFile(
        uint chunkOffset,
        bool compositionOffsets = false,
        bool directReferences = false,
        uint directReferenceSampleId = ColorTrackId,
        bool trackProperties = false,
        bool trackMetadata = false,
        bool metadataInItemData = false,
        bool invalidTrackMetadata = false,
        bool invalidRotation = false,
        int width = SyntheticWidth,
        int height = SyntheticHeight,
        byte[] av1Configuration = null,
        int? sampleSize = null,
        int? secondSampleSize = null,
        bool allSamplesSync = false,
        uint premultipliedByTrackId = 0,
        bool nonIdentityMovieMatrix = false,
        bool nonIdentityTrackMatrix = false,
        bool trackEnabled = true)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, true);
        long movie = BeginBox(writer, Heif4CharCode.Moov);

        long movieHeader = BeginBox(writer, Heif4CharCode.Mvhd);

        // ISO/IEC 14496-12 Section 8.2.2 orders the version-zero fields as creation time, modification time,
        // timescale, duration, preferred 16.16 rate, preferred 8.8 volume, reserved words, matrix, predefined words,
        // and the next available track identifier.
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, SyntheticMovieTimescale);
        WriteUInt32(writer, SyntheticTrackDuration);
        WriteUInt32(writer, UnityFixed16Point16);
        WriteUInt16(writer, UnityFixed8Point8);
        WriteUInt16(writer, 0);
        WriteZeros(writer, 2 * sizeof(uint));
        WritePresentationMatrix(writer, nonIdentityMovieMatrix);
        WriteZeros(writer, 6 * sizeof(uint));
        WriteUInt32(writer, UnrelatedTrackId);
        EndBox(writer, movieHeader);

        long track = BeginBox(writer, Heif4CharCode.Trak);
        WriteTrackHeader(writer, width, height, ColorTrackId, nonIdentityTrackMatrix, trackEnabled);
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
            compositionOffsets,
            directReferences,
            directReferenceSampleId,
            trackProperties,
            invalidRotation,
            width,
            height,
            av1Configuration,
            sampleSize,
            secondSampleSize,
            allSamplesSync);

        EndBox(writer, mediaInformation);
        EndBox(writer, media);
        EndBox(writer, track);
        EndBox(writer, movie);

        byte[] movieBytes = stream.ToArray();

        // The fixed outer length leaves deterministic space for file-addressed metadata and sample extents while
        // allowing individual tests to place an extent deliberately beyond the source boundary.
        byte[] file = new byte[SyntheticFileLength];

        movieBytes.CopyTo(file, 0);
        if (trackMetadata && !metadataInItemData)
        {
            TrackExifData.CopyTo(file.AsSpan(TrackExifOffset));
            TrackXmpData.CopyTo(file.AsSpan(TrackXmpOffset));
        }

        return file;
    }

    /// <summary>
    /// Appends an auxiliary alpha track to a synthetic color-track movie and links it by track identifier.
    /// </summary>
    /// <param name="chunkOffset">The absolute file offset of the color sample chunk.</param>
    /// <param name="alphaTimescale">The alpha track's media time scale in units per second.</param>
    /// <param name="premultipliedByTrackId">The track identifier named by the color premultiplication reference.</param>
    /// <param name="colorTransforms">Whether the color sample entry carries image presentation properties.</param>
    /// <param name="alphaTransforms">Whether the alpha sample entry carries image presentation properties.</param>
    /// <param name="alphaChunkOffset">The absolute alpha sample-chunk offset, or the color chunk offset when omitted.</param>
    /// <param name="width">The displayed and coded width of both tracks in pixels.</param>
    /// <param name="height">The displayed and coded height of both tracks in pixels.</param>
    /// <param name="av1Configuration">The AV1CodecConfigurationBox payload shared by the tracks.</param>
    /// <param name="sampleSize">The size of each sample in both tracks.</param>
    /// <param name="allSamplesSync">Whether all color and alpha samples are sync samples.</param>
    /// <returns>The fixed-length synthetic file containing the color and alpha tracks.</returns>
    private static byte[] CreateSequenceFileWithAlpha(
        uint chunkOffset,
        uint alphaTimescale,
        uint premultipliedByTrackId,
        bool colorTransforms = false,
        bool alphaTransforms = false,
        uint? alphaChunkOffset = null,
        int width = SyntheticWidth,
        int height = SyntheticHeight,
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
        WriteTrackHeader(writer, width, height, AlphaTrackId, false, true);
        WriteTrackReference(writer, Heif4CharCode.Auxl, ColorTrackId);

        long media = BeginBox(writer, Heif4CharCode.Mdia);
        WriteMediaHeader(writer, alphaTimescale);
        WriteHandler(writer, Heif4CharCode.Auxv);

        long mediaInformation = BeginBox(writer, Heif4CharCode.Minf);
        WriteDataInformation(writer);
        WriteSampleTable(
            writer,
            alphaChunkOffset ?? chunkOffset,
            false,
            false,
            ColorTrackId,
            alphaTransforms,
            false,
            width,
            height,
            av1Configuration,
            sampleSize,
            null,
            allSamplesSync,
            true);

        EndBox(writer, mediaInformation);
        EndBox(writer, media);
        EndBox(writer, track);

        byte[] alphaTrack = stream.ToArray();
        byte[] file = new byte[SyntheticFileLength];

        // The alpha TrackBox is appended inside the existing MovieBox, so patch the movie size after concatenation.
        colorFile.AsSpan(0, movieLength).CopyTo(file);
        alphaTrack.CopyTo(file, movieLength);
        BinaryPrimitives.WriteUInt32BigEndian(file, (uint)(movieLength + alphaTrack.Length));
        return file;
    }

    /// <summary>
    /// Prefixes a synthetic movie with the AVIF image-sequence FileTypeBox used by the public decoder entry points.
    /// </summary>
    /// <param name="trackProperties">Whether the sequence carries image presentation properties.</param>
    /// <param name="trackMetadata">Whether the sequence carries track-level Exif and XMP items.</param>
    /// <returns>The complete synthetic AVIF byte stream.</returns>
    private static byte[] CreateSequenceContainer(bool trackProperties, bool trackMetadata)
    {
        byte[] movie = CreateSequenceFile(
            SyntheticChunkOffset,
            trackProperties: trackProperties,
            trackMetadata: trackMetadata,
            metadataInItemData: true);

        byte[] data = new byte[movie.Length + FileTypeBoxLength];
        WriteSequenceFileTypeBox(data);
        movie.CopyTo(data, FileTypeBoxLength);
        return data;
    }

    /// <summary>
    /// Builds a two-frame AVIF sequence that stores the same independently decodable AV1 sample in both frames.
    /// </summary>
    /// <param name="sample">The complete AV1 sample payload.</param>
    /// <param name="configuration">The AV1CodecConfigurationBox payload describing the sample.</param>
    /// <returns>The complete synthetic AVIF byte stream.</returns>
    private static byte[] CreateDecodableAv1SequenceContainer(
        ReadOnlySpan<byte> sample,
        ReadOnlySpan<byte> configuration,
        bool trackProperties = false)
        => CreateDecodableAv1SequenceContainer(sample, sample, configuration, true, trackProperties);

    /// <summary>
    /// Builds a two-frame AVIF sequence with caller-provided AV1 samples so integrity tests can corrupt one sample
    /// without also corrupting the parser-owned container structures.
    /// </summary>
    /// <param name="firstSample">The first AV1 sample payload.</param>
    /// <param name="secondSample">The second AV1 sample payload.</param>
    /// <param name="configuration">The AV1CodecConfigurationBox payload describing both samples.</param>
    /// <param name="allSamplesSync">Whether both samples are marked independently decodable.</param>
    /// <returns>The complete synthetic AVIF byte stream.</returns>
    private static byte[] CreateDecodableAv1SequenceContainer(
        ReadOnlySpan<byte> firstSample,
        ReadOnlySpan<byte> secondSample,
        ReadOnlySpan<byte> configuration,
        bool allSamplesSync,
        bool trackProperties = false)
    {
        uint chunkOffset = FileTypeBoxLength + SyntheticFileLength;
        byte[] movie = CreateSequenceFile(
            chunkOffset,
            trackProperties: trackProperties,
            width: 4,
            height: 4,
            av1Configuration: configuration.ToArray(),
            sampleSize: firstSample.Length,
            secondSampleSize: secondSample.Length,
            allSamplesSync: allSamplesSync);

        byte[] data = new byte[chunkOffset + firstSample.Length + secondSample.Length];
        WriteSequenceFileTypeBox(data);
        movie.CopyTo(data, FileTypeBoxLength);
        firstSample.CopyTo(data.AsSpan((int)chunkOffset));
        secondSample.CopyTo(data.AsSpan((int)chunkOffset + firstSample.Length));
        return data;
    }

    /// <summary>
    /// Builds two frame-aligned AV1 tracks that intentionally reuse a color sample for the declared alpha track.
    /// </summary>
    /// <param name="sample">The AV1 sample payload stored in every color and alpha frame.</param>
    /// <param name="configuration">The AV1CodecConfigurationBox payload describing the sample.</param>
    /// <returns>The complete synthetic AVIF byte stream.</returns>
    private static byte[] CreateAv1SequenceWithNonMonochromeAlphaContainer(ReadOnlySpan<byte> sample, ReadOnlySpan<byte> configuration)
    {
        uint colorChunkOffset = FileTypeBoxLength + SyntheticFileLength;
        uint alphaChunkOffset = colorChunkOffset + (uint)(sample.Length * 2);
        byte[] movie = CreateSequenceFileWithAlpha(
            colorChunkOffset,
            SyntheticMovieTimescale,
            0,
            alphaChunkOffset: alphaChunkOffset,
            width: 4,
            height: 4,
            av1Configuration: configuration.ToArray(),
            sampleSize: sample.Length,
            allSamplesSync: true);

        byte[] data = new byte[alphaChunkOffset + (sample.Length * 2)];
        WriteSequenceFileTypeBox(data);
        movie.CopyTo(data, FileTypeBoxLength);
        sample.CopyTo(data.AsSpan((int)colorChunkOffset));
        sample.CopyTo(data.AsSpan((int)colorChunkOffset + sample.Length));
        sample.CopyTo(data.AsSpan((int)alphaChunkOffset));
        sample.CopyTo(data.AsSpan((int)alphaChunkOffset + sample.Length));
        return data;
    }

    /// <summary>
    /// Writes the AVIF image-sequence FileTypeBox shared by all complete synthetic decoder inputs.
    /// </summary>
    /// <param name="destination">The destination whose first 24 bytes receive the box.</param>
    private static void WriteSequenceFileTypeBox(Span<byte> destination)
    {
        int fieldOffset = 0;

        // ISO/IEC 14496-12 Section 4.3 stores the box size and type first, followed by the major brand, minor
        // version, and compatible brands. 'avis' selects the sequence presentation while 'avif' and 'mif1' declare
        // compatibility with the AVIF and HEIF image-item structures also present in these files.
        BinaryPrimitives.WriteUInt32BigEndian(destination[fieldOffset..], FileTypeBoxLength);
        fieldOffset += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(destination[fieldOffset..], (uint)Heif4CharCode.Ftyp);
        fieldOffset += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(destination[fieldOffset..], (uint)Heif4CharCode.Avis);
        fieldOffset += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(destination[fieldOffset..], 0);
        fieldOffset += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(destination[fieldOffset..], (uint)Heif4CharCode.Avif);
        fieldOffset += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(destination[fieldOffset..], (uint)Heif4CharCode.Mif1);
    }

    /// <summary>
    /// Writes a version-zero TrackHeaderBox with an enabled movie track, integral 16.16 dimensions, and a selectable
    /// presentation matrix.
    /// </summary>
    /// <param name="writer">The writer receiving big-endian box fields.</param>
    /// <param name="width">The displayed track width in pixels.</param>
    /// <param name="height">The displayed track height in pixels.</param>
    /// <param name="trackId">The nonzero file-defined track identifier.</param>
    /// <param name="nonIdentityMatrix">Whether to encode horizontal scaling instead of the unity matrix.</param>
    /// <param name="isEnabled">Whether to set the TrackHeaderBox enabled flag.</param>
    private static void WriteTrackHeader(
        BinaryWriter writer,
        int width,
        int height,
        uint trackId,
        bool nonIdentityMatrix,
        bool isEnabled)
    {
        const uint trackEnabledFlag = 1U << 0;
        const int fixedPointFractionalBits = 16;

        long trackHeader = BeginBox(writer, Heif4CharCode.Tkhd);

        // ISO/IEC 14496-12 Section 8.3.2 assigns bit zero to track_enabled. libavif sequence tracks set that bit without
        // requiring track_in_movie. The following fields hold times, identifier, duration, matrix, and 16.16 dimensions.
        WriteFullBoxHeader(writer, 0, isEnabled ? trackEnabledFlag : 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, trackId);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, SyntheticTrackDuration);
        WriteZeros(writer, (2 * sizeof(uint)) + (4 * sizeof(ushort)));
        WritePresentationMatrix(writer, nonIdentityMatrix);
        WriteUInt32(writer, (uint)width << fixedPointFractionalBits);
        WriteUInt32(writer, (uint)height << fixedPointFractionalBits);
        EndBox(writer, trackHeader);
    }

    /// <summary>
    /// Writes the nine fixed-point coefficients of an ISO base media presentation matrix.
    /// </summary>
    /// <param name="writer">The writer receiving big-endian matrix coefficients.</param>
    /// <param name="nonIdentityMatrix">Whether the horizontal 16.16 scale is two instead of one.</param>
    private static void WritePresentationMatrix(BinaryWriter writer, bool nonIdentityMatrix)
    {
        // The first six coefficients use 16.16 fixed point and the final perspective column uses 2.30. Altering only
        // the horizontal scale gives matrix-validation tests one controlled departure from the unity matrix.
        WriteUInt32(writer, nonIdentityMatrix ? DoubleFixed16Point16 : UnityFixed16Point16);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, UnityFixed16Point16);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, UnityFixed2Point30);
    }

    /// <summary>
    /// Writes one TrackReferenceBox child that links the owning track to a single referenced track identifier.
    /// </summary>
    /// <param name="writer">The writer receiving the reference boxes.</param>
    /// <param name="referenceType">The image-sequence reference relationship.</param>
    /// <param name="trackId">The referenced track identifier.</param>
    private static void WriteTrackReference(BinaryWriter writer, Heif4CharCode referenceType, uint trackId)
    {
        long references = BeginBox(writer, Heif4CharCode.Tref);
        long reference = BeginBox(writer, referenceType);
        WriteUInt32(writer, trackId);
        EndBox(writer, reference);
        EndBox(writer, references);
    }

    /// <summary>
    /// Writes a repeating single-entry EditListBox whose 200 movie-time-scale units are repeated to fill the
    /// 600-unit track duration, producing three total plays.
    /// </summary>
    /// <param name="writer">The writer receiving the edit boxes.</param>
    private static void WriteEditList(BinaryWriter writer)
    {
        const uint repeatEditListFlag = 1U << 0;
        const uint editEntryCount = 1;
        const uint mediaStartTime = 0;
        const ushort unityMediaRateInteger = 1;
        const ushort unityMediaRateFraction = 0;

        long edit = BeginBox(writer, Heif4CharCode.Edts);
        long editList = BeginBox(writer, Heif4CharCode.Elst);
        WriteFullBoxHeader(writer, 0, repeatEditListFlag);
        WriteUInt32(writer, editEntryCount);
        WriteUInt32(writer, SyntheticMediaDuration);
        WriteUInt32(writer, mediaStartTime);
        WriteUInt16(writer, unityMediaRateInteger);
        WriteUInt16(writer, unityMediaRateFraction);
        EndBox(writer, editList);
        EndBox(writer, edit);
    }

    /// <summary>
    /// Writes a version-zero MediaHeaderBox for two 100-unit samples and the packed ISO-639 language code "und".
    /// </summary>
    /// <param name="writer">The writer receiving the media header.</param>
    /// <param name="timescale">The media time scale in units per second.</param>
    private static void WriteMediaHeader(BinaryWriter writer, uint timescale = SyntheticMovieTimescale)
    {
        long mediaHeader = BeginBox(writer, Heif4CharCode.Mdhd);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, 0);
        WriteUInt32(writer, timescale);
        WriteUInt32(writer, SyntheticMediaDuration);
        WriteUInt16(writer, PackedUndeterminedLanguage);
        WriteUInt16(writer, 0);
        EndBox(writer, mediaHeader);
    }

    /// <summary>
    /// Writes a HandlerBox with the requested track role and an empty null-terminated handler name.
    /// </summary>
    /// <param name="writer">The writer receiving the handler box.</param>
    /// <param name="handlerType">The four-character handler role.</param>
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

    /// <summary>
    /// Writes a self-contained DataInformationBox whose single DataEntryUrlBox resolves sample offsets in this file.
    /// </summary>
    /// <param name="writer">The writer receiving the data-reference hierarchy.</param>
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

    /// <summary>
    /// Writes a track-level MetaBox containing one Exif item and one XMP MIME item, with extents addressing either
    /// the enclosing file or an ItemDataBox.
    /// </summary>
    /// <param name="writer">The writer receiving the metadata hierarchy.</param>
    /// <param name="useItemData">Whether item extents use construction method one.</param>
    /// <param name="invalidHandler">Whether to write an invalid video handler instead of the picture handler.</param>
    private static void WriteTrackMetadata(BinaryWriter writer, bool useItemData, bool invalidHandler)
    {
        const byte fourByteOffsetAndLengthSizes = 0x44;
        const ushort metadataItemCount = 2;

        long metadata = BeginBox(writer, Heif4CharCode.Meta);
        WriteFullBoxHeader(writer, 0, 0);
        WriteHandler(writer, invalidHandler ? Heif4CharCode.Vide : Heif4CharCode.Pict);

        long itemLocations = BeginBox(writer, Heif4CharCode.Iloc);
        WriteFullBoxHeader(writer, useItemData ? (byte)1 : (byte)0, 0);

        // ISO/IEC 14496-12 Section 8.11.3 packs offset_size and length_size into the high and low nibbles. Four-byte
        // fields cover the synthetic file while keeping the encoded records identical to normal HEIF metadata.
        writer.Write(fourByteOffsetAndLengthSizes);
        writer.Write((byte)0);
        WriteUInt16(writer, metadataItemCount);
        WriteTrackMetadataLocation(
            writer,
            TrackExifItemId,
            useItemData,
            useItemData ? 0U : TrackExifOffset,
            (uint)TrackExifData.Length);

        WriteTrackMetadataLocation(
            writer,
            TrackXmpItemId,
            useItemData,
            useItemData ? (uint)TrackExifData.Length : TrackXmpOffset,
            (uint)TrackXmpData.Length);

        EndBox(writer, itemLocations);

        long itemInformation = BeginBox(writer, Heif4CharCode.Iinf);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt16(writer, metadataItemCount);
        WriteTrackMetadataItem(writer, TrackExifItemId, Heif4CharCode.Exif);
        WriteTrackMetadataItem(writer, TrackXmpItemId, Heif4CharCode.Mime);
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

    /// <summary>
    /// Writes one ItemLocationBox record with a single extent encoded using four-byte offsets and lengths.
    /// </summary>
    /// <param name="writer">The writer receiving the item-location record.</param>
    /// <param name="itemId">The item identifier linked to an ItemInfoEntry.</param>
    /// <param name="useItemData">Whether the extent addresses ItemDataBox bytes.</param>
    /// <param name="offset">The extent offset relative to the selected construction method.</param>
    /// <param name="length">The extent length in bytes.</param>
    private static void WriteTrackMetadataLocation(BinaryWriter writer, ushort itemId, bool useItemData, uint offset, uint length)
    {
        const ushort itemDataConstructionMethod = 1;
        const ushort localDataReferenceIndex = 0;
        const ushort extentCount = 1;

        WriteUInt16(writer, itemId);
        if (useItemData)
        {
            WriteUInt16(writer, itemDataConstructionMethod);
        }

        WriteUInt16(writer, localDataReferenceIndex);
        WriteUInt16(writer, extentCount);
        WriteUInt32(writer, offset);
        WriteUInt32(writer, length);
    }

    /// <summary>
    /// Writes a version-two ItemInfoEntry for an Exif item or an XMP item using the registered RDF MIME type.
    /// </summary>
    /// <param name="writer">The writer receiving the item information entry.</param>
    /// <param name="itemId">The identifier matched by the location record.</param>
    /// <param name="itemType">The item's four-character type.</param>
    private static void WriteTrackMetadataItem(BinaryWriter writer, ushort itemId, Heif4CharCode itemType)
    {
        const byte itemInfoVersion = 2;
        const ushort noItemProtection = 0;

        long itemInformationEntry = BeginBox(writer, Heif4CharCode.Infe);
        WriteFullBoxHeader(writer, itemInfoVersion, 0);
        WriteUInt16(writer, itemId);
        WriteUInt16(writer, noItemProtection);
        WriteUInt32(writer, (uint)itemType);
        writer.Write((byte)0);
        if (itemType == Heif4CharCode.Mime)
        {
            writer.Write("application/rdf+xml"u8);
            writer.Write((byte)0);
        }

        EndBox(writer, itemInformationEntry);
    }

    /// <summary>
    /// Writes the timing, sample-to-chunk, size, chunk-offset, sync-sample, optional composition, and optional direct
    /// reference boxes for exactly two image-sequence samples.
    /// </summary>
    /// <param name="writer">The writer receiving the SampleTableBox.</param>
    /// <param name="chunkOffset">The absolute file offset of the single sample chunk.</param>
    /// <param name="compositionOffsets">Whether to write prohibited composition timing boxes.</param>
    /// <param name="directReferences">Whether to write AV1 direct-reference grouping.</param>
    /// <param name="directReferenceSampleId">The sample identifier referenced by the second sample.</param>
    /// <param name="trackProperties">Whether the visual sample entry contains presentation properties.</param>
    /// <param name="invalidRotation">Whether the rotation property contains reserved high bits.</param>
    /// <param name="width">The coded sample width in pixels.</param>
    /// <param name="height">The coded sample height in pixels.</param>
    /// <param name="av1Configuration">The AV1CodecConfigurationBox payload.</param>
    /// <param name="sampleSize">The first sample length, or the synthetic default.</param>
    /// <param name="secondSampleSize">The second sample length, or the first/default length.</param>
    /// <param name="allSamplesSync">Whether both samples are listed as sync samples.</param>
    /// <param name="alpha">Whether the sample entry describes an auxiliary alpha track.</param>
    private static void WriteSampleTable(
        BinaryWriter writer,
        uint chunkOffset,
        bool compositionOffsets,
        bool directReferences,
        uint directReferenceSampleId,
        bool trackProperties,
        bool invalidRotation,
        int width,
        int height,
        byte[] av1Configuration,
        int? sampleSize,
        int? secondSampleSize,
        bool allSamplesSync,
        bool alpha = false)
    {
        const uint singleEntry = 1;
        const uint firstChunk = 1;
        const uint sampleDescriptionIndex = 1;
        const uint variableSampleSizes = 0;

        long sampleTable = BeginBox(writer, Heif4CharCode.Stbl);
        WriteSampleDescription(writer, trackProperties, invalidRotation, width, height, av1Configuration, allSamplesSync, alpha);

        long timing = BeginBox(writer, Heif4CharCode.Stts);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, singleEntry);
        WriteUInt32(writer, SyntheticSampleCount);
        WriteUInt32(writer, SyntheticSampleDuration);
        EndBox(writer, timing);

        long sampleToChunk = BeginBox(writer, Heif4CharCode.Stsc);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, singleEntry);
        WriteUInt32(writer, firstChunk);
        WriteUInt32(writer, SyntheticSampleCount);
        WriteUInt32(writer, sampleDescriptionIndex);
        EndBox(writer, sampleToChunk);

        long sampleSizes = BeginBox(writer, Heif4CharCode.Stsz);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, variableSampleSizes);
        WriteUInt32(writer, SyntheticSampleCount);
        WriteUInt32(writer, (uint)(sampleSize ?? FirstSyntheticSampleLength));
        WriteUInt32(writer, (uint)(secondSampleSize ?? sampleSize ?? SecondSyntheticSampleLength));
        EndBox(writer, sampleSizes);

        long chunkOffsets = BeginBox(writer, Heif4CharCode.Stco);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, singleEntry);
        WriteUInt32(writer, chunkOffset);
        EndBox(writer, chunkOffsets);

        long syncSamples = BeginBox(writer, Heif4CharCode.Stss);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, allSamplesSync ? SyntheticSampleCount : singleEntry);
        WriteUInt32(writer, firstChunk);
        if (allSamplesSync)
        {
            WriteUInt32(writer, SyntheticSampleCount);
        }

        EndBox(writer, syncSamples);

        if (compositionOffsets)
        {
            const byte signedCompositionOffsetVersion = 1;
            const uint compositionEntryCount = SyntheticSampleCount;
            const uint oneSample = 1;
            uint hiddenSampleOffset = unchecked((uint)int.MinValue);

            long offsets = BeginBox(writer, Heif4CharCode.Ctts);
            WriteFullBoxHeader(writer, signedCompositionOffsetVersion, 0);
            WriteUInt32(writer, compositionEntryCount);
            WriteUInt32(writer, oneSample);
            WriteUInt32(writer, hiddenSampleOffset);
            WriteUInt32(writer, oneSample);
            WriteUInt32(writer, 0);
            EndBox(writer, offsets);

            // The CompositionToDecodeBox declares that visible presentation begins after the hidden sample's
            // 100-unit slot and ends at the two-sample media duration.
            long compositionToDecode = BeginBox(writer, Heif4CharCode.Cslg);
            WriteFullBoxHeader(writer, 0, 0);
            WriteUInt32(writer, 0);
            WriteUInt32(writer, 0);
            WriteUInt32(writer, 0);
            WriteUInt32(writer, SyntheticSampleDuration);
            WriteUInt32(writer, SyntheticMediaDuration);
            EndBox(writer, compositionToDecode);
        }

        if (directReferences)
        {
            WriteDirectReferenceSampleGroup(writer, directReferenceSampleId);
        }

        EndBox(writer, sampleTable);
    }

    /// <summary>
    /// Writes AV1 "refs" sample-group descriptions and maps the first sample to an independent description and the
    /// second sample to a description containing one direct reference.
    /// </summary>
    /// <param name="writer">The writer receiving the sample-group boxes.</param>
    /// <param name="directReferenceSampleId">The sample identifier named by the dependent description.</param>
    private static void WriteDirectReferenceSampleGroup(BinaryWriter writer, uint directReferenceSampleId)
    {
        const byte variableLengthDescriptionVersion = 1;
        const uint variableDescriptionLength = 0;
        const uint descriptionCount = SyntheticSampleCount;
        const uint independentDescriptionLength = sizeof(uint) + sizeof(byte);
        const uint dependentDescriptionLength = independentDescriptionLength + sizeof(uint);
        const uint independentSampleId = ColorTrackId;
        const uint dependentSampleId = 0;
        const byte noDirectReferences = 0;
        const byte oneDirectReference = 1;

        long descriptions = BeginBox(writer, Heif4CharCode.Sgpd);
        WriteFullBoxHeader(writer, variableLengthDescriptionVersion, 0);
        WriteUInt32(writer, (uint)Heif4CharCode.Refs);
        WriteUInt32(writer, variableDescriptionLength);
        WriteUInt32(writer, descriptionCount);
        WriteUInt32(writer, independentDescriptionLength);
        WriteUInt32(writer, independentSampleId);
        writer.Write(noDirectReferences);
        WriteUInt32(writer, dependentDescriptionLength);
        WriteUInt32(writer, dependentSampleId);
        writer.Write(oneDirectReference);
        WriteUInt32(writer, directReferenceSampleId);
        EndBox(writer, descriptions);

        const uint sampleGroupRunCount = SyntheticSampleCount;
        const uint oneSamplePerRun = 1;
        const uint independentDescriptionIndex = 1;
        const uint dependentDescriptionIndex = 2;

        long sampleMap = BeginBox(writer, Heif4CharCode.Sbgp);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, (uint)Heif4CharCode.Refs);
        WriteUInt32(writer, sampleGroupRunCount);
        WriteUInt32(writer, oneSamplePerRun);
        WriteUInt32(writer, independentDescriptionIndex);
        WriteUInt32(writer, oneSamplePerRun);
        WriteUInt32(writer, dependentDescriptionIndex);
        EndBox(writer, sampleMap);
    }

    /// <summary>
    /// Writes one VisualSampleEntry and its codec configuration, optional alpha role, optional presentation
    /// properties, and CodingConstraintsBox.
    /// </summary>
    /// <param name="writer">The writer receiving the SampleDescriptionBox.</param>
    /// <param name="trackProperties">Whether to append image presentation properties.</param>
    /// <param name="invalidRotation">Whether the rotation property contains reserved high bits.</param>
    /// <param name="width">The coded sample width in pixels.</param>
    /// <param name="height">The coded sample height in pixels.</param>
    /// <param name="av1Configuration">The AV1CodecConfigurationBox payload.</param>
    /// <param name="allSamplesSync">Whether coding constraints declare every reference picture intra coded.</param>
    /// <param name="alpha">Whether the entry carries the HEIF alpha auxiliary type.</param>
    private static void WriteSampleDescription(
        BinaryWriter writer,
        bool trackProperties,
        bool invalidRotation,
        int width,
        int height,
        byte[] av1Configuration,
        bool allSamplesSync,
        bool alpha)
    {
        const ushort localDataReferenceIndex = 1;
        const ushort visualSampleFrameCount = 1;
        const int compressorNameLength = 32;
        const ushort noColorTable = ushort.MaxValue;
        const uint allReferencePicturesIntraMask = 1U << 31;
        const uint intraPicturePredictionUsedMask = 1U << 30;
        const int maximumReferencesShift = 26;
        const uint maximumReferencesPerPicture = 15;

        long description = BeginBox(writer, Heif4CharCode.Stsd);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, 1);
        long sampleEntry = BeginBox(writer, Heif4CharCode.Av01);

        // ISO/IEC 14496-12 Section 12.1.3 defines six reserved bytes and a data-reference index before the visual
        // sample entry's predefined words, dimensions, 16.16 resolution, frame count, fixed compressor-name field,
        // pixel depth, and the -1 sentinel indicating that no color table is present.
        WriteZeros(writer, 6);
        WriteUInt16(writer, localDataReferenceIndex);
        WriteZeros(writer, (2 * sizeof(ushort)) + (3 * sizeof(uint)));
        WriteUInt16(writer, (ushort)width);
        WriteUInt16(writer, (ushort)height);
        WriteUInt32(writer, SyntheticHorizontalResolution);
        WriteUInt32(writer, SyntheticHorizontalResolution);
        WriteUInt32(writer, 0);
        WriteUInt16(writer, visualSampleFrameCount);
        WriteZeros(writer, compressorNameLength);
        WriteUInt16(writer, SyntheticPixelDepth);
        WriteUInt16(writer, noColorTable);

        long configuration = BeginBox(writer, Heif4CharCode.Av1C);
        ReadOnlySpan<byte> configurationPayload = av1Configuration is null ? DefaultAv1Configuration : av1Configuration;

        writer.Write(configurationPayload);
        EndBox(writer, configuration);

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

        long codingConstraintsBox = BeginBox(writer, Heif4CharCode.Ccst);
        WriteFullBoxHeader(writer, 0, 0);

        // HEIF CodingConstraintsBox places the two boolean constraints in bits 31 and 30 and the four-bit maximum
        // reference count in bits 29..26. The synthetic dependency path deliberately permits the maximum of fifteen.
        uint codingConstraints = intraPicturePredictionUsedMask | (maximumReferencesPerPicture << maximumReferencesShift);
        if (allSamplesSync)
        {
            codingConstraints |= allReferencePicturesIntraMask;
        }

        WriteUInt32(writer, codingConstraints);
        EndBox(writer, codingConstraintsBox);
        EndBox(writer, sampleEntry);
        EndBox(writer, description);
    }

    /// <summary>
    /// Writes representative color, aspect-ratio, clean-aperture, orientation, HDR, and viewing-environment
    /// properties inside a visual sample entry.
    /// </summary>
    /// <param name="writer">The writer receiving the property boxes.</param>
    /// <param name="invalidRotation">Whether the rotation byte contains reserved high bits.</param>
    /// <param name="width">The clean-aperture width in pixels.</param>
    /// <param name="height">The clean-aperture height in pixels.</param>
    private static void WriteTrackImageProperties(BinaryWriter writer, bool invalidRotation, int width, int height)
    {
        const byte fullRangeFlag = 1 << 7;
        const byte reservedRotationBits = 0xFC;
        const byte rotateCounterClockwise90Degrees = 1;
        const byte verticalMirrorAxis = 1;

        long color = BeginBox(writer, Heif4CharCode.Colr);
        WriteUInt32(writer, (uint)Heif4CharCode.Nclx);
        WriteUInt16(writer, (ushort)CicpColorPrimaries.ItuRBt709_6);
        WriteUInt16(writer, (ushort)CicpTransferCharacteristics.Iec61966_2_1);
        WriteUInt16(writer, (ushort)CicpMatrixCoefficients.ItuRBt601_7_525);
        writer.Write(fullRangeFlag);
        EndBox(writer, color);

        long pixelAspectRatio = BeginBox(writer, Heif4CharCode.Pasp);
        WriteUInt32(writer, SyntheticHorizontalPixelSpacing);
        WriteUInt32(writer, SyntheticVerticalPixelSpacing);
        EndBox(writer, pixelAspectRatio);

        const uint cleanApertureDenominator = 1;
        const uint centeredCleanApertureOffset = 0;

        long cleanAperture = BeginBox(writer, Heif4CharCode.Clap);
        WriteUInt32(writer, (uint)width);
        WriteUInt32(writer, cleanApertureDenominator);
        WriteUInt32(writer, (uint)height);
        WriteUInt32(writer, cleanApertureDenominator);
        WriteUInt32(writer, centeredCleanApertureOffset);
        WriteUInt32(writer, cleanApertureDenominator);
        WriteUInt32(writer, centeredCleanApertureOffset);
        WriteUInt32(writer, cleanApertureDenominator);
        EndBox(writer, cleanAperture);

        long rotation = BeginBox(writer, Heif4CharCode.Irot);
        writer.Write(invalidRotation ? reservedRotationBits : rotateCounterClockwise90Degrees);
        EndBox(writer, rotation);

        long mirror = BeginBox(writer, Heif4CharCode.Imir);
        writer.Write(verticalMirrorAxis);
        EndBox(writer, mirror);

        long contentLightLevel = BeginBox(writer, Heif4CharCode.Clli);
        WriteUInt16(writer, SyntheticMaximumContentLightLevel);
        WriteUInt16(writer, SyntheticMaximumFrameAverageLightLevel);
        EndBox(writer, contentLightLevel);

        // MasteringDisplayColorVolume stores chromaticity in 0.00002 increments and luminance in 0.0001 cd/m2.
        const ushort redPrimaryX = 15_000;
        const ushort redPrimaryY = 30_000;
        const ushort greenPrimaryX = 7_500;
        const ushort greenPrimaryY = 3_000;
        const ushort bluePrimaryX = 34_000;
        const ushort bluePrimaryY = 16_000;
        const ushort whitePointX = 15_635;
        const ushort whitePointY = 16_450;
        const uint maximumDisplayLuminance = 10_000_000;
        const uint minimumDisplayLuminance = 50;

        long masteringDisplay = BeginBox(writer, Heif4CharCode.Mdcv);
        WriteUInt16(writer, redPrimaryX);
        WriteUInt16(writer, redPrimaryY);
        WriteUInt16(writer, greenPrimaryX);
        WriteUInt16(writer, greenPrimaryY);
        WriteUInt16(writer, bluePrimaryX);
        WriteUInt16(writer, bluePrimaryY);
        WriteUInt16(writer, whitePointX);
        WriteUInt16(writer, whitePointY);
        WriteUInt32(writer, maximumDisplayLuminance);
        WriteUInt32(writer, minimumDisplayLuminance);
        EndBox(writer, masteringDisplay);

        const byte minimumLuminancePresentMask = 1 << 4;
        const byte maximumLuminancePresentMask = 1 << 3;
        const byte averageLuminancePresentMask = 1 << 2;
        const uint minimumLuminanceValue = 1_000_000;
        const uint maximumLuminanceValue = 10_000_000;
        const uint averageLuminanceValue = 5_000_000;

        long contentColorVolume = BeginBox(writer, Heif4CharCode.Cclv);

        // ISO/IEC 23000-22 declares the optional content-color-volume fields through bits 5..2. This payload carries
        // all three luminance values and deliberately omits the much larger primary-chromaticity field set.
        writer.Write((byte)(minimumLuminancePresentMask | maximumLuminancePresentMask | averageLuminancePresentMask));
        WriteUInt32(writer, minimumLuminanceValue);
        WriteUInt32(writer, maximumLuminanceValue);
        WriteUInt32(writer, averageLuminanceValue);
        EndBox(writer, contentColorVolume);

        const uint ambientIlluminance = 10_000;

        long ambientViewing = BeginBox(writer, Heif4CharCode.Amve);
        WriteUInt32(writer, ambientIlluminance);
        WriteUInt16(writer, whitePointX);
        WriteUInt16(writer, whitePointY);
        EndBox(writer, ambientViewing);

        const uint referenceViewingIlluminance = 10_000;
        const ushort referenceWhiteX = 3_127;
        const ushort referenceWhiteY = 3_290;
        const uint referenceBlackLuminance = 5_000;

        long referenceViewing = BeginBox(writer, Heif4CharCode.Reve);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, referenceViewingIlluminance);
        WriteUInt16(writer, referenceWhiteX);
        WriteUInt16(writer, referenceWhiteY);
        WriteUInt32(writer, referenceBlackLuminance);
        WriteUInt16(writer, referenceWhiteX);
        WriteUInt16(writer, referenceWhiteY);
        EndBox(writer, referenceViewing);

        const uint nominalDiffuseWhiteLuminance = 2_030_000;

        long nominalDiffuseWhite = BeginBox(writer, Heif4CharCode.Ndwt);
        WriteFullBoxHeader(writer, 0, 0);
        WriteUInt32(writer, nominalDiffuseWhiteLuminance);
        EndBox(writer, nominalDiffuseWhite);
    }

    /// <summary>
    /// Starts a small ISO base media box with a placeholder 32-bit size that <see cref="EndBox"/> patches later.
    /// </summary>
    /// <param name="writer">The writer receiving the box header.</param>
    /// <param name="type">The box's four-character type.</param>
    /// <returns>The stream position of the size field.</returns>
    private static long BeginBox(BinaryWriter writer, Heif4CharCode type)
    {
        long start = writer.BaseStream.Position;
        WriteUInt32(writer, 0);
        WriteUInt32(writer, (uint)type);
        return start;
    }

    /// <summary>
    /// Completes a box by patching its total byte length and restoring the writer to the end of the payload.
    /// </summary>
    /// <param name="writer">The writer containing the box.</param>
    /// <param name="start">The stream position returned by <see cref="BeginBox"/>.</param>
    private static void EndBox(BinaryWriter writer, long start)
    {
        long end = writer.BaseStream.Position;
        writer.BaseStream.Position = start;
        WriteUInt32(writer, checked((uint)(end - start)));
        writer.BaseStream.Position = end;
    }

    /// <summary>
    /// Writes the FullBox version byte and low 24 flag bits as one big-endian word.
    /// </summary>
    /// <param name="writer">The writer receiving the FullBox header.</param>
    /// <param name="version">The box syntax version.</param>
    /// <param name="flags">The box-specific low 24 flag bits.</param>
    private static void WriteFullBoxHeader(BinaryWriter writer, byte version, uint flags)
        => WriteUInt32(writer, ((uint)version << 24) | flags);

    /// <summary>
    /// Writes an unsigned 16-bit ISO base media field in network byte order.
    /// </summary>
    /// <param name="writer">The little-endian binary writer receiving the field.</param>
    /// <param name="value">The host-order value.</param>
    private static void WriteUInt16(BinaryWriter writer, ushort value)
        => writer.Write(BinaryPrimitives.ReverseEndianness(value));

    /// <summary>
    /// Writes an unsigned 32-bit ISO base media field in network byte order.
    /// </summary>
    /// <param name="writer">The little-endian binary writer receiving the field.</param>
    /// <param name="value">The host-order value.</param>
    private static void WriteUInt32(BinaryWriter writer, uint value)
        => writer.Write(BinaryPrimitives.ReverseEndianness(value));

    /// <summary>
    /// Writes reserved bytes whose governing box syntax requires all bits to be zero.
    /// </summary>
    /// <param name="writer">The writer receiving the reserved bytes.</param>
    /// <param name="count">The number of reserved bytes.</param>
    private static void WriteZeros(BinaryWriter writer, int count) => writer.Write(new byte[count]);

    /// <summary>
    /// Reads the synthetic movie box size and removes its standard eight-byte header to obtain the parser payload
    /// boundary expected by <see cref="HeifSequenceParser.Parse"/>.
    /// </summary>
    /// <param name="data">The synthetic file beginning with a MovieBox.</param>
    /// <returns>The validated movie payload length.</returns>
    private static int GetMoviePayloadLength(byte[] data)
        => checked((int)BinaryPrimitives.ReadUInt32BigEndian(data) - BoxHeaderLength);

    /// <summary>
    /// Creates a sequence parser with the decoder limits and integrity policy exercised by a test.
    /// </summary>
    /// <param name="maxFrames">The maximum number of sequence frames to retain.</param>
    /// <param name="skipMetadata">Whether optional metadata parsing is disabled.</param>
    /// <param name="segmentIntegrityHandling">The malformed-segment recovery policy.</param>
    /// <returns>The configured parser.</returns>
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
