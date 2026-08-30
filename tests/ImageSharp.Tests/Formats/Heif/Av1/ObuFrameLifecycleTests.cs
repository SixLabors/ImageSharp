// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies frame ownership and completion while parsing layered AV1 OBU payloads.
/// </summary>
[Trait("Format", "Avif")]
public class ObuFrameLifecycleTests
{
    private const int ProgressiveSequencePrefixLength = 19;
    private const int FirstProgressiveLayerLength = 55;
    private const int NoFailingReaderIndex = -1;
    private const int SecondFrameReaderIndex = 1;
    private const byte ForbiddenObuHeader = 0x80;
    private const byte InvalidTrailingByte = 1;

    /// <summary>
    /// The operating point in the fixture sequence header that selects both spatial layers.
    /// </summary>
    private const byte ProgressiveOperatingPointIndex = 0;

    /// <summary>
    /// The source and displayed width recorded by libavif for both progressive layers.
    /// </summary>
    private const int ProgressiveImageWidth = 33;

    /// <summary>
    /// The source and displayed height recorded by libavif for both progressive layers.
    /// </summary>
    private const int ProgressiveImageHeight = 11;

    // This is the complete 72-byte color-item payload assembled from both extents of libavif's
    // draw_points_idat_progressive.avif. It contains one sequence header followed by two coded
    // spatial layers, so the lifecycle test exercises real progressive item framing.
    private static ReadOnlySpan<byte> ProgressiveTwoFrameObuStream =>
    [

        // Temporal delimiter and progressive sequence header.
        0x12, 0x00,
        0x0A, 0x0F, 0x20, 0x13, 0x01, 0x00, 0x80, 0x81, 0x4E, 0x0A, 0x36, 0xBE, 0x48, 0x08, 0x20, 0x34, 0x80,

        // Base spatial layer: unextended combined-frame OBU with a 34-byte payload.
        0x32, 0x22, 0x14, 0x00, 0x27, 0xC0, 0x00, 0x00, 0x80, 0x00, 0x20, 0xF4, 0xAC, 0x60, 0x4B, 0x59,
        0xB6, 0x97, 0xB3, 0xD1, 0xF9, 0x22, 0xB7, 0x5B, 0xAC, 0xD5, 0xD1, 0x99, 0x8C, 0x5F, 0x30, 0x67,
        0xB4, 0x6C, 0x99, 0x80,

        // Enhancement spatial layer: extended combined-frame OBU with temporal_id 0 and spatial_id 1.
        0x36, 0x08, 0x0E, 0x33, 0x01, 0xC0, 0x20, 0x00, 0x00, 0x06, 0x80, 0x01, 0x00, 0xF3, 0xA2, 0xD4, 0x38
    ];

    /// <summary>
    /// Verifies that a bounded progressive payload is consumed completely and assigns fresh tile state to each coded frame.
    /// </summary>
    [Fact]
    public void ReadAllConsumesBothProgressiveFrames()
    {
        const int temporalId = 0;
        const int baseSpatialId = 0;
        const int enhancementSpatialId = 1;
        byte[] bitStream = [.. ProgressiveTwoFrameObuStream];
        Av1BitStreamReader reader = new(bitStream);
        using Av1ReferenceFrameStore referenceFrames = new();
        ObuReader obuReader = new(ProgressiveOperatingPointIndex, referenceFrames);
        LifecycleTileReaderFactory factory = new(obuReader, referenceFrames, NoFailingReaderIndex);

        obuReader.ReadAll(ref reader, bitStream.Length, factory.Create);

        ObuFrameHeader secondFrameHeader = Assert.IsType<ObuFrameHeader>(factory.Readers[1].CompletedFrameHeader);

        Assert.Equal(bitStream.Length * 8, reader.BitPosition);
        Assert.Equal(2, factory.Readers.Count);
        Assert.NotSame(factory.Readers[0], factory.Readers[1]);
        Assert.All(factory.Readers, frameReader => Assert.Equal(1, frameReader.TileCount));
        Assert.All(factory.Readers, frameReader => Assert.Equal(1, frameReader.CompletionCount));
        Assert.All(factory.Readers, frameReader => Assert.Equal(1, frameReader.TileCountAtCompletion));
        Assert.Equal(temporalId, factory.Readers[0].CompletedTemporalId);
        Assert.Equal(baseSpatialId, factory.Readers[0].CompletedSpatialId);
        Assert.Equal(temporalId, factory.Readers[1].CompletedTemporalId);
        Assert.Equal(enhancementSpatialId, factory.Readers[1].CompletedSpatialId);
        Assert.True(factory.Readers[1].SelectedReferencesWereRetained);
        Assert.Equal(ObuFrameType.InterFrame, secondFrameHeader.FrameType);
        Assert.Equal(ProgressiveImageWidth, secondFrameHeader.FrameSize.SuperResolutionUpscaledWidth);
        Assert.Equal(ProgressiveImageHeight, secondFrameHeader.FrameSize.FrameHeight);
        Assert.Equal(ProgressiveImageWidth, secondFrameHeader.FrameSize.RenderWidth);
        Assert.Equal(ProgressiveImageHeight, secondFrameHeader.FrameSize.RenderHeight);
    }

