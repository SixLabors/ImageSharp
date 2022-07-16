// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    /// <content/>
    public abstract partial class ExifTag
    {
        /// <summary>
        /// Gets the FaxProfile exif tag.
        /// </summary>
        public static ExifTag<byte> FaxProfile { get; } = new ExifTag<byte>(ExifTagValue.FaxProfile);

        /// <summary>
        /// Gets the ModeNumber exif tag.
        /// </summary>
        public static ExifTag<byte> ModeNumber { get; } = new ExifTag<byte>(ExifTagValue.ModeNumber);

        /// <summary>
        /// Gets the GPSAltitudeRef exif tag.
        /// </summary>
        public static ExifTag<byte> GPSAltitudeRef { get; } = new ExifTag<byte>(ExifTagValue.GPSAltitudeRef);
    }
}
