// <copyright file="CleanPooler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageSharp.Formats
{
    using System.Buffers;

    /// <summary>
    /// Wraps <see cref="ArrayPool{T}"/> to always provide arrays initialized with default(T)
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    internal class CleanPooler<T>
    {
        private static readonly ArrayPool<T> Pool = ArrayPool<T>.Create();

        /// <summary>
        /// Rents a clean array
        /// </summary>
        /// <param name="minimumLength">The minimum array length</param>
        /// <returns>A clean array of T</returns>
        public static T[] RentCleanArray(int minimumLength) => Pool.Rent(minimumLength);

        /// <summary>
        /// Retursn array to the pool
        /// </summary>
        /// <param name="array">The array</param>
        public static void ReturnArray(T[] array) => Pool.Return(array, true);
    }
}