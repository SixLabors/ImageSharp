// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Parses the bounded track and sample syntax required to identify a HEIF image sequence.
/// </summary>
internal sealed class HeifSequenceParser
{
    /// <summary>
    /// The size of the box header buffer. <see cref="HeifBoxReader.ReadHeader(Stream, long, Span{byte}, out Heif4CharCode, bool)"/> reads the size and type
    /// fields into eight bytes. The parser also reads the eight-byte prefix of a sample group box into this buffer.
    /// </summary>
    private const int HeaderBufferLength = 8;

    /// <summary>
    /// The length of the fixed fields of a visual sample entry. ISO/IEC 14496-12, section 12.1.3: the sample entry has 6 reserved bytes and a 16-bit data
    /// reference index. The visual sample entry adds 70 bytes.
    /// </summary>
    private const int VisualSampleEntryLength = 78;

    /// <summary>
    /// The configured allocator for box payloads and bounded table state.
    /// </summary>
    private readonly MemoryAllocator allocator;

    /// <summary>
    /// The shared reader for variable-length HEIF box headers and payloads.
    /// </summary>
    private readonly HeifBoxReader boxReader;

    /// <summary>
    /// The bounded parser for Exif and XMP items embedded in selected image tracks.
    /// </summary>
    private readonly HeifTrackMetadataParser metadataParser;

    /// <summary>
    /// The maximum number of sample descriptors retained for decoding or identification.
    /// </summary>
    private readonly int maxFrames;

    /// <summary>
    /// The general decoder options controlling frame limits, metadata loading, and recoverable segment errors.
    /// </summary>
    private readonly DecoderOptions options;

    /// <summary>
    /// The absolute stream position at which the current HEIF file begins.
    /// </summary>
    private long fileStartOffset;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeifSequenceParser"/> class.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    public HeifSequenceParser(DecoderOptions options)
    {
        this.options = options;
        this.allocator = options.Configuration.MemoryAllocator;
        this.boxReader = new HeifBoxReader(this.allocator);
        this.metadataParser = new HeifTrackMetadataParser(this.boxReader, options.SegmentIntegrityHandling);
        this.maxFrames = (int)options.MaxFrames;
    }

    /// <summary>
    /// Gets the child boxes of a sample table that the parser reads. The parser requires the first three child boxes.
    /// </summary>
    private static ReadOnlySpan<Heif4CharCode> SampleTableChildren =>
    [
        Heif4CharCode.Stsd,
        Heif4CharCode.Stts,
        Heif4CharCode.Stsc,
        Heif4CharCode.Stsz,
        Heif4CharCode.Stz2,
        Heif4CharCode.Stco,
        Heif4CharCode.Co64,
        Heif4CharCode.Stss,
        Heif4CharCode.Ctts,
        Heif4CharCode.Cslg
    ];

    /// <summary>
    /// Parses one movie box and selects its master image-sequence track and linked alpha track.
    /// </summary>
    /// <param name="stream">The seekable HEIF stream positioned at the movie payload.</param>
    /// <param name="boxLength">The validated movie payload length.</param>
    /// <param name="fileStartOffset">The absolute stream position at which the HEIF file begins.</param>
    /// <returns>The bounded image-sequence model required by the HEIF decoder.</returns>
    public HeifSequence Parse(Stream stream, long boxLength, long fileStartOffset)
    {
        this.fileStartOffset = fileStartOffset;
        long movieEnd = stream.Position + boxLength;

        Span<byte> headerBuffer = stackalloc byte[HeaderBufferLength];
        HeifBoxRange movieHeader = default;
        List<TrackIdentity> imageTracks = [];
        HashSet<uint> trackIds = [];

        // One pass reads the fixed identity fields of each track. Thus a file with unrelated media tracks cannot put their codec configurations and sample
        // tables into the retained model of the image decoder.
        while (stream.Position < movieEnd)
        {
            long childLength = HeifBoxReader.ReadHeader(stream, movieEnd, headerBuffer, out Heif4CharCode childType, false);
            long childStart = stream.Position;
            if (childType == Heif4CharCode.Mvhd)
            {
                if (movieHeader.IsPresent)
                {
                    throw new InvalidImageContentException("The movie box contains duplicate 'mvhd' boxes.");
                }

                movieHeader = new HeifBoxRange(childStart, childLength, childType);
            }
            else if (childType == Heif4CharCode.Trak)
            {
                TrackIdentity identity = this.ScanTrackIdentity(stream, childLength, headerBuffer);
                if (identity.HandlerType is Heif4CharCode.Pict or Heif4CharCode.Auxv)
                {
                    // ISO/IEC 14496-12, section 8.3.2: each track has a unique track_ID. A repeated identifier makes the track references ambiguous. The parser
                    // reads the identifier of image tracks only.
                    if (!trackIds.Add(identity.Id))
                    {
                        throw new InvalidImageContentException($"The HEIF image sequence contains more than one track with identifier {identity.Id}.");
                    }

                    imageTracks.Add(identity);
                }
            }

            stream.Position = childStart + childLength;
        }

        if (!movieHeader.IsPresent)
        {
            throw new InvalidImageContentException("The HEIF image sequence has no movie header.");
        }

        TrackIdentity? colorIdentity = null;
        foreach (TrackIdentity identity in imageTracks)
        {
            if (identity.IsEnabled && identity.HandlerType == Heif4CharCode.Pict && identity.AuxiliaryForTrackId == 0)
            {
                colorIdentity = identity;
                break;
            }
        }

        if (colorIdentity is null)
        {
            throw new InvalidImageContentException("The HEIF image sequence has no enabled picture track.");
        }

        uint movieTimescale = this.ParseMovieHeader(stream, movieHeader);
        HeifSequenceTrack colorTrack = this.ParseTrack(stream, colorIdentity.Value, headerBuffer);

        // Track references can come before the master track. Thus the parser selects the alpha track after the identity pass. The parser fully parses only the
        // master track and the linked auxiliary tracks. It stops at the first linked alpha track.
        HeifSequenceTrack? alphaTrack = null;
        foreach (TrackIdentity identity in imageTracks)
        {
            if (identity.AuxiliaryForTrackId == colorIdentity.Value.Id)
            {
                HeifSequenceTrack track = this.ParseTrack(stream, identity, headerBuffer);
                if (track.IsAlpha)
                {
                    alphaTrack = track;
                    break;
                }
            }
        }

        if (!this.ValidateAlphaTrack(colorTrack, alphaTrack))
        {
            // Alpha is optional image data. If the mode ignores the alpha error, the decoder decodes the color presentation without alpha.
            alphaTrack = null;
        }

        return new HeifSequence(colorTrack, alphaTrack, movieTimescale);
    }

    /// <summary>
    /// Reads the fixed identity fields used to select an image track without materializing its sample table.
    /// </summary>
    /// <param name="stream">The stream positioned at the track payload.</param>
    /// <param name="boxLength">The validated track payload length.</param>
    /// <param name="headerBuffer">The parser-owned box header buffer.</param>
    /// <returns>The track identity and image relationship fields.</returns>
    private TrackIdentity ScanTrackIdentity(Stream stream, long boxLength, Span<byte> headerBuffer)
    {
        // The scan indexes the track and media children one time. The identity keeps the ranges that ParseTrack reads for a selected track. The scan requires
        // only the handler here. Thus an unrelated media track needs no image boxes.
        Span<HeifBoxRange> children = stackalloc HeifBoxRange[5];
        ReadOnlySpan<Heif4CharCode> trackTypes = [Heif4CharCode.Mdia, Heif4CharCode.Tkhd, Heif4CharCode.Tref, Heif4CharCode.Edts, Heif4CharCode.Meta];
        HeifBoxReader.IndexChildren(stream, boxLength, headerBuffer, trackTypes, children, 1, "track");

        Span<HeifBoxRange> mediaChildren = stackalloc HeifBoxRange[3];
        stream.Position = children[0].Offset;
        ReadOnlySpan<Heif4CharCode> mediaTypes = [Heif4CharCode.Hdlr, Heif4CharCode.Mdhd, Heif4CharCode.Minf];
        HeifBoxReader.IndexChildren(stream, children[0].Length, headerBuffer, mediaTypes, mediaChildren, 1, "media");

        Heif4CharCode handlerType;
        using (IMemoryOwner<byte> handler = this.boxReader.ReadPayload(stream, mediaChildren[0]))
        {
            handlerType = HeifItemBoxParser.ParseHandler(handler.GetSpan());
        }

        if (handlerType is not Heif4CharCode.Pict and not Heif4CharCode.Auxv)
        {
            // Non-image tracks are outside this parser's retained ISOBMFF surface. Do not impose image dimensions,
            // matrices, or reference semantics on audio and other unrelated media carried by a valid image file.
            TrackIdentity unrelatedIdentity = default;
            unrelatedIdentity.HandlerType = handlerType;
            return unrelatedIdentity;
        }

        if (!children[1].IsPresent)
        {
            throw new InvalidImageContentException("A HEIF image-sequence track is missing its track header.");
        }

        TrackIdentity identity;
        using (IMemoryOwner<byte> trackHeader = this.boxReader.ReadPayload(stream, children[1]))
        {
            identity = ParseTrackHeader(trackHeader.GetSpan());
        }

        if (children[2].IsPresent)
        {
            using IMemoryOwner<byte> trackReferences = this.boxReader.ReadPayload(stream, children[2]);
            ParseTrackReferences(trackReferences.GetSpan(), ref identity);
        }

        identity.HandlerType = handlerType;
        identity.EditBox = children[3];
        identity.MetadataBox = children[4];
        identity.MediaHeaderBox = mediaChildren[1];
        identity.MediaInformationBox = mediaChildren[2];
        return identity;
    }

