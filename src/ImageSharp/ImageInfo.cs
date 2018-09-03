// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.MetaData;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Contains information about the image including dimensions, pixel type information and additional metadata
    /// </summary>
    public abstract class ImageInfo : IImageInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageInfo"/> class.
        /// </summary>
        /// <param name="pixelType">The image pixel type information.</param>
        /// <param name="size">The size of the image in pixels.</param>
        /// <param name="metaData">The images metadata.</param>
        protected ImageInfo(PixelTypeInfo pixelType, Size size, ImageMetaData metaData)
        {
            this.PixelType = pixelType;
            this.Width = size.Width;
            this.Height = size.Height;
            this.MetaData = metaData;
        }

        /// <inheritdoc />
        public PixelTypeInfo PixelType { get; }

        /// <inheritdoc />
        public int Width { get; }

        /// <inheritdoc />
        public int Height { get; }

        /// <inheritdoc />
        public ImageMetaData MetaData { get; }
    }
}