// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;

/// <summary>
/// Converts between JPEG XL color components and working RGB.
/// </summary>
/// <remarks>
/// The caller owns the row storage. A closed generic converter owns traversal; its operator owns
/// component ordering and arithmetic. Component conversion does not apply the final output conversion.
/// ICC device rows keep image alpha separate until the paired profile transform has finished.
/// </remarks>
internal abstract partial class JxlColorConverterBase
{
    // Coding XYB connects through linear RGB. The working space owns the transfer curve in either
    // direction and retains alpha. Original device components and ICC conversion use their own color description.
    private readonly RgbWorkingSpace? linearWorkingSpace;
    private readonly ColorProfileConverterExtensionsRgbRgb.RgbConversion? rgbConversion;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlColorConverterBase"/> class for direct or ICC device components.
    /// </summary>
    protected JxlColorConverterBase()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlColorConverterBase"/> class for conversion between linear components and working RGB.
    /// </summary>
    /// <param name="linearOutputWorkingSpace">The working space whose transfer curve connects encoded RGB and linear components.</param>
    protected JxlColorConverterBase(RgbWorkingSpace linearOutputWorkingSpace) => this.linearWorkingSpace = linearOutputWorkingSpace;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlColorConverterBase"/> class for a compact RGB description.
    /// </summary>
    /// <param name="sourceWorkingSpace">The restored components' transfer curve, primaries and reference white.</param>
    /// <param name="targetWorkingSpace">The required output transfer curve, primaries and reference white.</param>
    protected JxlColorConverterBase(RgbWorkingSpace sourceWorkingSpace, RgbWorkingSpace targetWorkingSpace)
    {
        Matrix4x4 adaptation = KnownChromaticAdaptationMatrices.Bradford;
        _ = Matrix4x4.Invert(adaptation, out Matrix4x4 inverse);
        this.rgbConversion = new ColorProfileConverterExtensionsRgbRgb.RgbConversion(
            sourceWorkingSpace,
            targetWorkingSpace,
            (sourceWorkingSpace.WhitePoint, targetWorkingSpace.WhitePoint),
            adaptation,
            inverse);
    }

    /// <summary>
    /// Prepares the source color description and selects its component operation once for the frame.
    /// </summary>
    /// <param name="header">The parsed image profile, component declaration and intensity target.</param>
    /// <param name="frame">The completed frame with its component transform and validated black-channel selection.</param>
    /// <returns>The closed converter from reconstructed components to working sRGB.</returns>
    public static JxlColorConverterBase Create(JxlHeader header, JxlFrame frame)
    {
        bool restoreCodingTransform = !frame.CodingColorTransformApplied;
        if (header.UsesXybTransform && restoreCodingTransform)
        {
            // Coding XYB reconstructs linear RGB with sRGB primaries and D65. Its embedded profile describes the suggested
            // original output space, not the components consumed by this inverse transform.
            return new JxlColorConverter<XybOperator>(new XybOperator(header.XybTransform), KnownRgbWorkingSpaces.SRgb);
        }

        if (!header.HasIccProfile && header.ProfileColorType == JxlColorType.Xyb)
        {
            // Declared XYB uses scaled device samples, independently of coding XYB's custom transform.
            // A completed coding inverse leaves those device samples in the retained planes.
            // Otherwise restore optional YCbCr coding before mapping the samples to linear RGB with sRGB primaries and D65.
            return frame.Header.YCbCr && restoreCodingTransform
                ? new JxlColorConverter<XybDeviceOperator<YCbCrOperator>>(new XybDeviceOperator<YCbCrOperator>(new YCbCrOperator()), KnownRgbWorkingSpaces.SRgb)
                : new JxlColorConverter<XybDeviceOperator<RgbOperator>>(new XybDeviceOperator<RgbOperator>(default), KnownRgbWorkingSpaces.SRgb);
        }

        if (frame.BlackExtra >= 0)
        {
            // Frame initialization requires a black channel for a CMYK profile and excludes it for other models.
            // Restore the four device components here. The pixel converter owns the following ICC transform.
            return frame.Header.YCbCr && restoreCodingTransform
                ? new JxlColorConverter<YccKOperator>(new YccKOperator())
                : new JxlColorConverter<CmykOperator>(default);
        }

        RgbWorkingSpace sourceSpace = frame.OriginalWorkingSpace
            ?? (!header.HasIccProfile && header.ProfileColorType != JxlColorType.Xyb ? GetWorkingSpace(header) : KnownRgbWorkingSpaces.SRgb);

        if (!sourceSpace.Equals(KnownRgbWorkingSpaces.SRgb))
        {
            // A completed coding transform leaves original-device components. Only rows that still
            // contain coding YCbCr need that inverse before the final compact-space conversion.
            return frame.Header.YCbCr && restoreCodingTransform
                ? new JxlColorConverter<YCbCrOperator>(new YCbCrOperator(), sourceSpace, KnownRgbWorkingSpaces.SRgb)
                : new JxlColorConverter<RgbOperator>(default, sourceSpace, KnownRgbWorkingSpaces.SRgb);
        }

        return frame.Header.YCbCr && restoreCodingTransform
            ? new JxlColorConverter<YCbCrOperator>(new YCbCrOperator())
            : new JxlColorConverter<RgbOperator>(default);
    }

