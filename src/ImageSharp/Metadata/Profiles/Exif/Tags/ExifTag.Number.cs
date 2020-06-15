// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    /// <content/>
    public abstract partial class ExifTag
    {
        /// <summary>
        /// Gets the ImageWidth exif tag.
        /// </summary>
        public static ExifTag<Number> ImageWidth { get; } = new ExifTag<Number>(ExifTagValue.ImageWidth);

        /// <summary>
        /// Gets the ImageLength exif tag.
        /// </summary>
        public static ExifTag<Number> ImageLength { get; } = new ExifTag<Number>(ExifTagValue.ImageLength);

        /// <summary>
        /// Gets the TileWidth exif tag.
        /// </summary>
        public static ExifTag<Number> TileWidth { get; } = new ExifTag<Number>(ExifTagValue.TileWidth);

        /// <summary>
        /// Gets the TileLength exif tag.
        /// </summary>
        public static ExifTag<Number> TileLength { get; } = new ExifTag<Number>(ExifTagValue.TileLength);

        /// <summary>
        /// Gets the BadFaxLines exif tag.
        /// </summary>
        public static ExifTag<Number> BadFaxLines { get; } = new ExifTag<Number>(ExifTagValue.BadFaxLines);

        /// <summary>
        /// Gets the ConsecutiveBadFaxLines exif tag.
        /// </summary>
        public static ExifTag<Number> ConsecutiveBadFaxLines { get; } = new ExifTag<Number>(ExifTagValue.ConsecutiveBadFaxLines);

        /// <summary>
        /// Gets the PixelXDimension exif tag.
        /// </summary>
        public static ExifTag<Number> PixelXDimension { get; } = new ExifTag<Number>(ExifTagValue.PixelXDimension);

        /// <summary>
        /// Gets the PixelYDimension exif tag.
        /// </summary>
        public static ExifTag<Number> PixelYDimension { get; } = new ExifTag<Number>(ExifTagValue.PixelYDimension);
    }
}
