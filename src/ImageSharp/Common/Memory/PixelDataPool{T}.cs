// <copyright file="PixelDataPool{T}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Buffers;

    /// <summary>
    /// Provides a resource pool that enables reusing instances of value type arrays for image data <see cref="T:T[]"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    public class PixelDataPool<T>
        where T : struct
    {
        /// <summary>
        /// The <see cref="ArrayPool{T}"/> which will be always cleared.
        /// </summary>
        private static readonly ArrayPool<T> CleanPool = ArrayPool<T>.Create(CalculateMaxArrayLength(), 50);

        /// <summary>
        /// The <see cref="ArrayPool{T}"/> which is not kept clean.
        /// </summary>
        private static readonly ArrayPool<T> DirtyPool = ArrayPool<T>.Create(CalculateMaxArrayLength(), 50);

        /// <summary>
        /// The backing <see cref="ArrayPool{T}"/>
        /// </summary>
        private ArrayPool<T> arrayPool;

        /// <summary>
        /// A value indicating whether clearArray is requested on <see cref="ArrayPool{T}.Return(T[], bool)"/>.
        /// </summary>
        private bool clearArray;

        private PixelDataPool(ArrayPool<T> arrayPool, bool clearArray)
        {
            this.clearArray = clearArray;
            this.arrayPool = arrayPool;
        }

        /// <summary>
        /// Gets the <see cref="PixelDataPool{T}"/> which will always return arrays initialized to default(T)
        /// </summary>
        public static PixelDataPool<T> Clean { get; } = new PixelDataPool<T>(CleanPool, true);

        /// <summary>
        /// Gets the <see cref="PixelDataPool{T}"/> which does not keep the arrays clean on Rent/Return.
        /// </summary>
        public static PixelDataPool<T> Dirty { get; } = new PixelDataPool<T>(DirtyPool, false);

        /// <summary>
        /// Rents the pixel array from the pool.
        /// </summary>
        /// <param name="minimumLength">The minimum length of the array to return.</param>
        /// <returns>The <see cref="T:TColor[]"/></returns>
        public T[] Rent(int minimumLength)
        {
            return CleanPool.Rent(minimumLength);
        }

        /// <summary>
        /// Returns the rented pixel array back to the pool.
        /// </summary>
        /// <param name="array">The array to return to the buffer pool.</param>
        public void Return(T[] array)
        {
            CleanPool.Return(array, this.clearArray);
        }

        /// <summary>
        /// Heuristically calculates a reasonable maxArrayLength value for the backing <see cref="CleanPool"/>.
        /// </summary>
        /// <returns>The maxArrayLength value</returns>
        internal static int CalculateMaxArrayLength()
        {
            // ReSharper disable once SuspiciousTypeConversion.Global
            if (default(T) is IPixel)
            {
                const int MaximumExpectedImageSize = 16384;
                return MaximumExpectedImageSize * MaximumExpectedImageSize;
            }
            else
            {
                return int.MaxValue;
            }
        }
    }
}