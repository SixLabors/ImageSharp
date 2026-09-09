// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Parses the image properties shared by HEIF items and image-sequence sample entries.
/// </summary>
internal static class HeifPropertyParser
{
    /// <summary>
    /// Parses relative horizontal and vertical pixel spacing.
    /// </summary>
    /// <param name="data">The complete pixel-aspect-ratio payload.</param>
    /// <returns>The validated relative pixel spacing.</returns>
    public static HeifPixelAspectRatio ParsePixelAspectRatio(ReadOnlySpan<byte> data)
    {
        EnsureExactLength(data, 8, "pixel aspect ratio");
        uint horizontalSpacing = BinaryPrimitives.ReadUInt32BigEndian(data);
        uint verticalSpacing = BinaryPrimitives.ReadUInt32BigEndian(data[4..]);
        if (horizontalSpacing == 0 || verticalSpacing == 0)
        {
            throw new InvalidImageContentException("The pixel aspect ratio property has zero spacing.");
        }

        return new HeifPixelAspectRatio(horizontalSpacing, verticalSpacing);
    }

    /// <summary>
    /// Parses and validates an embedded ICC profile.
    /// </summary>
    /// <param name="data">The complete ICC profile bytes.</param>
    /// <returns>The validated ICC profile.</returns>
    public static IccProfile ParseIccProfile(byte[] data)
    {
        if (data.Length == 0)
        {
            throw new InvalidImageContentException("The HEIF ICC color property contains an empty profile.");
        }

        // The HEIF parser allocates this exact array as the profile's final storage, so IccProfile can adopt it without
        // copying the potentially large profile payload.
        IccProfile profile = new(data);
        if (!profile.CheckIsValid())
        {
            throw new InvalidIccProfileException("Invalid HEIF ICC profile.");
        }

        return profile;
    }

    /// <summary>
    /// Parses a CICP color description from an <c>nclx</c> color-information payload.
    /// </summary>
    /// <param name="data">The complete payload following the <c>nclx</c> color type.</param>
    /// <returns>The CICP color description.</returns>
    public static CicpProfile ParseCicpProfile(ReadOnlySpan<byte> data)
    {
        EnsureExactLength(data, 7, "CICP color information");
        ushort colorPrimaries = BinaryPrimitives.ReadUInt16BigEndian(data);
        ushort transferCharacteristics = BinaryPrimitives.ReadUInt16BigEndian(data[2..]);
        ushort matrixCoefficients = BinaryPrimitives.ReadUInt16BigEndian(data[4..]);
        byte rangeAndReserved = data[6];
        if ((rangeAndReserved & 0x7F) != 0)
        {
            throw new InvalidImageContentException("The HEIF CICP color property has nonzero reserved bits.");
        }

        // The box fields are 16-bit so future registrations remain representable. ImageSharp's CICP profile exposes
        // the currently registered byte-sized H.273 values and maps larger future values to unspecified.
        byte colorPrimariesValue = colorPrimaries <= byte.MaxValue ? (byte)colorPrimaries : (byte)CicpColorPrimaries.Unspecified;
        byte transferCharacteristicsValue = transferCharacteristics <= byte.MaxValue
            ? (byte)transferCharacteristics
            : (byte)CicpTransferCharacteristics.Unspecified;

        byte matrixCoefficientsValue = matrixCoefficients <= byte.MaxValue
            ? (byte)matrixCoefficients
            : (byte)CicpMatrixCoefficients.Unspecified;

        return new CicpProfile(colorPrimariesValue, transferCharacteristicsValue, matrixCoefficientsValue, (rangeAndReserved & 0x80) != 0);
    }

    /// <summary>
    /// Parses content light-level information.
    /// </summary>
    /// <param name="data">The complete content-light-level payload.</param>
    /// <returns>The content light-level information.</returns>
    public static HeifContentLightLevel ParseContentLightLevel(ReadOnlySpan<byte> data)
    {
        EnsureExactLength(data, 4, "content light level");
        return new HeifContentLightLevel(BinaryPrimitives.ReadUInt16BigEndian(data), BinaryPrimitives.ReadUInt16BigEndian(data[2..]));
    }

