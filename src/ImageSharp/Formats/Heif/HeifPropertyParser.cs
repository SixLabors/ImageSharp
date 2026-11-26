// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Parses the image properties that HEIF items and image-sequence sample entries share.
/// </summary>
/// <remarks>
/// A property that only describes metadata is ancillary. Its parser reports damage through the ancillary mode. If the mode ignores the damage, the
/// parser returns <see langword="null"/>. Damage in a property that changes the decoded pixels throws, or the parser reports it as damaged image data.
/// </remarks>
internal static class HeifPropertyParser
{
    /// <summary>
    /// Validates that an item or a track has no value of a property type before the caller associates a value of that type.
    /// </summary>
    /// <remarks>
    /// A second property of a type that a parser associates only one time is damage. A metadata property is ancillary data. All other properties change
    /// the decoded pixels. Thus, the method reports a duplicate of such a property as damaged image data.
    /// </remarks>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="current">The value that the owner has, or <see langword="null"/>.</param>
    /// <param name="handling">The segment integrity mode.</param>
    /// <param name="ownerName">The owner kind for the error message: "item" or "track".</param>
    /// <param name="ownerId">The owner identifier for the error message.</param>
    /// <param name="propertyType">The property type for the error message.</param>
    /// <param name="ancillary"><see langword="true"/> if the property only describes metadata.</param>
    /// <returns>
    /// <see langword="true"/> if the owner has no value. <see langword="false"/> if the mode ignores the duplicate property. In that case, the owner keeps
    /// its first value.
    /// </returns>
    public static bool AssociateOnce<T>(
        T? current,
        SegmentIntegrityHandling handling,
        string ownerName,
        uint ownerId,
        Heif4CharCode propertyType,
        bool ancillary)
    {
        if (current is null)
        {
            return true;
        }

        string message = $"The {ownerName} {ownerId} has more than one '{propertyType}' property.";
        if (ancillary)
        {
            handling.ThrowOrIgnoreAncillaryError(message);
        }
        else
        {
            handling.ThrowOrIgnoreImageDataError(message);
        }

        return false;
    }

    /// <summary>
    /// Parses relative horizontal and vertical pixel spacing.
    /// </summary>
    /// <param name="data">The complete pixel-aspect-ratio payload.</param>
    /// <param name="handling">The segment integrity mode. The property is ancillary metadata.</param>
    /// <returns>The validated relative pixel spacing, or <see langword="null"/> when the mode ignores a damaged property.</returns>
    public static HeifPixelAspectRatio? ParsePixelAspectRatio(ReadOnlySpan<byte> data, SegmentIntegrityHandling handling)
    {
        HeifSpanReader reader = new(data, "pixel aspect ratio");
        uint horizontalSpacing = reader.ReadUInt32();
        uint verticalSpacing = reader.ReadUInt32();
        if (!reader.IsComplete || horizontalSpacing == 0 || verticalSpacing == 0)
        {
            handling.ThrowOrIgnoreAncillaryError("The pixel aspect ratio property has an invalid length or zero spacing.");
            return null;
        }

        return new HeifPixelAspectRatio(horizontalSpacing, verticalSpacing);
    }

    /// <summary>
    /// Parses the bit depth of each channel from a pixel-information property.
    /// </summary>
    /// <remarks>
    /// The decoder compares the bit depths with the codec configuration, as it does for a second pixel-information property. Thus, the parser reports a
    /// damaged property as damaged image data.
    /// </remarks>
    /// <param name="data">The complete pixel-information full-box payload.</param>
    /// <param name="handling">The segment integrity mode.</param>
    /// <returns>The bit depth of each channel, or <see langword="null"/> when the mode ignores a damaged property.</returns>
    public static byte[]? ParsePixelInformation(ReadOnlySpan<byte> data, SegmentIntegrityHandling handling)
    {
        // The full-box header comes before one bit-depth byte for each channel. The decoder resolves the property associations after it reuses the
        // pooled box buffer. Thus, the parser copies the channel bit depths one time, at this boundary.
        // ISO/IEC 23008-12, section 6.5.6: the full box has version 0 and flags 0.
        HeifSpanReader reader = new(data, "pixel information");
        if (reader.ReadUInt32() != 0)
        {
            handling.ThrowOrIgnoreImageDataError("The pixel information property has an unsupported version or flags.");
            return null;
        }

        int channelCount = reader.ReadUInt8();
        ReadOnlySpan<byte> channelBitDepths = reader.ReadBytes(channelCount);
        if (!reader.IsComplete || channelCount == 0)
        {
            handling.ThrowOrIgnoreImageDataError("The pixel information property has an invalid length or no channels.");
            return null;
        }

        int zeroPrecisionChannel = channelBitDepths.IndexOf((byte)0);
        if (zeroPrecisionChannel >= 0)
        {
            handling.ThrowOrIgnoreImageDataError($"The pixel information property declares zero precision for channel {zeroPrecisionChannel}.");
            return null;
        }

        return channelBitDepths.ToArray();
    }