    /// <summary>
    /// Selects the component conversion from working sRGB to the encoded color model.
    /// </summary>
    /// <param name="colorType">The encoded color model selected for the image.</param>
    /// <param name="usesCodingXyb">Whether quantized coding XYB precedes the declared output color encoding.</param>
    /// <param name="transform">The coding XYB coefficients and intensity. Other models do not use this transform.</param>
    /// <param name="hasBlack">Whether the original color's CMYK profile requires a separate black component.</param>
    /// <param name="targetWorkingSpace">The compact original-color encoding. ICC and coding XYB use their own conversion.</param>
    /// <returns>The prepared scalar and SIMD converter. The encoder owns its row storage.</returns>
    public static JxlColorConverterBase CreateForEncoding(
        JxlColorType colorType,
        bool usesCodingXyb,
        XybTransform transform,
        bool hasBlack,
        RgbWorkingSpace targetWorkingSpace)
    {
        if (hasBlack)
        {
            return new JxlColorConverter<CmykOperator>(default);
        }

        if (usesCodingXyb)
        {
            return new JxlColorConverter<XybForwardOperator>(
                new XybForwardOperator(transform), KnownRgbWorkingSpaces.SRgb, colorType == JxlColorType.Luminance);
        }

        RgbWorkingSpace sourceWorkingSpace = KnownRgbWorkingSpaces.SRgb;
        if (!targetWorkingSpace.Equals(sourceWorkingSpace))
        {
            return colorType == JxlColorType.Luminance
                ? new JxlColorConverter<LuminanceOperator>(default, sourceWorkingSpace, targetWorkingSpace)
                : new JxlColorConverter<RgbOperator>(default, sourceWorkingSpace, targetWorkingSpace);
        }

        return colorType switch
        {
            JxlColorType.Luminance => new JxlColorConverter<LuminanceOperator>(default),
            _ => new JxlColorConverter<RgbOperator>(default)
        };
    }

    /// <summary>
    /// Prepares the RGB working space described by validated compact RGB or grayscale metadata.
    /// </summary>
    /// <param name="header">The non-ICC compact color description, with parsed transfer, primaries, white and intensity.</param>
    /// <returns>The existing working-space implementation for the declared transfer curve and coordinates.</returns>
    public static RgbWorkingSpace GetWorkingSpace(JxlHeader header)
        => GetWorkingSpace(
            header.WhitePoint, header.Primaries, header.TransferCharacteristics, header.Gamma, header.IntensityTarget, header.UsesXybTransform);

