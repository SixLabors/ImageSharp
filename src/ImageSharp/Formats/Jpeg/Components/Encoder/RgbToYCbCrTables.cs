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
    internal struct RgbToYCbCrTables
    {
        // Speediest right-shift on some machines and gives us enough accuracy at 4 decimal places.
        private const int ScaleBits = 16;

        private const int GYOffset = 256;

        private const int BYOffset = 2 * 256;

        private const int RCbOffset = 3 * 256;

        private const int GCbOffset = 4 * 256;

        private const int BCbOffset = 5 * 256;

        // B=>Cb and R=>Cr are the same
        private const int RCrOffset = BCbOffset;

        private const int GCrOffset = 6 * 256;

        private const int BCrOffset = 7 * 256;

        private const int CBCrOffset = 128 << ScaleBits;

        private const int Half = 1 << (ScaleBits - 1);

        private static readonly int[] YCbCrTable = new int[8 * 256];

        /// <summary>
        /// Optimized method to allocates the correct y, cb, and cr values to the DCT blocks from the given r, g, b values.
        /// </summary>
        /// <param name="yBlockRaw">The The luminance block.</param>
        /// <param name="cbBlockRaw">The red chroma block.</param>
        /// <param name="crBlockRaw">The blue chroma block.</param>
        /// <param name="index">The current index.</param>
        /// <param name="r">The red value.</param>
        /// <param name="g">The green value.</param>
        /// <param name="b">The blue value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Allocate(ref float* yBlockRaw, ref float* cbBlockRaw, ref float* crBlockRaw, int index, int r, int g, int b)
        {
            // float y = (0.299F * r) + (0.587F * g) + (0.114F * b);
            yBlockRaw[index] = (YCbCrTable[r] + YCbCrTable[g + GYOffset] + YCbCrTable[b + BYOffset]) >> ScaleBits;

            // float cb = 128F + ((-0.168736F * r) - (0.331264F * g) + (0.5F * b));
            cbBlockRaw[index] = (YCbCrTable[r + RCbOffset] + YCbCrTable[g + GCbOffset] + YCbCrTable[b + BCbOffset]) >> ScaleBits;

            // float b = MathF.Round(y + (1.772F * cb), MidpointRounding.AwayFromZero);
            crBlockRaw[index] = (YCbCrTable[r + RCrOffset] + YCbCrTable[g + GCrOffset] + YCbCrTable[b + BCrOffset]) >> ScaleBits;
        }

        /// <summary>
        /// Initializes the YCbCr tables
        /// </summary>
        /// <returns>The intialized <see cref="RgbToYCbCrTables"/></returns>
        public RgbToYCbCrTables Init()
        {
            for (int i = 0; i <= 255; i++)
            {
                // The values for the calculations are left scaled up since we must add them together before rounding.
                YCbCrTable[i] = Fix(0.299F) * i;
                YCbCrTable[i + GYOffset] = Fix(0.587F) * i;
                YCbCrTable[i + BYOffset] = (Fix(0.114F) * i) + Half;
                YCbCrTable[i + RCbOffset] = (-Fix(0.168735892F)) * i;
                YCbCrTable[i + GCbOffset] = (-Fix(0.331264108F)) * i;

                // We use a rounding fudge - factor of 0.5 - epsilon for Cb and Cr.
                // This ensures that the maximum output will round to 255
                // not 256, and thus that we don't have to range-limit.
                //
                // B=>Cb and R=>Cr tables are the same
                YCbCrTable[i + BCbOffset] = (Fix(0.5F) * i) + CBCrOffset + Half - 1;

                YCbCrTable[i + GCrOffset] = (-Fix(0.418687589F)) * i;
                YCbCrTable[i + BCrOffset] = (-Fix(0.081312411F)) * i;
            }

            return this;
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