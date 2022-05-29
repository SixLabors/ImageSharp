// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression
{
    internal static class BitWriterUtils
    {
        public static void WriteBits(Span<byte> buffer, int pos, uint count, byte value)
        {
            int bitPos = pos % 8;
            int bufferPos = pos / 8;
            int startIdx = bufferPos + bitPos;
            int endIdx = (int)(startIdx + count);

            if (value == 1)
            {
                for (int i = startIdx; i < endIdx; i++)
                {
                    WriteBit(buffer, bufferPos, bitPos);

                    bitPos++;
                    if (bitPos >= 8)
                    {
                        bitPos = 0;
                        bufferPos++;
                    }
                }
            }
            else
            {
                for (int i = startIdx; i < endIdx; i++)
                {
                    WriteZeroBit(buffer, bufferPos, bitPos);

                    bitPos++;
                    if (bitPos >= 8)
                    {
                        bitPos = 0;
                        bufferPos++;
                    }
                }
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void WriteBit(Span<byte> buffer, int bufferPos, int bitPos) => buffer[bufferPos] |= (byte)(1 << (7 - bitPos));

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void WriteZeroBit(Span<byte> buffer, int bufferPos, int bitPos) => buffer[bufferPos] = (byte)(buffer[bufferPos] & ~(1 << (7 - bitPos)));
    }
}
