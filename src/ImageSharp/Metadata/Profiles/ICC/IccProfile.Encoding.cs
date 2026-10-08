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

        // Indexed pixels contain palette indices, so profile compatibility depends on the RGB palette rather than the index values.
        // Indexed can include the Alpha flag. Ignore it here because transparency does not change the palette's RGB color space.
        PixelColorType sampleColorType = (colorType & ~PixelColorType.Alpha) == PixelColorType.Indexed ? PixelColorType.RGB : colorType;

        // Match the samples supplied to ICC, using the shared mapping for BGR, alpha and YCCK conventions.
        // Omit incompatible profiles from output without changing the original image or frame metadata.
        if (profile is null
            || !PixelColorTypeIccMapping.TryGetDataSpace(sampleColorType, out IccColorSpaceType dataSpace)
            || profile.Header.DataColorSpace != dataSpace)
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