    /// <summary>
    /// Prepares the shared RGB conversion from a resolved compact color description.
    /// </summary>
    /// <param name="whitePoint">The reference white chromaticity stored in the image.</param>
    /// <param name="primaries">The RGB primary chromaticities stored in the image.</param>
    /// <param name="transferCharacteristics">The transfer identifier used when gamma is absent.</param>
    /// <param name="gamma">The optional compression exponent stored in the image.</param>
    /// <param name="intensityTarget">The encoded reference intensity in nits.</param>
    /// <param name="usesXybTransform">Whether coding XYB must reconstruct the original color encoding before composition.</param>
    /// <returns>The shared working space for both encoding and decoding.</returns>
    public static RgbWorkingSpace GetWorkingSpace(
        CieXyChromaticityCoordinates whitePoint,
        RgbPrimariesChromaticityCoordinates primaries,
        CicpTransferCharacteristics transferCharacteristics,
        Rational? gamma,
        float intensityTarget,
        bool usesXybTransform)
    {
        bool isWorkingSrgb = !gamma.HasValue
            && transferCharacteristics == CicpTransferCharacteristics.Iec61966_2_1
            && whitePoint == KnownIlluminants.D65Chromaticity
            && primaries.Equals(KnownRgbWorkingSpaces.SRgb.ChromaticityCoordinates);

        if (isWorkingSrgb)
        {
            return KnownRgbWorkingSpaces.SRgb;
        }

        // Keep parsed xy chromaticities rather than substituting rounded XYZ illuminant constants.
        // These options serve only the public xyY conversion during setup; they do not enter row traversal.
        CieXyy white = new(whitePoint.X, whitePoint.Y, 1F);
        CieXyz referenceWhite = white.ToProfileConnectingSpace(new ColorConversionOptions());
        if (gamma is Rational exponent)
        {
            // The file stores a compression exponent. The shared gamma working space expects its
            // reciprocal, converted only at that API's single-precision parameter boundary.
            float expansionGamma = (float)((double)exponent.Denominator / exponent.Numerator);
            return new GammaWorkingSpace(expansionGamma, referenceWhite, primaries);
        }

        // Header parsing admits only transfer codes 1, 8, 13, 16, 17 and 18. Keep their existing
        // working-space formulas; HLG uses the image intensity target as the display peak in nits.
        return transferCharacteristics switch
        {
            // Original-color CMS and jxl-rs apply the curve to the magnitude and retain the sign.
            // Native coding XYB instead uses the direct curve before original-space composition.
            // Keep its negative linear segment when reconstructing those retained components.
            CicpTransferCharacteristics.ItuRBt709_6 => new Rec709WorkingSpace(referenceWhite, primaries, !usesXybTransform),
            CicpTransferCharacteristics.Linear => new GammaWorkingSpace(1F, referenceWhite, primaries),
            CicpTransferCharacteristics.Iec61966_2_1 => new SRgbWorkingSpace(referenceWhite, primaries),
            CicpTransferCharacteristics.SmpteSt2084 => new PqWorkingSpace(intensityTarget, referenceWhite, primaries),
            CicpTransferCharacteristics.SmpteSt428_1 => new GammaWorkingSpace(2.6F, referenceWhite, primaries),
            _ => new HlgWorkingSpace(intensityTarget, referenceWhite, primaries)
        };
    }

    /// <summary>
    /// Prepares the original RGB encoding for composition, or linear-light RGB with sRGB primaries and D65 for a saved replacement.
    /// </summary>
    /// <param name="header">The parsed compact or ICC original color description.</param>
    /// <param name="frame">The frame that requires composition or post-transform reference saving.</param>
    /// <returns>The working space for the retained color samples.</returns>
    /// <exception cref="InvalidImageContentException">Composition requires an original encoding without a supported direct inverse.</exception>
    /// <remarks>
    /// The caller handles compact XYB through device scaling. Ordinary final output has its own converter selection.
    /// This method does not approximate ICC matrices or invert a gamut-mapping LUT before composition.
    /// </remarks>
    public static RgbWorkingSpace GetOriginalWorkingSpace(JxlHeader header, JxlFrameHeader frame)
    {
        if (!header.HasIccProfile && (header.ProfileColorType != JxlColorType.Luminance || header.WhitePoint == KnownIlluminants.D65Chromaticity))
        {
            return GetWorkingSpace(header);
        }

        if (header.HasIccProfile)
        {
            // The shared detector identifies matrix/TRC profiles that the library treats as sRGB.
            // Restore those samples with the existing sRGB inverse. This detector does not extract
            // arbitrary ICC primaries or transfer curves. Run it only when a frame needs retained
            // original samples, never on the ordinary coding-XYB output path.
            IccProfile? profile = header.IccProfile;
            if (profile is not null && profile.IsCanonicalSrgbMatrixTrc())
            {
                return KnownRgbWorkingSpaces.SRgb;
            }
        }

        if (frame.NeedsBlending)
        {
            // libjxl stage_blending.cc requires the selected encoding to equal the original encoding.
            // This path restores compact RGB, D65 grayscale and profiles identified as sRGB above.
            // Other ICC profiles need parsed component fields to select a direct inverse. A paired
            // ICC round trip can map gamut before blending, so it cannot substitute for that inverse.
            // Reject this unsupported composition instead of returning an image with its blend omitted.
            throw new InvalidImageContentException("JPEG XL frame composition cannot reconstruct the declared original color encoding.");
        }

        // Native dec_xyb.cc falls back to linear-light RGB with sRGB primaries and D65 when no direct original inverse is available.
        // dec_cache.cc can still save a full replacement in that selected encoding. A later frame
        // that requires original-space composition fails above before consuming this reference.
        RgbWorkingSpace srgb = KnownRgbWorkingSpaces.SRgb;
        return new GammaWorkingSpace(1F, srgb.WhitePoint, srgb.ChromaticityCoordinates);
    }