    /// <summary>
    /// Verifies that invalid trailing data in a subsequent sequence header is rejected before that sequence can create frame state.
    /// </summary>
    [Fact]
    public void ReadAllRejectsMalformedSubsequentSequenceBeforeCreatingItsFrameReader()
    {
        byte[] firstLayer = ProgressiveTwoFrameObuStream[..FirstProgressiveLayerLength].ToArray();
        byte[] malformedSequence = AddInvalidSequenceHeaderTrailingByte(ProgressiveTwoFrameObuStream[..ProgressiveSequencePrefixLength].ToArray());
        byte[] bitStream = [.. firstLayer, .. malformedSequence];
        using Av1ReferenceFrameStore referenceFrames = new();
        ObuReader obuReader = new(ProgressiveOperatingPointIndex, referenceFrames);
        LifecycleTileReaderFactory factory = new(obuReader, referenceFrames, NoFailingReaderIndex);

        Assert.Throws<InvalidImageContentException>(() => ReadObuStream(bitStream, obuReader, factory.Create));
        AssertParserSessionReset(obuReader, referenceFrames);
        LifecycleTileReader completedFrame = Assert.Single(factory.Readers);

        Assert.Equal(1, completedFrame.TileCount);
        Assert.Equal(1, completedFrame.CompletionCount);

        ReadObuStream(ProgressiveTwoFrameObuStream.ToArray(), obuReader, factory.Create);

        Assert.Equal(3, factory.Readers.Count);
        Assert.Equal(1, factory.Readers[1].CompletionCount);
        Assert.Equal(1, factory.Readers[2].CompletionCount);
    }

    /// <summary>
    /// Verifies that synchronous tile validation failure prevents completion of the affected coded frame.
    /// </summary>
    [Fact]
    public void ReadAllDoesNotCompleteFrameWhenTileValidationFails()
    {
        byte[] bitStream = [.. ProgressiveTwoFrameObuStream];
        using Av1ReferenceFrameStore referenceFrames = new();
        ObuReader obuReader = new(ProgressiveOperatingPointIndex, referenceFrames);
        LifecycleTileReaderFactory factory = new(obuReader, referenceFrames, SecondFrameReaderIndex);

        Assert.Throws<InvalidImageContentException>(() => ReadObuStream(bitStream, obuReader, factory.Create));
        Assert.Equal(2, factory.Readers.Count);
        Assert.Equal(1, factory.Readers[0].CompletionCount);
        Assert.Equal(1, factory.Readers[1].TileCount);
        Assert.Equal(0, factory.Readers[1].CompletionCount);
        AssertParserSessionReset(obuReader, referenceFrames);

        ReadObuStream(bitStream, obuReader, factory.Create);

        Assert.Equal(4, factory.Readers.Count);
        Assert.Equal(1, factory.Readers[2].CompletionCount);
        Assert.Equal(1, factory.Readers[3].CompletionCount);
    }

