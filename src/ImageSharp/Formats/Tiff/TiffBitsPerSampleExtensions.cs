// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Formats.Tiff.Constants;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    internal static class TiffBitsPerSampleExtensions
    {
        /// <summary>
        /// Gets the bits per channel array for a given BitsPerSample value, e,g, for RGB888: [8, 8, 8]
        /// </summary>
        /// <param name="tiffBitsPerSample">The tiff bits per sample.</param>
        /// <returns>Bits per sample array.</returns>
        public static ushort[] Bits(this TiffBitsPerSample tiffBitsPerSample)
        {
            switch (tiffBitsPerSample)
            {
                case TiffBitsPerSample.Bit1:
                    return TiffConstants.BitsPerSample1Bit;
                case TiffBitsPerSample.Bit4:
                    return TiffConstants.BitsPerSample4Bit;
                case TiffBitsPerSample.Bit8:
                    return TiffConstants.BitsPerSample8Bit;
                case TiffBitsPerSample.Bit24:
                    return TiffConstants.BitsPerSampleRgb8Bit;

                default:
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
                    if (bitsPerSample[0] == TiffConstants.BitsPerSampleRgb8Bit[0] &&
                        bitsPerSample[1] == TiffConstants.BitsPerSampleRgb8Bit[1] &&
                        bitsPerSample[2] == TiffConstants.BitsPerSampleRgb8Bit[2])
                    {
                        return TiffBitsPerSample.Bit24;
                    }

                    break;

                case 1:
                    if (bitsPerSample[0] == TiffConstants.BitsPerSample1Bit[0])
                    {
                        return TiffBitsPerSample.Bit1;
                    }

                    if (bitsPerSample[0] == TiffConstants.BitsPerSample4Bit[0])
                    {
                        return TiffBitsPerSample.Bit4;
                    }

                    if (bitsPerSample[0] == TiffConstants.BitsPerSample8Bit[0])
                    {
                        return TiffBitsPerSample.Bit8;
                    }

                    break;
            }

            return TiffBitsPerSample.Unknown;
        }
    }
}
