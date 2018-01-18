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
        internal const int MaxPooledBufferSizeInBytes = 32 * 1024 * 1024;

        /// <summary>
        /// The threshold to pool arrays in <see cref="LargeArrayPool"/> which has less buckets for memory safety.
        /// </summary>
        private const int LargeBufferThresholdInBytes = 8 * 1024 * 1024;

        /// <summary>
        /// The maximum array length of the <see cref="LargeArrayPool"/>.
        /// </summary>
        private static readonly int MaxLargeArrayLength = MaxPooledBufferSizeInBytes / Unsafe.SizeOf<T>();

        /// <summary>
        /// The maximum array length of the <see cref="NormalArrayPool"/>.
        /// </summary>
        private static readonly int MaxNormalArrayLength = LargeBufferThresholdInBytes / Unsafe.SizeOf<T>();

        /// <summary>
        /// The <see cref="ArrayPool{T}"/> for huge buffers, which is not kept clean.
        /// </summary>
        private static readonly ArrayPool<T> LargeArrayPool = ArrayPool<T>.Create(MaxLargeArrayLength, 8);

        /// <summary>
        /// The <see cref="ArrayPool{T}"/> for small-to-medium buffers which is not kept clean.
        /// </summary>
        private static readonly ArrayPool<T> NormalArrayPool = ArrayPool<T>.Create(MaxNormalArrayLength, 24);

        /// <summary>
        /// Rents the pixel array from the pool.
        /// </summary>
        /// <param name="minimumLength">The minimum length of the array to return.</param>
        /// <returns>The <see cref="T:TPixel[]"/></returns>
        public static T[] Rent(int minimumLength)
        {
            if (minimumLength <= MaxNormalArrayLength)
            {
                return NormalArrayPool.Rent(minimumLength);
            }
            else
            {
                return LargeArrayPool.Rent(minimumLength);
            }
        }

        /// <summary>
        /// Returns the rented pixel array back to the pool.
        /// </summary>
        /// <param name="array">The array to return to the buffer pool.</param>
        public static void Return(T[] array)
        {
            if (array.Length <= MaxNormalArrayLength)
            {
                NormalArrayPool.Return(array);
            }
            else
            {
                LargeArrayPool.Return(array);
            }
        }
    }
}