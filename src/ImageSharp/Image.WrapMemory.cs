// Copyright (c) Six Labors.
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
        /// allowing to view/manipulate it as an <see cref="Image{TPixel}"/> instance.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type</typeparam>
        /// <param name="configuration">The <see cref="Configuration"/></param>
        /// <param name="pixelMemory">The pixel memory.</param>
        /// <param name="width">The width of the memory image.</param>
        /// <param name="height">The height of the memory image.</param>
        /// <param name="metadata">The <see cref="ImageMetadata"/>.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The metadata is null.</exception>
        /// <returns>An <see cref="Image{TPixel}"/> instance</returns>
        public static Image<TPixel> WrapMemory<TPixel>(
            Configuration configuration,
            Memory<TPixel> pixelMemory,
            int width,
            int height,
            ImageMetadata metadata)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.NotNull(metadata, nameof(metadata));
            Guard.IsTrue(pixelMemory.Length == width * height, nameof(pixelMemory), "The length of the input memory doesn't match the specified image size");

            var memorySource = MemoryGroup<TPixel>.Wrap(pixelMemory);
            return new Image<TPixel>(configuration, memorySource, width, height, metadata);
        }

        /// <summary>
        /// Wraps an existing contiguous memory area of 'width' x 'height' pixels,
        /// allowing to view/manipulate it as an <see cref="Image{TPixel}"/> instance.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type</typeparam>
        /// <param name="configuration">The <see cref="Configuration"/></param>
        /// <param name="pixelMemory">The pixel memory.</param>
        /// <param name="width">The width of the memory image.</param>
        /// <param name="height">The height of the memory image.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <returns>An <see cref="Image{TPixel}"/> instance.</returns>
        public static Image<TPixel> WrapMemory<TPixel>(
            Configuration configuration,
            Memory<TPixel> pixelMemory,
            int width,
            int height)
            where TPixel : unmanaged, IPixel<TPixel>
            => WrapMemory(configuration, pixelMemory, width, height, new ImageMetadata());

        /// <summary>
        /// Wraps an existing contiguous memory area of 'width' x 'height' pixels,
        /// allowing to view/manipulate it as an <see cref="Image{TPixel}"/> instance.
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
            where TPixel : unmanaged, IPixel<TPixel>
            => WrapMemory(Configuration.Default, pixelMemory, width, height);

        /// <summary>
        /// Wraps an existing contiguous memory area of 'width' x 'height' pixels,
        /// allowing to view/manipulate it as an <see cref="Image{TPixel}"/> instance.
        /// The ownership of the <paramref name="pixelMemoryOwner"/> is being transferred to the new <see cref="Image{TPixel}"/> instance,
        /// meaning that the caller is not allowed to dispose <paramref name="pixelMemoryOwner"/>.
        /// It will be disposed together with the result image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type</typeparam>
        /// <param name="configuration">The <see cref="Configuration"/></param>
        /// <param name="pixelMemoryOwner">The <see cref="IMemoryOwner{T}"/> that is being transferred to the image</param>
        /// <param name="width">The width of the memory image.</param>
        /// <param name="height">The height of the memory image.</param>
        /// <param name="metadata">The <see cref="ImageMetadata"/></param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The metadata is null.</exception>
        /// <returns>An <see cref="Image{TPixel}"/> instance</returns>
        public static Image<TPixel> WrapMemory<TPixel>(
            Configuration configuration,
            IMemoryOwner<TPixel> pixelMemoryOwner,
            int width,
            int height,
            ImageMetadata metadata)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.NotNull(metadata, nameof(metadata));
            Guard.IsTrue(pixelMemoryOwner.Memory.Length == width * height, nameof(pixelMemoryOwner), "The length of the input memory doesn't match the specified image size");

            var memorySource = MemoryGroup<TPixel>.Wrap(pixelMemoryOwner);
            return new Image<TPixel>(configuration, memorySource, width, height, metadata);
        }

        /// <summary>
        /// Wraps an existing contiguous memory area of 'width' x 'height' pixels,
        /// allowing to view/manipulate it as an <see cref="Image{TPixel}"/> instance.
        /// The ownership of the <paramref name="pixelMemoryOwner"/> is being transferred to the new <see cref="Image{TPixel}"/> instance,
        /// meaning that the caller is not allowed to dispose <paramref name="pixelMemoryOwner"/>.
        /// It will be disposed together with the result image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="configuration">The <see cref="Configuration"/></param>
        /// <param name="pixelMemoryOwner">The <see cref="IMemoryOwner{T}"/> that is being transferred to the image.</param>
        /// <param name="width">The width of the memory image.</param>
        /// <param name="height">The height of the memory image.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <returns>An <see cref="Image{TPixel}"/> instance</returns>
        public static Image<TPixel> WrapMemory<TPixel>(
            Configuration configuration,
            IMemoryOwner<TPixel> pixelMemoryOwner,
            int width,
            int height)
            where TPixel : unmanaged, IPixel<TPixel>
            => WrapMemory(configuration, pixelMemoryOwner, width, height, new ImageMetadata());

        /// <summary>
        /// Wraps an existing contiguous memory area of 'width' x 'height' pixels,
        /// allowing to view/manipulate it as an <see cref="Image{TPixel}"/> instance.
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
            where TPixel : unmanaged, IPixel<TPixel>
            => WrapMemory(Configuration.Default, pixelMemoryOwner, width, height);

        /// <summary>
        /// Wraps an existing contiguous memory area of 'width' x 'height' pixels,
        /// allowing to view/manipulate it as an <see cref="Image{TPixel}"/> instance.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type</typeparam>
        /// <param name="configuration">The <see cref="Configuration"/></param>
        /// <param name="byteMemory">The byte memory representing the pixel data.</param>
        /// <param name="width">The width of the memory image.</param>
        /// <param name="height">The height of the memory image.</param>
        /// <param name="metadata">The <see cref="ImageMetadata"/>.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The metadata is null.</exception>
        /// <returns>An <see cref="Image{TPixel}"/> instance</returns>
        public static Image<TPixel> WrapMemory<TPixel>(
            Configuration configuration,
            Memory<byte> byteMemory,
            int width,
            int height,
            ImageMetadata metadata)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.NotNull(metadata, nameof(metadata));

            var memoryManager = new ByteMemoryManager<TPixel>(byteMemory);

            Guard.IsTrue(memoryManager.Memory.Length == width * height, nameof(byteMemory), "The length of the input memory doesn't match the specified image size");

            var memorySource = MemoryGroup<TPixel>.Wrap(memoryManager.Memory);
            return new Image<TPixel>(configuration, memorySource, width, height, metadata);
        }

        /// <summary>
        /// Wraps an existing contiguous memory area of 'width' x 'height' pixels,
        /// allowing to view/manipulate it as an <see cref="Image{TPixel}"/> instance.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type</typeparam>
        /// <param name="configuration">The <see cref="Configuration"/></param>
        /// <param name="byteMemory">The byte memory representing the pixel data.</param>
        /// <param name="width">The width of the memory image.</param>
        /// <param name="height">The height of the memory image.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <returns>An <see cref="Image{TPixel}"/> instance.</returns>
        public static Image<TPixel> WrapMemory<TPixel>(
            Configuration configuration,
            Memory<byte> byteMemory,
            int width,
            int height)
            where TPixel : unmanaged, IPixel<TPixel>
            => WrapMemory<TPixel>(configuration, byteMemory, width, height, new ImageMetadata());

        /// <summary>
        /// Wraps an existing contiguous memory area of 'width' x 'height' pixels,
        /// allowing to view/manipulate it as an <see cref="Image{TPixel}"/> instance.
        /// The memory is being observed, the caller remains responsible for managing it's lifecycle.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="byteMemory">The byte memory representing the pixel data.</param>
        /// <param name="width">The width of the memory image.</param>
        /// <param name="height">The height of the memory image.</param>
        /// <returns>An <see cref="Image{TPixel}"/> instance.</returns>
        public static Image<TPixel> WrapMemory<TPixel>(
            Memory<byte> byteMemory,
            int width,
            int height)
            where TPixel : unmanaged, IPixel<TPixel>
            => WrapMemory<TPixel>(Configuration.Default, byteMemory, width, height);
    }
}
