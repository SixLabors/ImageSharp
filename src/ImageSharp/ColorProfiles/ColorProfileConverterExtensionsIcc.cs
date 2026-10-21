// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles.Conversion.Icc;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using static SixLabors.ImageSharp.ColorProfiles.Icc.IccColorConverter;

namespace SixLabors.ImageSharp.ColorProfiles;

internal static class ColorProfileConverterExtensionsIcc
{
    /// <summary>
    /// Converts a color value from one ICC color profile to another using the specified color profile converter.
    /// </summary>
    /// <remarks>
    /// This method performs color conversion using ICC profiles, ensuring accurate color mapping
    /// between different color spaces. Both the source and target ICC profiles must be provided in the converter's
    /// options. The method supports perceptual adjustments when required by the profiles.
    /// </remarks>
    /// <typeparam name="TFrom">The type representing the source color profile. Must implement <see cref="IColorProfile{TFrom}"/>.</typeparam>
    /// <typeparam name="TTo">The type representing the destination color profile. Must implement <see cref="IColorProfile{TTo}"/>.</typeparam>
    /// <param name="converter">The color profile converter configured with source and target ICC profiles.</param>
    /// <param name="source">The color value to convert, defined in the source color profile.</param>
    /// <returns>
    /// A color value in the target color profile, resulting from the ICC profile-based conversion of the source value.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if either the source or target ICC profile is missing from the converter options.
    /// </exception>
    public static TTo ConvertUsingIccProfile<TFrom, TTo>(this ColorProfileConverter converter, in TFrom source)
        where TFrom : struct, IColorProfile<TFrom>
        where TTo : struct, IColorProfile<TTo>
    {
        // TODO: Validation of ICC Profiles against color profile. Is this possible?
        if (converter.Options.SourceIccProfile is null)
        {
            throw new InvalidOperationException("Source ICC profile is missing.");
        }

        if (converter.Options.TargetIccProfile is null)
        {
            throw new InvalidOperationException("Target ICC profile is missing.");
        }

        // The transforms depend only on the converter's options, so they are built once per converter.
        IccTransform transform = converter.GetIccTransform();
        return TTo.FromScaledVector4(transform.Converter.Convert(source.ToScaledVector4()));
    }

    /// <summary>
    /// Converts a span of color values from a source color profile to a destination color profile using ICC profiles.
    /// </summary>
    /// <remarks>
    /// Both source and target ICC profiles must be set.
    /// The converter selects their transform tables and applies the PCS adjustments for the profile connection.
    /// </remarks>
    /// <typeparam name="TFrom">The type representing the source color profile. Must implement <see cref="IColorProfile{TFrom}"/>.</typeparam>
    /// <typeparam name="TTo">The type representing the destination color profile. Must implement <see cref="IColorProfile{TTo}"/>.</typeparam>
    /// <param name="converter">The color profile converter that provides conversion options and ICC profiles.</param>
    /// <param name="source">
    /// A read-only span containing the source color values to convert. The values must conform to the source color
    /// profile.
    /// </param>
    /// <param name="destination">
    /// A span to receive the converted color values in the destination color profile. Must be at least as large as the
    /// source span.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the source or target ICC profile is missing from the converter options.
    /// </exception>
    public static void ConvertUsingIccProfile<TFrom, TTo>(this ColorProfileConverter converter, ReadOnlySpan<TFrom> source, Span<TTo> destination)
        where TFrom : struct, IColorProfile<TFrom>
        where TTo : struct, IColorProfile<TTo>
    {
        // TODO: Validation of ICC Profiles against color profile. Is this possible?
        if (converter.Options.SourceIccProfile is null)
        {
            throw new InvalidOperationException("Source ICC profile is missing.");
        }

        if (converter.Options.TargetIccProfile is null)
        {
            throw new InvalidOperationException("Target ICC profile is missing.");
        }

        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // The transforms depend only on the converter's options, so they are built once per converter.
        IccTransform transform = converter.GetIccTransform();

        transform.Converter.Convert(source, destination);
    }

