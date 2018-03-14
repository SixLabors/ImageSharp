// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.MetaData;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Encapsulates properties that descibe basic image information including dimensions, pixel type information
    /// and additional metadata
    /// </summary>
    public interface IImageInfo
    {
        /// <summary>
        /// Gets information about the image pixels.
        /// </summary>
        PixelTypeInfo PixelType { get; }

        /// <summary>
        /// Gets the width.
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Gets the height.
        /// </summary>
        int Height { get; }

        /// <summary>
        /// Gets the metadata of the image.
        /// </summary>
        ImageMetaData MetaData { get; }
    }
}