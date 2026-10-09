// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Parses AV1 open bitstream units and supplies decoded tile payloads to an AV1 tile reader.
/// </summary>
internal sealed class ObuReader
{
    /// <summary>
    /// The zero-based sequence-header operating-point index selected by the container.
    /// </summary>
    private readonly byte operatingPointIndex;

    /// <summary>
    /// The reconstructed frames retained by the owning decoder for inter-frame syntax and prediction.
    /// </summary>
    private readonly Av1ReferenceFrameStore? referenceFrames;

    /// <summary>
    /// The completed frame-identifier, validity, and order-hint state retained across frame headers in this session.
    /// </summary>
    private ObuFrameReferenceState frameReferenceState;

    /// <summary>
    /// The temporal- and spatial-layer mask for the selected operating point.
    /// </summary>
    private uint currentOperatingPointIdc;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObuReader"/> class using operating-point index zero without a reconstructed reference map.
    /// </summary>
    public ObuReader()
        : this(0)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ObuReader"/> class for one selected AV1 operating point without a reconstructed reference map.
    /// </summary>
    /// <param name="operatingPointIndex">The zero-based sequence-header operating-point index to decode.</param>
    public ObuReader(byte operatingPointIndex)
        => this.operatingPointIndex = operatingPointIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObuReader"/> class for one selected AV1 operating point and retained reference map.
    /// </summary>
    /// <param name="operatingPointIndex">The zero-based sequence-header operating-point index to decode.</param>
    /// <param name="referenceFrames">The reconstructed reference frames retained by the owning decoder.</param>
    public ObuReader(byte operatingPointIndex, Av1ReferenceFrameStore referenceFrames)
    {
        this.operatingPointIndex = operatingPointIndex;
        this.referenceFrames = referenceFrames;
    }

    /// <summary>
    /// Supplies a tile reader while it keeps the fixed-reader and factory call contracts separate.
    /// </summary>
    /// <remarks>
    /// The generic struct providers allocate no closure for a fixed-reader payload. No caller can pass an invalid pair of nullable arguments.
    /// </remarks>
    private interface ITileReaderProvider
    {
        /// <summary>
        /// Gets the tile reader for the current frame.
        /// </summary>
        /// <returns>The tile reader.</returns>
        IAv1TileReader Get();
    }

    /// <summary>
    /// Gets or sets the most recently parsed sequence header.
    /// </summary>
    public ObuSequenceHeader? SequenceHeader { get; set; }

    /// <summary>
    /// Gets or sets the frame header associated with the current coded frame.
    /// </summary>
    public ObuFrameHeader? FrameHeader { get; set; }

    /// <summary>
    /// Gets content light-level metadata parsed from the current AV1 session.
    /// </summary>
    public HeifContentLightLevel? ContentLightLevel { get; private set; }

    /// <summary>
    /// Gets mastering-display color-volume metadata parsed from the current AV1 session.
    /// </summary>
    public HeifMasteringDisplayColorVolume? MasteringDisplayColorVolume { get; private set; }

    /// <summary>
    /// Gets the sequence header established before frame-dependent syntax is read.
    /// </summary>
    public ObuSequenceHeader CurrentSequenceHeader =>
        this.SequenceHeader
        ?? throw new InvalidImageContentException("AV1 frame syntax appeared before a sequence header.");

    /// <summary>
    /// Gets the frame header established before frame-dependent syntax is read.
    /// </summary>
    public ObuFrameHeader CurrentFrameHeader =>
        this.FrameHeader
        ?? throw new InvalidImageContentException("AV1 tile syntax appeared before a frame header.");

    /// <summary>
    /// Clears metadata retained from the preceding bounded AV1 image payload.
    /// </summary>
    public void ResetMetadata()
    {
        this.ContentLightLevel = null;
        this.MasteringDisplayColorVolume = null;
    }

    /// <summary>
    /// Parses every open bitstream unit in one bounded AV1 payload.
    /// </summary>
    /// <param name="reader">The reader positioned at the first OBU.</param>
    /// <param name="dataSize">The number of bytes available for the bounded payload.</param>
    /// <param name="creator">Creates one tile reader when the first tile payload of each coded frame is encountered.</param>
    /// <param name="isAnnexB">A value indicating whether each OBU is prefixed by an Annex B length field.</param>
    public void ReadAll(ref Av1BitStreamReader reader, int dataSize, Func<IAv1TileReader> creator, bool isAnnexB = false)
    {
        ArgumentNullException.ThrowIfNull(creator);

        this.ReadAll(ref reader, dataSize, new TileReaderFactoryProvider(creator), isAnnexB);
    }

    /// <summary>
    /// Parses every open bitstream unit in one bounded AV1 payload using one existing tile reader.
    /// </summary>
    /// <param name="reader">The reader positioned at the first OBU.</param>
    /// <param name="dataSize">The number of bytes available for the bounded payload.</param>
    /// <param name="tileReader">The tile reader used for each coded frame in the payload.</param>
    /// <param name="isAnnexB">A value indicating whether each OBU is prefixed by an Annex B length field.</param>
    public void ReadAll(ref Av1BitStreamReader reader, int dataSize, IAv1TileReader tileReader, bool isAnnexB = false)
    {
        ArgumentNullException.ThrowIfNull(tileReader);

        this.ReadAll(ref reader, dataSize, new FixedTileReaderProvider(tileReader), isAnnexB);
    }

    /// <summary>
    /// Parses every open bitstream unit in one bounded AV1 payload.
    /// </summary>
    /// <param name="reader">The reader positioned at the first OBU.</param>
    /// <param name="dataSize">The number of bytes available for the bounded payload.</param>
    /// <typeparam name="TTileReaderProvider">The non-allocating tile-reader source used by this payload.</typeparam>
    /// <param name="tileReaderProvider">Provides one tile reader for each coded frame.</param>
    /// <param name="isAnnexB">A value indicating whether each OBU is prefixed by an Annex B length field.</param>
    private void ReadAll<TTileReaderProvider>(
        ref Av1BitStreamReader reader,
        int dataSize,
        TTileReaderProvider tileReaderProvider,
        bool isAnnexB)
        where TTileReaderProvider : struct, ITileReaderProvider
    {
        bool completed = false;

        try
        {
            int availableByteCount = reader.Length - Av1Math.DivideBy8Floor(reader.BitPosition);
            if ((reader.BitPosition & 0x7) != 0 || (uint)dataSize > (uint)availableByteCount)
            {
                throw new InvalidImageContentException("The AV1 OBU data boundary is invalid.");
            }

            bool seenFrameHeader = false;
            int nextTileStart = 0;
            Span<byte> primaryFrameHeaderPayload = default;
            IAv1TileReader? activeDecoder = null;

            while (dataSize > 0)
            {
                int annexObuSize = 0;
                if (isAnnexB)
                {
                    ReadObuSize(ref reader, out annexObuSize, out int annexLengthSize);
                    if (annexLengthSize > dataSize || annexObuSize < 1)
                    {
                        throw new InvalidImageContentException("The Annex B AV1 OBU length is invalid.");
                    }

                    dataSize -= annexLengthSize;
                    if (annexObuSize > dataSize)
                    {
                        throw new InvalidImageContentException("The Annex B AV1 OBU exceeds its temporal-unit boundary.");
                    }
                }
                else if (dataSize < 1)
                {
                    throw new InvalidImageContentException("The AV1 OBU header is truncated.");
                }

                int obuStartBitPosition = reader.BitPosition;
                ObuHeader header = ReadObuHeaderSize(ref reader, out _);
                int headerAndLengthSize = (reader.BitPosition - obuStartBitPosition) >> 3;
                int boundedObuSize = isAnnexB ? annexObuSize : dataSize;
                if (headerAndLengthSize > boundedObuSize)
                {
                    throw new InvalidImageContentException("The AV1 OBU header exceeds its declared boundary.");
                }

                // AV1-ISOBMFF permits the final low-overhead OBU to omit its size field. In that form, the remaining sample bytes are the payload.
                // No boundary follows, so this OBU is also the final OBU.
                int payloadSize = header.HasSize ? header.PayloadSize : boundedObuSize - headerAndLengthSize;
                if ((uint)payloadSize > (uint)(boundedObuSize - headerAndLengthSize))
                {
                    throw new InvalidImageContentException("The AV1 OBU payload exceeds its declared boundary.");
                }

                int completeObuSize = headerAndLengthSize + payloadSize;
                if (isAnnexB && completeObuSize != annexObuSize)
                {
                    throw new InvalidImageContentException("The nested and Annex B AV1 OBU lengths do not match.");
                }

                dataSize -= isAnnexB ? annexObuSize : completeObuSize;
                header.PayloadSize = payloadSize;

                // A dedicated payload reader prevents malformed syntax from consuming the following OBU. The parent advances once here. As a
                // result, the reader skips ignored metadata, padding, and reserved OBUs without a copy.
                Span<byte> obuPayload = reader.ReadBytes(payloadSize);

                // AV1 `operating_point_idc` uses bits 0-7 for temporal IDs and bits 8-11 for spatial IDs. An extended OBU belongs to the
                // operating point only when both of its bits are set. An all-zero mask and OBUs without an extension apply to all layers. Sequence
                // headers set the mask and temporal delimiters set the framing. As a result, the reader never drops them, even when their
                // extension identifies a layer outside the selected operating point.
                bool isOperatingPointIndependent = header.Type is ObuType.SequenceHeader or ObuType.TemporalDelimiter;
                bool isInCurrentOperatingPoint = this.currentOperatingPointIdc == 0
                    || !header.HasExtension
                    || (((this.currentOperatingPointIdc >> header.TemporalId) & 1U) != 0
                        && ((this.currentOperatingPointIdc >> (header.SpatialId + 8)) & 1U) != 0);

                if (!isOperatingPointIndependent && !isInCurrentOperatingPoint)
                {
                    continue;
                }

                Av1BitStreamReader payloadReader = new(obuPayload);
                IAv1TileReader? decoderToComplete = null;
                int decodedPayloadSize;

                switch (header.Type)
                {
                    case ObuType.SequenceHeader:
                        if (seenFrameHeader)
                        {
                            throw new InvalidImageContentException("An AV1 sequence header interrupts an incomplete coded frame.");
                        }

                        this.SequenceHeader = new();
                        ReadSequenceHeader(ref payloadReader, this.SequenceHeader);
                        if (this.operatingPointIndex >= this.SequenceHeader.OperatingPoint.Length)
                        {
                            throw new InvalidImageContentException(
                                $"The AV1 operating-point selector requests index {this.operatingPointIndex}, " +
                                $"but the sequence header declares {this.SequenceHeader.OperatingPoint.Length} operating points.");
                        }

                        this.currentOperatingPointIdc = this.SequenceHeader.OperatingPoint[this.operatingPointIndex].Idc;

                        // A sequence header starts a new reference state. The reader clears the syntax state and the decoded frames only after it
                        // accepts the complete header and the selected operating point. As a result, a later inter header cannot pair an empty
                        // parser map with samples from the old sequence.
                        this.frameReferenceState.Reset();
                        this.referenceFrames?.Reset();
                        decodedPayloadSize = Av1Math.DivideBy8Floor(payloadReader.BitPosition);
                        break;
                    case ObuType.FrameHeader:
                        if (this.SequenceHeader is null)
                        {
                            throw new InvalidImageContentException("An AV1 frame header appears before its sequence header.");
                        }

                        if (seenFrameHeader)
                        {
                            throw new InvalidImageContentException("An AV1 frame contains more than one primary frame header.");
                        }

                        seenFrameHeader = true;
                        ObuFrameHeader primaryFrameHeader = new()
                        {
                            TemporalId = header.TemporalId,
                            SpatialId = header.SpatialId
                        };

                        this.frameReferenceState.InitializeFrameHeader(primaryFrameHeader);
                        this.FrameHeader = primaryFrameHeader;
                        this.ReadFrameHeader(ref payloadReader, header, trailingBit: true);
                        decodedPayloadSize = Av1Math.DivideBy8Floor(payloadReader.BitPosition);
                        primaryFrameHeaderPayload = obuPayload[..decodedPayloadSize];

                        if (primaryFrameHeader.ShowExistingFrame)
                        {
                            // This header completes the frame with retained samples. No tile group belongs to it.
                            activeDecoder ??= tileReaderProvider.Get();
                            decoderToComplete = activeDecoder;
                        }

                        break;
                    case ObuType.RedundantFrameHeader:
                        if (!seenFrameHeader)
                        {
                            throw new InvalidImageContentException("A redundant AV1 frame header appears before its primary frame header.");
                        }

                        if (primaryFrameHeaderPayload.Length > obuPayload.Length
                            || !obuPayload[..primaryFrameHeaderPayload.Length].SequenceEqual(primaryFrameHeaderPayload))
                        {
                            throw new InvalidImageContentException("The redundant AV1 frame header does not match its primary header.");
                        }

                        // The primary header already owns the decoded frame state. A compare of its encoded bytes avoids a second parse of the
                        // same frame-header syntax.
                        decodedPayloadSize = primaryFrameHeaderPayload.Length;
                        break;
                    case ObuType.Frame:
                        if (this.SequenceHeader is null)
                        {
                            throw new InvalidImageContentException("An AV1 frame appears before its sequence header.");
                        }

                        if (seenFrameHeader)
                        {
                            throw new InvalidImageContentException("A combined AV1 frame OBU follows a separate frame header.");
                        }

                        seenFrameHeader = true;
                        ObuFrameHeader combinedFrameHeader = new()
                        {
                            TemporalId = header.TemporalId,
                            SpatialId = header.SpatialId
                        };

                        this.frameReferenceState.InitializeFrameHeader(combinedFrameHeader);
                        this.FrameHeader = combinedFrameHeader;
                        this.ReadFrameHeader(ref payloadReader, header, trailingBit: false);
                        primaryFrameHeaderPayload = obuPayload[..Av1Math.DivideBy8Floor(payloadReader.BitPosition)];

                        if (combinedFrameHeader.ShowExistingFrame)
                        {
                            // AV1 permits `show_existing_frame` only in a standalone frame-header OBU. A combined frame OBU must continue with a
                            // tile group, so it cannot use the header-only form that shows a retained frame.
                            throw new InvalidImageContentException("A combined AV1 frame OBU cannot display an existing frame.");
                        }

                        goto TILE_GROUP;
                    case ObuType.TileGroup:
                        TILE_GROUP:
                        if (!seenFrameHeader)
                        {
                            throw new InvalidImageContentException("An AV1 tile group appears before its frame header.");
                        }

                        activeDecoder ??= tileReaderProvider.Get();

                        // A combined frame OBU reaches this label after the reader consumes its frame-header part. The rest is the same tile-group
                        // syntax as a standalone tile OBU.
                        this.ReadTileGroup(ref payloadReader, activeDecoder, header, ref nextTileStart, out bool frameDecodingFinished);
                        if (frameDecodingFinished)
                        {
                            decoderToComplete = activeDecoder;
                        }

                        decodedPayloadSize = Av1Math.DivideBy8Floor(payloadReader.BitPosition);
                        break;
                    case ObuType.TemporalDelimiter:
                        if (seenFrameHeader)
                        {
                            throw new InvalidImageContentException("An AV1 temporal delimiter interrupts an incomplete coded frame.");
                        }

                        // AV1 section 5.6 defines no delimiter syntax. The common check after the switch permits zero bytes between the empty
                        // syntax and the declared payload boundary.
                        decodedPayloadSize = 0;
                        break;
                    case ObuType.Metadata:
                        decodedPayloadSize = this.ReadMetadata(obuPayload);
                        break;
                    case ObuType.TileList:
                        // Tile-list OBUs require AV1 large-scale tile mode, which this decoder does not implement. The reader rejects the syntax, so
                        // it never returns a partial reconstruction without an error.
                        throw new InvalidImageContentException("AV1 tile-list OBUs are not supported.");
                    case ObuType.Padding:
                        int lastNonzeroIndex = obuPayload.Length - 1;
                        while (lastNonzeroIndex >= 0 && obuPayload[lastNonzeroIndex] == 0)
                        {
                            lastNonzeroIndex--;
                        }

                        // AV1 padding contains only its trailing one bit and optional zero bytes. A header-only padding OBU is also valid, so an
                        // empty payload skips this final-byte check.
                        if (lastNonzeroIndex >= 0 && obuPayload[lastNonzeroIndex] != 0x80)
                        {
                            throw new InvalidImageContentException("The AV1 padding OBU has invalid trailing bits.");
                        }

                        if (obuPayload.Length > 0 && lastNonzeroIndex < 0)
                        {
                            throw new InvalidImageContentException("The AV1 padding OBU is missing its trailing one bit.");
                        }

                        decodedPayloadSize = payloadSize;
                        break;
                    default:
                        // Reserved OBUs do not contribute to this still-image reconstruction. The parent reader already skipped their declared
                        // payload. The reader rejects a nonempty unrecognized payload that contains only zeros, because it has no trailing one bit.
                        if (payloadSize > 0)
                        {
                            int ignoredLastNonzeroIndex = payloadSize - 1;
                            while (ignoredLastNonzeroIndex >= 0 && obuPayload[ignoredLastNonzeroIndex] == 0)
                            {
                                ignoredLastNonzeroIndex--;
                            }

                            if (ignoredLastNonzeroIndex < 0)
                            {
                                throw new InvalidImageContentException("The ignored AV1 OBU is missing its trailing one bit.");
                            }
                        }

                        decodedPayloadSize = payloadSize;
                        break;
                }

                // Inside its declared OBU payload, only zero bytes can follow the parsed syntax. Ignored metadata and reserved OBUs set
                // `decodedPayloadSize` to the full payload, because the reader does not consume their syntax here.
                for (int i = decodedPayloadSize; i < obuPayload.Length; i++)
                {
                    if (obuPayload[i] != 0)
                    {
                        throw new InvalidImageContentException("The AV1 OBU contains nonzero data after its decoded syntax.");
                    }
                }

                if (decoderToComplete is not null)
                {
                    // The reconstruction and the reference-buffer ownership complete before the reader publishes the matching syntax state. If
                    // the decoder fails, the earlier session state stays intact for deterministic cleanup.
                    decoderToComplete.CompleteFrame();
                    this.frameReferenceState.CompleteFrame(
                        this.CurrentFrameHeader,
                        this.CurrentSequenceHeader.IsFrameIdNumbersPresent);

                    activeDecoder = null;
                    seenFrameHeader = false;
                    nextTileStart = 0;
                    primaryFrameHeaderPayload = default;
                }
            }

            if (seenFrameHeader || activeDecoder is not null)
            {
                throw new InvalidImageContentException("The AV1 payload ends before the current coded frame is complete.");
            }

            completed = true;
        }
        finally
        {
            if (!completed)
            {
                // A bounded payload can commit earlier layers before a later OBU fails. After the reader releases the displaced owners, it
                // cannot roll back those changes. As a result, it resets the complete decoder session.
                this.Reset();
            }
        }
    }

