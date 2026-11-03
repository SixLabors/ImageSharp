// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Allows the conversion of color profiles.
/// </summary>
public class ColorProfileConverter
{
    /// <summary>
    /// The ICC transforms, built on first use. They depend only on the immutable options and hold no mutable state,
    /// so every conversion and every thread shares them.
    /// </summary>
    private readonly Lazy<ColorProfileConverterExtensionsIcc.IccTransform> iccTransform;

    /// <summary>
    /// Initializes a new instance of the <see cref="ColorProfileConverter"/> class.
    /// </summary>
    public ColorProfileConverter()
        : this(new ColorConversionOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ColorProfileConverter"/> class.
    /// </summary>
    /// <param name="options">The color profile conversion options.</param>
    public ColorProfileConverter(ColorConversionOptions options)
    {
        this.Options = options;

        // Retain the factory once instead of allocating it for each converted row.
        // PublicationOnly publishes one transform and permits retry after a failed construction, as before.
        this.iccTransform = new(() => new ColorProfileConverterExtensionsIcc.IccTransform(this.Options), LazyThreadSafetyMode.PublicationOnly);
    }

    /// <summary>
    /// Gets the color profile conversion options.
    /// </summary>
    public ColorConversionOptions Options { get; }

    /// <summary>
    /// Gets the ICC transforms of this converter, building them on first use.
    /// </summary>
    /// <returns>The <see cref="ColorProfileConverterExtensionsIcc.IccTransform"/>.</returns>
    internal ColorProfileConverterExtensionsIcc.IccTransform GetIccTransform()
        => this.iccTransform.Value;

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
