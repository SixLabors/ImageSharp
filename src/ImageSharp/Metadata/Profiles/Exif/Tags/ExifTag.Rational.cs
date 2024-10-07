// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

/// <content/>
public abstract partial class ExifTag
{
    /// <summary>
    /// Gets the XPosition exif tag.
    /// </summary>
    public static ExifTag<Rational> XPosition { get; } = new(ExifTagValue.XPosition);

    /// <summary>
    /// Gets the YPosition exif tag.
    /// </summary>
    public static ExifTag<Rational> YPosition { get; } = new(ExifTagValue.YPosition);

    /// <summary>
    /// Gets the XResolution exif tag.
    /// </summary>
    public static ExifTag<Rational> XResolution { get; } = new(ExifTagValue.XResolution);

    /// <summary>
    /// Gets the YResolution exif tag.
    /// </summary>
    public static ExifTag<Rational> YResolution { get; } = new(ExifTagValue.YResolution);

    /// <summary>
    /// Gets the BatteryLevel exif tag.
    /// </summary>
    public static ExifTag<Rational> BatteryLevel { get; } = new(ExifTagValue.BatteryLevel);

    /// <summary>
    /// Gets the ExposureTime exif tag.
    /// </summary>
    public static ExifTag<Rational> ExposureTime { get; } = new(ExifTagValue.ExposureTime);

    /// <summary>
    /// Gets the FNumber exif tag.
    /// </summary>
    public static ExifTag<Rational> FNumber { get; } = new(ExifTagValue.FNumber);

    /// <summary>
    /// Gets the MDScalePixel exif tag.
    /// </summary>
    public static ExifTag<Rational> MDScalePixel { get; } = new(ExifTagValue.MDScalePixel);

    /// <summary>
    /// Gets the CompressedBitsPerPixel exif tag.
    /// </summary>
    public static ExifTag<Rational> CompressedBitsPerPixel { get; } = new(ExifTagValue.CompressedBitsPerPixel);

    /// <summary>
    /// Gets the ApertureValue exif tag.
    /// </summary>
    public static ExifTag<Rational> ApertureValue { get; } = new(ExifTagValue.ApertureValue);

    /// <summary>
    /// Gets the MaxApertureValue exif tag.
    /// </summary>
    public static ExifTag<Rational> MaxApertureValue { get; } = new(ExifTagValue.MaxApertureValue);

    /// <summary>
    /// Gets the SubjectDistance exif tag.
    /// </summary>
    public static ExifTag<Rational> SubjectDistance { get; } = new(ExifTagValue.SubjectDistance);

    /// <summary>
    /// Gets the FocalLength exif tag.
    /// </summary>
    public static ExifTag<Rational> FocalLength { get; } = new(ExifTagValue.FocalLength);

    /// <summary>
    /// Gets the FlashEnergy2 exif tag.
    /// </summary>
    public static ExifTag<Rational> FlashEnergy2 { get; } = new(ExifTagValue.FlashEnergy2);

    /// <summary>
    /// Gets the FocalPlaneXResolution2 exif tag.
    /// </summary>
    public static ExifTag<Rational> FocalPlaneXResolution2 { get; } = new(ExifTagValue.FocalPlaneXResolution2);

    /// <summary>
    /// Gets the FocalPlaneYResolution2 exif tag.
    /// </summary>
    public static ExifTag<Rational> FocalPlaneYResolution2 { get; } = new(ExifTagValue.FocalPlaneYResolution2);

    /// <summary>
    /// Gets the ExposureIndex2 exif tag.
    /// </summary>
    public static ExifTag<Rational> ExposureIndex2 { get; } = new(ExifTagValue.ExposureIndex2);

    /// <summary>
    /// Gets the Humidity exif tag.
    /// </summary>
    public static ExifTag<Rational> Humidity { get; } = new(ExifTagValue.Humidity);

    /// <summary>
    /// Gets the Pressure exif tag.
    /// </summary>
    public static ExifTag<Rational> Pressure { get; } = new(ExifTagValue.Pressure);

    /// <summary>
    /// Gets the Acceleration exif tag.
    /// </summary>
    public static ExifTag<Rational> Acceleration { get; } = new(ExifTagValue.Acceleration);

    /// <summary>
    /// Gets the FlashEnergy exif tag.
    /// </summary>
    public static ExifTag<Rational> FlashEnergy { get; } = new(ExifTagValue.FlashEnergy);

    /// <summary>
    /// Gets the FocalPlaneXResolution exif tag.
    /// </summary>
    public static ExifTag<Rational> FocalPlaneXResolution { get; } = new(ExifTagValue.FocalPlaneXResolution);

    /// <summary>
    /// Gets the FocalPlaneYResolution exif tag.
    /// </summary>
    public static ExifTag<Rational> FocalPlaneYResolution { get; } = new(ExifTagValue.FocalPlaneYResolution);

    /// <summary>
    /// Gets the ExposureIndex exif tag.
    /// </summary>
    public static ExifTag<Rational> ExposureIndex { get; } = new(ExifTagValue.ExposureIndex);

    /// <summary>
    /// Gets the DigitalZoomRatio exif tag.
    /// </summary>
    public static ExifTag<Rational> DigitalZoomRatio { get; } = new(ExifTagValue.DigitalZoomRatio);

    /// <summary>
    /// Gets the GPSAltitude exif tag.
    /// </summary>
    public static ExifTag<Rational> GPSAltitude { get; } = new(ExifTagValue.GPSAltitude);

    /// <summary>
    /// Gets the GPSDOP exif tag.
    /// </summary>
    public static ExifTag<Rational> GPSDOP { get; } = new(ExifTagValue.GPSDOP);

    /// <summary>
    /// Gets the GPSSpeed exif tag.
    /// </summary>
    public static ExifTag<Rational> GPSSpeed { get; } = new(ExifTagValue.GPSSpeed);

    /// <summary>
    /// Gets the GPSTrack exif tag.
    /// </summary>
    public static ExifTag<Rational> GPSTrack { get; } = new(ExifTagValue.GPSTrack);

    /// <summary>
    /// Gets the GPSImgDirection exif tag.
    /// </summary>
    public static ExifTag<Rational> GPSImgDirection { get; } = new(ExifTagValue.GPSImgDirection);

    /// <summary>
    /// Gets the GPSDestBearing exif tag.
    /// </summary>
    public static ExifTag<Rational> GPSDestBearing { get; } = new(ExifTagValue.GPSDestBearing);

    /// <summary>
    /// Gets the GPSDestDistance exif tag.
    /// </summary>
    public static ExifTag<Rational> GPSDestDistance { get; } = new(ExifTagValue.GPSDestDistance);

    /// <summary>
    /// Gets the GPSHPositioningError exif tag.
    /// </summary>
    public static ExifTag<Rational> GPSHPositioningError { get; } = new(ExifTagValue.GPSHPositioningError);
}
