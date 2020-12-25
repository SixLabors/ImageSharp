// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace SixLabors.ImageSharp.Formats.Experimental.Webp.Lossless
{
    /// <summary>
    /// Utility functions for the lossless decoder.
    /// </summary>
    internal static unsafe class LosslessUtils
    {
        private const int PrefixLookupIdxMax = 512;

        private const int LogLookupIdxMax = 256;

        private const int ApproxLogMax = 4096;

        private const int ApproxLogWithCorrectionMax = 65536;

        private const double Log2Reciprocal = 1.44269504088896338700465094007086;

        /// <summary>
        /// Returns the exact index where array1 and array2 are different. For an index
        /// inferior or equal to bestLenMatch, the return value just has to be strictly
        /// inferior to best_lenMatch. The current behavior is to return 0 if this index
        /// is bestLenMatch, and the index itself otherwise.
        /// If no two elements are the same, it returns maxLimit.
        /// </summary>
        public static int FindMatchLength(Span<uint> array1, Span<uint> array2, int bestLenMatch, int maxLimit)
        {
            // Before 'expensive' linear match, check if the two arrays match at the
            // current best length index.
            if (array1[bestLenMatch] != array2[bestLenMatch])
            {
                return 0;
            }

            return VectorMismatch(array1, array2, maxLimit);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static int VectorMismatch(Span<uint> array1, Span<uint> array2, int length)
        {
            int matchLen = 0;

            while (matchLen < length && array1[matchLen] == array2[matchLen])
            {
                matchLen++;
            }

            return matchLen;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static int MaxFindCopyLength(int len)
        {
            return (len < BackwardReferenceEncoder.MaxLength) ? len : BackwardReferenceEncoder.MaxLength;
        }

        public static int PrefixEncodeBits(int distance, ref int extraBits)
        {
            if (distance < PrefixLookupIdxMax)
            {
                (int code, int extraBits) prefixCode = WebpLookupTables.PrefixEncodeCode[distance];
                extraBits = prefixCode.extraBits;
                return prefixCode.code;
            }
            else
            {
                return PrefixEncodeBitsNoLut(distance, ref extraBits);
            }
        }

        public static int PrefixEncode(int distance, ref int extraBits, ref int extraBitsValue)
        {
            if (distance < PrefixLookupIdxMax)
            {
                (int code, int extraBits) prefixCode = WebpLookupTables.PrefixEncodeCode[distance];
                extraBits = prefixCode.extraBits;
                extraBitsValue = WebpLookupTables.PrefixEncodeExtraBitsValue[distance];

                return prefixCode.code;
            }
            else
            {
                return PrefixEncodeNoLUT(distance, ref extraBits, ref extraBitsValue);
            }
        }

        /// <summary>
        /// Add green to blue and red channels (i.e. perform the inverse transform of 'subtract green').
        /// </summary>
        /// <param name="pixelData">The pixel data to apply the transformation.</param>
        public static void AddGreenToBlueAndRed(Span<uint> pixelData)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Avx2.IsSupported)
            {
                var mask = Vector256.Create(1, 255, 1, 255, 5, 255, 5, 255, 9, 255, 9, 255, 13, 255, 13, 255, 17, 255, 17, 255, 21, 255, 21, 255, 25, 255, 25, 255, 29, 255, 29, 255);
                int numPixels = pixelData.Length;
                int i;
                fixed (uint* p = pixelData)
                {
                    for (i = 0; i + 8 <= numPixels; i += 8)
                    {
                        var idx = p + i;
                        Vector256<byte> input = Avx.LoadVector256((ushort*)idx).AsByte();
                        Vector256<byte> in0g0g = Avx2.Shuffle(input, mask);
                        Vector256<byte> output = Avx2.Add(input, in0g0g);
                        Avx.Store((byte*)idx, output);
                    }

                    if (i != numPixels)
                    {
                        AddGreenToBlueAndRedNoneVectorized(pixelData.Slice(i));
                    }
                }
            }
            else if (Ssse3.IsSupported)
            {
                var mask = Vector128.Create(1, 255, 1, 255, 5, 255, 5, 255, 9, 255, 9, 255, 13, 255, 13, 255);
                int numPixels = pixelData.Length;
                int i;
                fixed (uint* p = pixelData)
                {
                    for (i = 0; i + 4 <= numPixels; i += 4)
                    {
                        var idx = p + i;
                        Vector128<byte> input = Sse2.LoadVector128((ushort*)idx).AsByte();
                        Vector128<byte> in0g0g = Ssse3.Shuffle(input, mask);
                        Vector128<byte> output = Sse2.Add(input, in0g0g);
                        Sse2.Store((byte*)idx, output.AsByte());
                    }

                    if (i != numPixels)
                    {
                        AddGreenToBlueAndRedNoneVectorized(pixelData.Slice(i));
                    }
                }
            }
            else if (Sse2.IsSupported)
            {
                var mask = SimdUtils.Shuffle.MmShuffle(2, 2, 0, 0);
                int numPixels = pixelData.Length;
                int i;
                fixed (uint* p = pixelData)
                {
                    for (i = 0; i + 4 <= numPixels; i += 4)
                    {
                        var idx = p + i;
                        Vector128<ushort> input = Sse2.LoadVector128((ushort*)idx);
                        Vector128<ushort> a = Sse2.ShiftRightLogical(input.AsUInt16(), 8); // 0 a 0 g
                        Vector128<ushort> b = Sse2.ShuffleLow(a, mask);
                        Vector128<ushort> c = Sse2.ShuffleHigh(b, mask); // 0g0g
                        Vector128<byte> output = Sse2.Add(input.AsByte(), c.AsByte());
                        Sse2.Store((byte*)idx, output);
                    }

                    if (i != numPixels)
                    {
                        AddGreenToBlueAndRedNoneVectorized(pixelData.Slice(i));
                    }
                }
            }
            else
#endif
            {
                AddGreenToBlueAndRedNoneVectorized(pixelData);
            }
        }

        private static void AddGreenToBlueAndRedNoneVectorized(Span<uint> pixelData)
        {
            int numPixels = pixelData.Length;
            for (int i = 0; i < numPixels; i++)
            {
                uint argb = pixelData[i];
                uint green = (argb >> 8) & 0xff;
                uint redBlue = argb & 0x00ff00ffu;
                redBlue += (green << 16) | green;
                redBlue &= 0x00ff00ffu;
                pixelData[i] = (argb & 0xff00ff00u) | redBlue;
            }
        }

        public static void SubtractGreenFromBlueAndRed(Span<uint> pixelData)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Avx2.IsSupported)
            {
                var mask = Vector256.Create(1, 255, 1, 255, 5, 255, 5, 255, 9, 255, 9, 255, 13, 255, 13, 255, 17, 255, 17, 255, 21, 255, 21, 255, 25, 255, 25, 255, 29, 255, 29, 255);
                int numPixels = pixelData.Length;
                int i;
                fixed (uint* p = pixelData)
                {
                    for (i = 0; i + 8 <= numPixels; i += 8)
                    {
                        var idx = p + i;
                        Vector256<byte> input = Avx.LoadVector256((ushort*)idx).AsByte();
                        Vector256<byte> in0g0g = Avx2.Shuffle(input, mask);
                        Vector256<byte> output = Avx2.Subtract(input, in0g0g);
                        Avx.Store((byte*)idx, output);
                    }

                    if (i != numPixels)
                    {
                        SubtractGreenFromBlueAndRedNoneVectorized(pixelData.Slice(i));
                    }
                }
            }
            else if (Ssse3.IsSupported)
            {
                var mask = Vector128.Create(1, 255, 1, 255, 5, 255, 5, 255, 9, 255, 9, 255, 13, 255, 13, 255);
                int numPixels = pixelData.Length;
                int i;
                fixed (uint* p = pixelData)
                {
                    for (i = 0; i + 4 <= numPixels; i += 4)
                    {
                        var idx = p + i;
                        Vector128<byte> input = Sse2.LoadVector128((ushort*)idx).AsByte();
                        Vector128<byte> in0g0g = Ssse3.Shuffle(input, mask);
                        Vector128<byte> output = Sse2.Subtract(input, in0g0g);
                        Sse2.Store((byte*)idx, output.AsByte());
                    }

                    if (i != numPixels)
                    {
                        SubtractGreenFromBlueAndRedNoneVectorized(pixelData.Slice(i));
                    }
                }
            }
            else if (Sse2.IsSupported)
            {
                var mask = SimdUtils.Shuffle.MmShuffle(2, 2, 0, 0);
                int numPixels = pixelData.Length;
                int i;
                fixed (uint* p = pixelData)
                {
                    for (i = 0; i + 4 <= numPixels; i += 4)
                    {
                        var idx = p + i;
                        Vector128<ushort> input = Sse2.LoadVector128((ushort*)idx);
                        Vector128<ushort> a = Sse2.ShiftRightLogical(input.AsUInt16(), 8); // 0 a 0 g
                        Vector128<ushort> b = Sse2.ShuffleLow(a, mask);
                        Vector128<ushort> c = Sse2.ShuffleHigh(b, mask); // 0g0g
                        Vector128<byte> output = Sse2.Subtract(input.AsByte(), c.AsByte());
                        Sse2.Store((byte*)idx, output);
                    }

                    if (i != numPixels)
                    {
                        SubtractGreenFromBlueAndRedNoneVectorized(pixelData.Slice(i));
                    }
                }
            }
            else
#endif
            {
                SubtractGreenFromBlueAndRedNoneVectorized(pixelData);
            }
        }

        private static void SubtractGreenFromBlueAndRedNoneVectorized(Span<uint> pixelData)
        {
            int numPixels = pixelData.Length;
            for (int i = 0; i < numPixels; i++)
            {
                uint argb = pixelData[i];
                uint green = (argb >> 8) & 0xff;
                uint newR = (((argb >> 16) & 0xff) - green) & 0xff;
                uint newB = (((argb >> 0) & 0xff) - green) & 0xff;
                pixelData[i] = (argb & 0xff00ff00u) | (newR << 16) | newB;
            }
        }

        /// <summary>
        /// If there are not many unique pixel values, it is more efficient to create a color index array and replace the pixel values by the array's indices.
        /// This will reverse the color index transform.
        /// </summary>
        /// <param name="transform">The transform data contains color table size and the entries in the color table.</param>
        /// <param name="pixelData">The pixel data to apply the reverse transform on.</param>
        public static void ColorIndexInverseTransform(Vp8LTransform transform, Span<uint> pixelData)
        {
            int bitsPerPixel = 8 >> transform.Bits;
            int width = transform.XSize;
            int height = transform.YSize;
            Span<uint> colorMap = transform.Data.GetSpan();
            int decodedPixels = 0;
            if (bitsPerPixel < 8)
            {
                int pixelsPerByte = 1 << transform.Bits;
                int countMask = pixelsPerByte - 1;
                int bitMask = (1 << bitsPerPixel) - 1;

                var decodedPixelData = new uint[width * height];
                int pixelDataPos = 0;
                for (int y = 0; y < height; ++y)
                {
                    uint packedPixels = 0;
                    for (int x = 0; x < width; ++x)
                    {
                        // We need to load fresh 'packed_pixels' once every
                        // 'pixelsPerByte' increments of x. Fortunately, pixelsPerByte
                        // is a power of 2, so we can just use a mask for that, instead of
                        // decrementing a counter.
                        if ((x & countMask) == 0)
                        {
                            packedPixels = GetArgbIndex(pixelData[pixelDataPos++]);
                        }

                        decodedPixelData[decodedPixels++] = colorMap[(int)(packedPixels & bitMask)];
                        packedPixels >>= bitsPerPixel;
                    }
                }

                decodedPixelData.AsSpan().CopyTo(pixelData);
            }
            else
            {
                for (int y = 0; y < height; ++y)
                {
                    for (int x = 0; x < width; ++x)
                    {
                        uint colorMapIndex = GetArgbIndex(pixelData[decodedPixels]);
                        pixelData[decodedPixels] = colorMap[(int)colorMapIndex];
                        decodedPixels++;
                    }
                }
            }
        }

        /// <summary>
        /// The goal of the color transform is to de-correlate the R, G and B values of each pixel.
        /// Color transform keeps the green (G) value as it is, transforms red (R) based on green and transforms blue (B) based on green and then based on red.
        /// </summary>
        /// <param name="transform">The transform data.</param>
        /// <param name="pixelData">The pixel data to apply the inverse transform on.</param>
        public static void ColorSpaceInverseTransform(Vp8LTransform transform, Span<uint> pixelData)
        {
            int width = transform.XSize;
            int yEnd = transform.YSize;
            int tileWidth = 1 << transform.Bits;
            int mask = tileWidth - 1;
            int safeWidth = width & ~mask;
            int remainingWidth = width - safeWidth;
            int tilesPerRow = SubSampleSize(width, transform.Bits);
            int y = 0;
            int predRowIdxStart = (y >> transform.Bits) * tilesPerRow;
            Span<uint> transformData = transform.Data.GetSpan();

            int pixelPos = 0;
            while (y < yEnd)
            {
                int predRowIdx = predRowIdxStart;
                var m = default(Vp8LMultipliers);
                int srcSafeEnd = pixelPos + safeWidth;
                int srcEnd = pixelPos + width;
                while (pixelPos < srcSafeEnd)
                {
                    uint colorCode = transformData[predRowIdx++];
                    ColorCodeToMultipliers(colorCode, ref m);
                    TransformColorInverse(m, pixelData, pixelPos, tileWidth);
                    pixelPos += tileWidth;
                }

                if (pixelPos < srcEnd)
                {
                    uint colorCode = transformData[predRowIdx];
                    ColorCodeToMultipliers(colorCode, ref m);
                    TransformColorInverse(m, pixelData, pixelPos, remainingWidth);
                    pixelPos += remainingWidth;
                }

                ++y;
                if ((y & mask) == 0)
                {
                    predRowIdxStart += tilesPerRow;
                }
            }
        }

        public static void TransformColor(Vp8LMultipliers m, Span<uint> data, int numPixels)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Sse2.IsSupported)
            {
                Vector128<int> multsrb = MkCst16(Cst5b(m.GreenToRed), Cst5b(m.GreenToBlue));
                Vector128<int> multsb2 = MkCst16(Cst5b(m.RedToBlue), 0);
                var maskalphagreen = Vector128.Create(0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255);
                var maskredblue = Vector128.Create(255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0);
                var shufflemask = SimdUtils.Shuffle.MmShuffle(2, 2, 0, 0);
                int idx;
                fixed (uint* src = data)
                {
                    for (idx = 0; idx + 4 <= numPixels; idx += 4)
                    {
                        var pos = src + idx;
                        Vector128<uint> input = Sse2.LoadVector128(pos);
                        Vector128<byte> a = Sse2.And(input.AsByte(), maskalphagreen);
                        Vector128<short> b = Sse2.ShuffleLow(a.AsInt16(), shufflemask);
                        Vector128<short> c = Sse2.ShuffleHigh(b.AsInt16(), shufflemask);
                        Vector128<short> d = Sse2.MultiplyHigh(c.AsInt16(), multsrb.AsInt16());
                        Vector128<short> e = Sse2.ShiftLeftLogical(input.AsInt16(), 8);
                        Vector128<short> f = Sse2.MultiplyHigh(e.AsInt16(), multsb2.AsInt16());
                        Vector128<int> g = Sse2.ShiftRightLogical(f.AsInt32(), 16);
                        Vector128<byte> h = Sse2.Add(g.AsByte(), d.AsByte());
                        Vector128<byte> i = Sse2.And(h, maskredblue);
                        Vector128<byte> output = Sse2.Subtract(input.AsByte(), i);
                        Sse2.Store((byte*)pos, output);
                    }

                    if (idx != numPixels)
                    {
                        TransformColorNoneVectorized(m, data.Slice(idx), numPixels - idx);
                    }
                }
            }
            else
