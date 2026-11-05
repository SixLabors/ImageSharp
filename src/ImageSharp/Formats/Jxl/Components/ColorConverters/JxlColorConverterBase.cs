// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;

/// <summary>
/// Restores JPEG XL color components and applies the selected output conversion.
/// </summary>
/// <remarks>
/// The pixel converter owns the row storage. A closed generic converter owns traversal; its operator owns
/// component ordering and arithmetic. Component conversion does not apply the final output conversion.
/// ICC device rows keep image alpha separate until the paired profile transform has finished.
/// </remarks>
internal abstract partial class JxlColorConverterBase
{
    // Coding XYB can reconstruct linear RGB in the original compact space. That space owns its
    // transfer curve and result bounds; device samples and ICC inputs need no transfer here.
    private readonly RgbWorkingSpace? linearOutputWorkingSpace;
    private readonly ColorProfileConverterExtensionsRgbRgb.RgbConversion? rgbConversion;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlColorConverterBase"/> class for direct or ICC device components.
    /// </summary>
    protected JxlColorConverterBase()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlColorConverterBase"/> class for linear RGB output.
    /// </summary>
    /// <param name="linearOutputWorkingSpace">The working space whose transfer curve encodes the restored linear components.</param>
    protected JxlColorConverterBase(RgbWorkingSpace linearOutputWorkingSpace) => this.linearOutputWorkingSpace = linearOutputWorkingSpace;

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
            // Coding XYB reconstructs linear sRGB directly. Its embedded profile describes the suggested
            // original output space, not the components consumed by this inverse transform.
            return new JxlColorConverter<XybOperator>(new XybOperator(header.XybTransform), KnownRgbWorkingSpaces.SRgb);
        }

        if (!header.HasIccProfile && !header.UsesXybTransform && header.ProfileColorType == JxlColorType.Xyb)
        {
            // Declared XYB uses scaled device samples, independently of coding XYB's custom transform.
            // Restore the optional YCbCr frame coding before mapping those samples to linear sRGB.
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
    /// Prepares the RGB working space described by validated compact RGB or grayscale metadata.
    /// </summary>
    /// <param name="header">The non-ICC compact color description, with parsed transfer, primaries, white and intensity.</param>
    /// <returns>The existing working-space implementation for the declared transfer curve and coordinates.</returns>
    public static RgbWorkingSpace GetWorkingSpace(JxlHeader header)
    {
        bool isWorkingSrgb = !header.Gamma.HasValue
            && header.TransferCharacteristics == CicpTransferCharacteristics.Iec61966_2_1
            && header.WhitePoint == KnownIlluminants.D65Chromaticity
            && header.Primaries.Equals(KnownRgbWorkingSpaces.SRgb.ChromaticityCoordinates);

        if (isWorkingSrgb)
        {
            return KnownRgbWorkingSpaces.SRgb;
        }

        // Keep parsed xy chromaticities rather than substituting rounded XYZ illuminant constants.
        // These options serve only the public xyY conversion during setup; they do not enter row traversal.
        CieXyy white = new(header.WhitePoint.X, header.WhitePoint.Y, 1F);
        CieXyz referenceWhite = white.ToProfileConnectingSpace(new ColorConversionOptions());
        RgbPrimariesChromaticityCoordinates primaries = header.Primaries;
        if (header.Gamma is Rational gamma)
        {
            // The file stores a compression exponent. The shared gamma working space expects its
            // reciprocal, converted only at that API's single-precision parameter boundary.
            float expansionGamma = (float)((double)gamma.Denominator / gamma.Numerator);
            return new GammaWorkingSpace(expansionGamma, referenceWhite, primaries);
        }

        // Header parsing admits only transfer codes 1, 8, 13, 16, 17 and 18. Keep their existing
        // working-space formulas; HLG uses the image intensity target as the display peak in nits.
        return header.TransferCharacteristics switch
        {
            CicpTransferCharacteristics.ItuRBt709_6 => new Rec709WorkingSpace(referenceWhite, primaries),
            CicpTransferCharacteristics.Linear => new GammaWorkingSpace(1F, referenceWhite, primaries),
            CicpTransferCharacteristics.Iec61966_2_1 => new SRgbWorkingSpace(referenceWhite, primaries),
            CicpTransferCharacteristics.SmpteSt2084 => new PqWorkingSpace(header.IntensityTarget, referenceWhite, primaries),
            CicpTransferCharacteristics.SmpteSt428_1 => new GammaWorkingSpace(2.6F, referenceWhite, primaries),
            _ => new HlgWorkingSpace(header.IntensityTarget, referenceWhite, primaries)
        };
    }

    /// <summary>
    /// Determines whether the component operators can restore the original encoding for composition or reference saving.
    /// </summary>
    /// <param name="header">The parsed coding transform and original color description.</param>
    /// <returns>Whether device reconstruction or direct compact RGB or D65 grayscale reconstruction is available.</returns>
    /// <remarks>
    /// Coding XYB uses a direct inverse for compact RGB and D65 grayscale. An ICC-only or declared XYB
    /// output description does not supply that direct RGB conversion. Ordinary final output has its own converter selection.
    /// </remarks>
    public static bool CanRestoreOriginalColor(JxlHeader header)
        => !header.UsesXybTransform
            || (!header.HasIccProfile && (header.ProfileColorType == JxlColorType.Rgb
                || (header.ProfileColorType == JxlColorType.Luminance && header.WhitePoint == KnownIlluminants.D65Chromaticity)));

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
        if (this.linearOutputWorkingSpace is not null)
        {
            this.linearOutputWorkingSpace.Compress(colors);
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
        where TSampleOperator : struct, JxlSampleConverter.IConversionOperator<TSample>;

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
        where TSampleOperator : struct, JxlSampleConverter.IConversionOperator<TSample>;

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
