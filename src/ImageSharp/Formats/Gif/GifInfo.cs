// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.MetaData;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// Contains information about the bmp including dimensions, pixel type information and additional metadata.
    /// </summary>
    public class GifInfo : ImageInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GifInfo" /> class.
        /// </summary>
        /// <param name="colorTableMode">The color table mode.</param>
        /// <param name="pixelType">The image pixel type information.</param>
        /// <param name="size">The size of the image in pixels.</param>
        /// <param name="metaData">The images metadata.</param>
        internal GifInfo(
            GifColorTableMode colorTableMode,
            PixelTypeInfo pixelType,
            Size size,
            ImageMetaData metaData)
            : base(pixelType, size, metaData) => this.ColorTableMode = colorTableMode;

        /// <summary>
        /// Gets the color table mode.
        /// </summary>
        public GifColorTableMode ColorTableMode { get; }
    }
}
