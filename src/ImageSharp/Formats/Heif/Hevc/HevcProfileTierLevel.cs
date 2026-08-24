// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Contains the general HEVC profile, tier, constraint, and level description declared by a parameter set.
/// </summary>
internal sealed class HevcProfileTierLevel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HevcProfileTierLevel"/> class.
    /// </summary>
    /// <param name="reader">The parameter-set raw byte sequence payload reader.</param>
    /// <param name="maxSubLayersMinusOne">The highest declared temporal sublayer index.</param>
    /// <exception cref="InvalidImageContentException">
    /// The profile-tier-level syntax is truncated or contains nonzero reserved bits.
    /// </exception>
    public HevcProfileTierLevel(ref HevcBitReader reader, int maxSubLayersMinusOne)
    {
        DebugGuard.MustBeBetweenOrEqualTo(maxSubLayersMinusOne, 0, 6, nameof(maxSubLayersMinusOne));

        this.ProfileSpace = (byte)reader.ReadBits(2);
        this.TierFlag = reader.ReadFlag();
        this.ProfileIdc = (byte)reader.ReadBits(5);
        this.ProfileCompatibilityFlags = reader.ReadBits(32);

        // The configuration record carries these 48 bits verbatim. Preserve their exact ordering so the
        // parameter set can be checked without reinterpreting profile-specific constraint layouts.
        this.ConstraintIndicatorFlags = ((ulong)reader.ReadBits(16) << 32) | reader.ReadBits(32);
        this.LevelIdc = (byte)reader.ReadBits(8);

        Span<bool> subLayerProfilePresent = stackalloc bool[6];
        Span<bool> subLayerLevelPresent = stackalloc bool[6];
        for (int subLayer = 0; subLayer < maxSubLayersMinusOne; subLayer++)
        {
            subLayerProfilePresent[subLayer] = reader.ReadFlag();
            subLayerLevelPresent[subLayer] = reader.ReadFlag();
        }

        if (maxSubLayersMinusOne > 0)
        {
            for (int subLayer = maxSubLayersMinusOne; subLayer < 8; subLayer++)
            {
                if (reader.ReadBits(2) != 0)
                {
                    throw new InvalidImageContentException("The HEVC profile-tier-level syntax has nonzero reserved bits.");
                }
            }
        }

        for (int subLayer = 0; subLayer < maxSubLayersMinusOne; subLayer++)
        {
            if (subLayerProfilePresent[subLayer])
            {
                // A sublayer profile repeats the fixed 88-bit profile and constraint structure. It is consumed
                // for alignment but not retained because one still-image item has no temporal playback model.
                reader.ReadBits(2);
                reader.ReadFlag();
                reader.ReadBits(5);
                reader.ReadBits(32);
                reader.ReadBits(16);
                reader.ReadBits(32);
            }

            if (subLayerLevelPresent[subLayer])
            {
                reader.ReadBits(8);
            }
        }
    }

    /// <summary>
    /// Gets the namespace of the declared profile identifier.
    /// </summary>
    public byte ProfileSpace { get; }

    /// <summary>
    /// Gets a value indicating whether the high tier is declared.
    /// </summary>
    public bool TierFlag { get; }

    /// <summary>
    /// Gets the five-bit profile identifier.
    /// </summary>
    public byte ProfileIdc { get; }

    /// <summary>
    /// Gets the profile-compatibility flags.
    /// </summary>
    public uint ProfileCompatibilityFlags { get; }

    /// <summary>
    /// Gets the 48-bit profile-constraint flags.
    /// </summary>
    public ulong ConstraintIndicatorFlags { get; }

    /// <summary>
    /// Gets the eight-bit level identifier.
    /// </summary>
    public byte LevelIdc { get; }

    /// <summary>
    /// Determines whether this parameter-set description is compatible with an image item's codec-configuration
    /// property.
    /// </summary>
    /// <param name="configuration">The associated HEVC codec configuration.</param>
    /// <returns><see langword="true"/> when the general profile, tier, compatibility, and level fields match.</returns>
    public bool Matches(HevcCodecConfiguration configuration)
        => this.ProfileSpace == configuration.GeneralProfileSpace
            && this.TierFlag == configuration.GeneralTierFlag
            && this.ProfileIdc == configuration.GeneralProfileIdc
            && this.ProfileCompatibilityFlags == configuration.GeneralProfileCompatibilityFlags
            && this.LevelIdc == configuration.GeneralLevelIdc;
}
