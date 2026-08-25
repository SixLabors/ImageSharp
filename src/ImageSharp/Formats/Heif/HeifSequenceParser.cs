// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Hevc;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Parses the bounded track and sample syntax required to identify a HEIF image sequence.
/// </summary>
internal sealed class HeifSequenceParser
{
    /// <summary>
    /// The reusable scratch size used by sequential table reads.
    /// </summary>
    private const int ScratchLength = 4096;

    /// <summary>
    /// The configured allocator used for parser scratch and bounded table state.
    /// </summary>
    private readonly MemoryAllocator allocator;

    /// <summary>
    /// The shared reader for variable-length HEIF box headers.
    /// </summary>
    private readonly HeifBoxReader boxReader;

    /// <summary>
    /// The maximum number of sample descriptors retained for decoding or identification.
    /// </summary>
    private readonly int maxFrames;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeifSequenceParser"/> class.
    /// </summary>
    /// <param name="allocator">The configured ImageSharp memory allocator.</param>
    /// <param name="maxFrames">The maximum number of image-sequence frames to retain.</param>
    public HeifSequenceParser(MemoryAllocator allocator, uint maxFrames)
    {
        this.allocator = allocator;
        this.boxReader = new HeifBoxReader(allocator);
        this.maxFrames = (int)maxFrames;
    }

    /// <summary>
    /// Parses one movie box and selects its master image-sequence track and linked alpha track.
    /// </summary>
    /// <param name="stream">The seekable HEIF stream positioned at the movie payload.</param>
    /// <param name="boxLength">The validated movie payload length.</param>
    /// <returns>The bounded image-sequence model required by the HEIF decoder.</returns>
    public HeifSequence Parse(Stream stream, long boxLength)
    {
        long movieStart = stream.Position;
        long movieEnd = checked(movieStart + boxLength);
        HeifBoxReader.EnsureInsideParent(boxLength, stream.Length - movieStart);

        using IMemoryOwner<byte> scratchOwner = this.allocator.Allocate<byte>(ScratchLength);
        Span<byte> scratch = scratchOwner.GetSpan();
        BoxReference movieHeader = default;
        uint colorTrackId = 0;

        // The first pass reads only fixed track identity fields. This prevents files containing unrelated media tracks
        // from forcing codec configurations and sample tables into the image decoder's retained model.
        while (stream.Position < movieEnd)
        {
            long childLength = HeifBoxReader.ReadHeader(stream, movieEnd, scratch, out Heif4CharCode childType);
            long childStart = stream.Position;
            if (childType == Heif4CharCode.Mvhd)
            {
                SetUnique(ref movieHeader, childStart, childLength, "movie", childType);
            }
            else if (childType == Heif4CharCode.Trak)
            {
                TrackIdentity identity = ScanTrackIdentity(stream, childLength, scratch);
                if (colorTrackId == 0
                    && identity.IsEnabledInMovie
                    && identity.HandlerType == Heif4CharCode.Pict
                    && identity.AuxiliaryForTrackId == 0)
                {
                    colorTrackId = identity.Id;
                }
            }

            stream.Position = checked(childStart + childLength);
        }

        if (!movieHeader.IsPresent)
        {
            throw new InvalidImageContentException("The HEIF image sequence has no movie header.");
        }

        if (colorTrackId == 0)
        {
            throw new InvalidImageContentException("The HEIF image sequence has no enabled picture track.");
        }

        stream.Position = movieHeader.Offset;
        uint movieTimescale = ParseMovieHeader(stream, movieHeader.Length, scratch);
        HeifSequenceTrack? colorTrack = null;
        HeifSequenceTrack? alphaTrack = null;

        // Track references can precede the master track. Re-scan the bounded movie now that the selected master ID is
        // known, and fully parse only that track and the one alpha auxiliary linked to it.
        stream.Position = movieStart;
        while (stream.Position < movieEnd)
        {
            long childLength = HeifBoxReader.ReadHeader(stream, movieEnd, scratch, out Heif4CharCode childType);
            long childStart = stream.Position;
            if (childType == Heif4CharCode.Trak)
            {
                TrackIdentity identity = ScanTrackIdentity(stream, childLength, scratch);
                bool isColor = identity.Id == colorTrackId;
                bool isLinkedAuxiliary = identity.AuxiliaryForTrackId == colorTrackId
                    && identity.HandlerType is Heif4CharCode.Auxv or Heif4CharCode.Pict;

                if (isColor || (isLinkedAuxiliary && alphaTrack is null))
                {
                    stream.Position = childStart;
                    HeifSequenceTrack track = this.ParseTrack(stream, childLength, identity, scratch);
                    if (isColor)
                    {
                        colorTrack = track;
                    }
                    else if (track.IsAlpha)
                    {
                        alphaTrack = track;
                    }
                }
            }

            stream.Position = checked(childStart + childLength);
        }

        if (colorTrack is null)
        {
            throw new InvalidImageContentException("The selected HEIF picture track could not be parsed.");
        }

        ValidateAlphaTrack(colorTrack, alphaTrack);
        return new HeifSequence(colorTrack, alphaTrack, movieTimescale);
    }

    /// <summary>
    /// Reads the fixed identity fields used to select an image track without materializing its sample table.
    /// </summary>
    /// <param name="stream">The stream positioned at the track payload.</param>
    /// <param name="boxLength">The validated track payload length.</param>
    /// <param name="scratch">The parser-owned reusable scratch span.</param>
    /// <returns>The track identity and image relationship fields.</returns>
    private static TrackIdentity ScanTrackIdentity(Stream stream, long boxLength, Span<byte> scratch)
    {
        long trackEnd = checked(stream.Position + boxLength);
        BoxReference trackHeader = default;
        BoxReference trackReferences = default;
        BoxReference media = default;

        while (stream.Position < trackEnd)
        {
            long childLength = HeifBoxReader.ReadHeader(stream, trackEnd, scratch, out Heif4CharCode childType);
            long childStart = stream.Position;
            switch (childType)
            {
                case Heif4CharCode.Tkhd:
                    SetUnique(ref trackHeader, childStart, childLength, "track", childType);
                    break;
                case Heif4CharCode.Tref:
                    SetUnique(ref trackReferences, childStart, childLength, "track", childType);
                    break;
                case Heif4CharCode.Mdia:
                    SetUnique(ref media, childStart, childLength, "track", childType);
                    break;
            }

            stream.Position = checked(childStart + childLength);
        }

        if (!trackHeader.IsPresent || !media.IsPresent)
        {
            throw new InvalidImageContentException("A HEIF image-sequence track is missing its header or media box.");
        }

        stream.Position = trackHeader.Offset;
        TrackIdentity identity = ParseTrackHeader(stream, trackHeader.Length, scratch);
        if (trackReferences.IsPresent)
        {
            stream.Position = trackReferences.Offset;
            ParseTrackReferences(stream, trackReferences.Length, ref identity, scratch);
        }

        stream.Position = media.Offset;
        identity.HandlerType = ScanMediaHandler(stream, media.Length, scratch);
        return identity;
    }

    /// <summary>
    /// Parses the retained behavior of one selected image-sequence track.
    /// </summary>
    /// <param name="stream">The stream positioned at the track payload.</param>
    /// <param name="boxLength">The validated track payload length.</param>
    /// <param name="identity">The fixed identity fields from the selection pass.</param>
    /// <param name="scratch">The parser-owned reusable scratch span.</param>
    /// <returns>The selected track with its validated sample descriptors.</returns>
    private HeifSequenceTrack ParseTrack(Stream stream, long boxLength, TrackIdentity identity, Span<byte> scratch)
    {
        long trackEnd = checked(stream.Position + boxLength);
        BoxReference edit = default;
        BoxReference media = default;

        while (stream.Position < trackEnd)
        {
            long childLength = HeifBoxReader.ReadHeader(stream, trackEnd, scratch, out Heif4CharCode childType);
            long childStart = stream.Position;
            if (childType == Heif4CharCode.Edts)
            {
                SetUnique(ref edit, childStart, childLength, "track", childType);
            }
            else if (childType == Heif4CharCode.Mdia)
            {
                SetUnique(ref media, childStart, childLength, "track", childType);
            }

            stream.Position = checked(childStart + childLength);
        }

        if (!media.IsPresent)
        {
            throw new InvalidImageContentException("A selected HEIF image-sequence track has no media box.");
        }

        HeifSequenceTrack track = new()
        {
            Id = identity.Id,
            Width = identity.Width,
            Height = identity.Height,
            Matrix = identity.Matrix,
            HandlerType = identity.HandlerType,
            TrackDuration = identity.TrackDuration,
            AuxiliaryForTrackId = identity.AuxiliaryForTrackId,
            IsPremultiplied = identity.PremultipliedByTrackId != 0
        };

        if (edit.IsPresent)
        {
            stream.Position = edit.Offset;
            ParseEdit(stream, edit.Length, track, scratch);
        }

        stream.Position = media.Offset;
        this.ParseMedia(stream, media.Length, track, scratch);
        if (track.Matrix.HasPerspective)
        {
            throw new InvalidImageContentException("The HEIF image-sequence track uses an unsupported perspective matrix.");
        }

        if (track.TotalSampleCount == 0 || track.Samples.Length == 0)
        {
            throw new InvalidImageContentException("The HEIF image-sequence track contains no retained image samples.");
        }

        return track;
    }

