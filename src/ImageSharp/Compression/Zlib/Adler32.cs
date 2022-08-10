// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

#pragma warning disable IDE0007 // Use implicit type
namespace SixLabors.ImageSharp.Compression.Zlib
{
    /// <summary>
    /// Calculates the 32 bit Adler checksum of a given buffer according to
    /// RFC 1950. ZLIB Compressed Data Format Specification version 3.3)
    /// </summary>
    internal static class Adler32
    {
        /// <summary>
        /// The default initial seed value of a Adler32 checksum calculation.
        /// </summary>
        public const uint SeedValue = 1U;

        // Largest prime smaller than 65536
        private const uint BASE = 65521;

        // NMAX is the largest n such that 255n(n+1)/2 + (n+1)(BASE-1) <= 2^32-1
        private const uint NMAX = 5552;

#if SUPPORTS_RUNTIME_INTRINSICS
        private const int MinBufferSize = 64;

        private const int BlockSize = 1 << 5;

        // The C# compiler emits this as a compile-time constant embedded in the PE file.
        private static ReadOnlySpan<byte> Tap1Tap2 => new byte[]
        {
            32, 31, 30, 29, 28, 27, 26, 25, 24, 23, 22, 21, 20, 19, 18, 17, // tap1
            16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 // tap2
        };
#endif

        /// <summary>
        /// Calculates the Adler32 checksum with the bytes taken from the span.
        /// </summary>
        /// <param name="buffer">The readonly span of bytes.</param>
        /// <returns>The <see cref="uint"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static uint Calculate(ReadOnlySpan<byte> buffer)
            => Calculate(SeedValue, buffer);

        /// <summary>
        /// Calculates the Adler32 checksum with the bytes taken from the span and seed.
        /// </summary>
        /// <param name="adler">The input Adler32 value.</param>
        /// <param name="buffer">The readonly span of bytes.</param>
        /// <returns>The <see cref="uint"/>.</returns>
        [MethodImpl(InliningOptions.HotPath | InliningOptions.ShortMethod)]
        public static uint Calculate(uint adler, ReadOnlySpan<byte> buffer)
        {
            if (buffer.IsEmpty)
            {
                return adler;
            }

#if SUPPORTS_RUNTIME_INTRINSICS
            if (Avx2.IsSupported && buffer.Length >= MinBufferSize)
            {
                return CalculateAvx2(adler, buffer);
            }

            if (Ssse3.IsSupported && buffer.Length >= MinBufferSize)
            {
                return CalculateSse(adler, buffer);
            }

            return CalculateScalar(adler, buffer);
#else
            return CalculateScalar(adler, buffer);
#endif
        }

