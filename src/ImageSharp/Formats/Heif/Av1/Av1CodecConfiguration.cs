// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Contains the image-description fields stored in an AV1 codec-configuration item property.
/// </summary>
internal sealed class Av1CodecConfiguration
{
    /// <summary>
    /// The optional open bitstream units following the fixed four-byte configuration record.
    /// </summary>
    private readonly byte[] configObus;

    /// <summary>
    /// The byte offset of the optional sequence-header payload within <see cref="configObus"/>, or <c>-1</c> when
    /// the configuration contains no sequence header.
    /// </summary>
    private readonly int configSequenceHeaderOffset;

    /// <summary>
    /// The byte length of the optional sequence-header payload within <see cref="configObus"/>.
    /// </summary>
    private readonly int configSequenceHeaderLength;

    /// <summary>
    /// The sequence-header OBU extension byte, or <c>-1</c> when its header has no extension.
    /// </summary>
    private readonly int configSequenceHeaderExtension;

    /// <summary>
    /// The content light-level metadata carried by the configuration OBUs, or <see langword="null"/> when absent.
    /// </summary>
    private readonly HeifContentLightLevel? configContentLightLevel;

    /// <summary>
    /// The mastering-display color volume carried by the configuration OBUs, or <see langword="null"/> when absent.
    /// </summary>
    private readonly HeifMasteringDisplayColorVolume? configMasteringDisplayColorVolume;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1CodecConfiguration"/> class from an AV1 codec-configuration
    /// item-property payload.
    /// </summary>
    /// <param name="boxBuffer">The configuration payload beginning with the marker and version fields.</param>
    /// <param name="options">The general options governing metadata validation.</param>
    public Av1CodecConfiguration(Span<byte> boxBuffer, DecoderOptions options)
    {
        if (boxBuffer.Length < 4)
        {
            throw new InvalidImageContentException("The AV1 codec configuration is truncated.");
        }

        Av1BitStreamReader reader = new(boxBuffer);
        uint marker = reader.ReadLiteral(1);
        uint version = reader.ReadLiteral(7);
        if (marker != 1 || version != 1)
        {
            throw new InvalidImageContentException("The AV1 codec configuration has an invalid marker or version.");
        }

        this.SequenceProfile = (byte)reader.ReadLiteral(3);
        this.SequenceLevelIndex = (byte)reader.ReadLiteral(5);
        this.SequenceTier = reader.ReadLiteral(1) == 1;
        this.HighBitDepth = reader.ReadLiteral(1) == 1;
        this.TwelveBit = reader.ReadLiteral(1) == 1;
        this.IsMonochrome = reader.ReadLiteral(1) == 1;
        this.ChromaSubsamplingX = reader.ReadLiteral(1) == 1;
        this.ChromaSubsamplingY = reader.ReadLiteral(1) == 1;
        this.ChromaSamplePosition = (byte)reader.ReadLiteral(2);
        if (this.SequenceProfile > (byte)ObuSequenceProfile.Professional
            || (this.TwelveBit && !this.HighBitDepth)
            || this.ChromaSamplePosition == (byte)ObuChromoSamplePosition.Reserved)
        {
            throw new InvalidImageContentException("The AV1 codec configuration contains invalid image-description fields.");
        }

        if (reader.ReadLiteral(3) != 0)
        {
            throw new InvalidImageContentException("The AV1 codec configuration has nonzero reserved bits.");
        }

        bool hasInitialPresentationDelay = reader.ReadLiteral(1) == 1;
        uint delayOrReserved = reader.ReadLiteral(4);
        if (!hasInitialPresentationDelay && delayOrReserved != 0)
        {
            throw new InvalidImageContentException("The AV1 codec configuration has a nonzero reserved delay field.");
        }

        // The delay syntax is consumed to validate the fixed record, but it describes sample presentation and has
        // no meaning for the independently presented image item supported by this bounded container implementation.
        this.configObus = boxBuffer[4..].ToArray();
        int sequenceHeaderCount = ScanObus(
            this.configObus,
            true,
            true,
            "AV1 codec configuration",
            options,
            out this.configSequenceHeaderOffset,
            out this.configSequenceHeaderLength,
            out this.configSequenceHeaderExtension,
            out this.configContentLightLevel,
            out this.configMasteringDisplayColorVolume);

        if (sequenceHeaderCount > 1)
        {
            throw new InvalidImageContentException("The AV1 codec configuration contains more than one sequence header OBU.");
        }
    }

