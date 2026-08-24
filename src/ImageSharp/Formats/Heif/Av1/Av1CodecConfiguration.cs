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
}