    /// <summary>
    /// The immutable source-to-target ICC connection of a converter.
    /// </summary>
    internal sealed class IccTransform
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccTransform"/> class.
        /// </summary>
        /// <param name="options">The conversion options carrying both ICC profiles.</param>
        public IccTransform(ColorConversionOptions options)
        {
            IccProfile sourceProfile = options.SourceIccProfile!;
            IccProfile targetProfile = options.TargetIccProfile!;

            // Select one intent for both transform tables and PCS adjustment. A curve-only working profile
            // must not select a different table when the same table-based profile becomes the destination.
            IccRenderingIntent renderingIntent = IccConverterBase.GetRenderingIntent(sourceProfile, targetProfile);
            ConversionParams source = new(sourceProfile, toPcs: true, renderingIntent, options.IccInterpolationMethod);
            ConversionParams target = new(targetProfile, toPcs: false, renderingIntent, options.IccInterpolationMethod);
            ColorConversionOptions pcsOptions = new()
            {
                MemoryAllocator = options.MemoryAllocator,
                SourceWhitePoint = KnownIlluminants.D50Icc,
                TargetWhitePoint = KnownIlluminants.D50Icc
            };

            PcsOperator pcs = new(source, target, pcsOptions);
            this.Converter = source.Converter.Connect(target.Converter, pcs);
        }

        /// <summary>
        /// Gets the complete source-to-target connection.
        /// </summary>
        public IIccColorConverter Converter { get; }
    }

    /// <summary>
    /// Holds one profile's selected transform, intent and PCS encoding for connection setup.
    /// </summary>
    internal sealed class ConversionParams
    {
        private readonly IccProfile profile;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConversionParams"/> class.
        /// </summary>
        /// <param name="profile">The profile used by this transform.</param>
        /// <param name="toPcs">Whether this transform converts device values to the PCS.</param>
        /// <param name="renderingIntent">The rendering intent shared by both profiles.</param>
        /// <param name="interpolationMethod">The interpolation method used for color lookup tables.</param>
        public ConversionParams(IccProfile profile, bool toPcs, IccRenderingIntent renderingIntent, IccInterpolationMethod interpolationMethod)
        {
            this.profile = profile;
            this.Intent = renderingIntent;
            this.Converter = toPcs
                ? new IccDataToPcsConverter(profile, renderingIntent, interpolationMethod)
                : new IccPcsToDataConverter(profile, renderingIntent, interpolationMethod);
        }

        /// <summary>
        /// Gets the selected device-to-PCS or PCS-to-device transform.
        /// </summary>
        public IccConverterBase Converter { get; }

        /// <summary>
        /// Gets the profile header that defines the data and PCS encodings.
        /// </summary>
        public IccProfileHeader Header => this.profile.Header;

        /// <summary>
        /// Gets the rendering intent selected for this profile connection.
        /// </summary>
        public IccRenderingIntent Intent { get; }

        /// <summary>
        /// Gets the profile connection space declared by the profile header.
        /// </summary>
        public IccColorSpaceType PcsType => this.Header.ProfileConnectionSpace;

        /// <summary>
        /// Gets the ICC version declared by the profile header.
        /// </summary>
        public IccVersion Version => this.Header.Version;

        /// <summary>
        /// Gets a value indicating whether a v2 perceptual table uses the legacy black point. Curve-only profiles have no perceptual table;
        /// treating them as v2 tables can omit the source adjustment while still applying its inverse at the target.
        /// </summary>
        public bool HasV2PerceptualHandling => this.Intent == IccRenderingIntent.Perceptual && this.Version.Major == 2 && !this.Converter.IsTrc;

        /// <summary>
        /// Gets a value indicating whether perceptual intent uses channel curves without a perceptual lookup table.
        /// </summary>
        public bool HasNoPerceptualHandling => this.Intent == IccRenderingIntent.Perceptual && this.Converter.IsTrc;

        /// <summary>
        /// Gets a value indicating whether the selected transform uses the legacy 16-bit LUT encoding.
        /// </summary>
        public bool Is16BitLutEntry => this.Converter.Is16BitLutEntry;
    }
}