        // Based on https://github.com/chromium/chromium/blob/master/third_party/zlib/adler32_simd.c
#if SUPPORTS_RUNTIME_INTRINSICS
        [MethodImpl(InliningOptions.HotPath | InliningOptions.ShortMethod)]
        private static unsafe uint CalculateSse(uint adler, ReadOnlySpan<byte> buffer)
        {
            uint s1 = adler & 0xFFFF;
            uint s2 = (adler >> 16) & 0xFFFF;

            // Process the data in blocks.
            uint length = (uint)buffer.Length;
            uint blocks = length / BlockSize;
            length -= blocks * BlockSize;

            fixed (byte* bufferPtr = &MemoryMarshal.GetReference(buffer))
            {
                fixed (byte* tapPtr = &MemoryMarshal.GetReference(Tap1Tap2))
                {
                    byte* localBufferPtr = bufferPtr;

                    // _mm_setr_epi8 on x86
                    Vector128<sbyte> tap1 = Sse2.LoadVector128((sbyte*)tapPtr);
                    Vector128<sbyte> tap2 = Sse2.LoadVector128((sbyte*)(tapPtr + 0x10));
                    Vector128<byte> zero = Vector128<byte>.Zero;
                    var ones = Vector128.Create((short)1);

                    while (blocks > 0)
                    {
                        uint n = NMAX / BlockSize;  /* The NMAX constraint. */
                        if (n > blocks)
                        {
                            n = blocks;
                        }

                        blocks -= n;

                        // Process n blocks of data. At most NMAX data bytes can be
                        // processed before s2 must be reduced modulo BASE.
                        Vector128<uint> v_ps = Vector128.CreateScalar(s1 * n);
                        Vector128<uint> v_s2 = Vector128.CreateScalar(s2);
                        Vector128<uint> v_s1 = Vector128<uint>.Zero;

                        do
                        {
                            // Load 32 input bytes.
                            Vector128<byte> bytes1 = Sse3.LoadDquVector128(localBufferPtr);
                            Vector128<byte> bytes2 = Sse3.LoadDquVector128(localBufferPtr + 0x10);

                            // Add previous block byte sum to v_ps.
                            v_ps = Sse2.Add(v_ps, v_s1);

                            // Horizontally add the bytes for s1, multiply-adds the
                            // bytes by [ 32, 31, 30, ... ] for s2.
                            v_s1 = Sse2.Add(v_s1, Sse2.SumAbsoluteDifferences(bytes1, zero).AsUInt32());
                            Vector128<short> mad1 = Ssse3.MultiplyAddAdjacent(bytes1, tap1);
                            v_s2 = Sse2.Add(v_s2, Sse2.MultiplyAddAdjacent(mad1, ones).AsUInt32());

                            v_s1 = Sse2.Add(v_s1, Sse2.SumAbsoluteDifferences(bytes2, zero).AsUInt32());
                            Vector128<short> mad2 = Ssse3.MultiplyAddAdjacent(bytes2, tap2);
                            v_s2 = Sse2.Add(v_s2, Sse2.MultiplyAddAdjacent(mad2, ones).AsUInt32());

                            localBufferPtr += BlockSize;
                        }
                        while (--n > 0);

                        v_s2 = Sse2.Add(v_s2, Sse2.ShiftLeftLogical(v_ps, 5));

                        // Sum epi32 ints v_s1(s2) and accumulate in s1(s2).
                        const byte S2301 = 0b1011_0001;  // A B C D -> B A D C
                        const byte S1032 = 0b0100_1110;  // A B C D -> C D A B

                        v_s1 = Sse2.Add(v_s1, Sse2.Shuffle(v_s1, S1032));

                        s1 += v_s1.ToScalar();

                        v_s2 = Sse2.Add(v_s2, Sse2.Shuffle(v_s2, S2301));
                        v_s2 = Sse2.Add(v_s2, Sse2.Shuffle(v_s2, S1032));

                        s2 = v_s2.ToScalar();

                        // Reduce.
                        s1 %= BASE;
                        s2 %= BASE;
                    }

                    if (length > 0)
                    {
                        HandleLeftOver(localBufferPtr, length, ref s1, ref s2);
                    }

                    return s1 | (s2 << 16);
                }
            }
        }

        // Based on: https://github.com/zlib-ng/zlib-ng/blob/develop/arch/x86/adler32_avx2.c
        [MethodImpl(InliningOptions.HotPath | InliningOptions.ShortMethod)]
        public static unsafe uint CalculateAvx2(uint adler, ReadOnlySpan<byte> buffer)
        {
            uint s1 = adler & 0xFFFF;
            uint s2 = (adler >> 16) & 0xFFFF;
            uint length = (uint)buffer.Length;

            fixed (byte* bufferPtr = &MemoryMarshal.GetReference(buffer))
            {
                byte* localBufferPtr = bufferPtr;

                Vector256<byte> zero = Vector256<byte>.Zero;
                var dot3v = Vector256.Create((short)1);
                var dot2v = Vector256.Create(32, 31, 30, 29, 28, 27, 26, 25, 24, 23, 22, 21, 20, 19, 18, 17, 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1);

                // Process n blocks of data. At most NMAX data bytes can be
                // processed before s2 must be reduced modulo BASE.
                var vs1 = Vector256.CreateScalar(s1);
                var vs2 = Vector256.CreateScalar(s2);

                while (length >= 32)
                {
                    int k = length < NMAX ? (int)length : (int)NMAX;
                    k -= k % 32;
                    length -= (uint)k;

                    Vector256<uint> vs10 = vs1;
                    Vector256<uint> vs3 = Vector256<uint>.Zero;

                    while (k >= 32)
                    {
                        // Load 32 input bytes.
                        Vector256<byte> block = Avx.LoadVector256(localBufferPtr);

                        // Sum of abs diff, resulting in 2 x int32's
                        Vector256<ushort> vs1sad = Avx2.SumAbsoluteDifferences(block, zero);

                        vs1 = Avx2.Add(vs1, vs1sad.AsUInt32());
                        vs3 = Avx2.Add(vs3, vs10);

                        // sum 32 uint8s to 16 shorts.
                        Vector256<short> vshortsum2 = Avx2.MultiplyAddAdjacent(block, dot2v);

                        // sum 16 shorts to 8 uint32s.
                        Vector256<int> vsum2 = Avx2.MultiplyAddAdjacent(vshortsum2, dot3v);

                        vs2 = Avx2.Add(vsum2.AsUInt32(), vs2);
                        vs10 = vs1;

                        localBufferPtr += BlockSize;
                        k -= 32;
                    }

                    // Defer the multiplication with 32 to outside of the loop.
                    vs3 = Avx2.ShiftLeftLogical(vs3, 5);
                    vs2 = Avx2.Add(vs2, vs3);

                    s1 = (uint)Numerics.EvenReduceSum(vs1.AsInt32());
                    s2 = (uint)Numerics.ReduceSum(vs2.AsInt32());

                    s1 %= BASE;
                    s2 %= BASE;

                    vs1 = Vector256.CreateScalar(s1);
                    vs2 = Vector256.CreateScalar(s2);
                }

                if (length > 0)
                {
                    HandleLeftOver(localBufferPtr, length, ref s1, ref s2);
                }

                return s1 | (s2 << 16);
            }
        }

