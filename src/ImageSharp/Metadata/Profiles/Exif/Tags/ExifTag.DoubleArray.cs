// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    /// <content/>
    public abstract partial class ExifTag
    {
        /// <summary>
        /// Gets the PixelScale exif tag.
        /// </summary>
        public static ExifTag<double[]> PixelScale { get; } = new ExifTag<double[]>(ExifTagValue.PixelScale);

        /// <summary>
        /// Gets the IntergraphMatrix exif tag.
        /// </summary>
        public static ExifTag<double[]> IntergraphMatrix { get; } = new ExifTag<double[]>(ExifTagValue.IntergraphMatrix);

        /// <summary>
        /// Gets the ModelTiePoint exif tag.
        /// </summary>
        public static ExifTag<double[]> ModelTiePoint { get; } = new ExifTag<double[]>(ExifTagValue.ModelTiePoint);

        /// <summary>
        /// Gets the ModelTransform exif tag.
        /// </summary>
        public static ExifTag<double[]> ModelTransform { get; } = new ExifTag<double[]>(ExifTagValue.ModelTransform);
    }
}
