// <copyright file="ByteExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Extension methods for the <see cref="byte"/> struct.
    /// </summary>
    internal static class ByteExtensions
    {
        /// <summary>
        /// Optimized <see cref="T:byte[]"/> reversal algorithm.
        /// </summary>
        /// <param name="source">The byte array.</param>
        public static void ReverseBytes(this byte[] source)
        {
            ReverseBytes(source, 0, source.Length);
        }

        /// <summary>
        /// Optimized <see cref="T:byte[]"/> reversal algorithm.
        /// </summary>
        /// <param name="source">The byte array.</param>
        /// <param name="index">The index.</param>
        /// <param name="length">The length.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="source"/> is null.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReverseBytes(this byte[] source, int index, int length)
        {
            Guard.NotNull(source, nameof(source));

            int i = index;
            int j = index + length - 1;
            while (i < j)
            {
                byte temp = source[i];
                source[i] = source[j];
                source[j] = temp;
                i++;
                j--;
            }
        }
    }
}