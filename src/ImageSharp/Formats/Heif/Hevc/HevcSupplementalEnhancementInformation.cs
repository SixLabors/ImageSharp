// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Reads the presentation and exposed metadata carried by prefix SEI NAL units for one bounded still picture.
/// </summary>
internal sealed class HevcSupplementalEnhancementInformation
{
    private const int DisplayOrientationPayloadType = 47;
    private const int MasteringDisplayColorVolumePayloadType = 137;
    private const int NoDisplayPayloadType = 135;
    private const int ContentLightLevelPayloadType = 144;
    private const int AlternativeTransferCharacteristicsPayloadType = 147;
    private const int AmbientViewingEnvironmentPayloadType = 148;
    private const int ContentColorVolumePayloadType = 149;

    /// <summary>
    /// Gets a value indicating whether the selected still picture is marked as unavailable for display.
    /// </summary>
    public bool NoDisplay { get; private set; }

    /// <summary>
    /// Gets a value indicating whether an active display-orientation message is present.
    /// </summary>
    public bool HasDisplayOrientation { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the cropped decoded picture is flipped horizontally before rotation.
    /// </summary>
    public bool HorizontalFlip { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the cropped decoded picture is flipped vertically before rotation.
    /// </summary>
    public bool VerticalFlip { get; private set; }

    /// <summary>
    /// Gets the unsigned fraction of one complete anticlockwise turn applied after flipping.
    /// </summary>
    public ushort AnticlockwiseRotation { get; private set; }

    /// <summary>
    /// Gets the preferred CICP transfer-characteristics code, when signaled.
    /// </summary>
    public byte? PreferredTransferCharacteristics { get; private set; }

    /// <summary>
    /// Gets the content light-level description, when signaled.
    /// </summary>
    public HeifContentLightLevel? ContentLightLevel { get; private set; }

    /// <summary>
    /// Gets the mastering-display color volume, when signaled.
    /// </summary>
    public HeifMasteringDisplayColorVolume? MasteringDisplayColorVolume { get; private set; }

    /// <summary>
    /// Gets the content color volume, when signaled and not cancelled.
    /// </summary>
    public HeifContentColorVolume? ContentColorVolume { get; private set; }

    /// <summary>
    /// Gets the ambient viewing environment, when signaled.
    /// </summary>
    public HeifAmbientViewingEnvironment? AmbientViewingEnvironment { get; private set; }

    /// <summary>
    /// Reads every byte-aligned message from one prefix SEI RBSP in bitstream order.
    /// </summary>
    /// <param name="rbsp">The decoded NAL payload, including its RBSP trailing byte.</param>
    public void ReadPrefixNalUnit(ReadOnlySpan<byte> rbsp)
    {
        if (rbsp.IsEmpty)
        {
            throw new InvalidImageContentException("The HEVC prefix SEI NAL unit is missing RBSP trailing bits.");
        }

        int offset = 0;
        while (rbsp.Length - offset > 1)
        {
            int payloadType = ReadExtendedValue(rbsp, ref offset, "payload type");
            int payloadSize = ReadExtendedValue(rbsp, ref offset, "payload size");
            if (payloadSize > rbsp.Length - offset)
            {
                throw new InvalidImageContentException("The HEVC prefix SEI message payload is truncated.");
            }

            ReadOnlySpan<byte> payload = rbsp.Slice(offset, payloadSize);
            offset += payloadSize;
            switch (payloadType)
            {
                case DisplayOrientationPayloadType:
                    this.ReadDisplayOrientation(payload);
                    break;
                case NoDisplayPayloadType:
                    this.ReadNoDisplay(payload);
                    break;
                case MasteringDisplayColorVolumePayloadType:
                    this.ReadMasteringDisplayColorVolume(payload);
                    break;
                case ContentLightLevelPayloadType:
                    this.ReadContentLightLevel(payload);
                    break;
                case AlternativeTransferCharacteristicsPayloadType:
                    this.ReadAlternativeTransferCharacteristics(payload);
                    break;
                case AmbientViewingEnvironmentPayloadType:
                    this.ReadAmbientViewingEnvironment(payload);
                    break;
                case ContentColorVolumePayloadType:
                    this.ReadContentColorVolume(payload);
                    break;
            }
        }

        if (offset != rbsp.Length - 1 || rbsp[offset] != 0x80)
        {
            throw new InvalidImageContentException("The HEVC prefix SEI NAL unit has invalid RBSP trailing bits.");
        }
    }

    /// <summary>
    /// Reads the legacy HEVC display-orientation payload retained by pinned HM.
    /// </summary>
    private void ReadDisplayOrientation(ReadOnlySpan<byte> payload)
    {
        HevcBitReader reader = new(payload);
        bool cancel = reader.ReadFlag();
        if (cancel)
        {
            this.HasDisplayOrientation = false;
            this.HorizontalFlip = false;
            this.VerticalFlip = false;
            this.AnticlockwiseRotation = 0;
            ValidatePayloadExtension(ref reader, "display orientation");
            return;
        }

        this.HorizontalFlip = reader.ReadFlag();
        this.VerticalFlip = reader.ReadFlag();
        this.AnticlockwiseRotation = (ushort)reader.ReadBits(16);
        _ = reader.ReadFlag();
        ValidatePayloadExtension(ref reader, "display orientation");
        this.HasDisplayOrientation = true;
    }

    /// <summary>
    /// Records that the selected picture is not intended for display.
    /// </summary>
    private void ReadNoDisplay(ReadOnlySpan<byte> payload)
    {
        // Pinned HM writes no syntax bits for this message, producing a zero-byte payload. A nonempty payload can
        // contain only the generic reserved extension and payload-alignment marker handled by the shared validator.
        ValidateByteAlignedPayloadExtension(payload, "no-display");
        this.NoDisplay = true;
    }

    /// <summary>
    /// Reads mastering-display metadata in its HEVC fixed-point representation.
    /// </summary>
    private void ReadMasteringDisplayColorVolume(ReadOnlySpan<byte> payload)
    {
        const int syntaxLength = 24;
        if (payload.Length < syntaxLength)
        {
            throw new InvalidImageContentException("The HEVC mastering-display color-volume SEI payload is truncated.");
        }

        this.MasteringDisplayColorVolume = HeifPropertyParser.ParseMasteringDisplayColorVolume(payload[..syntaxLength]);
        ValidateByteAlignedPayloadExtension(payload[syntaxLength..], "mastering-display color-volume");
    }

    /// <summary>
    /// Reads content light-level metadata in its HEVC fixed-width representation.
    /// </summary>
    private void ReadContentLightLevel(ReadOnlySpan<byte> payload)
    {
        const int syntaxLength = 4;
        if (payload.Length < syntaxLength)
        {
            throw new InvalidImageContentException("The HEVC content light-level SEI payload is truncated.");
        }

        this.ContentLightLevel = HeifPropertyParser.ParseContentLightLevel(payload[..syntaxLength]);
        ValidateByteAlignedPayloadExtension(payload[syntaxLength..], "content light-level");
    }

    /// <summary>
    /// Reads the preferred transfer function applied when the container does not provide one.
    /// </summary>
    private void ReadAlternativeTransferCharacteristics(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
        {
            throw new InvalidImageContentException("The HEVC alternative-transfer-characteristics SEI payload is truncated.");
        }

        this.PreferredTransferCharacteristics = payload[0];
        ValidateByteAlignedPayloadExtension(payload[1..], "alternative transfer characteristics");
    }

    /// <summary>
    /// Reads the nominal ambient viewing environment.
    /// </summary>
    private void ReadAmbientViewingEnvironment(ReadOnlySpan<byte> payload)
    {
        const int syntaxLength = 8;
        if (payload.Length < syntaxLength)
        {
            throw new InvalidImageContentException("The HEVC ambient-viewing-environment SEI payload is truncated.");
        }

        this.AmbientViewingEnvironment = HeifPropertyParser.ParseAmbientViewingEnvironment(payload[..syntaxLength]);
        ValidateByteAlignedPayloadExtension(payload[syntaxLength..], "ambient viewing environment");
    }

    /// <summary>
    /// Reads the bit-packed content color-volume syntax and applies cancellation in message order.
    /// </summary>
    private void ReadContentColorVolume(ReadOnlySpan<byte> payload)
    {
        HevcBitReader reader = new(payload);
        bool cancel = reader.ReadFlag();
        if (cancel)
        {
            this.ContentColorVolume = null;
            ValidatePayloadExtension(ref reader, "content color-volume");
            return;
        }

        _ = reader.ReadFlag();
        bool primariesPresent = reader.ReadFlag();
        bool minimumLuminancePresent = reader.ReadFlag();
        bool maximumLuminancePresent = reader.ReadFlag();
        bool averageLuminancePresent = reader.ReadFlag();
        RgbPrimariesChromaticityCoordinates? primaries = null;
        if (primariesPresent)
        {
            int greenX = unchecked((int)reader.ReadBits(32));
            int greenY = unchecked((int)reader.ReadBits(32));
            int blueX = unchecked((int)reader.ReadBits(32));
            int blueY = unchecked((int)reader.ReadBits(32));
            int redX = unchecked((int)reader.ReadBits(32));
            int redY = unchecked((int)reader.ReadBits(32));
            const int maximumChromaticityValue = 5_000_000;
            if (greenX is < -maximumChromaticityValue or > maximumChromaticityValue
                || greenY is < -maximumChromaticityValue or > maximumChromaticityValue
                || blueX is < -maximumChromaticityValue or > maximumChromaticityValue
                || blueY is < -maximumChromaticityValue or > maximumChromaticityValue
                || redX is < -maximumChromaticityValue or > maximumChromaticityValue
                || redY is < -maximumChromaticityValue or > maximumChromaticityValue)
            {
                throw new InvalidImageContentException("The HEVC content color-volume SEI payload has an out-of-range primary coordinate.");
            }

            const float chromaticityScale = 1F / 50000F;

            // H.274 stores signed primary coordinates in G, B, R order. Reorder them once at the codec boundary so
            // the retained value has the same observable RGB coordinate contract as the equivalent item property.
            primaries = new RgbPrimariesChromaticityCoordinates(
                new CieXyChromaticityCoordinates(redX * chromaticityScale, redY * chromaticityScale),
                new CieXyChromaticityCoordinates(greenX * chromaticityScale, greenY * chromaticityScale),
                new CieXyChromaticityCoordinates(blueX * chromaticityScale, blueY * chromaticityScale));
        }

        uint? minimumLuminance = minimumLuminancePresent ? reader.ReadBits(32) : null;
        uint? maximumLuminance = maximumLuminancePresent ? reader.ReadBits(32) : null;
        uint? averageLuminance = averageLuminancePresent ? reader.ReadBits(32) : null;
        if ((minimumLuminance is not null && averageLuminance is not null && minimumLuminance.Value > averageLuminance.Value)
            || (averageLuminance is not null && maximumLuminance is not null && averageLuminance.Value > maximumLuminance.Value)
            || (minimumLuminance is not null && maximumLuminance is not null && minimumLuminance.Value > maximumLuminance.Value))
        {
            throw new InvalidImageContentException("The HEVC content color-volume SEI luminance values are not in ascending order.");
        }

        ValidatePayloadExtension(ref reader, "content color-volume");
        const double luminanceScale = 1D / 10000000D;

        this.ContentColorVolume = new HeifContentColorVolume(
            primaries,
            minimumLuminance * luminanceScale,
            maximumLuminance * luminanceScale,
            averageLuminance * luminanceScale);
    }

    /// <summary>
    /// Reads an extended SEI payload type or size whose continuation bytes are all 255.
    /// </summary>
    private static int ReadExtendedValue(ReadOnlySpan<byte> data, ref int offset, string valueName)
    {
        int value = 0;
        while (true)
        {
            if ((uint)offset >= (uint)data.Length)
            {
                throw new InvalidImageContentException($"The HEVC prefix SEI {valueName} is truncated.");
            }

            int current = data[offset++];
            if (value > int.MaxValue - current)
            {
                throw new InvalidImageContentException($"The HEVC prefix SEI {valueName} is too large.");
            }

            value += current;
            if (current != byte.MaxValue)
            {
                return value;
            }
        }
    }

    /// <summary>
    /// Validates an optional extension following fixed byte-aligned SEI syntax.
    /// </summary>
    private static void ValidateByteAlignedPayloadExtension(ReadOnlySpan<byte> extension, string payloadName)
    {
        if (extension.IsEmpty)
        {
            return;
        }

        HevcBitReader reader = new(extension);
        ValidatePayloadExtension(ref reader, payloadName);
    }

    /// <summary>
    /// Validates reserved payload-extension data followed by its final one bit and zero padding.
    /// </summary>
    private static void ValidatePayloadExtension(ref HevcBitReader reader, string payloadName)
    {
        bool foundMarker = false;
        while (reader.BitsRemaining > 0)
        {
            foundMarker |= reader.ReadFlag();
        }

        // The final set bit is payload_bit_equal_to_one; any preceding bits are the reserved extension data that
        // pinned HM deliberately skips. An all-zero remainder has no marker and is therefore not a complete payload.
        if (!foundMarker)
        {
            throw new InvalidImageContentException($"The HEVC {payloadName} SEI payload has invalid trailing bits.");
        }
    }
}
