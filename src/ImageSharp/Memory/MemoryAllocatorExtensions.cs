// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;
using System.IO;
using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Extension methods for <see cref="MemoryAllocator"/>.
    /// </summary>
    public static class MemoryAllocatorExtensions
    {
        /// <summary>
        /// Allocates a buffer of value type objects interpreted as a 2D region
        /// of <paramref name="width"/> x <paramref name="height"/> elements.
        /// </summary>
        /// <typeparam name="T">The type of buffer items to allocate.</typeparam>
        /// <param name="memoryAllocator">The memory allocator.</param>
        /// <param name="width">The buffer width.</param>
        /// <param name="height">The buffer height.</param>
        /// <param name="options">The allocation options.</param>
        /// <returns>The <see cref="Buffer2D{T}"/>.</returns>
        public static Buffer2D<T> Allocate2D<T>(
            this MemoryAllocator memoryAllocator,
            int width,
            int height,
            AllocationOptions options = AllocationOptions.None)
            where T : struct
        {
            long groupLength = (long)width * height;
            MemoryGroup<T> memoryGroup = memoryAllocator.AllocateGroup<T>(groupLength, width, options);
            return new Buffer2D<T>(memoryGroup, width, height);
        }

        /// <summary>
        /// Allocates a buffer of value type objects interpreted as a 2D region
        /// of <paramref name="size"/> width x <paramref name="size"/> height elements.
        /// </summary>
        /// <typeparam name="T">The type of buffer items to allocate.</typeparam>
        /// <param name="memoryAllocator">The memory allocator.</param>
        /// <param name="size">The buffer size.</param>
        /// <param name="options">The allocation options.</param>
        /// <returns>The <see cref="Buffer2D{T}"/>.</returns>
        public static Buffer2D<T> Allocate2D<T>(
            this MemoryAllocator memoryAllocator,
            Size size,
            AllocationOptions options = AllocationOptions.None)
            where T : struct =>
            Allocate2D<T>(memoryAllocator, size.Width, size.Height, options);

        internal static Buffer2D<T> Allocate2DOveraligned<T>(
            this MemoryAllocator memoryAllocator,
            int width,
            int height,
            int alignmentMultiplier,
            AllocationOptions options = AllocationOptions.None)
            where T : struct
        {
            long groupLength = (long)width * height;
            MemoryGroup<T> memoryGroup = memoryAllocator.AllocateGroup<T>(
                groupLength,
                width * alignmentMultiplier,
                options);
            return new Buffer2D<T>(memoryGroup, width, height);
        }

        /// <summary>
        /// Allocates padded buffers. Generally used by encoder/decoders.
        /// </summary>
        /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/>.</param>
        /// <param name="width">Pixel count in the row</param>
        /// <param name="pixelSizeInBytes">The pixel size in bytes, eg. 3 for RGB.</param>
        /// <param name="paddingInBytes">The padding.</param>
        /// <returns>A <see cref="IMemoryOwner{Byte}"/>.</returns>
        internal static IMemoryOwner<byte> AllocatePaddedPixelRowBuffer(
            this MemoryAllocator memoryAllocator,
            int width,
            int pixelSizeInBytes,
            int paddingInBytes)
        {
            int length = (width * pixelSizeInBytes) + paddingInBytes;
            return memoryAllocator.Allocate<byte>(length);
        }
    }
}
