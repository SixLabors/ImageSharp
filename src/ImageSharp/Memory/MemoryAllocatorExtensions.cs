// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;

using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Extension methods for <see cref="MemoryAllocator"/>.
    /// </summary>
    internal static class MemoryAllocatorExtensions
    {
        public static Buffer2D<T> Allocate2D<T>(
            this MemoryAllocator memoryAllocator,
            int width,
            int height,
            AllocationOptions options = AllocationOptions.None)
            where T : struct
        {
            IMemoryOwner<T> buffer = memoryAllocator.Allocate<T>(width * height, options);
            var memorySource = new MemorySource<T>(buffer, true);

            return new Buffer2D<T>(memorySource, width, height);
        }

        public static Buffer2D<T> Allocate2D<T>(
            this MemoryAllocator memoryAllocator,
            Size size,
            AllocationOptions options = AllocationOptions.None)
            where T : struct =>
            Allocate2D<T>(memoryAllocator, size.Width, size.Height, options);

        /// <summary>
        /// Allocates padded buffers for BMP encoder/decoder. (Replacing old PixelRow/PixelArea)
        /// </summary>
        /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/></param>
        /// <param name="width">Pixel count in the row</param>
        /// <param name="pixelSizeInBytes">The pixel size in bytes, eg. 3 for RGB</param>
        /// <param name="paddingInBytes">The padding</param>
        /// <returns>A <see cref="IManagedByteBuffer"/></returns>
        public static IManagedByteBuffer AllocatePaddedPixelRowBuffer(
            this MemoryAllocator memoryAllocator,
            int width,
            int pixelSizeInBytes,
            int paddingInBytes)
        {
            int length = (width * pixelSizeInBytes) + paddingInBytes;
            return memoryAllocator.AllocateManagedByteBuffer(length);
        }
    }
}