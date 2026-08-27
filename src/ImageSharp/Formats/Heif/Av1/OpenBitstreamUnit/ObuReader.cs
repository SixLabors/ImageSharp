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
internal class ObuReader
{
    /// <summary>
    /// The number of bits used to address one of AV1's eight reference-map slots.
    /// </summary>
    private const int ReferenceFrameIndexBits = 3;

    /// <summary>
    /// The initial finite-subexponential group width used by every global-motion parameter.
    /// </summary>
    private const int GlobalMotionSubexponentialGroupBitCount = 3;

    /// <summary>
    /// The finite signed-domain size parameter for coded global-motion affine coefficients.
    /// </summary>
    private const int GlobalMotionAlphaValueMagnitude = (1 << 12) + 1;

    /// <summary>
    /// The number of fractional bits carried by coded global-motion affine coefficients.
    /// </summary>
    private const int GlobalMotionAlphaPrecisionBits = 15;

    /// <summary>
    /// The precision increase from a coded affine coefficient to the stored global-motion matrix.
    /// </summary>
    private const int GlobalMotionAlphaPrecisionDifference =
        Av1GlobalMotionParameters.ModelPrecisionBits - GlobalMotionAlphaPrecisionBits;

    /// <summary>
    /// The scale factor that restores a coded affine coefficient to the global-motion matrix precision.
    /// </summary>
    private const int GlobalMotionAlphaDecodeFactor = 1 << GlobalMotionAlphaPrecisionDifference;

    /// <summary>
    /// The signed magnitude bit count of a general affine model's translation components.
    /// </summary>
    private const int GlobalMotionAbsoluteTranslationBits = 12;

    /// <summary>
    /// The signed magnitude bit count of a translation-only model before precision adjustment.
    /// </summary>
    private const int GlobalMotionAbsoluteTranslationOnlyBits = 9;

    /// <summary>
    /// The number of fractional bits carried by general affine translation components.
    /// </summary>
    private const int GlobalMotionTranslationPrecisionBits = 6;

    /// <summary>
    /// The number of fractional bits carried by translation-only components.
    /// </summary>
    private const int GlobalMotionTranslationOnlyPrecisionBits = 3;

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
    /// The tile reader created for the current coded frame.
    /// </summary>
    private IAv1TileReader? decoder;