    /// <summary>
    /// Gets the sequence profile declared for the coded image.
    /// </summary>
    public byte SequenceProfile { get; }

    /// <summary>
    /// Gets the first operating point's sequence-level index.
    /// </summary>
    public byte SequenceLevelIndex { get; }

    /// <summary>
    /// Gets a value indicating whether the first operating point uses the high tier.
    /// </summary>
    public bool SequenceTier { get; }

    /// <summary>
    /// Gets a value indicating whether the coded image uses more than eight bits per sample.
    /// </summary>
    public bool HighBitDepth { get; }

    /// <summary>
    /// Gets a value indicating whether the coded image uses twelve bits per sample.
    /// </summary>
    public bool TwelveBit { get; }

    /// <summary>
    /// Gets the coded image sample precision in bits.
    /// </summary>
    public HeifBitDepth BitDepth => this.TwelveBit ? HeifBitDepth.Bit12 : this.HighBitDepth ? HeifBitDepth.Bit10 : HeifBitDepth.Bit8;

    /// <summary>
    /// Gets a value indicating whether the coded image contains only a luma plane.
    /// </summary>
    public bool IsMonochrome { get; }

    /// <summary>
    /// Gets a value indicating whether the coded image's chroma planes are horizontally subsampled.
    /// </summary>
    public bool ChromaSubsamplingX { get; }

    /// <summary>
    /// Gets a value indicating whether the coded image's chroma planes are vertically subsampled.
    /// </summary>
    public bool ChromaSubsamplingY { get; }

    /// <summary>
    /// Gets the position of vertically subsampled chroma samples relative to luma samples.
    /// </summary>
    public byte ChromaSamplePosition { get; }

    /// <summary>
    /// Gets the optional configuration open bitstream units following the fixed record.
    /// </summary>
    public ReadOnlyMemory<byte> ConfigObus => this.configObus;

    /// <summary>
    /// Validates the AV1 image item OBU layout and metadata against its item properties and configuration record.
    /// </summary>
    /// <param name="itemData">The complete AV1 image item payload.</param>
    /// <param name="itemContentLightLevel">
    /// The content light-level property associated with the image item, or <see langword="null"/> when absent.
    /// </param>
    /// <param name="itemMasteringDisplayColorVolume">
    /// The mastering-display property associated with the image item, or <see langword="null"/> when absent.
    /// </param>
    /// <param name="options">The general options governing metadata validation.</param>
    /// <param name="contentLightLevel">
    /// Receives the content light-level metadata carried by the combined configuration and item OBUs.
    /// </param>
    /// <param name="masteringDisplayColorVolume">
    /// Receives the mastering-display metadata carried by the combined configuration and item OBUs.
    /// </param>
    public void ValidateItemData(
        ReadOnlySpan<byte> itemData,
        HeifContentLightLevel? itemContentLightLevel,
        HeifMasteringDisplayColorVolume? itemMasteringDisplayColorVolume,
        DecoderOptions options,
        out HeifContentLightLevel? contentLightLevel,
        out HeifMasteringDisplayColorVolume? masteringDisplayColorVolume)
        => this.ValidateData(
            itemData,
            true,
            "AV1 image item",
            itemContentLightLevel,
            itemMasteringDisplayColorVolume,
            options,
            out contentLightLevel,
            out masteringDisplayColorVolume);

