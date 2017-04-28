// <copyright file="RgbToYCbCrTables.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpg
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Provides 8-bit lookup tables for converting from Rgb to YCbCr colorspace.
    /// Methods to build the tables are based on libjpeg implementation.
    /// </summary>
    internal unsafe struct RgbToYCbCrTables
    {
        /// <summary>
        /// The red luminance table
        /// </summary>
        public fixed int YRTable[256];

        /// <summary>
        /// The green luminance table
        /// </summary>
        public fixed int YGTable[256];

        /// <summary>
        /// The blue luminance table
        /// </summary>
        public fixed int YBTable[256];

        /// <summary>
        /// The red blue-chrominance table
        /// </summary>
        public fixed int CbRTable[256];

        /// <summary>
        /// The green blue-chrominance table
        /// </summary>
        public fixed int CbGTable[256];

        /// <summary>
        /// The blue blue-chrominance table
        /// B=>Cb and R=>Cr are the same
        /// </summary>
        public fixed int CbBTable[256];

        /// <summary>
        /// The green red-chrominance table
        /// </summary>
        public fixed int CrGTable[256];

        /// <summary>
        /// The blue red-chrominance table
        /// </summary>
        public fixed int CrBTable[256];

        // Speediest right-shift on some machines and gives us enough accuracy at 4 decimal places.
        private const int ScaleBits = 16;

        private const int CBCrOffset = 128 << ScaleBits;

        private const int Half = 1 << (ScaleBits - 1);

        /// <summary>
        /// Initializes the YCbCr tables
        /// </summary>
        /// <returns>The intialized <see cref="RgbToYCbCrTables"/></returns>
        public static RgbToYCbCrTables Create()
        {
            RgbToYCbCrTables tables = default(RgbToYCbCrTables);

            for (int i = 0; i <= 255; i++)
            {
                // The values for the calculations are left scaled up since we must add them together before rounding.
                tables.YRTable[i] = Fix(0.299F) * i;
                tables.YGTable[i] = Fix(0.587F) * i;
                tables.YBTable[i] = (Fix(0.114F) * i) + Half;
                tables.CbRTable[i] = (-Fix(0.168735892F)) * i;
                tables.CbGTable[i] = (-Fix(0.331264108F)) * i;

                // We use a rounding fudge - factor of 0.5 - epsilon for Cb and Cr.
                // This ensures that the maximum output will round to 255
                // not 256, and thus that we don't have to range-limit.
                //
                // B=>Cb and R=>Cr tables are the same
                tables.CbBTable[i] = (Fix(0.5F) * i) + CBCrOffset + Half - 1;

                tables.CrGTable[i] = (-Fix(0.418687589F)) * i;
                tables.CrBTable[i] = (-Fix(0.081312411F)) * i;
            }

            return tables;
        }

        /// <summary>
        /// Optimized method to allocates the correct y, cb, and cr values to the DCT blocks from the given r, g, b values.
        /// </summary>
        /// <param name="yBlockRaw">The The luminance block.</param>
        /// <param name="cbBlockRaw">The red chroma block.</param>
        /// <param name="crBlockRaw">The blue chroma block.</param>
        /// <param name="tables">The reference to the tables instance.</param>
        /// <param name="index">The current index.</param>
        /// <param name="r">The red value.</param>
        /// <param name="g">The green value.</param>
        /// <param name="b">The blue value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Allocate(ref float* yBlockRaw, ref float* cbBlockRaw, ref float* crBlockRaw, ref RgbToYCbCrTables* tables, int index, int r, int g, int b)
        {
            // float y = (0.299F * r) + (0.587F * g) + (0.114F * b);
            yBlockRaw[index] = (tables->YRTable[r] + tables->YGTable[g] + tables->YBTable[b]) >> ScaleBits;

            // float cb = 128F + ((-0.168736F * r) - (0.331264F * g) + (0.5F * b));
            cbBlockRaw[index] = (tables->CbRTable[r] + tables->CbGTable[g] + tables->CbBTable[b]) >> ScaleBits;

            // float b = MathF.Round(y + (1.772F * cb), MidpointRounding.AwayFromZero);
            crBlockRaw[index] = (tables->CbBTable[r] + tables->CrGTable[g] + tables->CrBTable[b]) >> ScaleBits;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Fix(float x)
        {
            return (int)((x * (1L << ScaleBits)) + 0.5F);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int RightShift(int x)
        {
            return x >> ScaleBits;
        }
    }
}