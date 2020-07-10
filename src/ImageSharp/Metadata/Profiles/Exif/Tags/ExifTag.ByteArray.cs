// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    /// <content/>
    public abstract partial class ExifTag
    {
        /// <summary>
        /// Gets the ClipPath exif tag.
        /// </summary>
        public static ExifTag<byte[]> ClipPath => new ExifTag<byte[]>(ExifTagValue.ClipPath);

        /// <summary>
        /// Gets the VersionYear exif tag.
        /// </summary>
        public static ExifTag<byte[]> VersionYear => new ExifTag<byte[]>(ExifTagValue.VersionYear);

        /// <summary>
        /// Gets the XMP exif tag.
        /// </summary>
        public static ExifTag<byte[]> XMP => new ExifTag<byte[]>(ExifTagValue.XMP);

        /// <summary>
        /// Gets the CFAPattern2 exif tag.
        /// </summary>
        public static ExifTag<byte[]> CFAPattern2 => new ExifTag<byte[]>(ExifTagValue.CFAPattern2);

        /// <summary>
        /// Gets the TIFFEPStandardID exif tag.
        /// </summary>
        public static ExifTag<byte[]> TIFFEPStandardID => new ExifTag<byte[]>(ExifTagValue.TIFFEPStandardID);

        /// <summary>
        /// Gets the XPTitle exif tag.
        /// </summary>
        public static ExifTag<byte[]> XPTitle => new ExifTag<byte[]>(ExifTagValue.XPTitle);

        /// <summary>
        /// Gets the XPComment exif tag.
        /// </summary>
        public static ExifTag<byte[]> XPComment => new ExifTag<byte[]>(ExifTagValue.XPComment);

        /// <summary>
        /// Gets the XPAuthor exif tag.
        /// </summary>
        public static ExifTag<byte[]> XPAuthor => new ExifTag<byte[]>(ExifTagValue.XPAuthor);

        /// <summary>
        /// Gets the XPKeywords exif tag.
        /// </summary>
        public static ExifTag<byte[]> XPKeywords => new ExifTag<byte[]>(ExifTagValue.XPKeywords);

        /// <summary>
        /// Gets the XPSubject exif tag.
        /// </summary>
        public static ExifTag<byte[]> XPSubject => new ExifTag<byte[]>(ExifTagValue.XPSubject);

        /// <summary>
        /// Gets the GPSVersionID exif tag.
        /// </summary>
        public static ExifTag<byte[]> GPSVersionID => new ExifTag<byte[]>(ExifTagValue.GPSVersionID);
    }
}