    /// <summary>
    /// Reads and validates an embedded ICC profile through the shared ICC reader.
    /// </summary>
    /// <param name="stream">The stream positioned at the profile payload.</param>
    /// <param name="length">The bounded payload length, without the color type.</param>
    /// <param name="handling">The segment integrity mode. The profile is ancillary data.</param>
    /// <returns>The validated ICC profile, or <see langword="null"/> when the mode ignores a damaged property.</returns>
    /// <exception cref="InvalidImageContentException">
    /// The mode does not ignore ancillary errors, and the profile is not valid. If the ICC reader rejects the profile, the inner exception is the
    /// <see cref="InvalidIccProfileException"/> from the ICC reader.
    /// </exception>
    public static IccProfile? ParseIccProfile(Stream stream, long length, SegmentIntegrityHandling handling)
    {
        // The shared reader rejects a profile that is larger than the maximum ICC profile size. The method validates the length first, so that the
        // 32-bit length parameter cannot overflow.
        if (length >= IccProfile.MaxProfileSize)
        {
            handling.ThrowOrIgnoreAncillaryError("The HEIF ICC color property is too large.");
            return null;
        }

        // Item and track boxes have the same ICC payload. The shared reader bounds storage before reading
        // and validates the declared profile size and tag table before exposing the profile.
        IccReader reader = new();
        if (!reader.TryReadSegment(stream, (int)length))
        {
            handling.ThrowOrIgnoreAncillaryError("The HEIF ICC color property is truncated.");
            return null;
        }

        try
        {
            return reader.ToProfile(handling.IgnoresAncillaryErrors());
        }
        catch (InvalidIccProfileException ex)
        {
            // The ICC reader rejects a profile that is not valid. The profile is ancillary data.
            handling.ThrowOrIgnoreAncillaryError("Invalid HEIF ICC profile.", ex);
            return null;
        }
    }