    /// <summary>
    /// Validates one AV1 track sample against its sync-sample declaration, sample-entry metadata, and configuration record.
    /// </summary>
    /// <param name="sampleData">The complete AV1 sample payload.</param>
    /// <param name="isSyncSample">Indicates that the sample is declared as a random-access point.</param>
    /// <param name="sampleContentLightLevel">
    /// The content light-level property associated with the sample entry, or <see langword="null"/> when absent.
    /// </param>
    /// <param name="sampleMasteringDisplayColorVolume">
    /// The mastering-display property associated with the sample entry, or <see langword="null"/> when absent.
    /// </param>
    /// <param name="options">The general options governing metadata validation.</param>
    /// <param name="contentLightLevel">
    /// Receives the content light-level metadata carried by the combined configuration and sample OBUs.
    /// </param>
    /// <param name="masteringDisplayColorVolume">
    /// Receives the mastering-display metadata carried by the combined configuration and sample OBUs.
    /// </param>
    public void ValidateSampleData(
        ReadOnlySpan<byte> sampleData,
        bool isSyncSample,
        HeifContentLightLevel? sampleContentLightLevel,
        HeifMasteringDisplayColorVolume? sampleMasteringDisplayColorVolume,
        DecoderOptions options,
        out HeifContentLightLevel? contentLightLevel,
        out HeifMasteringDisplayColorVolume? masteringDisplayColorVolume)
        => this.ValidateData(
            sampleData,
            isSyncSample,
            "AV1 track sample",
            sampleContentLightLevel,
            sampleMasteringDisplayColorVolume,
            options,
            out contentLightLevel,
            out masteringDisplayColorVolume);

    /// <summary>
    /// Validates one bounded AV1 payload while applying the item or track sequence-header requirement.
    /// </summary>
    /// <param name="data">The complete bounded AV1 payload.</param>
    /// <param name="sequenceHeaderRequired">Indicates that exactly one sequence header is required.</param>
    /// <param name="sourceName">The source description used by invalid-content errors.</param>
    /// <param name="containerContentLightLevel">The content light-level property associated with the payload.</param>
    /// <param name="containerMasteringDisplayColorVolume">The mastering-display property associated with the payload.</param>
    /// <param name="options">The general options governing metadata validation.</param>
    /// <param name="contentLightLevel">Receives validated OBU content light-level metadata.</param>
    /// <param name="masteringDisplayColorVolume">Receives validated OBU mastering-display metadata.</param>
    private void ValidateData(
        ReadOnlySpan<byte> data,
        bool sequenceHeaderRequired,
        string sourceName,
        HeifContentLightLevel? containerContentLightLevel,
        HeifMasteringDisplayColorVolume? containerMasteringDisplayColorVolume,
        DecoderOptions options,
        out HeifContentLightLevel? contentLightLevel,
        out HeifMasteringDisplayColorVolume? masteringDisplayColorVolume)
    {
        int sequenceHeaderCount = ScanObus(
            data,
            false,
            false,
            sourceName,
            options,
            out int dataSequenceHeaderOffset,
            out int dataSequenceHeaderLength,
            out int dataSequenceHeaderExtension,
            out HeifContentLightLevel? dataObuContentLightLevel,
            out HeifMasteringDisplayColorVolume? dataObuMasteringDisplayColorVolume);

        if (sequenceHeaderCount > 1 || (sequenceHeaderRequired && sequenceHeaderCount != 1))
        {
            string requirement = sequenceHeaderRequired ? "exactly one" : "at most one";
            throw new InvalidImageContentException($"The {sourceName} contains {sequenceHeaderCount} sequence header OBUs instead of {requirement}.");
        }

        if (this.configSequenceHeaderOffset >= 0 && dataSequenceHeaderOffset >= 0)
        {
            ReadOnlySpan<byte> configSequenceHeader = this.configObus.AsSpan(
                this.configSequenceHeaderOffset,
                this.configSequenceHeaderLength);

            ReadOnlySpan<byte> dataSequenceHeader = data.Slice(
                dataSequenceHeaderOffset,
                dataSequenceHeaderLength);

            // Compare the extension and payload rather than the encoded OBU size. Configuration OBUs must carry a
            // size field while a payload's final OBU may omit one, and different legal LEB128 widths do not alter
            // the Sequence Header OBU being repeated.
            if (this.configSequenceHeaderExtension != dataSequenceHeaderExtension
                || !configSequenceHeader.SequenceEqual(dataSequenceHeader))
            {
                throw new InvalidImageContentException(
                    $"The AV1 codec configuration sequence header does not match the {sourceName} sequence header.");
            }
        }

        contentLightLevel = null;
        masteringDisplayColorVolume = null;
        if (options.SkipMetadata)
        {
            return;
        }

        try
        {
            ValidateContentLightLevel(this.configContentLightLevel, containerContentLightLevel, "AV1 codec configuration");
            ValidateContentLightLevel(dataObuContentLightLevel, containerContentLightLevel, sourceName);
            ValidateMasteringDisplayColorVolume(
                this.configMasteringDisplayColorVolume,
                containerMasteringDisplayColorVolume,
                "AV1 codec configuration");

            ValidateMasteringDisplayColorVolume(
                dataObuMasteringDisplayColorVolume,
                containerMasteringDisplayColorVolume,
                sourceName);

            if (this.configContentLightLevel is not null
                && dataObuContentLightLevel is not null
                && !ContentLightLevelsMatch(this.configContentLightLevel.Value, dataObuContentLightLevel.Value))
            {
                throw new InvalidImageContentException(
                    $"The AV1 codec configuration and {sourceName} contain conflicting content light-level metadata.");
            }

            if (this.configMasteringDisplayColorVolume is not null
                && dataObuMasteringDisplayColorVolume is not null
                && this.configMasteringDisplayColorVolume.Value != dataObuMasteringDisplayColorVolume.Value)
            {
                throw new InvalidImageContentException(
                    $"The AV1 codec configuration and {sourceName} contain conflicting mastering-display metadata.");
            }

            // Configuration OBUs precede the payload OBUs, so a payload OBU supplies the effective value when both
            // sequences repeat the same metadata type.
            contentLightLevel = dataObuContentLightLevel ?? this.configContentLightLevel;
            masteringDisplayColorVolume = dataObuMasteringDisplayColorVolume ?? this.configMasteringDisplayColorVolume;
        }
        catch (Exception ex) when (ImageDecoderCore.ShouldIgnoreAncillarySegmentError(options, ex))
        {
            // Conflicting optional OBU metadata is discarded without weakening OBU framing or sequence-header checks.
        }
    }

