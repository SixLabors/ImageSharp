// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
        /// Gets the IPTC exif tag.
        /// </summary>
        public static ExifTag<byte[]> IPTC => new ExifTag<byte[]>(ExifTagValue.IPTC);

        /// <summary>
        /// Gets the IccProfile exif tag.
        /// </summary>
        public static ExifTag<byte[]> IccProfile => new ExifTag<byte[]>(ExifTagValue.IccProfile);

        /// <summary>
        /// Gets the CFAPattern2 exif tag.
        /// </summary>
        public static ExifTag<byte[]> CFAPattern2 => new ExifTag<byte[]>(ExifTagValue.CFAPattern2);

        /// <summary>
        /// Gets the TIFFEPStandardID exif tag.
        /// </summary>
        public static ExifTag<byte[]> TIFFEPStandardID => new ExifTag<byte[]>(ExifTagValue.TIFFEPStandardID);

        /// <summary>
        /// Gets the GPSVersionID exif tag.
        /// </summary>
        public static ExifTag<byte[]> GPSVersionID => new ExifTag<byte[]>(ExifTagValue.GPSVersionID);
    }
}