    /// <summary>
    /// Parses the retained behavior of one selected image-sequence track.
    /// </summary>
    /// <param name="stream">The seekable HEIF stream.</param>
    /// <param name="identity">The fixed identity fields and the child box ranges of the track.</param>
    /// <param name="headerBuffer">The parser-owned box header buffer.</param>
    /// <returns>The selected track with its validated sample descriptors.</returns>
    private HeifSequenceTrack ParseTrack(Stream stream, TrackIdentity identity, Span<byte> headerBuffer)
    {
        // The identity scan required only the handler of the media box. A selected image track also needs the media header and the media information.
        if (!identity.MediaHeaderBox.IsPresent)
        {
            throw new InvalidImageContentException($"The media box has no '{Heif4CharCode.Mdhd}' box.");
        }

        if (!identity.MediaInformationBox.IsPresent)
        {
            throw new InvalidImageContentException($"The media box has no '{Heif4CharCode.Minf}' box.");
        }

        HeifSequenceTrack track = new()
        {
            Id = identity.Id,
            Width = identity.Width,
            Height = identity.Height,
            HandlerType = identity.HandlerType,
            TrackDuration = identity.TrackDuration,
            AuxiliaryForTrackId = identity.AuxiliaryForTrackId,
            PremultipliedByTrackId = identity.PremultipliedByTrackId
        };

        HeifBoxRange editBox = identity.EditBox;
        if (editBox.IsPresent)
        {
            stream.Position = editBox.Offset;
            this.ParseEdit(stream, editBox.Length, track, headerBuffer);
        }

        HeifBoxRange metadataBox = identity.MetadataBox;
        if (metadataBox.IsPresent && !this.options.SkipMetadata)
        {
            stream.Position = metadataBox.Offset;
            track.Metadata = this.metadataParser.Parse(stream, metadataBox.Length, headerBuffer, this.fileStartOffset);
        }

        // The identity pass read the handler of the media box and indexed its children. Thus the parser does not read them again.
        using (IMemoryOwner<byte> mediaHeader = this.boxReader.ReadPayload(stream, identity.MediaHeaderBox))
        {
            ParseMediaHeader(mediaHeader.GetSpan(), track);
        }

        HeifBoxRange mediaInformationBox = identity.MediaInformationBox;
        Span<HeifBoxRange> informationChildren = stackalloc HeifBoxRange[2];
        stream.Position = mediaInformationBox.Offset;
        ReadOnlySpan<Heif4CharCode> informationTypes = [Heif4CharCode.Dinf, Heif4CharCode.Stbl];
        HeifBoxReader.IndexChildren(stream, mediaInformationBox.Length, headerBuffer, informationTypes, informationChildren, 2, "media information");

        stream.Position = informationChildren[0].Offset;
        this.ParseDataInformation(stream, informationChildren[0].Length, headerBuffer);
        stream.Position = informationChildren[1].Offset;
        this.ParseSampleTable(stream, informationChildren[1].Length, track, headerBuffer);
        return track;
    }

    /// <summary>
    /// Parses the movie time scale and validates that no general movie-canvas transformation is required.
    /// </summary>
    /// <param name="stream">The seekable HEIF stream.</param>
    /// <param name="box">The movie-header payload range.</param>
    /// <returns>The nonzero movie time scale.</returns>
    private uint ParseMovieHeader(Stream stream, HeifBoxRange box)
    {
        using IMemoryOwner<byte> payload = this.boxReader.ReadPayload(stream, box);

        // ISO/IEC 14496-12, section 8.2.2: version 1 widens the times and the duration from 32 to 64 bits. The rate, volume and reserved fields precede the
        // matrix. The pre_defined fields and next_track_ID follow it.
        HeifSpanReader reader = new(payload.GetSpan(), "movie header");
        byte version = reader.ReadFullBoxHeader(1, 0, out _);
        int timeSize = version == 1 ? 8 : 4;
        reader.Skip(2 * timeSize);
        uint timescale = reader.ReadUInt32();
        reader.Skip(timeSize + 4 + 2 + 10);
        ReadOnlySpan<byte> matrixData = reader.ReadBytes(36);
        reader.Skip(24 + 4);
        reader.EnsureNotTruncated();
        if (timescale == 0)
        {
            throw new InvalidImageContentException("The movie header has a zero time scale.");
        }

        if (!HeifTrackMatrix.Parse(matrixData).IsIdentity)
        {
            throw new NotSupportedException("The HEIF image sequence requires an unsupported movie presentation matrix.");
        }

        return timescale;
    }

    /// <summary>
    /// Parses track identity, dimensions, duration, and transformation matrix.
    /// </summary>
    /// <param name="payload">The track-header full-box payload.</param>
    /// <returns>The fixed track identity fields.</returns>
    private static TrackIdentity ParseTrackHeader(ReadOnlySpan<byte> payload)
    {
        const int fixedPointFractionalBits = 16;
        const uint trackEnabledFlag = 1 << 0;

        // ISO/IEC 14496-12, Section 8.3.2 defines 'tkhd' as a FullBox with creation and modification times, the track identifier, a reserved field, and the
        // duration. Version 1 widens each time and duration field from 32 to 64 bits. Two reserved 32-bit values and the 16-bit layer, alternate_group, volume,
        // and reserved values follow the duration. Then the matrix holds nine 32-bit fixed-point coefficients. The two 32-bit track dimensions follow the
        // matrix.
        HeifSpanReader reader = new(payload, "track header");
        byte version = reader.ReadFullBoxHeader(1, HeifSpanReader.AnyFlags, out uint flags);
        int timeSize = version == 1 ? 8 : 4;
        reader.Skip(2 * timeSize);
        uint id = reader.ReadUInt32();
        reader.Skip(4);
        ulong duration = reader.ReadUInt(timeSize);
        reader.Skip((2 * sizeof(uint)) + (4 * sizeof(ushort)));
        ReadOnlySpan<byte> matrixData = reader.ReadBytes(36);
        uint fixedWidth = reader.ReadUInt32();
        uint fixedHeight = reader.ReadUInt32();
        reader.EnsureNotTruncated();

        // Section 8.3.2 reserves track_ID zero. A zero identifier makes track references ambiguous, so reject it.
        if (id == 0)
        {
            throw new InvalidImageContentException("A HEIF image-sequence track has identifier zero.");
        }

        // The specification uses the all-ones value for an indefinite duration. Normalize the 32-bit version to
        // the 64-bit sentinel used by the sequence model so both TrackHeaderBox versions follow the same path.
        if (version == 0 && duration == uint.MaxValue)
        {
            duration = ulong.MaxValue;
        }

        // Section 8.3.2 stores width and height as unsigned 16.16 fixed-point values. ImageSharp dimensions are whole pixels. Thus the parser discards the
        // fractional half before it validates the display size. The integer half has 16 bits.
        int width = (int)(fixedWidth >> fixedPointFractionalBits);
        int height = (int)(fixedHeight >> fixedPointFractionalBits);
        if (width == 0 || height == 0)
        {
            throw new InvalidImageContentException("A HEIF image-sequence track has zero dimensions.");
        }

        // The Section 8.3.2 matrix transforms the track into the coordinate system of the movie presentation. The
        // decoder writes the stored raster without this transform, so it rejects a matrix that is not the identity.
        if (!HeifTrackMatrix.Parse(matrixData).IsIdentity)
        {
            throw new NotSupportedException("The HEIF image-sequence track requires an unsupported movie presentation matrix.");
        }

        // ISO/IEC 14496-12 Section 8.3.2 assigns bit zero to track_enabled. Many image sequence writers do not set
        // track_in_movie, so only the enabled bit takes part in track selection.
        bool isEnabled = (flags & trackEnabledFlag) != 0;
        return new TrackIdentity(id, isEnabled, width, height, duration);
    }

    /// <summary>
    /// Parses the image-specific track references used for alpha and premultiplication linkage.
    /// </summary>
    /// <param name="payload">The track-reference payload.</param>
    /// <param name="identity">The track identity receiving image relationships.</param>
    private static void ParseTrackReferences(ReadOnlySpan<byte> payload, ref TrackIdentity identity)
    {
        HeifSpanReader reader = new(payload, "track reference");
        while (reader.Remaining > 0)
        {
            ReadOnlySpan<byte> remaining = reader.RemainingData;
            int headerLength = HeifBoxReader.ParseHeader(remaining, out long childLength, out Heif4CharCode childType);
            reader.Skip(headerLength + (int)childLength);
            if (childType is not Heif4CharCode.Auxl and not Heif4CharCode.Prem)
            {
                continue;
            }

            // ISO/IEC 14496-12, section 8.3.3: a track reference type box holds a list of 32-bit track identifiers. The image relationships use the first
            // identifier.
            if (childLength < 4 || (childLength & 3) != 0)
            {
                throw new InvalidImageContentException($"The '{childType}' track reference has an invalid identifier list.");
            }

            HeifSpanReader idReader = new(remaining.Slice(headerLength, (int)childLength), "track reference type");
            uint referencedTrackId = idReader.ReadUInt32();
            if (referencedTrackId == 0)
            {
                throw new InvalidImageContentException($"The '{childType}' track reference contains identifier zero.");
            }

            if (childType == Heif4CharCode.Auxl)
            {
                if (identity.AuxiliaryForTrackId != 0)
                {
                    throw new InvalidImageContentException("The track contains duplicate alpha-auxiliary references.");
                }

                identity.AuxiliaryForTrackId = referencedTrackId;
            }
            else
            {
                if (identity.PremultipliedByTrackId != 0)
                {
                    throw new InvalidImageContentException("The track contains duplicate premultiplication references.");
                }

                identity.PremultipliedByTrackId = referencedTrackId;
            }
        }
    }

    /// <summary>
    /// Parses the media time scale and duration of a selected track.
    /// </summary>
    /// <param name="payload">The media-header full-box payload.</param>
    /// <param name="track">The selected track receiving timing state.</param>
    private static void ParseMediaHeader(ReadOnlySpan<byte> payload, HeifSequenceTrack track)
    {
        // ISO/IEC 14496-12, section 8.4.2: version 1 widens the times and the duration from 32 to 64 bits. The language and pre_defined fields follow the
        // duration.
        HeifSpanReader reader = new(payload, "media header");
        byte version = reader.ReadFullBoxHeader(1, 0, out _);
        int timeSize = version == 1 ? 8 : 4;
        reader.Skip(2 * timeSize);
        uint timescale = reader.ReadUInt32();
        ulong duration = reader.ReadUInt(timeSize);
        reader.Skip(4);
        reader.EnsureNotTruncated();
        if (timescale == 0)
        {
            throw new InvalidImageContentException("The HEIF image-sequence media header has a zero time scale.");
        }

        track.MediaTimescale = timescale;
        track.MediaDuration = duration;
    }

