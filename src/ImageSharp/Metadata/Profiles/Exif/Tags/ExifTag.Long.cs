// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

/// <content/>
public abstract partial class ExifTag
{
    /// <summary>
    /// Gets the SubfileType exif tag.
    /// </summary>
    public static ExifTag<uint> SubfileType { get; } = new(ExifTagValue.SubfileType);

    /// <summary>
    /// Gets the SubIFDOffset exif tag.
    /// </summary>
    public static ExifTag<uint> SubIFDOffset { get; } = new(ExifTagValue.SubIFDOffset);

    /// <summary>
    /// Gets the GPSIFDOffset exif tag.
    /// </summary>
    public static ExifTag<uint> GPSIFDOffset { get; } = new(ExifTagValue.GPSIFDOffset);

    /// <summary>
    /// Gets the T4Options exif tag.
    /// </summary>
    public static ExifTag<uint> T4Options { get; } = new(ExifTagValue.T4Options);

    /// <summary>
    /// Gets the T6Options exif tag.
    /// </summary>
    public static ExifTag<uint> T6Options { get; } = new(ExifTagValue.T6Options);

    /// <summary>
    /// Gets the XClipPathUnits exif tag.
    /// </summary>
    public static ExifTag<uint> XClipPathUnits { get; } = new(ExifTagValue.XClipPathUnits);

    /// <summary>
    /// Gets the YClipPathUnits exif tag.
    /// </summary>
    public static ExifTag<uint> YClipPathUnits { get; } = new(ExifTagValue.YClipPathUnits);

    /// <summary>
    /// Gets the ProfileType exif tag.
    /// </summary>
    public static ExifTag<uint> ProfileType { get; } = new(ExifTagValue.ProfileType);

    /// <summary>
    /// Gets the CodingMethods exif tag.
    /// </summary>
    public static ExifTag<uint> CodingMethods { get; } = new(ExifTagValue.CodingMethods);

    /// <summary>
    /// Gets the T82ptions exif tag.
    /// </summary>
    public static ExifTag<uint> T82ptions { get; } = new(ExifTagValue.T82ptions);

    /// <summary>
    /// Gets the JPEGInterchangeFormat exif tag.
    /// </summary>
    public static ExifTag<uint> JPEGInterchangeFormat { get; } = new(ExifTagValue.JPEGInterchangeFormat);

    /// <summary>
    /// Gets the JPEGInterchangeFormatLength exif tag.
    /// </summary>
    public static ExifTag<uint> JPEGInterchangeFormatLength { get; } = new(ExifTagValue.JPEGInterchangeFormatLength);

    /// <summary>
    /// Gets the MDFileTag exif tag.
    /// </summary>
    public static ExifTag<uint> MDFileTag { get; } = new(ExifTagValue.MDFileTag);

    /// <summary>
    /// Gets the StandardOutputSensitivity exif tag.
    /// </summary>
    public static ExifTag<uint> StandardOutputSensitivity { get; } = new(ExifTagValue.StandardOutputSensitivity);

    /// <summary>
    /// Gets the RecommendedExposureIndex exif tag.
    /// </summary>
    public static ExifTag<uint> RecommendedExposureIndex { get; } = new(ExifTagValue.RecommendedExposureIndex);

    /// <summary>
    /// Gets the ISOSpeed exif tag.
    /// </summary>
    public static ExifTag<uint> ISOSpeed { get; } = new(ExifTagValue.ISOSpeed);

    /// <summary>
    /// Gets the ISOSpeedLatitudeyyy exif tag.
    /// </summary>
    public static ExifTag<uint> ISOSpeedLatitudeyyy { get; } = new(ExifTagValue.ISOSpeedLatitudeyyy);

    /// <summary>
    /// Gets the ISOSpeedLatitudezzz exif tag.
    /// </summary>
    public static ExifTag<uint> ISOSpeedLatitudezzz { get; } = new(ExifTagValue.ISOSpeedLatitudezzz);

    /// <summary>
    /// Gets the FaxRecvParams exif tag.
    /// </summary>
    public static ExifTag<uint> FaxRecvParams { get; } = new(ExifTagValue.FaxRecvParams);

    /// <summary>
    /// Gets the FaxRecvTime exif tag.
    /// </summary>
    public static ExifTag<uint> FaxRecvTime { get; } = new(ExifTagValue.FaxRecvTime);

    /// <summary>
    /// Gets the ImageNumber exif tag.
    /// </summary>
    public static ExifTag<uint> ImageNumber { get; } = new(ExifTagValue.ImageNumber);
}