    /// <summary>
    /// The temporal- and spatial-layer mask for the selected operating point.
    /// </summary>
    private uint currentOperatingPointIdc;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObuReader"/> class using operating-point index zero without a
    /// reconstructed reference map.
    /// </summary>
    public ObuReader()
        : this(0)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ObuReader"/> class for one selected AV1 operating point without a
    /// reconstructed reference map.
    /// </summary>
    /// <param name="operatingPointIndex">The zero-based sequence-header operating-point index to decode.</param>
    public ObuReader(byte operatingPointIndex)
        => this.operatingPointIndex = operatingPointIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObuReader"/> class for one selected AV1 operating point and
    /// retained reference map.
    /// </summary>
    /// <param name="operatingPointIndex">The zero-based sequence-header operating-point index to decode.</param>
    /// <param name="referenceFrames">The reconstructed reference frames retained by the owning decoder.</param>
    public ObuReader(byte operatingPointIndex, Av1ReferenceFrameStore referenceFrames)
    {
        this.operatingPointIndex = operatingPointIndex;
        this.referenceFrames = referenceFrames;
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
    /// Parses every open bitstream unit in one bounded AV1 payload.
    /// </summary>
    /// <param name="reader">The reader positioned at the first OBU.</param>
    /// <param name="dataSize">The number of bytes available for the bounded payload.</param>
    /// <param name="creator">Creates one tile reader when the first tile payload of each coded frame is encountered.</param>
    /// <param name="isAnnexB">A value indicating whether each OBU is prefixed by an Annex B length field.</param>
    public void ReadAll(ref Av1BitStreamReader reader, int dataSize, Func<IAv1TileReader> creator, bool isAnnexB = false)
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

                // AV1-ISOBMFF permits the final low-overhead OBU to omit its size field. In that form the remaining
                // sample bytes are the payload, which also makes this OBU final because no following boundary exists.
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

                // A dedicated payload reader prevents malformed syntax from consuming the following OBU. The parent
                // advances once here, so ignored metadata, padding, and reserved OBUs are skipped without copying.
                Span<byte> obuPayload = reader.ReadBytes(payloadSize);

                // AV1 operating_point_idc uses bits 0-7 for temporal IDs and bits 8-11 for spatial IDs. libaom
                // requires both selected bits for an extended OBU, while an all-zero mask and unextended OBUs apply
                // universally. Sequence headers establish the mask and temporal delimiters define framing, so neither
                // can be filtered even when their extension identifies a layer outside the selected operating point.
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
                bool frameDecodingFinished = false;
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

                        // A sequence header starts a new reference domain. Clear both the syntax snapshot and decoded
                        // owners only after the complete header and selected operating point have been accepted, so a
                        // later inter header cannot pair an empty parser map with samples retained from the old sequence.
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

                        // The primary header already owns the decoded frame state. Matching its encoded bytes avoids
                        // parsing the same adaptive frame-header syntax twice, as in libaom's decoder.
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
                        goto TILE_GROUP;
                    case ObuType.TileGroup:
                        TILE_GROUP:
                        if (!seenFrameHeader)
                        {
                            throw new InvalidImageContentException("An AV1 tile group appears before its frame header.");
                        }

                        this.decoder ??= creator();

                        // A combined frame OBU reaches this label after its frame-header portion has
                        // been consumed, leaving the same tile-group syntax as a standalone tile OBU.
                        this.ReadTileGroup(ref payloadReader, this.decoder, header, ref nextTileStart, out frameDecodingFinished);
                        decodedPayloadSize = Av1Math.DivideBy8Floor(payloadReader.BitPosition);
                        break;
                    case ObuType.TemporalDelimiter:
                        if (seenFrameHeader)
                        {
                            throw new InvalidImageContentException("An AV1 temporal delimiter interrupts an incomplete coded frame.");
                        }

                        // AV1 section 5.6 defines no delimiter syntax. The common post-switch validation still permits
                        // zero bytes between the empty syntax and the declared payload boundary, matching libaom.
                        decodedPayloadSize = 0;
                        break;
                    case ObuType.Padding:
                        int lastNonzeroIndex = obuPayload.Length - 1;
                        while (lastNonzeroIndex >= 0 && obuPayload[lastNonzeroIndex] == 0)
                        {
                            lastNonzeroIndex--;
                        }

                        // AV1 padding contains only its trailing one bit and optional zero bytes. A header-only
                        // padding OBU is also valid, so the empty payload bypasses this final-byte check.
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
                        // Metadata, tile-list, and reserved OBUs do not contribute to this still-image reconstruction
                        // pass. Their declared payload has already been skipped by the parent reader. libaom rejects a
                        // nonempty unrecognized payload that contains only zeros because it has no trailing one bit.
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

                // Parsed syntax may be followed only by zero bytes within its declared OBU payload. Ignored metadata
                // and reserved OBUs set decodedPayloadSize to the full payload because their syntax is not consumed here.
                for (int i = decodedPayloadSize; i < obuPayload.Length; i++)
                {
                    if (obuPayload[i] != 0)
                    {
                        throw new InvalidImageContentException("The AV1 OBU contains nonzero data after its decoded syntax.");
                    }
                }

                if (frameDecodingFinished)
                {
                    // Complete reconstruction and reference-buffer ownership before publishing the matching syntax
                    // state. Any decoder failure leaves the preceding session snapshot intact for deterministic cleanup.
                    this.decoder!.CompleteFrame();
                    this.frameReferenceState.CompleteFrame(this.FrameHeader!, this.SequenceHeader!.IsFrameIdNumbersPresent);
                    this.decoder = null;
                    seenFrameHeader = false;
                    nextTileStart = 0;
                    primaryFrameHeaderPayload = default;
                }
            }

            if (seenFrameHeader || this.decoder is not null)
            {
                throw new InvalidImageContentException("The AV1 payload ends before the current coded frame is complete.");
            }

