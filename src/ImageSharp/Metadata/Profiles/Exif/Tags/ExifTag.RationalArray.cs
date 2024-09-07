// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

/// <content/>
public abstract partial class ExifTag
{
    /// <summary>
    /// Gets the WhitePoint exif tag.
    /// </summary>
    public static ExifTag<Rational[]> WhitePoint { get; } = new(ExifTagValue.WhitePoint);

    /// <summary>
    /// Gets the PrimaryChromaticities exif tag.
    /// </summary>
    public static ExifTag<Rational[]> PrimaryChromaticities { get; } = new(ExifTagValue.PrimaryChromaticities);

    /// <summary>
    /// Gets the YCbCrCoefficients exif tag.
    /// </summary>
    public static ExifTag<Rational[]> YCbCrCoefficients { get; } = new(ExifTagValue.YCbCrCoefficients);

    /// <summary>
    /// Gets the ReferenceBlackWhite exif tag.
    /// </summary>
    public static ExifTag<Rational[]> ReferenceBlackWhite { get; } = new(ExifTagValue.ReferenceBlackWhite);

    /// <summary>
    /// Gets the GPSLatitude exif tag.
    /// </summary>
    public static ExifTag<Rational[]> GPSLatitude { get; } = new(ExifTagValue.GPSLatitude);

    /// <summary>
    /// Gets the GPSLongitude exif tag.
    /// </summary>
    public static ExifTag<Rational[]> GPSLongitude { get; } = new(ExifTagValue.GPSLongitude);

    /// <summary>
    /// Gets the GPSTimestamp exif tag.
    /// </summary>
    public static ExifTag<Rational[]> GPSTimestamp { get; } = new(ExifTagValue.GPSTimestamp);

    /// <summary>
    /// Gets the GPSDestLatitude exif tag.
    /// </summary>
    public static ExifTag<Rational[]> GPSDestLatitude { get; } = new(ExifTagValue.GPSDestLatitude);

    /// <summary>
    /// Gets the GPSDestLongitude exif tag.
    /// </summary>
    public static ExifTag<Rational[]> GPSDestLongitude { get; } = new(ExifTagValue.GPSDestLongitude);

    /// <summary>
    /// Gets the LensSpecification exif tag.
    /// </summary>
    public static ExifTag<Rational[]> LensSpecification { get; } = new(ExifTagValue.LensSpecification);
}