    /// <summary>
    /// Verifies that malformed data after a completed real frame invalidates retained state without preventing reuse of the parser.
    /// </summary>
    [Fact]
    public void ReadAllClearsCompletedFrameStateWhenFollowingObuHeaderIsInvalid()
    {
        byte[] bitStream = [.. ProgressiveTwoFrameObuStream[..FirstProgressiveLayerLength], ForbiddenObuHeader];
        using Av1ReferenceFrameStore referenceFrames = new();
        ObuReader obuReader = new(ProgressiveOperatingPointIndex, referenceFrames);
        LifecycleTileReaderFactory factory = new(obuReader, referenceFrames, NoFailingReaderIndex);

        Assert.Throws<ImageFormatException>(() => ReadObuStream(bitStream, obuReader, factory.Create));
        AssertParserSessionReset(obuReader, referenceFrames);
        Assert.Equal(1, Assert.Single(factory.Readers).CompletionCount);

        ReadObuStream(ProgressiveTwoFrameObuStream.ToArray(), obuReader, factory.Create);

        Assert.Equal(3, factory.Readers.Count);
        Assert.Equal(1, factory.Readers[1].CompletionCount);
        Assert.Equal(1, factory.Readers[2].CompletionCount);
    }

    /// <summary>
    /// Verifies that a combined frame OBU cannot use the header-only retained-frame presentation form.
    /// </summary>
    [Fact]
    public void ReadAllRejectsShowExistingFrameInCombinedFrameObu()
    {
        byte[] bitStream =
        [
            .. ProgressiveTwoFrameObuStream[..FirstProgressiveLayerLength],

            // A one-byte combined-frame payload selecting retained slot zero. Current libaom rejects this form
            // because show_existing_frame is permitted only in a standalone frame-header OBU.
            0x32, 0x01, 0x80
        ];

        using Av1ReferenceFrameStore referenceFrames = new();
        ObuReader obuReader = new(ProgressiveOperatingPointIndex, referenceFrames);
        LifecycleTileReaderFactory factory = new(obuReader, referenceFrames, NoFailingReaderIndex);

        Assert.Throws<InvalidImageContentException>(() => ReadObuStream(bitStream, obuReader, factory.Create));
        AssertParserSessionReset(obuReader, referenceFrames);
        Assert.Equal(1, Assert.Single(factory.Readers).CompletionCount);
    }

    /// <summary>
    /// Extends the sequence-header OBU by one nonzero byte while retaining all following encoded frame bytes.
    /// </summary>
    /// <param name="stream">A temporal-delimiter and sequence-header OBU prefix.</param>
    /// <returns>A stream whose sequence-header syntax has invalid nonzero trailing data.</returns>
    private static byte[] AddInvalidSequenceHeaderTrailingByte(byte[] stream)
    {
        int sequenceObuOffset = GetNextObuOffset(stream, 0);
        Av1BitStreamReader sizeReader = new(stream.AsSpan(sequenceObuOffset + 1));
        uint sequencePayloadLength = (uint)sizeReader.ReadLittleEndianBytes128(out int oldSizeLength);
        int sequencePayloadOffset = sequenceObuOffset + 1 + oldSizeLength;
        int frameObuOffset = sequencePayloadOffset + (int)sequencePayloadLength;
        Span<byte> encodedSize = stackalloc byte[5];
        int newSizeLength = Av1BitStreamWriter.GetLittleEndianBytes128(sequencePayloadLength + 1, encodedSize);
        byte[] malformed = new byte[stream.Length + 1 + newSizeLength - oldSizeLength];

        stream.AsSpan(0, sequenceObuOffset + 1).CopyTo(malformed);
        encodedSize[..newSizeLength].CopyTo(malformed.AsSpan(sequenceObuOffset + 1));
        stream.AsSpan(sequencePayloadOffset, (int)sequencePayloadLength)
            .CopyTo(malformed.AsSpan(sequenceObuOffset + 1 + newSizeLength));

        int trailingByteOffset = sequenceObuOffset + 1 + newSizeLength + (int)sequencePayloadLength;
        malformed[trailingByteOffset] = InvalidTrailingByte;
        stream.AsSpan(frameObuOffset).CopyTo(malformed.AsSpan(trailingByteOffset + 1));
        return malformed;
    }

