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

        // Preserve the coordinates used by libjxl color_encoding_cms.h. Its sRGB coordinates differ slightly
        // from the ideal standard coordinates, so replacing them with a working-space preset would change this conversion.
        primaries = this.Primaries switch
        {
            JxlPrimaries.Custom => new(this.Red.GetValue(), this.Green.GetValue(), this.Blue.GetValue()),
            JxlPrimaries.SRgb => new(new(0.639998686F, 0.330010138F), new(0.300003784F, 0.600003357F), new(0.150002046F, 0.059997204F)),
            JxlPrimaries.Bt2020 => new(new(0.708F, 0.292F), new(0.170F, 0.797F), new(0.131F, 0.046F)),
            JxlPrimaries.P3 => new(new(0.680F, 0.320F), new(0.265F, 0.690F), new(0.150F, 0.060F)),
            _ => throw new InvalidOperationException("Invalid primaries: " + this.Primaries)
        };

        return true;
    }
}
