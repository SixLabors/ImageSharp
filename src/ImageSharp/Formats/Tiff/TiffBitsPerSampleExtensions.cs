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
        public static ushort[] BitsPerChannel(this TiffBitsPerSample tiffBitsPerSample)
        {
            switch (tiffBitsPerSample)
            {
                case TiffBitsPerSample.Bit1:
                    return TiffConstants.BitsPerSample1Bit;
                case TiffBitsPerSample.Bit2:
                    return TiffConstants.BitsPerSample2Bit;
                case TiffBitsPerSample.Bit4:
                    return TiffConstants.BitsPerSample4Bit;
                case TiffBitsPerSample.Bit6:
                    return TiffConstants.BitsPerSample6Bit;
                case TiffBitsPerSample.Bit8:
                    return TiffConstants.BitsPerSample8Bit;
                case TiffBitsPerSample.Bit10:
                    return TiffConstants.BitsPerSample10Bit;
                case TiffBitsPerSample.Bit12:
                    return TiffConstants.BitsPerSample12Bit;
                case TiffBitsPerSample.Bit14:
                    return TiffConstants.BitsPerSample14Bit;
                case TiffBitsPerSample.Bit16:
                    return TiffConstants.BitsPerSample16Bit;
                case TiffBitsPerSample.Rgb222:
                    return TiffConstants.BitsPerSampleRgb2Bit;
                case TiffBitsPerSample.Rgb444:
                    return TiffConstants.BitsPerSampleRgb4Bit;
                case TiffBitsPerSample.Rgb888:
                    return TiffConstants.BitsPerSampleRgb8Bit;
                case TiffBitsPerSample.Rgb101010:
                    return TiffConstants.BitsPerSampleRgb10Bit;
                case TiffBitsPerSample.Rgb121212:
                    return TiffConstants.BitsPerSampleRgb12Bit;
                case TiffBitsPerSample.Rgb141414:
                    return TiffConstants.BitsPerSampleRgb14Bit;
                case TiffBitsPerSample.Rgb161616:
                    return TiffConstants.BitsPerSampleRgb16Bit;
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
                    if (bitsPerSample[2] == TiffConstants.BitsPerSampleRgb16Bit[2] &&
                        bitsPerSample[1] == TiffConstants.BitsPerSampleRgb16Bit[1] &&
                        bitsPerSample[0] == TiffConstants.BitsPerSampleRgb16Bit[0])
                    {
                        return TiffBitsPerSample.Rgb161616;
                    }

                    if (bitsPerSample[2] == TiffConstants.BitsPerSampleRgb14Bit[2] &&
                        bitsPerSample[1] == TiffConstants.BitsPerSampleRgb14Bit[1] &&
                        bitsPerSample[0] == TiffConstants.BitsPerSampleRgb14Bit[0])
                    {
                        return TiffBitsPerSample.Rgb141414;
                    }

                    if (bitsPerSample[2] == TiffConstants.BitsPerSampleRgb12Bit[2] &&
                        bitsPerSample[1] == TiffConstants.BitsPerSampleRgb12Bit[1] &&
                        bitsPerSample[0] == TiffConstants.BitsPerSampleRgb12Bit[0])
                    {
                        return TiffBitsPerSample.Rgb121212;
                    }

                    if (bitsPerSample[2] == TiffConstants.BitsPerSampleRgb10Bit[2] &&
                        bitsPerSample[1] == TiffConstants.BitsPerSampleRgb10Bit[1] &&
                        bitsPerSample[0] == TiffConstants.BitsPerSampleRgb10Bit[0])
                    {
                        return TiffBitsPerSample.Rgb101010;
                    }

                    if (bitsPerSample[2] == TiffConstants.BitsPerSampleRgb8Bit[2] &&
                        bitsPerSample[1] == TiffConstants.BitsPerSampleRgb8Bit[1] &&
                        bitsPerSample[0] == TiffConstants.BitsPerSampleRgb8Bit[0])
                    {
                        return TiffBitsPerSample.Rgb888;
                    }

                    if (bitsPerSample[2] == TiffConstants.BitsPerSampleRgb4Bit[2] &&
                        bitsPerSample[1] == TiffConstants.BitsPerSampleRgb4Bit[1] &&
                        bitsPerSample[0] == TiffConstants.BitsPerSampleRgb4Bit[0])
                    {
                        return TiffBitsPerSample.Rgb444;
                    }

                    if (bitsPerSample[2] == TiffConstants.BitsPerSampleRgb2Bit[2] &&
                        bitsPerSample[1] == TiffConstants.BitsPerSampleRgb2Bit[1] &&
                        bitsPerSample[0] == TiffConstants.BitsPerSampleRgb2Bit[0])
                    {
                        return TiffBitsPerSample.Rgb222;
                    }

                    break;

                case 1:
                    if (bitsPerSample[0] == TiffConstants.BitsPerSample1Bit[0])
                    {
                        return TiffBitsPerSample.Bit1;
                    }

                    if (bitsPerSample[0] == TiffConstants.BitsPerSample2Bit[0])
                    {
                        return TiffBitsPerSample.Bit2;
                    }

                    if (bitsPerSample[0] == TiffConstants.BitsPerSample4Bit[0])
                    {
                        return TiffBitsPerSample.Bit4;
                    }

                    if (bitsPerSample[0] == TiffConstants.BitsPerSample6Bit[0])
                    {
                        return TiffBitsPerSample.Bit6;
                    }

                    if (bitsPerSample[0] == TiffConstants.BitsPerSample8Bit[0])
                    {
                        return TiffBitsPerSample.Bit8;
                    }

                    if (bitsPerSample[0] == TiffConstants.BitsPerSample10Bit[0])
                    {
                        return TiffBitsPerSample.Bit10;
                    }

                    if (bitsPerSample[0] == TiffConstants.BitsPerSample12Bit[0])
                    {
                        return TiffBitsPerSample.Bit12;
                    }

                    if (bitsPerSample[0] == TiffConstants.BitsPerSample14Bit[0])
                    {
                        return TiffBitsPerSample.Bit14;
                    }

                    if (bitsPerSample[0] == TiffConstants.BitsPerSample16Bit[0])
                    {
                        return TiffBitsPerSample.Bit16;
                    }

                    break;
            }

            return TiffBitsPerSample.Unknown;
        }
    }
}
