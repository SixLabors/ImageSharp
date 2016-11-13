// <copyright file="ByteExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    /// <summary>
    /// Extension methods for the <see cref="byte"/> struct.
    /// </summary>
    internal static class ByteExtensions
    {
        /// <summary>
        /// Converts a byte array to a new array where each value in the original array is represented
        /// by a the specified number of bits.
        /// </summary>
        /// <param name="source">The bytes to convert from. Cannot be null.</param>
        /// <param name="bits">The number of bits per value.</param>
        /// <returns>The resulting <see cref="T:byte[]"/> array. Is never null.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="source"/> is null.</exception>
        /// <exception cref="System.ArgumentException"><paramref name="bits"/> is less than or equals than zero.</exception>
        public static byte[] ToArrayByBitsLength(this byte[] source, int bits)
        {
            Guard.NotNull(source, nameof(source));
            Guard.MustBeGreaterThan(bits, 0, nameof(bits));

            byte[] result;

            if (bits < 8)
            {
                result = new byte[source.Length * 8 / bits];
                int mask = 0xFF >> (8 - bits);
                int resultOffset = 0;

                // ReSharper disable once ForCanBeConvertedToForeach
                for (int i = 0; i < source.Length; i++)
                {
                    byte b = source[i];
                    for (int shift = 0; shift < 8; shift += bits)
                    {
                        int colorIndex = (b >> (8 - bits - shift)) & mask;

                        result[resultOffset] = (byte)colorIndex;

                        resultOffset++;
                    }
                }
            }
            else
            {
                result = source;
            }

            return result;
        }

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
