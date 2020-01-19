// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

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
