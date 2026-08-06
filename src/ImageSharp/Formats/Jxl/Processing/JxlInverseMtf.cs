// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Inverse Move to Front implementation
/// </summary>
internal static class JxlInverseMtf
{
    // NOTE: here we use Vector512 to store 64 bytes in a
    // more efficient manner. However, it doesn't necessarily
    // require 512-bit CPU vector support.
    // If the user's CPU has 256-bit vectors, the JIT will emit
    // such instructions for each half. Likewise, if the user's
    // CPU only goes up to 128-bit vectors, the JIT will emit
    // 128-bit vector code for each quarter. And if the CPU
    // doesn't support SIMD at all, the JIT will emit scalar
    // instructions.
    public static void MoveToFront(Span<byte> v, byte index)
    {
        byte value = v[index];
        byte i = index;

        ref byte vR = ref MemoryMarshal.GetReference(v);

        if (i < 4)
        {
            for (; i != 0; --i)
            {
                v[i] = v[i - 1];
            }
        }
        else
        {
            int tail = i & 63;

            if (tail != 0)
            {
                i -= (byte)tail;
                Vector512<byte> vec = Vector512.LoadUnsafe(ref Unsafe.Add(ref vR, i));
                Vector512<byte> prev = Vector512.LoadUnsafe(ref Unsafe.Add(ref vR, i + 1));

                // TODO: optimize this?
                Span<byte> maskBytes = stackalloc byte[64];

                for (int j = 0; j < 64; j++)
                {
                    maskBytes[j] = (byte)(j < tail ? 0xFF : 0);
                }

                Vector512<byte> mask = Vector512.Create<byte>(maskBytes);
                Vector512<byte> filter = Vector512.ConditionalSelect(mask, vec, prev);
                filter.StoreUnsafe(ref Unsafe.Add(ref vR, i + 1));
            }

            while (i != 0)
            {
                i -= 64;
                Vector512<byte> vec = Vector512.LoadUnsafe(ref Unsafe.Add(ref vR, i));
                vec.StoreUnsafe(ref Unsafe.Add(ref vR, i + 1));
            }
        }

        v[0] = value;
    }

    public static void InverseMoveToFrontTransform(Span<byte> v, int vLength)
    {
        Span<byte> mtf = stackalloc byte[256 + 64];
        for (int i = 0; i < 256; i++)
        {
            mtf[i] = (byte)i;
        }

        for (int i = 0; i < vLength; i++)
        {
            byte index = v[i];
            v[i] = mtf[index];

            if (index != 0)
            {
                MoveToFront(mtf, index);
            }
        }
    }
}
