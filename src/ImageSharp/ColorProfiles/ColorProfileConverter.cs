// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Allows the conversion of color profiles.
/// </summary>
public class ColorProfileConverter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ColorProfileConverter"/> class.
    /// </summary>
    public ColorProfileConverter()
    : this(new())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ColorProfileConverter"/> class.
    /// </summary>
    /// <param name="options">The color profile conversion options.</param>
    public ColorProfileConverter(ColorConversionOptions options)
        => this.Options = options;

    /// <summary>
    /// Gets the color profile conversion options.
    /// </summary>
    public ColorConversionOptions Options { get; }

    internal (CieXyz From, CieXyz To) GetChromaticAdaptionWhitePoints<TFrom, TTo>()
               where TFrom : struct, IColorProfile
               where TTo : struct, IColorProfile
    {
        CieXyz sourceWhitePoint = TFrom.GetChromaticAdaptionWhitePointSource() == ChromaticAdaptionWhitePointSource.WhitePoint
            ? this.Options.SourceWhitePoint
            : this.Options.SourceRgbWorkingSpace.WhitePoint;

        CieXyz targetWhitePoint = TTo.GetChromaticAdaptionWhitePointSource() == ChromaticAdaptionWhitePointSource.WhitePoint
            ? this.Options.TargetWhitePoint
            : this.Options.TargetRgbWorkingSpace.WhitePoint;

        return (sourceWhitePoint, targetWhitePoint);
    }

    internal bool ShouldUseIccProfiles()
        => this.Options.SourceIccProfile != null && this.Options.TargetIccProfile != null;
}
