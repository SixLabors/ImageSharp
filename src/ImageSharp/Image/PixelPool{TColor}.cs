// <copyright file="PixelPool{TColor}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Buffers;

    /// <summary>
    /// Provides a resource pool that enables reusing instances of type <see cref="T:TColor[]"/>.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public static class PixelPool<TColor>
        where TColor : struct, IPixel<TColor>
    {
        /// <summary>
        /// The <see cref="ArrayPool{T}"/> used to pool data. TODO: Choose sensible default size and count
        /// </summary>
        private static readonly ArrayPool<TColor> ArrayPool = ArrayPool<TColor>.Create(int.MaxValue, 50);

        /// <summary>
        /// Rents the pixel array from the pool.
        /// </summary>
        /// <param name="minimumLength">The minimum length of the array to return.</param>
        /// <returns>The <see cref="T:TColor[]"/></returns>
        public static TColor[] RentPixels(int minimumLength)
        {
            return ArrayPool.Rent(minimumLength);
        }

        /// <summary>
        /// Returns the rented pixel array back to the pool.
        /// </summary>
        /// <param name="array">The array to return to the buffer pool.</param>
        public static void ReturnPixels(TColor[] array)
        {
            ArrayPool.Return(array, true);
        }
    }
}