    /// <summary>
    /// Parses mastering-display color-volume information.
    /// </summary>
    /// <param name="data">The complete mastering-display color-volume payload.</param>
    /// <returns>The mastering-display color-volume information.</returns>
    public static HeifMasteringDisplayColorVolume ParseMasteringDisplayColorVolume(ReadOnlySpan<byte> data)
    {
        EnsureExactLength(data, 24, "mastering display color volume");
        const float chromaticityScale = 1F / 50000F;
        const double luminanceScale = 1D / 10000D;

        // The registered payload inherits the G, B, R primary order used by its mastering-display source syntax.
        // Reorder it into ImageSharp's existing RGB coordinate type at the shared container boundary.
        CieXyChromaticityCoordinates greenPrimary = new(
            BinaryPrimitives.ReadUInt16BigEndian(data) * chromaticityScale,
            BinaryPrimitives.ReadUInt16BigEndian(data[2..]) * chromaticityScale);

        CieXyChromaticityCoordinates bluePrimary = new(
            BinaryPrimitives.ReadUInt16BigEndian(data[4..]) * chromaticityScale,
            BinaryPrimitives.ReadUInt16BigEndian(data[6..]) * chromaticityScale);

        CieXyChromaticityCoordinates redPrimary = new(
            BinaryPrimitives.ReadUInt16BigEndian(data[8..]) * chromaticityScale,
            BinaryPrimitives.ReadUInt16BigEndian(data[10..]) * chromaticityScale);

        return new HeifMasteringDisplayColorVolume(
            new RgbPrimariesChromaticityCoordinates(redPrimary, greenPrimary, bluePrimary),
            new CieXyChromaticityCoordinates(
                BinaryPrimitives.ReadUInt16BigEndian(data[12..]) * chromaticityScale,
                BinaryPrimitives.ReadUInt16BigEndian(data[14..]) * chromaticityScale),
            BinaryPrimitives.ReadUInt32BigEndian(data[16..]) * luminanceScale,
            BinaryPrimitives.ReadUInt32BigEndian(data[20..]) * luminanceScale);
    }

    /// <summary>
    /// Parses content color-volume information.
    /// </summary>
    /// <param name="data">The complete content color-volume payload.</param>
    /// <returns>The content color-volume information.</returns>
    public static HeifContentColorVolume ParseContentColorVolume(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            throw new InvalidImageContentException("The content color-volume property is truncated.");
        }

        byte flags = data[0];
        if ((flags & 0xC3) != 0)
        {
            throw new InvalidImageContentException("The content color-volume property has nonzero reserved flags.");
        }

        bool primariesPresent = (flags & 0x20) != 0;
        bool minimumLuminancePresent = (flags & 0x10) != 0;
        bool maximumLuminancePresent = (flags & 0x08) != 0;
        bool averageLuminancePresent = (flags & 0x04) != 0;
        if (!primariesPresent && !minimumLuminancePresent && !maximumLuminancePresent && !averageLuminancePresent)
        {
            throw new InvalidImageContentException("The content color-volume property does not describe any values.");
        }

        int expectedLength = 1
            + (primariesPresent ? 24 : 0)
            + (minimumLuminancePresent ? 4 : 0)
            + (maximumLuminancePresent ? 4 : 0)
            + (averageLuminancePresent ? 4 : 0);

        EnsureExactLength(data, expectedLength, "content color volume");
        int offset = 1;
        RgbPrimariesChromaticityCoordinates? primaries = null;
        if (primariesPresent)
        {
            int greenX = BinaryPrimitives.ReadInt32BigEndian(data[offset..]);
            int greenY = BinaryPrimitives.ReadInt32BigEndian(data[(offset + 4)..]);
            int blueX = BinaryPrimitives.ReadInt32BigEndian(data[(offset + 8)..]);
            int blueY = BinaryPrimitives.ReadInt32BigEndian(data[(offset + 12)..]);
            int redX = BinaryPrimitives.ReadInt32BigEndian(data[(offset + 16)..]);
            int redY = BinaryPrimitives.ReadInt32BigEndian(data[(offset + 20)..]);
            const int maximumChromaticityValue = 5_000_000;
            if (greenX is < -maximumChromaticityValue or > maximumChromaticityValue
                || greenY is < -maximumChromaticityValue or > maximumChromaticityValue
                || blueX is < -maximumChromaticityValue or > maximumChromaticityValue
                || blueY is < -maximumChromaticityValue or > maximumChromaticityValue
                || redX is < -maximumChromaticityValue or > maximumChromaticityValue
                || redY is < -maximumChromaticityValue or > maximumChromaticityValue)
            {
                throw new InvalidImageContentException("The content color-volume property has an out-of-range primary coordinate.");
            }

            const float chromaticityScale = 1F / 50000F;

            // Content-color-volume syntax also stores signed coordinates in G, B, R order.
            primaries = new RgbPrimariesChromaticityCoordinates(
                new CieXyChromaticityCoordinates(redX * chromaticityScale, redY * chromaticityScale),
                new CieXyChromaticityCoordinates(greenX * chromaticityScale, greenY * chromaticityScale),
                new CieXyChromaticityCoordinates(blueX * chromaticityScale, blueY * chromaticityScale));

            offset += 24;
        }

