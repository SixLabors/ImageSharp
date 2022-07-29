// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless
{
    /// <summary>
    /// Near-lossless image preprocessing adjusts pixel values to help compressibility with a guarantee
    /// of maximum deviation between original and resulting pixel values.
    /// </summary>
    internal static class NearLosslessEnc
    {
        private const int MinDimForNearLossless = 64;

        public static void ApplyNearLossless(int xSize, int ySize, int quality, Span<uint> argbSrc, Span<uint> argbDst, int stride)
        {
            uint[] copyBuffer = new uint[xSize * 3];
            int limitBits = LosslessUtils.NearLosslessBits(quality);

            // For small icon images, don't attempt to apply near-lossless compression.
            if ((xSize < MinDimForNearLossless && ySize < MinDimForNearLossless) || ySize < 3)
            {
                for (int i = 0; i < ySize; i++)
                {
                    argbSrc.Slice(i * stride, xSize).CopyTo(argbDst.Slice(i * xSize, xSize));
                }

                return;
            }

            NearLossless(xSize, ySize, argbSrc, stride, limitBits, copyBuffer, argbDst);
            for (int i = limitBits - 1; i != 0; i--)
            {
                NearLossless(xSize, ySize, argbDst, xSize, i, copyBuffer, argbDst);
            }
        }

        // Adjusts pixel values of image with given maximum error.
        private static void NearLossless(int xSize, int ySize, Span<uint> argbSrc, int stride, int limitBits, Span<uint> copyBuffer, Span<uint> argbDst)
        {
            int y;
            int limit = 1 << limitBits;
            Span<uint> prevRow = copyBuffer;
            Span<uint> currRow = copyBuffer.Slice(xSize, xSize);
            Span<uint> nextRow = copyBuffer.Slice(xSize * 2, xSize);
            argbSrc.Slice(0, xSize).CopyTo(currRow);
            argbSrc.Slice(xSize, xSize).CopyTo(nextRow);

            int srcOffset = 0;
            int dstOffset = 0;
            for (y = 0; y < ySize; y++)
            {
                if (y == 0 || y == ySize - 1)
                {
                    argbSrc.Slice(srcOffset, xSize).CopyTo(argbDst.Slice(dstOffset, xSize));
                }
                else
                {
                    argbSrc.Slice(srcOffset + stride, xSize).CopyTo(nextRow);
                    argbDst[dstOffset] = argbSrc[srcOffset];
                    argbDst[dstOffset + xSize - 1] = argbSrc[srcOffset + xSize - 1];
                    for (int x = 1; x < xSize - 1; x++)
                    {
                        if (IsSmooth(prevRow, currRow, nextRow, x, limit))
                        {
                            argbDst[dstOffset + x] = currRow[x];
                        }
                        else
                        {
                            argbDst[dstOffset + x] = ClosestDiscretizedArgb(currRow[x], limitBits);
                        }
                    }
                }

                Span<uint> temp = prevRow;
                prevRow = currRow;
                currRow = nextRow;
                nextRow = temp;
                srcOffset += stride;
                dstOffset += xSize;
            }
        }

        // Applies FindClosestDiscretized to all channels of pixel.
        private static uint ClosestDiscretizedArgb(uint a, int bits) =>
            (FindClosestDiscretized(a >> 24, bits) << 24) |
            (FindClosestDiscretized((a >> 16) & 0xff, bits) << 16) |
            (FindClosestDiscretized((a >> 8) & 0xff, bits) << 8) |
            FindClosestDiscretized(a & 0xff, bits);

        private static uint FindClosestDiscretized(uint a, int bits)
        {
            uint mask = (1u << bits) - 1;
            uint biased = a + (mask >> 1) + ((a >> bits) & 1);
            if (biased > 0xff)
            {
                return 0xff;
            }

            return biased & ~mask;
        }

        private static bool IsSmooth(Span<uint> prevRow, Span<uint> currRow, Span<uint> nextRow, int ix, int limit) =>
            IsNear(currRow[ix], currRow[ix - 1], limit) && // Check that all pixels in 4-connected neighborhood are smooth.
                    IsNear(currRow[ix], currRow[ix + 1], limit) &&
                    IsNear(currRow[ix], prevRow[ix], limit) &&
                    IsNear(currRow[ix], nextRow[ix], limit);

        // Checks if distance between corresponding channel values of pixels a and b is within the given limit.
        private static bool IsNear(uint a, uint b, int limit)
        {
            for (int k = 0; k < 4; ++k)
            {
                int delta = (int)((a >> (k * 8)) & 0xff) - (int)((b >> (k * 8)) & 0xff);
                if (delta >= limit || delta <= -limit)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
