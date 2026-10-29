// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jxl.Cms;

internal sealed class JxlColorEncoding
{
    public JxlWhitePoint WhitePoint { get; set; } = JxlWhitePoint.D65;

    public JxlPrimaries Primaries { get; set; } = JxlPrimaries.SRgb;

    /// <summary>
    /// Gets or sets the rendering intent requested by the color encoding.
    /// </summary>
    public IccRenderingIntent RenderingIntent { get; set; } = IccRenderingIntent.MediaRelativeColorimetric;

    public bool HaveFields { get; set; } = true;

    /// <summary>
    /// Gets or sets the ICC profile reconstructed from the compressed profile payload.
    /// </summary>
    /// <remarks>The profile contains decoded ICC data. Compressed JPEG XL profile bytes do not belong here.</remarks>
    public IccProfile? IccProfile { get; set; }

    public JxlColorSpace ColorSpace { get; set; } = JxlColorSpace.Rgb;

    public bool Cmyk { get; set; }

    public JxlCustomTransferFunction TransferFunction { get; set; }

    public JxlCustomXy White { get; set; }

    public JxlCustomXy Red { get; set; }

    public JxlCustomXy Green { get; set; }

    public JxlCustomXy Blue { get; set; }

    public bool HasPrimaries => this.ColorSpace is not (JxlColorSpace.Gray or JxlColorSpace.Xyb);

    public int Channels => (this.ColorSpace == JxlColorSpace.Gray) ? 1 : 3;

    /// <summary>
    /// Gets the red, green, and blue primary chromaticities from the decoded color-encoding fields.
    /// </summary>
    /// <param name="primaries">The primary chromaticities, or the default value when no primary fields are available.</param>
    /// <returns>Whether decoded color fields are available and the color space supplies RGB primaries.</returns>
    public bool TryGetPrimaries(out RgbPrimariesChromaticityCoordinates primaries)
    {
        primaries = default;

        // An ICC-only encoding has no decoded primary fields. Gray and XYB do not supply RGB primary fields either.
        if (!this.HaveFields || !this.HasPrimaries)
        {
            return false;
        }

        // JPEG XL signals primaries, white point, and transfer function separately. Read only the primary coordinates
        // from the known working spaces. Their white points and transfer functions do not replace the encoded fields.
        primaries = this.Primaries switch
        {
            JxlPrimaries.Custom => new(this.Red.GetValue(), this.Green.GetValue(), this.Blue.GetValue()),
            JxlPrimaries.SRgb => KnownRgbWorkingSpaces.SRgb.ChromaticityCoordinates,
            JxlPrimaries.Bt2020 => KnownRgbWorkingSpaces.Rec2020.ChromaticityCoordinates,
            JxlPrimaries.P3 => KnownRgbWorkingSpaces.DisplayP3.ChromaticityCoordinates,
            _ => throw new InvalidOperationException("Invalid primaries: " + this.Primaries)
        };

        return true;
    }
}
