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
    /// The number of bytes in the fixed AV1 codec-configuration record.
    /// </summary>
    public const int FixedHeaderSize = 4;

    /// <summary>
    /// The optional sequence-header payload retained from the configuration open bitstream units.
    /// </summary>
    private readonly byte[] configSequenceHeader;

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
    /// Initializes a new instance of the <see cref="Av1CodecConfiguration"/> class from an AV1 codec-configuration item-property payload.
    /// </summary>
    /// <param name="boxBuffer">The configuration payload beginning with the marker and version fields.</param>
    /// <param name="options">The general options governing metadata validation.</param>
    public Av1CodecConfiguration(Span<byte> boxBuffer, DecoderOptions options)
    {
        if (boxBuffer.Length < FixedHeaderSize)
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

        // The constructor reads the delay field only to validate the fixed record. The field describes sample presentation. It has no meaning for an image
        // item, which the decoder presents on its own.
        ReadOnlySpan<byte> configObus = boxBuffer[FixedHeaderSize..];

        // The configuration OBUs belong to the property box, not to image data. A truncated configuration OBU throws in each mode. The strict mode and the
        // absent image make each scan error throw, so the scan never stops early.
        _ = ScanObus(
            configObus,
            true,
            SegmentIntegrityHandling.Strict,
            false,
            "AV1 codec configuration",
            options,
            out int sequenceHeaderCount,
            out int configSequenceHeaderOffset,
            out int configSequenceHeaderLength,
            out this.configSequenceHeaderExtension,
            out this.configContentLightLevel,
            out this.configMasteringDisplayColorVolume);

        if (sequenceHeaderCount > 1)
        {
            throw new InvalidImageContentException("The AV1 codec configuration contains more than one sequence header OBU.");
        }

        if (configSequenceHeaderOffset < 0)
        {
            this.configSequenceHeader = [];
        }
        else
        {
            // The span from the property reader is pooled and reused. The constructor copies only the sequence-header bytes that the equivalence test against
            // the item or sample needs. It does not copy the other optional configuration OBUs.
            this.configSequenceHeader = new byte[configSequenceHeaderLength];
            configObus.Slice(configSequenceHeaderOffset, configSequenceHeaderLength).CopyTo(this.configSequenceHeader);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1CodecConfiguration"/> class from an encoded sequence header.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header describing the encoded image item.</param>
    public Av1CodecConfiguration(ObuSequenceHeader sequenceHeader)
    {
        ObuOperatingPoint operatingPoint = sequenceHeader.OperatingPoint[0];
        ObuColorConfig colorConfig = sequenceHeader.ColorConfig;

        this.SequenceProfile = (byte)sequenceHeader.SequenceProfile;
        this.SequenceLevelIndex = (byte)operatingPoint.SequenceLevelIndex;
        this.SequenceTier = operatingPoint.SequenceTier != 0;
        this.HighBitDepth = colorConfig.BitDepth is Av1BitDepth.TenBit or Av1BitDepth.TwelveBit;
        this.TwelveBit = colorConfig.BitDepth == Av1BitDepth.TwelveBit;
        this.IsMonochrome = colorConfig.IsMonochrome;
        this.ChromaSubsamplingX = colorConfig.SubSamplingX;
        this.ChromaSubsamplingY = colorConfig.SubSamplingY;
        this.ChromaSamplePosition = (byte)colorConfig.ChromaSamplePosition;
        this.configSequenceHeader = [];
        this.configSequenceHeaderExtension = -1;
        this.configContentLightLevel = null;
        this.configMasteringDisplayColorVolume = null;
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
    /// Gets the chroma sampling that the configuration describes.
    /// </summary>
    /// <returns>The chroma sampling.</returns>
    public HeifChromaSubsampling GetChromaSubsampling()
        => this.IsMonochrome
            ? HeifChromaSubsampling.Monochrome
            : this.ChromaSubsamplingX
                ? this.ChromaSubsamplingY ? HeifChromaSubsampling.Yuv420 : HeifChromaSubsampling.Yuv422
                : HeifChromaSubsampling.Yuv444;

    /// <summary>
    /// Writes the fixed AV1 codec-configuration record without optional configuration OBUs.
    /// </summary>
    /// <param name="destination">The destination receiving the four-byte record.</param>
    public void WriteFixedHeader(Span<byte> destination)
    {
        // The image item payload already begins with its required sequence header. An empty configOBUs field prevents a second copy of the same OBU in the
        // configuration property.
        destination[0] = 0x81;
        destination[1] = (byte)((this.SequenceProfile << 5) | this.SequenceLevelIndex);
        destination[2] = (byte)(
            (this.SequenceTier ? 1 << 7 : 0)
            | (this.HighBitDepth ? 1 << 6 : 0)
            | (this.TwelveBit ? 1 << 5 : 0)
            | (this.IsMonochrome ? 1 << 4 : 0)
            | (this.ChromaSubsamplingX ? 1 << 3 : 0)
            | (this.ChromaSubsamplingY ? 1 << 2 : 0)
            | this.ChromaSamplePosition);

        destination[3] = 0;
    }

    /// <summary>
    /// Validates the OBU layout and metadata of one AV1 image item or track sample against its container properties and configuration record.
    /// </summary>
    /// <param name="data">The complete bounded AV1 payload.</param>
    /// <param name="sequenceHeaderRequired">
    /// Indicates that exactly one sequence header is required. The value is <see langword="true"/> for an image item and for a sync sample.
    /// </param>
    /// <param name="sourceName">The source description used by invalid-content errors.</param>
    /// <param name="containerContentLightLevel">
    /// The content light-level property associated with the item or sample entry, or <see langword="null"/> when absent.
    /// </param>
    /// <param name="containerMasteringDisplayColorVolume">
    /// The mastering-display property associated with the item or sample entry, or <see langword="null"/> when absent.
    /// </param>
    /// <param name="options">The general options governing metadata validation.</param>
    /// <param name="imageExists">
    /// A value indicating whether the caller keeps an image without this payload. If <see langword="true"/>, the mode controls whether damaged payload data
    /// throws. If <see langword="false"/>, damaged payload data throws in each mode.
    /// </param>
    /// <param name="contentLightLevel">Receives the content light-level metadata carried by the combined configuration and payload OBUs.</param>
    /// <param name="masteringDisplayColorVolume">Receives the mastering-display metadata carried by the combined configuration and payload OBUs.</param>
    /// <returns>
    /// <see langword="true"/> if the payload is valid. <see langword="false"/> if the method reported damaged payload data and the mode ignores it. The caller
    /// then decodes no frame from the payload.
    /// </returns>
    public bool ValidateData(
        ReadOnlySpan<byte> data,
        bool sequenceHeaderRequired,
        string sourceName,
        HeifContentLightLevel? containerContentLightLevel,
        HeifMasteringDisplayColorVolume? containerMasteringDisplayColorVolume,
        DecoderOptions options,
        bool imageExists,
        out HeifContentLightLevel? contentLightLevel,
        out HeifMasteringDisplayColorVolume? masteringDisplayColorVolume)
    {
        contentLightLevel = null;
        masteringDisplayColorVolume = null;
        SegmentIntegrityHandling handling = options.SegmentIntegrityHandling;
        if (!ScanObus(
            data,
            false,
            handling,
            imageExists,
            sourceName,
            options,
            out int sequenceHeaderCount,
            out int dataSequenceHeaderOffset,
            out int dataSequenceHeaderLength,
            out int dataSequenceHeaderExtension,
            out HeifContentLightLevel? dataObuContentLightLevel,
            out HeifMasteringDisplayColorVolume? dataObuMasteringDisplayColorVolume))
        {
            return false;
        }

        // The checks that follow find damage before the AV1 decoder reads the payload. As a result, the payload holds no decoded image data yet. The mode can
        // ignore the damage only when the caller keeps an image without the payload.
        if (sequenceHeaderCount > 1 || (sequenceHeaderRequired && sequenceHeaderCount != 1))
        {
            string requirement = sequenceHeaderRequired ? "exactly one" : "at most one";
            handling.ThrowOrIgnoreImageDataError(
                imageExists,
                $"The {sourceName} contains {sequenceHeaderCount} sequence header OBUs instead of {requirement}.");

            return false;
        }

        if (this.configSequenceHeader.Length != 0 && dataSequenceHeaderOffset >= 0)
        {
            ReadOnlySpan<byte> dataSequenceHeader = data.Slice(dataSequenceHeaderOffset, dataSequenceHeaderLength);

            // The code compares the extension and the payload, not the encoded OBU size. Configuration OBUs must carry a size field. The final OBU of a payload
            // can omit it. Different legal LEB128 widths do not change the repeated Sequence Header OBU.
            if (this.configSequenceHeaderExtension != dataSequenceHeaderExtension
                || !this.configSequenceHeader.AsSpan().SequenceEqual(dataSequenceHeader))
            {
                handling.ThrowOrIgnoreImageDataError(
                    imageExists,
                    $"The AV1 codec configuration sequence header does not match the {sourceName} sequence header.");

                return false;
            }
        }

        if (options.SkipMetadata)
        {
            return true;
        }

        // Each check that follows compares optional metadata. A conflict is damaged ancillary data. The method reports the conflict and discards the OBU
        // metadata. The conflict has no effect on the OBU framing checks and the sequence-header checks that come before.
        string? metadataError = null;
        if (!ContentLightLevelMatchesProperty(this.configContentLightLevel, containerContentLightLevel))
        {
            metadataError = "The AV1 codec configuration content light-level metadata does not match the image-item property.";
        }
        else if (!ContentLightLevelMatchesProperty(dataObuContentLightLevel, containerContentLightLevel))
        {
            metadataError = $"The {sourceName} content light-level metadata does not match the image-item property.";
        }
        else if (!MasteringDisplayColorVolumeMatchesProperty(this.configMasteringDisplayColorVolume, containerMasteringDisplayColorVolume))
        {
            metadataError = "The AV1 codec configuration mastering-display metadata does not match the image-item property.";
        }
        else if (!MasteringDisplayColorVolumeMatchesProperty(dataObuMasteringDisplayColorVolume, containerMasteringDisplayColorVolume))
        {
            metadataError = $"The {sourceName} mastering-display metadata does not match the image-item property.";
        }
        else if (!ContentLightLevelMatchesProperty(this.configContentLightLevel, dataObuContentLightLevel))
        {
            metadataError = $"The AV1 codec configuration and {sourceName} contain conflicting content light-level metadata.";
        }
        else if (this.configMasteringDisplayColorVolume is not null
            && dataObuMasteringDisplayColorVolume is not null
            && this.configMasteringDisplayColorVolume.Value != dataObuMasteringDisplayColorVolume.Value)
        {
            metadataError = $"The AV1 codec configuration and {sourceName} contain conflicting mastering-display metadata.";
        }

        if (metadataError is not null)
        {
            handling.ThrowOrIgnoreAncillaryError(metadataError);
            return true;
        }

        // The configuration OBUs come before the payload OBUs. When the two sequences have the same metadata type, the payload OBU gives the effective value.
        contentLightLevel = dataObuContentLightLevel ?? this.configContentLightLevel;
        masteringDisplayColorVolume = dataObuMasteringDisplayColorVolume ?? this.configMasteringDisplayColorVolume;
        return true;
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
    /// <remarks>
    /// The method returns the damage and does not report it. The AV1 OBU reader reports the damage one time, through the damage route of its payload.
    /// </remarks>
    /// <param name="sequenceHeader">The decoded AV1 sequence header.</param>
    /// <returns>The message of the mismatch, or <see langword="null"/> when the fields match.</returns>
    public string? Validate(ObuSequenceHeader sequenceHeader)
    {
        ObuOperatingPoint operatingPoint = sequenceHeader.OperatingPoint[0];
        ObuColorConfig colorConfig = sequenceHeader.ColorConfig;
        bool highBitDepth = colorConfig.BitDepth is Av1BitDepth.TenBit or Av1BitDepth.TwelveBit;
        bool twelveBit = colorConfig.BitDepth == Av1BitDepth.TwelveBit;

        // The sequence header signals the chroma sample position only for 4:2:0. Other layouts have no such field, so the method does not compare their
        // container value with the parser default.
        bool hasChromaSamplePosition = !colorConfig.IsMonochrome && colorConfig.SubSamplingX && colorConfig.SubSamplingY;

        if (this.SequenceProfile != (byte)sequenceHeader.SequenceProfile
            || this.SequenceLevelIndex != operatingPoint.SequenceLevelIndex
            || this.SequenceTier != (operatingPoint.SequenceTier != 0)
            || this.HighBitDepth != highBitDepth
            || this.TwelveBit != twelveBit
            || this.IsMonochrome != colorConfig.IsMonochrome
            || this.ChromaSubsamplingX != colorConfig.SubSamplingX
            || this.ChromaSubsamplingY != colorConfig.SubSamplingY
            || (hasChromaSamplePosition && this.ChromaSamplePosition != (byte)colorConfig.ChromaSamplePosition))
        {
            return "The AV1 item configuration does not match its sequence header.";
        }

        return null;
    }

    /// <summary>
    /// Scans a low-overhead AV1 OBU sequence and locates its still-image description metadata.
    /// </summary>
    /// <param name="data">The complete bounded OBU sequence.</param>
    /// <param name="isConfiguration">
    /// A value indicating whether the data are the configuration OBUs of the property box. If <see langword="true"/>, each OBU must have its payload-size
    /// field, and the first OBU must be a sequence-header OBU. A truncated OBU then throws. If <see langword="false"/>, the data are an item payload or a
    /// sample payload. The AV1 decoder reports a truncated payload OBU one time, after it decodes the bytes that the payload holds. As a result, the scan keeps
    /// those bytes and does not report the truncated OBU.
    /// </param>
    /// <param name="payloadIntegrity">The segment integrity mode for an OBU header that is not valid.</param>
    /// <param name="imageExists">
    /// A value indicating whether the caller keeps an image without this data. If <see langword="true"/>, the mode controls whether an invalid OBU header
    /// throws. If <see langword="false"/>, an invalid OBU header throws in each mode.
    /// </param>
    /// <param name="sourceName">The source description used by invalid-content errors.</param>
    /// <param name="options">The general options governing metadata validation.</param>
    /// <param name="sequenceHeaderCount">Receives the number of sequence-header OBUs in the sequence.</param>
    /// <param name="sequenceHeaderOffset">Receives the first sequence-header payload offset, or <c>-1</c>.</param>
    /// <param name="sequenceHeaderLength">Receives the first sequence-header payload length.</param>
    /// <param name="sequenceHeaderExtension">Receives the first sequence-header extension byte, or <c>-1</c>.</param>
    /// <param name="contentLightLevel">
    /// Receives the content light-level metadata carried by the sequence, or <see langword="null"/> when absent.
    /// </param>
    /// <param name="masteringDisplayColorVolume">
    /// Receives the mastering-display metadata carried by the sequence, or <see langword="null"/> when absent.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the scan reached the end of the data. <see langword="false"/> if the scan reported an invalid OBU header and the mode ignores
    /// it.
    /// </returns>
    private static bool ScanObus(
        ReadOnlySpan<byte> data,
        bool isConfiguration,
        SegmentIntegrityHandling payloadIntegrity,
        bool imageExists,
        string sourceName,
        DecoderOptions options,
        out int sequenceHeaderCount,
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
        sequenceHeaderCount = 0;
        int obuIndex = 0;
        int offset = 0;
        while (offset < data.Length)
        {
            ObuFramingStatus framingStatus = ObuFramer.ReadObu(data[offset..], out ObuHeader obu);
            if (framingStatus == ObuFramingStatus.Truncated)
            {
                // The data ends inside the OBU header or its size field. No OBU follows it.
                if (isConfiguration)
                {
                    throw new InvalidImageContentException(ObuFramer.GetMessage(framingStatus));
                }

                break;
            }

            if (framingStatus is ObuFramingStatus.ForbiddenBit or ObuFramingStatus.InvalidSize)
            {
                // The scan runs before the AV1 decoder reads the data, so the data holds no decoded image yet. The bytes that remain have no OBU boundary, so
                // the scan stops here.
                payloadIntegrity.ThrowOrIgnoreImageDataError(imageExists, ObuFramer.GetMessage(framingStatus));
                return false;
            }

            if (isConfiguration && (!obu.HasSizeField || framingStatus == ObuFramingStatus.PayloadTruncated))
            {
                string message = obu.HasSizeField
                    ? ObuFramer.GetMessage(framingStatus)
                    : $"The {sourceName} contains an OBU without its required payload-size field.";

                throw new InvalidImageContentException(message);
            }

            int payloadOffset = offset + obu.PayloadOffset;
            if (obu.Type == ObuType.SequenceHeader)
            {
                if (isConfiguration && obuIndex != 0)
                {
                    throw new InvalidImageContentException($"The {sourceName} contains a sequence header OBU after another OBU.");
                }

                sequenceHeaderCount++;
                if (sequenceHeaderOffset < 0)
                {
                    sequenceHeaderOffset = payloadOffset;
                    sequenceHeaderLength = obu.PayloadLength;
                    sequenceHeaderExtension = obu.Extension;
                }
            }
            else if (obu.Type == ObuType.Metadata && !options.SkipMetadata)
            {
                string? metadataError = ReadHdrMetadata(
                    data.Slice(payloadOffset, obu.PayloadLength),
                    sourceName,
                    out HeifContentLightLevel? obuContentLightLevel,
                    out HeifMasteringDisplayColorVolume? obuMasteringDisplayColorVolume);

                if (metadataError is null && !ContentLightLevelMatchesProperty(obuContentLightLevel, contentLightLevel))
                {
                    metadataError = $"The {sourceName} contains conflicting content light-level metadata OBUs.";
                }

                if (metadataError is null
                    && obuMasteringDisplayColorVolume is not null
                    && masteringDisplayColorVolume is not null
                    && masteringDisplayColorVolume.Value != obuMasteringDisplayColorVolume.Value)
                {
                    metadataError = $"The {sourceName} contains conflicting mastering-display metadata OBUs.";
                }

                if (metadataError is not null)
                {
                    // The scan still sets the bounds of the OBU payload. The mode discards only the invalid optional metadata of the OBU.
                    options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError(metadataError);
                }
                else
                {
                    contentLightLevel = obuContentLightLevel ?? contentLightLevel;
                    masteringDisplayColorVolume = obuMasteringDisplayColorVolume ?? masteringDisplayColorVolume;
                }
            }

            offset += obu.Size;
            obuIndex++;
        }

        return true;
    }

    /// <summary>
    /// Reads still-image high-dynamic-range data from an AV1 metadata OBU payload.
    /// </summary>
    /// <param name="payload">The bounded metadata OBU payload.</param>
    /// <param name="sourceName">The source description used by invalid-content errors.</param>
    /// <param name="contentLightLevel">Receives decoded content light-level metadata when present.</param>
    /// <param name="masteringDisplayColorVolume">Receives decoded mastering-display metadata when present.</param>
    /// <returns>The error message, or <see langword="null"/> when the metadata is valid or is not HDR metadata.</returns>
    /// <remarks>
    /// This scan is the only parse and the only report of HDR metadata. The AV1 OBU reader checks the other metadata types and the metadata type field. The
    /// OBU reader reports their damage one time.
    /// </remarks>
    public static string? ReadHdrMetadata(
        ReadOnlySpan<byte> payload,
        string sourceName,
        out HeifContentLightLevel? contentLightLevel,
        out HeifMasteringDisplayColorVolume? masteringDisplayColorVolume)
    {
        contentLightLevel = null;
        masteringDisplayColorVolume = null;
        if (ObuFramer.TryReadLeb128(payload, out uint metadataType, out int offset) != ObuFramingStatus.Complete)
        {
            // Without a valid type, the payload is not HDR metadata that this scan can read.
            return null;
        }

        if (metadataType != (uint)ObuMetadataType.HdrCll
            && metadataType != (uint)ObuMetadataType.HdrMdcv)
        {
            return null;
        }

        int metadataLength = metadataType == (uint)ObuMetadataType.HdrCll ? 4 : 24;
        if (payload.Length - offset <= metadataLength)
        {
            return $"The {sourceName} contains truncated HDR metadata or no trailing bits.";
        }

        // Both fixed HDR structures end on a byte boundary. The scan accepts zero padding after the necessary 0x80 byte. As a result, the code finds the last
        // nonzero byte. It does not assume that the OBU payload ends directly after trailing_bits().
        ReadOnlySpan<byte> trailingData = payload[(offset + metadataLength)..];
        int lastNonzeroIndex = trailingData.LastIndexOfAnyExcept((byte)0);
        if (lastNonzeroIndex < 0 || trailingData[lastNonzeroIndex] != 0x80)
        {
            return $"The {sourceName} HDR metadata has invalid trailing bits.";
        }

        ReadOnlySpan<byte> metadataData = payload.Slice(offset, metadataLength);
        if (metadataType == (uint)ObuMetadataType.HdrCll)
        {
            contentLightLevel = new HeifContentLightLevel(
                BinaryPrimitives.ReadUInt16BigEndian(metadataData),
                BinaryPrimitives.ReadUInt16BigEndian(metadataData[2..]));

            return null;
        }

        const float chromaticityScale = 1F / 65536F;
        const double maximumLuminanceScale = 1D / 256D;
        const double minimumLuminanceScale = 1D / 16384D;

        // AV1 stores the primaries in R, G, B order. Its fixed-point units differ from the units of the ISOBMFF mdcv property. The scales below convert the AV1
        // values to the same ImageSharp color coordinates that the mdcv property gives.
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

        return null;
    }

    /// <summary>
    /// Determines whether content light-level metadata matches a related value when both values are present.
    /// </summary>
    /// <param name="obuContentLightLevel">The value carried by an AV1 metadata OBU.</param>
    /// <param name="itemContentLightLevel">The value carried by the associated image-item property or by another AV1 metadata OBU.</param>
    /// <returns><see langword="true"/> when either value is absent or both values are equal.</returns>
    private static bool ContentLightLevelMatchesProperty(HeifContentLightLevel? obuContentLightLevel, HeifContentLightLevel? itemContentLightLevel)
        => obuContentLightLevel is null
            || itemContentLightLevel is null
            || ContentLightLevelsMatch(obuContentLightLevel.Value, itemContentLightLevel.Value);

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
    /// Determines whether mastering-display metadata matches the related image-item property when both values are present.
    /// </summary>
    /// <param name="obuColorVolume">The value carried by an AV1 metadata OBU.</param>
    /// <param name="itemColorVolume">The value carried by the associated image-item property.</param>
    /// <returns><see langword="true"/> when either value is absent or both values are equal within their fixed-point precision.</returns>
    private static bool MasteringDisplayColorVolumeMatchesProperty(
        HeifMasteringDisplayColorVolume? obuColorVolume,
        HeifMasteringDisplayColorVolume? itemColorVolume)
        => obuColorVolume is null
            || itemColorVolume is null
            || MasteringDisplayColorVolumesMatch(obuColorVolume.Value, itemColorVolume.Value);

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
        // Each tolerance is half the sum of the two quantization steps. The AV1 steps are 1/65536, 1/256 and 1/16384. The mdcv steps are 1/50000 for
        // chromaticity and 1/10000 for luminance.
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
