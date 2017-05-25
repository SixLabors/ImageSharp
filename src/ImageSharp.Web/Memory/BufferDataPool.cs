// <copyright file="BufferDataPool.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Memory
{
    using System.Buffers;

    /// <summary>
    /// Provides a resource pool that enables reusing instances of value type arrays for image data <see cref="T:Byte[]"/>.
    /// </summary>
    public class BufferDataPool
    {
        /// <summary>
        /// The maximum length of each array in the pool (2^21).
        /// </summary>
        private const int MaxLength = 1024 * 1024 * 2;

        /// <summary>
        /// The <see cref="ArrayPool{Byte}"/> which is not kept clean. This gives us a pool of up to 100MB.
        /// </summary>
        private static readonly ArrayPool<byte> ArrayPool = ArrayPool<byte>.Create(MaxLength, 50);

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
                if (array != null)
                {
                    ArrayPool.Return(array);
                }
            }
            catch
            {
                // Do nothing. Someone didn't use the Bufferpool in their IImageResolver
                // and they only have themselves to blame for the performance hit.
            }
        }
    }
}