    /// <summary>
    /// Selects coding XYB reconstruction into the original compact RGB or grayscale encoding.
    /// </summary>
    /// <param name="header">The validated compact color description and coding XYB transform.</param>
    /// <param name="workingSpace">The original working space prepared once by the frame owner.</param>
    /// <returns>The coding inverse followed by the original transfer curve.</returns>
    /// <exception cref="InvalidImageContentException">The original RGB primaries do not produce an invertible conversion matrix.</exception>
    public static JxlColorConverterBase CreateForOriginalColor(JxlHeader header, RgbWorkingSpace workingSpace)
    {
        try
        {
            return new JxlColorConverter<XybOperator>(
                new XybOperator(header.XybTransform, workingSpace, header.ProfileColorType == JxlColorType.Luminance), workingSpace);
        }
        catch (ArgumentException exception)
        {
            // Shared inverse preparation validates its target matrix. Translate only this setup
            // failure at the codec boundary; row arithmetic does not catch or retry conversion errors.
            throw new InvalidImageContentException("The JPEG XL original RGB primaries do not produce an invertible conversion matrix.", exception);
        }
    }

    /// <summary>
    /// Selects the coding transform before pointwise image features without applying the final output conversion.
    /// </summary>
    /// <param name="header">The parsed image color declarations and coding XYB transform.</param>
    /// <param name="frame">The frame whose coding transform precedes its image features.</param>
    /// <returns>The component converter. Output transfer and profile conversion remain separate.</returns>
    public static JxlColorConverterBase CreateForImageFeatures(JxlHeader header, JxlFrame frame)
    {
        if (header.UsesXybTransform && !frame.CodingColorTransformApplied)
        {
            // Static spot colors follow the inverse coding transform in libjxl dec_cache.cc.
            // Keep its linear output here; the final transfer curve follows the spot calculation.
            return new JxlColorConverter<XybOperator>(new XybOperator(header.XybTransform));
        }

        // Inverse YCbCr restores the original device samples. CMYK remains in remaining-light units,
        // and declared device XYB keeps its sample encoding until after the spot calculation.
        return frame.Header.YCbCr && !frame.CodingColorTransformApplied
            ? new JxlColorConverter<YCbCrOperator>(new YCbCrOperator())
            : new JxlColorConverter<RgbOperator>(default);
    }

    /// <summary>
    /// Applies the selected non-ICC output conversion without repeating the row's coding transform.
    /// </summary>
    /// <param name="colors">The restored component row, converted in place. Alpha is retained.</param>
    public void ApplyOutputConversion(Span<Vector4> colors)
    {
        if (this.linearWorkingSpace is not null)
        {
            this.linearWorkingSpace.Compress(colors);
        }
        else if (this.rgbConversion.HasValue)
        {
            // Reuse the converter's prepared RGB arithmetic without constructing a general converter.
            // Transfer curves, including PQ and HLG, remain owned by their working-space implementations.
            this.rgbConversion.Value.Invoke(colors);
        }
    }

    /// <summary>
    /// Restores the factory-selected components to pixel-vector rows without applying the final output conversion.
    /// </summary>
    /// <typeparam name="TSample">The native integer carrier or restored float sample.</typeparam>
    /// <typeparam name="TSampleOperator">The prepared sample-representation operator.</typeparam>
    /// <param name="source">The source planes and separately normalized black and alpha rows.</param>
    /// <param name="destination">The restored components in X, Y and Z, with image alpha in W.</param>
    /// <param name="sampleOperator">The prepared integer or floating sample conversion.</param>
    public abstract void ConvertComponents<TSample, TSampleOperator>(
        in ComponentValues<TSample> source,
        Span<Vector4> destination,
        in TSampleOperator sampleOperator)
        where TSample : unmanaged
        where TSampleOperator : struct, JxlSampleConverter.IConversionOperator<TSample, float>;

