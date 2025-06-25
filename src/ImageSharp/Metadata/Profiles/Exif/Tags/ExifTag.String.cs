// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

/// <content/>
public abstract partial class ExifTag
{
    /// <summary>
    /// Gets the ImageDescription exif tag.
    /// </summary>
    public static ExifTag<string> ImageDescription { get; } = new(ExifTagValue.ImageDescription);

    /// <summary>
    /// Gets the Make exif tag.
    /// </summary>
    public static ExifTag<string> Make { get; } = new(ExifTagValue.Make);

    /// <summary>
    /// Gets the Model exif tag.
    /// </summary>
    public static ExifTag<string> Model { get; } = new(ExifTagValue.Model);

    /// <summary>
    /// Gets the Software exif tag.
    /// </summary>
    public static ExifTag<string> Software { get; } = new(ExifTagValue.Software);

    /// <summary>
    /// Gets the DateTime exif tag.
    /// </summary>
    public static ExifTag<string> DateTime { get; } = new(ExifTagValue.DateTime);

    /// <summary>
    /// Gets the Artist exif tag.
    /// </summary>
    public static ExifTag<string> Artist { get; } = new(ExifTagValue.Artist);

    /// <summary>
    /// Gets the HostComputer exif tag.
    /// </summary>
    public static ExifTag<string> HostComputer { get; } = new(ExifTagValue.HostComputer);

    /// <summary>
    /// Gets the Copyright exif tag.
    /// </summary>
    public static ExifTag<string> Copyright { get; } = new(ExifTagValue.Copyright);

    /// <summary>
    /// Gets the DocumentName exif tag.
    /// </summary>
    public static ExifTag<string> DocumentName { get; } = new(ExifTagValue.DocumentName);

    /// <summary>
    /// Gets the PageName exif tag.
    /// </summary>
    public static ExifTag<string> PageName { get; } = new(ExifTagValue.PageName);

    /// <summary>
    /// Gets the InkNames exif tag.
    /// </summary>
    public static ExifTag<string> InkNames { get; } = new(ExifTagValue.InkNames);

    /// <summary>
    /// Gets the TargetPrinter exif tag.
    /// </summary>
    public static ExifTag<string> TargetPrinter { get; } = new(ExifTagValue.TargetPrinter);

    /// <summary>
    /// Gets the ImageID exif tag.
    /// </summary>
    public static ExifTag<string> ImageID { get; } = new(ExifTagValue.ImageID);

    /// <summary>
    /// Gets the MDLabName exif tag.
    /// </summary>
    public static ExifTag<string> MDLabName { get; } = new(ExifTagValue.MDLabName);

    /// <summary>
    /// Gets the MDSampleInfo exif tag.
    /// </summary>
    public static ExifTag<string> MDSampleInfo { get; } = new(ExifTagValue.MDSampleInfo);

    /// <summary>
    /// Gets the MDPrepDate exif tag.
    /// </summary>
    public static ExifTag<string> MDPrepDate { get; } = new(ExifTagValue.MDPrepDate);

    /// <summary>
    /// Gets the MDPrepTime exif tag.
    /// </summary>
    public static ExifTag<string> MDPrepTime { get; } = new(ExifTagValue.MDPrepTime);

    /// <summary>
    /// Gets the MDFileUnits exif tag.
    /// </summary>
    public static ExifTag<string> MDFileUnits { get; } = new(ExifTagValue.MDFileUnits);

    /// <summary>
    /// Gets the SEMInfo exif tag.
    /// </summary>
    public static ExifTag<string> SEMInfo { get; } = new(ExifTagValue.SEMInfo);

    /// <summary>
    /// Gets the SpectralSensitivity exif tag.
    /// </summary>
    public static ExifTag<string> SpectralSensitivity { get; } = new(ExifTagValue.SpectralSensitivity);

    /// <summary>
    /// Gets the DateTimeOriginal exif tag.
    /// </summary>
    public static ExifTag<string> DateTimeOriginal { get; } = new(ExifTagValue.DateTimeOriginal);

    /// <summary>
    /// Gets the DateTimeDigitized exif tag.
    /// </summary>
    public static ExifTag<string> DateTimeDigitized { get; } = new(ExifTagValue.DateTimeDigitized);

    /// <summary>
    /// Gets the SubsecTime exif tag.
    /// </summary>
    public static ExifTag<string> SubsecTime { get; } = new(ExifTagValue.SubsecTime);

    /// <summary>
    /// Gets the SubsecTimeOriginal exif tag.
    /// </summary>
    public static ExifTag<string> SubsecTimeOriginal { get; } = new(ExifTagValue.SubsecTimeOriginal);

    /// <summary>
    /// Gets the SubsecTimeDigitized exif tag.
    /// </summary>
    public static ExifTag<string> SubsecTimeDigitized { get; } = new(ExifTagValue.SubsecTimeDigitized);

    /// <summary>
    /// Gets the RelatedSoundFile exif tag.
    /// </summary>
    public static ExifTag<string> RelatedSoundFile { get; } = new(ExifTagValue.RelatedSoundFile);

    /// <summary>
    /// Gets the FaxSubaddress exif tag.
    /// </summary>
    public static ExifTag<string> FaxSubaddress { get; } = new(ExifTagValue.FaxSubaddress);

    /// <summary>
    /// Gets the OffsetTime exif tag.
    /// </summary>
    public static ExifTag<string> OffsetTime { get; } = new(ExifTagValue.OffsetTime);

