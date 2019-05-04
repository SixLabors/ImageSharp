// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <content>
    /// Adds static methods allowing wrapping an existing memory area as an image.
    /// </content>
    public abstract partial class Image
    {
        /// <summary>
        /// Wraps an existing contiguous memory area of 'width' x 'height' pixels,
        /// allowing to view/manipulate it as an ImageSharp <see cref="Image{TPixel}"/> instance.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type</typeparam>
        /// <param name="config">The <see cref="ImageSharp.Configuration"/></param>
        /// <param name="pixelMemory">The pixel memory.</param>
        /// <param name="width">The width of the memory image.</param>
        /// <param name="height">The height of the memory image.</param>
        /// <param name="metadata">The <see cref="ImageMetadata"/>.</param>
        /// <returns>An <see cref="Image{TPixel}"/> instance</returns>
        public static Image<TPixel> WrapMemory<TPixel>(
            Configuration config,
            Memory<TPixel> pixelMemory,
            int width,
            int height,
            ImageMetadata metadata)
            where TPixel : struct, IPixel<TPixel>
        {
            var memorySource = new MemorySource<TPixel>(pixelMemory);
            return new Image<TPixel>(config, memorySource, width, height, metadata);
        }

        /// <summary>
        /// Wraps an existing contiguous memory area of 'width' x 'height' pixels,
        /// allowing to view/manipulate it as an ImageSharp <see cref="Image{TPixel}"/> instance.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type</typeparam>
        /// <param name="config">The <see cref="ImageSharp.Configuration"/></param>
        /// <param name="pixelMemory">The pixel memory.</param>
        /// <param name="width">The width of the memory image.</param>
        /// <param name="height">The height of the memory image.</param>
        /// <returns>An <see cref="Image{TPixel}"/> instance.</returns>
        public static Image<TPixel> WrapMemory<TPixel>(
            Configuration config,
            Memory<TPixel> pixelMemory,
            int width,
            int height)
            where TPixel : struct, IPixel<TPixel>
        {
            return WrapMemory(config, pixelMemory, width, height, new ImageMetadata());
        }

        /// <summary>
        /// Wraps an existing contiguous memory area of 'width' x 'height' pixels,
        /// allowing to view/manipulate it as an ImageSharp <see cref="Image{TPixel}"/> instance.
        /// The memory is being observed, the caller remains responsible for managing it's lifecycle.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="pixelMemory">The pixel memory.</param>
        /// <param name="width">The width of the memory image.</param>
        /// <param name="height">The height of the memory image.</param>
        /// <returns>An <see cref="Image{TPixel}"/> instance.</returns>
        public static Image<TPixel> WrapMemory<TPixel>(
            Memory<TPixel> pixelMemory,
            int width,
            int height)
            where TPixel : struct, IPixel<TPixel>
        {
            return WrapMemory(Configuration.Default, pixelMemory, width, height);
        }

        /// <summary>
        /// Wraps an existing contiguous memory area of 'width' x 'height' pixels,
        /// allowing to view/manipulate it as an ImageSharp <see cref="Image{TPixel}"/> instance.
        /// The ownership of the <paramref name="pixelMemoryOwner"/> is being transferred to the new <see cref="Image{TPixel}"/> instance,
        /// meaning that the caller is not allowed to dispose <paramref name="pixelMemoryOwner"/>.
        /// It will be disposed together with the result image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type</typeparam>
        /// <param name="config">The <see cref="ImageSharp.Configuration"/></param>
        /// <param name="pixelMemoryOwner">The <see cref="IMemoryOwner{T}"/> that is being transferred to the image</param>
        /// <param name="width">The width of the memory image.</param>
        /// <param name="height">The height of the memory image.</param>
        /// <param name="metadata">The <see cref="ImageMetadata"/></param>
        /// <returns>An <see cref="Image{TPixel}"/> instance</returns>
        public static Image<TPixel> WrapMemory<TPixel>(
            Configuration config,
            IMemoryOwner<TPixel> pixelMemoryOwner,
            int width,
            int height,
            ImageMetadata metadata)
            where TPixel : struct, IPixel<TPixel>
        {
            var memorySource = new MemorySource<TPixel>(pixelMemoryOwner, false);
            return new Image<TPixel>(config, memorySource, width, height, metadata);
        }

        /// <summary>
        /// Wraps an existing contiguous memory area of 'width' x 'height' pixels,
        /// allowing to view/manipulate it as an ImageSharp <see cref="Image{TPixel}"/> instance.
        /// The ownership of the <paramref name="pixelMemoryOwner"/> is being transferred to the new <see cref="Image{TPixel}"/> instance,
        /// meaning that the caller is not allowed to dispose <paramref name="pixelMemoryOwner"/>.
        /// It will be disposed together with the result image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="config">The <see cref="ImageSharp.Configuration"/></param>
        /// <param name="pixelMemoryOwner">The <see cref="IMemoryOwner{T}"/> that is being transferred to the image.</param>
        /// <param name="width">The width of the memory image.</param>
        /// <param name="height">The height of the memory image.</param>
        /// <returns>An <see cref="Image{TPixel}"/> instance</returns>
        public static Image<TPixel> WrapMemory<TPixel>(
            Configuration config,
            IMemoryOwner<TPixel> pixelMemoryOwner,
            int width,
            int height)
            where TPixel : struct, IPixel<TPixel>
        {
            return WrapMemory(config, pixelMemoryOwner, width, height, new ImageMetadata());
        }

        /// <summary>
        /// Wraps an existing contiguous memory area of 'width' x 'height' pixels,
        /// allowing to view/manipulate it as an ImageSharp <see cref="Image{TPixel}"/> instance.
        /// The ownership of the <paramref name="pixelMemoryOwner"/> is being transferred to the new <see cref="Image{TPixel}"/> instance,
        /// meaning that the caller is not allowed to dispose <paramref name="pixelMemoryOwner"/>.
        /// It will be disposed together with the result image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type</typeparam>
        /// <param name="pixelMemoryOwner">The <see cref="IMemoryOwner{T}"/> that is being transferred to the image.</param>
        /// <param name="width">The width of the memory image.</param>
        /// <param name="height">The height of the memory image.</param>
        /// <returns>An <see cref="Image{TPixel}"/> instance.</returns>
        public static Image<TPixel> WrapMemory<TPixel>(
            IMemoryOwner<TPixel> pixelMemoryOwner,
            int width,
            int height)
            where TPixel : struct, IPixel<TPixel>
        {
            return WrapMemory(Configuration.Default, pixelMemoryOwner, width, height);
        }
    }
}