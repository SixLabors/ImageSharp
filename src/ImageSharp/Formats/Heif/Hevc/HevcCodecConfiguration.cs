// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Contains the image-description fields and parameter-set arrays stored in an HEVC codec-configuration item
/// property.
/// </summary>
internal sealed class HevcCodecConfiguration
{
    /// <summary>
    /// The NAL-unit arrays carried by the codec-configuration property.
    /// </summary>
    private readonly HevcNalUnitArray[] nalUnitArrays;

    /// <summary>
    /// Initializes a new instance of the <see cref="HevcCodecConfiguration"/> class from an HEVC
    /// codec-configuration item-property payload.
    /// </summary>
    /// <param name="data">The complete bounded configuration payload.</param>
    public HevcCodecConfiguration(ReadOnlySpan<byte> data)
    {
        const int fixedRecordLength = 23;
        if (data.Length < fixedRecordLength)
        {
            throw new InvalidImageContentException("The HEVC codec configuration is truncated.");
        }

        int offset = 0;
        if (data[offset++] != 1)
        {
            throw new InvalidImageContentException("The HEVC codec configuration has an unsupported version.");
        }

        byte profile = data[offset++];
        this.GeneralProfileSpace = (byte)(profile >> 6);
        this.GeneralTierFlag = (profile & 0x20) != 0;
        this.GeneralProfileIdc = (byte)(profile & 0x1F);
        this.GeneralProfileCompatibilityFlags = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
        offset += 4;
        this.GeneralConstraintIndicatorFlags = ((ulong)BinaryPrimitives.ReadUInt32BigEndian(data[offset..]) << 16)
            | BinaryPrimitives.ReadUInt16BigEndian(data[(offset + 4)..]);

        offset += 6;
        this.GeneralLevelIdc = data[offset++];

        ushort spatialSegmentation = BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);
        offset += 2;
        byte parallelism = data[offset++];
        byte chromaFormat = data[offset++];
        byte lumaBitDepth = data[offset++];
        byte chromaBitDepth = data[offset++];
        if ((spatialSegmentation & 0xF000) != 0xF000
            || (parallelism & 0xFC) != 0xFC
            || (chromaFormat & 0xFC) != 0xFC
            || (lumaBitDepth & 0xF8) != 0xF8
            || (chromaBitDepth & 0xF8) != 0xF8)
        {
            throw new InvalidImageContentException("The HEVC codec configuration has invalid reserved bits.");
        }

        this.ChromaFormat = (byte)(chromaFormat & 3);
        this.BitDepthLuma = 8 + (lumaBitDepth & 7);
        this.BitDepthChroma = 8 + (chromaBitDepth & 7);

        // Average frame rate and temporal-layer signaling describe timed samples. Consume those fixed-record fields
        // to reach the image item's NAL length width without retaining playback state in the still-image model.
        offset += 2;
        byte temporalAndLengthFields = data[offset++];
        this.NalUnitLengthSize = (temporalAndLengthFields & 3) + 1;

        int arrayCount = data[offset++];
        this.nalUnitArrays = new HevcNalUnitArray[arrayCount];
        Span<bool> seenNalUnitTypes = stackalloc bool[64];
        for (int arrayIndex = 0; arrayIndex < arrayCount; arrayIndex++)
        {
            if (data.Length - offset < 3)
            {
                throw new InvalidImageContentException("The HEVC codec configuration contains a truncated NAL-unit array header.");
            }

            byte arrayHeader = data[offset++];
            if ((arrayHeader & 0x40) != 0)
            {
                throw new InvalidImageContentException("The HEVC codec configuration NAL-unit array has a nonzero reserved bit.");
            }

            bool isComplete = (arrayHeader & 0x80) != 0;
            byte nalUnitType = (byte)(arrayHeader & 0x3F);
            if (seenNalUnitTypes[nalUnitType])
            {
                throw new InvalidImageContentException($"The HEVC codec configuration contains more than one array for NAL-unit type {nalUnitType}.");
            }

            seenNalUnitTypes[nalUnitType] = true;
            int nalUnitCount = BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);
            offset += 2;
            HevcNalUnit[] nalUnits = new HevcNalUnit[nalUnitCount];
            for (int nalUnitIndex = 0; nalUnitIndex < nalUnitCount; nalUnitIndex++)
            {
                if (data.Length - offset < 2)
                {
                    throw new InvalidImageContentException("The HEVC codec configuration contains a truncated NAL-unit length.");
                }

                int nalUnitLength = BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);
                offset += 2;
                if (nalUnitLength < 2 || nalUnitLength > data.Length - offset)
                {
                    throw new InvalidImageContentException("The HEVC codec configuration contains an invalid NAL-unit length.");
                }

                HevcNalUnit nalUnit = new(data.Slice(offset, nalUnitLength));

                // The array header repeats the type so a damaged or misrouted parameter set is rejected before
                // its RBSP syntax can affect the image configuration.
                if (nalUnit.Header.NalUnitType != nalUnitType)
                {
                    throw new InvalidImageContentException("The HEVC codec configuration NAL-unit type does not match its array.");
                }

