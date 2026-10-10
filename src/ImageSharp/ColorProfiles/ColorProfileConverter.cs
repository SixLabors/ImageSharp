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
    private ColorProfileConverterExtensionsIcc.IccTransform? iccTransform;

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
        => this.Options = options;

    /// <summary>
    /// Gets the color profile conversion options.
    /// </summary>
    public ColorConversionOptions Options { get; }

    /// <summary>
    /// Gets the ICC transforms of this converter, building them on first use.
    /// </summary>
    /// <returns>The <see cref="ColorProfileConverterExtensionsIcc.IccTransform"/>.</returns>
    internal ColorProfileConverterExtensionsIcc.IccTransform GetIccTransform()
        => LazyInitializer.EnsureInitialized(ref this.iccTransform, () => new ColorProfileConverterExtensionsIcc.IccTransform(this.Options));

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
