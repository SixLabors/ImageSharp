// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Image features of a VP8X image.
    /// </summary>
    public class WebPFeatures
    {
        /// <summary>
        /// Gets or sets whether this image has a ICC Profile.
        /// </summary>
        public bool IccProfile { get; set; }

        /// <summary>
        /// Gets or sets whether this image has a alpha channel.
        /// </summary>
        public bool Alpha { get; set; }

        /// <summary>
        /// Gets or sets whether this image has a EXIF Profile.
        /// </summary>
        public bool ExifProfile { get; set; }

        /// <summary>
        /// Gets or sets whether this image has XMP Metadata.
        /// </summary>
        public bool XmpMetaData { get; set; }

        /// <summary>
        /// Gets or sets whether this image is a animation.
        /// </summary>
        public bool Animation { get; set; }
    }
}
