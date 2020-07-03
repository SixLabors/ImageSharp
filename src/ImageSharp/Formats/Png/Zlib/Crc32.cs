// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace SixLabors.ImageSharp.Formats.Png.Zlib
{
    /// <summary>
    /// Calculates the 32 bit Cyclic Redundancy Check (CRC) checksum of a given buffer
    /// according to the IEEE 802.3 specification.
    /// </summary>
    internal static partial class Crc32
    {
        /// <summary>
        /// The default initial seed value of a Crc32 checksum calculation.
        /// </summary>
        public const uint SeedValue = 0U;

#if SUPPORTS_RUNTIME_INTRINSICS
        private const int MinBufferSize = 64;
        private const int ChunksizeMask = 15;

        // Definitions of the bit-reflected domain constants k1, k2, k3, etc and
        // the CRC32+Barrett polynomials given at the end of the paper.
        private static readonly ulong[] K05Poly =
        {
            0x0154442bd4, 0x01c6e41596, // k1, k2
            0x01751997d0, 0x00ccaa009e, // k3, k4
            0x0163cd6124, 0x0000000000, // k5, k0
            0x01db710641, 0x01f7011641 // polynomial
        };
#endif

        /// <summary>
        /// Calculates the CRC checksum with the bytes taken from the span.
        /// </summary>
        /// <param name="buffer">The readonly span of bytes.</param>
        /// <returns>The <see cref="uint"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static uint Calculate(ReadOnlySpan<byte> buffer)
            => Calculate(SeedValue, buffer);

        /// <summary>
        /// Calculates the CRC checksum with the bytes taken from the span and seed.
        /// </summary>
        /// <param name="crc">The input CRC value.</param>
        /// <param name="buffer">The readonly span of bytes.</param>
        /// <returns>The <see cref="uint"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static uint Calculate(uint crc, ReadOnlySpan<byte> buffer)
        {
            if (buffer.IsEmpty)
            {
                return crc;
            }

#if SUPPORTS_RUNTIME_INTRINSICS
            if (Sse41.IsSupported && Pclmulqdq.IsSupported && buffer.Length >= MinBufferSize)
            {
                return ~CalculateSse(~crc, buffer);
            }
            else
            {
                return ~CalculateScalar(~crc, buffer);
            }
#else
            return ~CalculateScalar(~crc, buffer);
#endif
        }

#if SUPPORTS_RUNTIME_INTRINSICS
        // Based on https://github.com/chromium/chromium/blob/master/third_party/zlib/crc32_simd.c
        [MethodImpl(InliningOptions.HotPath | InliningOptions.ShortMethod)]
        private static unsafe uint CalculateSse(uint crc, ReadOnlySpan<byte> buffer)
        {
            int chunksize = buffer.Length & ~ChunksizeMask;
            int length = chunksize;

            fixed (byte* bufferPtr = buffer)
            fixed (ulong* k05PolyPtr = K05Poly)
            {
                byte* localBufferPtr = bufferPtr;
                ulong* localK05PolyPtr = k05PolyPtr;

                // There's at least one block of 64.
                Vector128<ulong> x1 = Sse2.LoadVector128((ulong*)(localBufferPtr + 0x00));
                Vector128<ulong> x2 = Sse2.LoadVector128((ulong*)(localBufferPtr + 0x10));
                Vector128<ulong> x3 = Sse2.LoadVector128((ulong*)(localBufferPtr + 0x20));
                Vector128<ulong> x4 = Sse2.LoadVector128((ulong*)(localBufferPtr + 0x30));
                Vector128<ulong> x5;

                x1 = Sse2.Xor(x1, Sse2.ConvertScalarToVector128UInt32(crc).AsUInt64());

                // k1, k2
                Vector128<ulong> x0 = Sse2.LoadVector128(localK05PolyPtr + 0x0);

                localBufferPtr += 64;
                length -= 64;

                // Parallel fold blocks of 64, if any.
                while (length >= 64)
                {
                    x5 = Pclmulqdq.CarrylessMultiply(x1, x0, 0x00);
                    Vector128<ulong> x6 = Pclmulqdq.CarrylessMultiply(x2, x0, 0x00);
                    Vector128<ulong> x7 = Pclmulqdq.CarrylessMultiply(x3, x0, 0x00);
                    Vector128<ulong> x8 = Pclmulqdq.CarrylessMultiply(x4, x0, 0x00);

                    x1 = Pclmulqdq.CarrylessMultiply(x1, x0, 0x11);
                    x2 = Pclmulqdq.CarrylessMultiply(x2, x0, 0x11);
                    x3 = Pclmulqdq.CarrylessMultiply(x3, x0, 0x11);
                    x4 = Pclmulqdq.CarrylessMultiply(x4, x0, 0x11);

                    Vector128<ulong> y5 = Sse2.LoadVector128((ulong*)(localBufferPtr + 0x00));
                    Vector128<ulong> y6 = Sse2.LoadVector128((ulong*)(localBufferPtr + 0x10));
                    Vector128<ulong> y7 = Sse2.LoadVector128((ulong*)(localBufferPtr + 0x20));
                    Vector128<ulong> y8 = Sse2.LoadVector128((ulong*)(localBufferPtr + 0x30));

                    x1 = Sse2.Xor(x1, x5);
                    x2 = Sse2.Xor(x2, x6);
                    x3 = Sse2.Xor(x3, x7);
                    x4 = Sse2.Xor(x4, x8);

                    x1 = Sse2.Xor(x1, y5);
                    x2 = Sse2.Xor(x2, y6);
                    x3 = Sse2.Xor(x3, y7);
                    x4 = Sse2.Xor(x4, y8);

                    localBufferPtr += 64;
                    length -= 64;
                }

                // Fold into 128-bits.
                // k3, k4
                x0 = Sse2.LoadVector128(k05PolyPtr + 0x2);

                x5 = Pclmulqdq.CarrylessMultiply(x1, x0, 0x00);
                x1 = Pclmulqdq.CarrylessMultiply(x1, x0, 0x11);
                x1 = Sse2.Xor(x1, x2);
                x1 = Sse2.Xor(x1, x5);

                x5 = Pclmulqdq.CarrylessMultiply(x1, x0, 0x00);
                x1 = Pclmulqdq.CarrylessMultiply(x1, x0, 0x11);
                x1 = Sse2.Xor(x1, x3);
                x1 = Sse2.Xor(x1, x5);

                x5 = Pclmulqdq.CarrylessMultiply(x1, x0, 0x00);
                x1 = Pclmulqdq.CarrylessMultiply(x1, x0, 0x11);
                x1 = Sse2.Xor(x1, x4);
                x1 = Sse2.Xor(x1, x5);

                // Single fold blocks of 16, if any.
                while (length >= 16)
                {
                    x2 = Sse2.LoadVector128((ulong*)localBufferPtr);

                    x5 = Pclmulqdq.CarrylessMultiply(x1, x0, 0x00);
                    x1 = Pclmulqdq.CarrylessMultiply(x1, x0, 0x11);
                    x1 = Sse2.Xor(x1, x2);
                    x1 = Sse2.Xor(x1, x5);

                    localBufferPtr += 16;
                    length -= 16;
                }

                // Fold 128 - bits to 64 - bits.
                x2 = Pclmulqdq.CarrylessMultiply(x1, x0, 0x10);
                x3 = Vector128.Create(~0, 0, ~0, 0).AsUInt64(); // _mm_setr_epi32 on x86
                x1 = Sse2.ShiftRightLogical128BitLane(x1, 8);
                x1 = Sse2.Xor(x1, x2);

                // k5, k0
                x0 = Sse2.LoadScalarVector128(localK05PolyPtr + 0x4);

                x2 = Sse2.ShiftRightLogical128BitLane(x1, 4);
                x1 = Sse2.And(x1, x3);
                x1 = Pclmulqdq.CarrylessMultiply(x1, x0, 0x00);
                x1 = Sse2.Xor(x1, x2);

                // Barret reduce to 32-bits.
                // polynomial
                x0 = Sse2.LoadVector128(localK05PolyPtr + 0x6);

                x2 = Sse2.And(x1, x3);
                x2 = Pclmulqdq.CarrylessMultiply(x2, x0, 0x10);
                x2 = Sse2.And(x2, x3);
                x2 = Pclmulqdq.CarrylessMultiply(x2, x0, 0x00);
                x1 = Sse2.Xor(x1, x2);

                crc = (uint)Sse41.Extract(x1.AsInt32(), 1);
                return buffer.Length - chunksize == 0 ? crc : CalculateScalar(crc, buffer.Slice(chunksize));
            }
        }
#endif

        [MethodImpl(InliningOptions.HotPath | InliningOptions.ShortMethod)]
        private static uint CalculateScalar(uint crc, ReadOnlySpan<byte> buffer)
        {
            ref uint crcTableRef = ref MemoryMarshal.GetReference(CrcTable.AsSpan());
            ref byte bufferRef = ref MemoryMarshal.GetReference(buffer);

            for (int i = 0; i < buffer.Length; i++)
            {
                crc = Unsafe.Add(ref crcTableRef, (int)((crc ^ Unsafe.Add(ref bufferRef, i)) & 0xFF)) ^ (crc >> 8);
            }

            return crc;
        }
    }
}