    /// <summary>
    /// Requires a selected image track to address sample bytes in the current HEIF file.
    /// </summary>
    /// <param name="stream">The stream positioned at the data-information payload.</param>
    /// <param name="boxLength">The validated data-information payload length.</param>
    /// <param name="headerBuffer">The parser-owned box header buffer.</param>
    private void ParseDataInformation(Stream stream, long boxLength, Span<byte> headerBuffer)
    {
        Span<HeifBoxRange> children = stackalloc HeifBoxRange[1];
        HeifBoxReader.IndexChildren(stream, boxLength, headerBuffer, [Heif4CharCode.Dref], children, 1, "data information");
        using IMemoryOwner<byte> payload = this.boxReader.ReadPayload(stream, children[0]);

        // ISO/IEC 14496-12, section 8.7.2: the data reference box holds an entry count and the entries. The parser accepts only one 'url ' entry with flag 1.
        // This flag means that the sample data is in the same file.
        HeifSpanReader reader = new(payload.GetSpan(), "data reference");
        reader.ReadFullBoxHeader(0, 0, out _);
        uint referenceCount = reader.ReadUInt32();
        reader.EnsureNotTruncated();
        if (referenceCount != 1)
        {
            throw new InvalidImageContentException("A HEIF image-sequence track must contain exactly one data reference.");
        }

        ReadOnlySpan<byte> remaining = reader.RemainingData;
        int headerLength = HeifBoxReader.ParseHeader(remaining, out long locationLength, out Heif4CharCode locationType);
        if (locationType != Heif4CharCode.Url || locationLength != 4)
        {
            throw new InvalidImageContentException("A HEIF image-sequence track uses an external data reference.");
        }

        HeifSpanReader locationReader = new(remaining.Slice(headerLength, 4), "data location");
        locationReader.ReadFullBoxHeader(0, 1, out uint flags);
        if (flags != 1)
        {
            throw new InvalidImageContentException("A HEIF image-sequence track uses an external data reference.");
        }

        reader.Skip(headerLength + 4);
        reader.EnsureEnd();
    }

    /// <summary>
    /// Indexes, reads and validates the bounded sample-table boxes required by an image sequence.
    /// </summary>
    /// <param name="stream">The stream positioned at the sample-table payload.</param>
    /// <param name="boxLength">The validated sample-table payload length.</param>
    /// <param name="track">The selected track receiving sample descriptors.</param>
    /// <param name="headerBuffer">The parser-owned box header buffer.</param>
    private void ParseSampleTable(Stream stream, long boxLength, HeifSequenceTrack track, Span<byte> headerBuffer)
    {
        long tableStart = stream.Position;
        Span<HeifBoxRange> children = stackalloc HeifBoxRange[SampleTableChildren.Length];
        HeifBoxReader.IndexChildren(stream, boxLength, headerBuffer, SampleTableChildren, children, 3, "sample table");

        // ISO/IEC 14496-12, sections 8.7.3 and 8.7.5: a sample table holds one sample-size box ('stsz' or 'stz2') and one chunk-offset box ('stco' or 'co64').
        if (children[3].IsPresent == children[4].IsPresent || children[5].IsPresent == children[6].IsPresent)
        {
            throw new InvalidImageContentException("A selected HEIF image-sequence sample table needs one sample-size box and one chunk-offset box.");
        }

        if (children[8].IsPresent || children[9].IsPresent)
        {
            // AV1-ISOBMFF defines the composition time of an AV1 sample as its decode time. AV1-ISOBMFF prohibits both boxes.
            throw new InvalidImageContentException("An AV1 image-sequence track contains prohibited composition timing boxes.");
        }

        stream.Position = children[0].Offset;
        this.ParseSampleDescription(stream, children[0].Length, track, headerBuffer);

        HeifBoxRange sampleSizesBox = children[3].IsPresent ? children[3] : children[4];
        HeifBoxRange chunkOffsetsBox = children[5].IsPresent ? children[5] : children[6];
        int chunkOffsetSize = chunkOffsetsBox.Type == Heif4CharCode.Co64 ? 8 : 4;
        using IMemoryOwner<byte> sampleSizes = this.boxReader.ReadPayload(stream, sampleSizesBox);
        using IMemoryOwner<byte> timeToSample = this.boxReader.ReadPayload(stream, children[1]);
        using IMemoryOwner<byte> sampleToChunk = this.boxReader.ReadPayload(stream, children[2]);
        using IMemoryOwner<byte> chunkOffsets = this.boxReader.ReadPayload(stream, chunkOffsetsBox);
        using IMemoryOwner<byte>? syncSamples = children[7].IsPresent ? this.boxReader.ReadPayload(stream, children[7]) : null;

        ReadOnlySpan<byte> storedSampleSizes = ReadSampleSizes(
            sampleSizes.GetSpan(),
            sampleSizesBox.Type,
            out uint sampleCount,
            out uint constantSampleSize,
            out int sampleSizeBits);

        SampleTable table = new()
        {
            SampleSizes = storedSampleSizes,
            SampleCount = sampleCount,
            ConstantSampleSize = constantSampleSize,
            SampleSizeBits = sampleSizeBits,
            TimeToSample = ReadEntryTable(timeToSample.GetSpan(), "sample timing", 8),
            SampleToChunk = ReadEntryTable(sampleToChunk.GetSpan(), "sample-to-chunk", 12),
            ChunkOffsets = ReadEntryTable(chunkOffsets.GetSpan(), "chunk offsets", chunkOffsetSize),
            ChunkOffsetSize = chunkOffsetSize,
            SyncSamples = syncSamples is null ? default : ReadEntryTable(syncSamples.GetSpan(), "sync samples", 4)
        };

        this.ValidateSampleTable(track, table, stream.Length - this.fileStartOffset);
        this.ParseDirectReferences(stream, tableStart, boxLength, track, headerBuffer);
    }

    /// <summary>
    /// Reads the fields of a full-width or compact sample-size box.
    /// </summary>
    /// <param name="payload">The sample-size full-box payload.</param>
    /// <param name="boxType">The 'stsz' or 'stz2' box type.</param>
    /// <param name="sampleCount">Receives the number of samples.</param>
    /// <param name="constantSampleSize">Receives the size of every sample, or zero when the box stores each size.</param>
    /// <param name="sampleSizeBits">Receives the width of each stored size in bits.</param>
    /// <returns>The stored sizes.</returns>
    private static ReadOnlySpan<byte> ReadSampleSizes(
        ReadOnlySpan<byte> payload,
        Heif4CharCode boxType,
        out uint sampleCount,
        out uint constantSampleSize,
        out int sampleSizeBits)
    {
        HeifSpanReader reader = new(payload, "sample sizes");
        reader.ReadFullBoxHeader(0, 0, out _);
        ReadOnlySpan<byte> sizes;
        if (boxType == Heif4CharCode.Stsz)
        {
            // ISO/IEC 14496-12, section 8.7.3.2: sample_size, sample_count, and a 32-bit size for each sample when sample_size is zero.
            constantSampleSize = reader.ReadUInt32();
            sampleCount = constantSampleSize == 0 ? reader.ReadEntryCount(4, 4) : reader.ReadUInt32();
            sampleSizeBits = 32;
            sizes = reader.ReadBytes(constantSampleSize == 0 ? (int)sampleCount * 4 : 0);
        }
        else
        {
            // ISO/IEC 14496-12, section 8.7.3.3: 24 reserved bits, field_size of 4, 8 or 16 bits, sample_count, and the packed sizes.
            uint reservedAndFieldSize = reader.ReadUInt32();
            reader.EnsureNotTruncated();
            sampleSizeBits = (int)(reservedAndFieldSize & 0xFF);
            if ((reservedAndFieldSize >> 8) != 0 || sampleSizeBits is not 4 and not 8 and not 16)
            {
                throw new InvalidImageContentException("The compact sample-size table has invalid reserved fields or field width.");
            }

            constantSampleSize = 0;
            sampleCount = reader.ReadUInt32();
            long sizeBytes = (((long)sampleCount * sampleSizeBits) + 7) / 8;
            sizes = reader.ReadBytes((int)Math.Min(sizeBytes, int.MaxValue));
        }

        reader.EnsureEnd();
        if (sampleCount == 0)
        {
            throw new InvalidImageContentException("The image-sequence sample-size table is empty.");
        }

        return sizes;
    }

    /// <summary>
    /// Reads the fields of a sample-table box that holds a 32-bit entry count and fixed-size entries.
    /// </summary>
    /// <param name="payload">The full-box payload.</param>
    /// <param name="name">The box name used in the error messages.</param>
    /// <param name="entrySize">The entry size in bytes.</param>
    /// <returns>The entries. The span holds at least one entry.</returns>
    private static ReadOnlySpan<byte> ReadEntryTable(ReadOnlySpan<byte> payload, string name, int entrySize)
    {
        HeifSpanReader reader = new(payload, name);
        reader.ReadFullBoxHeader(0, 0, out _);
        uint entryCount = reader.ReadEntryCount(4, entrySize);
        ReadOnlySpan<byte> entries = reader.ReadBytes((int)entryCount * entrySize);
        reader.EnsureEnd();
        if (entryCount == 0)
        {
            throw new InvalidImageContentException($"The image-sequence {name} table is empty.");
        }

        return entries;
    }