    /// <summary>
    /// Clears all parser, tile-reader, syntax-reference, and reconstructed-reference state owned by this session.
    /// </summary>
    public void Reset()
    {
        this.SequenceHeader = null;
        this.FrameHeader = null;
        this.ResetMetadata();
        this.currentOperatingPointIdc = 0;
        this.frameReferenceState.Reset();
        this.referenceFrames?.Reset();
    }

    /// <summary>
    /// Reads the fixed OBU header and optional extension fields.
    /// </summary>
    /// <param name="reader">The reader positioned at an OBU header.</param>
    /// <returns>The parsed OBU header.</returns>
    private static ObuHeader ReadObuHeader(ref Av1BitStreamReader reader)
    {
        ObuHeader header = new();
        if (reader.ReadBoolean())
        {
            throw new ImageFormatException("Forbidden bit in header should be unset.");
        }

        header.Size = 1;
        header.Type = (ObuType)reader.ReadLiteral(4);
        header.HasExtension = reader.ReadBoolean();
        header.HasSize = reader.ReadBoolean();

        // The reader consumes `obu_reserved_1bit` and accepts either value. Reserved fields do not change the decoded syntax, so this keeps
        // forward-compatible framing. The forbidden bit above stays a hard error.
        _ = reader.ReadBoolean();

        if (header.HasExtension)
        {
            header.Size++;
            header.TemporalId = (int)reader.ReadLiteral(3);
            header.SpatialId = (int)reader.ReadLiteral(2);

            // The reader also consumes `extension_header_reserved_3bits` and ignores their value.
            _ = reader.ReadLiteral(3);
        }
        else
        {
            header.SpatialId = 0;
            header.TemporalId = 0;
        }

        return header;
    }

    /// <summary>
    /// Reads an OBU size encoded as an unsigned little-endian base-128 value.
    /// </summary>
    /// <param name="reader">The reader positioned at the size value.</param>
    /// <param name="obuSize">The decoded OBU size.</param>
    /// <param name="lengthSize">The number of bytes occupied by the encoded size.</param>
    private static void ReadObuSize(ref Av1BitStreamReader reader, out int obuSize, out int lengthSize)
    {
        ulong rawSize = reader.ReadLittleEndianBytes128(out lengthSize);
        if (rawSize > int.MaxValue)
        {
            throw new InvalidImageContentException("The AV1 OBU size exceeds the supported image payload limit.");
        }

        obuSize = (int)rawSize;
    }

    /// <summary>
    /// Reads an OBU header followed by its optional payload-size field.
    /// </summary>
    /// <param name="reader">The reader positioned at an OBU header.</param>
    /// <param name="lengthSize">The number of bytes occupied by the payload-size field.</param>
    /// <returns>The parsed OBU header and payload size.</returns>
    private static ObuHeader ReadObuHeaderSize(ref Av1BitStreamReader reader, out int lengthSize)
    {
        ObuHeader header = ReadObuHeader(ref reader);
        lengthSize = 0;
        if (header.HasSize)
        {
            ReadObuSize(ref reader, out int payloadSize, out lengthSize);
            header.PayloadSize = payloadSize;
        }

        return header;
    }

    /// <summary>
    /// Reads and validates the trailing one bit followed by zero padding.
    /// </summary>
    /// <param name="reader">The reader positioned at the trailing bits.</param>
    /// <remarks>Consumes a byte, if already byte aligned before the check.</remarks>
    private static void ReadTrailingBits(ref Av1BitStreamReader reader)
    {
        int bitsBeforeAlignment = 8 - (reader.BitPosition & 0x7);
        uint trailing = reader.ReadLiteral(bitsBeforeAlignment);
        if (trailing != (1U << (bitsBeforeAlignment - 1)))
        {
            throw new ImageFormatException("Trailing bits not properly formatted.");
        }
    }

    /// <summary>
    /// Consumes zero padding until the reader reaches a byte boundary.
    /// </summary>
    /// <param name="reader">The reader to align.</param>
    private static void AlignToByteBoundary(ref Av1BitStreamReader reader)
    {
        int alignmentStartPosition = reader.BitPosition;
        while ((reader.BitPosition & 0x7) > 0)
        {
            int paddingBitPosition = reader.BitPosition;
            if (reader.ReadBoolean())
            {
                string message =
                    $"Incorrect byte alignment padding bit at offset {paddingBitPosition}; alignment started at offset {alignmentStartPosition}.";

                throw new ImageFormatException(message);
            }
        }
    }

    /// <summary>
    /// Reads and validates one AV1 metadata OBU payload.
    /// </summary>
    /// <param name="payload">The bounded metadata payload.</param>
    /// <returns>The number of payload bytes occupied by decoded syntax.</returns>
    private int ReadMetadata(Span<byte> payload)
    {
        int metadataOffset = 0;
        ulong metadataTypeValue = ReadMetadataType(payload, ref metadataOffset);
        Span<byte> metadataPayload = payload[metadataOffset..];

        if (metadataTypeValue == (ulong)ObuMetadataType.Reserved
            || metadataTypeValue > (ulong)ObuMetadataType.Timecode)
        {
            // Reserved and private metadata have no syntax that the decoder can interpret. The opaque payload includes the trailing bit, so it
            // must still contain at least one nonzero byte.
            if (FindLastNonzeroByteIndex(metadataPayload) < 0)
            {
                throw new InvalidImageContentException("The AV1 metadata OBU is missing its trailing one bit.");
            }

            return payload.Length;
        }

        ObuMetadataType metadataType = (ObuMetadataType)metadataTypeValue;
        if (metadataType is ObuMetadataType.HdrCll or ObuMetadataType.HdrMdcv)
        {
            Av1CodecConfiguration.ReadHdrMetadata(
                payload,
                "AV1 metadata OBU",
                out HeifContentLightLevel? contentLightLevel,
                out HeifMasteringDisplayColorVolume? masteringDisplayColorVolume);

            this.ContentLightLevel = contentLightLevel ?? this.ContentLightLevel;
            this.MasteringDisplayColorVolume = masteringDisplayColorVolume ?? this.MasteringDisplayColorVolume;
            return payload.Length;
        }

        if (metadataType == ObuMetadataType.ItutT35)
        {
            ValidateItutT35Metadata(metadataPayload);
            return payload.Length;
        }

        Av1BitStreamReader metadataReader = new(metadataPayload);
        if (metadataType == ObuMetadataType.Scalability)
        {
            ReadScalabilityMetadata(ref metadataReader);
        }
        else
        {
            ReadTimecodeMetadata(ref metadataReader);
        }

        ReadTrailingBits(ref metadataReader);
        return metadataOffset + Av1Math.DivideBy8Floor(metadataReader.BitPosition);
    }

    /// <summary>
    /// Reads the unsigned little-endian base-128 metadata type.
    /// </summary>
    /// <param name="payload">The bounded metadata payload.</param>
    /// <param name="offset">The byte offset, advanced past the metadata type.</param>
    /// <returns>The decoded metadata type.</returns>
    private static ulong ReadMetadataType(ReadOnlySpan<byte> payload, ref int offset)
    {
        ulong value = 0;
        for (int i = 0; i < 8; i++)
        {
            if ((uint)offset >= (uint)payload.Length)
            {
                throw new InvalidImageContentException("The AV1 metadata type is truncated.");
            }

            byte current = payload[offset++];
            value |= (ulong)(current & 0x7F) << (i * 7);
            if ((current & 0x80) == 0)
            {
                return value;
            }
        }

        throw new InvalidImageContentException("The AV1 metadata type exceeds the permitted LEB128 length.");
    }

    /// <summary>
    /// Validates byte-aligned ITU-T T.35 metadata syntax and trailing bits.
    /// </summary>
    /// <param name="payload">The metadata bytes following the metadata type.</param>
    private static void ValidateItutT35Metadata(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
        {
            throw new InvalidImageContentException("The AV1 ITU-T T.35 country code is missing.");
        }

        int countryCodeSize = payload[0] == 0xFF ? 2 : 1;
        if (payload.Length < countryCodeSize)
        {
            throw new InvalidImageContentException("The AV1 ITU-T T.35 country-code extension byte is missing.");
        }

        int lastNonzeroIndex = FindLastNonzeroByteIndex(payload);
        if (lastNonzeroIndex < countryCodeSize || payload[lastNonzeroIndex] != 0x80)
        {
            throw new InvalidImageContentException("The AV1 ITU-T T.35 metadata has invalid trailing bits.");
        }
    }

    /// <summary>
    /// Reads scalability metadata syntax so its bounded payload and trailing bits can be validated.
    /// </summary>
    /// <param name="reader">The metadata payload reader.</param>
    private static void ReadScalabilityMetadata(ref Av1BitStreamReader reader)
    {
        const uint scalabilityStructureMode = 14;
        uint scalabilityMode = reader.ReadLiteral(8);
        if (scalabilityMode != scalabilityStructureMode)
        {
            return;
        }

        int spatialLayerCount = (int)reader.ReadLiteral(2) + 1;
        bool hasSpatialLayerDimensions = reader.ReadBoolean();
        bool hasSpatialLayerDescriptions = reader.ReadBoolean();
        bool hasTemporalGroupDescriptions = reader.ReadBoolean();

        // AV1 reserves these three bits and requires decoders to consume and ignore them.
        _ = reader.ReadLiteral(3);

        if (hasSpatialLayerDimensions)
        {
            for (int i = 0; i < spatialLayerCount; i++)
            {
                _ = reader.ReadLiteral(16);
                _ = reader.ReadLiteral(16);
            }
        }

        if (hasSpatialLayerDescriptions)
        {
            for (int i = 0; i < spatialLayerCount; i++)
            {
                _ = reader.ReadLiteral(8);
            }
        }

        if (hasTemporalGroupDescriptions)
        {
            int temporalGroupSize = (int)reader.ReadLiteral(8);
            for (int i = 0; i < temporalGroupSize; i++)
            {
                _ = reader.ReadLiteral(3);
                _ = reader.ReadBoolean();
                _ = reader.ReadBoolean();
                int referenceCount = (int)reader.ReadLiteral(3);

                for (int j = 0; j < referenceCount; j++)
                {
                    _ = reader.ReadLiteral(8);
                }
            }
        }
    }

    /// <summary>
    /// Reads timecode metadata syntax so its bounded payload and trailing bits can be validated.
    /// </summary>
    /// <param name="reader">The metadata payload reader.</param>
    private static void ReadTimecodeMetadata(ref Av1BitStreamReader reader)
    {
        _ = reader.ReadLiteral(5);
        bool hasFullTimestamp = reader.ReadBoolean();
        _ = reader.ReadBoolean();
        _ = reader.ReadBoolean();
        _ = reader.ReadLiteral(9);

        if (hasFullTimestamp)
        {
            _ = reader.ReadLiteral(6);
            _ = reader.ReadLiteral(6);
            _ = reader.ReadLiteral(5);
        }
        else if (reader.ReadBoolean())
        {
            _ = reader.ReadLiteral(6);
            if (reader.ReadBoolean())
            {
                _ = reader.ReadLiteral(6);
                if (reader.ReadBoolean())
                {
                    _ = reader.ReadLiteral(5);
                }
            }
        }

        int timeOffsetLength = (int)reader.ReadLiteral(5);
        if (timeOffsetLength > 0)
        {
            _ = reader.ReadLiteral(timeOffsetLength);
        }
    }

