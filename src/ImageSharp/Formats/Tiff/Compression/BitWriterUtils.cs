// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression
{
    internal static class BitWriterUtils
    {
        public static void WriteBits(Span<byte> buffer, nint pos, nint count, byte value)
        {
            nint bitPos = Numerics.Modulo8(pos);
            nint bufferPos = pos / 8;
            nint startIdx = bufferPos + bitPos;
            nint endIdx = startIdx + count;

            if (value == 1)
            {
                for (nint i = startIdx; i < endIdx; i++)
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
                for (nint i = startIdx; i < endIdx; i++)
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
        public static void WriteBit(Span<byte> buffer, nint bufferPos, nint bitPos)
        {
            ref byte b = ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), bufferPos);
            b |= (byte)(1 << (int)(7 - bitPos));
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void WriteZeroBit(Span<byte> buffer, nint bufferPos, nint bitPos)
        {
            ref byte b = ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), bufferPos);
            b = (byte)(b & ~(1 << (int)(7 - bitPos)));
        }
    }
}
