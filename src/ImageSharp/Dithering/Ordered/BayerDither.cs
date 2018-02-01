// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Dithering
{
    /// <summary>
    /// Applies order dithering using a Bayer dithering matrix of arbitrary length.
    /// <see href="https://en.wikipedia.org/wiki/Ordered_dithering"/>
    /// </summary>
    public class BayerDither : OrderedDitherBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BayerDither"/> class.
        /// </summary>
        /// <param name="exponent">
        /// The exponent used to raise the base value (2).
        /// The value given determines the dimensions of the matrix with each dimension a power of 2. e.g 2^2 = 4, 2^3 = 8
        /// </param>
        public BayerDither(uint exponent)
            : base(ComputeBayer(exponent))
        {
        }

        private static Fast2DArray<uint> ComputeBayer(uint order)
        {
            uint dimension = (uint)(1 << (int)order);
            var matrix = new Fast2DArray<uint>((int)dimension);
            uint i = 0;
            for (int y = 0; y < dimension; y++)
            {
                for (int x = 0; x < dimension; x++)
                {
                    matrix[y, x] = Bayer(i / dimension, i % dimension, order);
                    i++;
                }
            }

            return matrix;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Bayer(uint x, uint y, uint order)
        {
            uint res = 0;
            for (uint i = 0; i < order; ++i)
            {
                uint xOdd_XOR_yOdd = (x & 1) ^ (y & 1);
                uint xOdd = x & 1;
                res = ((res << 1 | xOdd_XOR_yOdd) << 1) | xOdd;
                x >>= 1;
                y >>= 1;
            }

            return res;
        }
    }
}