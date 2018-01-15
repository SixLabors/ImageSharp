// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Provides a resource pool that enables reusing instances of value type arrays for image data <see cref="T:T[]"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    internal class PixelDataPool<T>
        where T : struct
    {
        /// <summary>
        /// The maximum size of pooled arrays in bytes.
        /// Currently set to 32MB, which is equivalent to 8 megapixels of raw <see cref="Rgba32"/> data.
        /// </summary>
        private const int MaxPooledBufferSizeInBytes = 32 * 1024 * 1024;

        /// <summary>
        /// The maximum array length of the <see cref="ArrayPool"/>.
        /// </summary>
        private static readonly int MaxArrayLength = MaxPooledBufferSizeInBytes / Unsafe.SizeOf<T>();

        /// <summary>
        /// The <see cref="ArrayPool{T}"/> which is not kept clean.
        /// </summary>
        private static readonly ArrayPool<T> ArrayPool = ArrayPool<T>.Create(MaxArrayLength, 50);

        /// <summary>
        /// Rents the pixel array from the pool.
        /// </summary>
        /// <param name="minimumLength">The minimum length of the array to return.</param>
        /// <returns>The <see cref="T:TPixel[]"/></returns>
        public static T[] Rent(int minimumLength)
        {
            return ArrayPool.Rent(minimumLength);
        }

        /// <summary>
        /// Returns the rented pixel array back to the pool.
        /// </summary>
        /// <param name="array">The array to return to the buffer pool.</param>
        public static void Return(T[] array)
        {
            ArrayPool.Return(array);
        }
    }
}