    /// <summary>
    /// Determines whether another item configuration describes the same coded-image sample layout.
    /// </summary>
    /// <param name="other">The configuration to compare.</param>
    /// <returns><see langword="true"/> when every fixed image-description field is equal.</returns>
    public bool HasMatchingImageConfiguration(Av1CodecConfiguration other)
        => this.SequenceProfile == other.SequenceProfile
            && this.SequenceLevelIndex == other.SequenceLevelIndex
            && this.SequenceTier == other.SequenceTier
            && this.HighBitDepth == other.HighBitDepth
            && this.TwelveBit == other.TwelveBit
            && this.IsMonochrome == other.IsMonochrome
            && this.ChromaSubsamplingX == other.ChromaSubsamplingX
            && this.ChromaSubsamplingY == other.ChromaSubsamplingY
            && this.ChromaSamplePosition == other.ChromaSamplePosition;

    /// <summary>
    /// Validates the configuration fields against the sequence header that describes the coded image item.
    /// </summary>
    /// <param name="sequenceHeader">The decoded AV1 sequence header.</param>
    public void Validate(ObuSequenceHeader sequenceHeader)
    {
        ObuOperatingPoint operatingPoint = sequenceHeader.OperatingPoint[0];
        ObuColorConfig colorConfig = sequenceHeader.ColorConfig;
        bool highBitDepth = colorConfig.BitDepth is Av1BitDepth.TenBit or Av1BitDepth.TwelveBit;
        bool twelveBit = colorConfig.BitDepth == Av1BitDepth.TwelveBit;

        if (this.SequenceProfile != (byte)sequenceHeader.SequenceProfile
            || this.SequenceLevelIndex != operatingPoint.SequenceLevelIndex
            || this.SequenceTier != (operatingPoint.SequenceTier != 0)
            || this.HighBitDepth != highBitDepth
            || this.TwelveBit != twelveBit
            || this.IsMonochrome != colorConfig.IsMonochrome
            || this.ChromaSubsamplingX != colorConfig.SubSamplingX
            || this.ChromaSubsamplingY != colorConfig.SubSamplingY
            || this.ChromaSamplePosition != (byte)colorConfig.ChromaSamplePosition)
        {
            throw new InvalidImageContentException("The AV1 item configuration does not match its sequence header.");
        }
    }