        uint? minimumLuminance = minimumLuminancePresent ? BinaryPrimitives.ReadUInt32BigEndian(data[offset..]) : null;
        offset += minimumLuminancePresent ? 4 : 0;
        uint? maximumLuminance = maximumLuminancePresent ? BinaryPrimitives.ReadUInt32BigEndian(data[offset..]) : null;
        offset += maximumLuminancePresent ? 4 : 0;
        uint? averageLuminance = averageLuminancePresent ? BinaryPrimitives.ReadUInt32BigEndian(data[offset..]) : null;
        if ((minimumLuminance is not null && averageLuminance is not null && minimumLuminance.Value > averageLuminance.Value)
            || (averageLuminance is not null && maximumLuminance is not null && averageLuminance.Value > maximumLuminance.Value)
            || (minimumLuminance is not null && maximumLuminance is not null && minimumLuminance.Value > maximumLuminance.Value))
        {
            throw new InvalidImageContentException("The content color-volume luminance values are not in ascending order.");
        }

        const double luminanceScale = 1D / 10000000D;

        // These values are normalized according to the signaled transfer characteristics. Preserve that
        // unitless meaning instead of presenting them as physical display luminance.
        return new HeifContentColorVolume(
            primaries,
            minimumLuminance * luminanceScale,
            maximumLuminance * luminanceScale,
            averageLuminance * luminanceScale);
    }

    /// <summary>
    /// Parses an ambient viewing environment.
    /// </summary>
    /// <param name="data">The complete ambient viewing-environment payload.</param>
    /// <returns>The ambient viewing environment.</returns>
    public static HeifAmbientViewingEnvironment ParseAmbientViewingEnvironment(ReadOnlySpan<byte> data)
    {
        EnsureExactLength(data, 8, "ambient viewing environment");
        uint illuminance = BinaryPrimitives.ReadUInt32BigEndian(data);
        ushort lightX = BinaryPrimitives.ReadUInt16BigEndian(data[4..]);
        ushort lightY = BinaryPrimitives.ReadUInt16BigEndian(data[6..]);
        if (illuminance == 0)
        {
            throw new InvalidImageContentException("The ambient viewing-environment property has zero illuminance.");
        }

        if (lightX > 50000 || lightY > 50000)
        {
            throw new InvalidImageContentException("The ambient viewing-environment property has an out-of-range chromaticity coordinate.");
        }

        const double illuminanceScale = 1D / 10000D;
        const float chromaticityScale = 1F / 50000F;

        // The property inherits H.274's fixed-point units: 0.0001 lux for illuminance and 0.00002 for each
        // normalized CIE chromaticity coordinate.
        return new HeifAmbientViewingEnvironment(
            illuminance * illuminanceScale,
            new CieXyChromaticityCoordinates(lightX * chromaticityScale, lightY * chromaticityScale));
    }

    /// <summary>
    /// Parses a reference viewing environment.
    /// </summary>
    /// <param name="data">The complete reference viewing-environment payload.</param>
    /// <returns>The reference viewing environment.</returns>
    public static HeifReferenceViewingEnvironment ParseReferenceViewingEnvironment(ReadOnlySpan<byte> data)
    {
        EnsureExactLength(data, 20, "reference viewing environment");
        if (BinaryPrimitives.ReadUInt32BigEndian(data) != 0)
        {
            throw new InvalidImageContentException("The reference viewing-environment property has an unsupported version or flags.");
        }

        ushort surroundX = BinaryPrimitives.ReadUInt16BigEndian(data[8..]);
        ushort surroundY = BinaryPrimitives.ReadUInt16BigEndian(data[10..]);
        ushort peripheryX = BinaryPrimitives.ReadUInt16BigEndian(data[16..]);
        ushort peripheryY = BinaryPrimitives.ReadUInt16BigEndian(data[18..]);
        if (surroundX > 10000 || surroundY > 10000 || peripheryX > 10000 || peripheryY > 10000)
        {
            throw new InvalidImageContentException("The reference viewing-environment property has an out-of-range chromaticity coordinate.");
        }

        const double luminanceScale = 1D / 10000D;
        const float chromaticityScale = 1F / 10000F;

        // The full-box header is followed by display-surround and wider-periphery fields. Both groups use
        // 0.0001 increments, but luminance is physical cd/m2 while the CIE coordinates are normalized.
        return new HeifReferenceViewingEnvironment(
            BinaryPrimitives.ReadUInt32BigEndian(data[4..]) * luminanceScale,
            new CieXyChromaticityCoordinates(surroundX * chromaticityScale, surroundY * chromaticityScale),
            BinaryPrimitives.ReadUInt32BigEndian(data[12..]) * luminanceScale,
            new CieXyChromaticityCoordinates(peripheryX * chromaticityScale, peripheryY * chromaticityScale));
    }

    /// <summary>
    /// Parses nominal diffuse-white information.
    /// </summary>
    /// <param name="data">The complete nominal diffuse-white payload.</param>
    /// <returns>The nominal diffuse-white information.</returns>
    public static HeifNominalDiffuseWhite ParseNominalDiffuseWhite(ReadOnlySpan<byte> data)
    {
        EnsureExactLength(data, 8, "nominal diffuse white");
        if (BinaryPrimitives.ReadUInt32BigEndian(data) != 0)
        {
            throw new InvalidImageContentException("The nominal diffuse-white property has an unsupported version or flags.");
        }

        uint luminance = BinaryPrimitives.ReadUInt32BigEndian(data[4..]);
        const double luminanceScale = 1D / 10000D;

        // A zero coded value requests the standard-defined default rather than describing black diffuse white.
        return new HeifNominalDiffuseWhite(luminance == 0 ? null : luminance * luminanceScale);
    }

    /// <summary>
    /// Parses the sequence-header operating point selected by an AV1 image item.
    /// </summary>
    /// <param name="data">The complete AV1 operating-point-selector payload.</param>
    /// <returns>The selected zero-based operating-point index.</returns>
    public static Av1OperatingPointSelector ParseAv1OperatingPointSelector(ReadOnlySpan<byte> data)
    {
        EnsureExactLength(data, 1, "AV1 operating-point selector");
        byte index = data[0];
        if (index >= Av1Constants.MaxOperatingPointCount)
        {
            // AV1 signals operating_points_cnt_minus_1 in five bits, so a sequence header cannot contain
            // an operating point whose zero-based index is greater than 31.
            throw new InvalidImageContentException($"The AV1 operating-point selector requests unsupported index {index}.");
        }

        return new Av1OperatingPointSelector(index);
    }

    /// <summary>
    /// Parses the spatial layer selected by an AV1 image item.
    /// </summary>
    /// <param name="data">The complete AV1 layer-selector payload.</param>
    /// <returns>The selected spatial-layer identifier.</returns>
    public static Av1LayerSelector ParseAv1LayerSelector(ReadOnlySpan<byte> data)
    {
        EnsureExactLength(data, 2, "AV1 layer selector");
        ushort layerId = BinaryPrimitives.ReadUInt16BigEndian(data);
        if (layerId != Av1LayerSelector.AllLayers && layerId >= Av1Constants.MaxSpatialLayerCount)
        {
            // AV1 OBU extension headers carry spatial_id in two bits. AVIF reserves 0xFFFF to request
            // progressive exposure or final-layer decoding instead of selecting one of those four IDs.
            throw new InvalidImageContentException($"The AV1 layer selector requests unsupported layer {layerId}.");
        }

        return new Av1LayerSelector(layerId);
    }

    /// <summary>
    /// Parses the explicit payload boundaries of a layered AV1 image item.
    /// </summary>
    /// <param name="data">The complete AV1 layered-image-indexing payload.</param>
    /// <returns>The three explicit layer sizes.</returns>
    public static Av1LayeredImageIndex ParseAv1LayeredImageIndex(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            throw new InvalidImageContentException("The AV1 layered-image indexing property has an invalid length.");
        }

        byte sizeFlags = data[0];
        if ((sizeFlags & 0xFE) != 0)
        {
            throw new InvalidImageContentException("The AV1 layered-image indexing property has nonzero reserved bits.");
        }

        bool usesLargeSizes = (sizeFlags & 1) != 0;
        int layerSizeWidth = usesLargeSizes ? 4 : 2;

        // a1lx stores the first three sizes explicitly. A fourth layer, when present, consumes the
        // remaining item payload and therefore has no stored size field.
        EnsureExactLength(data, 1 + (3 * layerSizeWidth), "AV1 layered-image indexing");
        if (usesLargeSizes)
        {
            return new Av1LayeredImageIndex(
                BinaryPrimitives.ReadUInt32BigEndian(data[1..]),
                BinaryPrimitives.ReadUInt32BigEndian(data[5..]),
                BinaryPrimitives.ReadUInt32BigEndian(data[9..]));
        }

        return new Av1LayeredImageIndex(
            BinaryPrimitives.ReadUInt16BigEndian(data[1..]),
            BinaryPrimitives.ReadUInt16BigEndian(data[3..]),
            BinaryPrimitives.ReadUInt16BigEndian(data[5..]));
    }

    /// <summary>
    /// Parses a clean-aperture crop description.
    /// </summary>
    /// <param name="data">The complete clean-aperture payload.</param>
    /// <returns>The clean-aperture crop description.</returns>
    public static HeifCleanAperture ParseCleanAperture(ReadOnlySpan<byte> data)
    {
        EnsureExactLength(data, 32, "clean aperture");
        return new HeifCleanAperture(
            BinaryPrimitives.ReadInt32BigEndian(data),
            BinaryPrimitives.ReadInt32BigEndian(data[4..]),
            BinaryPrimitives.ReadInt32BigEndian(data[8..]),
            BinaryPrimitives.ReadInt32BigEndian(data[12..]),
            BinaryPrimitives.ReadInt32BigEndian(data[16..]),
            BinaryPrimitives.ReadInt32BigEndian(data[20..]),
            BinaryPrimitives.ReadInt32BigEndian(data[24..]),
            BinaryPrimitives.ReadInt32BigEndian(data[28..]));
    }

    /// <summary>
    /// Parses the number of counter-clockwise quarter turns applied to an image.
    /// </summary>
    /// <param name="data">The complete image-rotation payload.</param>
    /// <returns>The number of counter-clockwise quarter turns.</returns>
    public static byte ParseRotation(ReadOnlySpan<byte> data)
    {
        EnsureExactLength(data, 1, "image rotation");
        if ((data[0] & 0xFC) != 0)
        {
            throw new InvalidImageContentException("The image rotation property has nonzero reserved bits.");
        }

        return (byte)(data[0] & 3);
    }

    /// <summary>
    /// Parses the horizontal or vertical image-mirror axis.
    /// </summary>
    /// <param name="data">The complete image-mirror payload.</param>
    /// <returns>Zero for the horizontal axis or one for the vertical axis.</returns>
    public static byte ParseMirrorAxis(ReadOnlySpan<byte> data)
    {
        EnsureExactLength(data, 1, "image mirror");
        if ((data[0] & 0xFE) != 0)
        {
            throw new InvalidImageContentException("The image mirror property has nonzero reserved bits.");
        }

        return (byte)(data[0] & 1);
    }

    /// <summary>
    /// Requires a fixed-size image property to contain exactly its registered payload length.
    /// </summary>
    /// <param name="data">The complete property payload.</param>
    /// <param name="length">The registered payload length.</param>
    /// <param name="name">The property name used in malformed-image diagnostics.</param>
    private static void EnsureExactLength(ReadOnlySpan<byte> data, int length, string name)
    {
        if (data.Length != length)
        {
            throw new InvalidImageContentException($"The {name} property has an invalid length.");
        }
    }
}