    /// <summary>
    /// Validates the sample-table boxes against each other and the file, and creates the retained sample descriptors.
    /// </summary>
    /// <remarks>
    /// This method is the sample table validator. It runs once after the parser reads the sample table boxes. The box readers only read fields. The validator
    /// limits each sample to the bytes that the file holds. It marks each sample that ends after the end of the file. Only the decoder reports an image data
    /// error for that sample.
    /// </remarks>
    /// <param name="track">The selected track receiving the retained sample descriptors.</param>
    /// <param name="table">The sample table fields.</param>
    /// <param name="fileLength">The number of file bytes from the start of the HEIF file to the end of the stream.</param>
    private void ValidateSampleTable(HeifSequenceTrack track, in SampleTable table, long fileLength)
    {
        uint sampleCount = table.SampleCount;

        // A table of stored sizes holds the bytes of each size. Thus, the box length limits its count. A box with a constant size stores no entries. Thus,
        // only the file limits the count, because each sample occupies ConstantSampleSize bytes of the file. The validator validates this limit before it
        // allocates the retained descriptors from the declared count.
        if (table.ConstantSampleSize != 0 && (ulong)table.ConstantSampleSize * sampleCount > (ulong)fileLength)
        {
            throw new InvalidImageContentException("The image-sequence constant-size samples do not fit in the file.");
        }

        int retainedCount = (int)Math.Min(sampleCount, (uint)this.maxFrames);
        HeifSequenceSample[] samples = new HeifSequenceSample[retainedCount];
        track.TotalSampleCount = sampleCount;
        track.Samples = samples;
        ValidateSampleSizes(table, samples);

        // The time-to-sample runs describe exactly the samples of the sample-size table.
        HeifSpanReader timing = new(table.TimeToSample, "sample timing");
        ulong describedSamples = 0;
        ulong decodedDuration = 0;
        int retainedOffset = 0;
        while (timing.Remaining > 0)
        {
            uint runLength = timing.ReadUInt32();
            uint sampleDelta = timing.ReadUInt32();
            if (runLength == 0 || sampleDelta == 0)
            {
                throw new InvalidImageContentException("The image-sequence timing table contains a zero run or duration.");
            }

            // The validator rejects an excess run before it adds the duration of the run. Thus the total duration is at most TotalSampleCount * uint.MaxValue,
            // and the 64-bit sum cannot overflow.
            if (runLength > sampleCount - describedSamples)
            {
                throw new InvalidImageContentException("The image-sequence timing table describes more samples than the track contains.");
            }

            describedSamples += runLength;
            decodedDuration += (ulong)runLength * sampleDelta;
            int retainedRun = (int)Math.Min(runLength, (uint)(samples.Length - retainedOffset));
            for (int i = 0; i < retainedRun; i++)
            {
                samples[retainedOffset + i].Duration = sampleDelta;
            }

            retainedOffset += retainedRun;
        }

        if (describedSamples != sampleCount)
        {
            throw new InvalidImageContentException("The image-sequence timing table does not describe every sample.");
        }

        if (track.MediaDuration != 0 && track.MediaDuration != ulong.MaxValue && track.MediaDuration != decodedDuration)
        {
            throw new InvalidImageContentException("The image-sequence sample durations do not match the media-header duration.");
        }

        // The sample-to-chunk runs start at chunk 1, increase, and map every sample to a chunk.
        uint chunkCount = (uint)(table.ChunkOffsets.Length / table.ChunkOffsetSize);
        HeifSpanReader runs = new(table.SampleToChunk, "sample-to-chunk");
        if ((uint)(table.SampleToChunk.Length / 12) > chunkCount)
        {
            throw new InvalidImageContentException("The sample-to-chunk table has more runs than the track has chunks.");
        }

        uint previousFirstChunk = 0;
        uint previousSamplesPerChunk = 0;
        describedSamples = 0;
        while (runs.Remaining > 0)
        {
            uint firstChunk = runs.ReadUInt32();
            uint samplesPerChunk = runs.ReadUInt32();
            uint sampleDescriptionIndex = runs.ReadUInt32();
            if ((previousFirstChunk == 0 && firstChunk != 1) || firstChunk <= previousFirstChunk || firstChunk > chunkCount
                || samplesPerChunk == 0 || sampleDescriptionIndex != 1)
            {
                throw new InvalidImageContentException("The sample-to-chunk table contains an invalid run.");
            }

            // The product of two 32-bit values fits in 64 bits. The validator rejects a run that maps more samples than the track has before it adds the run to
            // the sum. Thus the sum stays at or below the 32-bit sample count.
            ulong runSamples = (ulong)(firstChunk - previousFirstChunk) * previousSamplesPerChunk;
            if (runSamples > sampleCount - describedSamples)
            {
                throw new InvalidImageContentException("The sample-to-chunk table maps more samples than the track contains.");
            }

            describedSamples += runSamples;
            previousFirstChunk = firstChunk;
            previousSamplesPerChunk = samplesPerChunk;
        }

        describedSamples += (ulong)(chunkCount + 1U - previousFirstChunk) * previousSamplesPerChunk;
        if (describedSamples != sampleCount)
        {
            throw new InvalidImageContentException("The sample-to-chunk table does not map every declared sample.");
        }

        LocateSamples(table, samples, fileLength);
        if (table.SyncSamples.IsEmpty)
        {
            // ISO/IEC 14496-12, section 8.6.2: if the sync-sample box is not present, every sample is a sync sample.
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i].IsSync = true;
            }

