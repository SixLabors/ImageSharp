// <copyright file="BufferDataPool.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Memory
{
    using System;
    using System.Buffers;

    /// <summary>
    /// Provides a resource pool that enables reusing instances of value type arrays for image data <see cref="T:Byte[]"/>.
    /// </summary>
    public class BufferDataPool
    {
        /// <summary>
        /// The <see cref="ArrayPool{Byte}"/> which is not kept clean.
        /// </summary>
        private static readonly ArrayPool<byte> ArrayPool = ArrayPool<byte>.Create(CalculateMaxArrayLength(), 50);

        /// <summary>
        /// Rents the pixel array from the pool.
        /// </summary>
        /// <param name="minimumLength">The minimum length of the array to return.</param>
        /// <returns>The <see cref="T:Byte[]"/></returns>
        public static byte[] Rent(int minimumLength)
        {
            return ArrayPool.Rent(minimumLength);
        }

        /// <summary>
        /// Returns the rented pixel array back to the pool.
        /// </summary>
        /// <param name="array">The array to return to the buffer pool.</param>
        public static void Return(byte[] array)
        {
            try
            {
                ArrayPool.Return(array);
            }
            catch
            {
                // Do nothing. Someone didn't use the Bufferpool in their IImageService and they only have themselves to bame.
            }
        }

        /// <summary>
        /// Heuristically calculates a reasonable maxArrayLength value for the backing <see cref="ArrayPool{Byte}"/>.
        /// </summary>
        /// <returns>The maxArrayLength value</returns>
        internal static int CalculateMaxArrayLength()
        {
            // ReSharper disable once SuspiciousTypeConversion.Global
            const int MaximumExpectedImageSize = 16384;
            return MaximumExpectedImageSize * MaximumExpectedImageSize;
        }
    }
}