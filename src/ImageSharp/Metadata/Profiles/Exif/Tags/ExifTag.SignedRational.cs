// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

/// <content/>
public abstract partial class ExifTag
{
    /// <summary>
    /// Gets the ShutterSpeedValue exif tag.
    /// </summary>
    public static ExifTag<SignedRational> ShutterSpeedValue { get; } = new(ExifTagValue.ShutterSpeedValue);

    /// <summary>
    /// Gets the BrightnessValue exif tag.
    /// </summary>
    public static ExifTag<SignedRational> BrightnessValue { get; } = new(ExifTagValue.BrightnessValue);

    /// <summary>
    /// Gets the ExposureBiasValue exif tag.
    /// </summary>
    public static ExifTag<SignedRational> ExposureBiasValue { get; } = new(ExifTagValue.ExposureBiasValue);

    /// <summary>
    /// Gets the AmbientTemperature exif tag.
    /// </summary>
    public static ExifTag<SignedRational> AmbientTemperature { get; } = new(ExifTagValue.AmbientTemperature);

    /// <summary>
    /// Gets the WaterDepth exif tag.
    /// </summary>
    public static ExifTag<SignedRational> WaterDepth { get; } = new(ExifTagValue.WaterDepth);

    /// <summary>
    /// Gets the CameraElevationAngle exif tag.
    /// </summary>
    public static ExifTag<SignedRational> CameraElevationAngle { get; } = new(ExifTagValue.CameraElevationAngle);
}