    /// <summary>
    /// Gets the byte offset immediately following one explicitly sized OBU.
    /// </summary>
    /// <param name="stream">The complete OBU stream.</param>
    /// <param name="obuOffset">The fixed-header offset of the current OBU.</param>
    /// <returns>The fixed-header offset of the following OBU.</returns>
    private static int GetNextObuOffset(byte[] stream, int obuOffset)
    {
        Av1BitStreamReader sizeReader = new(stream.AsSpan(obuOffset + 1));
        ulong payloadLength = sizeReader.ReadLittleEndianBytes128(out int sizeLength);
        return obuOffset + 1 + sizeLength + (int)payloadLength;
    }

    /// <summary>
    /// Reads a complete OBU stream through reference-type state so malformed-input assertions do not capture a ref struct.
    /// </summary>
    /// <param name="stream">The complete bounded OBU stream.</param>
    /// <param name="obuReader">The stateful OBU parser.</param>
    /// <param name="creator">Creates the tile reader for each coded frame.</param>
    private static void ReadObuStream(byte[] stream, ObuReader obuReader, Func<IAv1TileReader> creator)
    {
        Av1BitStreamReader reader = new(stream);
        obuReader.ReadAll(ref reader, stream.Length, creator);
    }

    /// <summary>
    /// Verifies that an unsuccessful bounded parse removed all state that could refer to the rejected session.
    /// </summary>
    /// <param name="obuReader">The parser whose published header state must be empty.</param>
    /// <param name="referenceFrames">The reference map whose retained frame owners must be empty.</param>
    private static void AssertParserSessionReset(ObuReader obuReader, Av1ReferenceFrameStore referenceFrames)
    {
        Assert.Null(obuReader.SequenceHeader);
        Assert.Null(obuReader.FrameHeader);

        for (int slot = 0; slot < Av1Constants.ReferenceFrameCount; slot++)
        {
            Assert.Null(referenceFrames.Resolve(slot));
        }
    }

    /// <summary>
    /// Creates and retains one recording tile reader for every coded frame requested by the OBU parser.
    /// </summary>
    private sealed class LifecycleTileReaderFactory
    {
        private readonly ObuReader obuReader;
        private readonly Av1ReferenceFrameStore referenceFrames;
        private readonly int failingReaderIndex;

        /// <summary>
        /// Initializes a new instance of the <see cref="LifecycleTileReaderFactory"/> class.
        /// </summary>
        /// <param name="obuReader">The parser that owns the current frame-header state.</param>
        /// <param name="referenceFrames">The reconstructed reference map shared with the parser.</param>
        /// <param name="failingReaderIndex">The zero-based reader index whose tile validation should fail.</param>
        public LifecycleTileReaderFactory(
            ObuReader obuReader,
            Av1ReferenceFrameStore referenceFrames,
            int failingReaderIndex)
        {
            this.obuReader = obuReader;
            this.referenceFrames = referenceFrames;
            this.failingReaderIndex = failingReaderIndex;
        }

        /// <summary>
        /// Gets the tile readers created in coded-frame order.
        /// </summary>
        public List<LifecycleTileReader> Readers { get; } = [];

        /// <summary>
        /// Creates a fresh recording tile reader for the parser's current coded frame.
        /// </summary>
        /// <returns>The fresh tile reader.</returns>
        public LifecycleTileReader Create()
        {
            LifecycleTileReader reader = new(
                this.obuReader,
                this.referenceFrames,
                this.Readers.Count == this.failingReaderIndex);

            this.Readers.Add(reader);
            return reader;
        }
    }

    /// <summary>
    /// Records tile-reader ownership and the frame-header state observable at completion.
    /// </summary>
    private sealed class LifecycleTileReader : IAv1TileReader
    {
        private readonly ObuReader obuReader;
        private readonly Av1ReferenceFrameStore referenceFrames;
        private readonly bool failTileValidation;

        /// <summary>
        /// Initializes a new instance of the <see cref="LifecycleTileReader"/> class.
        /// </summary>
        /// <param name="obuReader">The parser whose current frame header is captured at completion.</param>
        /// <param name="referenceFrames">The reconstructed reference map shared with the parser.</param>
        /// <param name="failTileValidation">A value indicating whether tile validation should fail.</param>
        public LifecycleTileReader(
            ObuReader obuReader,
            Av1ReferenceFrameStore referenceFrames,
            bool failTileValidation)
        {
            this.obuReader = obuReader;
            this.referenceFrames = referenceFrames;
            this.failTileValidation = failTileValidation;
        }

