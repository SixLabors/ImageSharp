// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.MetaData;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Contains information about the bmp including dimensions, pixel type information and additional metadata.
    /// </summary>
    public class BmpInfo : ImageInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BmpInfo" /> class.
        /// </summary>
        /// <param name="pixelType">The image pixel type information.</param>
        /// <param name="size">The size of the image in pixels.</param>
        /// <param name="metaData">The images metadata.</param>
        internal BmpInfo(PixelTypeInfo pixelType, Size size, ImageMetaData metaData)
            : base(pixelType, size, metaData)
        {
        }
    }
}