    /// <summary>
    /// Parses a CICP color description from an <c>nclx</c> color-information payload.
    /// </summary>
    /// <param name="data">The complete payload following the <c>nclx</c> color type.</param>
    /// <returns>The CICP color description.</returns>
    public static CicpProfile ParseCicpProfile(ReadOnlySpan<byte> data)
    {
        HeifSpanReader reader = new(data, "CICP color information");
        ushort colorPrimaries = reader.ReadUInt16();
        ushort transferCharacteristics = reader.ReadUInt16();
        ushort matrixCoefficients = reader.ReadUInt16();
        byte rangeAndReserved = reader.ReadUInt8();
        reader.EnsureEnd();
        if ((rangeAndReserved & 0x7F) != 0)
        {
            throw new InvalidImageContentException("The HEIF CICP color property has nonzero reserved bits.");
        }

        // The box fields are 16-bit, so they can hold future registrations. The ImageSharp CICP profile holds the
        // registered H.273 values, which fit in a byte. Larger values become unspecified.
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
    /// <param name="handling">The segment integrity mode. The property is ancillary metadata.</param>
    /// <returns>The content light-level information, or <see langword="null"/> when the mode ignores a damaged property.</returns>
    public static HeifContentLightLevel? ParseContentLightLevel(ReadOnlySpan<byte> data, SegmentIntegrityHandling handling)
    {
        HeifSpanReader reader = new(data, "content light level");
        ushort maximumContentLightLevel = reader.ReadUInt16();
        ushort maximumFrameAverageLightLevel = reader.ReadUInt16();
        if (!reader.IsComplete)
        {
            handling.ThrowOrIgnoreAncillaryError("The content light level property has an invalid length.");
            return null;
        }

        return new HeifContentLightLevel(maximumContentLightLevel, maximumFrameAverageLightLevel);
    }

    /// <summary>
    /// Parses mastering-display color-volume information.
    /// </summary>
    /// <param name="data">The complete mastering-display color-volume payload.</param>
    /// <param name="handling">The segment integrity mode. The property is ancillary metadata.</param>
    /// <returns>
    /// The mastering-display color-volume information, or <see langword="null"/> when the mode ignores a damaged property.
    /// </returns>
    public static HeifMasteringDisplayColorVolume? ParseMasteringDisplayColorVolume(ReadOnlySpan<byte> data, SegmentIntegrityHandling handling)
    {
        const float chromaticityScale = 1F / 50000F;
        const double luminanceScale = 1D / 10000D;

        // The registered payload inherits the G, B, R primary order used by its mastering-display source syntax.
        // Reorder it into ImageSharp's existing RGB coordinate type at the shared container boundary.
        HeifSpanReader reader = new(data, "mastering display color volume");
        CieXyChromaticityCoordinates greenPrimary = new(reader.ReadUInt16() * chromaticityScale, reader.ReadUInt16() * chromaticityScale);
        CieXyChromaticityCoordinates bluePrimary = new(reader.ReadUInt16() * chromaticityScale, reader.ReadUInt16() * chromaticityScale);
        CieXyChromaticityCoordinates redPrimary = new(reader.ReadUInt16() * chromaticityScale, reader.ReadUInt16() * chromaticityScale);
        CieXyChromaticityCoordinates whitePoint = new(reader.ReadUInt16() * chromaticityScale, reader.ReadUInt16() * chromaticityScale);
        double maximumLuminance = reader.ReadUInt32() * luminanceScale;
        double minimumLuminance = reader.ReadUInt32() * luminanceScale;
        if (!reader.IsComplete)
        {
            handling.ThrowOrIgnoreAncillaryError("The mastering display color volume property has an invalid length.");
            return null;
        }

        return new HeifMasteringDisplayColorVolume(
            new RgbPrimariesChromaticityCoordinates(redPrimary, greenPrimary, bluePrimary),
            whitePoint,
            maximumLuminance,
            minimumLuminance);
    }

    /// <summary>
    /// Parses content color-volume information.
    /// </summary>
    /// <param name="data">The complete content color-volume payload.</param>
    /// <param name="handling">The segment integrity mode. The property is ancillary metadata.</param>
    /// <returns>The content color-volume information, or <see langword="null"/> when the mode ignores a damaged property.</returns>
    public static HeifContentColorVolume? ParseContentColorVolume(ReadOnlySpan<byte> data, SegmentIntegrityHandling handling)
    {
        const int maximumChromaticityValue = 5_000_000;
        const float chromaticityScale = 1F / 50000F;
        const double luminanceScale = 1D / 10000000D;

        HeifSpanReader reader = new(data, "content color volume");
        byte flags = reader.ReadUInt8();
        bool primariesPresent = (flags & 0x20) != 0;
        bool minimumLuminancePresent = (flags & 0x10) != 0;
        bool maximumLuminancePresent = (flags & 0x08) != 0;
        bool averageLuminancePresent = (flags & 0x04) != 0;

        // Content-color-volume syntax stores signed coordinates in G, B, R order.
        int greenX = 0, greenY = 0, blueX = 0, blueY = 0, redX = 0, redY = 0;
        if (primariesPresent)
        {
            greenX = reader.ReadInt32();
            greenY = reader.ReadInt32();
            blueX = reader.ReadInt32();
            blueY = reader.ReadInt32();
            redX = reader.ReadInt32();
            redY = reader.ReadInt32();
        }

        uint? minimumLuminance = minimumLuminancePresent ? reader.ReadUInt32() : null;
        uint? maximumLuminance = maximumLuminancePresent ? reader.ReadUInt32() : null;
        uint? averageLuminance = averageLuminancePresent ? reader.ReadUInt32() : null;
        string? error = null;
        if (!reader.IsComplete)
        {
            error = "The content color-volume property has an invalid length.";
        }
        else if ((flags & 0xC3) != 0)
        {
            error = "The content color-volume property has nonzero reserved flags.";
        }
        else if (!primariesPresent && !minimumLuminancePresent && !maximumLuminancePresent && !averageLuminancePresent)
        {
            error = "The content color-volume property does not describe any values.";
        }
        else if (greenX is < -maximumChromaticityValue or > maximumChromaticityValue
            || greenY is < -maximumChromaticityValue or > maximumChromaticityValue
            || blueX is < -maximumChromaticityValue or > maximumChromaticityValue
            || blueY is < -maximumChromaticityValue or > maximumChromaticityValue
            || redX is < -maximumChromaticityValue or > maximumChromaticityValue
            || redY is < -maximumChromaticityValue or > maximumChromaticityValue)
        {
            error = "The content color-volume property has an out-of-range primary coordinate.";
        }
        else if ((minimumLuminance is not null && averageLuminance is not null && minimumLuminance.Value > averageLuminance.Value)
            || (averageLuminance is not null && maximumLuminance is not null && averageLuminance.Value > maximumLuminance.Value)
            || (minimumLuminance is not null && maximumLuminance is not null && minimumLuminance.Value > maximumLuminance.Value))
        {
            error = "The content color-volume luminance values are not in ascending order.";
        }

        if (error is not null)
        {
            handling.ThrowOrIgnoreAncillaryError(error);
            return null;
        }

        RgbPrimariesChromaticityCoordinates? primaries = primariesPresent
            ? new RgbPrimariesChromaticityCoordinates(
                new CieXyChromaticityCoordinates(redX * chromaticityScale, redY * chromaticityScale),
                new CieXyChromaticityCoordinates(greenX * chromaticityScale, greenY * chromaticityScale),
                new CieXyChromaticityCoordinates(blueX * chromaticityScale, blueY * chromaticityScale))
            : null;

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
    /// <param name="handling">The segment integrity mode. The property is ancillary metadata.</param>
    /// <returns>The ambient viewing environment, or <see langword="null"/> when the mode ignores a damaged property.</returns>
    public static HeifAmbientViewingEnvironment? ParseAmbientViewingEnvironment(ReadOnlySpan<byte> data, SegmentIntegrityHandling handling)
    {
        HeifSpanReader reader = new(data, "ambient viewing environment");
        uint illuminance = reader.ReadUInt32();
        ushort lightX = reader.ReadUInt16();
        ushort lightY = reader.ReadUInt16();
        if (!reader.IsComplete || illuminance == 0 || lightX > 50000 || lightY > 50000)
        {
            handling.ThrowOrIgnoreAncillaryError("The ambient viewing-environment property has an invalid length or an out-of-range value.");
            return null;
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
    /// <param name="handling">The segment integrity mode. The property is ancillary metadata.</param>
    /// <returns>The reference viewing environment, or <see langword="null"/> when the mode ignores a damaged property.</returns>
    public static HeifReferenceViewingEnvironment? ParseReferenceViewingEnvironment(ReadOnlySpan<byte> data, SegmentIntegrityHandling handling)
    {
        // The display-surround and wider-periphery fields come after the full-box header. Both groups use 0.0001 increments. The luminance is in
        // physical cd/m2, but the CIE coordinates are normalized.
        HeifSpanReader reader = new(data, "reference viewing environment");
        uint versionAndFlags = reader.ReadUInt32();
        uint surroundLuminance = reader.ReadUInt32();
        ushort surroundX = reader.ReadUInt16();
        ushort surroundY = reader.ReadUInt16();
        uint peripheryLuminance = reader.ReadUInt32();
        ushort peripheryX = reader.ReadUInt16();
        ushort peripheryY = reader.ReadUInt16();
        if (!reader.IsComplete || versionAndFlags != 0 || surroundX > 10000 || surroundY > 10000 || peripheryX > 10000 || peripheryY > 10000)
        {
            handling.ThrowOrIgnoreAncillaryError("The reference viewing-environment property has an invalid length, version, flags, or value.");
            return null;
        }

        const double luminanceScale = 1D / 10000D;
        const float chromaticityScale = 1F / 10000F;
        return new HeifReferenceViewingEnvironment(
            surroundLuminance * luminanceScale,
            new CieXyChromaticityCoordinates(surroundX * chromaticityScale, surroundY * chromaticityScale),
            peripheryLuminance * luminanceScale,
            new CieXyChromaticityCoordinates(peripheryX * chromaticityScale, peripheryY * chromaticityScale));
    }

    /// <summary>
    /// Parses nominal diffuse-white information.
    /// </summary>
    /// <param name="data">The complete nominal diffuse-white payload.</param>
    /// <param name="handling">The segment integrity mode. The property is ancillary metadata.</param>
    /// <returns>The nominal diffuse-white information, or <see langword="null"/> when the mode ignores a damaged property.</returns>
    public static HeifNominalDiffuseWhite? ParseNominalDiffuseWhite(ReadOnlySpan<byte> data, SegmentIntegrityHandling handling)
    {
        HeifSpanReader reader = new(data, "nominal diffuse white");
        uint versionAndFlags = reader.ReadUInt32();
        uint luminance = reader.ReadUInt32();
        if (!reader.IsComplete || versionAndFlags != 0)
        {
            handling.ThrowOrIgnoreAncillaryError("The nominal diffuse-white property has an invalid length or an unsupported version or flags.");
            return null;
        }

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
        HeifSpanReader reader = new(data, "AV1 operating-point selector");
        byte index = reader.ReadUInt8();
        reader.EnsureEnd();
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
        HeifSpanReader reader = new(data, "AV1 layer selector");
        ushort layerId = reader.ReadUInt16();
        reader.EnsureEnd();
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
        HeifSpanReader reader = new(data, "AV1 layered-image indexing");
        byte sizeFlags = reader.ReadUInt8();
        if ((sizeFlags & 0xFE) != 0)
        {
            throw new InvalidImageContentException("The AV1 layered-image indexing property has nonzero reserved bits.");
        }

        // a1lx stores the first three sizes explicitly. A fourth layer, when present, consumes the
        // remaining item payload and therefore has no stored size field.
        int layerSizeWidth = (sizeFlags & 1) != 0 ? 4 : 2;
        uint firstLayerSize = (uint)reader.ReadUInt(layerSizeWidth);
        uint secondLayerSize = (uint)reader.ReadUInt(layerSizeWidth);
        uint thirdLayerSize = (uint)reader.ReadUInt(layerSizeWidth);
        reader.EnsureEnd();
        return new Av1LayeredImageIndex(firstLayerSize, secondLayerSize, thirdLayerSize);
    }

    /// <summary>
    /// Parses a clean-aperture crop description.
    /// </summary>
    /// <param name="data">The complete clean-aperture payload.</param>
    /// <returns>The clean-aperture crop description.</returns>
    public static HeifCleanAperture ParseCleanAperture(ReadOnlySpan<byte> data)
    {
        HeifSpanReader reader = new(data, "clean aperture");
        HeifCleanAperture cleanAperture = new(
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32());

        reader.EnsureEnd();
        return cleanAperture;
    }

    /// <summary>
    /// Parses the number of counter-clockwise quarter turns applied to an image.
    /// </summary>
    /// <param name="data">The complete image-rotation payload.</param>
    /// <returns>The number of counter-clockwise quarter turns.</returns>
    public static byte ParseRotation(ReadOnlySpan<byte> data)
    {
        HeifSpanReader reader = new(data, "image rotation");
        byte value = reader.ReadUInt8();
        reader.EnsureEnd();
        if ((value & 0xFC) != 0)
        {
            throw new InvalidImageContentException("The image rotation property has nonzero reserved bits.");
        }

        return (byte)(value & 3);
    }

    /// <summary>
    /// Parses the horizontal or vertical image-mirror axis.
    /// </summary>
    /// <param name="data">The complete image-mirror payload.</param>
    /// <returns>Zero for the horizontal axis or one for the vertical axis.</returns>
    public static byte ParseMirrorAxis(ReadOnlySpan<byte> data)
    {
        HeifSpanReader reader = new(data, "image mirror");
        byte value = reader.ReadUInt8();
        reader.EnsureEnd();
        if ((value & 0xFE) != 0)
        {
            throw new InvalidImageContentException("The image mirror property has nonzero reserved bits.");
        }

        return (byte)(value & 1);
    }
}
