// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Contains the bounded HEVC video-parameter-set fields required to validate and decode one still-image item.
/// </summary>
internal sealed class HevcVideoParameterSet
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HevcVideoParameterSet"/> class.
    /// </summary>
    /// <param name="nalUnit">The decoded video-parameter-set NAL unit.</param>
    /// <exception cref="InvalidImageContentException">
    /// The NAL unit is not a supported, conforming base-layer video parameter set.
    /// </exception>
    public HevcVideoParameterSet(HevcNalUnit nalUnit)
    {
        const byte videoParameterSetNalUnitType = 32;
        if (nalUnit.Header.NalUnitType != videoParameterSetNalUnitType
            || nalUnit.Header.LayerId != 0
            || nalUnit.Header.TemporalId != 0)
        {
            throw new InvalidImageContentException("The HEVC video parameter set has an invalid NAL-unit header.");
        }

        HevcBitReader reader = new(nalUnit.Rbsp.Span);
        this.Id = (byte)reader.ReadBits(4);

        bool baseLayerInternal = reader.ReadFlag();
        bool baseLayerAvailable = reader.ReadFlag();
        if (!baseLayerInternal || !baseLayerAvailable)
        {
            throw new InvalidImageContentException("The HEVC video parameter set does not make its base layer available.");
        }

        int maxLayersMinusOne = (int)reader.ReadBits(6);
        if (maxLayersMinusOne != 0)
        {
            // HEIF auxiliary images are separate image items. Importing an HEVC multilayer selection model would
            // exceed the one-presented-image contract and is not part of the exposed still-picture profiles.
            throw new InvalidImageContentException("Layered HEVC video parameter sets are not supported for still-image items.");
        }

        int maxSubLayersMinusOne = (int)reader.ReadBits(3);
        if (maxSubLayersMinusOne > 6)
        {
            throw new InvalidImageContentException("The HEVC video parameter set declares too many temporal sublayers.");
        }

        this.MaxSubLayers = maxSubLayersMinusOne + 1;
        this.TemporalIdNestingFlag = reader.ReadFlag();
        if (maxSubLayersMinusOne == 0 && !this.TemporalIdNestingFlag)
        {
            throw new InvalidImageContentException("The HEVC video parameter set has invalid temporal nesting.");
        }

        if (reader.ReadBits(16) != ushort.MaxValue)
        {
            throw new InvalidImageContentException("The HEVC video parameter set has invalid reserved bits.");
        }

        this.ProfileTierLevel = new HevcProfileTierLevel(ref reader, maxSubLayersMinusOne);

        bool subLayerOrderingInfoPresent = reader.ReadFlag();
        int firstOrderingSubLayer = subLayerOrderingInfoPresent ? 0 : maxSubLayersMinusOne;
        for (int subLayer = firstOrderingSubLayer; subLayer <= maxSubLayersMinusOne; subLayer++)
        {
            uint maxDecodedPictureBufferingMinusOne = reader.ReadUnsignedExpGolomb();
            uint maxNumReorderPictures = reader.ReadUnsignedExpGolomb();
            reader.ReadUnsignedExpGolomb();
            if (maxNumReorderPictures > maxDecodedPictureBufferingMinusOne)
            {
                throw new InvalidImageContentException("The HEVC video parameter set has invalid sublayer ordering limits.");
            }
        }

        uint maxLayerId = reader.ReadBits(6);
        uint numLayerSetsMinusOne = reader.ReadUnsignedExpGolomb();
        if (maxLayerId != 0 || numLayerSetsMinusOne != 0)
        {
            throw new InvalidImageContentException("HEVC layer sets are not supported for still-image items.");
        }

        bool timingInfoPresent = reader.ReadFlag();
        if (timingInfoPresent)
        {
            // Timing and hypothetical-reference-decoder values are required for bit alignment but do not describe
            // the pixels of the one image item, so they are deliberately consumed without retained playback state.
            reader.ReadBits(32);
            reader.ReadBits(32);
            if (reader.ReadFlag())
            {
                reader.ReadUnsignedExpGolomb();
            }

            uint hrdParameterCount = reader.ReadUnsignedExpGolomb();
            if (hrdParameterCount > 1024)
            {
                throw new InvalidImageContentException("The HEVC video parameter set declares too many HRD parameter sets.");
            }

            bool nalHrdParametersPresent = false;
            bool vclHrdParametersPresent = false;
            bool subPictureHrdParametersPresent = false;
            for (uint hrdIndex = 0; hrdIndex < hrdParameterCount; hrdIndex++)
            {
                uint layerSetIndex = reader.ReadUnsignedExpGolomb();
                if (layerSetIndex != 0)
                {
                    throw new InvalidImageContentException("The HEVC HRD parameters reference an unsupported layer set.");
                }

                bool commonInformationPresent = hrdIndex == 0 || reader.ReadFlag();
                HevcParameterSetSyntax.SkipHrdParameters(
                    ref reader,
                    commonInformationPresent,
                    maxSubLayersMinusOne,
                    ref nalHrdParametersPresent,
                    ref vclHrdParametersPresent,
                    ref subPictureHrdParametersPresent);
            }
        }

        if (reader.ReadFlag())
        {
            while (reader.HasMoreRbspData())
            {
                reader.ReadFlag();
            }
        }

        reader.ReadRbspTrailingBits();
    }

    /// <summary>
    /// Gets the four-bit video-parameter-set identifier.
    /// </summary>
    public byte Id { get; }

    /// <summary>
    /// Gets the declared number of temporal sublayers.
    /// </summary>
    public int MaxSubLayers { get; }

    /// <summary>
    /// Gets a value indicating whether temporal identifiers are nested.
    /// </summary>
    public bool TemporalIdNestingFlag { get; }

    /// <summary>
    /// Gets the general profile, tier, constraint, and level description.
    /// </summary>
    public HevcProfileTierLevel ProfileTierLevel { get; }
}
