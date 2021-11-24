// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    [Trait("Format", "Jpg")]
    public class ZigZagTests
    {
        private static void CanHandleAllPossibleCoefficients(ReadOnlySpan<byte> order)
        {
            // Mimic the behaviour of the huffman scan decoder using all possible byte values
            short[] block = new short[64];

            for (int h = 0; h < 255; h++)
            {
                for (int i = 1; i < 64; i++)
                {
                    int s = h;
                    int r = s >> 4;
                    s &= 15;

                    if (s != 0)
                    {
                        i += r;
                        block[order[i++]] = (short)s;
                    }
                    else
                    {
                        if (r == 0)
                        {
                            break;
                        }

                        i += 16;
                    }
                }
            }
        }

        [Fact]
        public static void ZigZagCanHandleAllPossibleCoefficients() =>
            CanHandleAllPossibleCoefficients(ZigZag.ZigZagOrder);

        [Fact]
        public static void TrasposingZigZagCanHandleAllPossibleCoefficients() =>
            CanHandleAllPossibleCoefficients(ZigZag.TransposingOrder);
    }
}
