// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;

internal abstract partial class JxlColorConverterBase
{
    /// <summary>
    /// Prepares the source color description and selects its component operation once for the frame.
    /// </summary>
    /// <param name="header">The parsed image profile, component declaration and intensity target.</param>
    /// <param name="frame">The frame's RGB or YCbCr component transform.</param>
    /// <param name="allocator">The decoder's allocator for shared profile conversion.</param>
    /// <returns>The closed converter from reconstructed components to working sRGB.</returns>
    public static JxlColorConverterBase Create(JxlHeader header, JxlFrameHeader frame, MemoryAllocator allocator)
    {
        IccProfile? sourceProfile = null;
        if (!header.UsesXybTransform)
        {
            // Native components require their embedded profile even when ancillary metadata is skipped.
            // XYB instead reconstructs sRGB; its embedded ICC describes the suggested original output space.
            IccProfile.TryGetIccProfileForColorConversion(header.IccProfile, out sourceProfile);
        }

        RgbWorkingSpace sourceSpace = KnownRgbWorkingSpaces.SRgb;
        if (!header.UsesXybTransform && !header.HasIccProfile)
        {
            bool isWorkingSrgb = !header.Gamma.HasValue
                && header.TransferCharacteristics == CicpTransferCharacteristics.Iec61966_2_1
                && header.WhitePoint == KnownIlluminants.D65Chromaticity
                && header.Primaries.Equals(KnownRgbWorkingSpaces.SRgb.ChromaticityCoordinates);

            if (!isWorkingSrgb)
            {
                // Keep parsed xy chromaticities rather than substituting rounded XYZ illuminant constants.
                // The public xyY conversion retains an options parameter; these temporary setup options
                // do not escape or enter row traversal. The retained conversion options are prepared below.
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

        YCbCrTransform standard = KnownYCbCrMatrices.BT601;
        ColorConversionOptions options = new()
        {
            MemoryAllocator = allocator,
            SourceIccProfile = sourceProfile,
            TargetIccProfile = sourceProfile is null ? null : CompactSrgbV4Profile.Profile,
            SourceRgbWorkingSpace = sourceSpace,
            TargetRgbWorkingSpace = KnownRgbWorkingSpaces.SRgb,
            XybTransform = header.XybTransform,
            YCbCrTransform = frame.YCbCr
                ? new YCbCrTransform(standard.Forward, standard.Inverse, new Vector3(-128F / 255F, 0F, 0F))
                : standard
        };

        ColorProfileConverter converter = new(options);
        if (header.UsesXybTransform)
        {
            return new JxlColorConverter<XybOperator>(converter);
        }

        if (frame.YCbCr)
        {
            // YCbCr restoration precedes ICC. CMYK also consumes the separately selected black channel.
            return sourceProfile?.Header.DataColorSpace == IccColorSpaceType.Cmyk
                ? new JxlColorConverter<YccKOperator>(converter)
                : new JxlColorConverter<YCbCrOperator>(converter);
        }

        if (sourceProfile is not null)
        {
            // ICC owns all normalized device models. Only CMYK needs the format's inverted four-component mapping.
            return sourceProfile.Header.DataColorSpace == IccColorSpaceType.Cmyk
                ? new JxlColorConverter<CmykOperator>(converter)
                : new JxlColorConverter<IccOperator>(converter);
        }

        return sourceSpace.Equals(KnownRgbWorkingSpaces.SRgb)
            ? new JxlColorConverter<RgbOperator>(converter)
            : new JxlColorConverter<RgbWorkingSpaceOperator>(converter);
    }
}