    /// <summary>
    /// Parses the movie time scale required to interpret track edit durations.
    /// </summary>
    /// <param name="stream">The stream positioned at the movie-header payload.</param>
    /// <param name="boxLength">The validated movie-header payload length.</param>
    /// <param name="scratch">The parser-owned reusable scratch span.</param>
    /// <returns>The nonzero movie time scale.</returns>
    private static uint ParseMovieHeader(Stream stream, long boxLength, Span<byte> scratch)
    {
        ReadOnlySpan<byte> prefix = ReadPrefix(stream, boxLength, scratch, 4, "movie header");
        byte version = prefix[0];
        int requiredLength = version switch
        {
            0 => 20,
            1 => 32,
            _ => throw new InvalidImageContentException($"The movie header has unsupported version {version}.")
        };

        prefix = ReadPrefixFromStart(stream, boxLength, scratch, requiredLength, "movie header");
        EnsureZeroFlags(prefix, "movie header");
        uint timescale = BinaryPrimitives.ReadUInt32BigEndian(prefix[(version == 0 ? 12 : 20)..]);
        if (timescale == 0)
        {
            throw new InvalidImageContentException("The movie header has a zero time scale.");
        }

        return timescale;
    }

    /// <summary>
    /// Parses track identity, dimensions, duration, and transformation matrix.
    /// </summary>
    /// <param name="stream">The stream positioned at the track-header payload.</param>
    /// <param name="boxLength">The validated track-header payload length.</param>
    /// <param name="scratch">The parser-owned reusable scratch span.</param>
    /// <returns>The fixed track identity fields.</returns>
    private static TrackIdentity ParseTrackHeader(Stream stream, long boxLength, Span<byte> scratch)
    {
        ReadOnlySpan<byte> prefix = ReadPrefix(stream, boxLength, scratch, 4, "track header");
        byte version = prefix[0];
        int requiredLength = version switch
        {
            0 => 84,
            1 => 96,
            _ => throw new InvalidImageContentException($"The track header has unsupported version {version}.")
        };

        prefix = ReadPrefixFromStart(stream, boxLength, scratch, requiredLength, "track header");
        uint flags = ReadFlags(prefix);
        int trackIdOffset = version == 0 ? 12 : 20;
        int durationOffset = version == 0 ? 20 : 28;
        int matrixOffset = version == 0 ? 40 : 52;
        int widthOffset = version == 0 ? 76 : 88;
        uint id = BinaryPrimitives.ReadUInt32BigEndian(prefix[trackIdOffset..]);
        if (id == 0)
        {
            throw new InvalidImageContentException("A HEIF image-sequence track has identifier zero.");
        }

        ulong duration = version == 0
            ? BinaryPrimitives.ReadUInt32BigEndian(prefix[durationOffset..])
            : BinaryPrimitives.ReadUInt64BigEndian(prefix[durationOffset..]);

        if (version == 0 && duration == uint.MaxValue)
        {
            duration = ulong.MaxValue;
        }

        int width = checked((int)(BinaryPrimitives.ReadUInt32BigEndian(prefix[widthOffset..]) >> 16));
        int height = checked((int)(BinaryPrimitives.ReadUInt32BigEndian(prefix[(widthOffset + 4)..]) >> 16));
        if (width == 0 || height == 0)
        {
            throw new InvalidImageContentException("A HEIF image-sequence track has zero dimensions.");
        }

        HeifTrackMatrix matrix = new(
            BinaryPrimitives.ReadInt32BigEndian(prefix[matrixOffset..]),
            BinaryPrimitives.ReadInt32BigEndian(prefix[(matrixOffset + 4)..]),
            BinaryPrimitives.ReadInt32BigEndian(prefix[(matrixOffset + 8)..]),
            BinaryPrimitives.ReadInt32BigEndian(prefix[(matrixOffset + 12)..]),
            BinaryPrimitives.ReadInt32BigEndian(prefix[(matrixOffset + 16)..]),
            BinaryPrimitives.ReadInt32BigEndian(prefix[(matrixOffset + 20)..]),
            BinaryPrimitives.ReadInt32BigEndian(prefix[(matrixOffset + 24)..]),
            BinaryPrimitives.ReadInt32BigEndian(prefix[(matrixOffset + 28)..]),
            BinaryPrimitives.ReadInt32BigEndian(prefix[(matrixOffset + 32)..]));

        return new TrackIdentity(id, (flags & 3) == 3, width, height, duration, matrix);
    }

