// <copyright file="PixelDataPool{T}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Memory
{
    using System.Buffers;

    using ImageSharp.PixelFormats;

    /// <summary>
    /// Provides a resource pool that enables reusing instances of value type arrays for image data <see cref="T:T[]"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    internal class PixelDataPool<T>
        where T : struct
    {
        /// <summary>
        /// The <see cref="ArrayPool{T}"/> which is not kept clean.
        /// </summary>
        private static readonly ArrayPool<T> ArrayPool = ArrayPool<T>.Create(CalculateMaxArrayLength(), 50);

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

        /// <summary>
        /// Heuristically calculates a reasonable maxArrayLength value for the backing <see cref="ArrayPool{T}"/>.
        /// </summary>
        /// <returns>The maxArrayLength value</returns>
        internal static int CalculateMaxArrayLength()
        {
            // ReSharper disable once SuspiciousTypeConversion.Global
            if (default(T) is IPixel)
            {
                const int MaximumExpectedImageSize = 16384 * 16384;
                return MaximumExpectedImageSize;
            }
            else
            {
                const int MaxArrayLength = 1024 * 1024; // Match default pool.
                return MaxArrayLength;
            }
        }
    }
}