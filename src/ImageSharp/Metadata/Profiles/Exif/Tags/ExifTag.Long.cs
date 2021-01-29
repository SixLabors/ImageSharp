// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    /// <content/>
    public abstract partial class ExifTag
    {
        /// <summary>
        /// Gets the SubfileType exif tag.
        /// </summary>
        public static ExifTag<uint> SubfileType { get; } = new ExifTag<uint>(ExifTagValue.SubfileType);

        /// <summary>
        /// Gets the SubIFDOffset exif tag.
        /// </summary>
        public static ExifTag<uint> SubIFDOffset { get; } = new ExifTag<uint>(ExifTagValue.SubIFDOffset);

        /// <summary>
        /// Gets the GPSIFDOffset exif tag.
        /// </summary>
        public static ExifTag<uint> GPSIFDOffset { get; } = new ExifTag<uint>(ExifTagValue.GPSIFDOffset);

        /// <summary>
        /// Gets the T4Options exif tag.
        /// </summary>
        public static ExifTag<uint> T4Options { get; } = new ExifTag<uint>(ExifTagValue.T4Options);

        /// <summary>
        /// Gets the T6Options exif tag.
        /// </summary>
        public static ExifTag<uint> T6Options { get; } = new ExifTag<uint>(ExifTagValue.T6Options);

        /// <summary>
        /// Gets the XClipPathUnits exif tag.
        /// </summary>
        public static ExifTag<uint> XClipPathUnits { get; } = new ExifTag<uint>(ExifTagValue.XClipPathUnits);

        /// <summary>
        /// Gets the YClipPathUnits exif tag.
        /// </summary>
        public static ExifTag<uint> YClipPathUnits { get; } = new ExifTag<uint>(ExifTagValue.YClipPathUnits);

        /// <summary>
        /// Gets the ProfileType exif tag.
        /// </summary>
        public static ExifTag<uint> ProfileType { get; } = new ExifTag<uint>(ExifTagValue.ProfileType);

        /// <summary>
        /// Gets the CodingMethods exif tag.
        /// </summary>
        public static ExifTag<uint> CodingMethods { get; } = new ExifTag<uint>(ExifTagValue.CodingMethods);

        /// <summary>
        /// Gets the T82ptions exif tag.
        /// </summary>
        public static ExifTag<uint> T82ptions { get; } = new ExifTag<uint>(ExifTagValue.T82ptions);

        /// <summary>
        /// Gets the JPEGInterchangeFormat exif tag.
        /// </summary>
        public static ExifTag<uint> JPEGInterchangeFormat { get; } = new ExifTag<uint>(ExifTagValue.JPEGInterchangeFormat);

        /// <summary>
        /// Gets the JPEGInterchangeFormatLength exif tag.
        /// </summary>
        public static ExifTag<uint> JPEGInterchangeFormatLength { get; } = new ExifTag<uint>(ExifTagValue.JPEGInterchangeFormatLength);

        /// <summary>
        /// Gets the MDFileTag exif tag.
        /// </summary>
        public static ExifTag<uint> MDFileTag { get; } = new ExifTag<uint>(ExifTagValue.MDFileTag);

        /// <summary>
        /// Gets the StandardOutputSensitivity exif tag.
        /// </summary>
        public static ExifTag<uint> StandardOutputSensitivity { get; } = new ExifTag<uint>(ExifTagValue.StandardOutputSensitivity);

        /// <summary>
        /// Gets the RecommendedExposureIndex exif tag.
        /// </summary>
        public static ExifTag<uint> RecommendedExposureIndex { get; } = new ExifTag<uint>(ExifTagValue.RecommendedExposureIndex);

        /// <summary>
        /// Gets the ISOSpeed exif tag.
        /// </summary>
        public static ExifTag<uint> ISOSpeed { get; } = new ExifTag<uint>(ExifTagValue.ISOSpeed);

        /// <summary>
        /// Gets the ISOSpeedLatitudeyyy exif tag.
        /// </summary>
        public static ExifTag<uint> ISOSpeedLatitudeyyy { get; } = new ExifTag<uint>(ExifTagValue.ISOSpeedLatitudeyyy);

        /// <summary>
        /// Gets the ISOSpeedLatitudezzz exif tag.
        /// </summary>
        public static ExifTag<uint> ISOSpeedLatitudezzz { get; } = new ExifTag<uint>(ExifTagValue.ISOSpeedLatitudezzz);

        /// <summary>
        /// Gets the FaxRecvParams exif tag.
        /// </summary>
        public static ExifTag<uint> FaxRecvParams { get; } = new ExifTag<uint>(ExifTagValue.FaxRecvParams);

        /// <summary>
        /// Gets the FaxRecvTime exif tag.
        /// </summary>
        public static ExifTag<uint> FaxRecvTime { get; } = new ExifTag<uint>(ExifTagValue.FaxRecvTime);

        /// <summary>
        /// Gets the ImageNumber exif tag.
        /// </summary>
        public static ExifTag<uint> ImageNumber { get; } = new ExifTag<uint>(ExifTagValue.ImageNumber);
    }
}