    /// <summary>
    /// Parses the image-specific track references used for alpha and premultiplication linkage.
    /// </summary>
    /// <param name="stream">The stream positioned at the track-reference payload.</param>
    /// <param name="boxLength">The validated track-reference payload length.</param>
    /// <param name="identity">The track identity receiving image relationships.</param>
    /// <param name="scratch">The parser-owned reusable scratch span.</param>
    private static void ParseTrackReferences(Stream stream, long boxLength, ref TrackIdentity identity, Span<byte> scratch)
    {
        long referenceEnd = checked(stream.Position + boxLength);
        while (stream.Position < referenceEnd)
        {
            long childLength = HeifBoxReader.ReadHeader(stream, referenceEnd, scratch, out Heif4CharCode childType);
            long childStart = stream.Position;
            if (childType is Heif4CharCode.Auxl or Heif4CharCode.Prem)
            {
                if (childLength < 4 || (childLength & 3) != 0)
                {
                    throw new InvalidImageContentException($"The '{childType}' track reference has an invalid identifier list.");
                }

                ReadOnlySpan<byte> data = ReadPrefix(stream, childLength, scratch, 4, $"{childType} track reference");
                uint referencedTrackId = BinaryPrimitives.ReadUInt32BigEndian(data);
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

            stream.Position = checked(childStart + childLength);
        }
    }

    /// <summary>
    /// Finds and parses the handler type from one media box.
    /// </summary>
    /// <param name="stream">The stream positioned at the media payload.</param>
    /// <param name="boxLength">The validated media payload length.</param>
    /// <param name="scratch">The parser-owned reusable scratch span.</param>
    /// <returns>The declared media handler type.</returns>
    private static Heif4CharCode ScanMediaHandler(Stream stream, long boxLength, Span<byte> scratch)
    {
        long mediaEnd = checked(stream.Position + boxLength);
        BoxReference handler = default;
        while (stream.Position < mediaEnd)
        {
            long childLength = HeifBoxReader.ReadHeader(stream, mediaEnd, scratch, out Heif4CharCode childType);
            long childStart = stream.Position;
            if (childType == Heif4CharCode.Hdlr)
            {
                SetUnique(ref handler, childStart, childLength, "media", childType);
            }

            stream.Position = checked(childStart + childLength);
        }

        if (!handler.IsPresent)
        {
            throw new InvalidImageContentException("A HEIF image-sequence media box has no handler.");
        }

        stream.Position = handler.Offset;
        return ParseHandler(stream, handler.Length, scratch);
    }

    /// <summary>
    /// Parses the media header and sample table of a selected image track.
    /// </summary>
    /// <param name="stream">The stream positioned at the media payload.</param>
    /// <param name="boxLength">The validated media payload length.</param>
    /// <param name="track">The selected track receiving media state.</param>
    /// <param name="scratch">The parser-owned reusable scratch span.</param>
    private void ParseMedia(Stream stream, long boxLength, HeifSequenceTrack track, Span<byte> scratch)
    {
        long mediaEnd = checked(stream.Position + boxLength);
        BoxReference mediaHeader = default;
        BoxReference handler = default;
        BoxReference mediaInformation = default;

        while (stream.Position < mediaEnd)
        {
            long childLength = HeifBoxReader.ReadHeader(stream, mediaEnd, scratch, out Heif4CharCode childType);
            long childStart = stream.Position;
            switch (childType)
            {
                case Heif4CharCode.Mdhd:
                    SetUnique(ref mediaHeader, childStart, childLength, "media", childType);
                    break;
                case Heif4CharCode.Hdlr:
                    SetUnique(ref handler, childStart, childLength, "media", childType);
                    break;
                case Heif4CharCode.Minf:
                    SetUnique(ref mediaInformation, childStart, childLength, "media", childType);
                    break;
            }

            stream.Position = checked(childStart + childLength);
        }

        if (!mediaHeader.IsPresent || !handler.IsPresent || !mediaInformation.IsPresent)
        {
            throw new InvalidImageContentException("A selected HEIF image-sequence media box is incomplete.");
        }

        stream.Position = mediaHeader.Offset;
        ParseMediaHeader(stream, mediaHeader.Length, track, scratch);
        stream.Position = handler.Offset;
        Heif4CharCode handlerType = ParseHandler(stream, handler.Length, scratch);
        if (handlerType != track.HandlerType)
        {
            throw new InvalidImageContentException("The HEIF image-sequence track handler changed between parser passes.");
        }

        stream.Position = mediaInformation.Offset;
        this.ParseMediaInformation(stream, mediaInformation.Length, track, scratch);
    }

    /// <summary>
    /// Parses the media time scale and duration of a selected track.
    /// </summary>
    /// <param name="stream">The stream positioned at the media-header payload.</param>
    /// <param name="boxLength">The validated media-header payload length.</param>
    /// <param name="track">The selected track receiving timing state.</param>
    /// <param name="scratch">The parser-owned reusable scratch span.</param>
    private static void ParseMediaHeader(Stream stream, long boxLength, HeifSequenceTrack track, Span<byte> scratch)
    {
        ReadOnlySpan<byte> prefix = ReadPrefix(stream, boxLength, scratch, 4, "media header");
        byte version = prefix[0];
        int requiredLength = version switch
        {
            0 => 24,
            1 => 36,
            _ => throw new InvalidImageContentException($"The media header has unsupported version {version}.")
        };

        prefix = ReadPrefixFromStart(stream, boxLength, scratch, requiredLength, "media header");
        EnsureZeroFlags(prefix, "media header");
        int timescaleOffset = version == 0 ? 12 : 20;
        track.MediaTimescale = BinaryPrimitives.ReadUInt32BigEndian(prefix[timescaleOffset..]);
        track.MediaDuration = version == 0
            ? BinaryPrimitives.ReadUInt32BigEndian(prefix[(timescaleOffset + 4)..])
            : BinaryPrimitives.ReadUInt64BigEndian(prefix[(timescaleOffset + 4)..]);

        if (track.MediaTimescale == 0)
        {
            throw new InvalidImageContentException("The HEIF image-sequence media header has a zero time scale.");
        }
    }

    /// <summary>
    /// Parses a media handler and returns its four-character handler type.
    /// </summary>
    /// <param name="stream">The stream positioned at the handler payload.</param>
    /// <param name="boxLength">The validated handler payload length.</param>
    /// <param name="scratch">The parser-owned reusable scratch span.</param>
    /// <returns>The declared handler type.</returns>
    private static Heif4CharCode ParseHandler(Stream stream, long boxLength, Span<byte> scratch)
    {
        ReadOnlySpan<byte> prefix = ReadPrefix(stream, boxLength, scratch, 24, "handler");
        EnsureVersionAndFlags(prefix, 0, 0, "handler");
        if (BinaryPrimitives.ReadUInt32BigEndian(prefix[4..]) != 0)
        {
            throw new InvalidImageContentException("The HEIF image-sequence handler has a nonzero predefined field.");
        }

        return (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(prefix[8..]);
    }

    /// <summary>
    /// Locates the self-contained data reference and sample table inside a selected track.
    /// </summary>
    /// <param name="stream">The stream positioned at the media-information payload.</param>
    /// <param name="boxLength">The validated media-information payload length.</param>
    /// <param name="track">The selected track receiving its sample table.</param>
    /// <param name="scratch">The parser-owned reusable scratch span.</param>
    private void ParseMediaInformation(Stream stream, long boxLength, HeifSequenceTrack track, Span<byte> scratch)
    {
        long informationEnd = checked(stream.Position + boxLength);
        BoxReference dataInformation = default;
        BoxReference sampleTable = default;
        while (stream.Position < informationEnd)
        {
            long childLength = HeifBoxReader.ReadHeader(stream, informationEnd, scratch, out Heif4CharCode childType);
            long childStart = stream.Position;
            if (childType == Heif4CharCode.Dinf)
            {
                SetUnique(ref dataInformation, childStart, childLength, "media information", childType);
            }
            else if (childType == Heif4CharCode.Stbl)
            {
                SetUnique(ref sampleTable, childStart, childLength, "media information", childType);
            }

            stream.Position = checked(childStart + childLength);
        }

        if (!dataInformation.IsPresent || !sampleTable.IsPresent)
        {
            throw new InvalidImageContentException("A selected HEIF image-sequence track has no data reference or sample table.");
        }

        stream.Position = dataInformation.Offset;
        ParseDataInformation(stream, dataInformation.Length, scratch);
        stream.Position = sampleTable.Offset;
        this.ParseSampleTable(stream, sampleTable.Length, track, scratch);
    }

    /// <summary>
    /// Requires a selected image track to address sample bytes in the current HEIF file.
    /// </summary>
    /// <param name="stream">The stream positioned at the data-information payload.</param>
    /// <param name="boxLength">The validated data-information payload length.</param>
    /// <param name="scratch">The parser-owned reusable scratch span.</param>
    private static void ParseDataInformation(Stream stream, long boxLength, Span<byte> scratch)
    {
        long informationEnd = checked(stream.Position + boxLength);
        BoxReference dataReference = default;
        while (stream.Position < informationEnd)
        {
            long childLength = HeifBoxReader.ReadHeader(stream, informationEnd, scratch, out Heif4CharCode childType);
            long childStart = stream.Position;
            if (childType == Heif4CharCode.Dref)
            {
                SetUnique(ref dataReference, childStart, childLength, "data information", childType);
            }

            stream.Position = checked(childStart + childLength);
        }

        if (!dataReference.IsPresent)
        {
            throw new InvalidImageContentException("A selected HEIF image-sequence track has no data-reference box.");
        }

        stream.Position = dataReference.Offset;
        long referenceEnd = checked(stream.Position + dataReference.Length);
        ReadOnlySpan<byte> prefix = ReadPrefix(stream, dataReference.Length, scratch, 8, "data reference");
        EnsureVersionAndFlags(prefix, 0, 0, "data reference");
        if (BinaryPrimitives.ReadUInt32BigEndian(prefix[4..]) != 1)
        {
            throw new InvalidImageContentException("A HEIF image-sequence track must contain exactly one data reference.");
        }

        long locationLength = HeifBoxReader.ReadHeader(stream, referenceEnd, scratch, out Heif4CharCode locationType);
        if (locationType != Heif4CharCode.Url || locationLength != 4)
        {
            throw new InvalidImageContentException("A HEIF image-sequence track uses an external data reference.");
        }

        prefix = ReadPrefix(stream, locationLength, scratch, 4, "data location");
        EnsureVersionAndFlags(prefix, 0, 1, "data location");
        if (stream.Position != referenceEnd)
        {
            throw new InvalidImageContentException("The data-reference box contains undeclared entries.");
        }
    }

    /// <summary>
    /// Indexes and resolves the bounded sample-table boxes required by an image sequence.
    /// </summary>
    /// <param name="stream">The stream positioned at the sample-table payload.</param>
    /// <param name="boxLength">The validated sample-table payload length.</param>
    /// <param name="track">The selected track receiving sample descriptors.</param>
    /// <param name="scratch">The parser-owned reusable scratch span.</param>
    private void ParseSampleTable(Stream stream, long boxLength, HeifSequenceTrack track, Span<byte> scratch)
    {
        long tableEnd = checked(stream.Position + boxLength);
        BoxReference sampleDescription = default;
        BoxReference sampleTiming = default;
        BoxReference sampleToChunk = default;
        BoxReference sampleSizes = default;
        BoxReference chunkOffsets = default;
        BoxReference syncSamples = default;
        BoxReference compositionOffsets = default;
        BoxReference compositionToDecode = default;

        while (stream.Position < tableEnd)
        {
            long childLength = HeifBoxReader.ReadHeader(stream, tableEnd, scratch, out Heif4CharCode childType);
            long childStart = stream.Position;
            switch (childType)
            {
                case Heif4CharCode.Stsd:
                    SetUnique(ref sampleDescription, childStart, childLength, "sample table", childType);
                    break;
                case Heif4CharCode.Stts:
                    SetUnique(ref sampleTiming, childStart, childLength, "sample table", childType);
                    break;
                case Heif4CharCode.Stsc:
                    SetUnique(ref sampleToChunk, childStart, childLength, "sample table", childType);
                    break;
                case Heif4CharCode.Stsz:
                case Heif4CharCode.Stz2:
                    SetUnique(ref sampleSizes, childStart, childLength, "sample table", childType);
                    sampleSizes.Type = childType;
                    break;
                case Heif4CharCode.Stco:
                case Heif4CharCode.Co64:
                    SetUnique(ref chunkOffsets, childStart, childLength, "sample table", childType);
                    chunkOffsets.Type = childType;
                    break;
                case Heif4CharCode.Stss:
                    SetUnique(ref syncSamples, childStart, childLength, "sample table", childType);
                    break;
                case Heif4CharCode.Ctts:
                    SetUnique(ref compositionOffsets, childStart, childLength, "sample table", childType);
                    break;
                case Heif4CharCode.Cslg:
                    SetUnique(ref compositionToDecode, childStart, childLength, "sample table", childType);
                    break;
            }

            stream.Position = checked(childStart + childLength);
        }

        if (!sampleDescription.IsPresent
            || !sampleTiming.IsPresent
            || !sampleToChunk.IsPresent
            || !sampleSizes.IsPresent
            || !chunkOffsets.IsPresent)
        {
            throw new InvalidImageContentException("A selected HEIF image-sequence sample table is incomplete.");
        }

        stream.Position = sampleDescription.Offset;
        this.ParseSampleDescription(stream, sampleDescription.Length, track, scratch);
        stream.Position = sampleSizes.Offset;
        this.ParseSampleSizes(stream, sampleSizes.Length, sampleSizes.Type, track, scratch);
        stream.Position = sampleTiming.Offset;
        ulong decodedDuration = ParseSampleTiming(stream, sampleTiming.Length, track, scratch);

        if (track.MediaDuration != 0 && track.MediaDuration != ulong.MaxValue && track.MediaDuration != decodedDuration)
        {
            throw new InvalidImageContentException("The image-sequence sample durations do not match the media-header duration.");
        }

        stream.Position = chunkOffsets.Offset;
        uint chunkCount = ReadChunkCount(stream, chunkOffsets.Length, chunkOffsets.Type, scratch);
        stream.Position = sampleToChunk.Offset;
        using IMemoryOwner<SampleToChunkEntry> entries = this.ParseSampleToChunk(
            stream,
            sampleToChunk.Length,
            chunkCount,
            track,
            scratch,
            out int entryCount);

        stream.Position = chunkOffsets.Offset;
        ResolveSampleLocations(stream, chunkOffsets.Length, chunkOffsets.Type, chunkCount, entries.GetSpan()[..entryCount], track, scratch);
        if (syncSamples.IsPresent)
        {
            stream.Position = syncSamples.Offset;
            ParseSyncSamples(stream, syncSamples.Length, track, scratch);
        }
        else
        {
            Span<HeifSequenceSample> samples = track.Samples;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i].IsSync = true;
            }
        }

        if (compositionOffsets.IsPresent)
        {
            if (track.CodecType == Heif4CharCode.Av01)
            {
                // AV1-ISOBMFF defines AV1 sample composition time as decode time and explicitly prohibits ctts.
                throw new InvalidImageContentException("An AV1 image-sequence track contains a prohibited composition-offset box.");
            }

            stream.Position = compositionOffsets.Offset;
            CompositionSummary composition = ParseCompositionOffsets(stream, compositionOffsets.Length, track, scratch);
            if (composition.HasHiddenSamples && (!compositionToDecode.IsPresent || !track.HasEditList))
            {
                throw new InvalidImageContentException("A HEVC image-sequence track has hidden samples without the required composition and edit boxes.");
            }

            if (compositionToDecode.IsPresent)
            {
                stream.Position = compositionToDecode.Offset;
                ParseCompositionToDecode(stream, compositionToDecode.Length, composition, scratch);
            }
        }
        else if (compositionToDecode.IsPresent)
        {
            throw new InvalidImageContentException("The composition-to-decode box has no composition-offset table.");
        }

        SetCompositionTimes(track);
    }