    /// <summary>
    /// Scans a low-overhead AV1 OBU sequence and locates its still-image description metadata.
    /// </summary>
    /// <param name="data">The complete bounded OBU sequence.</param>
    /// <param name="requireSizeFields">Indicates that every OBU must carry its registered payload-size field.</param>
    /// <param name="sequenceHeaderMustBeFirst">
    /// Indicates that a sequence-header OBU, when present, must be the first OBU in the sequence.
    /// </param>
    /// <param name="sourceName">The source description used by invalid-content errors.</param>
    /// <param name="options">The general options governing metadata validation.</param>
    /// <param name="sequenceHeaderOffset">Receives the first sequence-header payload offset, or <c>-1</c>.</param>
    /// <param name="sequenceHeaderLength">Receives the first sequence-header payload length.</param>
    /// <param name="sequenceHeaderExtension">Receives the first sequence-header extension byte, or <c>-1</c>.</param>
    /// <param name="contentLightLevel">
    /// Receives the content light-level metadata carried by the sequence, or <see langword="null"/> when absent.
    /// </param>
    /// <param name="masteringDisplayColorVolume">
    /// Receives the mastering-display metadata carried by the sequence, or <see langword="null"/> when absent.
    /// </param>
    /// <returns>The number of sequence-header OBUs in the sequence.</returns>
    private static int ScanObus(
        ReadOnlySpan<byte> data,
        bool requireSizeFields,
        bool sequenceHeaderMustBeFirst,
        string sourceName,
        DecoderOptions options,
        out int sequenceHeaderOffset,
        out int sequenceHeaderLength,
        out int sequenceHeaderExtension,
        out HeifContentLightLevel? contentLightLevel,
        out HeifMasteringDisplayColorVolume? masteringDisplayColorVolume)
    {
        sequenceHeaderOffset = -1;
        sequenceHeaderLength = 0;
        sequenceHeaderExtension = -1;
        contentLightLevel = null;
        masteringDisplayColorVolume = null;
        int sequenceHeaderCount = 0;
        int obuIndex = 0;
        int offset = 0;
        while (offset < data.Length)
        {
            byte header = data[offset++];
            if ((header & 0x81) != 0)
            {
                throw new InvalidImageContentException($"The {sourceName} contains an OBU with a set forbidden or reserved header bit.");
            }

            ObuType type = (ObuType)((header >> 3) & 0x0F);
            bool hasExtension = (header & 0x04) != 0;
            bool hasSizeField = (header & 0x02) != 0;
            int extension = -1;
            if (hasExtension)
            {
                if (offset >= data.Length)
                {
                    throw new InvalidImageContentException($"The {sourceName} contains a truncated OBU extension header.");
                }

                extension = data[offset++];
                if ((extension & 0x07) != 0)
                {
                    throw new InvalidImageContentException($"The {sourceName} contains an OBU extension with nonzero reserved bits.");
                }
            }

            if (requireSizeFields && !hasSizeField)
            {
                throw new InvalidImageContentException($"The {sourceName} contains an OBU without its required payload-size field.");
            }

            int payloadLength;
            if (hasSizeField)
            {
                payloadLength = ReadObuPayloadLength(data, ref offset, sourceName);
            }
            else
            {
                // Low-overhead image item syntax permits only the final OBU to omit its size, in which case the
                // remaining item bytes are that OBU's payload and cannot contain another independently parsed OBU.
                payloadLength = data.Length - offset;
            }

            if (payloadLength > data.Length - offset)
            {
                throw new InvalidImageContentException($"The {sourceName} contains an OBU payload that exceeds its data boundary.");
            }

            if (type == ObuType.SequenceHeader)
            {
                if (sequenceHeaderMustBeFirst && obuIndex != 0)
                {
                    throw new InvalidImageContentException($"The {sourceName} contains a sequence header OBU after another OBU.");
                }

                sequenceHeaderCount++;
                if (sequenceHeaderOffset < 0)
                {
                    sequenceHeaderOffset = offset;
                    sequenceHeaderLength = payloadLength;
                    sequenceHeaderExtension = extension;
                }
            }
            else if (type == ObuType.Metadata && !options.SkipMetadata)
            {
                try
                {
                    ReadHdrMetadata(
                        data.Slice(offset, payloadLength),
                        sourceName,
                        out HeifContentLightLevel? obuContentLightLevel,
                        out HeifMasteringDisplayColorVolume? obuMasteringDisplayColorVolume);

                    if (obuContentLightLevel is not null)
                    {
                        if (contentLightLevel is not null
                            && !ContentLightLevelsMatch(contentLightLevel.Value, obuContentLightLevel.Value))
                        {
                            throw new InvalidImageContentException($"The {sourceName} contains conflicting content light-level metadata OBUs.");
                        }

                        contentLightLevel = obuContentLightLevel;
                    }

                    if (obuMasteringDisplayColorVolume is not null)
                    {
                        if (masteringDisplayColorVolume is not null
                            && masteringDisplayColorVolume.Value != obuMasteringDisplayColorVolume.Value)
                        {
                            throw new InvalidImageContentException($"The {sourceName} contains conflicting mastering-display metadata OBUs.");
                        }

                        masteringDisplayColorVolume = obuMasteringDisplayColorVolume;
                    }
                }
                catch (Exception ex) when (ImageDecoderCore.ShouldIgnoreAncillarySegmentError(options, ex))
                {
                    // The OBU payload remains bounded by the image-data scan; only its invalid optional metadata is discarded.
                }
            }

            offset += payloadLength;
            obuIndex++;
        }

        return sequenceHeaderCount;
    }

