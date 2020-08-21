// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    /// <content/>
    public abstract partial class ExifTag
    {
        /// <summary>
        /// Gets the StripOffsets exif tag.
        /// </summary>
        public static ExifTag<Number[]> StripOffsets { get; } = new ExifTag<Number[]>(ExifTagValue.StripOffsets);

        /// <summary>
        /// Gets the TileByteCounts exif tag.
        /// </summary>
        public static ExifTag<Number[]> TileByteCounts { get; } = new ExifTag<Number[]>(ExifTagValue.TileByteCounts);

        /// <summary>
        /// Gets the ImageLayer exif tag.
        /// </summary>
        public static ExifTag<Number[]> ImageLayer { get; } = new ExifTag<Number[]>(ExifTagValue.ImageLayer);
    }
}
