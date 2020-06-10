// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    /// <content/>
    public abstract partial class ExifTag
    {
        /// <summary>
        /// Gets the XPosition exif tag.
        /// </summary>
        public static ExifTag<Rational> XPosition { get; } = new ExifTag<Rational>(ExifTagValue.XPosition);

        /// <summary>
        /// Gets the YPosition exif tag.
        /// </summary>
        public static ExifTag<Rational> YPosition { get; } = new ExifTag<Rational>(ExifTagValue.YPosition);

        /// <summary>
        /// Gets the XResolution exif tag.
        /// </summary>
        public static ExifTag<Rational> XResolution { get; } = new ExifTag<Rational>(ExifTagValue.XResolution);

        /// <summary>
        /// Gets the YResolution exif tag.
        /// </summary>
        public static ExifTag<Rational> YResolution { get; } = new ExifTag<Rational>(ExifTagValue.YResolution);

        /// <summary>
        /// Gets the BatteryLevel exif tag.
        /// </summary>
        public static ExifTag<Rational> BatteryLevel { get; } = new ExifTag<Rational>(ExifTagValue.BatteryLevel);

        /// <summary>
        /// Gets the ExposureTime exif tag.
        /// </summary>
        public static ExifTag<Rational> ExposureTime { get; } = new ExifTag<Rational>(ExifTagValue.ExposureTime);

        /// <summary>
        /// Gets the FNumber exif tag.
        /// </summary>
        public static ExifTag<Rational> FNumber { get; } = new ExifTag<Rational>(ExifTagValue.FNumber);

        /// <summary>
        /// Gets the MDScalePixel exif tag.
        /// </summary>
        public static ExifTag<Rational> MDScalePixel { get; } = new ExifTag<Rational>(ExifTagValue.MDScalePixel);

        /// <summary>
        /// Gets the CompressedBitsPerPixel exif tag.
        /// </summary>
        public static ExifTag<Rational> CompressedBitsPerPixel { get; } = new ExifTag<Rational>(ExifTagValue.CompressedBitsPerPixel);

        /// <summary>
        /// Gets the ApertureValue exif tag.
        /// </summary>
        public static ExifTag<Rational> ApertureValue { get; } = new ExifTag<Rational>(ExifTagValue.ApertureValue);

        /// <summary>
        /// Gets the MaxApertureValue exif tag.
        /// </summary>
        public static ExifTag<Rational> MaxApertureValue { get; } = new ExifTag<Rational>(ExifTagValue.MaxApertureValue);

        /// <summary>
        /// Gets the SubjectDistance exif tag.
        /// </summary>
        public static ExifTag<Rational> SubjectDistance { get; } = new ExifTag<Rational>(ExifTagValue.SubjectDistance);

        /// <summary>
        /// Gets the FocalLength exif tag.
        /// </summary>
        public static ExifTag<Rational> FocalLength { get; } = new ExifTag<Rational>(ExifTagValue.FocalLength);

        /// <summary>
        /// Gets the FlashEnergy2 exif tag.
        /// </summary>
        public static ExifTag<Rational> FlashEnergy2 { get; } = new ExifTag<Rational>(ExifTagValue.FlashEnergy2);

        /// <summary>
        /// Gets the FocalPlaneXResolution2 exif tag.
        /// </summary>
        public static ExifTag<Rational> FocalPlaneXResolution2 { get; } = new ExifTag<Rational>(ExifTagValue.FocalPlaneXResolution2);

        /// <summary>
        /// Gets the FocalPlaneYResolution2 exif tag.
        /// </summary>
        public static ExifTag<Rational> FocalPlaneYResolution2 { get; } = new ExifTag<Rational>(ExifTagValue.FocalPlaneYResolution2);

        /// <summary>
        /// Gets the ExposureIndex2 exif tag.
        /// </summary>
        public static ExifTag<Rational> ExposureIndex2 { get; } = new ExifTag<Rational>(ExifTagValue.ExposureIndex2);

        /// <summary>
        /// Gets the Humidity exif tag.
        /// </summary>
        public static ExifTag<Rational> Humidity { get; } = new ExifTag<Rational>(ExifTagValue.Humidity);

        /// <summary>
        /// Gets the Pressure exif tag.
        /// </summary>
        public static ExifTag<Rational> Pressure { get; } = new ExifTag<Rational>(ExifTagValue.Pressure);

        /// <summary>
        /// Gets the Acceleration exif tag.
        /// </summary>
        public static ExifTag<Rational> Acceleration { get; } = new ExifTag<Rational>(ExifTagValue.Acceleration);

        /// <summary>
        /// Gets the FlashEnergy exif tag.
        /// </summary>
        public static ExifTag<Rational> FlashEnergy { get; } = new ExifTag<Rational>(ExifTagValue.FlashEnergy);

        /// <summary>
        /// Gets the FocalPlaneXResolution exif tag.
        /// </summary>
        public static ExifTag<Rational> FocalPlaneXResolution { get; } = new ExifTag<Rational>(ExifTagValue.FocalPlaneXResolution);

        /// <summary>
        /// Gets the FocalPlaneYResolution exif tag.
        /// </summary>
        public static ExifTag<Rational> FocalPlaneYResolution { get; } = new ExifTag<Rational>(ExifTagValue.FocalPlaneYResolution);

        /// <summary>
        /// Gets the ExposureIndex exif tag.
        /// </summary>
        public static ExifTag<Rational> ExposureIndex { get; } = new ExifTag<Rational>(ExifTagValue.ExposureIndex);

        /// <summary>
        /// Gets the DigitalZoomRatio exif tag.
        /// </summary>
        public static ExifTag<Rational> DigitalZoomRatio { get; } = new ExifTag<Rational>(ExifTagValue.DigitalZoomRatio);

        /// <summary>
        /// Gets the GPSAltitude exif tag.
        /// </summary>
        public static ExifTag<Rational> GPSAltitude { get; } = new ExifTag<Rational>(ExifTagValue.GPSAltitude);

        /// <summary>
        /// Gets the GPSDOP exif tag.
        /// </summary>
        public static ExifTag<Rational> GPSDOP { get; } = new ExifTag<Rational>(ExifTagValue.GPSDOP);

        /// <summary>
        /// Gets the GPSSpeed exif tag.
        /// </summary>
        public static ExifTag<Rational> GPSSpeed { get; } = new ExifTag<Rational>(ExifTagValue.GPSSpeed);

        /// <summary>
        /// Gets the GPSTrack exif tag.
        /// </summary>
        public static ExifTag<Rational> GPSTrack { get; } = new ExifTag<Rational>(ExifTagValue.GPSTrack);

        /// <summary>
        /// Gets the GPSImgDirection exif tag.
        /// </summary>
        public static ExifTag<Rational> GPSImgDirection { get; } = new ExifTag<Rational>(ExifTagValue.GPSImgDirection);

        /// <summary>
        /// Gets the GPSDestBearing exif tag.
        /// </summary>
        public static ExifTag<Rational> GPSDestBearing { get; } = new ExifTag<Rational>(ExifTagValue.GPSDestBearing);

        /// <summary>
        /// Gets the GPSDestDistance exif tag.
        /// </summary>
        public static ExifTag<Rational> GPSDestDistance { get; } = new ExifTag<Rational>(ExifTagValue.GPSDestDistance);
    }
}
