// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;

/// <summary>
/// Restores JPEG XL components and applies the selected conversion to working sRGB.
/// </summary>
/// <remarks>
/// The pixel converter owns the row storage. A closed generic converter owns traversal; its operator owns
/// component ordering and arithmetic. Component conversion does not apply the final output conversion.
/// ICC device rows keep image alpha separate until the paired profile transform has finished.
/// </remarks>
internal abstract partial class JxlColorConverterBase
{
    // Coding XYB produces linear sRGB. The compander owns the following transfer and result bounds.
    private readonly bool isLinearRgb;
    private readonly ColorProfileConverterExtensionsRgbRgb.RgbConversion? rgbConversion;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlColorConverterBase"/> class for direct or ICC device components.
    /// </summary>
    /// <param name="isLinearRgb">Whether output conversion compresses restored linear sRGB components.</param>
    protected JxlColorConverterBase(bool isLinearRgb) => this.isLinearRgb = isLinearRgb;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlColorConverterBase"/> class for a compact RGB description.
    /// </summary>
    /// <param name="sourceWorkingSpace">The restored components' transfer curve, primaries and reference white.</param>
    protected JxlColorConverterBase(RgbWorkingSpace sourceWorkingSpace)
    {
        Matrix4x4 adaptation = KnownChromaticAdaptationMatrices.Bradford;
        _ = Matrix4x4.Invert(adaptation, out Matrix4x4 inverse);
        RgbWorkingSpace target = KnownRgbWorkingSpaces.SRgb;
        this.rgbConversion = new ColorProfileConverterExtensionsRgbRgb.RgbConversion(
            sourceWorkingSpace,
            target,
            (sourceWorkingSpace.WhitePoint, target.WhitePoint),
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
        if (header.UsesXybTransform)
        {
            // Coding XYB reconstructs linear sRGB directly. Its embedded profile describes the suggested
            // original output space, not the components consumed by this inverse transform.
            return new JxlColorConverter<XybOperator>(new XybOperator(header.XybTransform), true);
        }

        if (!header.HasIccProfile && header.ProfileColorType == JxlColorType.Xyb)
        {
            // Declared XYB uses scaled device samples, independently of coding XYB's custom transform.
            // Restore the optional YCbCr frame coding before mapping those samples to linear sRGB.
            return frame.Header.YCbCr
                ? new JxlColorConverter<XybDeviceOperator<YCbCrOperator>>(new XybDeviceOperator<YCbCrOperator>(new YCbCrOperator()), true)
                : new JxlColorConverter<XybDeviceOperator<RgbOperator>>(new XybDeviceOperator<RgbOperator>(default), true);
        }

        if (frame.BlackExtra >= 0)
        {
            // Frame initialization requires a black channel for a CMYK profile and excludes it for other models.
            // Restore the four device components here. The pixel converter owns the following ICC transform.
            return frame.Header.YCbCr
                ? new JxlColorConverter<YccKOperator>(new YccKOperator(), false)
                : new JxlColorConverter<CmykOperator>(default, false);
        }

        RgbWorkingSpace sourceSpace = KnownRgbWorkingSpaces.SRgb;
        if (!header.HasIccProfile && header.ProfileColorType != JxlColorType.Xyb)
        {
            bool isWorkingSrgb = !header.Gamma.HasValue
                && header.TransferCharacteristics == CicpTransferCharacteristics.Iec61966_2_1
                && header.WhitePoint == KnownIlluminants.D65Chromaticity
                && header.Primaries.Equals(KnownRgbWorkingSpaces.SRgb.ChromaticityCoordinates);

            if (!isWorkingSrgb)
            {
                // Keep parsed xy chromaticities rather than substituting rounded XYZ illuminant constants.
                // The public xyY conversion retains an options parameter; these temporary setup options
                // do not escape or enter row traversal.
                CieXyy white = new(header.WhitePoint.X, header.WhitePoint.Y, 1F);
                CieXyz referenceWhite = white.ToProfileConnectingSpace(new ColorConversionOptions());
                RgbPrimariesChromaticityCoordinates primaries = header.Primaries;
                if (header.Gamma is Rational gamma)
                {
                    // The file stores a compression exponent. The shared gamma working space expects
                    // its reciprocal, converted only at that API's single-precision parameter boundary.
                    float expansionGamma = (float)((double)gamma.Denominator / gamma.Numerator);
                    sourceSpace = new GammaWorkingSpace(expansionGamma, referenceWhite, primaries);
                }
                else
                {
                    // Header parsing admits only transfer codes 1, 8, 13, 16, 17 and 18. These native
                    // curves use their established working-space implementations, not similar CICP curves
                    // with different coefficients. HLG is the only remaining validated switch case.
                    sourceSpace = header.TransferCharacteristics switch
                    {
                        CicpTransferCharacteristics.ItuRBt709_6 => new Rec709WorkingSpace(referenceWhite, primaries),
                        CicpTransferCharacteristics.Linear => new GammaWorkingSpace(1F, referenceWhite, primaries),
                        CicpTransferCharacteristics.Iec61966_2_1 => new SRgbWorkingSpace(referenceWhite, primaries),
                        CicpTransferCharacteristics.SmpteSt2084 => new PqWorkingSpace(header.IntensityTarget, referenceWhite, primaries),
                        CicpTransferCharacteristics.SmpteSt428_1 => new GammaWorkingSpace(2.6F, referenceWhite, primaries),
                        _ => new HlgWorkingSpace(header.IntensityTarget, referenceWhite, primaries)
                    };
                }
            }
        }

        if (!sourceSpace.Equals(KnownRgbWorkingSpaces.SRgb))
        {
            // A compact color description supplies transfer, primaries and white directly. Prepare the
            // RGB conversion stages once and reuse the existing working-space companders without ICC.
            return frame.Header.YCbCr
                ? new JxlColorConverter<YCbCrOperator>(new YCbCrOperator(), sourceSpace)
                : new JxlColorConverter<RgbOperator>(default, sourceSpace);
        }

        return frame.Header.YCbCr
            ? new JxlColorConverter<YCbCrOperator>(new YCbCrOperator(), false)
            : new JxlColorConverter<RgbOperator>(default, false);
    }

    /// <summary>
    /// Selects the coding transform before pointwise image features without applying the final output conversion.
    /// </summary>
    /// <param name="header">The parsed image color declarations and coding XYB transform.</param>
    /// <param name="frame">The frame whose coding transform precedes its image features.</param>
    /// <returns>The converter to linear sRGB for coding XYB, or the original device components for other coding transforms.</returns>
    public static JxlColorConverterBase CreateForImageFeatures(JxlHeader header, JxlFrame frame)
    {
        if (header.UsesXybTransform)
        {
            // Static spot colors follow the inverse coding transform in libjxl dec_cache.cc.
            // Keep its linear output here; the final transfer curve follows the spot calculation.
            return new JxlColorConverter<XybOperator>(new XybOperator(header.XybTransform), false);
        }

        // Inverse YCbCr restores the original device samples. CMYK remains in remaining-light units,
        // and declared device XYB keeps its sample encoding until after the spot calculation.
        return frame.Header.YCbCr
            ? new JxlColorConverter<YCbCrOperator>(new YCbCrOperator(), false)
            : new JxlColorConverter<RgbOperator>(default, false);
    }

    /// <summary>
    /// Applies the selected non-ICC output conversion without repeating the row's coding transform.
    /// </summary>
    /// <param name="colors">The restored component row, converted in place. Alpha is retained.</param>
    public void ApplyOutputConversion(Span<Vector4> colors)
    {
        if (this.isLinearRgb)
        {
            SRgbCompanding.Compress(colors);
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
    /// Restores the factory-selected ICC device components without applying the paired profile transform.
    /// </summary>
    /// <typeparam name="TSample">The native integer carrier or restored float sample.</typeparam>
    /// <typeparam name="TSampleOperator">The prepared sample-representation operator.</typeparam>
    /// <param name="source">The source planes and separately normalized black row. Image alpha is not read.</param>
    /// <param name="destination">The ICC device components. W holds black for CMYK, or zero for a three-component device.</param>
    /// <param name="sampleOperator">The prepared integer or floating sample conversion.</param>
    public abstract void ConvertIccComponents<TSample, TSampleOperator>(
        in ComponentValues<TSample> source,
        Span<Vector4> destination,
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