    /// <summary>
    /// Reads a bounded AV1 little-endian base-128 OBU payload length.
    /// </summary>
    /// <param name="data">The complete bounded OBU sequence.</param>
    /// <param name="offset">The current byte offset, advanced past the encoded length.</param>
    /// <param name="sourceName">The source description used by invalid-content errors.</param>
    /// <returns>The payload length representable by the current item buffer.</returns>
    private static int ReadObuPayloadLength(ReadOnlySpan<byte> data, ref int offset, string sourceName)
    {
        ulong value = ReadLeb128(data, ref offset, sourceName, "OBU payload length");
        if (value > int.MaxValue)
        {
            throw new InvalidImageContentException($"The {sourceName} contains an OBU payload too large to buffer.");
        }

        return (int)value;
    }

    /// <summary>
    /// Reads still-image high-dynamic-range data from an AV1 metadata OBU payload.
    /// </summary>
    /// <param name="payload">The bounded metadata OBU payload.</param>
    /// <param name="sourceName">The source description used by invalid-content errors.</param>
    /// <param name="contentLightLevel">Receives decoded content light-level metadata when present.</param>
    /// <param name="masteringDisplayColorVolume">Receives decoded mastering-display metadata when present.</param>
    private static void ReadHdrMetadata(
        ReadOnlySpan<byte> payload,
        string sourceName,
        out HeifContentLightLevel? contentLightLevel,
        out HeifMasteringDisplayColorVolume? masteringDisplayColorVolume)
    {
        contentLightLevel = null;
        masteringDisplayColorVolume = null;
        int offset = 0;
        ulong metadataType = ReadLeb128(payload, ref offset, sourceName, "metadata type");
        if (metadataType != (ulong)ObuMetadataType.HdrCll
            && metadataType != (ulong)ObuMetadataType.HdrMdcv)
        {
            return;
        }

        int metadataLength = metadataType == (ulong)ObuMetadataType.HdrCll ? 4 : 24;
        if (payload.Length - offset <= metadataLength)
        {
            throw new InvalidImageContentException($"The {sourceName} contains truncated HDR metadata or no trailing bits.");
        }

        ReadOnlySpan<byte> metadataData = payload.Slice(offset, metadataLength);
        ValidateByteAlignedMetadataTrailingBits(payload[(offset + metadataLength)..], sourceName);

        if (metadataType == (ulong)ObuMetadataType.HdrCll)
        {
            contentLightLevel = new HeifContentLightLevel(
                BinaryPrimitives.ReadUInt16BigEndian(metadataData),
                BinaryPrimitives.ReadUInt16BigEndian(metadataData[2..]));

            return;
        }

        const float chromaticityScale = 1F / 65536F;
        const double maximumLuminanceScale = 1D / 256D;
        const double minimumLuminanceScale = 1D / 16384D;

        // AV1 stores the primaries in R, G, B order and uses codec-specific fixed-point units that differ from the
        // ISOBMFF mdcv property. Decode both representations to the same observable ImageSharp color coordinates.
        CieXyChromaticityCoordinates redPrimary = new(
            BinaryPrimitives.ReadUInt16BigEndian(metadataData) * chromaticityScale,
            BinaryPrimitives.ReadUInt16BigEndian(metadataData[2..]) * chromaticityScale);

        CieXyChromaticityCoordinates greenPrimary = new(
            BinaryPrimitives.ReadUInt16BigEndian(metadataData[4..]) * chromaticityScale,
            BinaryPrimitives.ReadUInt16BigEndian(metadataData[6..]) * chromaticityScale);

        CieXyChromaticityCoordinates bluePrimary = new(
            BinaryPrimitives.ReadUInt16BigEndian(metadataData[8..]) * chromaticityScale,
            BinaryPrimitives.ReadUInt16BigEndian(metadataData[10..]) * chromaticityScale);

        masteringDisplayColorVolume = new HeifMasteringDisplayColorVolume(
            new RgbPrimariesChromaticityCoordinates(redPrimary, greenPrimary, bluePrimary),
            new CieXyChromaticityCoordinates(
                BinaryPrimitives.ReadUInt16BigEndian(metadataData[12..]) * chromaticityScale,
                BinaryPrimitives.ReadUInt16BigEndian(metadataData[14..]) * chromaticityScale),
            BinaryPrimitives.ReadUInt32BigEndian(metadataData[16..]) * maximumLuminanceScale,
            BinaryPrimitives.ReadUInt32BigEndian(metadataData[20..]) * minimumLuminanceScale);
    }

