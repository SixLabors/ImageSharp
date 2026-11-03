// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;

/// <summary>
/// Converts reconstructed JPEG XL components to working sRGB before destination pixel packing.
/// </summary>
/// <remarks>
/// The pixel converter owns the row storage. A closed generic converter owns traversal; its operator owns
/// component ordering and arithmetic. Only the ICC overload uses the profile converter.
/// Alpha remains separate from device components until the color transform has finished.
/// </remarks>
internal abstract partial class JxlColorConverterBase
{
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
    /// Converts native components directly to working sRGB pixel vectors without ICC conversion.
    /// </summary>
    /// <typeparam name="TSample">The four-byte integer carrier or restored float sample.</typeparam>
    /// <typeparam name="TSampleOperator">The prepared sample-representation operator.</typeparam>
    /// <param name="source">The source planes and separately normalized black and alpha rows.</param>
    /// <param name="destination">The working sRGB pixel vectors.</param>
    /// <param name="sampleOperator">The prepared integer or floating sample conversion.</param>
    public abstract void ConvertToRgb<TSample, TSampleOperator>(
        in ComponentValues<TSample> source,
        Span<Vector4> destination,
        in TSampleOperator sampleOperator)
        where TSample : unmanaged
        where TSampleOperator : struct, JxlSampleConverter.IConversionOperator<TSample>;

    /// <summary>
    /// Restores device components and converts them to working sRGB through an ICC transform.
    /// </summary>
    /// <typeparam name="TSample">The four-byte integer carrier or restored float sample.</typeparam>
    /// <typeparam name="TSampleOperator">The prepared sample-representation operator.</typeparam>
    /// <param name="source">The source planes and separately normalized black and alpha rows.</param>
    /// <param name="destination">The working sRGB pixel vectors. ICC device storage reuses this row.</param>
    /// <param name="profileConverter">The paired source-profile to sRGB transform.</param>
    /// <param name="sampleOperator">The prepared integer or floating sample conversion.</param>
    public abstract void ConvertToRgb<TSample, TSampleOperator>(
        in ComponentValues<TSample> source,
        Span<Vector4> destination,
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