    /// <summary>
    /// Gets the OffsetTimeOriginal exif tag.
    /// </summary>
    public static ExifTag<string> OffsetTimeOriginal { get; } = new(ExifTagValue.OffsetTimeOriginal);

    /// <summary>
    /// Gets the OffsetTimeDigitized exif tag.
    /// </summary>
    public static ExifTag<string> OffsetTimeDigitized { get; } = new(ExifTagValue.OffsetTimeDigitized);

    /// <summary>
    /// Gets the SecurityClassification exif tag.
    /// </summary>
    public static ExifTag<string> SecurityClassification { get; } = new(ExifTagValue.SecurityClassification);

    /// <summary>
    /// Gets the ImageHistory exif tag.
    /// </summary>
    public static ExifTag<string> ImageHistory { get; } = new(ExifTagValue.ImageHistory);

    /// <summary>
    /// Gets the ImageUniqueID exif tag.
    /// </summary>
    public static ExifTag<string> ImageUniqueID { get; } = new(ExifTagValue.ImageUniqueID);

    /// <summary>
    /// Gets the OwnerName exif tag.
    /// </summary>
    public static ExifTag<string> OwnerName { get; } = new(ExifTagValue.OwnerName);

    /// <summary>
    /// Gets the SerialNumber exif tag.
    /// </summary>
    public static ExifTag<string> SerialNumber { get; } = new(ExifTagValue.SerialNumber);

    /// <summary>
    /// Gets the LensMake exif tag.
    /// </summary>
    public static ExifTag<string> LensMake { get; } = new(ExifTagValue.LensMake);

    /// <summary>
    /// Gets the LensModel exif tag.
    /// </summary>
    public static ExifTag<string> LensModel { get; } = new(ExifTagValue.LensModel);

    /// <summary>
    /// Gets the LensSerialNumber exif tag.
    /// </summary>
    public static ExifTag<string> LensSerialNumber { get; } = new(ExifTagValue.LensSerialNumber);

    /// <summary>
    /// Gets the GDALMetadata exif tag.
    /// </summary>
    public static ExifTag<string> GDALMetadata { get; } = new(ExifTagValue.GDALMetadata);

    /// <summary>
    /// Gets the GDALNoData exif tag.
    /// </summary>
    public static ExifTag<string> GDALNoData { get; } = new(ExifTagValue.GDALNoData);

    /// <summary>
    /// Gets the GPSLatitudeRef exif tag.
    /// </summary>
    public static ExifTag<string> GPSLatitudeRef { get; } = new(ExifTagValue.GPSLatitudeRef);

    /// <summary>
    /// Gets the GPSLongitudeRef exif tag.
    /// </summary>
    public static ExifTag<string> GPSLongitudeRef { get; } = new(ExifTagValue.GPSLongitudeRef);

    /// <summary>
    /// Gets the GPSSatellites exif tag.
    /// </summary>
    public static ExifTag<string> GPSSatellites { get; } = new(ExifTagValue.GPSSatellites);

    /// <summary>
    /// Gets the GPSStatus exif tag.
    /// </summary>
    public static ExifTag<string> GPSStatus { get; } = new(ExifTagValue.GPSStatus);

    /// <summary>
    /// Gets the GPSMeasureMode exif tag.
    /// </summary>
    public static ExifTag<string> GPSMeasureMode { get; } = new(ExifTagValue.GPSMeasureMode);

    /// <summary>
    /// Gets the GPSSpeedRef exif tag.
    /// </summary>
    public static ExifTag<string> GPSSpeedRef { get; } = new(ExifTagValue.GPSSpeedRef);

    /// <summary>
    /// Gets the GPSTrackRef exif tag.
    /// </summary>
    public static ExifTag<string> GPSTrackRef { get; } = new(ExifTagValue.GPSTrackRef);

    /// <summary>
    /// Gets the GPSImgDirectionRef exif tag.
    /// </summary>
    public static ExifTag<string> GPSImgDirectionRef { get; } = new(ExifTagValue.GPSImgDirectionRef);

    /// <summary>
    /// Gets the GPSMapDatum exif tag.
    /// </summary>
    public static ExifTag<string> GPSMapDatum { get; } = new(ExifTagValue.GPSMapDatum);

    /// <summary>
    /// Gets the GPSDestLatitudeRef exif tag.
    /// </summary>
    public static ExifTag<string> GPSDestLatitudeRef { get; } = new(ExifTagValue.GPSDestLatitudeRef);

    /// <summary>
    /// Gets the GPSDestLongitudeRef exif tag.
    /// </summary>
    public static ExifTag<string> GPSDestLongitudeRef { get; } = new(ExifTagValue.GPSDestLongitudeRef);

    /// <summary>
    /// Gets the GPSDestBearingRef exif tag.
    /// </summary>
    public static ExifTag<string> GPSDestBearingRef { get; } = new(ExifTagValue.GPSDestBearingRef);

    /// <summary>
    /// Gets the GPSDestDistanceRef exif tag.
    /// </summary>
    public static ExifTag<string> GPSDestDistanceRef { get; } = new(ExifTagValue.GPSDestDistanceRef);

    /// <summary>
    /// Gets the GPSDateStamp exif tag.
    /// </summary>
    public static ExifTag<string> GPSDateStamp { get; } = new(ExifTagValue.GPSDateStamp);
}