                nalUnits[nalUnitIndex] = nalUnit;
                offset += nalUnitLength;
            }

            this.nalUnitArrays[arrayIndex] = new HevcNalUnitArray(nalUnitType, isComplete, nalUnits);
        }

        if (offset != data.Length)
        {
            throw new InvalidImageContentException("The HEVC codec configuration contains unexpected trailing data.");
        }
    }

    /// <summary>
    /// Gets the profile namespace declared by the coded image.
    /// </summary>
    public byte GeneralProfileSpace { get; }

    /// <summary>
    /// Gets a value indicating whether the coded image uses the high tier.
    /// </summary>
    public bool GeneralTierFlag { get; }

    /// <summary>
    /// Gets the profile identifier declared by the coded image.
    /// </summary>
    public byte GeneralProfileIdc { get; }

    /// <summary>
    /// Gets the profile-compatibility flags declared by the coded image.
    /// </summary>
    public uint GeneralProfileCompatibilityFlags { get; }

    /// <summary>
    /// Gets the 48-bit profile-constraint flags declared by the coded image.
    /// </summary>
    public ulong GeneralConstraintIndicatorFlags { get; }

    /// <summary>
    /// Gets the level identifier declared by the coded image.
    /// </summary>
    public byte GeneralLevelIdc { get; }

    /// <summary>
    /// Gets the coded chroma format, where zero denotes monochrome and one through three denote 4:2:0, 4:2:2,
    /// and 4:4:4 respectively.
    /// </summary>
    public byte ChromaFormat { get; }

    /// <summary>
    /// Gets the coded luma sample precision in bits.
    /// </summary>
    public int BitDepthLuma { get; }

    /// <summary>
    /// Gets the coded chroma sample precision in bits.
    /// </summary>
    public int BitDepthChroma { get; }

    /// <summary>
    /// Gets the maximum coded color-component precision in bits.
    /// </summary>
    public int BitDepth => this.IsMonochrome ? this.BitDepthLuma : Math.Max(this.BitDepthLuma, this.BitDepthChroma);

    /// <summary>
    /// Gets a value indicating whether the coded image contains only a luma plane.
    /// </summary>
    public bool IsMonochrome => this.ChromaFormat == 0;

    /// <summary>
    /// Gets the number of bytes used by each length-delimited NAL unit in the associated image item.
    /// </summary>
    public int NalUnitLengthSize { get; }

    /// <summary>
    /// Gets the bounded NAL-unit arrays carried by the codec-configuration property.
    /// </summary>
    public IReadOnlyList<HevcNalUnitArray> NalUnitArrays => this.nalUnitArrays;

    /// <summary>
    /// Validates the associated pixel-information property against the coded luma and chroma sample precisions.
    /// </summary>
    /// <param name="channelBitDepths">The per-channel precisions associated with the HEVC image item.</param>
    public void ValidateChannelBitDepths(ReadOnlySpan<byte> channelBitDepths)
    {
        int expectedChannelCount = this.IsMonochrome ? 1 : 3;
        if (channelBitDepths.Length != expectedChannelCount || channelBitDepths[0] != this.BitDepthLuma)
        {
            throw new InvalidImageContentException("The HEVC item pixel information does not match its codec configuration.");
        }

        for (int channel = 1; channel < channelBitDepths.Length; channel++)
        {
            if (channelBitDepths[channel] != this.BitDepthChroma)
            {
                throw new InvalidImageContentException("The HEVC item pixel information does not match its codec configuration.");
            }
        }
    }

    /// <summary>
    /// Determines whether another configuration describes the same coded-image sample layout.
    /// </summary>
    /// <param name="other">The configuration to compare.</param>
    /// <returns><see langword="true"/> when the profile, level, chroma format, and sample precisions match.</returns>
    public bool HasMatchingImageConfiguration(HevcCodecConfiguration other)
        => this.GeneralProfileSpace == other.GeneralProfileSpace
            && this.GeneralTierFlag == other.GeneralTierFlag
            && this.GeneralProfileIdc == other.GeneralProfileIdc
            && this.GeneralProfileCompatibilityFlags == other.GeneralProfileCompatibilityFlags
            && this.GeneralConstraintIndicatorFlags == other.GeneralConstraintIndicatorFlags
            && this.GeneralLevelIdc == other.GeneralLevelIdc
            && this.ChromaFormat == other.ChromaFormat
            && this.BitDepthLuma == other.BitDepthLuma
            && this.BitDepthChroma == other.BitDepthChroma;
}

/// <summary>
/// Contains every configuration NAL unit declared for one HEVC NAL-unit type.
/// </summary>
internal sealed class HevcNalUnitArray
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HevcNalUnitArray"/> class.
    /// </summary>
    /// <param name="nalUnitType">The six-bit HEVC NAL-unit type.</param>
    /// <param name="isComplete">A value indicating whether the array contains every NAL unit of this type.</param>
    /// <param name="nalUnits">The decoded bounded NAL units.</param>
    public HevcNalUnitArray(byte nalUnitType, bool isComplete, HevcNalUnit[] nalUnits)
    {
        this.NalUnitType = nalUnitType;
        this.IsComplete = isComplete;
        this.NalUnits = nalUnits;
    }

    /// <summary>
    /// Gets the six-bit HEVC NAL-unit type shared by every entry in the array.
    /// </summary>
    public byte NalUnitType { get; }

    /// <summary>
    /// Gets a value indicating whether the array contains every NAL unit of this type for the coded image.
    /// </summary>
    public bool IsComplete { get; }

    /// <summary>
    /// Gets the decoded NAL units.
    /// </summary>
    public IReadOnlyList<HevcNalUnit> NalUnits { get; }
}
