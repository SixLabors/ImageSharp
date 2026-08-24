// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
    /// Initializes a new instance of the <see cref="Av1CodecConfiguration"/> class from an AV1 codec-configuration
    /// item-property payload.
    /// </summary>
    /// <param name="boxBuffer">The configuration payload beginning with the marker and version fields.</param>
    public Av1CodecConfiguration(Span<byte> boxBuffer)
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
            out this.configSequenceHeaderOffset,
            out this.configSequenceHeaderLength,
            out this.configSequenceHeaderExtension);

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
    public int BitDepth => this.TwelveBit ? 12 : this.HighBitDepth ? 10 : 8;

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
    /// Validates the AV1 image item OBU layout and any sequence header repeated by the configuration record.
    /// </summary>
    /// <param name="itemData">The complete AV1 image item payload.</param>
    public void ValidateItemData(ReadOnlySpan<byte> itemData)
    {
        int sequenceHeaderCount = ScanObus(
            itemData,
            false,
            false,
            "AV1 image item",
            out int itemSequenceHeaderOffset,
            out int itemSequenceHeaderLength,
            out int itemSequenceHeaderExtension);

        if (sequenceHeaderCount != 1)
        {
            throw new InvalidImageContentException($"The AV1 image item contains {sequenceHeaderCount} sequence header OBUs instead of exactly one.");
        }

        if (this.configSequenceHeaderOffset >= 0)
        {
            ReadOnlySpan<byte> configSequenceHeader = this.configObus.AsSpan(
                this.configSequenceHeaderOffset,
                this.configSequenceHeaderLength);

            ReadOnlySpan<byte> itemSequenceHeader = itemData.Slice(
                itemSequenceHeaderOffset,
                itemSequenceHeaderLength);

            // Compare the extension and payload rather than the encoded OBU size. Configuration OBUs must carry a
            // size field while an image item's final OBU may omit one, and different legal LEB128 widths do not alter
            // the Sequence Header OBU being repeated.
            if (this.configSequenceHeaderExtension != itemSequenceHeaderExtension
                || !configSequenceHeader.SequenceEqual(itemSequenceHeader))
            {
                throw new InvalidImageContentException("The AV1 codec configuration sequence header does not match the image item sequence header.");
            }
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
    /// Scans a low-overhead AV1 OBU sequence and locates its first sequence-header payload.
    /// </summary>
    /// <param name="data">The complete bounded OBU sequence.</param>
    /// <param name="requireSizeFields">Indicates that every OBU must carry its registered payload-size field.</param>
    /// <param name="sequenceHeaderMustBeFirst">
    /// Indicates that a sequence-header OBU, when present, must be the first OBU in the sequence.
    /// </param>
    /// <param name="sourceName">The source description used by invalid-content errors.</param>
    /// <param name="sequenceHeaderOffset">Receives the first sequence-header payload offset, or <c>-1</c>.</param>
    /// <param name="sequenceHeaderLength">Receives the first sequence-header payload length.</param>
    /// <param name="sequenceHeaderExtension">Receives the first sequence-header extension byte, or <c>-1</c>.</param>
    /// <returns>The number of sequence-header OBUs in the sequence.</returns>
    private static int ScanObus(
        ReadOnlySpan<byte> data,
        bool requireSizeFields,
        bool sequenceHeaderMustBeFirst,
        string sourceName,
        out int sequenceHeaderOffset,
        out int sequenceHeaderLength,
        out int sequenceHeaderExtension)
    {
        sequenceHeaderOffset = -1;
        sequenceHeaderLength = 0;
        sequenceHeaderExtension = -1;
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
        ulong value = 0;
        for (int byteIndex = 0; byteIndex < 8; byteIndex++)
        {
            if (offset >= data.Length)
            {
                throw new InvalidImageContentException($"The {sourceName} contains a truncated OBU payload length.");
            }

            byte current = data[offset++];
            value |= (ulong)(current & 0x7F) << (byteIndex * 7);
            if ((current & 0x80) == 0)
            {
                if (value > int.MaxValue)
                {
                    throw new InvalidImageContentException($"The {sourceName} contains an OBU payload too large to buffer.");
                }

                return (int)value;
            }
        }

        throw new InvalidImageContentException($"The {sourceName} contains an unterminated OBU payload length.");
    }
}