    /// <summary>
    /// Validates the trailing bits of byte-aligned fixed-length AV1 metadata.
    /// </summary>
    /// <param name="trailingData">The metadata payload bytes following its fixed fields.</param>
    /// <param name="sourceName">The source description used by invalid-content errors.</param>
    private static void ValidateByteAlignedMetadataTrailingBits(ReadOnlySpan<byte> trailingData, string sourceName)
    {
        byte lastNonzeroByte = 0;
        for (int i = trailingData.Length - 1; i >= 0; i--)
        {
            if (trailingData[i] != 0)
            {
                lastNonzeroByte = trailingData[i];
                break;
            }
        }

        // Both fixed HDR structures end on a byte boundary. libaom accepts zero padding after the required 0x80 byte,
        // so locate the last nonzero byte rather than assuming the OBU payload ends immediately after trailing_bits().
        if (lastNonzeroByte != 0x80)
        {
            throw new InvalidImageContentException($"The {sourceName} HDR metadata has invalid trailing bits.");
        }
    }

    /// <summary>
    /// Reads a bounded AV1 little-endian base-128 value.
    /// </summary>
    /// <param name="data">The complete bounded byte sequence.</param>
    /// <param name="offset">The current byte offset, advanced past the encoded value.</param>
    /// <param name="sourceName">The source description used by invalid-content errors.</param>
    /// <param name="valueName">The value description used by invalid-content errors.</param>
    /// <returns>The decoded unsigned value.</returns>
    private static ulong ReadLeb128(
        ReadOnlySpan<byte> data,
        ref int offset,
        string sourceName,
        string valueName)
    {
        ulong value = 0;
        for (int byteIndex = 0; byteIndex < 8; byteIndex++)
        {
            if (offset >= data.Length)
            {
                throw new InvalidImageContentException($"The {sourceName} contains a truncated {valueName}.");
            }

            byte current = data[offset++];
            value |= (ulong)(current & 0x7F) << (byteIndex * 7);
            if ((current & 0x80) == 0)
            {
                return value;
            }
        }

        throw new InvalidImageContentException($"The {sourceName} contains an unterminated {valueName}.");
    }

    /// <summary>
    /// Validates content light-level metadata against the corresponding image-item property when both are present.
    /// </summary>
    /// <param name="obuContentLightLevel">The value carried by an AV1 metadata OBU.</param>
    /// <param name="itemContentLightLevel">The value carried by the associated image-item property.</param>
    /// <param name="sourceName">The OBU source description used by invalid-content errors.</param>
    private static void ValidateContentLightLevel(
        HeifContentLightLevel? obuContentLightLevel,
        HeifContentLightLevel? itemContentLightLevel,
        string sourceName)
    {
        if (obuContentLightLevel is not null
            && itemContentLightLevel is not null
            && !ContentLightLevelsMatch(obuContentLightLevel.Value, itemContentLightLevel.Value))
        {
            throw new InvalidImageContentException($"The {sourceName} content light-level metadata does not match the image-item property.");
        }
    }

