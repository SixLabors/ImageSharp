// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;

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

    private bool useIccProfiles;

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
        this.useIccProfiles = options.SourceIccProfile != null && options.TargetIccProfile != null;

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

    /// <summary>
    /// Creates the converter for decoding. If a recovered profile cannot supply a transform,
    /// the converter uses its normal color conversion without ICC.
    /// </summary>
    /// <param name="options">The decoder's color conversion options.</param>
    /// <returns>The converter for the selected color conversion.</returns>
    internal static ColorProfileConverter CreateForDecoding(ColorConversionOptions options)
    {
        ColorProfileConverter converter = new(options);
        if (converter.useIccProfiles && options.SourceIccProfile is { WasRecovered: true } profile)
        {
            if (profile.IsUnusableForDecoding)
            {
                converter.useIccProfiles = false;
                return converter;
            }

            try
            {
                // Build the transform on the converter that will use it. Check before any pixels are written.
                _ = converter.GetIccTransform();
            }
            catch (Exception ex) when (ex is InvalidIccProfileException or ArgumentException or NotSupportedException or NotImplementedException)
            {
                converter.useIccProfiles = false;
                profile.IsUnusableForDecoding = true;
            }
        }

        return converter;
    }

    /// <summary>
    /// Creates a decoder's ICC converter if the profile can supply the required transform.
    /// </summary>
    /// <param name="options">The decoder's ICC conversion options.</param>
    /// <returns>The converter, or null when decoding must use its existing fallback.</returns>
    internal static ColorProfileConverter? CreateIccForDecoding(ColorConversionOptions options)
    {
        if (options.SourceIccProfile is { IsUnusableForDecoding: true })
        {
            return null;
        }

        ColorProfileConverter converter = CreateForDecoding(options);
        return converter.useIccProfiles ? converter : null;
    }

    /// <summary>
    /// Checks whether the converter uses an ICC profile pair.
    /// </summary>
    /// <returns>Whether ICC conversion is enabled.</returns>
    internal bool ShouldUseIccProfiles() => this.useIccProfiles;
}