            return;
        }

        HeifSpanReader sync = new(table.SyncSamples, "sync samples");
        uint previousSample = 0;
        while (sync.Remaining > 0)
        {
            uint sampleNumber = sync.ReadUInt32();
            if (sampleNumber <= previousSample || sampleNumber > sampleCount)
            {
                throw new InvalidImageContentException("The sync-sample table contains an invalid sample number.");
            }

            if (sampleNumber <= samples.Length)
            {
                samples[(int)sampleNumber - 1].IsSync = true;
            }

            previousSample = sampleNumber;
        }

        if (!samples[0].IsSync)
        {
            throw new InvalidImageContentException("The first retained image-sequence sample is not a random-access sample.");
        }
    }

    /// <summary>
    /// Validates every stored sample size and gives the retained samples their lengths.
    /// </summary>
    /// <param name="table">The sample table fields.</param>
    /// <param name="samples">The retained sample descriptors.</param>
    private static void ValidateSampleSizes(in SampleTable table, HeifSequenceSample[] samples)
    {
        uint sampleCount = table.SampleCount;
        if (table.ConstantSampleSize != 0)
        {
            int size = ValidateSampleSize(table.ConstantSampleSize);
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i].Length = size;
            }

            return;
        }

        HeifSpanReader reader = new(table.SampleSizes, "sample sizes");
        for (uint i = 0; i < sampleCount; i++)
        {
            uint size;
            if (table.SampleSizeBits == 4)
            {
                // Two 4-bit sizes share one byte. The first size is in the high nibble.
                byte packed = reader.ReadUInt8();
                int firstSize = ValidateSampleSize((uint)(packed >> 4));
                if (i < samples.Length)
                {
                    samples[(int)i].Length = firstSize;
                }

                i++;
                if (i >= sampleCount)
                {
                    if ((packed & 15) != 0)
                    {
                        throw new InvalidImageContentException("The compact sample-size table has nonzero padding bits.");
                    }

                    break;
                }

                size = (uint)(packed & 15);
            }
            else
            {
                size = (uint)reader.ReadUInt(table.SampleSizeBits / 8);
            }

            int validatedSize = ValidateSampleSize(size);
            if (i < samples.Length)
            {
                samples[(int)i].Length = validatedSize;
            }
        }
    }

    /// <summary>
    /// Resolves the file offset of each retained sample from the chunk offsets and the sample-to-chunk runs.
    /// </summary>
    /// <param name="table">The validated sample table fields.</param>
    /// <param name="samples">The retained sample descriptors.</param>
    /// <param name="fileLength">The number of file bytes from the start of the HEIF file to the end of the stream.</param>
    private static void LocateSamples(in SampleTable table, Span<HeifSequenceSample> samples, long fileLength)
    {
        HeifSpanReader chunks = new(table.ChunkOffsets, "chunk offsets");
        HeifSpanReader runs = new(table.SampleToChunk, "sample-to-chunk");

        // The validator validated that the runs start at chunk 1, increase, and stay in the chunk count. Each run stores first_chunk, samples_per_chunk and
        // sample_description_index.
        runs.Skip(4);
        uint samplesPerChunk = runs.ReadUInt32();
        runs.Skip(4);
        uint nextFirstChunk = runs.Remaining > 0 ? runs.ReadUInt32() : 0;
        int retainedSample = 0;
        for (uint chunkNumber = 1; retainedSample < samples.Length; chunkNumber++)
        {
            if (chunkNumber == nextFirstChunk)
            {
                samplesPerChunk = runs.ReadUInt32();
                runs.Skip(4);
                nextFirstChunk = runs.Remaining > 0 ? runs.ReadUInt32() : 0;
            }

            // A chunk that starts after the end of the file holds no bytes. The method keeps every sample offset at or before the end of the file. Thus the sum
            // of an offset and a 32-bit size cannot overflow.
            ulong sampleOffset = Math.Min(chunks.ReadUInt(table.ChunkOffsetSize), (ulong)fileLength);
            for (uint sampleInChunk = 0; sampleInChunk < samplesPerChunk && retainedSample < samples.Length; sampleInChunk++)
            {
                ref HeifSequenceSample sample = ref samples[retainedSample++];
                ulong sampleEnd = sampleOffset + (uint)sample.Length;
                if (sampleEnd > (ulong)fileLength)
                {
                    // The file ends inside or before this sample. The sample keeps only the bytes that the file holds. The decoder reports the truncated sample
                    // through the image data mode.
                    sampleEnd = (ulong)fileLength;
                    sample.Length = (int)(sampleEnd - sampleOffset);
                    sample.IsTruncated = true;
                }

                sample.Offset = (long)sampleOffset;
                sampleOffset = sampleEnd;
            }
        }
    }

    /// <summary>
    /// Parses the single visual sample entry and its codec and coding-constraint children.
    /// </summary>
    /// <param name="stream">The stream positioned at the sample-description payload.</param>
    /// <param name="boxLength">The validated sample-description payload length.</param>
    /// <param name="track">The selected track receiving its codec configuration.</param>
    /// <param name="headerBuffer">The parser-owned box header buffer.</param>
    private void ParseSampleDescription(Stream stream, long boxLength, HeifSequenceTrack track, Span<byte> headerBuffer)
    {
        long descriptionEnd = stream.Position + boxLength;

        // ISO/IEC 14496-12, section 8.5.2: the full-box header and the entry count precede the sample entries.
        Span<byte> descriptionHeader = headerBuffer[..(int)Math.Min(boxLength, 8)];
        HeifBoxReader.ReadExactly(stream, descriptionHeader, "The sample description box is truncated.");
        HeifSpanReader descriptionReader = new(descriptionHeader, "sample description");
        descriptionReader.ReadFullBoxHeader(1, 0, out _);
        uint entryCount = descriptionReader.ReadUInt32();
        descriptionReader.EnsureEnd();
        if (entryCount != 1)
        {
            throw new InvalidImageContentException("A HEIF image-sequence track must contain exactly one sample description.");
        }

        long entryLength = HeifBoxReader.ReadHeader(stream, descriptionEnd, headerBuffer, out Heif4CharCode entryType, false);
        if (entryType != Heif4CharCode.Av01 || entryLength < VisualSampleEntryLength)
        {
            throw new InvalidImageContentException($"The image-sequence sample entry '{entryType}' is unsupported or truncated.");
        }

        long entryEnd = stream.Position + entryLength;
        Span<byte> sampleEntry = stackalloc byte[VisualSampleEntryLength];
        HeifBoxReader.ReadExactly(stream, sampleEntry, "The visual sample entry is truncated.");

        // The 6 reserved bytes and the data reference index of the sample entry precede the pre_defined and reserved fields, the coded width and the coded
        // height of the visual sample entry. The parser does not use the other fields.
        HeifSpanReader entryReader = new(sampleEntry, "visual sample entry");
        entryReader.Skip(6);
        ushort dataReferenceIndex = entryReader.ReadUInt16();
        entryReader.Skip(16);
        int codedWidth = entryReader.ReadUInt16();
        int codedHeight = entryReader.ReadUInt16();
        if (dataReferenceIndex != 1)
        {
            throw new InvalidImageContentException("The image-sequence sample entry uses a nonlocal data reference.");
        }

        if (codedWidth == 0 || codedHeight == 0)
        {
            throw new InvalidImageContentException("The image-sequence sample entry has zero coded dimensions.");
        }

        track.CodecType = entryType;
        track.CodedWidth = codedWidth;
        track.CodedHeight = codedHeight;
        bool codingConstraintsSeen = false;
        bool auxiliaryTypeSeen = false;
        while (stream.Position < entryEnd)
        {
            long childLength = HeifBoxReader.ReadHeader(stream, entryEnd, headerBuffer, out Heif4CharCode childType, false);
            long childStart = stream.Position;
            switch (childType)
            {
                case Heif4CharCode.Av1C:
                    if (track.Av1CodecConfiguration is not null)
                    {
                        throw new InvalidImageContentException("The AV1 image-sequence sample entry has duplicate codec configurations.");
                    }

                    using (IMemoryOwner<byte> configuration = this.boxReader.ReadPayload(stream, childLength))
                    {
                        track.Av1CodecConfiguration = new Av1CodecConfiguration(configuration.GetSpan(), this.options);
                    }

                    break;
                case Heif4CharCode.Ccst:
                    if (codingConstraintsSeen)
                    {
                        throw new InvalidImageContentException("The image-sequence sample entry has duplicate coding constraints.");
                    }

                    using (IMemoryOwner<byte> constraints = this.boxReader.ReadPayload(stream, childLength))
                    {
                        ParseCodingConstraints(constraints.GetSpan(), track);
                    }

                    codingConstraintsSeen = true;
                    break;
                case Heif4CharCode.Auxi:
                    if (auxiliaryTypeSeen)
                    {
                        throw new InvalidImageContentException("The image-sequence sample entry has duplicate auxiliary types.");
                    }

                    using (IMemoryOwner<byte> auxiliaryType = this.boxReader.ReadPayload(stream, childLength))
                    {
                        track.IsAlpha = ParseAuxiliaryType(auxiliaryType.GetSpan());
                    }

                    auxiliaryTypeSeen = true;
                    break;
                case Heif4CharCode.Colr:
                    this.ParseTrackColorInformation(stream, childLength, track, headerBuffer);
                    break;
                case Heif4CharCode.Pasp:
                case Heif4CharCode.Clli:
                case Heif4CharCode.Mdcv:
                case Heif4CharCode.Cclv:
                case Heif4CharCode.Amve:
                case Heif4CharCode.Reve:
                case Heif4CharCode.Ndwt:
                case Heif4CharCode.Clap:
                case Heif4CharCode.Irot:
                case Heif4CharCode.Imir:
                    // Except for the clean aperture, rotation and mirror properties, these properties change only the exposed image metadata.
                    bool isMetadata = childType is not Heif4CharCode.Clap and not Heif4CharCode.Irot and not Heif4CharCode.Imir;
                    if (!isMetadata || !this.options.SkipMetadata)
                    {
                        using IMemoryOwner<byte> property = this.boxReader.ReadPayload(stream, childLength);
                        this.ParseTrackImageProperty(childType, property.GetSpan(), track);
                    }

                    break;
            }

            stream.Position = childStart + childLength;
        }

        if (stream.Position != descriptionEnd || track.Av1CodecConfiguration is null || !codingConstraintsSeen)
        {
            throw new InvalidImageContentException("The image-sequence sample description is incomplete or has trailing entries.");
        }
    }

    /// <summary>
    /// Parses one presentation, color, or HDR property carried by a visual sample entry.
    /// </summary>
    /// <remarks>
    /// The track properties obey the same rules as the item properties. A damaged metadata property is an ancillary error. A damaged presentation property
    /// causes an exception. <see cref="HeifPropertyParser.AssociateOnce"/> reports a duplicate property.
    /// </remarks>
    /// <param name="boxType">The registered image property type.</param>
    /// <param name="data">The property payload.</param>
    /// <param name="track">The selected image track receiving the property.</param>
    private void ParseTrackImageProperty(Heif4CharCode boxType, ReadOnlySpan<byte> data, HeifSequenceTrack track)
    {
        const string owner = "track";
        SegmentIntegrityHandling handling = this.options.SegmentIntegrityHandling;
        switch (boxType)
        {
            case Heif4CharCode.Pasp:
                HeifPixelAspectRatio? pixelAspectRatio = HeifPropertyParser.ParsePixelAspectRatio(data, handling);
                if (pixelAspectRatio is not null && HeifPropertyParser.AssociateOnce(track.PixelAspectRatio, handling, owner, track.Id, boxType, true))
                {
                    track.PixelAspectRatio = pixelAspectRatio;
                }

                break;
            case Heif4CharCode.Clli:
                HeifContentLightLevel? contentLightLevel = HeifPropertyParser.ParseContentLightLevel(data, handling);
                if (contentLightLevel is not null && HeifPropertyParser.AssociateOnce(track.ContentLightLevel, handling, owner, track.Id, boxType, true))
                {
                    track.ContentLightLevel = contentLightLevel;
                }

                break;
            case Heif4CharCode.Mdcv:
                HeifMasteringDisplayColorVolume? masteringDisplayColorVolume = HeifPropertyParser.ParseMasteringDisplayColorVolume(data, handling);
                if (masteringDisplayColorVolume is not null
                    && HeifPropertyParser.AssociateOnce(track.MasteringDisplayColorVolume, handling, owner, track.Id, boxType, true))
                {
                    track.MasteringDisplayColorVolume = masteringDisplayColorVolume;
                }

                break;
            case Heif4CharCode.Cclv:
                HeifContentColorVolume? contentColorVolume = HeifPropertyParser.ParseContentColorVolume(data, handling);
                if (contentColorVolume is not null && HeifPropertyParser.AssociateOnce(track.ContentColorVolume, handling, owner, track.Id, boxType, true))
                {
                    track.ContentColorVolume = contentColorVolume;
                }

                break;
            case Heif4CharCode.Amve:
                HeifAmbientViewingEnvironment? ambientViewingEnvironment = HeifPropertyParser.ParseAmbientViewingEnvironment(data, handling);
                if (ambientViewingEnvironment is not null
                    && HeifPropertyParser.AssociateOnce(track.AmbientViewingEnvironment, handling, owner, track.Id, boxType, true))
                {
                    track.AmbientViewingEnvironment = ambientViewingEnvironment;
                }

                break;
            case Heif4CharCode.Reve:
                HeifReferenceViewingEnvironment? referenceViewingEnvironment = HeifPropertyParser.ParseReferenceViewingEnvironment(data, handling);
                if (referenceViewingEnvironment is not null
                    && HeifPropertyParser.AssociateOnce(track.ReferenceViewingEnvironment, handling, owner, track.Id, boxType, true))
                {
                    track.ReferenceViewingEnvironment = referenceViewingEnvironment;
                }

                break;
            case Heif4CharCode.Ndwt:
                HeifNominalDiffuseWhite? nominalDiffuseWhite = HeifPropertyParser.ParseNominalDiffuseWhite(data, handling);
                if (nominalDiffuseWhite is not null && HeifPropertyParser.AssociateOnce(track.NominalDiffuseWhite, handling, owner, track.Id, boxType, true))
                {
                    track.NominalDiffuseWhite = nominalDiffuseWhite;
                }

                break;
            case Heif4CharCode.Clap:
                // ToRectangle throws when the clean-aperture fractions do not give an integer rectangle inside the coded size.
                HeifCleanAperture cleanAperture = HeifPropertyParser.ParseCleanAperture(data);
                _ = cleanAperture.ToRectangle(new Size(track.CodedWidth, track.CodedHeight));
                if (HeifPropertyParser.AssociateOnce(track.CleanAperture, handling, owner, track.Id, boxType, false))
                {
                    track.CleanAperture = cleanAperture;
                }

                break;
            case Heif4CharCode.Irot:
                byte rotationAngle = HeifPropertyParser.ParseRotation(data);
                if (HeifPropertyParser.AssociateOnce(track.RotationAngle, handling, owner, track.Id, boxType, false))
                {
                    track.RotationAngle = rotationAngle;
                }

                break;
            case Heif4CharCode.Imir:
                byte mirrorAxis = HeifPropertyParser.ParseMirrorAxis(data);
                if (HeifPropertyParser.AssociateOnce(track.MirrorAxis, handling, owner, track.Id, boxType, false))
                {
                    track.MirrorAxis = mirrorAxis;
                }

                break;
        }
    }

    /// <summary>
    /// Parses one ICC or CICP color-information property from a visual sample entry.
    /// </summary>
    /// <param name="stream">The stream positioned at the color-information payload.</param>
    /// <param name="boxLength">The validated color-information payload length.</param>
    /// <param name="track">The selected image track receiving the color description.</param>
    /// <param name="headerBuffer">The parser-owned box header buffer.</param>
    private void ParseTrackColorInformation(Stream stream, long boxLength, HeifSequenceTrack track, Span<byte> headerBuffer)
    {
        Span<byte> profileTypeBuffer = headerBuffer[..(int)Math.Min(boxLength, 4)];
        HeifBoxReader.ReadExactly(stream, profileTypeBuffer, "The HEIF color-information property is truncated.");
        HeifSpanReader profileTypeReader = new(profileTypeBuffer, "color information");
        Heif4CharCode profileType = (Heif4CharCode)profileTypeReader.ReadUInt32();
        profileTypeReader.EnsureEnd();

        SegmentIntegrityHandling handling = this.options.SegmentIntegrityHandling;
        if (profileType == Heif4CharCode.Nclx)
        {
            using IMemoryOwner<byte> payload = this.boxReader.ReadPayload(stream, boxLength - 4);
            CicpProfile cicpProfile = HeifPropertyParser.ParseCicpProfile(payload.GetSpan());
            if (HeifPropertyParser.AssociateOnce(track.CicpProfile, handling, "track", track.Id, Heif4CharCode.Colr, false))
            {
                track.CicpProfile = cicpProfile;
            }
        }
        else if (profileType is Heif4CharCode.RICC or Heif4CharCode.Prof)
        {
            // The ICC profile sets the sample colors. Thus the parser reads it also when the decoder skips optional metadata.
            IccProfile? iccProfile = HeifPropertyParser.ParseIccProfile(stream, boxLength - 4, handling);
            if (iccProfile is not null && HeifPropertyParser.AssociateOnce(track.IccProfile, handling, "track", track.Id, Heif4CharCode.Colr, true))
            {
                track.IccProfile = iccProfile;
            }
        }
    }

    /// <summary>
    /// Parses coding constraints that bound inter-picture references for an image sequence.
    /// </summary>
    /// <param name="payload">The coding-constraints full-box payload.</param>
    /// <param name="track">The selected track receiving coding constraints.</param>
    private static void ParseCodingConstraints(ReadOnlySpan<byte> payload, HeifSequenceTrack track)
    {
        // ISO/IEC 23008-12, section 7.2.3: one byte of flags and four bits of the maximum reference count, then 26 reserved bits.
        HeifSpanReader reader = new(payload, "coding constraints");
        reader.ReadFullBoxHeader(0, 0, out _);
        uint constraints = reader.ReadUInt32();
        reader.EnsureEnd();
        if ((constraints & 0x03FFFFFF) != 0)
        {
            throw new InvalidImageContentException("The image-sequence coding constraints contain nonzero reserved bits.");
        }

        track.AllReferencePicturesIntra = (constraints & 0x80000000) != 0;
        track.IntraPicturePredictionUsed = (constraints & 0x40000000) != 0;
        track.MaximumReferencesPerPicture = (byte)((constraints >> 26) & 15);
    }

    /// <summary>
    /// Determines whether an auxiliary-track type identifies an alpha image sequence.
    /// </summary>
    /// <param name="payload">The auxiliary-type full-box payload.</param>
    /// <returns><see langword="true"/> when the payload contains the registered HEIF alpha URN.</returns>
    private static bool ParseAuxiliaryType(ReadOnlySpan<byte> payload)
    {
        HeifSpanReader reader = new(payload, "auxiliary type");
        reader.ReadFullBoxHeader(0, 0, out _);
        ReadOnlySpan<byte> type = reader.RemainingData;
        reader.EnsureNotTruncated();
        if (type.IsEmpty || type[^1] != 0)
        {
            throw new InvalidImageContentException("The image-sequence auxiliary type is not null terminated.");
        }

        return type[..^1].SequenceEqual("urn:mpeg:mpegB:cicp:systems:auxiliary:alpha"u8);
    }

    /// <summary>
    /// Finds the direct-reference sample group of a sample table and resolves it into compact zero-based sample indices.
    /// </summary>
    /// <remarks>
    /// A sample table can hold sample group boxes of other grouping types. Thus the generic child index does not apply. This method selects the two boxes of
    /// the 'refs' grouping type. It rejects a duplicate of either box.
    /// </remarks>
    /// <param name="stream">The seekable source stream.</param>
    /// <param name="tableStart">The absolute offset of the sample-table payload.</param>
    /// <param name="tableLength">The sample-table payload length.</param>
    /// <param name="track">The selected image track receiving its dependency graph.</param>
    /// <param name="headerBuffer">The parser-owned box header buffer.</param>
    private void ParseDirectReferences(Stream stream, long tableStart, long tableLength, HeifSequenceTrack track, Span<byte> headerBuffer)
    {
        HeifBoxRange descriptionsBox = default;
        HeifBoxRange sampleMapBox = default;
        long tableEnd = tableStart + tableLength;
        stream.Position = tableStart;
        while (stream.Position < tableEnd)
        {
            long childLength = HeifBoxReader.ReadHeader(stream, tableEnd, headerBuffer, out Heif4CharCode childType, false);
            long childStart = stream.Position;
            if (childType is Heif4CharCode.Sgpd or Heif4CharCode.Sbgp)
            {
                // Both boxes start with the full-box header and the grouping type.
                Span<byte> prefix = headerBuffer[..(int)Math.Min(childLength, 8)];
                HeifBoxReader.ReadExactly(stream, prefix, "The sample group box is truncated.");
                HeifSpanReader prefixReader = new(prefix, "sample group");
                prefixReader.Skip(4);
                Heif4CharCode groupingType = (Heif4CharCode)prefixReader.ReadUInt32();
                prefixReader.EnsureEnd();
                if (groupingType == Heif4CharCode.Refs)
                {
                    ref HeifBoxRange box = ref childType == Heif4CharCode.Sgpd ? ref descriptionsBox : ref sampleMapBox;
                    if (box.IsPresent)
                    {
                        throw new InvalidImageContentException($"The sample table box contains duplicate '{childType}' boxes.");
                    }

                    box = new HeifBoxRange(childStart, childLength, childType);
                }
            }

            stream.Position = childStart + childLength;
        }

        if (descriptionsBox.IsPresent != sampleMapBox.IsPresent)
        {
            throw new InvalidImageContentException("The direct-reference sample group is missing its description or sample map.");
        }

        if (!descriptionsBox.IsPresent)
        {
            return;
        }

        using IMemoryOwner<byte> descriptions = this.boxReader.ReadPayload(stream, descriptionsBox);
        using IMemoryOwner<byte> sampleMap = this.boxReader.ReadPayload(stream, sampleMapBox);
        using IMemoryOwner<SampleGroupAssignment> assignmentOwner = this.allocator.Allocate<SampleGroupAssignment>(track.Samples.Length);
        Span<SampleGroupAssignment> assignments = assignmentOwner.GetSpan()[..track.Samples.Length];
        uint greatestGroupIndex = ParseSampleToGroup(sampleMap.GetSpan(), track, assignments);

        // Sorting the retained value-type assignments lets each group description be applied in one sequential pass.
        // This avoids a dictionary and prevents attacker-controlled group counts from causing quadratic lookup work.
        assignments.Sort();
        int directReferenceCount = ParseDirectReferenceDescriptions(descriptions.GetSpan(), greatestGroupIndex, track, assignments, [], false);

        using IMemoryOwner<SampleIdIndexEntry> sampleIdOwner = this.allocator.Allocate<SampleIdIndexEntry>(track.Samples.Length);
        Span<SampleIdIndexEntry> sampleIds = sampleIdOwner.GetSpan()[..track.Samples.Length];
        int sampleIdCount = 0;
        Span<HeifSequenceSample> samples = track.Samples;
        for (int i = 0; i < samples.Length; i++)
        {
            uint sampleId = samples[i].SampleId;
            if (sampleId != 0)
            {
                sampleIds[sampleIdCount++] = new SampleIdIndexEntry(sampleId, i);
            }
        }

        sampleIds = sampleIds[..sampleIdCount];
        sampleIds.Sort();
        for (int i = 1; i < sampleIds.Length; i++)
        {
            if (sampleIds[i - 1].Id == sampleIds[i].Id)
            {
                throw new InvalidImageContentException("The direct-reference sample group contains a duplicate positive sample identifier.");
            }
        }

        if (directReferenceCount == 0)
        {
            return;
        }

        // The second pass reads the same buffered payload. Thus its entries agree with the entries of the sizing pass.
        track.DirectReferenceSampleIndices = new int[directReferenceCount];
        _ = ParseDirectReferenceDescriptions(descriptions.GetSpan(), greatestGroupIndex, track, assignments, sampleIds, true);

        if (track.AllReferencePicturesIntra)
        {
            ReadOnlySpan<int> referenceIndices = track.DirectReferenceSampleIndices;
            for (int i = 0; i < referenceIndices.Length; i++)
            {
                if (samples[referenceIndices[i]].DirectReferenceCount != 0)
                {
                    throw new InvalidImageContentException("The direct-reference sample group contradicts its all-reference-pictures-intra constraint.");
                }
            }
        }
    }

    /// <summary>
    /// Expands the run-length sample-to-group map only for samples retained by the decoder.
    /// </summary>
    /// <param name="payload">The sample-to-group full-box payload of the 'refs' grouping type.</param>
    /// <param name="track">The selected track whose complete sample count is validated.</param>
    /// <param name="assignments">The exact retained assignment span.</param>
    /// <returns>The greatest group-description index used by any declared sample.</returns>
    private static uint ParseSampleToGroup(ReadOnlySpan<byte> payload, HeifSequenceTrack track, Span<SampleGroupAssignment> assignments)
    {
        // ISO/IEC 14496-12, section 8.9.2: the grouping type, the grouping-type parameter of version 1, the entry count and the runs of sample_count and
        // group_description_index.
        HeifSpanReader reader = new(payload, "sample-to-group");
        byte version = reader.ReadFullBoxHeader(1, 0, out _);
        reader.Skip(4);
        if (version == 1 && reader.ReadUInt32() != 0)
        {
            throw new InvalidImageContentException("The direct-reference sample group has a nonzero grouping-type parameter.");
        }

        uint entryCount = reader.ReadEntryCount(4, 8);
        ulong describedSamples = 0;
        int retainedOffset = 0;
        uint greatestGroupIndex = 0;
        for (uint i = 0; i < entryCount; i++)
        {
            uint sampleCount = reader.ReadUInt32();
            uint groupDescriptionIndex = reader.ReadUInt32();
            if (sampleCount == 0)
            {
                throw new InvalidImageContentException("The direct-reference sample map contains a zero-length run.");
            }

            // The parser rejects a run that describes more samples than the track has before it adds the run to the sum. Thus the sum cannot overflow.
            if (sampleCount > track.TotalSampleCount - describedSamples)
            {
                throw new InvalidImageContentException("The direct-reference sample map describes more samples than the track contains.");
            }

            describedSamples += sampleCount;
            greatestGroupIndex = Math.Max(greatestGroupIndex, groupDescriptionIndex);
            int retainedRun = (int)Math.Min(sampleCount, (uint)(assignments.Length - retainedOffset));
            for (int j = 0; j < retainedRun; j++)
            {
                assignments[retainedOffset + j] = new SampleGroupAssignment(groupDescriptionIndex, retainedOffset + j);
            }

            retainedOffset += retainedRun;
        }

        reader.EnsureEnd();
        if (entryCount == 0 || describedSamples != track.TotalSampleCount || retainedOffset != assignments.Length)
        {
            throw new InvalidImageContentException("The direct-reference sample map does not describe every sample.");
        }

        return greatestGroupIndex;
    }

    /// <summary>
    /// Parses direct-reference descriptions, first sizing and then resolving the retained dependency graph.
    /// </summary>
    /// <param name="payload">The sample-group-description full-box payload of the 'refs' grouping type.</param>
    /// <param name="greatestGroupIndex">The greatest description index used by the complete sample map.</param>
    /// <param name="track">The selected track receiving sample identifiers and dependency indices.</param>
    /// <param name="assignments">The retained sample assignments sorted by group-description index.</param>
    /// <param name="sampleIds">The sorted positive sample identifiers, or an empty span during the sizing pass.</param>
    /// <param name="resolveReferences">Whether this pass resolves reference identifiers into compact sample indices.</param>
    /// <returns>The exact number of retained direct-reference indices.</returns>
    private static int ParseDirectReferenceDescriptions(
        ReadOnlySpan<byte> payload,
        uint greatestGroupIndex,
        HeifSequenceTrack track,
        ReadOnlySpan<SampleGroupAssignment> assignments,
        ReadOnlySpan<SampleIdIndexEntry> sampleIds,
        bool resolveReferences)
    {
        // ISO/IEC 14496-12, section 8.9.3: version 1 adds default_length. Version 2 also adds default_group_description_index. An entry of the 'refs' grouping
        // type holds a sample identifier, a reference count, and the referenced sample identifiers.
        HeifSpanReader reader = new(payload, "sample-group descriptions");
        byte version = reader.ReadFullBoxHeader(2, 0, out _);
        if (version == 0)
        {
            throw new InvalidImageContentException("The direct-reference sample-group-description box has an unsupported version.");
        }

        reader.Skip(4);
        uint defaultLength = reader.ReadUInt32();
        uint defaultGroupIndex = version == 2 ? reader.ReadUInt32() : 0;
        uint entryCount = reader.ReadEntryCount(4, defaultLength == 0 ? 9 : 5);
        if (greatestGroupIndex > entryCount || defaultGroupIndex > entryCount)
        {
            throw new InvalidImageContentException("The direct-reference sample map uses an undefined group-description index.");
        }

        int assignmentOffset = 0;
        int directReferenceCount = 0;
        while (assignmentOffset < assignments.Length && assignments[assignmentOffset].GroupDescriptionIndex == 0)
        {
            assignmentOffset++;
        }

        for (uint entry = 0; entry < entryCount; entry++)
        {
            uint descriptionIndex = entry + 1;
            uint descriptionLength = defaultLength == 0 ? reader.ReadUInt32() : defaultLength;
            uint sampleId = reader.ReadUInt32();
            byte referenceCount = reader.ReadUInt8();
            if (descriptionLength != 5U + ((uint)referenceCount * 4U))
            {
                throw new InvalidImageContentException("The direct-reference sample-group entry has an invalid length.");
            }

            int firstAssignment = assignmentOffset;
            while (assignmentOffset < assignments.Length && assignments[assignmentOffset].GroupDescriptionIndex == descriptionIndex)
            {
                if (!resolveReferences)
                {
                    ref HeifSequenceSample sample = ref track.Samples[assignments[assignmentOffset].SampleIndex];
                    if (referenceCount > track.MaximumReferencesPerPicture || directReferenceCount > int.MaxValue - referenceCount)
                    {
                        throw new InvalidImageContentException("The direct-reference sample group exceeds its coding constraints or supported size.");
                    }

                    sample.SampleId = sampleId;
                    sample.DirectReferenceOffset = directReferenceCount;
                    sample.DirectReferenceCount = referenceCount;
                    directReferenceCount += referenceCount;
                }

                assignmentOffset++;
            }

            for (int reference = 0; reference < referenceCount; reference++)
            {
                uint referenceSampleId = reader.ReadUInt32();
                if (referenceSampleId == 0)
                {
                    throw new InvalidImageContentException("The direct-reference sample group contains identifier zero in a reference list.");
                }

                if (resolveReferences)
                {
                    // The entries compare by identifier only, so the sample index of the search key has no effect. The generic comparable overload
                    // does not box the key.
                    int found = sampleIds.BinarySearch(new SampleIdIndexEntry(referenceSampleId, 0));
                    if (found < 0)
                    {
                        throw new InvalidImageContentException("A direct-reference sample identifier does not name a retained sample.");
                    }

                    int referencedSampleIndex = sampleIds[found].SampleIndex;
                    for (int assignment = firstAssignment; assignment < assignmentOffset; assignment++)
                    {
                        int sampleIndex = assignments[assignment].SampleIndex;
                        HeifSequenceSample sample = track.Samples[sampleIndex];
                        if (sample.IsSync || referencedSampleIndex >= sampleIndex)
                        {
                            throw new InvalidImageContentException("A direct-reference sample is not earlier in decode order or is attached to a sync sample.");
                        }

                        track.DirectReferenceSampleIndices[sample.DirectReferenceOffset + reference] = referencedSampleIndex;
                    }
                }
            }
        }

        reader.EnsureEnd();
        if (assignmentOffset != assignments.Length)
        {
            throw new InvalidImageContentException("The direct-reference sample-group descriptions do not cover the retained sample map.");
        }

        return directReferenceCount;
    }

    /// <summary>
    /// Parses the single normal-rate edit list used to signal image-sequence repetition.
    /// </summary>
    /// <param name="stream">The stream positioned at the edit-container payload.</param>
    /// <param name="boxLength">The validated edit-container payload length.</param>
    /// <param name="track">The selected track receiving repetition behavior.</param>
    /// <param name="headerBuffer">The parser-owned box header buffer.</param>
    private void ParseEdit(Stream stream, long boxLength, HeifSequenceTrack track, Span<byte> headerBuffer)
    {
        Span<HeifBoxRange> children = stackalloc HeifBoxRange[1];
        HeifBoxReader.IndexChildren(stream, boxLength, headerBuffer, [Heif4CharCode.Elst], children, 1, "edit");
        using IMemoryOwner<byte> payload = this.boxReader.ReadPayload(stream, children[0]);

        // ISO/IEC 14496-12, section 8.6.6: flag bit 0 repeats the edit list. Version 1 widens the segment duration and the media time from 32 to 64 bits. The
        // media rate integer and fraction follow them.
        HeifSpanReader reader = new(payload.GetSpan(), "edit list");
        byte version = reader.ReadFullBoxHeader(1, 1, out uint flags);
        uint entryCount = reader.ReadUInt32();
        ulong segmentDuration = reader.ReadUInt(version == 1 ? 8 : 4);
        long mediaTime = version == 1 ? reader.ReadInt64() : reader.ReadInt32();
        short mediaRateInteger = reader.ReadInt16();
        short mediaRateFraction = reader.ReadInt16();
        reader.EnsureEnd();
        if (entryCount != 1)
        {
            throw new InvalidImageContentException("The image-sequence edit list has unsupported entries.");
        }

        if (segmentDuration == 0 || mediaTime != 0 || mediaRateInteger != 1 || mediaRateFraction != 0)
        {
            throw new InvalidImageContentException("The image-sequence edit list requires unsupported splicing or playback rate behavior.");
        }

        if ((flags & 1) == 0)
        {
            track.RepeatCount = 1;
        }
        else if (track.TrackDuration == ulong.MaxValue)
        {
            track.RepeatCount = 0;
        }
        else
        {
            if (track.TrackDuration == 0)
            {
                throw new InvalidImageContentException("A repeating image-sequence track has zero duration.");
            }

            ulong plays = (track.TrackDuration / segmentDuration) + (track.TrackDuration % segmentDuration == 0 ? 0UL : 1UL);

            // The public metadata uses zero for indefinite repetition. Preserve extremely large finite edit counts as
            // indefinite rather than wrapping the observable ushort play count.
            track.RepeatCount = plays is 0 or > ushort.MaxValue ? (ushort)0 : (ushort)plays;
        }

        track.HasEditList = true;
    }

    /// <summary>
    /// Validates that a linked alpha track can be matched frame-for-frame with its master color track.
    /// </summary>
    /// <remarks>
    /// Alpha is optional image data. The image data mode reports an alpha track that does not match the color track. If the mode ignores the error, the decoder
    /// decodes the sequence without alpha.
    /// </remarks>
    /// <param name="colorTrack">The selected master color track.</param>
    /// <param name="alphaTrack">The optional linked alpha track.</param>
    /// <returns><see langword="true"/> when the alpha track is valid or absent. <see langword="false"/> when the mode ignored an alpha track error.</returns>
    private bool ValidateAlphaTrack(HeifSequenceTrack colorTrack, HeifSequenceTrack? alphaTrack)
    {
        string? error = null;
        if (colorTrack.PremultipliedByTrackId != 0 && (alphaTrack is null || colorTrack.PremultipliedByTrackId != alphaTrack.Id))
        {
            error = "The color image-sequence track references an unrelated premultiplication track.";
        }
        else if (alphaTrack is not null)
        {
            // The decoder reads the alpha plane from the luma plane of a monochrome AV1 sequence. The parser accepts only an AV1 sample entry with a codec
            // configuration. The parser retains min(TotalSampleCount, MaxFrames) samples of each track. Thus equal totals give equal retained counts.
            if (!alphaTrack.Av1CodecConfiguration!.IsMonochrome)
            {
                error = "An AV1 alpha image-sequence track must be encoded as monochrome.";
            }
            else if (alphaTrack.TotalSampleCount != colorTrack.TotalSampleCount)
            {
                error = "The alpha and color image-sequence tracks contain different sample counts.";
            }

            for (int i = 0; error is null && i < colorTrack.Samples.Length; i++)
            {
                HeifSequenceSample colorSample = colorTrack.Samples[i];
                HeifSequenceSample alphaSample = alphaTrack.Samples[i];
                ulong colorDuration = (ulong)colorSample.Duration * alphaTrack.MediaTimescale;
                ulong alphaDuration = (ulong)alphaSample.Duration * colorTrack.MediaTimescale;
                if (colorDuration != alphaDuration)
                {
                    error = "The alpha and color image-sequence samples have different presentation durations.";
                }
            }
        }

        if (error is not null)
        {
            this.options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError(error);
            colorTrack.IsPremultiplied = false;
            return false;
        }

        if (alphaTrack is null)
        {
            return true;
        }

        colorTrack.IsPremultiplied = colorTrack.PremultipliedByTrackId != 0;
        bool alphaHasPresentationProperties = alphaTrack.CleanAperture is not null || alphaTrack.RotationAngle is not null || alphaTrack.MirrorAxis is not null;
        if (alphaHasPresentationProperties &&
            (!Nullable.Equals(colorTrack.CleanAperture, alphaTrack.CleanAperture) ||
             colorTrack.RotationAngle != alphaTrack.RotationAngle ||
             colorTrack.MirrorAxis != alphaTrack.MirrorAxis))
        {
            // For compatibility, an alpha track without transform properties is valid. If the alpha track declares a
            // transform, all of its transforms must match the color track. The decoder combines alpha and color before
            // it applies the transforms of the color track.
            throw new NotSupportedException("The alpha and color image-sequence tracks use different presentation transforms.");
        }

        return true;
    }

    /// <summary>
    /// Validates and narrows a sample size to the decoder's contiguous-buffer length type.
    /// </summary>
    /// <param name="size">The file-defined unsigned sample size.</param>
    /// <returns>The positive sample size as an <see cref="int"/>.</returns>
    private static int ValidateSampleSize(uint size)
    {
        if (size == 0 || size > int.MaxValue)
        {
            throw new InvalidImageContentException("An image-sequence sample has an unsupported size.");
        }

        return (int)size;
    }

    /// <summary>
    /// Identifies a selected track without retaining its codec or sample-table payloads.
    /// </summary>
    private struct TrackIdentity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TrackIdentity"/> struct.
        /// </summary>
        /// <param name="id">The file-defined track identifier.</param>
        /// <param name="isEnabled">Whether the track is enabled.</param>
        /// <param name="width">The displayed track width.</param>
        /// <param name="height">The displayed track height.</param>
        /// <param name="trackDuration">The track duration in movie-time-scale units.</param>
        public TrackIdentity(uint id, bool isEnabled, int width, int height, ulong trackDuration)
        {
            this.Id = id;
            this.IsEnabled = isEnabled;
            this.Width = width;
            this.Height = height;
            this.TrackDuration = trackDuration;
        }

        /// <summary>
        /// Gets the file-defined track identifier.
        /// </summary>
        public uint Id { get; }

        /// <summary>
        /// Gets a value indicating whether the track is enabled.
        /// </summary>
        public bool IsEnabled { get; }

        /// <summary>
        /// Gets the displayed track width.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the displayed track height.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the track duration in movie-time-scale units.
        /// </summary>
        public ulong TrackDuration { get; }

        /// <summary>
        /// Gets or sets the media handler type.
        /// </summary>
        public Heif4CharCode HandlerType { get; set; }

        /// <summary>
        /// Gets or sets the master track served by this auxiliary track.
        /// </summary>
        public uint AuxiliaryForTrackId { get; set; }

        /// <summary>
        /// Gets or sets the track identifier used by premultiplication signaling.
        /// </summary>
        public uint PremultipliedByTrackId { get; set; }

        /// <summary>
        /// Gets or sets the payload range of the edit box of the track, or an absent range.
        /// </summary>
        public HeifBoxRange EditBox { get; set; }

        /// <summary>
        /// Gets or sets the payload range of the metadata box of the track, or an absent range.
        /// </summary>
        public HeifBoxRange MetadataBox { get; set; }

        /// <summary>
        /// Gets or sets the payload range of the media-header box of the track, or an absent range.
        /// </summary>
        public HeifBoxRange MediaHeaderBox { get; set; }

        /// <summary>
        /// Gets or sets the payload range of the media-information box of the track, or an absent range.
        /// </summary>
        public HeifBoxRange MediaInformationBox { get; set; }
    }

    /// <summary>
    /// Holds the fields of the sample-table boxes that <see cref="ValidateSampleTable"/> validates against each other.
    /// </summary>
    private readonly ref struct SampleTable
    {
        /// <summary>
        /// Gets the number of samples of the sample-size box.
        /// </summary>
        public uint SampleCount { get; init; }

        /// <summary>
        /// Gets the size of every sample, or zero when the sample-size box stores each size.
        /// </summary>
        public uint ConstantSampleSize { get; init; }

        /// <summary>
        /// Gets the width of each stored sample size in bits.
        /// </summary>
        public int SampleSizeBits { get; init; }

        /// <summary>
        /// Gets the stored sample sizes.
        /// </summary>
        public ReadOnlySpan<byte> SampleSizes { get; init; }

        /// <summary>
        /// Gets the time-to-sample runs of sample_count and sample_delta.
        /// </summary>
        public ReadOnlySpan<byte> TimeToSample { get; init; }

        /// <summary>
        /// Gets the sample-to-chunk runs of first_chunk, samples_per_chunk and sample_description_index.
        /// </summary>
        public ReadOnlySpan<byte> SampleToChunk { get; init; }

        /// <summary>
        /// Gets the chunk offsets.
        /// </summary>
        public ReadOnlySpan<byte> ChunkOffsets { get; init; }

        /// <summary>
        /// Gets the width of each chunk offset in bytes.
        /// </summary>
        public int ChunkOffsetSize { get; init; }

        /// <summary>
        /// Gets the one-based sync sample numbers. The span is empty when the sample table has no sync-sample box, because the parser rejects an empty
        /// sync-sample table.
        /// </summary>
        public ReadOnlySpan<byte> SyncSamples { get; init; }
    }

    /// <summary>
    /// Associates one retained sample with its one-based direct-reference group description.
    /// </summary>
    private readonly struct SampleGroupAssignment : IComparable<SampleGroupAssignment>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SampleGroupAssignment"/> struct.
        /// </summary>
        /// <param name="groupDescriptionIndex">The one-based group-description index, or zero for no group.</param>
        /// <param name="sampleIndex">The zero-based retained sample index.</param>
        public SampleGroupAssignment(uint groupDescriptionIndex, int sampleIndex)
        {
            this.GroupDescriptionIndex = groupDescriptionIndex;
            this.SampleIndex = sampleIndex;
        }

        /// <summary>
        /// Gets the one-based group-description index, or zero for no group.
        /// </summary>
        public uint GroupDescriptionIndex { get; }

        /// <summary>
        /// Gets the zero-based retained sample index.
        /// </summary>
        public int SampleIndex { get; }

        /// <summary>
        /// Compares this assignment with another assignment in group-description and sample order.
        /// </summary>
        /// <param name="other">The other assignment.</param>
        /// <returns>A value indicating the relative sort order.</returns>
        public int CompareTo(SampleGroupAssignment other)
        {
            int result = this.GroupDescriptionIndex.CompareTo(other.GroupDescriptionIndex);
            return result != 0 ? result : this.SampleIndex.CompareTo(other.SampleIndex);
        }
    }

    /// <summary>
    /// Maps one positive direct-reference identifier to its retained decode-order sample index.
    /// </summary>
    private readonly struct SampleIdIndexEntry : IComparable<SampleIdIndexEntry>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SampleIdIndexEntry"/> struct.
        /// </summary>
        /// <param name="id">The positive file-defined sample identifier.</param>
        /// <param name="sampleIndex">The zero-based retained sample index.</param>
        public SampleIdIndexEntry(uint id, int sampleIndex)
        {
            this.Id = id;
            this.SampleIndex = sampleIndex;
        }

        /// <summary>
        /// Gets the positive file-defined sample identifier.
        /// </summary>
        public uint Id { get; }

        /// <summary>
        /// Gets the zero-based retained sample index.
        /// </summary>
        public int SampleIndex { get; }

        /// <summary>
        /// Compares this entry with another entry by sample identifier.
        /// </summary>
        /// <param name="other">The other identifier entry.</param>
        /// <returns>A value indicating the relative sort order.</returns>
        public int CompareTo(SampleIdIndexEntry other) => this.Id.CompareTo(other.Id);
    }
}
