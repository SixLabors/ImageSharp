// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    /// <content/>
    public abstract partial class ExifTag
    {
        /// <summary>
        /// Gets the ImageDescription exif tag.
        /// </summary>
        public static ExifTag<string> ImageDescription { get; } = new ExifTag<string>(ExifTagValue.ImageDescription);

        /// <summary>
        /// Gets the Make exif tag.
        /// </summary>
        public static ExifTag<string> Make { get; } = new ExifTag<string>(ExifTagValue.Make);

        /// <summary>
        /// Gets the Model exif tag.
        /// </summary>
        public static ExifTag<string> Model { get; } = new ExifTag<string>(ExifTagValue.Model);

        /// <summary>
        /// Gets the Software exif tag.
        /// </summary>
        public static ExifTag<string> Software { get; } = new ExifTag<string>(ExifTagValue.Software);

        /// <summary>
        /// Gets the DateTime exif tag.
        /// </summary>
        public static ExifTag<string> DateTime { get; } = new ExifTag<string>(ExifTagValue.DateTime);

        /// <summary>
        /// Gets the Artist exif tag.
        /// </summary>
        public static ExifTag<string> Artist { get; } = new ExifTag<string>(ExifTagValue.Artist);

        /// <summary>
        /// Gets the HostComputer exif tag.
        /// </summary>
        public static ExifTag<string> HostComputer { get; } = new ExifTag<string>(ExifTagValue.HostComputer);

        /// <summary>
        /// Gets the Copyright exif tag.
        /// </summary>
        public static ExifTag<string> Copyright { get; } = new ExifTag<string>(ExifTagValue.Copyright);

        /// <summary>
        /// Gets the DocumentName exif tag.
        /// </summary>
        public static ExifTag<string> DocumentName { get; } = new ExifTag<string>(ExifTagValue.DocumentName);

        /// <summary>
        /// Gets the PageName exif tag.
        /// </summary>
        public static ExifTag<string> PageName { get; } = new ExifTag<string>(ExifTagValue.PageName);

        /// <summary>
        /// Gets the InkNames exif tag.
        /// </summary>
        public static ExifTag<string> InkNames { get; } = new ExifTag<string>(ExifTagValue.InkNames);

        /// <summary>
        /// Gets the TargetPrinter exif tag.
        /// </summary>
        public static ExifTag<string> TargetPrinter { get; } = new ExifTag<string>(ExifTagValue.TargetPrinter);

        /// <summary>
        /// Gets the ImageID exif tag.
        /// </summary>
        public static ExifTag<string> ImageID { get; } = new ExifTag<string>(ExifTagValue.ImageID);

        /// <summary>
        /// Gets the MDLabName exif tag.
        /// </summary>
        public static ExifTag<string> MDLabName { get; } = new ExifTag<string>(ExifTagValue.MDLabName);

        /// <summary>
        /// Gets the MDSampleInfo exif tag.
        /// </summary>
        public static ExifTag<string> MDSampleInfo { get; } = new ExifTag<string>(ExifTagValue.MDSampleInfo);

        /// <summary>
        /// Gets the MDPrepDate exif tag.
        /// </summary>
        public static ExifTag<string> MDPrepDate { get; } = new ExifTag<string>(ExifTagValue.MDPrepDate);

        /// <summary>
        /// Gets the MDPrepTime exif tag.
        /// </summary>
        public static ExifTag<string> MDPrepTime { get; } = new ExifTag<string>(ExifTagValue.MDPrepTime);

        /// <summary>
        /// Gets the MDFileUnits exif tag.
        /// </summary>
        public static ExifTag<string> MDFileUnits => new ExifTag<string>(ExifTagValue.MDFileUnits);

        /// <summary>
        /// Gets the SEMInfo exif tag.
        /// </summary>
        public static ExifTag<string> SEMInfo { get; } = new ExifTag<string>(ExifTagValue.SEMInfo);

        /// <summary>
        /// Gets the SpectralSensitivity exif tag.
        /// </summary>
        public static ExifTag<string> SpectralSensitivity { get; } = new ExifTag<string>(ExifTagValue.SpectralSensitivity);

        /// <summary>
        /// Gets the DateTimeOriginal exif tag.
        /// </summary>
        public static ExifTag<string> DateTimeOriginal { get; } = new ExifTag<string>(ExifTagValue.DateTimeOriginal);

        /// <summary>
        /// Gets the DateTimeDigitized exif tag.
        /// </summary>
        public static ExifTag<string> DateTimeDigitized { get; } = new ExifTag<string>(ExifTagValue.DateTimeDigitized);

        /// <summary>
        /// Gets the SubsecTime exif tag.
        /// </summary>
        public static ExifTag<string> SubsecTime { get; } = new ExifTag<string>(ExifTagValue.SubsecTime);

        /// <summary>
        /// Gets the SubsecTimeOriginal exif tag.
        /// </summary>
        public static ExifTag<string> SubsecTimeOriginal { get; } = new ExifTag<string>(ExifTagValue.SubsecTimeOriginal);

        /// <summary>
        /// Gets the SubsecTimeDigitized exif tag.
        /// </summary>
        public static ExifTag<string> SubsecTimeDigitized { get; } = new ExifTag<string>(ExifTagValue.SubsecTimeDigitized);

        /// <summary>
        /// Gets the RelatedSoundFile exif tag.
        /// </summary>
        public static ExifTag<string> RelatedSoundFile { get; } = new ExifTag<string>(ExifTagValue.RelatedSoundFile);

        /// <summary>
        /// Gets the FaxSubaddress exif tag.
        /// </summary>
        public static ExifTag<string> FaxSubaddress { get; } = new ExifTag<string>(ExifTagValue.FaxSubaddress);

        /// <summary>
        /// Gets the OffsetTime exif tag.
        /// </summary>
        public static ExifTag<string> OffsetTime { get; } = new ExifTag<string>(ExifTagValue.OffsetTime);

        /// <summary>
        /// Gets the OffsetTimeOriginal exif tag.
        /// </summary>
        public static ExifTag<string> OffsetTimeOriginal { get; } = new ExifTag<string>(ExifTagValue.OffsetTimeOriginal);

        /// <summary>
        /// Gets the OffsetTimeDigitized exif tag.
        /// </summary>
        public static ExifTag<string> OffsetTimeDigitized { get; } = new ExifTag<string>(ExifTagValue.OffsetTimeDigitized);

        /// <summary>
        /// Gets the SecurityClassification exif tag.
        /// </summary>
        public static ExifTag<string> SecurityClassification { get; } = new ExifTag<string>(ExifTagValue.SecurityClassification);

        /// <summary>
        /// Gets the ImageHistory exif tag.
        /// </summary>
        public static ExifTag<string> ImageHistory { get; } = new ExifTag<string>(ExifTagValue.ImageHistory);

        /// <summary>
        /// Gets the ImageUniqueID exif tag.
        /// </summary>
        public static ExifTag<string> ImageUniqueID { get; } = new ExifTag<string>(ExifTagValue.ImageUniqueID);

        /// <summary>
        /// Gets the OwnerName exif tag.
        /// </summary>
        public static ExifTag<string> OwnerName { get; } = new ExifTag<string>(ExifTagValue.OwnerName);

        /// <summary>
        /// Gets the SerialNumber exif tag.
        /// </summary>
        public static ExifTag<string> SerialNumber { get; } = new ExifTag<string>(ExifTagValue.SerialNumber);

        /// <summary>
        /// Gets the LensMake exif tag.
        /// </summary>
        public static ExifTag<string> LensMake { get; } = new ExifTag<string>(ExifTagValue.LensMake);

        /// <summary>
        /// Gets the LensModel exif tag.
        /// </summary>
        public static ExifTag<string> LensModel { get; } = new ExifTag<string>(ExifTagValue.LensModel);

        /// <summary>
        /// Gets the LensSerialNumber exif tag.
        /// </summary>
        public static ExifTag<string> LensSerialNumber { get; } = new ExifTag<string>(ExifTagValue.LensSerialNumber);

        /// <summary>
        /// Gets the GDALMetadata exif tag.
        /// </summary>
        public static ExifTag<string> GDALMetadata { get; } = new ExifTag<string>(ExifTagValue.GDALMetadata);

        /// <summary>
        /// Gets the GDALNoData exif tag.
        /// </summary>
        public static ExifTag<string> GDALNoData { get; } = new ExifTag<string>(ExifTagValue.GDALNoData);

        /// <summary>
        /// Gets the GPSLatitudeRef exif tag.
        /// </summary>
        public static ExifTag<string> GPSLatitudeRef { get; } = new ExifTag<string>(ExifTagValue.GPSLatitudeRef);

        /// <summary>
        /// Gets the GPSLongitudeRef exif tag.
        /// </summary>
        public static ExifTag<string> GPSLongitudeRef { get; } = new ExifTag<string>(ExifTagValue.GPSLongitudeRef);

        /// <summary>
        /// Gets the GPSSatellites exif tag.
        /// </summary>
        public static ExifTag<string> GPSSatellites { get; } = new ExifTag<string>(ExifTagValue.GPSSatellites);

        /// <summary>
        /// Gets the GPSStatus exif tag.
        /// </summary>
        public static ExifTag<string> GPSStatus { get; } = new ExifTag<string>(ExifTagValue.GPSStatus);

        /// <summary>
        /// Gets the GPSMeasureMode exif tag.
        /// </summary>
        public static ExifTag<string> GPSMeasureMode { get; } = new ExifTag<string>(ExifTagValue.GPSMeasureMode);

        /// <summary>
        /// Gets the GPSSpeedRef exif tag.
        /// </summary>
        public static ExifTag<string> GPSSpeedRef { get; } = new ExifTag<string>(ExifTagValue.GPSSpeedRef);

        /// <summary>
        /// Gets the GPSTrackRef exif tag.
        /// </summary>
        public static ExifTag<string> GPSTrackRef { get; } = new ExifTag<string>(ExifTagValue.GPSTrackRef);

        /// <summary>
        /// Gets the GPSImgDirectionRef exif tag.
        /// </summary>
        public static ExifTag<string> GPSImgDirectionRef { get; } = new ExifTag<string>(ExifTagValue.GPSImgDirectionRef);

        /// <summary>
        /// Gets the GPSMapDatum exif tag.
        /// </summary>
        public static ExifTag<string> GPSMapDatum { get; } = new ExifTag<string>(ExifTagValue.GPSMapDatum);

        /// <summary>
        /// Gets the GPSDestLatitudeRef exif tag.
        /// </summary>
        public static ExifTag<string> GPSDestLatitudeRef { get; } = new ExifTag<string>(ExifTagValue.GPSDestLatitudeRef);

        /// <summary>
        /// Gets the GPSDestLongitudeRef exif tag.
        /// </summary>
        public static ExifTag<string> GPSDestLongitudeRef { get; } = new ExifTag<string>(ExifTagValue.GPSDestLongitudeRef);

        /// <summary>
        /// Gets the GPSDestBearingRef exif tag.
        /// </summary>
        public static ExifTag<string> GPSDestBearingRef { get; } = new ExifTag<string>(ExifTagValue.GPSDestBearingRef);

        /// <summary>
        /// Gets the GPSDestDistanceRef exif tag.
        /// </summary>
        public static ExifTag<string> GPSDestDistanceRef { get; } = new ExifTag<string>(ExifTagValue.GPSDestDistanceRef);

        /// <summary>
        /// Gets the GPSDateStamp exif tag.
        /// </summary>
        public static ExifTag<string> GPSDateStamp { get; } = new ExifTag<string>(ExifTagValue.GPSDateStamp);
    }
}
