// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.ColorProfiles.Conversion.Icc;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using static SixLabors.ImageSharp.ColorProfiles.Icc.IccColorConverter;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Converts typed color profiles through the configured source and target ICC transforms.
/// </summary>
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

        // Model loading and packing depend on TFrom and TTo; the ICC loop depends only on the configured operator.
        // Keep them separate so Native AOT retains one bulk transform implementation for each operator.
        // One 4 KiB stack block holds sixteen full Vector512 blocks or sixty-four full Vector128 blocks.
        // Reuse it for the whole span, with several SIMD blocks per transform call and no row-sized allocation.
        // The model loader writes every component in the selected block before the transform reads it.
        // Do not clear the scratch storage again before that write.
        Unsafe.SkipInit(out InlineArray256<Vector4> storage);
        Span<Vector4> vectors = storage;

        while (!source.IsEmpty)
        {
            int length = Math.Min(source.Length, vectors.Length);
            Span<Vector4> block = vectors[..length];

            // The model APIs retain their existing component units and packing rules. The ICC loop transforms
            // this block in place, with SIMD traversal and profile stages in the same implementation.
            TFrom.ToScaledVector4(source[..length], block);
            transform.Converter.Convert(block, block);
            TTo.FromScaledVector4(block, destination[..length]);

            source = source[length..];
            destination = destination[length..];
        }
    }

    /// <summary>
    /// Checks if the profile describes a device that supports black point detection.
    /// </summary>
    /// <param name="profile">The external ICC profile.</param>
    /// <returns>Whether the profile supports black point detection.</returns>
    private static bool SupportsBlackPointCompensation(IccProfile profile)
        => profile.Header.DataColorSpace is IccColorSpaceType.Rgb or IccColorSpaceType.Gray or IccColorSpaceType.Cmyk
        && profile.Header.Class is IccProfileClass.InputDevice or IccProfileClass.DisplayDevice or IccProfileClass.OutputDevice or IccProfileClass.ColorSpace;

    /// <summary>
    /// Detects the neutral black point of an ICC device in D50 XYZ units.
    /// </summary>
    /// <param name="profile">The device profile.</param>
    /// <param name="intent">The rendering intent of the connection.</param>
    /// <param name="options">The interpolation and curve settings.</param>
    /// <param name="pcsOptions">The D50 Lab and XYZ conversion settings.</param>
    /// <param name="existingForward">The device-to-PCS transform to reuse, or null to create one.</param>
    /// <returns>The black point, or zero if the profile cannot provide it.</returns>
    private static Vector3 DetectBlackPoint(
        IccProfile profile,
        IccRenderingIntent intent,
        ColorConversionOptions options,
        ColorConversionOptions pcsOptions,
        ConversionParams? existingForward)
    {
        if (profile.Header.Version.Major >= 4
            && intent is IccRenderingIntent.Perceptual or IccRenderingIntent.Saturation)
        {
            if (!IccConverterBase.IsMatrixShaper(profile))
            {
                // ICC v4 tables use the reference-medium black point for these intents.
                return new Vector3(.00336F, .0034731F, .00287F);
            }

            // Matrix profiles use their relative colorimetric black point for these intents.
            intent = IccRenderingIntent.MediaRelativeColorimetric;
            existingForward = null;
        }

        bool cmykRoundTrip = intent == IccRenderingIntent.MediaRelativeColorimetric
            && profile.Header.Class == IccProfileClass.OutputDevice && profile.Header.DataColorSpace == IccColorSpaceType.Cmyk;

        CieLab lab;

        if (cmykRoundTrip)
        {
            if (!IccConverterBase.SupportsBlackPointIntent(profile, IccRenderingIntent.Perceptual)
                || !IccConverterBase.SupportsConversion(profile, false, IccRenderingIntent.Perceptual)
                || (existingForward is null && !IccConverterBase.SupportsConversion(profile, true, intent)))
            {
                return Vector3.Zero;
            }

            // Maximum ink can exceed the profile's ink limit. Use the perceptual reverse table to get printable black.
            // The relative forward table measures that black in the connection space.
            ConversionParams reverse = new(
                profile, false, IccRenderingIntent.Perceptual, options.IccInterpolationMethod, options.UseSymmetricIccCurves);

            ConversionParams forward = existingForward ?? new(
                profile, true, intent, options.IccInterpolationMethod, options.UseSymmetricIccCurves);

            Vector4 blackPcs = reverse.PcsType == IccColorSpaceType.CieLab ? default(CieLab).ToScaledVector4() : Vector4.Zero;

            if (reverse.Is16BitLutEntry && reverse.PcsType == IccColorSpaceType.CieLab)
            {
                // Legacy Lab tables encode L*=100 as 0xFF00.
                blackPcs *= 65280F / 65535F;
            }

            Vector4 deviceBlack = reverse.Converter.Convert(blackPcs);
            lab = DecodeBlackPointPcs(forward.Converter.Convert(deviceBlack), forward, pcsOptions);
            lab = new CieLab(MathF.Min(lab.L, 50F), 0F, 0F);
        }
        else
        {
            // The transform can fall back to AToB0. Black point detection needs a table for the selected intent or matrix and curve tags.
            if (!IccConverterBase.SupportsBlackPointIntent(profile, intent))
            {
                return Vector3.Zero;
            }

            ConversionParams forward = existingForward ?? new(profile, true, intent, options.IccInterpolationMethod, options.UseSymmetricIccCurves);
            Vector4 endpoint = profile.Header.DataColorSpace == IccColorSpaceType.Cmyk ? Vector4.One : Vector4.Zero;
            lab = DecodeBlackPointPcs(forward.Converter.Convert(endpoint), forward, pcsOptions);
            lab = new CieLab(lab.L is < 0F or > 50F ? 0F : lab.L, 0F, 0F);
        }

        return lab.ToProfileConnectingSpace(pcsOptions).ToVector3();
    }

    /// <summary>
    /// Converts encoded Lab or XYZ values to Lab.
    /// </summary>
    /// <param name="encoded">The encoded PCS value.</param>
    /// <param name="profile">The profile transform that supplies the PCS encoding.</param>
    /// <param name="options">The D50 conversion settings.</param>
    /// <returns>The output in ordinary Lab units.</returns>
    private static CieLab DecodeBlackPointPcs(Vector4 encoded, ConversionParams profile, ColorConversionOptions options)
    {
        if (profile.PcsType == IccColorSpaceType.CieLab)
        {
            return CieLab.FromScaledVector4(profile.Is16BitLutEntry ? encoded * (65535F / 65280F) : encoded);
        }

        CieXyz xyz = CieXyz.FromScaledVector4(encoded);
        return CieLab.FromProfileConnectingSpace(options, in xyz);
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
            ConversionParams source = new(sourceProfile, toPcs: true, renderingIntent, options.IccInterpolationMethod, options.UseSymmetricIccCurves);
            ConversionParams target = new(targetProfile, toPcs: false, renderingIntent, options.IccInterpolationMethod, options.UseSymmetricIccCurves);
            ColorConversionOptions pcsOptions = new()
            {
                MemoryAllocator = options.MemoryAllocator,
                SourceWhitePoint = KnownIlluminants.D50Icc,
                TargetWhitePoint = KnownIlluminants.D50Icc
            };

            bool compensateBlack = options.UseBlackPointCompensation
                && renderingIntent != IccRenderingIntent.AbsoluteColorimetric
                && target.Converter.IsTrc
                && SupportsBlackPointCompensation(sourceProfile) && SupportsBlackPointCompensation(targetProfile);

            Vector3 blackScale = Vector3.One;
            Vector3 blackOffset = Vector3.Zero;

            if (compensateBlack)
            {
                // Each XYZ scale is the target black-to-white range divided by the source range.
                // The offset keeps D50 white fixed. The connection retains both values for pixel conversion.
                Vector3 sourceBlack = DetectBlackPoint(sourceProfile, renderingIntent, options, pcsOptions, source);
                Vector3 targetBlack = DetectBlackPoint(targetProfile, renderingIntent, options, pcsOptions, null);
                Vector3 white = KnownIlluminants.D50Icc.ToVector3();
                blackScale = (targetBlack - white) / (sourceBlack - white);
                blackOffset = -white * (targetBlack - sourceBlack) / (sourceBlack - white);
            }

            PcsOperator pcs = new(source, target, pcsOptions, compensateBlack, blackScale, blackOffset);
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
        /// <param name="symmetricCurves">Whether gamma and parametric curves use the input magnitude and restore its negative sign.</param>
        public ConversionParams(
            IccProfile profile,
            bool toPcs,
            IccRenderingIntent renderingIntent,
            IccInterpolationMethod interpolationMethod,
            bool symmetricCurves)
        {
            this.profile = profile;
            this.Intent = renderingIntent;
            this.Converter = toPcs
                ? new IccDataToPcsConverter(profile, renderingIntent, interpolationMethod, symmetricCurves)
                : new IccPcsToDataConverter(profile, renderingIntent, interpolationMethod, symmetricCurves);
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