    /// <summary>
    /// Restores native components and converts their typed ICC profile to working RGB in the same row storage.
    /// </summary>
    /// <typeparam name="TSample">The native integer carrier or restored float sample.</typeparam>
    /// <typeparam name="TSampleOperator">The prepared sample-representation operator.</typeparam>
    /// <param name="source">The native planes and normalized black row. Image alpha is not read.</param>
    /// <param name="storage">Four floats per pixel. Converted Rgb values occupy its first 3 * width floats contiguously.</param>
    /// <param name="profileConverter">The paired source-profile to sRGB transform.</param>
    /// <param name="sampleOperator">The prepared integer or floating sample conversion.</param>
    public abstract void ConvertToRgb<TSample, TSampleOperator>(
        in ComponentValues<TSample> source,
        Span<float> storage,
        ColorProfileConverter profileConverter,
        in TSampleOperator sampleOperator)
        where TSample : unmanaged
        where TSampleOperator : struct, JxlSampleConverter.IConversionOperator<TSample, float>;

    /// <summary>
    /// Converts a borrowed working sRGB row to native component planes and retains its alpha.
    /// </summary>
    /// <param name="source">The mutable converted pixel row. Its alpha values remain unchanged.</param>
    /// <param name="component0">The first encoded component row, with source.Length elements.</param>
    /// <param name="component1">The second component row, or empty for luminance.</param>
    /// <param name="component2">The third component row, or empty for luminance.</param>
    /// <param name="black">The remaining-light black row for CMYK, or empty for other color models.</param>
    /// <param name="alpha">The alpha output row, or empty when the image has no alpha channel.</param>
    /// <param name="associateAlpha">Whether to associate working RGB with alpha before profile conversion and the coding transform.</param>
    /// <param name="profileConverter">The prepared sRGB-to-device ICC transform, or null for compact RGB, luminance or coding XYB.</param>
    /// <param name="scratch">Separate ICC workspace: three floats per pixel for RGB or gray, or seven for RGB-to-CMYK expansion.</param>
    /// <remarks>
    /// The encoder allocates every row before traversal. This method retains float components until sample packing.
    /// A coding XYB profile describes original output coordinates. It does not change the working sRGB input of the coding transform.
    /// </remarks>
    public abstract void ConvertFromRgb(
        Span<Vector4> source,
        Span<float> component0,
        Span<float> component1,
        Span<float> component2,
        Span<float> black,
        Span<float> alpha,
        bool associateAlpha,
        ColorProfileConverter? profileConverter,
        Span<float> scratch);

    /// <summary>
    /// Borrows one row of reconstructed components without transferring ownership.
    /// </summary>
    /// <typeparam name="TSample">The four-byte source sample type.</typeparam>
    public readonly ref struct ComponentValues<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ComponentValues{TSample}"/> struct.
        /// </summary>
        /// <param name="component0">The first source component.</param>
        /// <param name="component1">The second source component, or the repeated luminance row.</param>
        /// <param name="component2">The third source component, or the repeated luminance row.</param>
        /// <param name="black">The normalized black row required by CMYK, or an empty span for other models.</param>
        /// <param name="alpha">The normalized alpha row, or an empty span for opaque output.</param>
        public ComponentValues(
            ReadOnlySpan<TSample> component0,
            ReadOnlySpan<TSample> component1,
            ReadOnlySpan<TSample> component2,
            ReadOnlySpan<float> black,
            ReadOnlySpan<float> alpha)
        {
            this.Component0 = component0;
            this.Component1 = component1;
            this.Component2 = component2;
            this.Black = black;
            this.Alpha = alpha;
        }

        /// <summary>
        /// Gets the first source row.
        /// </summary>
        public ReadOnlySpan<TSample> Component0 { get; }

        /// <summary>
        /// Gets the second source row.
        /// </summary>
        public ReadOnlySpan<TSample> Component1 { get; }

        /// <summary>
        /// Gets the third source row.
        /// </summary>
        public ReadOnlySpan<TSample> Component2 { get; }

        /// <summary>
        /// Gets normalized black components, separate from alpha.
        /// </summary>
        public ReadOnlySpan<float> Black { get; }

        /// <summary>
        /// Gets normalized alpha components, separate from every device channel.
        /// </summary>
        public ReadOnlySpan<float> Alpha { get; }
    }
}
