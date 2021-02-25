// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Formats.Experimental.Tiff;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    internal static class TiffBitsPerSampleExtensions
    {
        private static readonly ushort[] One = { 1 };

        private static readonly ushort[] Four = { 4 };

        private static readonly ushort[] Eight = { 8 };

        private static readonly ushort[] Rgb888 = { 8, 8, 8 };

        /// <summary>
        /// Gets the bits per channel array for a given BitsPerSample value, e,g, for RGB888: [8, 8, 8]
        /// </summary>
        /// <param name="tiffBitsPerSample">The tiff bits per sample.</param>
        /// <returns>Bits per sample array.</returns>
        public static ushort[] Bits(this TiffBitsPerSample tiffBitsPerSample)
        {
            switch (tiffBitsPerSample)
            {
                case TiffBitsPerSample.One:
                    return One;
                case TiffBitsPerSample.Four:
                    return Four;
                case TiffBitsPerSample.Eight:
                    return Eight;
                case TiffBitsPerSample.Rgb888:
                    return Rgb888;

                default:
                    TiffThrowHelper.ThrowNotSupported("The bits per pixels are not supported");
                    return Array.Empty<ushort>();
            }
        }

        /// <summary>
        /// Maps an array of bits per sample to a concrete enum value.
        /// </summary>
        /// <param name="bitsPerSample">The bits per sample array.</param>
        /// <returns>TiffBitsPerSample enum value.</returns>
        public static TiffBitsPerSample GetBitsPerSample(this ushort[] bitsPerSample)
        {
            switch (bitsPerSample.Length)
            {
                case 3:
                    if (bitsPerSample[0] == Rgb888[0] && bitsPerSample[1] == Rgb888[1] && bitsPerSample[2] == Rgb888[2])
                    {
                        return TiffBitsPerSample.Rgb888;
                    }

                    break;

                case 1:
                    if (bitsPerSample[0] == One[0])
                    {
                        return TiffBitsPerSample.One;
                    }

                    if (bitsPerSample[0] == Four[0])
                    {
                        return TiffBitsPerSample.Four;
                    }

                    if (bitsPerSample[0] == Eight[0])
                    {
                        return TiffBitsPerSample.Eight;
                    }

                    break;
            }

            return TiffBitsPerSample.Unknown;
        }

        /// <summary>
        /// Gets the bits per pixel for the given bits per sample.
        /// </summary>
        /// <param name="tiffBitsPerSample">The tiff bits per sample.</param>
        /// <returns>Bits per pixel.</returns>
        public static int BitsPerPixel(this TiffBitsPerSample tiffBitsPerSample)
        {
            var bitsPerSample = tiffBitsPerSample.Bits();
            int bitsPerPixel = 0;
            foreach (var bits in bitsPerSample)
            {
                bitsPerPixel += bits;
            }

            return bitsPerPixel;
        }
    }
}
