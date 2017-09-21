// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    /// <summary>
    /// Provides 8-bit lookup tables for converting from YCbCr to Rgb colorspace.
    /// Methods to build the tables are based on libjpeg implementation.
    /// </summary>
    internal struct PdfJsYCbCrToRgbTables
    {
        /// <summary>
        /// The red red-chrominance table
        /// </summary>
        public static int[] CrRTable = new int[256];

        /// <summary>
        /// The blue blue-chrominance table
        /// </summary>
        public static int[] CbBTable = new int[256];

        /// <summary>
        /// The green red-chrominance table
        /// </summary>
        public static int[] CrGTable = new int[256];

        /// <summary>
        /// The green blue-chrominance table
        /// </summary>
        public static int[] CbGTable = new int[256];

        // Speediest right-shift on some machines and gives us enough accuracy at 4 decimal places.
        private const int ScaleBits = 16;

        private const int Half = 1 << (ScaleBits - 1);

        private const int MinSample = 0;

        private const int HalfSample = 128;

        private const int MaxSample = 255;

        /// <summary>
        /// Initializes the YCbCr tables
        /// </summary>
        public static void Create()
        {
            for (int i = 0, x = -128; i <= 255; i++, x++)
            {
                // i is the actual input pixel value, in the range 0..255
                // The Cb or Cr value we are thinking of is x = i - 128
                // Cr=>R value is nearest int to 1.402 * x
                CrRTable[i] = RightShift((Fix(1.402F) * x) + Half);

                // Cb=>B value is nearest int to 1.772 * x
                CbBTable[i] = RightShift((Fix(1.772F) * x) + Half);

                // Cr=>G value is scaled-up -0.714136286
                CrGTable[i] = (-Fix(0.714136286F)) * x;

                // Cb => G value is scaled - up - 0.344136286 * x
                // We also add in Half so that need not do it in inner loop
                CbGTable[i] = ((-Fix(0.344136286F)) * x) + Half;
            }
        }

        /// <summary>
        /// Optimized method to pack bytes to the image from the YCbCr color space.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="packed">The packed pixel.</param>
        /// <param name="y">The y luminance component.</param>
        /// <param name="cb">The cb chroma component.</param>
        /// <param name="cr">The cr chroma component.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PackYCbCr<TPixel>(ref TPixel packed, byte y, byte cb, byte cr)
            where TPixel : struct, IPixel<TPixel>
        {
            byte r = (byte)(y + CrRTable[cr]).Clamp(0, 255);

            // The values for the G calculation are left scaled up, since we must add them together before rounding.
            byte g = (byte)(y + RightShift(CbGTable[cb] + CrGTable[cr])).Clamp(0, 255);

            byte b = (byte)(y + CbBTable[cb]).Clamp(0, 255);

            packed.PackFromRgba32(new Rgba32(r, g, b, 255));
        }

        /// <summary>
        /// Optimized method to pack bytes to the image from the YccK color space.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="packed">The packed pixel.</param>
        /// <param name="y">The y luminance component.</param>
        /// <param name="cb">The cb chroma component.</param>
        /// <param name="cr">The cr chroma component.</param>
        /// <param name="k">The keyline component.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PackYccK<TPixel>(ref TPixel packed, byte y, byte cb, byte cr, byte k)
            where TPixel : struct, IPixel<TPixel>
        {
            int c = (MaxSample - (y + CrRTable[cr])).Clamp(0, 255);

            // The values for the G calculation are left scaled up, since we must add them together before rounding.
            int m = (MaxSample - (y + RightShift(CbGTable[cb] + CrGTable[cr]))).Clamp(0, 255);

            int cy = (MaxSample - (y + CbBTable[cb])).Clamp(0, 255);

            byte r = (byte)((c * k) / MaxSample);
            byte g = (byte)((m * k) / MaxSample);
            byte b = (byte)((cy * k) / MaxSample);

            packed.PackFromRgba32(new Rgba32(r, g, b, MaxSample));
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