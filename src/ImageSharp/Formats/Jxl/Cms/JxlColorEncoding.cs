// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Cms;

internal sealed class JxlColorEncoding
{
    public JxlWhitePoint WhitePoint { get; set; } = JxlWhitePoint.D65;

    public JxlPrimaries Primaries { get; set; } = JxlPrimaries.SRgb;

    public JxlRenderingIntent RenderingIntent { get; set; } = JxlRenderingIntent.Relative;

    public bool HaveFields { get; set; } = true;

    public JxlIccBytes? Icc { get; set; }

    public JxlColorSpace ColorSpace { get; set; } = JxlColorSpace.Rgb;

    public bool Cmyk { get; set; }

    public JxlCustomTransferFunction TransferFunction { get; set; }

    public JxlCustomXy White { get; set; }

    public JxlCustomXy Red { get; set; }

    public JxlCustomXy Green { get; set; }

    public JxlCustomXy Blue { get; set; }

    public bool HasPrimaries => this.ColorSpace is not (JxlColorSpace.Gray or JxlColorSpace.Xyb);

    public int Channels => (this.ColorSpace == JxlColorSpace.Gray) ? 1 : 3;

    public bool TryGetPrimaries(out JxlCieXyPrimaries xy)
    {
        xy = default;

        if (!this.HasPrimaries || !this.HasPrimaries)
        {
            return false;
        }

        switch (this.Primaries)
        {
            case JxlPrimaries.Custom:
                xy.R = this.Red.GetValue();
                xy.G = this.Green.GetValue();
                xy.B = this.Blue.GetValue();
                break;

            case JxlPrimaries.SRgb:
                xy.R = new(0.639998686f, 0.330010138f);
                xy.G = new(0.300003784f, 0.600003357f);
                xy.B = new(0.150002046f, 0.059997204f);
                break;

            case JxlPrimaries.Bt2020:
                xy.R = new(0.708f, 0.292f);
                xy.G = new(0.170f, 0.797f);
                xy.B = new(0.131f, 0.046f);
                break;

            case JxlPrimaries.P3:
                xy.R = new(0.680f, 0.320f);
                xy.G = new(0.265f, 0.690f);
                xy.B = new(0.150f, 0.060f);
                break;

            default:
                throw new InvalidOperationException("Invalid primaries: " + this.Primaries);
        }

        return true;
    }
}
