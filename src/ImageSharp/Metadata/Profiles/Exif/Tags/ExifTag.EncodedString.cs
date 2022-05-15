// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    /// <content/>
    public abstract partial class ExifTag
    {
        /// <summary>
        /// Gets the UserComment exif tag.
        /// </summary>
        public static ExifTag<EncodedString> UserComment { get; } = new ExifTag<EncodedString>(ExifTagValue.UserComment);

        /// <summary>
        /// Gets the GPSProcessingMethod exif tag.
        /// </summary>
        public static ExifTag<EncodedString> GPSProcessingMethod { get; } = new ExifTag<EncodedString>(ExifTagValue.GPSProcessingMethod);

        /// <summary>
        /// Gets the GPSAreaInformation exif tag.
        /// </summary>
        public static ExifTag<EncodedString> GPSAreaInformation { get; } = new ExifTag<EncodedString>(ExifTagValue.GPSAreaInformation);
    }
}
