// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Contains the length-delimited NAL units and IDR slice segments carried by one HEVC still-image item.
/// </summary>
internal sealed class HevcImageItemBitstream
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HevcImageItemBitstream"/> class.
    /// </summary>
    /// <param name="data">The complete bounded payload of one <c>hvc1</c> image item.</param>
    /// <param name="configuration">The codec configuration associated with the same image item.</param>
    /// <exception cref="InvalidImageContentException">
    /// NAL-unit framing is malformed, the payload contains sequence or layered coding, or the item does not contain
    /// exactly one independently decodable IDR picture.
    /// </exception>
    public HevcImageItemBitstream(ReadOnlySpan<byte> data, HevcCodecConfiguration configuration)
    {
        List<HevcNalUnit> nalUnits = [];
        List<HevcSliceSegmentHeader> sliceSegments = [];
        HevcSupplementalEnhancementInformation supplementalEnhancementInformation = new();
        int offset = 0;
        while (offset < data.Length)
        {
            if (data.Length - offset < configuration.NalUnitLengthSize)
            {
                throw new InvalidImageContentException("The HEVC image item has a truncated NAL-unit length.");
            }

            int nalUnitLength = ReadNalUnitLength(data[offset..], configuration.NalUnitLengthSize);
            offset += configuration.NalUnitLengthSize;
            if (nalUnitLength < 2 || nalUnitLength > data.Length - offset)
            {
                throw new InvalidImageContentException("The HEVC image item has an invalid NAL-unit length.");
            }

            HevcNalUnit nalUnit = new(data.Slice(offset, nalUnitLength));
            offset += nalUnitLength;
            if (nalUnit.Header.LayerId != 0 || nalUnit.Header.TemporalId != 0)
            {
                throw new InvalidImageContentException("The HEVC image item contains layered or temporal-substream NAL units.");
            }

            nalUnits.Add(nalUnit);
            if (nalUnit.Header.IsVideoCodingLayer)
            {
                if (!nalUnit.Header.IsInstantaneousDecoderRefresh)
                {
                    throw new InvalidImageContentException("The HEVC image item contains a coded picture that is not independently decodable.");
                }

                HevcSliceSegmentHeader sliceSegment = new(nalUnit, configuration.PictureParameterSets);
                if (sliceSegments.Count == 0 && !sliceSegment.FirstSliceSegmentInPicture)
                {
                    throw new InvalidImageContentException("The first HEVC image-item slice is not marked as the first picture segment.");
                }

                if (sliceSegments.Count != 0 && sliceSegment.FirstSliceSegmentInPicture)
                {
                    throw new InvalidImageContentException("The HEVC image item contains more than one coded picture.");
                }

                sliceSegments.Add(sliceSegment);
                continue;
            }

            if (nalUnit.Header.NalUnitType is 32 or 33 or 34)
            {
                // hvc1 image items obtain all parameter sets from the associated hvcC property. Accepting in-band
                // replacements would silently apply the more permissive hev1 sample contract to this still image.
                throw new InvalidImageContentException("The HEVC hvc1 image item contains an in-band parameter set.");
            }

            if (nalUnit.Header.NalUnitType is 36 or 37)
            {
                throw new InvalidImageContentException("The HEVC image item contains an end-of-sequence NAL unit.");
            }

            if (nalUnit.Header.NalUnitType == 39)
            {
                // Prefix SEI belongs to the following VCL NAL unit. Once this bounded item has started its only
                // picture, another prefix unit would describe a second access unit that the item is not allowed to carry.
                if (sliceSegments.Count != 0)
                {
                    throw new InvalidImageContentException("The HEVC image item contains prefix SEI after its first coded slice.");
                }

                // Prefix SEI messages are associated with this item's only access unit. Parse the observable still-image
                // state in NAL and message order without retaining generic video persistence or timing state.
                supplementalEnhancementInformation.ReadPrefixNalUnit(nalUnit.Rbsp.Span);
            }
        }

        if (sliceSegments.Count == 0)
        {
            throw new InvalidImageContentException("The HEVC image item contains no independently decodable picture.");
        }

        this.NalUnits = nalUnits;
        this.SliceSegments = sliceSegments;
        this.SupplementalEnhancementInformation = supplementalEnhancementInformation;
    }

    /// <summary>
    /// Gets every decoded NAL unit in item order, including permitted delimiter, filler, and supplemental units.
    /// </summary>
    public IReadOnlyList<HevcNalUnit> NalUnits { get; }

    /// <summary>
    /// Gets the ordered slice segments that reconstruct the item's single IDR picture.
    /// </summary>
    public IReadOnlyList<HevcSliceSegmentHeader> SliceSegments { get; }

    /// <summary>
    /// Gets the presentation and exposed metadata decoded from prefix SEI NAL units.
    /// </summary>
    public HevcSupplementalEnhancementInformation SupplementalEnhancementInformation { get; }

    /// <summary>
    /// Reads an unsigned one-through-four-byte NAL-unit length without assuming four-byte item framing.
    /// </summary>
    /// <param name="data">The item bytes beginning at the length field.</param>
    /// <param name="lengthSize">The codec-configuration-selected length-field width.</param>
    /// <returns>The bounded signed integer NAL-unit length.</returns>
    /// <exception cref="InvalidImageContentException">The unsigned length exceeds the supported item-buffer range.</exception>
    private static int ReadNalUnitLength(ReadOnlySpan<byte> data, int lengthSize)
    {
        uint value = 0;
        for (int byteIndex = 0; byteIndex < lengthSize; byteIndex++)
        {
            value = (value << 8) | data[byteIndex];
        }

        if (value > int.MaxValue)
        {
            throw new InvalidImageContentException("The HEVC image-item NAL-unit length is too large.");
        }

        return (int)value;
    }
}
