// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <content>
/// Selects compatible encoding profiles and conversion from sRGB working pixels.
/// </content>
public sealed partial class IccProfile
{
    /// <summary>
    /// Gets the retained profile whose data color space matches the encoded color type.
    /// </summary>
    /// <param name="profile">The retained image or frame profile, or null.</param>
    /// <param name="colorType">The encoded color type. Indexed output checks the RGB palette.</param>
    /// <returns>The supplied profile when compatible; otherwise, null. No converter is allocated.</returns>
    internal static IccProfile? GetIccProfileForEncoding(IccProfile? profile, PixelColorType colorType)
    {
        // Indices select palette entries; they are not color components. Alpha does not change the palette's RGB color space.
        PixelColorType sampleColorType = (colorType & ~PixelColorType.Alpha) == PixelColorType.Indexed ? PixelColorType.RGB : colorType;

        // Use the same mapping for alpha, BGR and YCCK as the typed color converters.
        // A mismatch omits the profile without changing source metadata or creating a replacement profile.
        return profile is not null
            && PixelColorTypeIccMapping.TryGetDataSpace(sampleColorType, out IccColorSpaceType dataSpace)
            && profile.Header.DataColorSpace == dataSpace
                ? profile
                : null;
    }

    /// <summary>
    /// Gets the original profile compatible with the output samples and the converter from sRGB to that profile.
    /// </summary>
    /// <param name="profile">The original profile selected by the encoder from image or frame metadata.</param>
    /// <param name="colorType">The color type at the ICC boundary, or indexed output with an RGB palette.</param>
    /// <param name="memoryAllocator">The allocator used for color conversion.</param>
    /// <param name="converter">The output converter, or <see langword="null"/> when no conversion is required.</param>
    /// <returns>The original profile when compatible; otherwise, <see langword="null"/>.</returns>
    internal static IccProfile? GetIccProfileForEncoding(
        IccProfile? profile, PixelColorType colorType, MemoryAllocator memoryAllocator, out ColorProfileConverter? converter)
    {
        converter = null;

        profile = GetIccProfileForEncoding(profile, colorType);
        if (profile is null)
        {
            return null;
        }

        // Identity conversion still retains the original profile for embedding.
        if (TryGetIccProfileForColorConversion(profile, out _))
        {
            ColorConversionOptions options = new()
            {
                SourceIccProfile = CompactSrgbV4Profile.Profile,
                TargetIccProfile = profile,
                MemoryAllocator = memoryAllocator,
            };

            converter = new ColorProfileConverter(options);
        }

        return profile;
    }
}
