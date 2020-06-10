// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    /// <content/>
    public abstract partial class ExifTag
    {
        /// <summary>
        /// Gets the WhitePoint exif tag.
        /// </summary>
        public static ExifTag<Rational[]> WhitePoint { get; } = new ExifTag<Rational[]>(ExifTagValue.WhitePoint);

        /// <summary>
        /// Gets the PrimaryChromaticities exif tag.
        /// </summary>
        public static ExifTag<Rational[]> PrimaryChromaticities { get; } = new ExifTag<Rational[]>(ExifTagValue.PrimaryChromaticities);

        /// <summary>
        /// Gets the YCbCrCoefficients exif tag.
        /// </summary>
        public static ExifTag<Rational[]> YCbCrCoefficients { get; } = new ExifTag<Rational[]>(ExifTagValue.YCbCrCoefficients);

        /// <summary>
        /// Gets the ReferenceBlackWhite exif tag.
        /// </summary>
        public static ExifTag<Rational[]> ReferenceBlackWhite { get; } = new ExifTag<Rational[]>(ExifTagValue.ReferenceBlackWhite);

        /// <summary>
        /// Gets the GPSLatitude exif tag.
        /// </summary>
        public static ExifTag<Rational[]> GPSLatitude { get; } = new ExifTag<Rational[]>(ExifTagValue.GPSLatitude);

        /// <summary>
        /// Gets the GPSLongitude exif tag.
        /// </summary>
        public static ExifTag<Rational[]> GPSLongitude { get; } = new ExifTag<Rational[]>(ExifTagValue.GPSLongitude);

        /// <summary>
        /// Gets the GPSTimestamp exif tag.
        /// </summary>
        public static ExifTag<Rational[]> GPSTimestamp { get; } = new ExifTag<Rational[]>(ExifTagValue.GPSTimestamp);

        /// <summary>
        /// Gets the GPSDestLatitude exif tag.
        /// </summary>
        public static ExifTag<Rational[]> GPSDestLatitude { get; } = new ExifTag<Rational[]>(ExifTagValue.GPSDestLatitude);

        /// <summary>
        /// Gets the GPSDestLongitude exif tag.
        /// </summary>
        public static ExifTag<Rational[]> GPSDestLongitude { get; } = new ExifTag<Rational[]>(ExifTagValue.GPSDestLongitude);

        /// <summary>
        /// Gets the LensSpecification exif tag.
        /// </summary>
        public static ExifTag<Rational[]> LensSpecification { get; } = new ExifTag<Rational[]>(ExifTagValue.LensSpecification);
    }
}
