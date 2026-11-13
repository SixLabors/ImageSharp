// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression;

internal static class BitWriterUtils
{
    /// <summary>
    /// Checks that a decoded run or scanline fits in the output buffer before writing bits.
    /// </summary>
    /// <param name="bufferLength">The output buffer length in bytes.</param>
    /// <param name="position">The first bit to write.</param>
    /// <param name="count">The number of bits to write.</param>
    public static void ValidateWriteRange(int bufferLength, nint position, nint count)
    {
        // A span may hold more bits than a 32-bit integer can represent.
        long capacity = (long)bufferLength * 8;
        if (position < 0 || count < 0 || (long)count > capacity - (long)position)
        {
            TiffThrowHelper.ThrowImageFormatException("The TIFF fax data exceeds the output buffer.");
        }
    }

    public static void WriteBits(Span<byte> buffer, nint pos, nint count, byte value)
    {
        ValidateWriteRange(buffer.Length, pos, count);

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