        /// <summary>
        /// Gets the number of tile payloads delivered to this frame reader.
        /// </summary>
        public int TileCount { get; private set; }

        /// <summary>
        /// Gets the number of frame-completion notifications delivered to this frame reader.
        /// </summary>
        public int CompletionCount { get; private set; }

        /// <summary>
        /// Gets the number of delivered tile payloads observed when the frame was completed.
        /// </summary>
        public int TileCountAtCompletion { get; private set; }

        /// <summary>
        /// Gets the primary frame-header OBU temporal identifier observed at completion.
        /// </summary>
        public int CompletedTemporalId { get; private set; }

        /// <summary>
        /// Gets the primary frame-header OBU spatial identifier observed at completion.
        /// </summary>
        public int CompletedSpatialId { get; private set; }

        /// <summary>
        /// Gets the frame header observed at successful completion.
        /// </summary>
        public ObuFrameHeader CompletedFrameHeader { get; private set; }

        /// <summary>
        /// Gets a value indicating whether every inter-reference role resolved to a retained reconstructed owner before
        /// the completed frame changed the reference map.
        /// </summary>
        public bool SelectedReferencesWereRetained { get; private set; }

        /// <inheritdoc/>
        public void ReadTile(Span<byte> tileData, int tileNum)
        {
            this.TileCount++;
            if (this.failTileValidation)
            {
                // The final tile owns the remaining declared OBU payload. A trailing-symbol or entropy validation
                // failure therefore originates at this boundary and must prevent the later completion callback.
                throw new InvalidImageContentException("The test tile payload failed validation.");
            }
        }

        /// <inheritdoc/>
        public void CompleteFrame()
        {
            ObuSequenceHeader sequenceHeader = Assert.IsType<ObuSequenceHeader>(this.obuReader.SequenceHeader);
            ObuFrameHeader frameHeader = Assert.IsType<ObuFrameHeader>(this.obuReader.FrameHeader);
            bool selectedReferencesWereRetained = true;

            if (!frameHeader.IsIntra)
            {
                Span<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();

                // Resolve all seven roles before committing the current frame. The refresh mask may replace those
                // slots, so checking after Commit would verify the new owner instead of the references just parsed.
                for (int reference = 0; reference < Av1Constants.ReferencesPerFrame; reference++)
                {
                    selectedReferencesWereRetained &= this.referenceFrames.Resolve((int)referenceFrameIndices[reference]) is not null;
                }
            }

            Av1FrameInfo frameInfo = new(sequenceHeader);
            Av1FrameBuffer<byte> frameBuffer = new(
                Configuration.Default,
                sequenceHeader,
                sequenceHeader.ColorConfig.GetColorFormat(),
                is16BitPipeline: false);

            Av1ReferenceFrame referenceFrame = new(frameBuffer, frameHeader, frameInfo);

            // A retained AV1 buffer exposes the visible post-super-resolution geometry rather than the sequence maxima
            // used for allocation. frame_size_with_refs reads these exact dimensions for the following coded layer.
            frameBuffer.Width = frameHeader.FrameSize.SuperResolutionUpscaledWidth;
            frameBuffer.Height = frameHeader.FrameSize.FrameHeight;

            if (!this.referenceFrames.Commit(frameHeader.RefreshFrameFlags, referenceFrame, showFrame: frameHeader.ShowFrame))
            {
                // A hidden frame with no refresh role remains caller-owned; match production by releasing it immediately.
                referenceFrame.Dispose();
            }

            this.TileCountAtCompletion = this.TileCount;
            this.CompletedTemporalId = frameHeader.TemporalId;
            this.CompletedSpatialId = frameHeader.SpatialId;
            this.CompletedFrameHeader = frameHeader;
            this.SelectedReferencesWereRetained = selectedReferencesWereRetained;
            this.CompletionCount++;
        }
    }
}