    /// <summary>
    /// Finds the final nonzero byte in one bounded payload.
    /// </summary>
    /// <param name="payload">The payload to inspect.</param>
    /// <returns>The final nonzero byte index, or <c>-1</c> when every byte is zero.</returns>
    private static int FindLastNonzeroByteIndex(ReadOnlySpan<byte> payload)
    {
        for (int i = payload.Length - 1; i >= 0; i--)
        {
            if (payload[i] != 0)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Computes the mode-information dimensions and stride for the current frame.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header defining the maximum frame geometry and superblock size.</param>
    private void ComputeImageSize(ObuSequenceHeader sequenceHeader)
    {
        ObuFrameHeader frameHeader = this.CurrentFrameHeader;
        frameHeader.ModeInfoColumnCount = 2 * ((frameHeader.FrameSize.FrameWidth + 7) >> 3);
        frameHeader.ModeInfoRowCount = 2 * ((frameHeader.FrameSize.FrameHeight + 7) >> 3);
        frameHeader.ModeInfoStride = Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameWidth, Av1Constants.MaxSuperBlockSizeLog2) >> Av1Constants.ModeInfoSizeLog2;
    }

    /// <summary>
    /// Reads an AV1 sequence-header OBU payload.
    /// </summary>
    /// <param name="reader">The reader positioned at the sequence-header payload.</param>
    /// <param name="sequenceHeader">The sequence header to populate.</param>
    public static void ReadSequenceHeader(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader)
    {
        sequenceHeader.SequenceProfile = (ObuSequenceProfile)reader.ReadLiteral(3);
        if (sequenceHeader.SequenceProfile > Av1Constants.MaxSequenceProfile)
        {
            throw new ImageFormatException("Unknown sequence profile.");
        }

        sequenceHeader.IsStillPicture = reader.ReadBoolean();
        sequenceHeader.IsReducedStillPictureHeader = reader.ReadBoolean();
        if (!sequenceHeader.IsStillPicture && sequenceHeader.IsReducedStillPictureHeader)
        {
            // The reduced header omits state that a multi-frame sequence requires. As a result, AV1 permits it only when the sequence declares
            // that it contains a single still picture.
            throw new InvalidImageContentException("An AV1 reduced still-picture header requires the still-picture flag.");
        }

        if (sequenceHeader.IsReducedStillPictureHeader)
        {
            sequenceHeader.TimingInfo = null;
            sequenceHeader.DecoderModelInfoPresentFlag = false;
            sequenceHeader.InitialDisplayDelayPresentFlag = false;
            ObuOperatingPoint operatingPoint = new();
            sequenceHeader.OperatingPoint[0] = operatingPoint;
            operatingPoint.OperatorIndex = 0;
            operatingPoint.SequenceLevelIndex = (int)reader.ReadLiteral(Av1Constants.LevelBits);
            if (!IsValidSequenceLevel(sequenceHeader.OperatingPoint[0].SequenceLevelIndex))
            {
                throw new ImageFormatException("Invalid sequence level.");
            }

            operatingPoint.SequenceTier = 0;
            operatingPoint.IsDecoderModelInfoPresent = false;
            operatingPoint.IsInitialDisplayDelayPresent = false;
        }
        else
        {
            sequenceHeader.TimingInfoPresentFlag = reader.ReadBoolean();
            if (sequenceHeader.TimingInfoPresentFlag)
            {
                ReadTimingInfo(ref reader, sequenceHeader);
                sequenceHeader.DecoderModelInfoPresentFlag = reader.ReadBoolean();
                if (sequenceHeader.DecoderModelInfoPresentFlag)
                {
                    ReadDecoderModelInfo(ref reader, sequenceHeader);
                }
                else
                {
                    sequenceHeader.DecoderModelInfoPresentFlag = false;
                }
            }

            sequenceHeader.InitialDisplayDelayPresentFlag = reader.ReadBoolean();
            int operatingPointsCnt = (int)reader.ReadLiteral(Av1Constants.OperatingPointCountBits) + 1;
            if (sequenceHeader.OperatingPoint.Length != operatingPointsCnt)
            {
                sequenceHeader.OperatingPoint = new ObuOperatingPoint[operatingPointsCnt];
            }

            for (int i = 0; i < operatingPointsCnt; i++)
            {
                sequenceHeader.OperatingPoint[i] = new ObuOperatingPoint
                {
                    Idc = reader.ReadLiteral(Av1Constants.OperatingPointIdcBits),
                    SequenceLevelIndex = (int)reader.ReadLiteral(Av1Constants.LevelBits)
                };

                if (!IsValidSequenceLevel(sequenceHeader.OperatingPoint[i].SequenceLevelIndex))
                {
                    throw new InvalidImageContentException("The AV1 sequence header contains an undefined sequence-level index.");
                }

                if (sequenceHeader.OperatingPoint[i].SequenceLevelIndex >= Av1Constants.SequenceTierMinimumLevelIndex)
                {
                    sequenceHeader.OperatingPoint[i].SequenceTier = (int)reader.ReadLiteral(1);
                }
                else
                {
                    sequenceHeader.OperatingPoint[i].SequenceTier = 0;
                }

                if (sequenceHeader.DecoderModelInfoPresentFlag)
                {
                    sequenceHeader.OperatingPoint[i].IsDecoderModelInfoPresent = reader.ReadBoolean();
                    if (sequenceHeader.OperatingPoint[i].IsDecoderModelInfoPresent)
                    {
                        // The reader keeps the scheduling values, so a writer can write the parsed sequence header again. Pixel reconstruction does
                        // not use this decoder-model state.
                        ObuDecoderModelInfo decoderModelInfo = sequenceHeader.GetDecoderModelInfo();
                        ReadOperatingParametersInfo(
                            ref reader,
                            (int)decoderModelInfo.BufferDelayLength,
                            sequenceHeader.OperatingPoint[i]);
                    }
                }
                else
                {
                    sequenceHeader.OperatingPoint[i].IsDecoderModelInfoPresent = false;
                }

                if (sequenceHeader.InitialDisplayDelayPresentFlag)
                {
                    sequenceHeader.OperatingPoint[i].IsInitialDisplayDelayPresent = reader.ReadBoolean();
                    if (sequenceHeader.OperatingPoint[i].IsInitialDisplayDelayPresent)
                    {
                        sequenceHeader.OperatingPoint[i].InitialDisplayDelay = reader.ReadLiteral(4) + 1;
                        if (sequenceHeader.OperatingPoint[i].InitialDisplayDelay > 10)
                        {
                            throw new InvalidImageContentException("The AV1 initial display delay exceeds ten decoded frames.");
                        }
                    }
                }
            }
        }

        // The bounded item decoder or sequence decoder supplies the operating-point selector. The code above still parses every operating
        // point, because its timing syntax comes before the shared coded-image dimensions.
        sequenceHeader.FrameWidthBits = (int)reader.ReadLiteral(4) + 1;
        sequenceHeader.FrameHeightBits = (int)reader.ReadLiteral(4) + 1;
        sequenceHeader.MaxFrameWidth = (int)reader.ReadLiteral(sequenceHeader.FrameWidthBits) + 1;
        sequenceHeader.MaxFrameHeight = (int)reader.ReadLiteral(sequenceHeader.FrameHeightBits) + 1;
        if (sequenceHeader.IsReducedStillPictureHeader)
        {
            sequenceHeader.IsFrameIdNumbersPresent = false;
        }
        else
        {
            sequenceHeader.IsFrameIdNumbersPresent = reader.ReadBoolean();
        }

        if (sequenceHeader.IsFrameIdNumbersPresent)
        {
            sequenceHeader.DeltaFrameIdLength = (int)reader.ReadLiteral(4) + 2;
            sequenceHeader.AdditionalFrameIdLength = reader.ReadLiteral(3) + 1;
            sequenceHeader.FrameIdLength = sequenceHeader.DeltaFrameIdLength + (int)sequenceHeader.AdditionalFrameIdLength;
            if (sequenceHeader.FrameIdLength > 16)
            {
                throw new InvalidImageContentException("The AV1 frame identifier length exceeds sixteen bits.");
            }
        }

        sequenceHeader.Use128x128Superblock = reader.ReadBoolean();
        sequenceHeader.EnableFilterIntra = reader.ReadBoolean();
        sequenceHeader.EnableIntraEdgeFilter = reader.ReadBoolean();

        if (sequenceHeader.IsReducedStillPictureHeader)
        {
            sequenceHeader.EnableInterIntraCompound = false;
            sequenceHeader.EnableMaskedCompound = false;
            sequenceHeader.EnableWarpedMotion = false;
            sequenceHeader.EnableDualFilter = false;
            sequenceHeader.OrderHintInfo.EnableJointCompound = false;
            sequenceHeader.OrderHintInfo.EnableReferenceFrameMotionVectors = false;
            sequenceHeader.ForceScreenContentTools = Av1Constants.SelectScreenContentTools;
            sequenceHeader.ForceIntegerMotionVector = Av1Constants.SelectIntegerMotionVector;
            sequenceHeader.OrderHintInfo.OrderHintBits = 0;
        }
        else
        {
            sequenceHeader.EnableInterIntraCompound = reader.ReadBoolean();
            sequenceHeader.EnableMaskedCompound = reader.ReadBoolean();
            sequenceHeader.EnableWarpedMotion = reader.ReadBoolean();
            sequenceHeader.EnableDualFilter = reader.ReadBoolean();
            sequenceHeader.EnableOrderHint = reader.ReadBoolean();
            if (sequenceHeader.EnableOrderHint)
            {
                sequenceHeader.OrderHintInfo.EnableJointCompound = reader.ReadBoolean();
                sequenceHeader.OrderHintInfo.EnableReferenceFrameMotionVectors = reader.ReadBoolean();
            }
            else
            {
                sequenceHeader.OrderHintInfo.EnableJointCompound = false;
                sequenceHeader.OrderHintInfo.EnableReferenceFrameMotionVectors = false;
            }

            bool seqChooseScreenContentTools = reader.ReadBoolean();
            if (seqChooseScreenContentTools)
            {
                sequenceHeader.ForceScreenContentTools = Av1Constants.SelectScreenContentTools;
            }
            else
            {
                sequenceHeader.ForceScreenContentTools = (int)reader.ReadLiteral(1);
            }

            if (sequenceHeader.ForceScreenContentTools > 0)
            {
                bool seqChooseIntegerMv = reader.ReadBoolean();
                if (seqChooseIntegerMv)
                {
                    sequenceHeader.ForceIntegerMotionVector = Av1Constants.SelectIntegerMotionVector;
                }
                else
                {
                    sequenceHeader.ForceIntegerMotionVector = (int)reader.ReadLiteral(1);
                }
            }
            else
            {
                sequenceHeader.ForceIntegerMotionVector = Av1Constants.SelectIntegerMotionVector;
            }

            if (sequenceHeader.EnableOrderHint)
            {
                sequenceHeader.OrderHintInfo.OrderHintBits = (int)reader.ReadLiteral(3) + 1;
            }
            else
            {
                sequenceHeader.OrderHintInfo.OrderHintBits = 0;
            }
        }

        sequenceHeader.EnableSuperResolution = reader.ReadBoolean();
        sequenceHeader.EnableCdef = reader.ReadBoolean();
        sequenceHeader.EnableRestoration = reader.ReadBoolean();
        sequenceHeader.ColorConfig = ReadColorConfig(ref reader, sequenceHeader);
        sequenceHeader.AreFilmGrainingParametersPresent = reader.ReadBoolean();
        ReadTrailingBits(ref reader);
    }

    /// <summary>
    /// Reads the sequence color configuration.
    /// </summary>
    /// <param name="reader">The reader positioned at the color-configuration syntax.</param>
    /// <param name="sequenceHeader">The sequence header that determines the permitted color formats.</param>
    /// <returns>The parsed color configuration.</returns>
    private static ObuColorConfig ReadColorConfig(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader)
    {
        ObuColorConfig colorConfig = new();
        ReadBitDepth(ref reader, colorConfig, sequenceHeader);
        colorConfig.IsMonochrome = false;
        if (sequenceHeader.SequenceProfile != ObuSequenceProfile.High)
        {
            colorConfig.IsMonochrome = reader.ReadBoolean();
        }

        colorConfig.IsColorDescriptionPresent = reader.ReadBoolean();
        colorConfig.ColorPrimaries = ObuColorPrimaries.Unspecified;
        colorConfig.TransferCharacteristics = ObuTransferCharacteristics.Unspecified;
        colorConfig.MatrixCoefficients = ObuMatrixCoefficients.Unspecified;
        if (colorConfig.IsColorDescriptionPresent)
        {
            colorConfig.ColorPrimaries = (ObuColorPrimaries)reader.ReadLiteral(8);
            colorConfig.TransferCharacteristics = (ObuTransferCharacteristics)reader.ReadLiteral(8);
            colorConfig.MatrixCoefficients = (ObuMatrixCoefficients)reader.ReadLiteral(8);
        }

        colorConfig.ColorRange = false;
        colorConfig.SubSamplingX = false;
        colorConfig.SubSamplingY = false;
        colorConfig.ChromaSamplePosition = ObuChromoSamplePosition.Unknown;
        colorConfig.HasSeparateUvDelta = false;
        if (colorConfig.IsMonochrome)
        {
            colorConfig.ColorRange = reader.ReadBoolean();
            colorConfig.SubSamplingX = true;
            colorConfig.SubSamplingY = true;
            return colorConfig;
        }
        else if (
            colorConfig.ColorPrimaries == ObuColorPrimaries.Bt709 &&
            colorConfig.TransferCharacteristics == ObuTransferCharacteristics.Srgb &&
            colorConfig.MatrixCoefficients == ObuMatrixCoefficients.Identity)
        {
            if (sequenceHeader.SequenceProfile != ObuSequenceProfile.High
                && !(sequenceHeader.SequenceProfile == ObuSequenceProfile.Professional
                    && colorConfig.BitDepth == Av1BitDepth.TwelveBit))
            {
                throw new InvalidImageContentException("The AV1 sRGB identity-matrix color configuration is incompatible with its sequence profile.");
            }

            // AV1 defines this RGB identity-matrix combination as full-range 4:4:4. It omits the range and subsampling syntax of other color
            // combinations.
            colorConfig.ColorRange = true;
            colorConfig.SubSamplingX = false;
            colorConfig.SubSamplingY = false;
        }
        else
        {
            colorConfig.ColorRange = reader.ReadBoolean();
            switch (sequenceHeader.SequenceProfile)
            {
                case ObuSequenceProfile.Main:
                    colorConfig.SubSamplingX = true;
                    colorConfig.SubSamplingY = true;
                    break;
                case ObuSequenceProfile.High:
                    colorConfig.SubSamplingX = false;
                    colorConfig.SubSamplingY = false;
                    break;
                case ObuSequenceProfile.Professional:
                default:
                    if (colorConfig.BitDepth == Av1BitDepth.TwelveBit)
                    {
                        colorConfig.SubSamplingX = reader.ReadBoolean();
                        if (colorConfig.SubSamplingX)
                        {
                            colorConfig.SubSamplingY = reader.ReadBoolean();
                        }
                    }
                    else
                    {
                        colorConfig.SubSamplingX = true;
                        colorConfig.SubSamplingY = false;
                    }

                    break;
            }

            if (colorConfig.MatrixCoefficients == ObuMatrixCoefficients.Identity
                && (colorConfig.SubSamplingX || colorConfig.SubSamplingY))
            {
                throw new InvalidImageContentException("The AV1 identity matrix requires 4:4:4 color sampling.");
            }

            if (colorConfig.SubSamplingX && colorConfig.SubSamplingY)
            {
                colorConfig.ChromaSamplePosition = (ObuChromoSamplePosition)reader.ReadLiteral(2);
            }
        }

        colorConfig.HasSeparateUvDelta = reader.ReadBoolean();
        return colorConfig;
    }

    /// <summary>
    /// Reads the decoder-model field widths and decoding-clock units.
    /// </summary>
    /// <param name="reader">The reader positioned at the decoder-model syntax.</param>
    /// <param name="sequenceHeader">The sequence header that receives the decoder-model information.</param>
    private static void ReadDecoderModelInfo(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader) => sequenceHeader.DecoderModelInfo = new ObuDecoderModelInfo
    {
        BufferDelayLength = reader.ReadLiteral(5) + 1,
        NumUnitsInDecodingTick = reader.ReadLiteral(32),
        BufferRemovalTimeLength = reader.ReadLiteral(5) + 1,
        FramePresentationTimeLength = reader.ReadLiteral(5) + 1
    };

    /// <summary>
    /// Reads the decoder-model parameters for one operating point.
    /// </summary>
    /// <param name="reader">The reader positioned at the operating-point parameters.</param>
    /// <param name="bufferDelayLength">The bit width of each encoded buffer delay.</param>
    /// <param name="operatingPoint">The operating point that receives the decoded parameters.</param>
    private static void ReadOperatingParametersInfo(
        ref Av1BitStreamReader reader,
        int bufferDelayLength,
        ObuOperatingPoint operatingPoint)
    {
        operatingPoint.DecoderBufferDelay = reader.ReadLiteral(bufferDelayLength);
        operatingPoint.EncoderBufferDelay = reader.ReadLiteral(bufferDelayLength);
        operatingPoint.LowDelayMode = reader.ReadBoolean();
    }

    /// <summary>
    /// Reads the sequence timing information.
    /// </summary>
    /// <param name="reader">The reader positioned at the timing-information syntax.</param>
    /// <param name="sequenceHeader">The sequence header that receives the timing information.</param>
    private static void ReadTimingInfo(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader)
    {
        uint numUnitsInDisplayTick = reader.ReadLiteral(32);
        uint timeScale = reader.ReadLiteral(32);
        if (numUnitsInDisplayTick == 0 || timeScale == 0)
        {
            throw new InvalidImageContentException("The AV1 timing tick and time scale must both be nonzero.");
        }

        sequenceHeader.TimingInfo = new ObuTimingInfo
        {
            NumUnitsInDisplayTick = numUnitsInDisplayTick,
            TimeScale = timeScale,
            EqualPictureInterval = reader.ReadBoolean()
        };

        if (sequenceHeader.TimingInfo.EqualPictureInterval)
        {
            uint numTicksPerPictureMinusOne = reader.ReadUnsignedVariableLength();
            if (numTicksPerPictureMinusOne == uint.MaxValue)
            {
                throw new InvalidImageContentException("The AV1 ticks-per-picture value exceeds its permitted range.");
            }

            sequenceHeader.TimingInfo.NumTicksPerPicture = numTicksPerPictureMinusOne + 1;
        }
    }

    /// <summary>
    /// Reads the bit depth permitted by the selected sequence profile.
    /// </summary>
    /// <param name="reader">The reader positioned at the high-bit-depth flag.</param>
    /// <param name="colorConfig">The color configuration that receives the bit depth.</param>
    /// <param name="sequenceHeader">The sequence header containing the selected profile.</param>
    private static void ReadBitDepth(ref Av1BitStreamReader reader, ObuColorConfig colorConfig, ObuSequenceHeader sequenceHeader)
    {
        bool hasHighBitDepth = reader.ReadBoolean();
        if (sequenceHeader.SequenceProfile == ObuSequenceProfile.Professional && hasHighBitDepth)
        {
            colorConfig.BitDepth = reader.ReadBoolean() ? Av1BitDepth.TwelveBit : Av1BitDepth.TenBit;
        }
        else if (sequenceHeader.SequenceProfile <= ObuSequenceProfile.Professional)
        {
            colorConfig.BitDepth = hasHighBitDepth ? Av1BitDepth.TenBit : Av1BitDepth.EightBit;
        }
        else
        {
            colorConfig.BitDepth = Av1BitDepth.EightBit;
        }
    }

    /// <summary>
    /// Reads the super-resolution parameters and derives the coded frame width.
    /// </summary>
    /// <param name="reader">The reader positioned at the super-resolution syntax.</param>
    private void ReadSuperResolutionParameters(ref Av1BitStreamReader reader)
    {
        ObuSequenceHeader sequenceHeader = this.CurrentSequenceHeader;
        ObuFrameHeader frameHeader = this.CurrentFrameHeader;
        bool useSuperResolution = false;

        if (sequenceHeader.EnableSuperResolution)
        {
            useSuperResolution = reader.ReadBoolean();
        }

        if (useSuperResolution)
        {
            frameHeader.FrameSize.SuperResolutionDenominator =
                (int)reader.ReadLiteral(Av1Constants.SuperResolutionScaleBits) + Av1Constants.SuperResolutionScaleDenominatorMinimum;
        }
        else
        {
            frameHeader.FrameSize.SuperResolutionDenominator = Av1Constants.ScaleNumerator;
        }

        frameHeader.FrameSize.SuperResolutionUpscaledWidth = frameHeader.FrameSize.FrameWidth;

        // AV1 signals the upscaled width first. Tile and block decoding use the coded width, which is the upscaled width times the fixed scale
        // numerator, divided by the signaled denominator and rounded to the nearest integer.
        frameHeader.FrameSize.FrameWidth =
            ((frameHeader.FrameSize.SuperResolutionUpscaledWidth * Av1Constants.ScaleNumerator) +
            (frameHeader.FrameSize.SuperResolutionDenominator / 2)) /
            frameHeader.FrameSize.SuperResolutionDenominator;

        if (frameHeader.FrameSize.SuperResolutionDenominator != Av1Constants.ScaleNumerator)
        {
            // Appendix A requires a coded width of at least 16 samples when super-resolution is active. The exception is an upscaled image that
            // is narrower than 16 samples.
            int minimumWidth = Math.Min(16, frameHeader.FrameSize.SuperResolutionUpscaledWidth);
            frameHeader.FrameSize.FrameWidth = Math.Max(minimumWidth, frameHeader.FrameSize.FrameWidth);
        }
    }

    /// <summary>
    /// Reads the optional render dimensions for the current frame.
    /// </summary>
    /// <param name="reader">The reader positioned at the render-size syntax.</param>
    private void ReadRenderSize(ref Av1BitStreamReader reader)
    {
        ObuFrameHeader frameHeader = this.CurrentFrameHeader;
        bool renderSizeAndFrameSizeDifferent = reader.ReadBoolean();

        if (renderSizeAndFrameSizeDifferent)
        {
            // `render_width_minus_1` and `render_height_minus_1` are fixed 16-bit fields. They do not use the bit widths of the coded
            // dimensions in the sequence header.
            frameHeader.FrameSize.RenderWidth = (int)reader.ReadLiteral(16) + 1;
            frameHeader.FrameSize.RenderHeight = (int)reader.ReadLiteral(16) + 1;
        }
        else
        {
            frameHeader.FrameSize.RenderWidth = frameHeader.FrameSize.SuperResolutionUpscaledWidth;
            frameHeader.FrameSize.RenderHeight = frameHeader.FrameSize.FrameHeight;
        }
    }

    /// <summary>
    /// Reads or derives the current frame dimensions.
    /// </summary>
    /// <param name="reader">The reader positioned at the frame-size syntax.</param>
    /// <param name="frameSizeOverrideFlag">A value indicating whether dimensions are signaled instead of inherited from the sequence maximum.</param>
    private void ReadFrameSize(ref Av1BitStreamReader reader, bool frameSizeOverrideFlag)
    {
        ObuSequenceHeader sequenceHeader = this.CurrentSequenceHeader;
        ObuFrameHeader frameHeader = this.CurrentFrameHeader;

        if (frameSizeOverrideFlag)
        {
            frameHeader.FrameSize.FrameWidth = (int)reader.ReadLiteral(sequenceHeader.FrameWidthBits) + 1;
            frameHeader.FrameSize.FrameHeight = (int)reader.ReadLiteral(sequenceHeader.FrameHeightBits) + 1;

            // Section 5.9.7 signals frame dimensions with the bit widths of the sequence maxima, but the values must not exceed those maxima.
            // The reader rejects an oversized result here, so later buffer geometry never accepts a value that the sequence header does not
            // permit.
            if (frameHeader.FrameSize.FrameWidth > sequenceHeader.MaxFrameWidth ||
                frameHeader.FrameSize.FrameHeight > sequenceHeader.MaxFrameHeight)
            {
                throw new InvalidImageContentException("AV1 frame dimensions exceed the sequence maximum dimensions.");
            }
        }
        else
        {
            frameHeader.FrameSize.FrameWidth = sequenceHeader.MaxFrameWidth;
            frameHeader.FrameSize.FrameHeight = sequenceHeader.MaxFrameHeight;
        }

        this.ReadSuperResolutionParameters(ref reader);
        this.ComputeImageSize(sequenceHeader);
    }

    /// <summary>
    /// Reads or inherits inter-frame dimensions using the seven selected reference roles.
    /// </summary>
    /// <param name="reader">The reader positioned at the frame-size-with-references syntax.</param>
    /// <param name="referenceFrames">The retained reconstructed frames selected by the current reference mapping.</param>
    private void ReadFrameSizeWithReferences(ref Av1BitStreamReader reader, Av1ReferenceFrameStore referenceFrames)
    {
        ObuSequenceHeader sequenceHeader = this.CurrentSequenceHeader;
        ObuFrameHeader frameHeader = this.CurrentFrameHeader;
        ObuFrameSize frameSize = frameHeader.FrameSize;
        Span<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
        bool foundReference = false;

        // `frame_size_with_refs` carries one `found_ref` bit per selected role, until the first bit that is set. A set bit ends this syntax
        // immediately. The bitstream has no flags for the remaining roles.
        for (int reference = 0; reference < Av1Constants.ReferencesPerFrame; reference++)
        {
            if (!reader.ReadBoolean())
            {
                continue;
            }

            Av1ReferenceFrame referenceFrame = referenceFrames.ResolveRequired((int)referenceFrameIndices[reference]);
            ObuFrameSize referenceSize = referenceFrame.FrameHeader.FrameSize;

            // AV1 5.9.7 inherits the visible dimensions of the reference buffer after super-resolution, and its render rectangle. The current
            // frame then signals its own super-resolution denominator. As a result, the reader does not copy the coded width and denominator
            // of the reference.
            frameSize.FrameWidth = referenceFrame.FrameBuffer.Width;
            frameSize.FrameHeight = referenceFrame.FrameBuffer.Height;
            frameSize.RenderWidth = referenceSize.RenderWidth;
            frameSize.RenderHeight = referenceSize.RenderHeight;
            this.ReadSuperResolutionParameters(ref reader);
            this.ComputeImageSize(sequenceHeader);
            foundReference = true;
            break;
        }

        if (!foundReference)
        {
            // When no reference supplies dimensions, `frame_size_with_refs` carries the explicit frame size, the super-resolution syntax, and
            // the render-size syntax, in that order.
            this.ReadFrameSize(ref reader, true);
            this.ReadRenderSize(ref reader);
        }

        bool hasCompatibleReferenceSize = false;
        ObuColorConfig colorConfig = sequenceHeader.ColorConfig;

        for (int reference = 0; reference < Av1Constants.ReferencesPerFrame; reference++)
        {
            Av1ReferenceFrame referenceFrame = referenceFrames.ResolveRequired((int)referenceFrameIndices[reference]);
            int referenceWidth = referenceFrame.FrameBuffer.Width;
            int referenceHeight = referenceFrame.FrameBuffer.Height;

            // AV1 6.8.6 permits a reference dimension from one half through sixteen times the current coded dimension. At least one of the
            // seven selected roles must satisfy both axes, or the reader rejects the frame.
            hasCompatibleReferenceSize |=
                (2 * frameSize.FrameWidth) >= referenceWidth &&
                (2 * frameSize.FrameHeight) >= referenceHeight &&
                frameSize.FrameWidth <= (16 * referenceWidth) &&
                frameSize.FrameHeight <= (16 * referenceHeight);

            ObuColorConfig referenceColorConfig = referenceFrame.FrameBuffer.ColorConfig;

            // Every selected reference uses the same sample format for prediction. A different bit depth or chroma subsampling changes the
            // meaning of the samples. As a result, the reader rejects it, even when no block in the current frame selects that role.
            if (referenceFrame.FrameBuffer.BitDepth != colorConfig.BitDepth ||
                referenceColorConfig.SubSamplingX != colorConfig.SubSamplingX ||
                referenceColorConfig.SubSamplingY != colorConfig.SubSamplingY)
            {
                throw new InvalidImageContentException("An AV1 inter frame selects a reference with an incompatible color format.");
            }
        }

        if (!hasCompatibleReferenceSize)
        {
            throw new InvalidImageContentException("An AV1 inter frame has no reference with compatible dimensions.");
        }
    }

    /// <summary>
    /// Reads the frame-level interpolation-filter selection.
    /// </summary>
    /// <param name="reader">The reader positioned at the interpolation-filter syntax.</param>
    /// <returns>The fixed filter family or the per-block switchable selection.</returns>
    private static Av1InterpolationFilter ReadFrameInterpolationFilter(ref Av1BitStreamReader reader)
    {
        // A leading one omits the two-bit fixed-family field and gives the choice to each inter block. Otherwise, the literal values map
        // directly to regular, smooth, sharp, and bilinear, as AV1 6.10.2 defines.
        return reader.ReadBoolean()
            ? Av1InterpolationFilter.Switchable
            : (Av1InterpolationFilter)reader.ReadLiteral(2);
    }

    /// <summary>
    /// Reads the tile layout and derives tile boundaries in mode-information units.
    /// </summary>
    /// <param name="reader">The reader positioned at the tile-information syntax.</param>
    /// <param name="sequenceHeader">The sequence header defining superblock geometry.</param>
    /// <param name="frameHeader">The frame header defining the current frame geometry.</param>
    /// <returns>The parsed tile layout.</returns>
    private static ObuTileGroupHeader ReadTileInfo(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        ObuTileGroupHeader tileInfo = new();
        int superblockColumnCount;
        int superblockRowCount;
        int superblockSizeLog2 = sequenceHeader.SuperblockSizeLog2;
        int superblockShift = superblockSizeLog2 - Av1Constants.ModeInfoSizeLog2;
        superblockColumnCount = (frameHeader.ModeInfoColumnCount + sequenceHeader.SuperblockModeInfoSize - 1) >> superblockShift;
        superblockRowCount = (frameHeader.ModeInfoRowCount + sequenceHeader.SuperblockModeInfoSize - 1) >> superblockShift;

        int maxTileAreaOfSuperBlock = Av1Constants.MaxTileArea >> (superblockSizeLog2 << 1);

        // The bitstream limits tile dimensions in superblocks. The decoder stores boundaries in mode-information units for direct use during
        // block traversal.
        tileInfo.MaxTileWidthSuperblock = Av1Constants.MaxTileWidth >> superblockSizeLog2;
        tileInfo.MaxTileHeightSuperblock = (Av1Constants.MaxTileArea / Av1Constants.MaxTileWidth) >> superblockSizeLog2;
        tileInfo.MinLog2TileColumnCount = TileLog2(tileInfo.MaxTileWidthSuperblock, superblockColumnCount);
        tileInfo.MaxLog2TileColumnCount = (int)Av1Math.CeilLog2((uint)Math.Min(superblockColumnCount, Av1Constants.MaxTileColumnCount));
        tileInfo.MaxLog2TileRowCount = (int)Av1Math.CeilLog2((uint)Math.Min(superblockRowCount, Av1Constants.MaxTileRowCount));
        tileInfo.MinLog2TileCount = Math.Max(tileInfo.MinLog2TileColumnCount, TileLog2(maxTileAreaOfSuperBlock, superblockColumnCount * superblockRowCount));
        tileInfo.HasUniformTileSpacing = reader.ReadBoolean();

        // The tile limits of AV1 set the size of the boundary storage. AV1 permits at most 64 tile rows or columns. Arrays sized from the
        // sequence keep thousands of unused entries on wide frames.
        if (tileInfo.HasUniformTileSpacing)
        {
            tileInfo.TileColumnCountLog2 = tileInfo.MinLog2TileColumnCount;
            while (tileInfo.TileColumnCountLog2 < tileInfo.MaxLog2TileColumnCount)
            {
                if (reader.ReadBoolean())
                {
                    tileInfo.TileColumnCountLog2++;
                }
                else
                {
                    break;
                }
            }

            int tileWidthSuperblock = Av1Math.DivideLog2Ceiling(superblockColumnCount, tileInfo.TileColumnCountLog2);
            DebugGuard.MustBeLessThanOrEqualTo(tileWidthSuperblock, tileInfo.MaxTileWidthSuperblock, nameof(tileWidthSuperblock));
            int i = 0;
            for (int startSuperblock = 0; startSuperblock < superblockColumnCount; startSuperblock += tileWidthSuperblock)
            {
                tileInfo.TileColumnStartModeInfo[i] = startSuperblock << superblockShift;
                i++;
            }

            tileInfo.TileColumnStartModeInfo[i] = frameHeader.ModeInfoColumnCount;
            tileInfo.TileColumnCount = i;

            tileInfo.MinLog2TileRowCount = Math.Max(tileInfo.MinLog2TileCount - tileInfo.TileColumnCountLog2, 0);
            tileInfo.TileRowCountLog2 = tileInfo.MinLog2TileRowCount;
            while (tileInfo.TileRowCountLog2 < tileInfo.MaxLog2TileRowCount)
            {
                if (reader.ReadBoolean())
                {
                    tileInfo.TileRowCountLog2++;
                }
                else
                {
                    break;
                }
            }

            int tileHeightSuperblock = Av1Math.DivideLog2Ceiling(superblockRowCount, tileInfo.TileRowCountLog2);
            DebugGuard.MustBeLessThanOrEqualTo(tileHeightSuperblock, tileInfo.MaxTileHeightSuperblock, nameof(tileHeightSuperblock));
            i = 0;
            for (int startSuperblock = 0; startSuperblock < superblockRowCount; startSuperblock += tileHeightSuperblock)
            {
                tileInfo.TileRowStartModeInfo[i] = startSuperblock << superblockShift;
                i++;
            }

            tileInfo.TileRowStartModeInfo[i] = frameHeader.ModeInfoRowCount;
            tileInfo.TileRowCount = i;
        }
        else
        {
            uint widestTileSuperBlock = 0U;
            int startSuperBlock = 0;
            int i = 0;
            for (; startSuperBlock < superblockColumnCount; i++)
            {
                if (i == Av1Constants.MaxTileColumnCount)
                {
                    throw new InvalidImageContentException("The AV1 frame exceeds the maximum tile-column count.");
                }

                tileInfo.TileColumnStartModeInfo[i] = startSuperBlock << superblockShift;
                uint maxWidth = (uint)Math.Min(superblockColumnCount - startSuperBlock, tileInfo.MaxTileWidthSuperblock);
                uint widthInSuperBlocks = reader.ReadNonSymmetric(maxWidth) + 1;
                widestTileSuperBlock = Math.Max(widthInSuperBlocks, widestTileSuperBlock);
                startSuperBlock += (int)widthInSuperBlocks;
            }

            if (startSuperBlock != superblockColumnCount)
            {
                throw new ImageFormatException("Super block tiles width does not add up to total width.");
            }

            tileInfo.TileColumnStartModeInfo[i] = frameHeader.ModeInfoColumnCount;
            tileInfo.TileColumnCount = i;
            tileInfo.TileColumnCountLog2 = TileLog2(1, tileInfo.TileColumnCount);
            if (tileInfo.MinLog2TileCount > 0)
            {
                maxTileAreaOfSuperBlock = (superblockRowCount * superblockColumnCount) >> (tileInfo.MinLog2TileCount + 1);
            }
            else
            {
                maxTileAreaOfSuperBlock = superblockRowCount * superblockColumnCount;
            }

            DebugGuard.MustBeGreaterThan(widestTileSuperBlock, 0U, nameof(widestTileSuperBlock));
            tileInfo.MaxTileHeightSuperblock = Math.Max(maxTileAreaOfSuperBlock / (int)widestTileSuperBlock, 1);

            startSuperBlock = 0;
            for (i = 0; startSuperBlock < superblockRowCount; i++)
            {
                if (i == Av1Constants.MaxTileRowCount)
                {
                    throw new InvalidImageContentException("The AV1 frame exceeds the maximum tile-row count.");
                }

                tileInfo.TileRowStartModeInfo[i] = startSuperBlock << superblockShift;
                uint maxHeight = (uint)Math.Min(superblockRowCount - startSuperBlock, tileInfo.MaxTileHeightSuperblock);
                uint heightInSuperBlocks = reader.ReadNonSymmetric(maxHeight) + 1;
                startSuperBlock += (int)heightInSuperBlocks;
            }

            if (startSuperBlock != superblockRowCount)
            {
                throw new ImageFormatException("Super block tiles height does not add up to total height.");
            }

            tileInfo.TileRowStartModeInfo[i] = frameHeader.ModeInfoRowCount;
            tileInfo.TileRowCount = i;
            tileInfo.TileRowCountLog2 = TileLog2(1, tileInfo.TileRowCount);
        }

        if (tileInfo.TileColumnCount > Av1Constants.MaxTileColumnCount || tileInfo.TileRowCount > Av1Constants.MaxTileRowCount)
        {
            throw new ImageFormatException("Tile width or height too big.");
        }

        if (tileInfo.TileColumnCount > 1)
        {
            int minimumInnerTileWidth = 64 << (frameHeader.FrameSize.FrameWidth != frameHeader.FrameSize.SuperResolutionUpscaledWidth ? 1 : 0);
            for (int column = 0; column < tileInfo.TileColumnCount - 1; column++)
            {
                int tileWidth = (tileInfo.TileColumnStartModeInfo[column + 1] - tileInfo.TileColumnStartModeInfo[column])
                    << Av1Constants.ModeInfoSizeLog2;

                // The check excludes the rightmost column, because that column receives the remainder of the coded width. Every inner column
                // must be at least 64 pixels wide. When the frame uses super-resolution scaling, the minimum is 128 pixels.
                if (tileWidth < minimumInnerTileWidth)
                {
                    throw new InvalidImageContentException("The AV1 frame contains an inner tile column narrower than the permitted minimum.");
                }
            }
        }

        if (tileInfo.TileColumnCountLog2 > 0 || tileInfo.TileRowCountLog2 > 0)
        {
            tileInfo.ContextUpdateTileId = reader.ReadLiteral(tileInfo.TileRowCountLog2 + tileInfo.TileColumnCountLog2);
            tileInfo.TileSizeBytes = (int)reader.ReadLiteral(2) + 1;
        }
        else
        {
            tileInfo.ContextUpdateTileId = 0;
        }

        if (tileInfo.ContextUpdateTileId >= (tileInfo.TileColumnCount * tileInfo.TileRowCount))
        {
            throw new ImageFormatException("Context update Tile ID too large.");
        }

        return tileInfo;
    }

    /// <summary>
    /// Reads the uncompressed syntax for one coded frame in a bounded AV1 image item or image sequence.
    /// </summary>
    /// <param name="reader">The reader positioned at the uncompressed frame header.</param>
    /// <param name="header">The OBU header identifying the frame's temporal and spatial layers.</param>
    private void ReadUncompressedFrameHeader(ref Av1BitStreamReader reader, ObuHeader header)
    {
        ObuSequenceHeader sequenceHeader = this.CurrentSequenceHeader;
        ObuFrameHeader frameHeader = this.CurrentFrameHeader;
        Av1ReferenceFrame? primaryReference = null;
        bool frameSizeOverrideFlag = false;
        int idLength = sequenceHeader.FrameIdLength;

        if (sequenceHeader.IsReducedStillPictureHeader)
        {
            frameHeader.ShowExistingFrame = false;
            frameHeader.FrameType = ObuFrameType.KeyFrame;
            frameHeader.ShowFrame = true;
            frameHeader.ShowableFrame = false;
            frameHeader.ErrorResilientMode = true;
        }
        else
        {
            frameHeader.ShowExistingFrame = reader.ReadBoolean();
            if (frameHeader.ShowExistingFrame)
            {
                if (sequenceHeader.IsStillPicture)
                {
                    throw new InvalidImageContentException("An AV1 still picture cannot display a previously decoded frame.");
                }

                frameHeader.FrameToShowMapIdx = reader.ReadLiteral(Av1Constants.ReferenceFrameIndexBits);

                if (sequenceHeader.DecoderModelInfoPresentFlag && sequenceHeader.TimingInfo?.EqualPictureInterval == false)
                {
                    // 5.9.31. Temporal point info syntax.
                    ObuDecoderModelInfo decoderModelInfo = sequenceHeader.GetDecoderModelInfo();
                    frameHeader.FramePresentationTime = reader.ReadLiteral((int)decoderModelInfo.FramePresentationTimeLength);
                }

                if (sequenceHeader.IsFrameIdNumbersPresent)
                {
                    frameHeader.DisplayFrameId = reader.ReadLiteral(idLength);
                }

                Av1ReferenceFrameStore? retainedReferenceFrames = this.referenceFrames;
                if (retainedReferenceFrames is null)
                {
                    throw new InvalidOperationException("AV1 existing-frame presentation requires a reconstructed reference map.");
                }

                int existingFrameSlot = (int)frameHeader.FrameToShowMapIdx;
                Av1ReferenceFrame? existingFrame = retainedReferenceFrames.Resolve(existingFrameSlot);
                if (existingFrame is null)
                {
                    throw new InvalidImageContentException("The AV1 existing-frame header selects an unoccupied reference-map slot.");
                }

                if (!existingFrame.FrameHeader.ShowableFrame)
                {
                    throw new InvalidImageContentException("The AV1 existing-frame header selects a frame that is not showable.");
                }

                if (sequenceHeader.IsFrameIdNumbersPresent &&
                    (!frameHeader.GetReferenceValidity()[existingFrameSlot] ||
                    frameHeader.DisplayFrameId != frameHeader.GetReferenceFrameIds()[existingFrameSlot]))
                {
                    throw new InvalidImageContentException("The AV1 existing-frame header has a mismatched display frame identifier.");
                }

                ObuFrameHeader existingFrameHeader = existingFrame.FrameHeader;
                frameHeader.FrameType = existingFrameHeader.FrameType;
                frameHeader.ShowFrame = true;
                frameHeader.ShowableFrame = existingFrameHeader.ShowableFrame;
                frameHeader.OrderHint = existingFrameHeader.OrderHint;
                frameHeader.FrameSize = existingFrameHeader.FrameSize;
                frameHeader.FilmGrainParameters = existingFrameHeader.FilmGrainParameters;

                if (existingFrameHeader.FrameType == ObuFrameType.KeyFrame)
                {
                    frameHeader.RefreshFrameFlags = byte.MaxValue;
                    frameHeader.CurrentFrameId = frameHeader.GetReferenceFrameIds()[existingFrameSlot];
                }
                else
                {
                    frameHeader.RefreshFrameFlags = 0;
                    if (this.frameReferenceState.HasCurrentFrameId)
                    {
                        // When a non-key existing frame is shown, the session keeps its current frame identifier. The header does not read or
                        // replace it.
                        frameHeader.CurrentFrameId = this.frameReferenceState.CurrentFrameId;
                    }
                }

                return;
            }

            frameHeader.FrameType = (ObuFrameType)reader.ReadLiteral(Av1Constants.FrameTypeBits);
            frameHeader.ShowFrame = reader.ReadBoolean();
            if (sequenceHeader.IsStillPicture && (frameHeader.FrameType != ObuFrameType.KeyFrame || !frameHeader.ShowFrame))
            {
                throw new InvalidImageContentException("An AV1 still picture must be encoded as a shown key frame.");
            }

            if (frameHeader.ShowFrame && sequenceHeader.DecoderModelInfoPresentFlag && sequenceHeader.TimingInfo?.EqualPictureInterval == false)
            {
                // 5.9.31. Temporal point info syntax.
                ObuDecoderModelInfo decoderModelInfo = sequenceHeader.GetDecoderModelInfo();
                frameHeader.FramePresentationTime = reader.ReadLiteral((int)decoderModelInfo.FramePresentationTimeLength);
            }

            if (frameHeader.ShowFrame)
            {
                frameHeader.ShowableFrame = frameHeader.FrameType != ObuFrameType.KeyFrame;
            }
            else
            {
                frameHeader.ShowableFrame = reader.ReadBoolean();
            }

            if (frameHeader.FrameType == ObuFrameType.SwitchFrame || (frameHeader.FrameType == ObuFrameType.KeyFrame && frameHeader.ShowFrame))
            {
                frameHeader.ErrorResilientMode = true;
            }
            else
            {
                frameHeader.ErrorResilientMode = reader.ReadBoolean();
            }
        }

        if (frameHeader.FrameType == ObuFrameType.KeyFrame && frameHeader.ShowFrame)
        {
            frameHeader.GetReferenceValidity().Clear();
        }

        frameHeader.DisableCdfUpdate = reader.ReadBoolean();
        if (sequenceHeader.ForceScreenContentTools == 2)
        {
            frameHeader.AllowScreenContentTools = reader.ReadBoolean();
        }
        else
        {
            frameHeader.AllowScreenContentTools = sequenceHeader.ForceScreenContentTools != 0;
        }

        if (frameHeader.AllowScreenContentTools)
        {
            if (sequenceHeader.ForceIntegerMotionVector == 2)
            {
                frameHeader.ForceIntegerMotionVector = reader.ReadBoolean();
            }
            else
            {
                frameHeader.ForceIntegerMotionVector = sequenceHeader.ForceIntegerMotionVector != 0;
            }
        }
        else
        {
            frameHeader.ForceIntegerMotionVector = false;
        }

        if (frameHeader.IsIntra)
        {
            frameHeader.ForceIntegerMotionVector = true;
        }

        bool havePreviousFrameId = this.frameReferenceState.HasCurrentFrameId &&
            !(frameHeader.FrameType == ObuFrameType.KeyFrame && frameHeader.ShowFrame);

        uint previousFrameId = this.frameReferenceState.CurrentFrameId;

        if (sequenceHeader.IsFrameIdNumbersPresent)
        {
            frameHeader.CurrentFrameId = reader.ReadLiteral(idLength);
            if (havePreviousFrameId)
            {
                uint frameIdModulus = 1U << idLength;
                uint diffFrameId = frameHeader.CurrentFrameId > previousFrameId
                    ? frameHeader.CurrentFrameId - previousFrameId
                    : frameIdModulus + frameHeader.CurrentFrameId - previousFrameId;

                if (frameHeader.CurrentFrameId == previousFrameId || diffFrameId >= 1U << (idLength - 1))
                {
                    throw new ImageFormatException("Current frame ID cannot be same as previous Frame ID");
                }
            }

            frameHeader.MarkReferenceFrames(idLength, sequenceHeader.DeltaFrameIdLength);
        }
        else
        {
            frameHeader.CurrentFrameId = 0;
        }

        if (frameHeader.FrameType == ObuFrameType.SwitchFrame)
        {
            frameSizeOverrideFlag = true;
        }
        else if (sequenceHeader.IsReducedStillPictureHeader)
        {
            frameSizeOverrideFlag = false;
        }
        else
        {
            frameSizeOverrideFlag = reader.ReadBoolean();
        }

        frameHeader.OrderHint = reader.ReadLiteral(sequenceHeader.OrderHintInfo.OrderHintBits);

        if (frameHeader.IsIntra || frameHeader.ErrorResilientMode)
        {
            frameHeader.PrimaryReferenceFrame = Av1Constants.PrimaryReferenceFrameNone;
        }
        else
        {
            frameHeader.PrimaryReferenceFrame = reader.ReadLiteral(Av1Constants.PrimaryReferenceBits);
        }

        if (sequenceHeader.DecoderModelInfoPresentFlag)
        {
            bool bufferRemovalTimePresent = reader.ReadBoolean();
            if (bufferRemovalTimePresent)
            {
                ObuDecoderModelInfo decoderModelInfo = sequenceHeader.GetDecoderModelInfo();
                int bufferRemovalTimeLength = (int)decoderModelInfo.BufferRemovalTimeLength;
                foreach (ObuOperatingPoint operatingPoint in sequenceHeader.OperatingPoint)
                {
                    // A layer-specific OBU carries one removal time only for each operating point that selects both of its layer IDs. The
                    // value affects only scheduling, so the reader consumes it and does not keep it.
                    bool appliesToLayer = operatingPoint.Idc == 0 ||
                        (((operatingPoint.Idc >> header.TemporalId) & 1U) != 0 &&
                        ((operatingPoint.Idc >> (header.SpatialId + 8)) & 1U) != 0);

                    if (operatingPoint.IsDecoderModelInfoPresent && appliesToLayer)
                    {
                        _ = reader.ReadLiteral(bufferRemovalTimeLength);
                    }
                }
            }
        }

        frameHeader.AllowHighPrecisionMotionVector = false;
        frameHeader.UseReferenceFrameMotionVectors = false;
        frameHeader.AllowIntraBlockCopy = false;
        if (frameHeader.FrameType == ObuFrameType.SwitchFrame || (frameHeader.FrameType == ObuFrameType.KeyFrame && frameHeader.ShowFrame))
        {
            frameHeader.RefreshFrameFlags = 0xFFU;
        }
        else
        {
            frameHeader.RefreshFrameFlags = reader.ReadLiteral(8);
        }

        if (frameHeader.FrameType == ObuFrameType.IntraOnlyFrame)
        {
            if (frameHeader.RefreshFrameFlags == byte.MaxValue)
            {
                throw new InvalidImageContentException("An AV1 intra-only frame cannot refresh every reference-map slot.");
            }
        }

        if (!frameHeader.IsIntra || (frameHeader.RefreshFrameFlags != 0xFFU))
        {
            if (frameHeader.ErrorResilientMode && sequenceHeader.OrderHintInfo.EnableOrderHint)
            {
                Span<uint> referenceOrderHints = frameHeader.GetReferenceOrderHints();
                Span<bool> referenceValidity = frameHeader.GetReferenceValidity();
                for (int i = 0; i < Av1Constants.ReferenceFrameCount; i++)
                {
                    uint referenceOrderHint = reader.ReadLiteral(sequenceHeader.OrderHintInfo.OrderHintBits);
                    if (referenceOrderHint != referenceOrderHints[i])
                    {
                        referenceValidity[i] = false;
                    }
                }
            }
        }

        if (frameHeader.IsIntra)
        {
            this.ReadFrameSize(ref reader, frameSizeOverrideFlag);
            this.ReadRenderSize(ref reader);
            if (frameHeader.AllowScreenContentTools && frameHeader.FrameSize.RenderWidth != 0)
            {
                if (frameHeader.FrameSize.FrameWidth == frameHeader.FrameSize.SuperResolutionUpscaledWidth)
                {
                    frameHeader.AllowIntraBlockCopy = reader.ReadBoolean();
                }
            }
        }
        else
        {
            Av1ReferenceFrameStore? retainedReferenceFrames = this.referenceFrames;

            if (retainedReferenceFrames is null)
            {
                // Inter-frame size syntax reads dimensions from reconstructed references. A header-only parser does not own those samples. The
                // production decoder supplies the reference store in its constructor.
                throw new InvalidOperationException("AV1 inter-frame parsing requires a reconstructed reference map.");
            }

            ReadReferenceFrameIndices(ref reader, sequenceHeader, frameHeader, retainedReferenceFrames);

            if (frameHeader.PrimaryReferenceSlot.HasValue)
            {
                // The reference-index parse validates the slot before it stores the slot on the header. The reader keeps the owner here, so
                // every inherited frame state comes from the same primary reference.
                primaryReference = retainedReferenceFrames.ResolveRequired(frameHeader.PrimaryReferenceSlot.Value);
            }

            if (!frameHeader.ErrorResilientMode && frameSizeOverrideFlag)
            {
                this.ReadFrameSizeWithReferences(ref reader, retainedReferenceFrames);
            }
            else
            {
                this.ReadFrameSize(ref reader, frameSizeOverrideFlag);
                this.ReadRenderSize(ref reader);
            }

            if (!frameHeader.ForceIntegerMotionVector)
            {
                frameHeader.AllowHighPrecisionMotionVector = reader.ReadBoolean();
            }

            frameHeader.InterpolationFilter = ReadFrameInterpolationFilter(ref reader);
            frameHeader.IsMotionModeSwitchable = reader.ReadBoolean();
        }

        bool mightAllowReferenceFrameMotionVectors =
            !frameHeader.ErrorResilientMode &&
            sequenceHeader.OrderHintInfo.EnableReferenceFrameMotionVectors &&
            sequenceHeader.OrderHintInfo.EnableOrderHint &&
            !frameHeader.IsIntra;

        if (mightAllowReferenceFrameMotionVectors)
        {
            // AV1 5.9.2 carries this flag only when temporal order hints and the sequence-level reference-MV tool are both available. All other
            // frames derive false and consume no bit.
            frameHeader.UseReferenceFrameMotionVectors = reader.ReadBoolean();
        }

        // The frame motion state derives the sign bias from the retained reference order hints when it initializes. The sign bias is not mutable
        // uncompressed-header state, so the frame header does not store a copy.
        if (sequenceHeader.IsReducedStillPictureHeader || frameHeader.DisableCdfUpdate)
        {
            frameHeader.DisableFrameEndUpdateCdf = true;
        }
        else
        {
            frameHeader.DisableFrameEndUpdateCdf = reader.ReadBoolean();
        }

        if (primaryReference is not null)
        {
            // When the update flags omit new values, the loop-filter deltas come from the primary frame. The reader copies the two fixed tables
            // before the parse. As a result, the header keeps the unchanged entries and does not share the tables of the primary frame.
            primaryReference.FrameHeader.LoopFilterParameters.ReferenceDeltas.CopyTo(frameHeader.LoopFilterParameters.ReferenceDeltas);
            primaryReference.FrameHeader.LoopFilterParameters.ModeDeltas.CopyTo(frameHeader.LoopFilterParameters.ModeDeltas);
        }

        // Entropy defaults depend on `base_q_idx`, which comes after the tile information in the header. As a result, `Av1TileReader` loads
        // the retained primary state or the defaults of the selected quantizer band at the first tile boundary.

        // The reference-map refresh stays pending until the reconstruction completes. The parse only records the validated refresh flags and
        // selected slots on the frame header.
        frameHeader.TilesInfo = ReadTileInfo(ref reader, sequenceHeader, frameHeader);
        ReadQuantizationParameters(ref reader, sequenceHeader, frameHeader);
        ReadSegmentationParameters(ref reader, frameHeader, primaryReference?.FrameHeader.SegmentationParameters);
        ReadFrameDeltaQParameters(ref reader, frameHeader);
        ReadFrameDeltaLoopFilterParameters(ref reader, frameHeader);

        // The primary frame keeps its decoded segment map in `Av1FrameInfo`. Inter block parsing copies or predicts segment identifiers from that
        // map as `update_map` selects, so the frame header does not store a copy of the map.
        Av1QuantizationLookup.UpdateFrameQuantizationState(frameHeader);

        if (frameHeader.CodedLossless)
        {
            DebugGuard.IsFalse(frameHeader.DeltaQParameters.IsPresent, nameof(frameHeader.DeltaQParameters.IsPresent), "No Delta Q parameters are allowed for lossless frame.");
        }

        this.ReadLoopFilterParameters(ref reader, sequenceHeader);
        ReadCdefParameters(ref reader, sequenceHeader, frameHeader);
        ReadLoopRestorationParameters(ref reader, sequenceHeader, frameHeader);
        ReadTransformMode(ref reader, frameHeader);

        frameHeader.ReferenceMode = ReadFrameReferenceMode(ref reader, frameHeader);
        ReadSkipModeParameters(ref reader, sequenceHeader, frameHeader);
        if (frameHeader.IsIntra || frameHeader.ErrorResilientMode || !sequenceHeader.EnableWarpedMotion)
        {
            frameHeader.AllowWarpedMotion = false;
        }
        else
        {
            frameHeader.AllowWarpedMotion = reader.ReadBoolean();
        }

        frameHeader.UseReducedTransformSet = reader.ReadBoolean();
        this.ReadGlobalMotionParameters(ref reader, frameHeader);
        this.ReadFilmGrainFilterParameters(ref reader, sequenceHeader, frameHeader);
    }

    /// <summary>
    /// Reads or derives the seven reference-map slots used by an inter frame and resolves its primary context source.
    /// </summary>
    /// <param name="reader">The reader positioned at the inter-reference signaling syntax.</param>
    /// <param name="sequenceHeader">The sequence header defining frame-ID and order-hint domains.</param>
    /// <param name="frameHeader">The frame header that receives the seven-entry reference mapping.</param>
    /// <param name="referenceFrames">The retained reconstructed frames backing the eight reference-map slots.</param>
    private static void ReadReferenceFrameIndices(
        ref Av1BitStreamReader reader,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1ReferenceFrameStore referenceFrames)
    {
        Span<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
        Span<bool> referenceValidity = frameHeader.GetReferenceValidity();
        bool usesShortSignaling = sequenceHeader.OrderHintInfo.EnableOrderHint && reader.ReadBoolean();

        if (usesShortSignaling)
        {
            uint lastFrameIndex = reader.ReadLiteral(Av1Constants.ReferenceFrameIndexBits);
            uint goldenFrameIndex = reader.ReadLiteral(Av1Constants.ReferenceFrameIndexBits);
            InlineArray8<bool> slotOccupancyStorage = default;
            Span<bool> slotOccupancy = slotOccupancyStorage;

            referenceFrames.FillOccupancy(slotOccupancy);

            // Short signaling transmits only LAST and GOLDEN. The specification derives the other five roles from the stored slot order hints
            // and the slot occupancy. Frame-ID validity does not change this derivation.
            Av1ReferenceFrameDerivation.DeriveShortSignaledReferences(
                frameHeader.OrderHint,
                sequenceHeader.OrderHintInfo.OrderHintBits,
                lastFrameIndex,
                goldenFrameIndex,
                frameHeader.GetReferenceOrderHints(),
                slotOccupancy,
                referenceFrameIndices);
        }

        Span<uint> referenceFrameIds = frameHeader.GetReferenceFrameIds();
        uint frameIdModulus = 1U << sequenceHeader.FrameIdLength;

        for (int reference = 0; reference < Av1Constants.ReferencesPerFrame; reference++)
        {
            uint slot = referenceFrameIndices[reference];
            if (!usesShortSignaling)
            {
                slot = reader.ReadLiteral(Av1Constants.ReferenceFrameIndexBits);
                referenceFrameIndices[reference] = slot;
            }

            // Slot occupancy and frame-ID validity are two independent states. Short signaling derives the roles from every occupied slot. The
            // validity check of each role comes after that derivation.
            if (referenceFrames.Resolve((int)slot) is null)
            {
                throw new InvalidImageContentException("An AV1 inter frame selects an unoccupied reference-map slot.");
            }

            if (!referenceValidity[(int)slot])
            {
                throw new InvalidImageContentException("An AV1 inter frame selects a reference that is not valid for referencing.");
            }

            if (sequenceHeader.IsFrameIdNumbersPresent)
            {
                uint deltaFrameId = reader.ReadLiteral(sequenceHeader.DeltaFrameIdLength) + 1U;
                uint expectedFrameId = (frameHeader.CurrentFrameId + frameIdModulus - deltaFrameId) % frameIdModulus;

                if (referenceFrameIds[(int)slot] != expectedFrameId)
                {
                    throw new InvalidImageContentException("An AV1 inter reference does not match its signaled frame identifier.");
                }
            }
        }

        if (frameHeader.PrimaryReferenceFrame != Av1Constants.PrimaryReferenceFrameNone)
        {
            // `primary_ref_frame` indexes the seven inter-reference roles, not the eight slots of the retained map. The reader resolves it
            // once, so the entropy, segmentation, loop-filter, and motion state all select the same retained owner later.
            frameHeader.PrimaryReferenceSlot = (byte)referenceFrameIndices[(int)frameHeader.PrimaryReferenceFrame];
        }
    }

    /// <summary>
    /// Reads an AV1 frame header and removes its byte length from the remaining OBU payload size.
    /// </summary>
    /// <param name="reader">The reader positioned at the frame-header payload.</param>
    /// <param name="header">The OBU header whose remaining payload size is updated.</param>
    /// <param name="trailingBit">A value indicating whether trailing-bit syntax follows the frame header.</param>
    public void ReadFrameHeader(ref Av1BitStreamReader reader, ObuHeader header, bool trailingBit)
    {
        int startBitPosition = reader.BitPosition;
        this.ReadUncompressedFrameHeader(ref reader, header);
        if (trailingBit)
        {
            ReadTrailingBits(ref reader);
        }

        AlignToByteBoundary(ref reader);

        int endPosition = reader.BitPosition;
        int headerBytes = (endPosition - startBitPosition) / 8;
        header.PayloadSize -= headerBytes;
    }

    /// <summary>
    /// Reads a tile-group header and passes each contained tile payload to the tile reader.
    /// </summary>
    /// <param name="reader">The reader positioned at the tile-group payload.</param>
    /// <param name="decoder">The tile reader that decodes each tile payload.</param>
    /// <param name="header">The OBU header containing the remaining tile-group payload size.</param>
    /// <param name="nextTileStart">The zero-based tile index that must begin this group and receives the next expected index.</param>
    /// <param name="isLastTileGroup">Receives whether this group completes the frame's ordered tile coverage.</param>
    private void ReadTileGroup(
        ref Av1BitStreamReader reader,
        IAv1TileReader decoder,
        ObuHeader header,
        ref int nextTileStart,
        out bool isLastTileGroup)
    {
        ObuSequenceHeader sequenceHeader = this.CurrentSequenceHeader;
        ObuFrameHeader frameHeader = this.CurrentFrameHeader;
        ObuTileGroupHeader tileInfo = frameHeader.TilesInfo;
        int tileCount = tileInfo.TileColumnCount * tileInfo.TileRowCount;
        int startBitPosition = reader.BitPosition;
        bool tileStartAndEndPresentFlag = false;
        if (tileCount > 1)
        {
            tileStartAndEndPresentFlag = reader.ReadBoolean();
        }

        if (header.Type == ObuType.Frame && tileStartAndEndPresentFlag)
        {
            throw new InvalidImageContentException("A combined AV1 frame OBU cannot signal explicit tile-group bounds.");
        }

        int tileGroupStart = 0;
        int tileGroupEnd = tileCount - 1;
        if (tileCount != 1 && tileStartAndEndPresentFlag)
        {
            int tileBits = tileInfo.TileColumnCountLog2 + tileInfo.TileRowCountLog2;
            tileGroupStart = (int)reader.ReadLiteral(tileBits);
            tileGroupEnd = (int)reader.ReadLiteral(tileBits);
        }

        if (tileGroupStart != nextTileStart || tileGroupStart > tileGroupEnd || tileGroupEnd >= tileCount)
        {
            throw new InvalidImageContentException("The AV1 tile groups do not provide complete ordered frame coverage.");
        }

        AlignToByteBoundary(ref reader);
        int endBitPosition = reader.BitPosition;
        int headerBytes = (endBitPosition - startBitPosition) / 8;
        header.PayloadSize -= headerBytes;

        bool noIbc = !frameHeader.AllowIntraBlockCopy;
        bool doLoopFilter = noIbc && (frameHeader.LoopFilterParameters.FilterLevel[0] != 0 || frameHeader.LoopFilterParameters.FilterLevel[1] != 0);
        bool doCdef = noIbc && (!frameHeader.CodedLossless &&
            (frameHeader.CdefParameters.BitCount != 0 ||
            frameHeader.CdefParameters.YStrength[0] != 0 ||
            frameHeader.CdefParameters.UvStrength[0] != 0));

        bool doLoopRestoration = noIbc &&
            (frameHeader.LoopRestorationParameters.Items[(int)Av1Plane.Y].Type != ObuRestorationType.None ||
            frameHeader.LoopRestorationParameters.Items[(int)Av1Plane.U].Type != ObuRestorationType.None ||
            frameHeader.LoopRestorationParameters.Items[(int)Av1Plane.V].Type != ObuRestorationType.None);

        // The tile group stores each tile size except the last as size minus one. The last tile consumes the bytes that remain in the OBU payload.
        for (int tileNum = tileGroupStart; tileNum <= tileGroupEnd; tileNum++)
        {
            bool isLastTile = tileNum == tileGroupEnd;
            int tileDataSize;
            if (!isLastTile)
            {
                if (header.PayloadSize <= tileInfo.TileSizeBytes)
                {
                    throw new InvalidImageContentException("The AV1 tile group ends before its declared tile-size field and payload.");
                }

                uint tileDataSizeMinusOne = reader.ReadLittleEndian(tileInfo.TileSizeBytes);
                header.PayloadSize -= tileInfo.TileSizeBytes;

                // The compare uses the encoded unsigned value, before the addition of one. Otherwise, a four-byte 0xFFFFFFFF field wraps to a
                // zero-length signed tile and moves the boundary of the next tile.
                if (tileDataSizeMinusOne >= (uint)header.PayloadSize)
                {
                    throw new InvalidImageContentException("The AV1 tile size exceeds the remaining tile-group payload.");
                }

                tileDataSize = (int)tileDataSizeMinusOne + 1;
                header.PayloadSize -= tileDataSize;
            }
            else
            {
                tileDataSize = header.PayloadSize;
                header.PayloadSize = 0;
                if (tileDataSize <= 0)
                {
                    throw new InvalidImageContentException("The AV1 tile group contains an empty tile payload.");
                }
            }

            Span<byte> tileData = reader.GetSymbolReader(tileDataSize);
            decoder.ReadTile(tileData, tileNum);
        }

        nextTileStart = tileGroupEnd + 1;
        isLastTileGroup = nextTileStart == tileCount;

        if (tileGroupEnd != tileCount - 1)
        {
            return;
        }
    }

    /// <summary>
    /// Reads an optional signed quantizer-index delta.
    /// </summary>
    /// <param name="reader">The reader positioned at a delta-quantizer field.</param>
    /// <returns>The decoded delta, or zero when the field is absent.</returns>
    private static int ReadDeltaQ(ref Av1BitStreamReader reader)
    {
        int deltaQ = 0;
        if (reader.ReadBoolean())
        {
            deltaQ = reader.ReadSignedFromUnsigned(7);
        }

        return deltaQ;
    }

    /// <summary>
    /// Reads the frame-level delta-quantizer configuration.
    /// </summary>
    /// <param name="reader">The reader positioned at the delta-quantizer parameters.</param>
    /// <param name="frameHeader">The frame header that receives the parameters.</param>
    private static void ReadFrameDeltaQParameters(ref Av1BitStreamReader reader, ObuFrameHeader frameHeader)
    {
        frameHeader.DeltaQParameters.Resolution = 1;
        frameHeader.DeltaQParameters.IsPresent = false;
        if (frameHeader.QuantizationParameters.BaseQIndex > 0)
        {
            frameHeader.DeltaQParameters.IsPresent = reader.ReadBoolean();
        }

        if (frameHeader.DeltaQParameters.IsPresent)
        {
            frameHeader.DeltaQParameters.Resolution = 1 << (int)reader.ReadLiteral(2);
        }
    }

    /// <summary>
    /// Reads the frame-level delta-loop-filter configuration.
    /// </summary>
    /// <param name="reader">The reader positioned at the delta-loop-filter parameters.</param>
    /// <param name="frameHeader">The frame header that receives the parameters.</param>
    private static void ReadFrameDeltaLoopFilterParameters(ref Av1BitStreamReader reader, ObuFrameHeader frameHeader)
    {
        frameHeader.DeltaLoopFilterParameters.IsPresent = false;
        frameHeader.DeltaLoopFilterParameters.Resolution = 1;
        frameHeader.DeltaLoopFilterParameters.IsMulti = false;
        if (frameHeader.DeltaQParameters.IsPresent)
        {
            if (!frameHeader.AllowIntraBlockCopy)
            {
                frameHeader.DeltaLoopFilterParameters.IsPresent = reader.ReadBoolean();
            }

            if (frameHeader.DeltaLoopFilterParameters.IsPresent)
            {
                frameHeader.DeltaLoopFilterParameters.Resolution = 1 << (int)reader.ReadLiteral(2);
                frameHeader.DeltaLoopFilterParameters.IsMulti = reader.ReadBoolean();
            }
        }
    }

    /// <summary>
    /// Reads the base index, plane deltas, and optional quantization matrices for a frame.
    /// </summary>
    /// <param name="reader">The reader positioned at the quantization parameters.</param>
    /// <param name="sequenceHeader">The sequence header defining the active color planes.</param>
    /// <param name="frameHeader">The frame header that receives the quantization parameters.</param>
    private static void ReadQuantizationParameters(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        ObuQuantizationParameters quantParams = frameHeader.QuantizationParameters;
        ObuColorConfig colorInfo = sequenceHeader.ColorConfig;
        quantParams.BaseQIndex = (int)reader.ReadLiteral(8);
        quantParams.DeltaQDc[(int)Av1Plane.Y] = ReadDeltaQ(ref reader);
        quantParams.DeltaQAc[(int)Av1Plane.Y] = 0;
        if (colorInfo.PlaneCount > 1)
        {
            quantParams.HasSeparateUvDelta = false;
            if (colorInfo.HasSeparateUvDelta)
            {
                quantParams.HasSeparateUvDelta = reader.ReadBoolean();
            }

            quantParams.DeltaQDc[(int)Av1Plane.U] = ReadDeltaQ(ref reader);
            quantParams.DeltaQAc[(int)Av1Plane.U] = ReadDeltaQ(ref reader);
            if (quantParams.HasSeparateUvDelta)
            {
                quantParams.DeltaQDc[(int)Av1Plane.V] = ReadDeltaQ(ref reader);
                quantParams.DeltaQAc[(int)Av1Plane.V] = ReadDeltaQ(ref reader);
            }
            else
            {
                quantParams.DeltaQDc[(int)Av1Plane.V] = quantParams.DeltaQDc[(int)Av1Plane.U];
                quantParams.DeltaQAc[(int)Av1Plane.V] = quantParams.DeltaQAc[(int)Av1Plane.U];
            }
        }
        else
        {
            quantParams.DeltaQDc[(int)Av1Plane.U] = 0;
            quantParams.DeltaQAc[(int)Av1Plane.U] = 0;
            quantParams.DeltaQDc[(int)Av1Plane.V] = 0;
            quantParams.DeltaQAc[(int)Av1Plane.V] = 0;
        }

        quantParams.IsUsingQMatrix = reader.ReadBoolean();
        if (quantParams.IsUsingQMatrix)
        {
            quantParams.QMatrix[(int)Av1Plane.Y] = (int)reader.ReadLiteral(4);
            quantParams.QMatrix[(int)Av1Plane.U] = (int)reader.ReadLiteral(4);
            if (!colorInfo.HasSeparateUvDelta)
            {
                quantParams.QMatrix[(int)Av1Plane.V] = quantParams.QMatrix[(int)Av1Plane.U];
            }
            else
            {
                quantParams.QMatrix[(int)Av1Plane.V] = (int)reader.ReadLiteral(4);
            }
        }
        else
        {
            quantParams.QMatrix[(int)Av1Plane.Y] = 0;
            quantParams.QMatrix[(int)Av1Plane.U] = 0;
            quantParams.QMatrix[(int)Av1Plane.V] = 0;
        }
    }

    /// <summary>
    /// Reads the segmentation map controls and per-segment feature values.
    /// </summary>
    /// <param name="reader">The reader positioned at the segmentation parameters.</param>
    /// <param name="frameHeader">The frame header that receives the segmentation state.</param>
    /// <param name="primaryParameters">
    /// The primary-reference feature state, or <see langword="null"/> when the frame has no primary reference.
    /// </param>
    private static void ReadSegmentationParameters(
        ref Av1BitStreamReader reader,
        ObuFrameHeader frameHeader,
        ObuSegmentationParameters? primaryParameters)
    {
        frameHeader.SegmentationParameters.Enabled = reader.ReadBoolean();

        if (frameHeader.SegmentationParameters.Enabled)
        {
            if (frameHeader.PrimaryReferenceFrame == Av1Constants.PrimaryReferenceFrameNone)
            {
                frameHeader.SegmentationParameters.SegmentationUpdateMap = 1;
                frameHeader.SegmentationParameters.SegmentationTemporalUpdate = 0;
                frameHeader.SegmentationParameters.SegmentationUpdateData = 1;
            }
            else
            {
                frameHeader.SegmentationParameters.SegmentationUpdateMap = reader.ReadBoolean() ? 1 : 0;
                if (frameHeader.SegmentationParameters.SegmentationUpdateMap == 1)
                {
                    frameHeader.SegmentationParameters.SegmentationTemporalUpdate = reader.ReadBoolean() ? 1 : 0;
                }

                frameHeader.SegmentationParameters.SegmentationUpdateData = reader.ReadBoolean() ? 1 : 0;
            }

            if (frameHeader.SegmentationParameters.SegmentationUpdateData == 1)
            {
                for (int i = 0; i < Av1Constants.MaxSegmentCount; i++)
                {
                    for (int j = 0; j < Av1Constants.SegmentationLevelMax; j++)
                    {
                        int featureValue = 0;
                        bool featureEnabled = reader.ReadBoolean();
                        frameHeader.SegmentationParameters.SetFeatureEnabled(i, j, featureEnabled);
                        int clippedValue = 0;
                        if (featureEnabled)
                        {
                            int bitsToRead = Av1Constants.SegmentationFeatureBits[j];
                            int limit = Av1Constants.SegmentationFeatureMax[j];
                            if (Av1Constants.SegmentationFeatureSigned[j] == 1)
                            {
                                featureValue = reader.ReadSignedFromUnsigned(1 + bitsToRead);
                                clippedValue = Av1Math.Clip3(-limit, limit, featureValue);
                            }
                            else
                            {
                                featureValue = (int)reader.ReadLiteral(bitsToRead);
                                clippedValue = featureValue;
                            }
                        }

                        frameHeader.SegmentationParameters.SetFeatureData(i, j, clippedValue);
                    }
                }
            }
            else
            {
                // When `update_data` is zero, the frame keeps the complete feature mask and values of the primary frame. The current header owns
                // its arrays, so a later reference replacement cannot change the inherited state.
                if (primaryParameters is null)
                {
                    throw new InvalidImageContentException("AV1 segmentation cannot inherit data without a primary reference.");
                }

                frameHeader.SegmentationParameters.CopyFeaturesFrom(primaryParameters);
            }
        }
        else
        {
            for (int i = 0; i < Av1Constants.MaxSegmentCount; i++)
            {
                for (int j = 0; j < Av1Constants.SegmentationLevelMax; j++)
                {
                    frameHeader.SegmentationParameters.SetFeatureEnabled(i, j, false);
                    frameHeader.SegmentationParameters.SetFeatureData(i, j, 0);
                }
            }
        }

        frameHeader.SegmentationParameters.SegmentIdPrecedesSkip = false;
        frameHeader.SegmentationParameters.LastActiveSegmentId = 0;
        for (int i = 0; i < Av1Constants.MaxSegmentCount; i++)
        {
            for (int j = 0; j < Av1Constants.SegmentationLevelMax; j++)
            {
                if (frameHeader.SegmentationParameters.IsFeatureActive(i, (ObuSegmentationLevelFeature)j))
                {
                    frameHeader.SegmentationParameters.LastActiveSegmentId = i;
                    if (j >= (int)ObuSegmentationLevelFeature.ReferenceFrame)
                    {
                        frameHeader.SegmentationParameters.SegmentIdPrecedesSkip = true;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Reads the deblocking-loop-filter levels and optional reference and mode deltas.
    /// </summary>
    /// <param name="reader">The reader positioned at the loop-filter parameters.</param>
    /// <param name="sequenceHeader">The sequence header defining the active color planes.</param>
    private void ReadLoopFilterParameters(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader)
    {
        ObuFrameHeader frameHeader = this.CurrentFrameHeader;
        if (frameHeader.CodedLossless || frameHeader.AllowIntraBlockCopy)
        {
            return;
        }

        frameHeader.LoopFilterParameters.FilterLevel[0] = (int)reader.ReadLiteral(6);
        frameHeader.LoopFilterParameters.FilterLevel[1] = (int)reader.ReadLiteral(6);

        if (sequenceHeader.ColorConfig.PlaneCount > 1)
        {
            if (frameHeader.LoopFilterParameters.FilterLevel[0] > 0 || frameHeader.LoopFilterParameters.FilterLevel[1] > 0)
            {
                frameHeader.LoopFilterParameters.FilterLevelU = (int)reader.ReadLiteral(6);
                frameHeader.LoopFilterParameters.FilterLevelV = (int)reader.ReadLiteral(6);
            }
        }

        frameHeader.LoopFilterParameters.SharpnessLevel = (int)reader.ReadLiteral(3);
        frameHeader.LoopFilterParameters.ReferenceDeltaModeEnabled = reader.ReadBoolean();
        if (frameHeader.LoopFilterParameters.ReferenceDeltaModeEnabled)
        {
            frameHeader.LoopFilterParameters.ReferenceDeltaModeUpdate = reader.ReadBoolean();
            if (frameHeader.LoopFilterParameters.ReferenceDeltaModeUpdate)
            {
                for (int i = 0; i < Av1Constants.TotalReferencesPerFrame; i++)
                {
                    if (reader.ReadBoolean())
                    {
                        frameHeader.LoopFilterParameters.ReferenceDeltas[i] = reader.ReadSignedFromUnsigned(7);
                    }
                }

                for (int i = 0; i < 2; i++)
                {
                    if (reader.ReadBoolean())
                    {
                        frameHeader.LoopFilterParameters.ModeDeltas[i] = reader.ReadSignedFromUnsigned(7);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Reads or derives the transform-size selection mode.
    /// </summary>
    /// <param name="reader">The reader positioned at the transform-mode flag.</param>
    /// <param name="frameHeader">The frame header that receives the transform mode.</param>
    private static void ReadTransformMode(ref Av1BitStreamReader reader, ObuFrameHeader frameHeader)
    {
        if (frameHeader.CodedLossless)
        {
            frameHeader.TransformMode = Av1TransformMode.Only4x4;
        }
        else
        {
            if (reader.ReadBoolean())
            {
                frameHeader.TransformMode = Av1TransformMode.Select;
            }
            else
            {
                frameHeader.TransformMode = Av1TransformMode.Largest;
            }
        }
    }

    /// <summary>
    /// Reads the loop-restoration type and restoration-unit size for each plane.
    /// </summary>
    /// <param name="reader">The reader positioned at the loop-restoration parameters.</param>
    /// <param name="sequenceHeader">The sequence header defining restoration availability and color planes.</param>
    /// <param name="frameHeader">The frame header that receives the restoration parameters.</param>
    private static void ReadLoopRestorationParameters(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        if (frameHeader.CodedLossless || frameHeader.AllowIntraBlockCopy || !sequenceHeader.EnableRestoration)
        {
            return;
        }

        frameHeader.LoopRestorationParameters.UsesLoopRestoration = false;
        frameHeader.LoopRestorationParameters.UsesChromaLoopRestoration = false;
        int planesCount = sequenceHeader.ColorConfig.PlaneCount;
        for (int i = 0; i < planesCount; i++)
        {
            // The AV1 frame syntax orders its two restoration bits as none, switchable, Wiener, and self-guided projection. These are the
            // `ObuRestorationType` values.
            frameHeader.LoopRestorationParameters.Items[i].Type = (ObuRestorationType)reader.ReadLiteral(2);

            if (frameHeader.LoopRestorationParameters.Items[i].Type != ObuRestorationType.None)
            {
                frameHeader.LoopRestorationParameters.UsesLoopRestoration = true;
                if (i > 0)
                {
                    frameHeader.LoopRestorationParameters.UsesChromaLoopRestoration = true;
                }
            }
        }

        if (frameHeader.LoopRestorationParameters.UsesLoopRestoration)
        {
            frameHeader.LoopRestorationParameters.UnitShift = (int)reader.ReadLiteral(1);
            if (sequenceHeader.Use128x128Superblock)
            {
                frameHeader.LoopRestorationParameters.UnitShift++;
            }
            else if (frameHeader.LoopRestorationParameters.UnitShift != 0)
            {
                // A frame with 64x64 superblocks signals the extra size bit only when the first size bit selects a restoration unit larger than
                // 64 samples.
                frameHeader.LoopRestorationParameters.UnitShift += (int)reader.ReadLiteral(1);
            }

            frameHeader.LoopRestorationParameters.Items[0].Size = Av1Constants.RestorationMaxTileSize >> (2 - frameHeader.LoopRestorationParameters.UnitShift);
            frameHeader.LoopRestorationParameters.UVShift = 0;
            if (sequenceHeader.ColorConfig.SubSamplingX && sequenceHeader.ColorConfig.SubSamplingY && frameHeader.LoopRestorationParameters.UsesChromaLoopRestoration)
            {
                frameHeader.LoopRestorationParameters.UVShift = (int)reader.ReadLiteral(1);
            }

            frameHeader.LoopRestorationParameters.Items[1].Size = frameHeader.LoopRestorationParameters.Items[0].Size >> frameHeader.LoopRestorationParameters.UVShift;
            frameHeader.LoopRestorationParameters.Items[2].Size = frameHeader.LoopRestorationParameters.Items[0].Size >> frameHeader.LoopRestorationParameters.UVShift;
        }
    }

    /// <summary>
    /// Reads constrained directional enhancement filter strengths for the active planes.
    /// </summary>
    /// <param name="reader">The reader positioned at the CDEF parameters.</param>
    /// <param name="sequenceHeader">The sequence header defining CDEF availability and color planes.</param>
    /// <param name="frameHeader">The frame header that receives the CDEF parameters.</param>
    private static void ReadCdefParameters(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        ObuConstraintDirectionalEnhancementFilterParameters cdefInfo = frameHeader.CdefParameters;
        bool multiPlane = sequenceHeader.ColorConfig.PlaneCount > 1;
        if (frameHeader.CodedLossless || frameHeader.AllowIntraBlockCopy || !sequenceHeader.EnableCdef)
        {
            cdefInfo.BitCount = 0;
            cdefInfo.YStrength[0] = 0;
            cdefInfo.YStrength[4] = 0;
            cdefInfo.UvStrength[0] = 0;
            cdefInfo.UvStrength[4] = 0;
            cdefInfo.Damping = 0;
            return;
        }

        cdefInfo.Damping = (int)reader.ReadLiteral(2) + 3;
        cdefInfo.BitCount = (int)reader.ReadLiteral(2);
        for (int i = 0; i < (1 << frameHeader.CdefParameters.BitCount); i++)
        {
            cdefInfo.YStrength[i] = (int)reader.ReadLiteral(6);

            if (multiPlane)
            {
                cdefInfo.UvStrength[i] = (int)reader.ReadLiteral(6);
            }
        }
    }

    /// <summary>
    /// Reads global-motion parameters when permitted by the frame type.
    /// </summary>
    /// <param name="reader">The reader positioned at the global-motion parameters.</param>
    /// <param name="frameHeader">The current frame header.</param>
    private void ReadGlobalMotionParameters(ref Av1BitStreamReader reader, ObuFrameHeader frameHeader)
    {
        Span<Av1GlobalMotionParameters> parameters = frameHeader.GetGlobalMotionParameters();
        parameters.Fill(Av1GlobalMotionParameters.Identity);

        if (frameHeader.IsIntra)
        {
            return;
        }

        ObuFrameHeader? primaryReferenceHeader = null;
        byte? primaryReferenceSlot = frameHeader.PrimaryReferenceSlot;
        if (primaryReferenceSlot is not null)
        {
            // `primary_ref_frame` identifies the earlier frame whose models for the same seven reference roles supply the recentering values.
            // The reference slots are already valid when the reader reaches this syntax.
            Av1ReferenceFrameStore referenceFrames = this.referenceFrames
                ?? throw new InvalidImageContentException("AV1 global motion requires a reconstructed reference map.");

            primaryReferenceHeader = referenceFrames.ResolveRequired(primaryReferenceSlot.Value).FrameHeader;
        }

        for (int referenceIndex = 0; referenceIndex < Av1Constants.ReferencesPerFrame; referenceIndex++)
        {
            Av1GlobalMotionParameters referenceParameters = primaryReferenceHeader is null
                ? Av1GlobalMotionParameters.Identity
                : primaryReferenceHeader.GetGlobalMotionParameters()[referenceIndex];

            ReadGlobalMotionModel(
                ref reader,
                ref parameters[referenceIndex],
                referenceParameters,
                frameHeader.AllowHighPrecisionMotionVector);
        }
    }

    /// <summary>
    /// Reads one global-motion model relative to the corresponding model retained by the primary reference frame.
    /// </summary>
    /// <param name="reader">The reader positioned at the model type and parameter syntax.</param>
    /// <param name="parameters">The destination global-motion model.</param>
    /// <param name="referenceParameters">The same-role model retained by the primary reference frame.</param>
    /// <param name="allowHighPrecisionMotionVector">
    /// A value indicating whether translation-only parameters retain their high-precision bit.
    /// </param>
    private static void ReadGlobalMotionModel(
        ref Av1BitStreamReader reader,
        ref Av1GlobalMotionParameters parameters,
        Av1GlobalMotionParameters referenceParameters,
        bool allowHighPrecisionMotionVector)
    {
        Av1GlobalMotionType type = Av1GlobalMotionType.Identity;
        if (reader.ReadBoolean())
        {
            if (reader.ReadBoolean())
            {
                type = Av1GlobalMotionType.RotationZoom;
            }
            else
            {
                type = reader.ReadBoolean() ? Av1GlobalMotionType.Translation : Av1GlobalMotionType.Affine;
            }
        }

        parameters = Av1GlobalMotionParameters.Identity;
        parameters.Type = type;
        if (type >= Av1GlobalMotionType.RotationZoom)
        {
            // Diagonal terms are coded as a delta from the identity scale. Off-diagonal terms are centered on zero. After the decode, the
            // reader scales both back to the common sixteen-bit matrix precision.
            int referenceHorizontalScale =
                (referenceParameters[2] >> Av1GlobalMotionParameters.AlphaPrecisionDifference) -
                (1 << Av1GlobalMotionParameters.AlphaPrecisionBits);

            parameters[2] =
                (reader.ReadSignedReferenceSubexponential(
                    Av1GlobalMotionParameters.AlphaValueMagnitude,
                    Av1GlobalMotionParameters.SubexponentialGroupBitCount,
                    referenceHorizontalScale) *
                    Av1GlobalMotionParameters.AlphaDecodeFactor) +
                Av1GlobalMotionParameters.ModelScale;

            parameters[3] = reader.ReadSignedReferenceSubexponential(
                Av1GlobalMotionParameters.AlphaValueMagnitude,
                Av1GlobalMotionParameters.SubexponentialGroupBitCount,
                referenceParameters[3] >> Av1GlobalMotionParameters.AlphaPrecisionDifference) *
                Av1GlobalMotionParameters.AlphaDecodeFactor;
        }

        if (type >= Av1GlobalMotionType.Affine)
        {
            int referenceVerticalScale =
                (referenceParameters[5] >> Av1GlobalMotionParameters.AlphaPrecisionDifference) -
                (1 << Av1GlobalMotionParameters.AlphaPrecisionBits);

            parameters[4] = reader.ReadSignedReferenceSubexponential(
                Av1GlobalMotionParameters.AlphaValueMagnitude,
                Av1GlobalMotionParameters.SubexponentialGroupBitCount,
                referenceParameters[4] >> Av1GlobalMotionParameters.AlphaPrecisionDifference) *
                Av1GlobalMotionParameters.AlphaDecodeFactor;

            parameters[5] =
                (reader.ReadSignedReferenceSubexponential(
                    Av1GlobalMotionParameters.AlphaValueMagnitude,
                    Av1GlobalMotionParameters.SubexponentialGroupBitCount,
                    referenceVerticalScale) *
                    Av1GlobalMotionParameters.AlphaDecodeFactor) +
                Av1GlobalMotionParameters.ModelScale;
        }
        else
        {
            // Rotation-zoom sets the second matrix row to the perpendicular vector of the first row. Identity and translation models get the
            // same derived identity coefficients.
            parameters[4] = -parameters[3];
            parameters[5] = parameters[2];
        }

        if (type >= Av1GlobalMotionType.Translation)
        {
            // Translation-only models use a wider coordinate range than affine models. When high-precision motion is disabled, AV1 removes one
            // coded bit and adds one reconstruction shift. As a result, the displacement grid stays in quarter-sample units. Affine translation
            // keeps the fixed precision difference between the model and the translation.
            int precisionAdjustment = type == Av1GlobalMotionType.Translation && !allowHighPrecisionMotionVector ? 1 : 0;
            int translationBits = type == Av1GlobalMotionType.Translation
                ? Av1GlobalMotionParameters.AbsoluteTranslationOnlyBits - precisionAdjustment
                : Av1GlobalMotionParameters.AbsoluteTranslationBits;

            int translationPrecisionDifference = type == Av1GlobalMotionType.Translation
                ? Av1GlobalMotionParameters.ModelPrecisionBits -
                    Av1GlobalMotionParameters.TranslationOnlyPrecisionBits +
                    precisionAdjustment
                : Av1GlobalMotionParameters.ModelPrecisionBits -
                    Av1GlobalMotionParameters.TranslationPrecisionBits;

            int translationDecodeFactor = 1 << translationPrecisionDifference;
            int translationValueMagnitude = (1 << translationBits) + 1;
            parameters[0] = reader.ReadSignedReferenceSubexponential(
                translationValueMagnitude,
                Av1GlobalMotionParameters.SubexponentialGroupBitCount,
                referenceParameters[0] >> translationPrecisionDifference) * translationDecodeFactor;

            parameters[1] = reader.ReadSignedReferenceSubexponential(
                translationValueMagnitude,
                Av1GlobalMotionParameters.SubexponentialGroupBitCount,
                referenceParameters[1] >> translationPrecisionDifference) * translationDecodeFactor;
        }

        // Invalid shear does not make the frame header invalid. AV1 keeps the decoded model and marks it unavailable to warped prediction. For
        // this reason, the parameters store the validity and the reader does not throw here.
        parameters.UpdateShearParameters();
    }

    /// <summary>
    /// Reads or derives the reference prediction mode.
    /// </summary>
    /// <param name="reader">The reader positioned at the reference-mode flag.</param>
    /// <param name="frameHeader">The current frame header.</param>
    /// <returns>The frame reference mode.</returns>
    private static ObuReferenceMode ReadFrameReferenceMode(ref Av1BitStreamReader reader, ObuFrameHeader frameHeader)
    {
        if (frameHeader.IsIntra)
        {
            return ObuReferenceMode.SingleReference;
        }

        return (ObuReferenceMode)reader.ReadLiteral(1);
    }

    /// <summary>
    /// Reads skip-mode enablement when the frame is eligible to use it.
    /// </summary>
    /// <param name="reader">The reader positioned at the skip-mode syntax.</param>
    /// <param name="sequenceHeader">The sequence header controlling order hints.</param>
    /// <param name="frameHeader">The frame header that receives the skip-mode state.</param>
    private static void ReadSkipModeParameters(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        ObuSkipModeParameters parameters = frameHeader.SkipModeParameters;
        parameters.Derive(sequenceHeader.OrderHintInfo, frameHeader);
        parameters.SkipModeFlag = parameters.SkipModeAllowed && reader.ReadBoolean();
    }

    /// <summary>
    /// Reads film-grain synthesis parameters for the current displayed frame.
    /// </summary>
    /// <param name="reader">The reader positioned at the film-grain parameters.</param>
    /// <param name="sequenceHeader">The sequence header defining film-grain availability and color sampling.</param>
    /// <param name="frameHeader">The current frame header.</param>
    private void ReadFilmGrainFilterParameters(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        ObuFilmGrainParameters grainParams = frameHeader.FilmGrainParameters;
        if (!sequenceHeader.AreFilmGrainingParametersPresent || (!frameHeader.ShowFrame && !frameHeader.ShowableFrame))
        {
            return;
        }

        grainParams.ApplyGrain = reader.ReadBoolean();
        if (!grainParams.ApplyGrain)
        {
            return;
        }

        grainParams.GrainSeed = reader.ReadLiteral(16);

        if (frameHeader.FrameType == ObuFrameType.InterFrame)
        {
            grainParams.UpdateGrain = reader.ReadBoolean();
        }
        else
        {
            // Only inter frames can inherit parameters from a reference frame. When grain is enabled, other frames always carry a complete
            // parameter set.
            grainParams.UpdateGrain = true;
        }

        if (!grainParams.UpdateGrain)
        {
            grainParams.FilmGrainParamsRefIdx = reader.ReadLiteral(3);
            Span<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
            bool isSelectedReference = false;
            for (int reference = 0; reference < Av1Constants.ReferencesPerFrame; reference++)
            {
                isSelectedReference |= referenceFrameIndices[reference] == grainParams.FilmGrainParamsRefIdx;
            }

            Av1ReferenceFrame? referenceFrame = isSelectedReference
                ? this.referenceFrames?.Resolve((int)grainParams.FilmGrainParamsRefIdx)
                : null;

            if (referenceFrame is null)
            {
                throw new InvalidImageContentException("The AV1 film-grain reference does not provide retained parameters.");
            }

            uint grainSeed = grainParams.GrainSeed;
            uint referenceIndex = grainParams.FilmGrainParamsRefIdx;

            // AV1 inherits the complete parameter set, but always uses the seed that the new frame signals. The fixed inline buffers make this
            // a value copy, not nine small array allocations.
            grainParams.CopyFrom(referenceFrame.FrameHeader.FilmGrainParameters);
            grainParams.GrainSeed = grainSeed;
            grainParams.FilmGrainParamsRefIdx = referenceIndex;
            return;
        }

        grainParams.NumYPoints = reader.ReadLiteral(4);
        if (grainParams.NumYPoints > 14)
        {
            throw new InvalidImageContentException("The AV1 film-grain luma scaling function exceeds fourteen points.");
        }

        for (int i = 0; i < grainParams.NumYPoints; i++)
        {
            grainParams.PointYValue[i] = (byte)reader.ReadLiteral(8);
            if (i > 0 && grainParams.PointYValue[i] <= grainParams.PointYValue[i - 1])
            {
                throw new InvalidImageContentException("The AV1 film-grain luma scaling coordinates are not strictly increasing.");
            }

            grainParams.PointYScaling[i] = (byte)reader.ReadLiteral(8);
        }

        if (sequenceHeader.ColorConfig.IsMonochrome)
        {
            grainParams.ChromaScalingFromLuma = false;
        }
        else
        {
            grainParams.ChromaScalingFromLuma = reader.ReadBoolean();
        }

        if (sequenceHeader.ColorConfig.IsMonochrome ||
            grainParams.ChromaScalingFromLuma ||
            (sequenceHeader.ColorConfig.SubSamplingX && sequenceHeader.ColorConfig.SubSamplingY && grainParams.NumYPoints == 0))
        {
            grainParams.NumCbPoints = 0;
            grainParams.NumCrPoints = 0;
        }
        else
        {
            grainParams.NumCbPoints = reader.ReadLiteral(4);
            if (grainParams.NumCbPoints > 10)
            {
                throw new InvalidImageContentException("The AV1 film-grain blue-difference scaling function exceeds ten points.");
            }

            for (int i = 0; i < grainParams.NumCbPoints; i++)
            {
                grainParams.PointCbValue[i] = (byte)reader.ReadLiteral(8);
                if (i > 0 && grainParams.PointCbValue[i] <= grainParams.PointCbValue[i - 1])
                {
                    throw new InvalidImageContentException("The AV1 film-grain blue-difference scaling coordinates are not strictly increasing.");
                }

                grainParams.PointCbScaling[i] = (byte)reader.ReadLiteral(8);
            }

            grainParams.NumCrPoints = reader.ReadLiteral(4);
            if (grainParams.NumCrPoints > 10)
            {
                throw new InvalidImageContentException("The AV1 film-grain red-difference scaling function exceeds ten points.");
            }

            for (int i = 0; i < grainParams.NumCrPoints; i++)
            {
                grainParams.PointCrValue[i] = (byte)reader.ReadLiteral(8);
                if (i > 0 && grainParams.PointCrValue[i] <= grainParams.PointCrValue[i - 1])
                {
                    throw new InvalidImageContentException("The AV1 film-grain red-difference scaling coordinates are not strictly increasing.");
                }

                grainParams.PointCrScaling[i] = (byte)reader.ReadLiteral(8);
            }

            if (sequenceHeader.ColorConfig.SubSamplingX &&
                sequenceHeader.ColorConfig.SubSamplingY &&
                (grainParams.NumCbPoints == 0) != (grainParams.NumCrPoints == 0))
            {
                throw new InvalidImageContentException("AV1 4:2:0 film grain must apply to both chroma planes or neither.");
            }
        }

        grainParams.GrainScalingMinus8 = reader.ReadLiteral(2);
        grainParams.ArCoeffLag = reader.ReadLiteral(2);
        uint numPosLuma = 2 * grainParams.ArCoeffLag * (grainParams.ArCoeffLag + 1);

        uint numPosChroma = 0;
        if (grainParams.NumYPoints != 0)
        {
            numPosChroma = numPosLuma + 1;
            for (int i = 0; i < numPosLuma; i++)
            {
                grainParams.ArCoeffsYPlus128[i] = (byte)reader.ReadLiteral(8);
            }
        }
        else
        {
            numPosChroma = numPosLuma;
        }

        if (grainParams.ChromaScalingFromLuma || grainParams.NumCbPoints != 0)
        {
            for (int i = 0; i < numPosChroma; i++)
            {
                grainParams.ArCoeffsCbPlus128[i] = (byte)reader.ReadLiteral(8);
            }
        }

        if (grainParams.ChromaScalingFromLuma || grainParams.NumCrPoints != 0)
        {
            for (int i = 0; i < numPosChroma; i++)
            {
                grainParams.ArCoeffsCrPlus128[i] = (byte)reader.ReadLiteral(8);
            }
        }

        grainParams.ArCoeffShiftMinus6 = reader.ReadLiteral(2);
        grainParams.GrainScaleShift = reader.ReadLiteral(2);
        if (grainParams.NumCbPoints != 0)
        {
            grainParams.CbMult = reader.ReadLiteral(8);
            grainParams.CbLumaMult = reader.ReadLiteral(8);
            grainParams.CbOffset = reader.ReadLiteral(9);
        }

        if (grainParams.NumCrPoints != 0)
        {
            grainParams.CrMult = reader.ReadLiteral(8);
            grainParams.CrLumaMult = reader.ReadLiteral(8);
            grainParams.CrOffset = reader.ReadLiteral(9);
        }

        grainParams.OverlapFlag = reader.ReadBoolean();
        grainParams.ClipToRestrictedRange = reader.ReadBoolean();
    }

    /// <summary>
    /// Determines whether a sequence-level index is assigned by the AV1 specification.
    /// </summary>
    /// <param name="sequenceLevelIndex">The sequence-level index.</param>
    /// <returns><see langword="true"/> for assigned indices; otherwise, <see langword="false"/>.</returns>
    private static bool IsValidSequenceLevel(int sequenceLevelIndex)
        => sequenceLevelIndex is < 24 or 31;

    /// <summary>
    /// Returns the smallest shift for which <paramref name="blockSize"/> shifted left reaches <paramref name="target"/>.
    /// </summary>
    /// <param name="blockSize">The initial block count.</param>
    /// <param name="target">The minimum shifted value.</param>
    /// <returns>The required base-2 shift.</returns>
    public static int TileLog2(int blockSize, int target)
    {
        int k;
        for (k = 0; (blockSize << k) < target; k++)
        {
        }

        return k;
    }

    /// <summary>
    /// Supplies a tile reader from a factory that creates one reader for each coded frame.
    /// </summary>
    private readonly struct TileReaderFactoryProvider : ITileReaderProvider
    {
        private readonly Func<IAv1TileReader> creator;

        /// <summary>
        /// Initializes a new instance of the <see cref="TileReaderFactoryProvider"/> struct.
        /// </summary>
        /// <param name="creator">The factory that creates one tile reader for each coded frame.</param>
        public TileReaderFactoryProvider(Func<IAv1TileReader> creator) => this.creator = creator;

        /// <summary>
        /// Creates a new tile reader for the current frame.
        /// </summary>
        /// <returns>The new tile reader.</returns>
        public IAv1TileReader Get() => this.creator();
    }

    /// <summary>
    /// Supplies the same existing tile reader for every coded frame.
    /// </summary>
    private readonly struct FixedTileReaderProvider : ITileReaderProvider
    {
        private readonly IAv1TileReader tileReader;

        /// <summary>
        /// Initializes a new instance of the <see cref="FixedTileReaderProvider"/> struct.
        /// </summary>
        /// <param name="tileReader">The tile reader to use for every coded frame.</param>
        public FixedTileReaderProvider(IAv1TileReader tileReader) => this.tileReader = tileReader;

        /// <summary>
        /// Gets the fixed tile reader.
        /// </summary>
        /// <returns>The fixed tile reader.</returns>
        public IAv1TileReader Get() => this.tileReader;
    }
}
