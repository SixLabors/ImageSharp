// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Jpeg.Components;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    [Trait("Format", "Jpg")]
    public class ZigZagTests
    {
        [Fact]
        public void ZigZagCanHandleAllPossibleCoefficients()
        {
            // Mimic the behaviour of the huffman scan decoder using all possible byte values
            var block = new short[64];
            var zigzag = ZigZag.CreateUnzigTable();

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
                        block[zigzag[i++]] = (short)s;
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
    }
}
