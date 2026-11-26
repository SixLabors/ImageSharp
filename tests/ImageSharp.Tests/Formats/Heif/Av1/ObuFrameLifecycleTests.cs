// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies frame ownership and completion while parsing layered AV1 OBU payloads.
/// </summary>
[Trait("Format", "Avif")]
public class ObuFrameLifecycleTests
{
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
    /// Verifies that a bounded progressive payload is consumed completely and that one tile reader completes each coded frame.
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
        LifecycleTileReader tileReader = new(obuReader, referenceFrames);

        obuReader.ReadAll(ref reader, bitStream.Length, tileReader);

        Assert.Equal(2, tileReader.CompletedFrameHeaders.Count);
        ObuFrameHeader firstFrameHeader = tileReader.CompletedFrameHeaders[0];
        ObuFrameHeader secondFrameHeader = tileReader.CompletedFrameHeaders[1];

        Assert.Equal(bitStream.Length * 8, reader.BitPosition);
        Assert.Equal(2, tileReader.TileCount);

        // Each frame has one tile, so the reader holds one more tile at each completion.
        Assert.Equal([1, 2], tileReader.TileCountsAtCompletion);
        Assert.Equal(temporalId, firstFrameHeader.TemporalId);
        Assert.Equal(baseSpatialId, firstFrameHeader.SpatialId);
        Assert.Equal(temporalId, secondFrameHeader.TemporalId);
        Assert.Equal(enhancementSpatialId, secondFrameHeader.SpatialId);
        Assert.True(tileReader.SelectedReferencesWereRetained);
        Assert.Equal(ObuFrameType.InterFrame, secondFrameHeader.FrameType);
        Assert.Equal(ProgressiveImageWidth, secondFrameHeader.FrameSize.SuperResolutionUpscaledWidth);
        Assert.Equal(ProgressiveImageHeight, secondFrameHeader.FrameSize.FrameHeight);
        Assert.Equal(ProgressiveImageWidth, secondFrameHeader.FrameSize.RenderWidth);
        Assert.Equal(ProgressiveImageHeight, secondFrameHeader.FrameSize.RenderHeight);
    }

    /// <summary>
    /// Verifies that production existing-frame presentation needs no tile reader, frame buffer, or reconstruction graph.
    /// </summary>
    [Fact]
    public void SequenceDecoderPresentsExistingFrameWithoutTileState()
    {
        byte[] bitStream = [.. ProgressiveTwoFrameObuStream];

        // A standalone frame-header OBU selects retained slot zero. The payload contains show_existing_frame, the
        // three-bit slot index, and the required trailing-one bit; no tile-group OBU follows it.
        byte[] showExistingFrame = [0x1A, 0x01, 0x88];
        using Av1Decoder decoder = new(Configuration.Default, ProgressiveOperatingPointIndex, SegmentIntegrityHandling.Strict, false);
        using ImageFrame<Rgba32> reconstructed = new(Configuration.Default, ProgressiveImageWidth, ProgressiveImageHeight);
        decoder.DecodeSequenceFrame(
            bitStream,
            null,
            null,
            reconstructed.Size,
            reconstructed.Bounds,
            reconstructed.PixelBuffer.GetRegion(reconstructed.Bounds),
            default,
            null,
            null,
            false,
            out _);

        using ImageFrame<Rgba32> existing = new(Configuration.Default, ProgressiveImageWidth, ProgressiveImageHeight);
        decoder.DecodeSequenceFrame(
            showExistingFrame,
            null,
            null,
            existing.Size,
            existing.Bounds,
            existing.PixelBuffer.GetRegion(existing.Bounds),
            default,
            null,
            null,
            false,
            out _);

        Assert.Equal(reconstructed.Size, existing.Size);
        for (int row = 0; row < reconstructed.Height; row++)
        {
            Assert.True(
                reconstructed.PixelBuffer.DangerousGetRowSpan(row)
                    .SequenceEqual(existing.PixelBuffer.DangerousGetRowSpan(row)));
        }

        Assert.Null(decoder.FrameInfo);
    }

    /// <summary>
    /// Verifies that a payload that ends inside the header of its last OBU throws in strict mode. In the
    /// IgnoreImageData mode, the decoder keeps the frames before that header. Thus the output equals the output of
    /// the payload without the partial header.
    /// </summary>
    [Fact]
    public void DecodeKeepsFramesWhenPayloadEndsInsideObuHeader()
    {
        // 0x12 is the header of a temporal delimiter OBU with obu_has_size_field set. The payload ends before the
        // size field.
        byte[] truncated = [.. ProgressiveTwoFrameObuStream, 0x12];
        using Image<Rgba32> expected = DecodeProgressiveStream([.. ProgressiveTwoFrameObuStream], SegmentIntegrityHandling.Strict);

        Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<Rgba32> image = DecodeProgressiveStream(truncated, SegmentIntegrityHandling.Strict);
        });

        using Image<Rgba32> actual = DecodeProgressiveStream(truncated, SegmentIntegrityHandling.IgnoreImageData);
        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }

    /// <summary>
    /// Decodes a progressive two-layer payload as one sequence sample.
    /// </summary>
    /// <param name="bitStream">The AV1 payload.</param>
    /// <param name="integrityHandling">The segment integrity mode of the decoder.</param>
    /// <returns>The presented frame.</returns>
    private static Image<Rgba32> DecodeProgressiveStream(byte[] bitStream, SegmentIntegrityHandling integrityHandling)
    {
        using Av1Decoder decoder = new(Configuration.Default, ProgressiveOperatingPointIndex, integrityHandling, false);
        Image<Rgba32> image = new(Configuration.Default, ProgressiveImageWidth, ProgressiveImageHeight);
        try
        {
            ImageFrame<Rgba32> frame = image.Frames.RootFrame;
            decoder.DecodeSequenceFrame(
                bitStream,
                null,
                null,
                frame.Size,
                frame.Bounds,
                frame.PixelBuffer.GetRegion(frame.Bounds),
                default,
                null,
                null,
                false,
                out _);

            return image;
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Records tile-reader ownership and the frame-header state observable at completion.
    /// </summary>
    private sealed class LifecycleTileReader : IAv1TileReader
    {
        private readonly ObuReader obuReader;
        private readonly Av1ReferenceFrameStore referenceFrames;

        /// <summary>
        /// Initializes a new instance of the <see cref="LifecycleTileReader"/> class.
        /// </summary>
        /// <param name="obuReader">The parser whose current frame header is captured at completion.</param>
        /// <param name="referenceFrames">The reconstructed reference map shared with the parser.</param>
        public LifecycleTileReader(ObuReader obuReader, Av1ReferenceFrameStore referenceFrames)
        {
            this.obuReader = obuReader;
            this.referenceFrames = referenceFrames;
        }

        /// <summary>
        /// Gets the number of tile payloads delivered to this reader.
        /// </summary>
        public int TileCount { get; private set; }

        /// <summary>
        /// Gets the number of delivered tile payloads at each frame completion, in coded-frame order.
        /// </summary>
        public List<int> TileCountsAtCompletion { get; } = [];

        /// <summary>
        /// Gets the frame header of each successful completion, in coded-frame order.
        /// </summary>
        public List<ObuFrameHeader> CompletedFrameHeaders { get; } = [];

        /// <summary>
        /// Gets a value indicating whether every inter-reference role resolved to a retained reconstructed owner before
        /// the last completed frame changed the reference map.
        /// </summary>
        public bool SelectedReferencesWereRetained { get; private set; }

        /// <inheritdoc/>
        public string ReadTile(Span<byte> tileData, int tileNum)
        {
            this.TileCount++;
            return null;
        }

        /// <inheritdoc/>
        public string CompleteFrame()
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

            using Av1FrameInfo frameInfo = new(sequenceHeader);
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

            this.TileCountsAtCompletion.Add(this.TileCount);
            this.CompletedFrameHeaders.Add(frameHeader);
            this.SelectedReferencesWereRetained = selectedReferencesWereRetained;
            return null;
        }
    }
}
