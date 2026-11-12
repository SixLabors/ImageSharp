// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;

namespace SixLabors.ImageSharp.Formats.Exr;

/// <summary>
/// Converts pixels between the EXR RGB working space and sRGB through the typed pixel conversion API.
/// </summary>
internal static class ExrColorConverter
{
    /// <summary>
    /// Creates the working-space converter for one EXR encoding or decoding operation.
    /// </summary>
    /// <param name="metadata">The primaries and white point of the linear EXR samples.</param>
    /// <param name="encoding">Whether conversion writes linear EXR samples instead of sRGB working pixels.</param>
    /// <returns>The converter with the EXR and sRGB working spaces in the requested order.</returns>
    public static ColorProfileConverter Create(ExrMetadata metadata, bool encoding)
    {
        // Use the established D65 reference when the chromaticities specify D65. This keeps
        // the same white-point representation on both sides of a transfer-curve-only conversion.
        CieXyz referenceWhite = metadata.WhitePoint == KnownIlluminants.D65Chromaticity
            ? KnownIlluminants.D65
            : new CieXyy(metadata.WhitePoint.X, metadata.WhitePoint.Y, 1F).ToProfileConnectingSpace(new ColorConversionOptions());

        GammaWorkingSpace linear = new(1F, referenceWhite, metadata.Primaries);
        return new ColorProfileConverter(new ColorConversionOptions
        {
            SourceRgbWorkingSpace = encoding ? KnownRgbWorkingSpaces.SRgb : linear,
            TargetRgbWorkingSpace = encoding ? linear : KnownRgbWorkingSpaces.SRgb
        });
    }
}
