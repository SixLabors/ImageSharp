// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// A factory for creating ordered dither matrices.
    /// </summary>
    internal static class OrderedDitherFactory
    {
        /// <summary>
        /// Creates an ordered dithering matrix with equal sides of arbitrary length.
        /// <see href="https://en.wikipedia.org/wiki/Ordered_dithering"/>
        /// </summary>
        /// <param name="length">The length of the matrix sides</param>
        /// <returns>The <see cref="DenseMatrix{T}"/></returns>
        public static DenseMatrix<uint> CreateDitherMatrix(uint length)
        {
            // Calculate the the logarithm of length to the base 2
            uint exponent = 0;
            uint bayerLength = 0;
            do
            {
                exponent++;
                bayerLength = (uint)(1 << (int)exponent);
            }
            while (length > bayerLength);

            // Create our Bayer matrix that matches the given exponent and dimensions
            var matrix = new DenseMatrix<uint>((int)length);
            uint i = 0;
            for (int y = 0; y < length; y++)
            {
                for (int x = 0; x < length; x++)
                {
                    matrix[y, x] = Bayer(i / length, i % length, exponent);
                    i++;
                }
            }

            // If the user requested a matrix with a non-power-of-2 length e.g. 3x3 and we used 4x4 algorithm,
            // we need to convert the numbers so that the resulting range is un-gapped.
            // We generated:  We saved:   We compress the number range:
            //  0  8  2 10     0  8  2    0 5 2
            // 12  4 14  6    12  4 14    7 4 8
            //  3 11  1  9     3 11  1    3 6 1
            // 15  7 13  5
            uint maxValue = bayerLength * bayerLength;
            uint missing = 0;
            for (uint v = 0; v < maxValue; ++v)
            {
                bool found = false;
                for (int y = 0; y < length; ++y)
                {
                    for (int x = 0; x < length; x++)
                    {
                        if (matrix[y, x] == v)
                        {
                            matrix[y, x] -= missing;
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    ++missing;
                }
            }

            return matrix;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Bayer(uint x, uint y, uint order)
        {
            uint result = 0;
            for (uint i = 0; i < order; ++i)
            {
                uint xOddXorYOdd = (x & 1) ^ (y & 1);
                uint xOdd = x & 1;
                result = ((result << 1 | xOddXorYOdd) << 1) | xOdd;
                x >>= 1;
                y >>= 1;
            }

            return result;
        }
    }
}