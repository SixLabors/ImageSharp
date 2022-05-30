// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression
{
    internal static class BitWriterUtils
    {
        public static void WriteBits(Span<byte> buffer, int pos, int count, byte value)
        {
            int bitPos = Numerics.Modulo8(pos);
            int bufferPos = pos / 8;
            int startIdx = bufferPos + bitPos;
            int endIdx = startIdx + count;

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
        public static void WriteBit(Span<byte> buffer, int bufferPos, int bitPos)
        {
            ref byte b = ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), bufferPos);
            b |= (byte)(1 << (7 - bitPos));
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void WriteZeroBit(Span<byte> buffer, int bufferPos, int bitPos)
        {
            ref byte b = ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), bufferPos);
            b = (byte)(b & ~(1 << (7 - bitPos)));
        }
    }
}