    /// <summary>
    /// Determines whether two content light-level descriptions carry the same observable values.
    /// </summary>
    /// <param name="left">The first content light-level description.</param>
    /// <param name="right">The second content light-level description.</param>
    /// <returns><see langword="true"/> when both light-level fields are equal.</returns>
    private static bool ContentLightLevelsMatch(HeifContentLightLevel left, HeifContentLightLevel right)
    {
        return left.MaximumContentLightLevel == right.MaximumContentLightLevel
            && left.MaximumPictureAverageLightLevel == right.MaximumPictureAverageLightLevel;
    }

    /// <summary>
    /// Validates mastering-display metadata against the corresponding image-item property when both are present.
    /// </summary>
    /// <param name="obuColorVolume">The value carried by an AV1 metadata OBU.</param>
    /// <param name="itemColorVolume">The value carried by the associated image-item property.</param>
    /// <param name="sourceName">The OBU source description used by invalid-content errors.</param>
    private static void ValidateMasteringDisplayColorVolume(
        HeifMasteringDisplayColorVolume? obuColorVolume,
        HeifMasteringDisplayColorVolume? itemColorVolume,
        string sourceName)
    {
        if (obuColorVolume is not null
            && itemColorVolume is not null
            && !MasteringDisplayColorVolumesMatch(obuColorVolume.Value, itemColorVolume.Value))
        {
            throw new InvalidImageContentException($"The {sourceName} mastering-display metadata does not match the image-item property.");
        }
    }

    /// <summary>
    /// Determines whether AV1 and ISOBMFF mastering-display values agree within their fixed-point precision.
    /// </summary>
    /// <param name="obuColorVolume">The mastering-display values decoded from the AV1 representation.</param>
    /// <param name="itemColorVolume">The mastering-display values decoded from the ISOBMFF representation.</param>
    /// <returns><see langword="true"/> when all decoded values agree within their combined quantization error.</returns>
    private static bool MasteringDisplayColorVolumesMatch(
        HeifMasteringDisplayColorVolume obuColorVolume,
        HeifMasteringDisplayColorVolume itemColorVolume)
    {
        const float chromaticityTolerance = ((1F / 65536F) + (1F / 50000F)) / 2F;
        const double maximumLuminanceTolerance = ((1D / 256D) + (1D / 10000D)) / 2D;
        const double minimumLuminanceTolerance = ((1D / 16384D) + (1D / 10000D)) / 2D;

        return ChromaticitiesMatch(obuColorVolume.Primaries.R, itemColorVolume.Primaries.R, chromaticityTolerance)
            && ChromaticitiesMatch(obuColorVolume.Primaries.G, itemColorVolume.Primaries.G, chromaticityTolerance)
            && ChromaticitiesMatch(obuColorVolume.Primaries.B, itemColorVolume.Primaries.B, chromaticityTolerance)
            && ChromaticitiesMatch(obuColorVolume.WhitePoint, itemColorVolume.WhitePoint, chromaticityTolerance)
            && ValuesMatch(obuColorVolume.MaximumLuminance, itemColorVolume.MaximumLuminance, maximumLuminanceTolerance)
            && ValuesMatch(obuColorVolume.MinimumLuminance, itemColorVolume.MinimumLuminance, minimumLuminanceTolerance);
    }

    /// <summary>
    /// Determines whether two chromaticity-coordinate pairs agree within the supplied fixed-point tolerance.
    /// </summary>
    /// <param name="left">The first chromaticity-coordinate pair.</param>
    /// <param name="right">The second chromaticity-coordinate pair.</param>
    /// <param name="tolerance">The maximum permitted difference on either coordinate axis.</param>
    /// <returns><see langword="true"/> when both coordinate differences are within the tolerance.</returns>
    private static bool ChromaticitiesMatch(
        CieXyChromaticityCoordinates left,
        CieXyChromaticityCoordinates right,
        float tolerance)
    {
        return ValuesMatch(left.X, right.X, tolerance)
            && ValuesMatch(left.Y, right.Y, tolerance);
    }

    /// <summary>
    /// Determines whether two decoded fixed-point values agree within the supplied tolerance.
    /// </summary>
    /// <param name="left">The first decoded value.</param>
    /// <param name="right">The second decoded value.</param>
    /// <param name="tolerance">The maximum permitted absolute difference.</param>
    /// <returns><see langword="true"/> when the absolute difference does not exceed the tolerance.</returns>
    private static bool ValuesMatch(double left, double right, double tolerance)
        => Math.Abs(left - right) <= tolerance;
}
