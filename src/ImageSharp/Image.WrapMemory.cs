// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;

namespace SixLabors.ImageSharp
{
    /// <content>
    /// Adds static methods allowing wrapping an existing memory area as an image.
    /// </content>
    public static partial class Image
    {
        // TODO: This is a WIP API, should be public when finished.

        /// <summary>
        /// Wraps an existing contigous memory area of 'width'x'height' pixels,
        /// allowing to view/manipulate it as an ImageSharp <see cref="Image{TPixel}"/> instance.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type</typeparam>
        /// <param name="config">The <see cref="Configuration"/></param>
        /// <param name="pixelMemory">The pixel memory</param>
        /// <param name="width">The width of the memory image</param>
        /// <param name="height">The height of the memory image</param>
        /// <param name="metaData">The <see cref="ImageMetaData"/></param>
        /// <returns>An <see cref="Image{TPixel}"/> instance</returns>
        internal static Image<TPixel> WrapMemory<TPixel>(
            Configuration config,
            Memory<TPixel> pixelMemory,
            int width,
            int height,
            ImageMetaData metaData)
            where TPixel : struct, IPixel<TPixel>
        {
            return new Image<TPixel>(config, pixelMemory, width, height, metaData);
        }

        /// <summary>
        /// Wraps an existing contigous memory area of 'width'x'height' pixels,
        /// allowing to view/manipulate it as an ImageSharp <see cref="Image{TPixel}"/> instance.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type</typeparam>
        /// <param name="pixelMemory">The pixel memory</param>
        /// <param name="width">The width of the memory image</param>
        /// <param name="height">The height of the memory image</param>
        /// <returns>An <see cref="Image{TPixel}"/> instance</returns>
        internal static Image<TPixel> WrapMemory<TPixel>(
            Memory<TPixel> pixelMemory,
            int width,
            int height)
            where TPixel : struct, IPixel<TPixel>
        {
            return WrapMemory(Configuration.Default, pixelMemory, width, height, new ImageMetaData());
        }
    }
}