// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Maps named pixel color types to their associated ICC data spaces.
/// </summary>
internal static class PixelColorTypeIccMapping
{
    /// <summary>
    /// Gets the ICC data space associated with the given color type.
    /// </summary>
    /// <param name="colorType">The pixel color type.</param>
    /// <param name="dataSpace">The corresponding ICC data space.</param>
    /// <returns>Whether the color type names an ICC data space.</returns>
    public static bool TryGetDataSpace(PixelColorType colorType, out IccColorSpaceType dataSpace)
    {
        // Alpha is carried separately from the ICC color channels.
        switch (colorType & ~PixelColorType.Alpha)
        {
            case PixelColorType.RGB:
            case PixelColorType.BGR:
                dataSpace = IccColorSpaceType.Rgb;
                return true;
            case PixelColorType.Luminance:
            case PixelColorType.Binary:
                dataSpace = IccColorSpaceType.Gray;
                return true;
            case PixelColorType.YCbCr:
                dataSpace = IccColorSpaceType.YCbCr;
                return true;
            case PixelColorType.CMYK:
            case PixelColorType.YCCK:
                dataSpace = IccColorSpaceType.Cmyk;
                return true;
            case PixelColorType.CMY:
                dataSpace = IccColorSpaceType.Cmy;
                return true;
            case PixelColorType.XYZ:
                dataSpace = IccColorSpaceType.CieXyz;
                return true;
            case PixelColorType.Lab:
                dataSpace = IccColorSpaceType.CieLab;
                return true;
            case PixelColorType.Luv:
                dataSpace = IccColorSpaceType.CieLuv;
                return true;
            case PixelColorType.Yxy:
                dataSpace = IccColorSpaceType.CieYxy;
                return true;
            case PixelColorType.HSV:
                dataSpace = IccColorSpaceType.Hsv;
                return true;
            case PixelColorType.HLS:
                dataSpace = IccColorSpaceType.Hls;
                return true;
            default:
                dataSpace = default;
                return false;
        }
    }

    /// <summary>
    /// Gets a named pixel color type for an ICC data space.
    /// </summary>
    /// <param name="dataSpace">The ICC data space.</param>
    /// <param name="colorType">The corresponding named pixel color type.</param>
    /// <returns>Whether the ICC data space has a named pixel color type.</returns>
    public static bool TryGetColorType(IccColorSpaceType dataSpace, out PixelColorType colorType)
    {
        colorType = dataSpace switch
        {
            IccColorSpaceType.Rgb => PixelColorType.RGB,
            IccColorSpaceType.Gray => PixelColorType.Luminance,
            IccColorSpaceType.YCbCr => PixelColorType.YCbCr,
            IccColorSpaceType.Cmyk => PixelColorType.CMYK,
            IccColorSpaceType.Cmy => PixelColorType.CMY,
            IccColorSpaceType.CieXyz => PixelColorType.XYZ,
            IccColorSpaceType.CieLab => PixelColorType.Lab,
            IccColorSpaceType.CieLuv => PixelColorType.Luv,
            IccColorSpaceType.CieYxy => PixelColorType.Yxy,
            IccColorSpaceType.Hsv => PixelColorType.HSV,
            IccColorSpaceType.Hls => PixelColorType.HLS,
            _ => PixelColorType.None
        };

        // Generic multichannel spaces have no named pixel color type.
        return colorType != PixelColorType.None;
    }
}