            completed = true;
        }
        catch (IndexOutOfRangeException exception)
        {
            throw new InvalidImageContentException("The AV1 OBU syntax exceeds its payload boundary.", exception);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidImageContentException("The AV1 OBU syntax exceeds its payload boundary.", exception);
        }
        finally
        {
            if (!completed)
            {
                // A bounded payload can commit earlier layers before a later OBU fails. Those transitions cannot be
                // rolled back after displaced owners have been released, so invalidate the complete decoder session.
                this.Reset();
            }
        }
    }

    /// <summary>
    /// Clears all parser, tile-reader, syntax-reference, and reconstructed-reference state owned by this session.
    /// </summary>
    public void Reset()
    {
        this.decoder = null;
        this.SequenceHeader = null;
        this.FrameHeader = null;
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
        if (reader.ReadBoolean())
        {
            throw new ImageFormatException("Reserved bit in header should be unset.");
        }

        if (header.HasExtension)
        {
            header.Size++;
            header.TemporalId = (int)reader.ReadLiteral(3);
            header.SpatialId = (int)reader.ReadLiteral(2);
            if (reader.ReadLiteral(3) != 0u)
            {
                throw new ImageFormatException("Reserved bits in header extension should be unset.");
            }
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
        while ((reader.BitPosition & 0x7) > 0)
        {
            if (reader.ReadBoolean())
            {
                throw new ImageFormatException("Incorrect byte alignment padding bits.");
            }
        }
    }

    /// <summary>
    /// Computes the mode-information dimensions and stride for the current frame.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header defining the maximum frame geometry and superblock size.</param>
    /// <remarks>SVT: compute_image_size</remarks>
    private void ComputeImageSize(ObuSequenceHeader sequenceHeader)
    {
        ObuFrameHeader frameHeader = this.FrameHeader!;
        frameHeader.ModeInfoColumnCount = 2 * ((frameHeader.FrameSize.FrameWidth + 7) >> 3);
        frameHeader.ModeInfoRowCount = 2 * ((frameHeader.FrameSize.FrameHeight + 7) >> 3);
        frameHeader.ModeInfoStride = Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameWidth, Av1Constants.MaxSuperBlockSizeLog2) >> Av1Constants.ModeInfoSizeLog2;
    }

    /// <summary>
    /// Reads an AV1 sequence-header OBU payload.
    /// </summary>
    /// <param name="reader">The reader positioned at the sequence-header payload.</param>
    /// <param name="sequenceHeader">The sequence header to populate.</param>
    internal static void ReadSequenceHeader(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader)
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
            // The reduced header omits state required by a multi-frame sequence, so AV1 permits it only when the
            // sequence is explicitly declared to contain a single still picture.
            throw new InvalidImageContentException("An AV1 reduced still-picture header requires the still-picture flag.");
        }

        if (sequenceHeader.IsReducedStillPictureHeader)
        {
            sequenceHeader.TimingInfo = null;
            sequenceHeader.DecoderModelInfoPresentFlag = false;
            sequenceHeader.InitialDisplayDelayPresentFlag = false;
            sequenceHeader.OperatingPoint = new ObuOperatingPoint[1];
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
            uint operatingPointsCnt = reader.ReadLiteral(5) + 1;
            sequenceHeader.OperatingPoint = new ObuOperatingPoint[operatingPointsCnt];
            for (int i = 0; i < operatingPointsCnt; i++)
            {
                sequenceHeader.OperatingPoint[i] = new ObuOperatingPoint
                {
                    Idc = reader.ReadLiteral(12),
                    SequenceLevelIndex = (int)reader.ReadLiteral(5)
                };
                if (sequenceHeader.OperatingPoint[i].SequenceLevelIndex > 7)
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
                        // Operating-point delays affect scheduling rather than still-image reconstruction, but their
                        // syntax must be consumed so the following image dimensions remain bit aligned.
                        ReadOperatingParametersInfo(ref reader, (int)sequenceHeader.DecoderModelInfo!.BufferDelayLength);
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
                    }
                }
            }
        }

        // Video related flags removed

        // SVT-TODO: int operatingPoint = this.ChooseOperatingPoint();
        // sequenceHeader.OperatingPointIndex = (int)operatingPointIndices[operatingPoint];
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
        }

        // Video related flags removed
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
            sequenceHeader.ForceScreenContentTools = 2; // SELECT_SCREEN_CONTENT_TOOLS
            sequenceHeader.ForceIntegerMotionVector = 2; // SELECT_INTEGER_MV
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
                sequenceHeader.ForceScreenContentTools = 2; // SELECT_SCREEN_CONTENT_TOOLS
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
                    sequenceHeader.ForceIntegerMotionVector = 2; // SELECT_INTEGER_MV
                }
                else
                {
                    sequenceHeader.ForceIntegerMotionVector = (int)reader.ReadLiteral(1);
                }
            }
            else
            {
                sequenceHeader.ForceIntegerMotionVector = 2; // SELECT_INTEGER_MV
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

        // Video related flags removed
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
            // AV1 defines this RGB identity-matrix combination as full-range 4:4:4 and omits
            // the range and subsampling syntax that other color combinations carry.
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
    /// Consumes operating-point buffer parameters that do not affect still-image reconstruction.
    /// </summary>
    /// <param name="reader">The reader positioned at the operating-point parameters.</param>
    /// <param name="bufferDelayLength">The bit width of each encoded buffer delay.</param>
    private static void ReadOperatingParametersInfo(ref Av1BitStreamReader reader, int bufferDelayLength)
    {
        _ = reader.ReadLiteral(bufferDelayLength);
        _ = reader.ReadLiteral(bufferDelayLength);
        _ = reader.ReadBoolean();
    }

    /// <summary>
    /// Reads the sequence timing information.
    /// </summary>
    /// <param name="reader">The reader positioned at the timing-information syntax.</param>
    /// <param name="sequenceHeader">The sequence header that receives the timing information.</param>
    private static void ReadTimingInfo(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader)
    {
        sequenceHeader.TimingInfo = new ObuTimingInfo
        {
            NumUnitsInDisplayTick = reader.ReadLiteral(32),
            TimeScale = reader.ReadLiteral(32),
            EqualPictureInterval = reader.ReadBoolean()
        };

        if (sequenceHeader.TimingInfo.EqualPictureInterval)
        {
            sequenceHeader.TimingInfo.NumTicksPerPicture = reader.ReadUnsignedVariableLength() + 1;
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
        ObuSequenceHeader sequenceHeader = this.SequenceHeader!;
        ObuFrameHeader frameHeader = this.FrameHeader!;
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

        // AV1 signals the upscaled width first. Tile and block decoding use the nearest-integer coded width obtained
        // from the fixed scale numerator and signaled denominator.
        frameHeader.FrameSize.FrameWidth =
            ((frameHeader.FrameSize.SuperResolutionUpscaledWidth * Av1Constants.ScaleNumerator) +
            (frameHeader.FrameSize.SuperResolutionDenominator / 2)) /
            frameHeader.FrameSize.SuperResolutionDenominator;

        if (frameHeader.FrameSize.SuperResolutionDenominator != Av1Constants.ScaleNumerator)
        {
            // Appendix A requires an active super-resolution coded width of at least 16 samples,
            // except when the signaled upscaled image itself is narrower than that minimum.
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
        ObuFrameHeader frameHeader = this.FrameHeader!;
        bool renderSizeAndFrameSizeDifferent = reader.ReadBoolean();

        if (renderSizeAndFrameSizeDifferent)
        {
            // render_width_minus_1 and render_height_minus_1 are fixed 16-bit fields, independent of the sequence's
            // coded-dimension bit widths.
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
        ObuSequenceHeader sequenceHeader = this.SequenceHeader!;
        ObuFrameHeader frameHeader = this.FrameHeader!;

        if (frameSizeOverrideFlag)
        {
            frameHeader.FrameSize.FrameWidth = (int)reader.ReadLiteral(sequenceHeader.FrameWidthBits) + 1;
            frameHeader.FrameSize.FrameHeight = (int)reader.ReadLiteral(sequenceHeader.FrameHeightBits) + 1;

            // Section 5.9.7 signals frame dimensions using the sequence maxima's bit widths, but the resulting values
            // remain constrained by those maxima. Rejecting the oversized result here prevents later buffer geometry
            // from accepting a value that the sequence header does not permit.
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
        ObuSequenceHeader sequenceHeader = this.SequenceHeader!;
        ObuFrameHeader frameHeader = this.FrameHeader!;
        ObuFrameSize frameSize = frameHeader.FrameSize;
        Span<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
        bool foundReference = false;

        // frame_size_with_refs carries one found_ref bit per selected role only until the first one is set. A set bit
        // terminates this syntax immediately; no flags for the remaining roles are present in the bitstream.
        for (int reference = 0; reference < Av1Constants.ReferencesPerFrame; reference++)
        {
            if (!reader.ReadBoolean())
            {
                continue;
            }

            Av1ReferenceFrame referenceFrame = referenceFrames.Resolve((int)referenceFrameIndices[reference])!;
            ObuFrameSize referenceSize = referenceFrame.FrameHeader.FrameSize;

            // AV1 5.9.7 inherits the reference buffer's visible post-super-resolution dimensions, corresponding to
            // libaom's y_crop_width and y_crop_height, plus its render rectangle. The current frame then signals its own
            // super-resolution denominator, so the reference's coded width and denominator are not copied.
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
            // When no reference supplies dimensions, frame_size_with_refs carries the ordinary explicit frame size,
            // current super-resolution syntax, and render-size syntax in that order.
            this.ReadFrameSize(ref reader, true);
            this.ReadRenderSize(ref reader);
        }

        bool hasCompatibleReferenceSize = false;
        ObuColorConfig colorConfig = sequenceHeader.ColorConfig;

        for (int reference = 0; reference < Av1Constants.ReferencesPerFrame; reference++)
        {
            Av1ReferenceFrame referenceFrame = referenceFrames.Resolve((int)referenceFrameIndices[reference])!;
            int referenceWidth = referenceFrame.FrameBuffer.Width;
            int referenceHeight = referenceFrame.FrameBuffer.Height;

            // AV1 6.8.6 permits a reference dimension from one half through sixteen times the current coded
            // dimension. setup_frame_size_with_refs requires at least one of the seven selected roles to satisfy both
            // axes before the frame may proceed.
            hasCompatibleReferenceSize |=
                (2 * frameSize.FrameWidth) >= referenceWidth &&
                (2 * frameSize.FrameHeight) >= referenceHeight &&
                frameSize.FrameWidth <= (16 * referenceWidth) &&
                frameSize.FrameHeight <= (16 * referenceHeight);

            ObuColorConfig referenceColorConfig = referenceFrame.FrameBuffer.ColorConfig;

            // Every selected reference participates in the same prediction sample domain. Mixing bit depth or chroma
            // subsampling would change sample interpretation and is prohibited even when that role is not selected by
            // any block in the current frame.
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
        // A leading one omits the two-bit fixed-family field and delegates the choice to each inter block. Otherwise,
        // the literal values map directly to regular, smooth, sharp, and bilinear as defined by AV1 6.10.2.
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

        // The bitstream constrains tile dimensions in superblocks, while the decoder stores
        // boundaries in mode-information units for direct use during block traversal.
        tileInfo.MaxTileWidthSuperblock = Av1Constants.MaxTileWidth >> superblockSizeLog2;
        tileInfo.MaxTileHeightSuperblock = (Av1Constants.MaxTileArea / Av1Constants.MaxTileWidth) >> superblockSizeLog2;
        tileInfo.MinLog2TileColumnCount = TileLog2(tileInfo.MaxTileWidthSuperblock, superblockColumnCount);
        tileInfo.MaxLog2TileColumnCount = (int)Av1Math.CeilLog2((uint)Math.Min(superblockColumnCount, Av1Constants.MaxTileColumnCount));
        tileInfo.MaxLog2TileRowCount = (int)Av1Math.CeilLog2((uint)Math.Min(superblockRowCount, Av1Constants.MaxTileRowCount));
        tileInfo.MinLog2TileCount = Math.Max(tileInfo.MinLog2TileColumnCount, TileLog2(maxTileAreaOfSuperBlock, superblockColumnCount * superblockRowCount));
        tileInfo.HasUniformTileSpacing = reader.ReadBoolean();
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
            tileInfo.TileColumnStartModeInfo = new int[superblockColumnCount + 1];
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
            tileInfo.TileRowStartModeInfo = new int[superblockRowCount + 1];
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
    /// Reads the uncompressed syntax for one coded frame in a bounded AV1 image item or layered image sequence.
    /// </summary>
    /// <param name="reader">The reader positioned at the uncompressed frame header.</param>
    /// <param name="header">The OBU header identifying the frame's temporal and spatial layers.</param>
    private void ReadUncompressedFrameHeader(ref Av1BitStreamReader reader, ObuHeader header)
    {
        ObuSequenceHeader sequenceHeader = this.SequenceHeader!;
        ObuFrameHeader frameHeader = this.FrameHeader!;
        Av1ReferenceFrame? primaryReference = null;
        int idLength = sequenceHeader.FrameIdLength;
        bool frameSizeOverrideFlag = false;
        if (sequenceHeader.IsFrameIdNumbersPresent)
        {
            DebugGuard.MustBeLessThanOrEqualTo(idLength, 16, nameof(idLength));
        }

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

                frameHeader.FrameToShowMapIdx = reader.ReadLiteral(3);

                if (sequenceHeader.DecoderModelInfoPresentFlag && sequenceHeader.TimingInfo?.EqualPictureInterval == false)
                {
                    // 5.9.31. Temporal point info syntax.
                    frameHeader.FramePresentationTime = reader.ReadLiteral((int)sequenceHeader!.DecoderModelInfo!.FramePresentationTimeLength);
                }

                if (sequenceHeader.IsFrameIdNumbersPresent)
                {
                    frameHeader.DisplayFrameId = reader.ReadLiteral(idLength);
                }

                // The bounded image-item decoder retains reference state only to reconstruct coded dependent layers.
                // show_existing_frame is a presentation-timeline operation and remains outside that image-only scope.
                throw new InvalidImageContentException("An AV1 image item cannot display a previously decoded frame.");
            }

            frameHeader.FrameType = (ObuFrameType)reader.ReadLiteral(2);
            frameHeader.ShowFrame = reader.ReadBoolean();
            if (sequenceHeader.IsStillPicture && (frameHeader.FrameType != ObuFrameType.KeyFrame || !frameHeader.ShowFrame))
            {
                throw new InvalidImageContentException("An AV1 still picture must be encoded as a shown key frame.");
            }

            if (frameHeader.ShowFrame && sequenceHeader.DecoderModelInfoPresentFlag && sequenceHeader.TimingInfo?.EqualPictureInterval == false)
            {
                // 5.9.31. Temporal point info syntax.
                frameHeader.FramePresentationTime = reader.ReadLiteral((int)sequenceHeader!.DecoderModelInfo!.FramePresentationTimeLength);
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
                int bufferRemovalTimeLength = (int)sequenceHeader.DecoderModelInfo!.BufferRemovalTimeLength;
                foreach (ObuOperatingPoint operatingPoint in sequenceHeader.OperatingPoint)
                {
                    // A layer-specific OBU carries one removal time only for operating points which select both
                    // of its layer IDs; the value affects scheduling, so consume it without retaining video state.
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
            DebugGuard.IsTrue(frameHeader.RefreshFrameFlags != 0xFFU, nameof(frameHeader.RefreshFrameFlags));
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
                // Inter-frame size syntax reads dimensions from reconstructed references. Header-only parser users do
                // not own those samples, while the production decoder establishes this dependency in its constructor.
                throw new InvalidOperationException("AV1 inter-frame parsing requires a reconstructed reference map.");
            }

            ReadReferenceFrameIndices(ref reader, sequenceHeader, frameHeader, retainedReferenceFrames);

            if (frameHeader.PrimaryReferenceSlot.HasValue)
            {
                // Reference-index parsing validates the resolved slot before publishing it on the header. Retaining
                // the owner here keeps every inherited frame state tied to the same normative primary reference.
                primaryReference = retainedReferenceFrames.Resolve(frameHeader.PrimaryReferenceSlot.Value)!;
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
            // AV1 5.9.2 carries this flag only when temporal order hints and the sequence-level reference-MV tool are
            // both available. All other frame classes derive false without consuming a bit.
            frameHeader.UseReferenceFrameMotionVectors = reader.ReadBoolean();
        }

        // SetupFrameBufferReferences(sequenceHeader, frameHeader);
        // CheckAddTemporalMotionVectorBuffer(sequenceHeader, frameHeader);

        // SetupFrameSignBias(sequenceHeader, frameHeader);
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
            // When update flags omit new values, loop-filter deltas inherit from the primary frame. Copying the two
            // fixed tables before parsing lets the existing header object retain unchanged entries without aliases.
            primaryReference.FrameHeader.LoopFilterParameters.ReferenceDeltas.AsSpan().CopyTo(frameHeader.LoopFilterParameters.ReferenceDeltas);
            primaryReference.FrameHeader.LoopFilterParameters.ModeDeltas.AsSpan().CopyTo(frameHeader.LoopFilterParameters.ModeDeltas);
        }

        // Entropy defaults depend on base_q_idx, which follows tile information in the header. Av1TileReader therefore
        // loads either the retained primary snapshot or the selected quantizer-band defaults at the first tile boundary.

        // GenerateNextReferenceFrameMap(sequenceHeader, frameHeader);
        frameHeader.TilesInfo = ReadTileInfo(ref reader, sequenceHeader, frameHeader);
        ReadQuantizationParameters(ref reader, sequenceHeader, frameHeader);
        ReadSegmentationParameters(ref reader, frameHeader, primaryReference?.FrameHeader.SegmentationParameters);
        ReadFrameDeltaQParameters(ref reader, frameHeader);
        ReadFrameDeltaLoopFilterParameters(ref reader, frameHeader);

        // SetupSegmentationDequantization();
        // The primary frame retains its decoded segment map in Av1FrameInfo. Inter block parsing copies or predicts
        // segment identifiers from that map according to update_map instead of duplicating it in the frame header.
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
            uint lastFrameIndex = reader.ReadLiteral(ReferenceFrameIndexBits);
            uint goldenFrameIndex = reader.ReadLiteral(ReferenceFrameIndexBits);
            InlineArray8<bool> slotOccupancyStorage = default;
            Span<bool> slotOccupancy = slotOccupancyStorage;

            referenceFrames.FillOccupancy(slotOccupancy);

            // Short signaling transmits only LAST and GOLDEN. The remaining five roles are a normative derivation from
            // the persisted slot order hints and physical slot occupancy, not frame-ID validity or a decoder heuristic.
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
                slot = reader.ReadLiteral(ReferenceFrameIndexBits);
                referenceFrameIndices[reference] = slot;
            }

            // Slot occupancy and frame-ID validity are independent normative states. Short signaling derives roles
            // from every occupied slot before this per-role validity check, matching av1_set_frame_refs followed by
            // libaom's valid_for_referencing check.
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
            // primary_ref_frame indexes the seven inter-reference roles, not the eight-slot retained map. Resolve it
            // once so entropy, segmentation, loop-filter, and motion state all select the same retained owner later.
            frameHeader.PrimaryReferenceSlot = (byte)referenceFrameIndices[(int)frameHeader.PrimaryReferenceFrame];
        }
    }

    /// <summary>
    /// Determines whether segmentation and a specific per-segment feature are both enabled.
    /// </summary>
    /// <param name="segmentationParameters">The frame segmentation state.</param>
    /// <param name="segmentId">The segment identifier.</param>
    /// <param name="feature">The feature to inspect.</param>
    /// <returns><see langword="true"/> when the feature is active; otherwise, <see langword="false"/>.</returns>
    private static bool IsSegmentationFeatureActive(ObuSegmentationParameters segmentationParameters, int segmentId, ObuSegmentationLevelFeature feature)
        => segmentationParameters.Enabled && segmentationParameters.IsFeatureActive(segmentId, feature);

    /// <summary>
    /// Reads an AV1 frame header and removes its byte length from the remaining OBU payload size.
    /// </summary>
    /// <param name="reader">The reader positioned at the frame-header payload.</param>
    /// <param name="header">The OBU header whose remaining payload size is updated.</param>
    /// <param name="trailingBit">A value indicating whether trailing-bit syntax follows the frame header.</param>
    internal void ReadFrameHeader(ref Av1BitStreamReader reader, ObuHeader header, bool trailingBit)
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
        ObuSequenceHeader sequenceHeader = this.SequenceHeader!;
        ObuFrameHeader frameHeader = this.FrameHeader!;
        ObuTileGroupHeader tileInfo = this.FrameHeader!.TilesInfo;
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

        // All tile sizes except the final size are explicitly stored as size-minus-one. The
        // last tile consumes the bytes that remain in the OBU payload.
        for (int tileNum = tileGroupStart; tileNum <= tileGroupEnd; tileNum++)
        {
            bool isLastTile = tileNum == tileGroupEnd;
            int tileDataSize = header.PayloadSize;
            if (!isLastTile)
            {
                tileDataSize = (int)reader.ReadLittleEndian(tileInfo.TileSizeBytes) + 1;
                header.PayloadSize -= tileDataSize + tileInfo.TileSizeBytes;
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

        // TODO: Share doCdef and doLoopRestoration
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
                        frameHeader.SegmentationParameters.FeatureEnabled[i, j] = featureEnabled;
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

                        frameHeader.SegmentationParameters.FeatureData[i, j] = clippedValue;
                    }
                }
            }
            else
            {
                // update_data equal to zero preserves the complete feature mask and values from the primary frame.
                // The current header owns its arrays, so later reference replacement cannot mutate inherited state.
                frameHeader.SegmentationParameters.CopyFeaturesFrom(primaryParameters!);
            }
        }
        else
        {
            for (int i = 0; i < Av1Constants.MaxSegmentCount; i++)
            {
                for (int j = 0; j < Av1Constants.SegmentationLevelMax; j++)
                {
                    frameHeader.SegmentationParameters.FeatureEnabled[i, j] = false;
                    frameHeader.SegmentationParameters.FeatureData[i, j] = 0;
                }
            }
        }

        frameHeader.SegmentationParameters.SegmentIdPrecedesSkip = false;
        frameHeader.SegmentationParameters.LastActiveSegmentId = 0;
        for (int i = 0; i < Av1Constants.MaxSegmentCount; i++)
        {
            for (int j = 0; j < Av1Constants.SegmentationLevelMax; j++)
            {
                if (frameHeader.SegmentationParameters.FeatureEnabled[i, j])
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
        ObuFrameHeader frameHeader = this.FrameHeader!;
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
            // The AV1 frame syntax orders its two restoration bits as none, switchable,
            // Wiener, and self-guided projection, matching ObuRestorationType values.
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
                // A 64x64-superblock frame signals the extra size bit only after selecting a
                // restoration unit larger than 64 samples with the first size bit.
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
            // primary_ref_frame identifies the preceding frame whose same seven canonical reference roles supply the
            // recentering values. Reference-slot validation has already completed before this syntax is reached.
            primaryReferenceHeader = this.referenceFrames!.Resolve(primaryReferenceSlot.Value)!.FrameHeader;
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
            // Diagonal terms are coded as a delta from the identity scale, whereas off-diagonal terms are centered
            // directly around zero. Both are restored to the common sixteen-bit matrix precision after decoding.
            parameters[2] =
                (reader.ReadSignedReferenceSubexponential(
                    GlobalMotionAlphaValueMagnitude,
                    GlobalMotionSubexponentialGroupBitCount,
                    (referenceParameters[2] >> GlobalMotionAlphaPrecisionDifference) - (1 << GlobalMotionAlphaPrecisionBits)) * GlobalMotionAlphaDecodeFactor) +
                Av1GlobalMotionParameters.ModelScale;

            parameters[3] = reader.ReadSignedReferenceSubexponential(
                GlobalMotionAlphaValueMagnitude,
                GlobalMotionSubexponentialGroupBitCount,
                referenceParameters[3] >> GlobalMotionAlphaPrecisionDifference) * GlobalMotionAlphaDecodeFactor;
        }

        if (type >= Av1GlobalMotionType.Affine)
        {
            parameters[4] = reader.ReadSignedReferenceSubexponential(
                GlobalMotionAlphaValueMagnitude,
                GlobalMotionSubexponentialGroupBitCount,
                referenceParameters[4] >> GlobalMotionAlphaPrecisionDifference) * GlobalMotionAlphaDecodeFactor;

            parameters[5] =
                (reader.ReadSignedReferenceSubexponential(
                    GlobalMotionAlphaValueMagnitude,
                    GlobalMotionSubexponentialGroupBitCount,
                    (referenceParameters[5] >> GlobalMotionAlphaPrecisionDifference) - (1 << GlobalMotionAlphaPrecisionBits)) * GlobalMotionAlphaDecodeFactor) +
                Av1GlobalMotionParameters.ModelScale;
        }
        else
        {
            // Rotation-zoom constrains the second matrix row to the perpendicular vector of the first row. Identity
            // and translation models retain the same derived identity coefficients.
            parameters[4] = -parameters[3];
            parameters[5] = parameters[2];
        }

        if (type >= Av1GlobalMotionType.Translation)
        {
            // Translation-only models use a wider coordinate domain than affine models. When high-precision motion is
            // disabled, AV1 removes one coded bit and adds one reconstruction shift so the physical displacement grid
            // remains in quarter-sample units. Affine translation retains the fixed model-to-translation precision gap.
            int precisionAdjustment = type == Av1GlobalMotionType.Translation && !allowHighPrecisionMotionVector ? 1 : 0;
            int translationBits = type == Av1GlobalMotionType.Translation
                ? GlobalMotionAbsoluteTranslationOnlyBits - precisionAdjustment
                : GlobalMotionAbsoluteTranslationBits;

            int translationPrecisionDifference = type == Av1GlobalMotionType.Translation
                ? Av1GlobalMotionParameters.ModelPrecisionBits - GlobalMotionTranslationOnlyPrecisionBits + precisionAdjustment
                : Av1GlobalMotionParameters.ModelPrecisionBits - GlobalMotionTranslationPrecisionBits;

            int translationDecodeFactor = 1 << translationPrecisionDifference;
            int translationValueMagnitude = (1 << translationBits) + 1;
            parameters[0] = reader.ReadSignedReferenceSubexponential(
                translationValueMagnitude,
                GlobalMotionSubexponentialGroupBitCount,
                referenceParameters[0] >> translationPrecisionDifference) * translationDecodeFactor;

            parameters[1] = reader.ReadSignedReferenceSubexponential(
                translationValueMagnitude,
                GlobalMotionSubexponentialGroupBitCount,
                referenceParameters[1] >> translationPrecisionDifference) * translationDecodeFactor;
        }

        // Invalid shear does not invalidate the frame header. AV1 retains the decoded model and marks it unavailable
        // to warped prediction, which is why validity is stored with the parameters instead of throwing here.
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
            // Only inter frames can inherit parameters from a reference frame. Intra frames always carry a complete
            // parameter set when grain is enabled.
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

            // AV1 inherits the complete parameter set but always uses the new frame's independently signaled seed.
            // Fixed inline buffers make this a value copy rather than nine small array allocations.
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
}
