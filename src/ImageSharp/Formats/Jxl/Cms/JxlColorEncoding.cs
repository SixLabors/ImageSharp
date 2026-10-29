// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jxl.Cms;

/// <summary>
/// Describes JPEG XL color fields and an optional decoded ICC profile.
/// </summary>
internal sealed class JxlColorEncoding : IJxlFields
{
    /// <summary>
    /// The encoded shortcut that replaces the serialized color fields with their defaults.
    /// </summary>
    private bool allDefault;

    /// <summary>
    /// Profile attached after the compressed payload is decoded. It is not part of the serialized color fields.
    /// </summary>
    private IccProfile? iccProfile;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlColorEncoding"/> class.
    /// </summary>
    public JxlColorEncoding() => JxlBundle.Init(this);

    public JxlWhitePoint WhitePoint { get; set; } = JxlWhitePoint.D65;

    public JxlPrimaries Primaries { get; set; } = JxlPrimaries.SRgb;

    /// <summary>
    /// Gets or sets the rendering intent requested by the color encoding.
    /// </summary>
    public IccRenderingIntent RenderingIntent { get; set; } = IccRenderingIntent.MediaRelativeColorimetric;

    /// <summary>
    /// Gets or sets whether white point, primaries and transfer fields describe the current encoding.
    /// </summary>
    /// <remarks>ColorSpace remains valid for an ICC-only header even when this value is false.</remarks>
    public bool HaveFields { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the codestream contains a compressed ICC payload instead of direct color fields.
    /// </summary>
    /// <remarks>This serialized flag remains set after the decoder attaches the reconstructed profile.</remarks>
    public bool NeedsIcc { get; set; }

    /// <summary>
    /// Gets or sets the ICC profile reconstructed from the compressed profile payload.
    /// </summary>
    /// <remarks>
    /// A null value means that no ICC profile is attached. Named color fields do not require a generated profile.
    /// The decoder attaches the decoded profile to the original metadata encoding before it publishes the headers.
    /// Attaching a profile invalidates the direct color fields until they are derived from that profile.
    /// This property does not change the serialized <see cref="NeedsIcc"/> flag.
    /// </remarks>
    public IccProfile? IccProfile
    {
        get => this.iccProfile;
        set
        {
            this.iccProfile = value;

            if (value is not null)
            {
                this.HaveFields = false;
                this.Cmyk = value.Header.DataColorSpace == IccColorSpaceType.Cmyk;
            }
            else
            {
                this.Cmyk = false;
            }
        }
    }

    public JxlColorSpace ColorSpace { get; set; } = JxlColorSpace.Rgb;

    /// <summary>
    /// Gets or sets whether the attached profile describes four CMYK device components.
    /// </summary>
    public bool Cmyk { get; set; }

    /// <summary>
    /// Gets or sets the transfer function, which defaults to sRGB when the header uses default color fields.
    /// </summary>
    public JxlCustomTransferFunction TransferFunction { get; set; } = new();

    public JxlCustomXy White { get; set; }

    public JxlCustomXy Red { get; set; }

    public JxlCustomXy Green { get; set; }

    public JxlCustomXy Blue { get; set; }

    public bool HasPrimaries => this.ColorSpace is not (JxlColorSpace.Gray or JxlColorSpace.Xyb);

    public int Channels => (this.ColorSpace == JxlColorSpace.Gray) ? 1 : 3;

    /// <summary>
    /// Visits the serialized color fields without reading or generating an ICC profile.
    /// </summary>
    /// <param name="visitor">The header field operation.</param>
    /// <returns>Whether all present fields use valid code points and gamma values.</returns>
    public bool Visit(JxlVisitor visitor)
    {
        if (visitor.IsReading)
        {
            // A new header describes a new encoding, even when this object is reused. Its old profile cannot
            // describe the incoming fields. Generic default/size visitors preserve this nonserialized state.
            this.iccProfile = null;
            this.Cmyk = false;
        }

        if (visitor.AllDefault(this, ref this.allDefault))
        {
            // Reset every serialized field, including dormant custom coordinates, when the shortcut is read.
            // The visitor preserves nonserialized state, including an attached profile.
            visitor.SetDefault(this);
        }
        else
        {
            bool needsIcc = this.NeedsIcc;

            if (!visitor.Boolean(false, ref needsIcc))
            {
                return false;
            }

            this.NeedsIcc = needsIcc;
            JxlColorSpace colorSpace = this.ColorSpace;

            // Color space is always present. It determines channel decoding even when an ICC payload follows.
            if (!visitor.Enum(JxlColorSpace.Rgb, ref colorSpace))
            {
                return false;
            }

            this.ColorSpace = colorSpace;

            if (visitor.Conditional(!needsIcc))
            {
                bool implicitWhitePoint = colorSpace == JxlColorSpace.Xyb;

                if (implicitWhitePoint)
                {
                    this.WhitePoint = JxlWhitePoint.D65;
                }

                if (visitor.Conditional(!implicitWhitePoint))
                {
                    JxlWhitePoint whitePoint = this.WhitePoint;

                    if (!visitor.Enum(JxlWhitePoint.D65, ref whitePoint))
                    {
                        return false;
                    }

                    this.WhitePoint = whitePoint;

                    if (visitor.Conditional(whitePoint == JxlWhitePoint.Custom))
                    {
                        // These nested fields are structs. Visit their primitive fields directly and assign the changed
                        // value back; passing them through IJxlFields would box a copy and lose the decoded coordinates.
                        JxlCustomXy white = this.White;

                        if (!white.Visit(visitor))
                        {
                            return false;
                        }

                        this.White = white;
                    }
                }

                if (visitor.Conditional(this.HasPrimaries))
                {
                    JxlPrimaries primaries = this.Primaries;

                    if (!visitor.Enum(JxlPrimaries.SRgb, ref primaries))
                    {
                        return false;
                    }

                    this.Primaries = primaries;

                    if (visitor.Conditional(primaries == JxlPrimaries.Custom))
                    {
                        JxlCustomXy red = this.Red;
                        JxlCustomXy green = this.Green;
                        JxlCustomXy blue = this.Blue;

                        if (!red.Visit(visitor) || !green.Visit(visitor) || !blue.Visit(visitor))
                        {
                            return false;
                        }

                        this.Red = red;
                        this.Green = green;
                        this.Blue = blue;
                    }
                }

                JxlCustomTransferFunction transferFunction = this.TransferFunction;

                if (!transferFunction.Visit(visitor, colorSpace))
                {
                    return false;
                }

                this.TransferFunction = transferFunction;
                IccRenderingIntent intent = this.RenderingIntent;

                if (!visitor.Enum(IccRenderingIntent.MediaRelativeColorimetric, ref intent))
                {
                    return false;
                }

                this.RenderingIntent = intent;

                // Unknown values are defined enum members, but only an ICC payload can describe their color meaning.
                if (colorSpace == JxlColorSpace.Unknown || transferFunction.IsUnknown)
                {
                    return false;
                }
            }
        }

        if (visitor.IsReading)
        {
            // ICC-only headers retain ColorSpace for reconstruction. Their other direct fields are not valid,
            // even while IccProfile is still null and the compressed payload has not yet been decoded.
            this.HaveFields = !this.NeedsIcc;
        }

        return true;
    }

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
