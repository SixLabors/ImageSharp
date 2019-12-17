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
            // TODO: use memory allocator instead
            var output = new uint[pixelData.Length];

            int processedPixels = 0;
            int yStart = 0;
            int width = transform.XSize;

            // First Row follows the L (mode=1) mode.
            PredictorAdd0(pixelData, null, processedPixels++, 1, output);
            PredictorAdd1(pixelData, null, processedPixels, width - 1, output);
            processedPixels += width - 1;
            yStart++;

            int y = yStart;
            int yEnd = transform.YSize;
            int tileWidth = 1 << transform.Bits;
            int mask = tileWidth - 1;
            int tilesPerRow = SubSampleSize(width, transform.Bits);

            while (y < yEnd)
            {
                int x = 1;

                // First pixel follows the T (mode=2) mode.
                PredictorAdd2(pixelData, out-width, 1, output);

                // .. the rest:
            }
        }

        // TODO: the pridictor add methods should be generated
        private static void PredictorAdd0(uint[] input, uint[] upper, int startIdx, int numberOfPixels, uint[] output)
        {
            for (int x = startIdx; x < numberOfPixels; ++x)
            {
                uint pred = Predictor0();
                output[x] = AddPixels(input[x], pred);
            }
        }

        private static void PredictorAdd1(uint[] input, uint[] upper, int startIdx, int numberOfPixels, uint[] output)
        {
            uint left = output[startIdx - 1];
            for (int i = 0; i < numberOfPixels; ++i)
            {
                output[i] = left = AddPixels(input[i], left);
            }
        }

        private static void PredictorAdd2(uint[] input, uint[] upper, int startIdx, int numberOfPixels, uint[] output)
        {
            for (int x = startIdx; x < numberOfPixels; ++x)
            {
                uint pred = Predictor2(output[x - 1], upper.AsSpan(x));
                output[x] = AddPixels(input[x], pred);
            }
        }

        private static void PredictorAdd3(uint[] input, uint[] upper, int startIdx, int numberOfPixels, uint[] output)
        {
            for (int x = startIdx; x < numberOfPixels; ++x)
            {
                uint pred = Predictor3(output[x - 1], upper.AsSpan(x));
                output[x] = AddPixels(input[x], pred);
            }
        }

        private static void PredictorAdd4(uint[] input, uint[] upper, int startIdx, int numberOfPixels, uint[] output)
        {
            for (int x = startIdx; x < numberOfPixels; ++x)
            {
                uint pred = Predictor4(output[x - 1], upper.AsSpan(x));
                output[x] = AddPixels(input[x], pred);
            }
        }

        private static void PredictorAdd5(uint[] input, uint[] upper, int startIdx, int numberOfPixels, uint[] output)
        {
            for (int x = startIdx; x < numberOfPixels; ++x)
            {
                uint pred = Predictor5(output[x - 1], upper.AsSpan(x));
                output[x] = AddPixels(input[x], pred);
            }
        }

        private static void PredictorAdd6(uint[] input, uint[] upper, int startIdx, int numberOfPixels, uint[] output)
        {
            for (int x = startIdx; x < numberOfPixels; ++x)
            {
                uint pred = Predictor6(output[x - 1], upper.AsSpan(x));
                output[x] = AddPixels(input[x], pred);
            }
        }

        private static void PredictorAdd7(uint[] input, uint[] upper, int startIdx, int numberOfPixels, uint[] output)
        {
            for (int x = startIdx; x < numberOfPixels; ++x)
            {
                uint pred = Predictor7(output[x - 1], upper.AsSpan(x));
                output[x] = AddPixels(input[x], pred);
            }
        }

        private static void PredictorAdd8(uint[] input, uint[] upper, int startIdx, int numberOfPixels, uint[] output)
        {
            for (int x = startIdx; x < numberOfPixels; ++x)
            {
                uint pred = Predictor8(output[x - 1], upper.AsSpan(x));
                output[x] = AddPixels(input[x], pred);
            }
        }

        private static void PredictorAdd9(uint[] input, uint[] upper, int startIdx, int numberOfPixels, uint[] output)
        {
            for (int x = startIdx; x < numberOfPixels; ++x)
            {
                uint pred = Predictor9(output[x - 1], upper.AsSpan(x));
                output[x] = AddPixels(input[x], pred);
            }
        }

        private static void PredictorAdd10(uint[] input, uint[] upper, int startIdx, int numberOfPixels, uint[] output)
        {
            for (int x = startIdx; x < numberOfPixels; ++x)
            {
                uint pred = Predictor10(output[x - 1], upper.AsSpan(x));
                output[x] = AddPixels(input[x], pred);
            }
        }

        private static void PredictorAdd11(uint[] input, uint[] upper, int startIdx, int numberOfPixels, uint[] output)
        {
            for (int x = startIdx; x < numberOfPixels; ++x)
            {
                uint pred = Predictor11(output[x - 1], upper.AsSpan(x));
                output[x] = AddPixels(input[x], pred);
            }
        }

        private static void PredictorAdd12(uint[] input, uint[] upper, int startIdx, int numberOfPixels, uint[] output)
        {
            for (int x = startIdx; x < numberOfPixels; ++x)
            {
                uint pred = Predictor12(output[x - 1], upper.AsSpan(x));
                output[x] = AddPixels(input[x], pred);
            }
        }

        private static void PredictorAdd13(uint[] input, uint[] upper, int startIdx, int numberOfPixels, uint[] output)
        {
            for (int x = startIdx; x < numberOfPixels; ++x)
            {
                uint pred = Predictor13(output[x - 1], upper.AsSpan(x));
                output[x] = AddPixels(input[x], pred);
            }
        }

        private static uint Predictor0()
        {
            return WebPConstants.ArgbBlack;
        }

        private static uint Predictor1(uint left, uint[] top)
        {
            return left;
        }

        private static uint Predictor2(uint left, Span<uint> top)
        {
            return top[0];
        }

        private static uint Predictor3(uint left, Span<uint> top)
        {
            return top[1];
        }

        private static uint Predictor4(uint left, Span<uint> top)
        {
            return top[-1];
        }

        private static uint Predictor5(uint left, Span<uint> top)
        {
            uint pred = Average3(left, top[0], top[1]);
            return pred;
        }

        private static uint Predictor6(uint left, Span<uint> top)
        {
            uint pred = Average2(left, top[-1]);
            return pred;
        }

        private static uint Predictor7(uint left, Span<uint> top)
        {
            uint pred = Average2(left, top[0]);
            return pred;
        }

        private static uint Predictor8(uint left, Span<uint> top)
        {
            uint pred = Average2(top[-1], top[0]);
            return pred;
        }

        private static uint Predictor9(uint left, Span<uint> top)
        {
            uint pred = Average2(top[0], top[1]);
            return pred;
        }

        private static uint Predictor10(uint left, Span<uint> top)
        {
            uint pred = Average4(left, top[-1], top[0], top[1]);
            return pred;
        }

        private static uint Predictor11(uint left, Span<uint> top)
        {
            uint pred = Select(top[0], left, top[-1]);
            return pred;
        }

        private static uint Predictor12(uint left, Span<uint> top)
        {
            uint pred = ClampedAddSubtractFull(left, top[0], top[-1]);
            return pred;
        }

        private static uint Predictor13(uint left, Span<uint> top)
        {
            uint pred = ClampedAddSubtractHalf(left, top[0], top[-1]);
            return pred;
        }

        private static uint ClampedAddSubtractFull(uint c0, uint c1, uint c2)
        {
            int a = AddSubtractComponentFull(c0 >> 24, c1 >> 24, c2 >> 24);
            int r = AddSubtractComponentFull((c0 >> 16) & 0xff,
                (c1 >> 16) & 0xff,
                (c2 >> 16) & 0xff);
            int g = AddSubtractComponentFull((c0 >> 8) & 0xff,
                (c1 >> 8) & 0xff,
                (c2 >> 8) & 0xff);
            int b = AddSubtractComponentFull(c0 & 0xff, c1 & 0xff, c2 & 0xff);
            return (uint)(((uint)a << 24) | (r << 16) | (g << 8) | b);
        }

        private static uint ClampedAddSubtractHalf(uint c0, uint c1, uint c2)
        {
            uint ave = Average2(c0, c1);
            int a = AddSubtractComponentHalf(ave >> 24, c2 >> 24);
            int r = AddSubtractComponentHalf((ave >> 16) & 0xff, (c2 >> 16) & 0xff);
            int g = AddSubtractComponentHalf((ave >> 8) & 0xff, (c2 >> 8) & 0xff);
            int b = AddSubtractComponentHalf((ave >> 0) & 0xff, (c2 >> 0) & 0xff);
            return (uint)(((uint)a << 24) | (r << 16) | (g << 8) | b);
        }

        private static int AddSubtractComponentHalf(uint a, uint b)
        {
            return (int)Clip255(a + ((a - b) / 2));
        }

        private static int AddSubtractComponentFull(uint a, uint b, uint c)
        {
            return (int)Clip255(a + b - c);
        }

        private static uint Clip255(uint a)
        {
            if (a < 256)
            {
                return a;
            }

            // return 0, when a is a negative integer.
            // return 255, when a is positive.
            return ~a >> 24;
        }

        private static uint Select(uint a, uint b, uint c)
        {
            int paMinusPb =
                Sub3(a >> 24, b >> 24, c >> 24) +
                Sub3((a >> 16) & 0xff, (b >> 16) & 0xff, (c >> 16) & 0xff) +
                Sub3((a >> 8) & 0xff, (b >> 8) & 0xff, (c >> 8) & 0xff) +
                Sub3( a & 0xff, b & 0xff, c & 0xff);
            return (paMinusPb <= 0) ? a : b;
        }

        private static int Sub3(uint a, uint b, uint c)
        {
            uint pb = b - c;
            uint pa = a - c;
            return (int)(Math.Abs(pb) - Math.Abs(pa));
        }

        private static uint Average2(uint a0, uint a1)
        {
            return (((a0 ^ a1) & 0xfefefefeu) >> 1) + (a0 & a1);
        }

        private static uint Average3(uint a0, uint a1, uint a2)
        {
            return Average2(Average2(a0, a2), a1);
        }

        private static uint Average4(uint a0, uint a1, uint a2, uint a3)
        {
            return Average2(Average2(a0, a1), Average2(a2, a3));
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
