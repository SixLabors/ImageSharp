// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="ImageMetadata"/> type.
    /// </summary>
    public static partial class MetadataExtensions
    {
        /// <summary>
        /// Gets the orientation of the image.
        /// </summary>
        /// <param name="metadata">The metadata.</param>
        /// <returns>The <see cref="OrientationMode"/></returns>
        public static OrientationMode GetOrientation(this ImageMetadata metadata) => (OrientationMode)(metadata.ExifProfile?.GetValue(ExifTag.Orientation)?.Value ?? 0);
    }
}
