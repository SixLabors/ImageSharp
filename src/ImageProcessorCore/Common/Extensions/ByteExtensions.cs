// <copyright file="ByteExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
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
        /// <param name="bytes">The bytes to convert from. Cannot be null.</param>
        /// <param name="bits">The number of bits per value.</param>
        /// <returns>The resulting <see cref="T:byte[]"/> array. Is never null.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="bytes"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="bits"/> is less than or equals than zero.</exception>
        public static byte[] ToArrayByBitsLength(this byte[] bytes, int bits)
        {
            Guard.NotNull(bytes, "bytes");
            Guard.MustBeGreaterThan(bits, 0, "bits");

            byte[] result;

            if (bits < 8)
            {
                result = new byte[bytes.Length * 8 / bits];

                // BUGFIX I dont think it should be there, but I am not sure if it breaks something else
                // int factor = (int)Math.Pow(2, bits) - 1;
                int mask = 0xFF >> (8 - bits);
                int resultOffset = 0;

                foreach (byte b in bytes)
                {
                    for (int shift = 0; shift < 8; shift += bits)
                    {
                        int colorIndex = (b >> (8 - bits - shift)) & mask; // * (255 / factor);

                        result[resultOffset] = (byte)colorIndex;

                        resultOffset++;
                    }
                }
            }
            else
            {
                result = bytes;
            }

            return result;
        }
    }
}
