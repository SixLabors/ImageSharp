// <copyright file="YCbCrToRgbTables.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpg
{
    using System.Runtime.CompilerServices;
    using ImageSharp.PixelFormats;

    /// <summary>
    /// Provides 8-bit lookup tables for converting from YCbCr to Rgb colorspace.
    /// Methods to build the tables are based on libjpeg implementation.
    /// </summary>
    internal unsafe struct YCbCrToRgbTables
    {
        /// <summary>
        /// The red red-chrominance table
        /// </summary>
        public fixed int CrRTable[256];

        /// <summary>
        /// The blue blue-chrominance table
        /// </summary>
        public fixed int CbBTable[256];

        /// <summary>
        /// The green red-chrominance table
        /// </summary>
        public fixed int CrGTable[256];

        /// <summary>
        /// The green blue-chrominance table
        /// </summary>
        public fixed int CbGTable[256];

        // Speediest right-shift on some machines and gives us enough accuracy at 4 decimal places.
        private const int ScaleBits = 16;

        private const int Half = 1 << (ScaleBits - 1);

        /// <summary>
        /// Initializes the YCbCr tables
        /// </summary>
        /// <returns>The intialized <see cref="YCbCrToRgbTables"/></returns>
        public static YCbCrToRgbTables Create()
        {
            YCbCrToRgbTables tables = default(YCbCrToRgbTables);

            for (int i = 0, x = -128; i <= 255; i++, x++)
            {
                // i is the actual input pixel value, in the range 0..255
                // The Cb or Cr value we are thinking of is x = i - 128
                // Cr=>R value is nearest int to 1.402 * x
                tables.CrRTable[i] = RightShift((Fix(1.402F) * x) + Half);

                // Cb=>B value is nearest int to 1.772 * x
                tables.CbBTable[i] = RightShift((Fix(1.772F) * x) + Half);

                // Cr=>G value is scaled-up -0.714136286
                tables.CrGTable[i] = (-Fix(0.714136286F)) * x;

                // Cb => G value is scaled - up - 0.344136286 * x
                // We also add in Half so that need not do it in inner loop
                tables.CbGTable[i] = ((-Fix(0.344136286F)) * x) + Half;
            }

            return tables;
        }

        /// <summary>
        /// Optimized method to pack bytes to the image from the YCbCr color space.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="packed">The packed pixel.</param>
        /// <param name="tables">The reference to the tables instance.</param>
        /// <param name="y">The y luminance component.</param>
        /// <param name="cb">The cb chroma component.</param>
        /// <param name="cr">The cr chroma component.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Pack<TPixel>(ref TPixel packed, YCbCrToRgbTables* tables, byte y, byte cb, byte cr)
            where TPixel : struct, IPixel<TPixel>
        {
            // float r = MathF.Round(y + (1.402F * cr), MidpointRounding.AwayFromZero);
            byte r = (byte)(y + tables->CrRTable[cr]).Clamp(0, 255);

            // float g = MathF.Round(y - (0.344136F * cb) - (0.714136F * cr), MidpointRounding.AwayFromZero);
            // The values for the G calculation are left scaled up, since we must add them together before rounding.
            byte g = (byte)(y + RightShift(tables->CbGTable[cb] + tables->CrGTable[cr])).Clamp(0, 255);

            // float b = MathF.Round(y + (1.772F * cb), MidpointRounding.AwayFromZero);
            byte b = (byte)(y + tables->CbBTable[cb]).Clamp(0, 255);

            packed.PackFromRgba32(new Rgba32(r, g, b, 255));
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