    /// <summary>
    /// Parses the single visual sample entry and its codec and coding-constraint children.
    /// </summary>
    /// <param name="stream">The stream positioned at the sample-description payload.</param>
    /// <param name="boxLength">The validated sample-description payload length.</param>
    /// <param name="track">The selected track receiving its codec configuration.</param>
    /// <param name="scratch">The parser-owned reusable scratch span.</param>
    private void ParseSampleDescription(Stream stream, long boxLength, HeifSequenceTrack track, Span<byte> scratch)
    {
        long descriptionEnd = checked(stream.Position + boxLength);
        ReadOnlySpan<byte> prefix = ReadPrefix(stream, boxLength, scratch, 8, "sample description");
        byte version = prefix[0];
        if (version is not 0 and not 1 || ReadFlags(prefix) != 0)
        {
            throw new InvalidImageContentException("The sample-description box has an unsupported version or flags.");
        }

        if (BinaryPrimitives.ReadUInt32BigEndian(prefix[4..]) != 1)
        {
            throw new InvalidImageContentException("A HEIF image-sequence track must contain exactly one sample description.");
        }

        long entryLength = HeifBoxReader.ReadHeader(stream, descriptionEnd, scratch, out Heif4CharCode entryType);
        if (entryType is not Heif4CharCode.Av01 and not Heif4CharCode.Hvc1 || entryLength < 78)
        {
            throw new InvalidImageContentException($"The image-sequence sample entry '{entryType}' is unsupported or truncated.");
        }

        long entryEnd = checked(stream.Position + entryLength);
        prefix = ReadPrefix(stream, entryLength, scratch, 78, "visual sample entry");
        if (BinaryPrimitives.ReadUInt16BigEndian(prefix[6..]) != 1)
        {
            throw new InvalidImageContentException("The image-sequence sample entry uses a nonlocal data reference.");
        }

        int codedWidth = BinaryPrimitives.ReadUInt16BigEndian(prefix[24..]);
        int codedHeight = BinaryPrimitives.ReadUInt16BigEndian(prefix[26..]);
        if (codedWidth == 0 || codedHeight == 0)
        {
            throw new InvalidImageContentException("The image-sequence sample entry has zero coded dimensions.");
        }

        track.CodecType = entryType;
        track.CodedWidth = codedWidth;
        track.CodedHeight = codedHeight;
        bool configurationSeen = false;
        bool codingConstraintsSeen = false;
        bool auxiliaryTypeSeen = false;
        while (stream.Position < entryEnd)
        {
            long childLength = HeifBoxReader.ReadHeader(stream, entryEnd, scratch, out Heif4CharCode childType);
            long childStart = stream.Position;
            switch (childType)
            {
                case Heif4CharCode.Av1C when entryType == Heif4CharCode.Av01:
                    if (configurationSeen)
                    {
                        throw new InvalidImageContentException("The AV1 image-sequence sample entry has duplicate codec configurations.");
                    }

                    using (IMemoryOwner<byte> configuration = this.boxReader.ReadPayload(stream, childLength))
                    {
                        track.Av1CodecConfiguration = new Av1CodecConfiguration(configuration.GetSpan());
                    }

                    configurationSeen = true;
                    break;
                case Heif4CharCode.HvcC when entryType == Heif4CharCode.Hvc1:
                    if (configurationSeen)
                    {
                        throw new InvalidImageContentException("The HEVC image-sequence sample entry has duplicate codec configurations.");
                    }

                    using (IMemoryOwner<byte> configuration = this.boxReader.ReadPayload(stream, childLength))
                    {
                        track.HevcCodecConfiguration = new HevcCodecConfiguration(configuration.GetSpan());
                    }

                    configurationSeen = true;
                    break;
                case Heif4CharCode.Ccst:
                    if (codingConstraintsSeen)
                    {
                        throw new InvalidImageContentException("The image-sequence sample entry has duplicate coding constraints.");
                    }

                    ParseCodingConstraints(stream, childLength, track, scratch);
                    codingConstraintsSeen = true;
                    break;
                case Heif4CharCode.Auxi:
                    if (auxiliaryTypeSeen)
                    {
                        throw new InvalidImageContentException("The image-sequence sample entry has duplicate auxiliary types.");
                    }

                    track.IsAlpha = this.ParseAuxiliaryType(stream, childLength);
                    auxiliaryTypeSeen = true;
                    break;
            }

            stream.Position = checked(childStart + childLength);
        }

        if (stream.Position != descriptionEnd || !configurationSeen || !codingConstraintsSeen)
        {
            throw new InvalidImageContentException("The image-sequence sample description is incomplete or has trailing entries.");
        }
    }

    /// <summary>
    /// Parses coding constraints that bound inter-picture references for an image sequence.
    /// </summary>
    /// <param name="stream">The stream positioned at the coding-constraints payload.</param>
    /// <param name="boxLength">The validated coding-constraints payload length.</param>
    /// <param name="track">The selected track receiving coding constraints.</param>
    /// <param name="scratch">The parser-owned reusable scratch span.</param>
    private static void ParseCodingConstraints(Stream stream, long boxLength, HeifSequenceTrack track, Span<byte> scratch)
    {
        ReadOnlySpan<byte> data = ReadPrefix(stream, boxLength, scratch, 8, "coding constraints");
        if (boxLength != 8)
        {
            throw new InvalidImageContentException("The image-sequence coding-constraints box has an invalid length.");
        }

        EnsureVersionAndFlags(data, 0, 0, "coding constraints");
        uint constraints = BinaryPrimitives.ReadUInt32BigEndian(data[4..]);
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
    /// <param name="stream">The stream positioned at the auxiliary-type payload.</param>
    /// <param name="boxLength">The validated auxiliary-type payload length.</param>
    /// <returns><see langword="true"/> when the payload contains either registered HEIF alpha URN.</returns>
    private bool ParseAuxiliaryType(Stream stream, long boxLength)
    {
        if (boxLength < 5 || boxLength > int.MaxValue)
        {
            throw new InvalidImageContentException("The image-sequence auxiliary type is truncated or too large.");
        }

        using IMemoryOwner<byte> payload = this.boxReader.ReadPayload(stream, boxLength);
        ReadOnlySpan<byte> data = payload.GetSpan();
        EnsureVersionAndFlags(data, 0, 0, "auxiliary type");
        ReadOnlySpan<byte> type = data[4..];
        if (type[^1] != 0)
        {
            throw new InvalidImageContentException("The image-sequence auxiliary type is not null terminated.");
        }

        type = type[..^1];
        return type.SequenceEqual("urn:mpeg:mpegB:cicp:systems:auxiliary:alpha"u8)
            || type.SequenceEqual("urn:mpeg:hevc:2015:auxid:1"u8);
    }

    /// <summary>
    /// Parses either full-width or compact sample sizes into the retained descriptor array.
    /// </summary>
    /// <param name="stream">The stream positioned at the sample-size payload.</param>
    /// <param name="boxLength">The validated sample-size payload length.</param>
    /// <param name="boxType">The full-width or compact sample-size box type.</param>
    /// <param name="track">The selected track receiving retained sample lengths.</param>
    /// <param name="scratch">The parser-owned reusable scratch span.</param>
    private void ParseSampleSizes(Stream stream, long boxLength, Heif4CharCode boxType, HeifSequenceTrack track, Span<byte> scratch)
    {
        if (boxType == Heif4CharCode.Stsz)
        {
            this.ParseFullSampleSizes(stream, boxLength, track, scratch);
        }
        else
        {
            this.ParseCompactSampleSizes(stream, boxLength, track, scratch);
        }
    }

    /// <summary>
    /// Parses a full-width sample-size table without retaining entries beyond <see cref="maxFrames"/>.
    /// </summary>
    /// <param name="stream">The stream positioned at the sample-size payload.</param>
    /// <param name="boxLength">The validated sample-size payload length.</param>
    /// <param name="track">The selected track receiving retained sample lengths.</param>
    /// <param name="scratch">The parser-owned reusable scratch span.</param>
    private void ParseFullSampleSizes(Stream stream, long boxLength, HeifSequenceTrack track, Span<byte> scratch)
    {
        ReadOnlySpan<byte> prefix = ReadPrefix(stream, boxLength, scratch, 12, "sample sizes");
        EnsureVersionAndFlags(prefix, 0, 0, "sample sizes");
        uint constantSize = BinaryPrimitives.ReadUInt32BigEndian(prefix[4..]);
        uint sampleCount = BinaryPrimitives.ReadUInt32BigEndian(prefix[8..]);
        if (sampleCount == 0)
        {
            throw new InvalidImageContentException("The image-sequence sample-size table is empty.");
        }

        long entryBytes = constantSize == 0 ? checked((long)sampleCount * 4) : 0;
        if (boxLength != 12 + entryBytes)
        {
            throw new InvalidImageContentException("The image-sequence sample-size table length does not match its entry count.");
        }

        int retainedCount = (int)Math.Min(sampleCount, (uint)this.maxFrames);
        track.TotalSampleCount = sampleCount;
        track.Samples = new HeifSequenceSample[retainedCount];
        if (constantSize != 0)
        {
            int size = ValidateSampleSize(constantSize);
            Span<HeifSequenceSample> samples = track.Samples;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i].Length = size;
            }

            return;
        }

