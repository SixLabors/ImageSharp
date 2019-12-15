// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Utility functions for the lossless decoder.
    /// </summary>
    internal static class LosslessUtils
    {
        /// <summary>
        /// Add green to blue and red channels (i.e. perform the inverse transform of 'subtract green').
        /// </summary>
        /// <param name="pixelData">The pixel data to apply the transformation.</param>
        public static void AddGreenToBlueAndRed(uint[] pixelData)
        {
            for (int i = 0; i < pixelData.Length; i++)
            {
                uint argb = pixelData[i];
                uint green = (argb >> 8) & 0xff;
                uint redBlue = argb & 0x00ff00ffu;
                redBlue += (green << 16) | green;
                redBlue &= 0x00ff00ffu;
                pixelData[i] = (argb & 0xff00ff00u) | redBlue;
            }
        }

        public static void ColorIndexInverseTransform(Vp8LTransform transform, uint[] pixelData)
        {
            int bitsPerPixel = 8 >> transform.Bits;
            int width = transform.XSize;
            int height = transform.YSize;
            uint[] colorMap = transform.Data;
            int decodedPixels = 0;
            if (bitsPerPixel < 8)
            {
                int pixelsPerByte = 1 << transform.Bits;
                int countMask = pixelsPerByte - 1;
                int bitMask = (1 << bitsPerPixel) - 1;

                // TODO: use memoryAllocator here
                var decodedPixelData = new uint[width * height];
                int pixelDataPos = 0;
                for (int y = 0; y < height; ++y)
                {
                    uint packedPixels = 0;
                    for (int x = 0; x < width; ++x)
                    {
                        // We need to load fresh 'packed_pixels' once every
                        // 'pixelsPerByte' increments of x. Fortunately, pixelsPerByte
                        // is a power of 2, so can just use a mask for that, instead of
                        // decrementing a counter.
                        if ((x & countMask) is 0)
                        {
                            packedPixels = GetARGBIndex(pixelData[pixelDataPos++]);
                        }

                        decodedPixelData[decodedPixels++] = colorMap[packedPixels & bitMask];
                        packedPixels >>= bitsPerPixel;
                    }
                }

                decodedPixelData.AsSpan().CopyTo(pixelData);

                return;
            }

            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; ++x)
                {
                    uint colorMapIndex = GetARGBIndex(pixelData[decodedPixels]);
                    pixelData[decodedPixels] = colorMap[colorMapIndex];
                    decodedPixels++;
                }
            }
        }

        public static void ColorSpaceInverseTransform(Vp8LTransform transform, uint[] pixelData)
        {
            int width = transform.XSize;
            int yEnd = transform.YSize;
            int tileWidth = 1 << transform.Bits;
            int mask = tileWidth - 1;
            int safeWidth = width & ~mask;
            int remainingWidth = width - safeWidth;
            int tilesPerRow = SubSampleSize(width, transform.Bits);
            int y = 0;
            uint predRow = transform.Data[(y >> transform.Bits) * tilesPerRow];

            int pixelPos = 0;
            while (y < yEnd)
            {
                uint pred = predRow;
                Vp8LMultipliers m = default(Vp8LMultipliers);
                int srcSafeEnd = pixelPos + safeWidth;
                int srcEnd = pixelPos + width;
                while (pixelPos < srcSafeEnd)
                {
                    ColorCodeToMultipliers(pred++, ref m);
                    TransformColorInverse(m, pixelData, pixelPos, tileWidth);
                    pixelPos += tileWidth;
                }

                if (pixelPos < srcEnd)
                {
                    ColorCodeToMultipliers(pred++, ref m);
                    TransformColorInverse(m, pixelData, pixelPos, remainingWidth);
                    pixelPos += remainingWidth;
                }

                ++y;
                if ((y & mask) == 0)
                {
                    predRow += (uint)tilesPerRow;
                }
            }
        }

        public static void TransformColorInverse(Vp8LMultipliers m, uint[] pixelData, int start, int numPixels)
        {
            int end = start + numPixels;
            for (int i = start; i < end; i++)
            {
                uint argb = pixelData[i];
                sbyte green = (sbyte)(argb >> 8);
                uint red = argb >> 16;
                int newRed = (int)(red & 0xff);
                int newBlue = (int)argb & 0xff;
                newRed += ColorTransformDelta(m.GreenToRed, (sbyte)green);
                newRed &= 0xff;
                newBlue += ColorTransformDelta(m.GreenToBlue, (sbyte)green);
                newBlue += ColorTransformDelta(m.RedToBlue, (sbyte)newRed);
                newBlue &= 0xff;

                // uint pixelValue = (uint)((argb & 0xff00ff00u) | (newRed << 16) | newBlue);
                pixelData[i] = (uint)((argb & 0xff00ff00u) | (newRed << 16) | newBlue);
            }
        }

        public static uint[] ExpandColorMap(int numColors, Vp8LTransform transform, uint[] transformData)
        {
            int finalNumColors = 1 << (8 >> transform.Bits);

            // TODO: use memoryAllocator here
            var newColorMap = new uint[finalNumColors];
            newColorMap[0] = transformData[0];

            Span<byte> data = MemoryMarshal.Cast<uint, byte>(transformData);
            Span<byte> newData = MemoryMarshal.Cast<uint, byte>(newColorMap);
            int i;
            for (i = 4; i < 4 * numColors; ++i)
            {
                // Equivalent to AddPixelEq(), on a byte-basis.
                newData[i] = (byte)((data[i] + newData[i - 4]) & 0xff);
            }

            for (; i < 4 * finalNumColors; ++i)
            {
                newData[i] = 0;  // black tail.
            }

            return newColorMap;
        }

        public static void PredictorInverseTransform(Vp8LTransform transform, uint[] pixelData)
        {
            int processedPixels = 0;
            int yStart = 0;
            int width = transform.XSize;

            // PredictorAdd0(in, NULL, 1, out);
            PredictorAdd1(pixelData, width - 1);
            processedPixels += width;
            yStart++;

            int y = yStart;
            int tileWidth = 1 << transform.Bits;
            int mask = tileWidth - 1;
            int tilesPerRow = SubSampleSize(width, transform.Bits);
        }

        private static uint Predictor0C(uint left, uint[] top)
        {
            return WebPConstants.ArgbBlack;
        }

        /// <summary>
        /// Computes sampled size of 'size' when sampling using 'sampling bits'.
        /// </summary>
        public static int SubSampleSize(int size, int samplingBits)
        {
            return (size + (1 << samplingBits) - 1) >> samplingBits;
        }

        /// <summary>
        /// Sum of each component, mod 256.
        /// </summary>
        private static uint AddPixels(uint a, uint b)
        {
            uint alphaAndGreen = (a & 0xff00ff00u) + (b & 0xff00ff00u);
            uint redAndBlue = (a & 0x00ff00ffu) + (b & 0x00ff00ffu);
            return (alphaAndGreen & 0xff00ff00u) | (redAndBlue & 0x00ff00ffu);
        }

        /// <summary>
        /// Difference of each component, mod 256.
        /// </summary>
        private static uint SubPixels(uint a, uint b)
        {
            uint alphaAndGreen = 0x00ff00ffu + (a & 0xff00ff00u) - (b & 0xff00ff00u);
            uint redAndBlue = 0xff00ff00u + (a & 0x00ff00ffu) - (b & 0x00ff00ffu);
            return (alphaAndGreen & 0xff00ff00u) | (redAndBlue & 0x00ff00ffu);
        }

        private static void PredictorAdd1(uint[] pixelData, int numPixels)
        {
            /*for (int i = 0; i < num_pixels; ++i)
            {
                pixelData[i] = VP8LAddPixels(in[i], left);
            }*/
        }

        private static uint GetARGBIndex(uint idx)
        {
            return (idx >> 8) & 0xff;
        }

        private static int ColorTransformDelta(sbyte colorPred, sbyte color)
        {
            return ((int)colorPred * color) >> 5;
        }

        private static void ColorCodeToMultipliers(uint colorCode, ref Vp8LMultipliers m)
        {
            m.GreenToRed = (sbyte)(colorCode & 0xff);
            m.GreenToBlue = (sbyte)((colorCode >> 8) & 0xff);
            m.RedToBlue = (sbyte)((colorCode >> 16) & 0xff);
        }

        internal struct Vp8LMultipliers
        {
            public sbyte GreenToRed;

            public sbyte GreenToBlue;

            public sbyte RedToBlue;
        }
    }
}