#endif
            {
                TransformColorNoneVectorized(m, data, numPixels);
            }
        }

        public static void TransformColorNoneVectorized(Vp8LMultipliers m, Span<uint> data, int numPixels)
        {
            for (int i = 0; i < numPixels; i++)
            {
                uint argb = data[i];
                sbyte green = U32ToS8(argb >> 8);
                sbyte red = U32ToS8(argb >> 16);
                int newRed = red & 0xff;
                int newBlue = (int)(argb & 0xff);
                newRed -= ColorTransformDelta((sbyte)m.GreenToRed, green);
                newRed &= 0xff;
                newBlue -= ColorTransformDelta((sbyte)m.GreenToBlue, green);
                newBlue -= ColorTransformDelta((sbyte)m.RedToBlue, red);
                newBlue &= 0xff;
                data[i] = (argb & 0xff00ff00u) | ((uint)newRed << 16) | (uint)newBlue;
            }
        }

        /// <summary>
        /// Reverses the color space transform.
        /// </summary>
        /// <param name="m">The color transform element.</param>
        /// <param name="pixelData">The pixel data to apply the inverse transform on.</param>
        /// <param name="start">The start index of reverse transform.</param>
        /// <param name="numPixels">The number of pixels to apply the transform.</param>
        public static void TransformColorInverse(Vp8LMultipliers m, Span<uint> pixelData, int start, int numPixels)
        {
            int end = start + numPixels;
            for (int i = start; i < end; i++)
            {
                uint argb = pixelData[i];
                sbyte green = (sbyte)(argb >> 8);
                uint red = argb >> 16;
                int newRed = (int)(red & 0xff);
                int newBlue = (int)argb & 0xff;
                newRed += ColorTransformDelta((sbyte)m.GreenToRed, green);
                newRed &= 0xff;
                newBlue += ColorTransformDelta((sbyte)m.GreenToBlue, green);
                newBlue += ColorTransformDelta((sbyte)m.RedToBlue, (sbyte)newRed);
                newBlue &= 0xff;

                pixelData[i] = (argb & 0xff00ff00u) | ((uint)newRed << 16) | (uint)newBlue;
            }
        }

        /// <summary>
        /// This will reverse the predictor transform.
        /// The predictor transform can be used to reduce entropy by exploiting the fact that neighboring pixels are often correlated.
        /// In the predictor transform, the current pixel value is predicted from the pixels already decoded (in scan-line order) and only the residual value (actual - predicted) is encoded.
        /// The prediction mode determines the type of prediction to use. The image is divided into squares and all the pixels in a square use same prediction mode.
        /// </summary>
        /// <param name="transform">The transform data.</param>
        /// <param name="pixelData">The pixel data to apply the inverse transform.</param>
        /// <param name="outputSpan">The resulting pixel data with the reversed transformation data.</param>
        public static void PredictorInverseTransform(
            Vp8LTransform transform,
            Span<uint> pixelData,
            Span<uint> outputSpan)
        {
            fixed (uint* inputFixed = pixelData)
            fixed (uint* outputFixed = outputSpan)
            {
                uint* input = inputFixed;
                uint* output = outputFixed;

                int width = transform.XSize;
                Span<uint> transformData = transform.Data.GetSpan();

                // First Row follows the L (mode=1) mode.
                PredictorAdd0(input, 1, output);
                PredictorAdd1(input + 1, width - 1, output + 1);
                input += width;
                output += width;

                int y = 1;
                int yEnd = transform.YSize;
                int tileWidth = 1 << transform.Bits;
                int mask = tileWidth - 1;
                int tilesPerRow = SubSampleSize(width, transform.Bits);
                int predictorModeIdxBase = (y >> transform.Bits) * tilesPerRow;
                while (y < yEnd)
                {
                    int predictorModeIdx = predictorModeIdxBase;
                    int x = 1;

                    // First pixel follows the T (mode=2) mode.
                    PredictorAdd2(input, output - width, 1, output);

                    // .. the rest:
                    while (x < width)
                    {
                        uint predictorMode = (transformData[predictorModeIdx++] >> 8) & 0xf;
                        int xEnd = (x & ~mask) + tileWidth;
                        if (xEnd > width)
                        {
                            xEnd = width;
                        }

                        // There are 14 different prediction modes.
                        // In each prediction mode, the current pixel value is predicted from one
                        // or more neighboring pixels whose values are already known.
                        switch (predictorMode)
                        {
                            case 0:
                                PredictorAdd0(input + x, xEnd - x, output + x);
                                break;
                            case 1:
                                PredictorAdd1(input + x, xEnd - x, output + x);
                                break;
                            case 2:
                                PredictorAdd2(input + x, output + x - width, xEnd - x, output + x);
                                break;
                            case 3:
                                PredictorAdd3(input + x, output + x - width, xEnd - x, output + x);
                                break;
                            case 4:
                                PredictorAdd4(input + x, output + x - width, xEnd - x, output + x);
                                break;
                            case 5:
                                PredictorAdd5(input + x, output + x - width, xEnd - x, output + x);
                                break;
                            case 6:
                                PredictorAdd6(input + x, output + x - width, xEnd - x, output + x);
                                break;
                            case 7:
                                PredictorAdd7(input + x, output + x - width, xEnd - x, output + x);
                                break;
                            case 8:
                                PredictorAdd8(input + x, output + x - width, xEnd - x, output + x);
                                break;
                            case 9:
                                PredictorAdd9(input + x, output + x - width, xEnd - x, output + x);
                                break;
                            case 10:
                                PredictorAdd10(input + x, output + x - width, xEnd - x, output + x);
                                break;
                            case 11:
                                PredictorAdd11(input + x, output + x - width, xEnd - x, output + x);
                                break;
                            case 12:
                                PredictorAdd12(input + x, output + x - width, xEnd - x, output + x);
                                break;
                            case 13:
                                PredictorAdd13(input + x, output + x - width, xEnd - x, output + x);
                                break;
                        }

                        x = xEnd;
                    }

                    input += width;
                    output += width;
                    ++y;

                    if ((y & mask) == 0)
                    {
                        // Use the same mask, since tiles are squares.
                        predictorModeIdxBase += tilesPerRow;
                    }
                }
            }

            outputSpan.CopyTo(pixelData);
        }

        public static void ExpandColorMap(int numColors, Span<uint> transformData, Span<uint> newColorMap)
        {
            newColorMap[0] = transformData[0];
            Span<byte> data = MemoryMarshal.Cast<uint, byte>(transformData);
            Span<byte> newData = MemoryMarshal.Cast<uint, byte>(newColorMap);
            int numColorsX4 = 4 * numColors;
            int i;
            for (i = 4; i < numColorsX4; i++)
            {
                // Equivalent to AddPixelEq(), on a byte-basis.
                newData[i] = (byte)((data[i] + newData[i - 4]) & 0xff);
            }

            int colorMapLength4 = 4 * newColorMap.Length;
            for (; i < colorMapLength4; i++)
            {
                newData[i] = 0;  // black tail.
            }
        }

        /// <summary>
        /// Difference of each component, mod 256.
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static uint SubPixels(uint a, uint b)
        {
            uint alphaAndGreen = 0x00ff00ffu + (a & 0xff00ff00u) - (b & 0xff00ff00u);
            uint redAndBlue = 0xff00ff00u + (a & 0x00ff00ffu) - (b & 0x00ff00ffu);
            return (alphaAndGreen & 0xff00ff00u) | (redAndBlue & 0x00ff00ffu);
        }

        /// <summary>
        /// Bundles multiple (1, 2, 4 or 8) pixels into a single pixel.
        /// </summary>
        public static void BundleColorMap(Span<byte> row, int width, int xBits, Span<uint> dst)
        {
            int x;
            if (xBits > 0)
            {
                int bitDepth = 1 << (3 - xBits);
                int mask = (1 << xBits) - 1;
                uint code = 0xff000000;
                for (x = 0; x < width; x++)
                {
                    int xsub = x & mask;
                    if (xsub == 0)
                    {
                        code = 0xff000000;
                    }

                    code |= (uint)(row[x] << (8 + (bitDepth * xsub)));
                    dst[x >> xBits] = code;
                }
            }
            else
            {
                for (x = 0; x < width; x++)
                {
                    dst[x] = (uint)(0xff000000 | (row[x] << 8));
                }
            }
        }

        /// <summary>
        /// Compute the combined Shanon's entropy for distribution {X} and {X+Y}.
        /// </summary>
        /// <returns>Shanon entropy.</returns>
        public static float CombinedShannonEntropy(int[] x, int[] y)
        {
            double retVal = 0.0d;
            uint sumX = 0, sumXY = 0;
            for (int i = 0; i < 256; i++)
            {
                uint xi = (uint)x[i];
                if (xi != 0)
                {
                    uint xy = xi + (uint)y[i];
                    sumX += xi;
                    retVal -= FastSLog2(xi);
                    sumXY += xy;
                    retVal -= FastSLog2(xy);
                }
                else if (y[i] != 0)
                {
                    sumXY += (uint)y[i];
                    retVal -= FastSLog2((uint)y[i]);
                }
            }

            retVal += FastSLog2(sumX) + FastSLog2(sumXY);
            return (float)retVal;
        }

        public static byte TransformColorRed(sbyte greenToRed, uint argb)
        {
            sbyte green = U32ToS8(argb >> 8);
            int newRed = (int)(argb >> 16);
            newRed -= ColorTransformDelta(greenToRed, green);
            return (byte)(newRed & 0xff);
        }

        public static byte TransformColorBlue(sbyte greenToBlue, sbyte redToBlue, uint argb)
        {
            sbyte green = U32ToS8(argb >> 8);
            sbyte red = U32ToS8(argb >> 16);
            int newBlue = (int)(argb & 0xff);
            newBlue -= ColorTransformDelta(greenToBlue, green);
            newBlue -= ColorTransformDelta(redToBlue, red);
            return (byte)(newBlue & 0xff);
        }

        /// <summary>
        /// Fast calculation of log2(v) for integer input.
        /// </summary>
        public static float FastLog2(uint v)
        {
            return (v < LogLookupIdxMax) ? WebpLookupTables.Log2Table[v] : FastLog2Slow(v);
        }

        /// <summary>
        /// Fast calculation of v * log2(v) for integer input.
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static float FastSLog2(uint v)
        {
            return (v < LogLookupIdxMax) ? WebpLookupTables.SLog2Table[v] : FastSLog2Slow(v);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ColorCodeToMultipliers(uint colorCode, ref Vp8LMultipliers m)
        {
            m.GreenToRed = (byte)(colorCode & 0xff);
            m.GreenToBlue = (byte)((colorCode >> 8) & 0xff);
            m.RedToBlue = (byte)((colorCode >> 16) & 0xff);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static int NearLosslessBits(int nearLosslessQuality)
        {
            // 100 -> 0
            // 80..99 -> 1
            // 60..79 -> 2
            // 40..59 -> 3
            // 20..39 -> 4
            //  0..19 -> 5
            return 5 - (nearLosslessQuality / 20);
        }

        private static float FastSLog2Slow(uint v)
        {
            Guard.MustBeGreaterThanOrEqualTo(v, LogLookupIdxMax, nameof(v));
            if (v < ApproxLogWithCorrectionMax)
            {
                int logCnt = 0;
                uint y = 1;
                float vF = v;
                uint origV = v;
                do
                {
                    ++logCnt;
                    v >>= 1;
                    y <<= 1;
                }
                while (v >= LogLookupIdxMax);

                // vf = (2^log_cnt) * Xf; where y = 2^log_cnt and Xf < 256
                // Xf = floor(Xf) * (1 + (v % y) / v)
                // log2(Xf) = log2(floor(Xf)) + log2(1 + (v % y) / v)
                // The correction factor: log(1 + d) ~ d; for very small d values, so
                // log2(1 + (v % y) / v) ~ LOG_2_RECIPROCAL * (v % y)/v
                // LOG_2_RECIPROCAL ~ 23/16
                var correction = (int)((23 * (origV & (y - 1))) >> 4);
                return (vF * (WebpLookupTables.Log2Table[v] + logCnt)) + correction;
            }
            else
            {
                return (float)(Log2Reciprocal * v * Math.Log(v));
            }
        }

        private static float FastLog2Slow(uint v)
        {
            Guard.MustBeGreaterThanOrEqualTo(v, LogLookupIdxMax, nameof(v));
            if (v < ApproxLogWithCorrectionMax)
            {
                int logCnt = 0;
                uint y = 1;
                uint origV = v;
                do
                {
                    ++logCnt;
                    v = v >> 1;
                    y = y << 1;
                }
                while (v >= LogLookupIdxMax);

                double log2 = WebpLookupTables.Log2Table[v] + logCnt;
                if (origV >= ApproxLogMax)
                {
                    // Since the division is still expensive, add this correction factor only
                    // for large values of 'v'.
                    int correction = (int)(23 * (origV & (y - 1))) >> 4;
                    log2 += (double)correction / origV;
                }

                return (float)log2;
            }
            else
            {
                return (float)(Log2Reciprocal * Math.Log(v));
            }
        }

        /// <summary>
        /// Splitting of distance and length codes into prefixes and
        /// extra bits. The prefixes are encoded with an entropy code
        /// while the extra bits are stored just as normal bits.
        /// </summary>
        private static int PrefixEncodeBitsNoLut(int distance, ref int extraBits)
        {
            int highestBit = WebpCommonUtils.BitsLog2Floor((uint)--distance);
            int secondHighestBit = (distance >> (highestBit - 1)) & 1;
            extraBits = highestBit - 1;
            var code = (2 * highestBit) + secondHighestBit;
            return code;
        }

        private static int PrefixEncodeNoLUT(int distance, ref int extraBits, ref int extraBitsValue)
        {
            int highestBit = WebpCommonUtils.BitsLog2Floor((uint)--distance);
            int secondHighestBit = (distance >> (highestBit - 1)) & 1;
            extraBits = highestBit - 1;
            extraBitsValue = distance & ((1 << extraBits) - 1);
            int code = (2 * highestBit) + secondHighestBit;
            return code;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void PredictorAdd0(uint* input, int numberOfPixels, uint* output)
        {
            for (int x = 0; x < numberOfPixels; ++x)
            {
                output[x] = AddPixels(input[x], WebpConstants.ArgbBlack);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void PredictorAdd1(uint* input, int numberOfPixels, uint* output)
        {
            uint left = output[-1];
            for (int x = 0; x < numberOfPixels; ++x)
            {
                output[x] = left = AddPixels(input[x], left);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void PredictorAdd2(uint* input, uint* upper, int numberOfPixels, uint* output)
        {
            for (int x = 0; x < numberOfPixels; ++x)
            {
                uint pred = Predictor2(output[x - 1], upper + x);
                output[x] = AddPixels(input[x], pred);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void PredictorAdd3(uint* input, uint* upper, int numberOfPixels, uint* output)
        {
            for (int x = 0; x < numberOfPixels; ++x)
            {
                uint pred = Predictor3(output[x - 1], upper + x);
                output[x] = AddPixels(input[x], pred);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void PredictorAdd4(uint* input, uint* upper, int numberOfPixels, uint* output)
        {
            for (int x = 0; x < numberOfPixels; ++x)
            {
                uint pred = Predictor4(output[x - 1], upper + x);
                output[x] = AddPixels(input[x], pred);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void PredictorAdd5(uint* input, uint* upper, int numberOfPixels, uint* output)
        {
            for (int x = 0; x < numberOfPixels; ++x)
            {
                uint pred = Predictor5(output[x - 1], upper + x);
                output[x] = AddPixels(input[x], pred);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void PredictorAdd6(uint* input, uint* upper, int numberOfPixels, uint* output)
        {
            for (int x = 0; x < numberOfPixels; ++x)
            {
                uint pred = Predictor6(output[x - 1], upper + x);
                output[x] = AddPixels(input[x], pred);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void PredictorAdd7(uint* input, uint* upper, int numberOfPixels, uint* output)
        {
            for (int x = 0; x < numberOfPixels; ++x)
            {
                uint pred = Predictor7(output[x - 1], upper + x);
                output[x] = AddPixels(input[x], pred);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void PredictorAdd8(uint* input, uint* upper, int numberOfPixels, uint* output)
        {
            for (int x = 0; x < numberOfPixels; ++x)
            {
                uint pred = Predictor8(output[x - 1], upper + x);
                output[x] = AddPixels(input[x], pred);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void PredictorAdd9(uint* input, uint* upper, int numberOfPixels, uint* output)
        {
            for (int x = 0; x < numberOfPixels; ++x)
            {
                uint pred = Predictor9(output[x - 1], upper + x);
                output[x] = AddPixels(input[x], pred);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void PredictorAdd10(uint* input, uint* upper, int numberOfPixels, uint* output)
        {
            for (int x = 0; x < numberOfPixels; ++x)
            {
                uint pred = Predictor10(output[x - 1], upper + x);
                output[x] = AddPixels(input[x], pred);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void PredictorAdd11(uint* input, uint* upper, int numberOfPixels, uint* output)
        {
            for (int x = 0; x < numberOfPixels; ++x)
            {
                uint pred = Predictor11(output[x - 1], upper + x);
                output[x] = AddPixels(input[x], pred);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void PredictorAdd12(uint* input, uint* upper, int numberOfPixels, uint* output)
        {
            for (int x = 0; x < numberOfPixels; ++x)
            {
                uint pred = Predictor12(output[x - 1], upper + x);
                output[x] = AddPixels(input[x], pred);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void PredictorAdd13(uint* input, uint* upper, int numberOfPixels, uint* output)
        {
            for (int x = 0; x < numberOfPixels; ++x)
            {
                uint pred = Predictor13(output[x - 1], upper + x);
                output[x] = AddPixels(input[x], pred);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static uint Predictor2(uint left, uint* top)
        {
            return top[0];
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static uint Predictor3(uint left, uint* top)
        {
            return top[1];
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static uint Predictor4(uint left, uint* top)
        {
            return top[-1];
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static uint Predictor5(uint left, uint* top)
        {
            return Average3(left, top[0], top[1]);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static uint Predictor6(uint left, uint* top)
        {
            return Average2(left, top[-1]);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static uint Predictor7(uint left, uint* top)
        {
            return Average2(left, top[0]);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static uint Predictor8(uint left, uint* top)
        {
            return Average2(top[-1], top[0]);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static uint Predictor9(uint left, uint* top)
        {
            return Average2(top[0], top[1]);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static uint Predictor10(uint left, uint* top)
        {
            return Average4(left, top[-1], top[0], top[1]);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static uint Predictor11(uint left, uint* top)
        {
            return Select(top[0], left, top[-1]);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static uint Predictor12(uint left, uint* top)
        {
            return ClampedAddSubtractFull(left, top[0], top[-1]);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static uint Predictor13(uint left, uint* top)
        {
            return ClampedAddSubtractHalf(left, top[0], top[-1]);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void PredictorSub0(uint* input, int numPixels, uint* output)
        {
            for (int i = 0; i < numPixels; ++i)
            {
                output[i] = SubPixels(input[i], WebpConstants.ArgbBlack);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void PredictorSub1(uint* input, int numPixels, uint* output)
        {
            for (int i = 0; i < numPixels; ++i)
            {
                output[i] = SubPixels(input[i], input[i - 1]);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void PredictorSub2(uint* input, uint* upper, int numPixels, uint* output)
        {
            for (int x = 0; x < numPixels; ++x)
            {
                uint pred = Predictor2(input[x - 1], upper + x);
                output[x] = SubPixels(input[x], pred);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void PredictorSub3(uint* input, uint* upper, int numPixels, uint* output)
        {
            for (int x = 0; x < numPixels; ++x)
            {
                uint pred = Predictor3(input[x - 1], upper + x);
                output[x] = SubPixels(input[x], pred);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void PredictorSub4(uint* input, uint* upper, int numPixels, uint* output)
        {
            for (int x = 0; x < numPixels; ++x)
            {
                uint pred = Predictor4(input[x - 1], upper + x);
                output[x] = SubPixels(input[x], pred);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void PredictorSub5(uint* input, uint* upper, int numPixels, uint* output)
        {
            for (int x = 0; x < numPixels; ++x)
            {
                uint pred = Predictor5(input[x - 1], upper + x);
                output[x] = SubPixels(input[x], pred);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void PredictorSub6(uint* input, uint* upper, int numPixels, uint* output)
        {
            for (int x = 0; x < numPixels; ++x)
            {
                uint pred = Predictor6(input[x - 1], upper + x);
                output[x] = SubPixels(input[x], pred);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void PredictorSub7(uint* input, uint* upper, int numPixels, uint* output)
        {
            for (int x = 0; x < numPixels; ++x)
            {
                uint pred = Predictor7(input[x - 1], upper + x);
                output[x] = SubPixels(input[x], pred);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void PredictorSub8(uint* input, uint* upper, int numPixels, uint* output)
        {
            for (int x = 0; x < numPixels; ++x)
            {
                uint pred = Predictor8(input[x - 1], upper + x);
                output[x] = SubPixels(input[x], pred);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void PredictorSub9(uint* input, uint* upper, int numPixels, uint* output)
        {
            for (int x = 0; x < numPixels; ++x)
            {
                uint pred = Predictor9(input[x - 1], upper + x);
                output[x] = SubPixels(input[x], pred);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void PredictorSub10(uint* input, uint* upper, int numPixels, uint* output)
        {
            for (int x = 0; x < numPixels; ++x)
            {
                uint pred = Predictor10(input[x - 1], upper + x);
                output[x] = SubPixels(input[x], pred);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void PredictorSub11(uint* input, uint* upper, int numPixels, uint* output)
        {
            for (int x = 0; x < numPixels; ++x)
            {
                uint pred = Predictor11(input[x - 1], upper + x);
                output[x] = SubPixels(input[x], pred);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void PredictorSub12(uint* input, uint* upper, int numPixels, uint* output)
        {
            for (int x = 0; x < numPixels; ++x)
            {
                uint pred = Predictor12(input[x - 1], upper + x);
                output[x] = SubPixels(input[x], pred);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void PredictorSub13(uint* input, uint* upper, int numPixels, uint* output)
        {
            for (int x = 0; x < numPixels; ++x)
            {
                uint pred = Predictor13(input[x - 1], upper + x);
                output[x] = SubPixels(input[x], pred);
            }
        }

        /// <summary>
        /// Computes sampled size of 'size' when sampling using 'sampling bits'.
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static int SubSampleSize(int size, int samplingBits)
        {
            return (size + (1 << samplingBits) - 1) >> samplingBits;
        }

        /// <summary>
        /// Sum of each component, mod 256.
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static uint AddPixels(uint a, uint b)
        {
            uint alphaAndGreen = (a & 0xff00ff00u) + (b & 0xff00ff00u);
            uint redAndBlue = (a & 0x00ff00ffu) + (b & 0x00ff00ffu);
            return (alphaAndGreen & 0xff00ff00u) | (redAndBlue & 0x00ff00ffu);
        }

        // For sign-extended multiplying constants, pre-shifted by 5:
        [MethodImpl(InliningOptions.ShortMethod)]
        public static short Cst5b(int x)
        {
            return (short)(((short)(x << 8)) >> 5);
        }

        private static uint ClampedAddSubtractFull(uint c0, uint c1, uint c2)
        {
            int a = AddSubtractComponentFull(
                (int)(c0 >> 24),
                (int)(c1 >> 24),
                (int)(c2 >> 24));
            int r = AddSubtractComponentFull(
                (int)((c0 >> 16) & 0xff),
                (int)((c1 >> 16) & 0xff),
                (int)((c2 >> 16) & 0xff));
            int g = AddSubtractComponentFull(
                (int)((c0 >> 8) & 0xff),
                (int)((c1 >> 8) & 0xff),
                (int)((c2 >> 8) & 0xff));
            int b = AddSubtractComponentFull((int)(c0 & 0xff), (int)(c1 & 0xff), (int)(c2 & 0xff));
            return ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
        }

        private static uint ClampedAddSubtractHalf(uint c0, uint c1, uint c2)
        {
            uint ave = Average2(c0, c1);
            int a = AddSubtractComponentHalf((int)(ave >> 24), (int)(c2 >> 24));
            int r = AddSubtractComponentHalf((int)((ave >> 16) & 0xff), (int)((c2 >> 16) & 0xff));
            int g = AddSubtractComponentHalf((int)((ave >> 8) & 0xff), (int)((c2 >> 8) & 0xff));
            int b = AddSubtractComponentHalf((int)(ave & 0xff), (int)(c2 & 0xff));
            return ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int AddSubtractComponentHalf(int a, int b)
        {
            return (int)Clip255((uint)(a + ((a - b) / 2)));
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int AddSubtractComponentFull(int a, int b, int c)
        {
            return (int)Clip255((uint)(a + b - c));
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static uint Clip255(uint a)
        {
            return a < 256 ? a : ~a >> 24;
        }

#if SUPPORTS_RUNTIME_INTRINSICS
        [MethodImpl(InliningOptions.ShortMethod)]
        private static Vector128<int> MkCst16(int hi, int lo)
        {
            return Vector128.Create((hi << 16) | (lo & 0xffff));
        }
#endif

        private static uint Select(uint a, uint b, uint c)
        {
            int paMinusPb =
                Sub3((int)(a >> 24), (int)(b >> 24), (int)(c >> 24)) +
                Sub3((int)((a >> 16) & 0xff), (int)((b >> 16) & 0xff), (int)((c >> 16) & 0xff)) +
                Sub3((int)((a >> 8) & 0xff), (int)((b >> 8) & 0xff), (int)((c >> 8) & 0xff)) +
                Sub3((int)(a & 0xff), (int)(b & 0xff), (int)(c & 0xff));
            return (paMinusPb <= 0) ? a : b;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int Sub3(int a, int b, int c)
        {
            int pb = b - c;
            int pa = a - c;
            return Math.Abs(pb) - Math.Abs(pa);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static uint Average2(uint a0, uint a1)
        {
            return (((a0 ^ a1) & 0xfefefefeu) >> 1) + (a0 & a1);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static uint Average3(uint a0, uint a1, uint a2)
        {
            return Average2(Average2(a0, a2), a1);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static uint Average4(uint a0, uint a1, uint a2, uint a3)
        {
            return Average2(Average2(a0, a1), Average2(a2, a3));
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static uint GetArgbIndex(uint idx)
        {
            return (idx >> 8) & 0xff;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int ColorTransformDelta(sbyte colorPred, sbyte color)
        {
            return (colorPred * color) >> 5;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static sbyte U32ToS8(uint v)
        {
            return (sbyte)(v & 0xff);
        }
    }
}