        private static unsafe void HandleLeftOver(byte* localBufferPtr, uint length, ref uint s1, ref uint s2)
        {
            if (length >= 16)
            {
                s2 += s1 += localBufferPtr[0];
                s2 += s1 += localBufferPtr[1];
                s2 += s1 += localBufferPtr[2];
                s2 += s1 += localBufferPtr[3];
                s2 += s1 += localBufferPtr[4];
                s2 += s1 += localBufferPtr[5];
                s2 += s1 += localBufferPtr[6];
                s2 += s1 += localBufferPtr[7];
                s2 += s1 += localBufferPtr[8];
                s2 += s1 += localBufferPtr[9];
                s2 += s1 += localBufferPtr[10];
                s2 += s1 += localBufferPtr[11];
                s2 += s1 += localBufferPtr[12];
                s2 += s1 += localBufferPtr[13];
                s2 += s1 += localBufferPtr[14];
                s2 += s1 += localBufferPtr[15];

                localBufferPtr += 16;
                length -= 16;
            }

            while (length-- > 0)
            {
                s2 += s1 += *localBufferPtr++;
            }

            if (s1 >= BASE)
            {
                s1 -= BASE;
            }

            s2 %= BASE;
        }
#endif

        [MethodImpl(InliningOptions.HotPath | InliningOptions.ShortMethod)]
        private static unsafe uint CalculateScalar(uint adler, ReadOnlySpan<byte> buffer)
        {
            uint s1 = adler & 0xFFFF;
            uint s2 = (adler >> 16) & 0xFFFF;
            uint k;

            fixed (byte* bufferPtr = buffer)
            {
                var localBufferPtr = bufferPtr;
                uint length = (uint)buffer.Length;

                while (length > 0)
                {
                    k = length < NMAX ? length : NMAX;
                    length -= k;

                    while (k >= 16)
                    {
                        s2 += s1 += localBufferPtr[0];
                        s2 += s1 += localBufferPtr[1];
                        s2 += s1 += localBufferPtr[2];
                        s2 += s1 += localBufferPtr[3];
                        s2 += s1 += localBufferPtr[4];
                        s2 += s1 += localBufferPtr[5];
                        s2 += s1 += localBufferPtr[6];
                        s2 += s1 += localBufferPtr[7];
                        s2 += s1 += localBufferPtr[8];
                        s2 += s1 += localBufferPtr[9];
                        s2 += s1 += localBufferPtr[10];
                        s2 += s1 += localBufferPtr[11];
                        s2 += s1 += localBufferPtr[12];
                        s2 += s1 += localBufferPtr[13];
                        s2 += s1 += localBufferPtr[14];
                        s2 += s1 += localBufferPtr[15];

                        localBufferPtr += 16;
                        k -= 16;
                    }

                    while (k-- > 0)
                    {
                        s2 += s1 += *localBufferPtr++;
                    }

                    s1 %= BASE;
                    s2 %= BASE;
                }

                return (s2 << 16) | s1;
            }
        }
    }
}