        TableReader reader = new(stream, entryBytes, scratch, "sample sizes");
        for (uint i = 0; i < sampleCount; i++)
        {
            int size = ValidateSampleSize(reader.ReadUInt32());
            if (i < retainedCount)
            {
                track.Samples[(int)i].Length = size;
            }
        }
    }

    /// <summary>
    /// Parses a compact sample-size table without expanding entries beyond <see cref="maxFrames"/>.
    /// </summary>
    /// <param name="stream">The stream positioned at the compact sample-size payload.</param>
    /// <param name="boxLength">The validated compact sample-size payload length.</param>
    /// <param name="track">The selected track receiving retained sample lengths.</param>
    /// <param name="scratch">The parser-owned reusable scratch span.</param>
    private void ParseCompactSampleSizes(Stream stream, long boxLength, HeifSequenceTrack track, Span<byte> scratch)
    {
        ReadOnlySpan<byte> prefix = ReadPrefix(stream, boxLength, scratch, 12, "compact sample sizes");
        EnsureVersionAndFlags(prefix, 0, 0, "compact sample sizes");
        if (prefix[4] != 0 || prefix[5] != 0 || prefix[6] != 0 || prefix[7] is not 4 and not 8 and not 16)
        {
            throw new InvalidImageContentException("The compact sample-size table has invalid reserved fields or field width.");
        }

        int fieldSize = prefix[7];
        uint sampleCount = BinaryPrimitives.ReadUInt32BigEndian(prefix[8..]);
        if (sampleCount == 0)
        {
            throw new InvalidImageContentException("The compact sample-size table is empty.");
        }

        long entryBytes = checked((((long)sampleCount * fieldSize) + 7) / 8);
        if (boxLength != 12 + entryBytes)
        {
            throw new InvalidImageContentException("The compact sample-size table length does not match its entry count.");
        }

        int retainedCount = (int)Math.Min(sampleCount, (uint)this.maxFrames);
        track.TotalSampleCount = sampleCount;
        track.Samples = new HeifSequenceSample[retainedCount];
        TableReader reader = new(stream, entryBytes, scratch, "compact sample sizes");
        for (uint i = 0; i < sampleCount; i++)
        {
            uint size;
            if (fieldSize == 4)
            {
                byte packed = reader.ReadByte();
                size = (uint)(packed >> 4);
                if (i < retainedCount)
                {
                    track.Samples[(int)i].Length = ValidateSampleSize(size);
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
                size = fieldSize == 8 ? reader.ReadByte() : reader.ReadUInt16();
            }

            int validatedSize = ValidateSampleSize(size);
            if (i < retainedCount)
            {
                track.Samples[(int)i].Length = validatedSize;
            }
        }
    }

    /// <summary>
    /// Expands retained sample durations and validates the complete timing run table.
    /// </summary>
    /// <param name="stream">The stream positioned at the time-to-sample payload.</param>
    /// <param name="boxLength">The validated time-to-sample payload length.</param>
    /// <param name="track">The selected track receiving retained durations.</param>
    /// <param name="scratch">The parser-owned reusable scratch span.</param>
    /// <returns>The total decoded duration in media-time-scale units.</returns>
    private static ulong ParseSampleTiming(Stream stream, long boxLength, HeifSequenceTrack track, Span<byte> scratch)
    {
        ReadOnlySpan<byte> prefix = ReadPrefix(stream, boxLength, scratch, 8, "sample timing");
        EnsureVersionAndFlags(prefix, 0, 0, "sample timing");
        uint entryCount = BinaryPrimitives.ReadUInt32BigEndian(prefix[4..]);
        long entryBytes = checked((long)entryCount * 8);
        if (entryCount == 0 || boxLength != 8 + entryBytes)
        {
            throw new InvalidImageContentException("The image-sequence timing table is empty or has an invalid length.");
        }

        TableReader reader = new(stream, entryBytes, scratch, "sample timing");
        ulong describedSamples = 0;
        ulong decodedDuration = 0;
        int retainedOffset = 0;
        for (uint entry = 0; entry < entryCount; entry++)
        {
            uint sampleCount = reader.ReadUInt32();
            uint sampleDelta = reader.ReadUInt32();
            if (sampleCount == 0 || sampleDelta == 0)
            {
                throw new InvalidImageContentException("The image-sequence timing table contains a zero run or duration.");
            }

            describedSamples = checked(describedSamples + sampleCount);
            decodedDuration = checked(decodedDuration + ((ulong)sampleCount * sampleDelta));
            int retainedRun = Math.Min((int)Math.Min(sampleCount, int.MaxValue), track.Samples.Length - retainedOffset);
            Span<HeifSequenceSample> samples = track.Samples;
            for (int i = 0; i < retainedRun; i++)
            {
                samples[retainedOffset + i].Duration = sampleDelta;
            }

            retainedOffset += retainedRun;
        }

        if (describedSamples != track.TotalSampleCount)
        {
            throw new InvalidImageContentException("The image-sequence timing table does not describe every sample.");
        }

        return decodedDuration;
    }

    /// <summary>
    /// Reads and validates the declared chunk count without retaining chunk offsets.
    /// </summary>
    /// <param name="stream">The stream positioned at a chunk-offset payload.</param>
    /// <param name="boxLength">The validated chunk-offset payload length.</param>
    /// <param name="boxType">The 32-bit or 64-bit chunk-offset box type.</param>
    /// <param name="scratch">The parser-owned reusable scratch span.</param>
    /// <returns>The nonzero number of chunks.</returns>
    private static uint ReadChunkCount(Stream stream, long boxLength, Heif4CharCode boxType, Span<byte> scratch)
    {
        ReadOnlySpan<byte> prefix = ReadPrefix(stream, boxLength, scratch, 8, "chunk offsets");
        EnsureVersionAndFlags(prefix, 0, 0, "chunk offsets");
        uint chunkCount = BinaryPrimitives.ReadUInt32BigEndian(prefix[4..]);
        int entrySize = boxType == Heif4CharCode.Co64 ? 8 : 4;
        if (chunkCount == 0 || boxLength != 8 + checked((long)chunkCount * entrySize))
        {
            throw new InvalidImageContentException("The image-sequence chunk-offset table is empty or has an invalid length.");
        }

        return chunkCount;
    }

    /// <summary>
    /// Parses sample-to-chunk runs into allocator-owned state bounded by the retained frame count.
    /// </summary>
    /// <param name="stream">The stream positioned at the sample-to-chunk payload.</param>
    /// <param name="boxLength">The validated sample-to-chunk payload length.</param>
    /// <param name="chunkCount">The validated number of chunks.</param>
    /// <param name="track">The selected track whose complete sample count is validated.</param>
    /// <param name="scratch">The parser-owned reusable scratch span.</param>
    /// <param name="retainedEntryCount">Receives the number of retained mapping entries.</param>
    /// <returns>Allocator-owned sample-to-chunk runs that cover all retained samples.</returns>
    private IMemoryOwner<SampleToChunkEntry> ParseSampleToChunk(
        Stream stream,
        long boxLength,
        uint chunkCount,
        HeifSequenceTrack track,
        Span<byte> scratch,
        out int retainedEntryCount)
    {
        ReadOnlySpan<byte> prefix = ReadPrefix(stream, boxLength, scratch, 8, "sample-to-chunk");
        EnsureVersionAndFlags(prefix, 0, 0, "sample-to-chunk");
        uint entryCount = BinaryPrimitives.ReadUInt32BigEndian(prefix[4..]);
        long entryBytes = checked((long)entryCount * 12);
        if (entryCount == 0 || entryCount > chunkCount || boxLength != 8 + entryBytes)
        {
            throw new InvalidImageContentException("The sample-to-chunk table is empty or has an invalid length or entry count.");
        }

        int retainedCapacity = (int)Math.Min(entryCount, (uint)track.Samples.Length);
        IMemoryOwner<SampleToChunkEntry> owner = this.allocator.Allocate<SampleToChunkEntry>(retainedCapacity);
        Span<SampleToChunkEntry> retainedEntries = owner.GetSpan();
        TableReader reader = new(stream, entryBytes, scratch, "sample-to-chunk");
        uint previousFirstChunk = 0;
        uint previousSamplesPerChunk = 0;
        ulong describedSamples = 0;
        retainedEntryCount = 0;

        try
        {
            for (uint i = 0; i < entryCount; i++)
            {
                uint firstChunk = reader.ReadUInt32();
                uint samplesPerChunk = reader.ReadUInt32();
                uint sampleDescriptionIndex = reader.ReadUInt32();
                if ((i == 0 && firstChunk != 1) || firstChunk <= previousFirstChunk || firstChunk > chunkCount
                    || samplesPerChunk == 0 || sampleDescriptionIndex != 1)
                {
                    throw new InvalidImageContentException("The sample-to-chunk table contains an invalid run.");
                }

                if (i != 0)
                {
                    describedSamples = checked(describedSamples + ((ulong)(firstChunk - previousFirstChunk) * previousSamplesPerChunk));
                }

                if (retainedEntryCount < retainedEntries.Length)
                {
                    retainedEntries[retainedEntryCount++] = new SampleToChunkEntry(firstChunk, samplesPerChunk);
                }

                previousFirstChunk = firstChunk;
                previousSamplesPerChunk = samplesPerChunk;
            }

            describedSamples = checked(describedSamples + ((ulong)(chunkCount + 1U - previousFirstChunk) * previousSamplesPerChunk));
            if (describedSamples != track.TotalSampleCount)
            {
                throw new InvalidImageContentException("The sample-to-chunk table does not map every declared sample.");
            }

            return owner;
        }
        catch
        {
            owner.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Resolves retained samples directly from sequential chunk offsets and compact mapping runs.
    /// </summary>
    /// <param name="stream">The stream positioned at the chunk-offset payload.</param>
    /// <param name="boxLength">The validated chunk-offset payload length.</param>
    /// <param name="boxType">The 32-bit or 64-bit chunk-offset box type.</param>
    /// <param name="chunkCount">The validated number of chunks.</param>
    /// <param name="entries">The retained sample-to-chunk runs.</param>
    /// <param name="track">The selected track receiving absolute sample locations.</param>
    /// <param name="scratch">The parser-owned reusable scratch span.</param>
    private static void ResolveSampleLocations(
        Stream stream,
        long boxLength,
        Heif4CharCode boxType,
        uint chunkCount,
        ReadOnlySpan<SampleToChunkEntry> entries,
        HeifSequenceTrack track,
        Span<byte> scratch)
    {
        _ = ReadChunkCount(stream, boxLength, boxType, scratch);
        int entrySize = boxType == Heif4CharCode.Co64 ? 8 : 4;
        TableReader reader = new(stream, checked((long)chunkCount * entrySize), scratch, "chunk offsets");
        int retainedSample = 0;
        int runIndex = 0;
        for (uint chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            ulong chunkOffset = entrySize == 8 ? reader.ReadUInt64() : reader.ReadUInt32();
            if (chunkOffset > (ulong)stream.Length)
            {
                throw new InvalidImageContentException("An image-sequence chunk offset extends beyond the file.");
            }

            uint chunkNumber = chunkIndex + 1;
            if (runIndex + 1 < entries.Length && entries[runIndex + 1].FirstChunk <= chunkNumber)
            {
                runIndex++;
            }

            uint samplesPerChunk = entries[runIndex].SamplesPerChunk;
            ulong sampleOffset = chunkOffset;
            for (uint sampleInChunk = 0; sampleInChunk < samplesPerChunk && retainedSample < track.Samples.Length; sampleInChunk++)
            {
                ref HeifSequenceSample sample = ref track.Samples[retainedSample++];
                ulong sampleEnd = checked(sampleOffset + (uint)sample.Length);
                if (sampleEnd > (ulong)stream.Length || sampleOffset > long.MaxValue)
                {
                    throw new InvalidImageContentException("An image-sequence sample extends beyond the file.");
                }

                sample.Offset = (long)sampleOffset;
                sampleOffset = sampleEnd;
            }
        }

        if (retainedSample != track.Samples.Length)
        {
            throw new InvalidImageContentException("The image-sequence chunk table does not locate every retained sample.");
        }
    }

    /// <summary>
    /// Applies explicit one-based sync-sample declarations to retained sample descriptors.
    /// </summary>
    /// <param name="stream">The stream positioned at the sync-sample payload.</param>
    /// <param name="boxLength">The validated sync-sample payload length.</param>
    /// <param name="track">The selected track receiving random-access markers.</param>
    /// <param name="scratch">The parser-owned reusable scratch span.</param>
    private static void ParseSyncSamples(Stream stream, long boxLength, HeifSequenceTrack track, Span<byte> scratch)
    {
        ReadOnlySpan<byte> prefix = ReadPrefix(stream, boxLength, scratch, 8, "sync samples");
        EnsureVersionAndFlags(prefix, 0, 0, "sync samples");
        uint entryCount = BinaryPrimitives.ReadUInt32BigEndian(prefix[4..]);
        long entryBytes = checked((long)entryCount * 4);
        if (entryCount == 0 || boxLength != 8 + entryBytes)
        {
            throw new InvalidImageContentException("The sync-sample table is empty or has an invalid length.");
        }

        TableReader reader = new(stream, entryBytes, scratch, "sync samples");
        uint previousSample = 0;
        for (uint i = 0; i < entryCount; i++)
        {
            uint sampleNumber = reader.ReadUInt32();
            if (sampleNumber <= previousSample || sampleNumber > track.TotalSampleCount)
            {
                throw new InvalidImageContentException("The sync-sample table contains an invalid sample number.");
            }

            if (sampleNumber <= track.Samples.Length)
            {
                track.Samples[(int)sampleNumber - 1].IsSync = true;
            }

            previousSample = sampleNumber;
        }

        if (!track.Samples[0].IsSync)
        {
            throw new InvalidImageContentException("The first retained image-sequence sample is not a random-access sample.");
        }
    }

    /// <summary>
    /// Parses HEVC decode-to-composition offsets and marks non-output reference samples.
    /// </summary>
    /// <param name="stream">The stream positioned at the composition-offset payload.</param>
    /// <param name="boxLength">The validated composition-offset payload length.</param>
    /// <param name="track">The selected HEVC track receiving retained composition offsets.</param>
    /// <param name="scratch">The parser-owned reusable scratch span.</param>
    /// <returns>The complete visible-offset range and hidden-sample state.</returns>
    private static CompositionSummary ParseCompositionOffsets(Stream stream, long boxLength, HeifSequenceTrack track, Span<byte> scratch)
    {
        ReadOnlySpan<byte> prefix = ReadPrefix(stream, boxLength, scratch, 8, "composition offsets");
        byte version = prefix[0];
        if (version is not 0 and not 1 || ReadFlags(prefix) != 0)
        {
            throw new InvalidImageContentException("The composition-offset box has an unsupported version or flags.");
        }

        uint entryCount = BinaryPrimitives.ReadUInt32BigEndian(prefix[4..]);
        long entryBytes = checked((long)entryCount * 8);
        if (entryCount == 0 || boxLength != 8 + entryBytes)
        {
            throw new InvalidImageContentException("The composition-offset table is empty or has an invalid length.");
        }

        TableReader reader = new(stream, entryBytes, scratch, "composition offsets");
        ulong describedSamples = 0;
        int retainedOffset = 0;
        long leastOffset = long.MaxValue;
        long greatestOffset = long.MinValue;
        bool hasHiddenSamples = false;
        for (uint entry = 0; entry < entryCount; entry++)
        {
            uint sampleCount = reader.ReadUInt32();
            uint rawOffset = reader.ReadUInt32();
            if (sampleCount == 0)
            {
                throw new InvalidImageContentException("The composition-offset table contains a zero-length run.");
            }

            bool hidden = version == 1 && rawOffset == 0x80000000;
            long compositionOffset = version == 0 ? rawOffset : unchecked((int)rawOffset);
            describedSamples = checked(describedSamples + sampleCount);
            hasHiddenSamples |= hidden;
            if (!hidden)
            {
                leastOffset = Math.Min(leastOffset, compositionOffset);
                greatestOffset = Math.Max(greatestOffset, compositionOffset);
            }

            int retainedRun = Math.Min((int)Math.Min(sampleCount, int.MaxValue), track.Samples.Length - retainedOffset);
            Span<HeifSequenceSample> samples = track.Samples;
            for (int i = 0; i < retainedRun; i++)
            {
                samples[retainedOffset + i].CompositionOffset = compositionOffset;
                samples[retainedOffset + i].IsHidden = hidden;
            }

            retainedOffset += retainedRun;
        }

        if (describedSamples != track.TotalSampleCount || leastOffset == long.MaxValue)
        {
            throw new InvalidImageContentException("The composition-offset table does not describe every sample or contains no output sample.");
        }

        return new CompositionSummary(leastOffset, greatestOffset, hasHiddenSamples);
    }

    /// <summary>
    /// Validates the track-wide composition bounds associated with HEVC non-output and reordered samples.
    /// </summary>
    /// <param name="stream">The stream positioned at the composition-to-decode payload.</param>
    /// <param name="boxLength">The validated composition-to-decode payload length.</param>
    /// <param name="composition">The offset range derived from the complete composition-offset table.</param>
    /// <param name="scratch">The parser-owned reusable scratch span.</param>
    private static void ParseCompositionToDecode(Stream stream, long boxLength, CompositionSummary composition, Span<byte> scratch)
    {
        ReadOnlySpan<byte> prefix = ReadPrefix(stream, boxLength, scratch, 4, "composition-to-decode");
        byte version = prefix[0];
        int fieldSize = version switch
        {
            0 => 4,
            1 => 8,
            _ => throw new InvalidImageContentException($"The composition-to-decode box has unsupported version {version}.")
        };

        int requiredLength = 4 + (fieldSize * 5);
        prefix = ReadPrefixFromStart(stream, boxLength, scratch, requiredLength, "composition-to-decode");
        if (boxLength != requiredLength || ReadFlags(prefix) != 0)
        {
            throw new InvalidImageContentException("The composition-to-decode box has unsupported flags or length.");
        }

        long shift = ReadSignedInteger(prefix[4..], fieldSize);
        long leastOffset = ReadSignedInteger(prefix[(4 + fieldSize)..], fieldSize);
        long greatestOffset = ReadSignedInteger(prefix[(4 + (fieldSize * 2))..], fieldSize);
        long compositionStart = ReadSignedInteger(prefix[(4 + (fieldSize * 3))..], fieldSize);
        long compositionEnd = ReadSignedInteger(prefix[(4 + (fieldSize * 4))..], fieldSize);
        long requiredShift = composition.LeastOffset < 0 ? checked(-composition.LeastOffset) : 0;
        if (shift < requiredShift
            || leastOffset != composition.LeastOffset
            || greatestOffset != composition.GreatestOffset
            || (compositionEnd != 0 && compositionEnd < compositionStart))
        {
            throw new InvalidImageContentException("The composition-to-decode box does not match the track's composition offsets.");
        }
    }

    /// <summary>
    /// Computes retained sample composition times while preserving decode-order storage.
    /// </summary>
    /// <param name="track">The selected track whose durations and offsets have been validated.</param>
    private static void SetCompositionTimes(HeifSequenceTrack track)
    {
        long decodeTime = 0;
        Span<HeifSequenceSample> samples = track.Samples;
        for (int i = 0; i < samples.Length; i++)
        {
            ref HeifSequenceSample sample = ref samples[i];
            sample.CompositionTime = sample.IsHidden ? long.MinValue : checked(decodeTime + sample.CompositionOffset);
            decodeTime = checked(decodeTime + sample.Duration);
        }
    }

    /// <summary>
    /// Reads one signed composition field of the version-selected fixed width.
    /// </summary>
    /// <param name="data">The field bytes.</param>
    /// <param name="fieldSize">The four-byte or eight-byte field width.</param>
    /// <returns>The signed field value.</returns>
    private static long ReadSignedInteger(ReadOnlySpan<byte> data, int fieldSize)
        => fieldSize == 4 ? BinaryPrimitives.ReadInt32BigEndian(data) : BinaryPrimitives.ReadInt64BigEndian(data);

    /// <summary>
    /// Parses the single normal-rate edit list used to signal image-sequence repetition.
    /// </summary>
    /// <param name="stream">The stream positioned at the edit-container payload.</param>
    /// <param name="boxLength">The validated edit-container payload length.</param>
    /// <param name="track">The selected track receiving repetition behavior.</param>
    /// <param name="scratch">The parser-owned reusable scratch span.</param>
    private static void ParseEdit(Stream stream, long boxLength, HeifSequenceTrack track, Span<byte> scratch)
    {
        long editEnd = checked(stream.Position + boxLength);
        BoxReference editList = default;
        while (stream.Position < editEnd)
        {
            long childLength = HeifBoxReader.ReadHeader(stream, editEnd, scratch, out Heif4CharCode childType);
            long childStart = stream.Position;
            if (childType == Heif4CharCode.Elst)
            {
                SetUnique(ref editList, childStart, childLength, "edit", childType);
            }

            stream.Position = checked(childStart + childLength);
        }

        if (!editList.IsPresent)
        {
            throw new InvalidImageContentException("The image-sequence edit container has no edit list.");
        }

        stream.Position = editList.Offset;
        ReadOnlySpan<byte> prefix = ReadPrefix(stream, editList.Length, scratch, 8, "edit list");
        byte version = prefix[0];
        uint flags = ReadFlags(prefix);
        int entryLength = version switch
        {
            0 => 12,
            1 => 20,
            _ => throw new InvalidImageContentException($"The edit list has unsupported version {version}.")
        };

        if ((flags & ~1U) != 0 || BinaryPrimitives.ReadUInt32BigEndian(prefix[4..]) != 1 || editList.Length != 8 + entryLength)
        {
            throw new InvalidImageContentException("The image-sequence edit list has unsupported flags, entries, or length.");
        }

        prefix = ReadPrefix(stream, entryLength, scratch, entryLength, "edit-list entry");
        ulong segmentDuration;
        long mediaTime;
        int rateOffset;
        if (version == 0)
        {
            segmentDuration = BinaryPrimitives.ReadUInt32BigEndian(prefix);
            mediaTime = BinaryPrimitives.ReadInt32BigEndian(prefix[4..]);
            rateOffset = 8;
        }
        else
        {
            segmentDuration = BinaryPrimitives.ReadUInt64BigEndian(prefix);
            mediaTime = BinaryPrimitives.ReadInt64BigEndian(prefix[8..]);
            rateOffset = 16;
        }

        if (segmentDuration == 0 || mediaTime != 0 || BinaryPrimitives.ReadInt16BigEndian(prefix[rateOffset..]) != 1
            || BinaryPrimitives.ReadInt16BigEndian(prefix[(rateOffset + 2)..]) != 0)
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
    /// <param name="colorTrack">The selected master color track.</param>
    /// <param name="alphaTrack">The optional linked alpha track.</param>
    private static void ValidateAlphaTrack(HeifSequenceTrack colorTrack, HeifSequenceTrack? alphaTrack)
    {
        if (alphaTrack is null)
        {
            return;
        }

        if (alphaTrack.TotalSampleCount != colorTrack.TotalSampleCount || alphaTrack.Samples.Length != colorTrack.Samples.Length)
        {
            throw new InvalidImageContentException("The alpha and color image-sequence tracks contain different sample counts.");
        }

        for (int i = 0; i < colorTrack.Samples.Length; i++)
        {
            ulong colorDuration = (ulong)colorTrack.Samples[i].Duration * alphaTrack.MediaTimescale;
            ulong alphaDuration = (ulong)alphaTrack.Samples[i].Duration * colorTrack.MediaTimescale;
            if (colorDuration != alphaDuration)
            {
                throw new InvalidImageContentException("The alpha and color image-sequence samples have different presentation durations.");
            }
        }
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
    /// Records one unique child box while retaining only its stream range.
    /// </summary>
    /// <param name="reference">The child reference owned by the bounded parent parser.</param>
    /// <param name="offset">The absolute payload offset.</param>
    /// <param name="length">The validated payload length.</param>
    /// <param name="parentName">The parent name used in malformed-image diagnostics.</param>
    /// <param name="boxType">The unique child box type.</param>
    private static void SetUnique(ref BoxReference reference, long offset, long length, string parentName, Heif4CharCode boxType)
    {
        if (reference.IsPresent)
        {
            throw new InvalidImageContentException($"The {parentName} box contains duplicate '{boxType}' boxes.");
        }

        reference = new BoxReference(offset, length, boxType);
    }

    /// <summary>
    /// Reads a fixed prefix from the stream's current position.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="boxLength">The validated enclosing payload length.</param>
    /// <param name="scratch">The reusable destination scratch span.</param>
    /// <param name="length">The required prefix length.</param>
    /// <param name="name">The payload name used in malformed-image diagnostics.</param>
    /// <returns>The requested prefix within <paramref name="scratch"/>.</returns>
    private static ReadOnlySpan<byte> ReadPrefix(Stream stream, long boxLength, Span<byte> scratch, int length, string name)
    {
        if (boxLength < length)
        {
            throw new InvalidImageContentException($"The {name} payload is truncated.");
        }

        Span<byte> destination = scratch[..length];
        HeifBoxReader.ReadExactly(stream, destination, $"The {name} payload is truncated.");
        return destination;
    }

    /// <summary>
    /// Rewinds to the start of a partially read payload and reads a larger fixed prefix.
    /// </summary>
    /// <param name="stream">The source stream positioned after a four-byte prefix.</param>
    /// <param name="boxLength">The validated enclosing payload length.</param>
    /// <param name="scratch">The reusable destination scratch span.</param>
    /// <param name="length">The required prefix length.</param>
    /// <param name="name">The payload name used in malformed-image diagnostics.</param>
    /// <returns>The requested prefix within <paramref name="scratch"/>.</returns>
    private static ReadOnlySpan<byte> ReadPrefixFromStart(Stream stream, long boxLength, Span<byte> scratch, int length, string name)
    {
        stream.Position -= 4;
        return ReadPrefix(stream, boxLength, scratch, length, name);
    }

    /// <summary>
    /// Reads the lower 24-bit flags field from a full-box prefix.
    /// </summary>
    /// <param name="data">The prefix beginning with version and flags.</param>
    /// <returns>The unsigned flags value.</returns>
    private static uint ReadFlags(ReadOnlySpan<byte> data) => BinaryPrimitives.ReadUInt32BigEndian(data) & 0x00FFFFFF;

    /// <summary>
    /// Requires a full-box prefix to use one supported version and flags value.
    /// </summary>
    /// <param name="data">The prefix beginning with version and flags.</param>
    /// <param name="version">The required version.</param>
    /// <param name="flags">The required flags.</param>
    /// <param name="name">The box name used in malformed-image diagnostics.</param>
    private static void EnsureVersionAndFlags(ReadOnlySpan<byte> data, byte version, uint flags, string name)
    {
        if (data[0] != version || ReadFlags(data) != flags)
        {
            throw new InvalidImageContentException($"The {name} box has unsupported version or flags.");
        }
    }

    /// <summary>
    /// Requires a full-box prefix to contain zero flags.
    /// </summary>
    /// <param name="data">The prefix beginning with version and flags.</param>
    /// <param name="name">The box name used in malformed-image diagnostics.</param>
    private static void EnsureZeroFlags(ReadOnlySpan<byte> data, string name)
    {
        if (ReadFlags(data) != 0)
        {
            throw new InvalidImageContentException($"The {name} box has unsupported flags.");
        }
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
        /// <param name="isEnabledInMovie">Whether the track is enabled and used in the movie presentation.</param>
        /// <param name="width">The displayed track width.</param>
        /// <param name="height">The displayed track height.</param>
        /// <param name="trackDuration">The track duration in movie-time-scale units.</param>
        /// <param name="matrix">The track presentation matrix.</param>
        public TrackIdentity(uint id, bool isEnabledInMovie, int width, int height, ulong trackDuration, HeifTrackMatrix matrix)
        {
            this.Id = id;
            this.IsEnabledInMovie = isEnabledInMovie;
            this.Width = width;
            this.Height = height;
            this.TrackDuration = trackDuration;
            this.Matrix = matrix;
            this.HandlerType = default;
            this.AuxiliaryForTrackId = 0;
            this.PremultipliedByTrackId = 0;
        }

        /// <summary>
        /// Gets the file-defined track identifier.
        /// </summary>
        public uint Id { get; }

        /// <summary>
        /// Gets a value indicating whether the track is enabled and used in the movie presentation.
        /// </summary>
        public bool IsEnabledInMovie { get; }

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
        /// Gets the track presentation matrix.
        /// </summary>
        public HeifTrackMatrix Matrix { get; }

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
    }

    /// <summary>
    /// Retains one unique child payload range without creating a general box object model.
    /// </summary>
    private struct BoxReference
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BoxReference"/> struct.
        /// </summary>
        /// <param name="offset">The absolute payload offset.</param>
        /// <param name="length">The validated payload length.</param>
        /// <param name="type">The child box type.</param>
        public BoxReference(long offset, long length, Heif4CharCode type)
        {
            this.Offset = offset;
            this.Length = length;
            this.Type = type;
            this.IsPresent = true;
        }

        /// <summary>
        /// Gets the absolute payload offset.
        /// </summary>
        public long Offset { get; }

        /// <summary>
        /// Gets the validated payload length.
        /// </summary>
        public long Length { get; }

        /// <summary>
        /// Gets or sets the child box type when one logical slot accepts multiple concrete box types.
        /// </summary>
        public Heif4CharCode Type { get; set; }

        /// <summary>
        /// Gets a value indicating whether the child was present.
        /// </summary>
        public bool IsPresent { get; }
    }

    /// <summary>
    /// Maps a one-based chunk run to its number of samples per chunk.
    /// </summary>
    private readonly struct SampleToChunkEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SampleToChunkEntry"/> struct.
        /// </summary>
        /// <param name="firstChunk">The one-based first chunk in the run.</param>
        /// <param name="samplesPerChunk">The number of samples stored in each run chunk.</param>
        public SampleToChunkEntry(uint firstChunk, uint samplesPerChunk)
        {
            this.FirstChunk = firstChunk;
            this.SamplesPerChunk = samplesPerChunk;
        }

        /// <summary>
        /// Gets the one-based first chunk in the run.
        /// </summary>
        public uint FirstChunk { get; }

        /// <summary>
        /// Gets the number of samples stored in each run chunk.
        /// </summary>
        public uint SamplesPerChunk { get; }
    }

    /// <summary>
    /// Contains the visible composition-offset range derived from a complete HEVC track.
    /// </summary>
    private readonly struct CompositionSummary
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CompositionSummary"/> struct.
        /// </summary>
        /// <param name="leastOffset">The smallest visible composition offset.</param>
        /// <param name="greatestOffset">The greatest visible composition offset.</param>
        /// <param name="hasHiddenSamples">Whether the track contains non-output samples.</param>
        public CompositionSummary(long leastOffset, long greatestOffset, bool hasHiddenSamples)
        {
            this.LeastOffset = leastOffset;
            this.GreatestOffset = greatestOffset;
            this.HasHiddenSamples = hasHiddenSamples;
        }

        /// <summary>
        /// Gets the smallest visible composition offset.
        /// </summary>
        public long LeastOffset { get; }

        /// <summary>
        /// Gets the greatest visible composition offset.
        /// </summary>
        public long GreatestOffset { get; }

        /// <summary>
        /// Gets a value indicating whether the track contains non-output samples.
        /// </summary>
        public bool HasHiddenSamples { get; }
    }

    /// <summary>
    /// Reads fixed-width sample-table values through one bounded reusable buffer.
    /// </summary>
    private ref struct TableReader
    {
        /// <summary>
        /// The source stream shared by the container parser.
        /// </summary>
        private readonly Stream stream;

        /// <summary>
        /// The parser-owned buffer reused for sequential values.
        /// </summary>
        private readonly Span<byte> buffer;

        /// <summary>
        /// The table name used in malformed-image diagnostics.
        /// </summary>
        private readonly string name;

        /// <summary>
        /// The number of bytes not yet loaded from the bounded table payload.
        /// </summary>
        private long remaining;

        /// <summary>
        /// The next unread byte in <see cref="buffer"/>.
        /// </summary>
        private int offset;

        /// <summary>
        /// The number of valid bytes currently stored in <see cref="buffer"/>.
        /// </summary>
        private int count;

        /// <summary>
        /// Initializes a new instance of the <see cref="TableReader"/> struct.
        /// </summary>
        /// <param name="stream">The stream positioned at the table entries.</param>
        /// <param name="length">The exact number of bounded entry bytes.</param>
        /// <param name="buffer">The parser-owned reusable buffer.</param>
        /// <param name="name">The table name used in malformed-image diagnostics.</param>
        public TableReader(Stream stream, long length, Span<byte> buffer, string name)
        {
            this.stream = stream;
            this.buffer = buffer;
            this.name = name;
            this.remaining = length;
            this.offset = 0;
            this.count = 0;
        }

        /// <summary>
        /// Reads one unsigned byte from the bounded table.
        /// </summary>
        /// <returns>The next byte.</returns>
        public byte ReadByte()
        {
            this.Ensure(1);
            return this.buffer[this.offset++];
        }

        /// <summary>
        /// Reads one big-endian unsigned 16-bit value from the bounded table.
        /// </summary>
        /// <returns>The next 16-bit value.</returns>
        public ushort ReadUInt16()
        {
            this.Ensure(2);
            ushort value = BinaryPrimitives.ReadUInt16BigEndian(this.buffer[this.offset..]);
            this.offset += 2;
            return value;
        }

        /// <summary>
        /// Reads one big-endian unsigned 32-bit value from the bounded table.
        /// </summary>
        /// <returns>The next 32-bit value.</returns>
        public uint ReadUInt32()
        {
            this.Ensure(4);
            uint value = BinaryPrimitives.ReadUInt32BigEndian(this.buffer[this.offset..]);
            this.offset += 4;
            return value;
        }

        /// <summary>
        /// Reads one big-endian unsigned 64-bit value from the bounded table.
        /// </summary>
        /// <returns>The next 64-bit value.</returns>
        public ulong ReadUInt64()
        {
            this.Ensure(8);
            ulong value = BinaryPrimitives.ReadUInt64BigEndian(this.buffer[this.offset..]);
            this.offset += 8;
            return value;
        }

        /// <summary>
        /// Refills the reusable buffer without reading beyond the bounded table payload.
        /// </summary>
        /// <param name="required">The number of contiguous bytes required by the next value.</param>
        private void Ensure(int required)
        {
            int buffered = this.count - this.offset;
            if (buffered >= required)
            {
                return;
            }

            if (buffered > 0)
            {
                this.buffer.Slice(this.offset, buffered).CopyTo(this.buffer);
            }

            this.offset = 0;
            this.count = buffered;
            while (this.count < required && this.remaining > 0)
            {
                int requested = (int)Math.Min(this.buffer.Length - this.count, this.remaining);
                int read = this.stream.Read(this.buffer.Slice(this.count, requested));
                if (read == 0)
                {
                    throw new InvalidImageContentException($"The {this.name} table is truncated.");
                }

                this.count += read;
                this.remaining -= read;
            }

            if (this.count < required)
            {
                throw new InvalidImageContentException($"The {this.name} table is truncated.");
            }
        }
    }
}
