// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#if NET5_0_OR_GREATER
using System.Runtime.Intrinsics.Arm;
#endif
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
        private const uint Base = 65521;

        // NMAX is the largest n such that 255n(n+1)/2 + (n+1)(BASE-1) <= 2^32-1
        private const uint Nmax = 5552;

#if SUPPORTS_RUNTIME_INTRINSICS
        private const int MinBufferSize = 64;

        private const uint ArmBlockSize = 1 << 5;

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
            if (Ssse3.IsSupported && buffer.Length >= MinBufferSize)
            {
                return CalculateSse(adler, buffer);
            }
#if NET5_0_OR_GREATER
            if (AdvSimd.IsSupported)
            {
                return CalculateArm(adler, buffer);
            }
#endif
#endif
            return CalculateScalar(adler, buffer);
        }

        // Based on https://github.com/chromium/chromium/blob/master/third_party/zlib/adler32_simd.c
#if SUPPORTS_RUNTIME_INTRINSICS
        [MethodImpl(InliningOptions.HotPath | InliningOptions.ShortMethod)]
        private static unsafe uint CalculateSse(uint adler, ReadOnlySpan<byte> buffer)
        {
            uint s1 = adler & 0xFFFF;
            uint s2 = (adler >> 16) & 0xFFFF;

            // Process the data in blocks.
            const int BLOCK_SIZE = 1 << 5;

            uint length = (uint)buffer.Length;
            uint blocks = length / BLOCK_SIZE;
            length -= blocks * BLOCK_SIZE;

            int index = 0;
            fixed (byte* bufferPtr = buffer)
            {
                fixed (byte* tapPtr = Tap1Tap2)
                {
                    index += (int)blocks * BLOCK_SIZE;
                    byte* localBufferPtr = bufferPtr;

                    // _mm_setr_epi8 on x86
                    Vector128<sbyte> tap1 = Sse2.LoadVector128((sbyte*)tapPtr);
                    Vector128<sbyte> tap2 = Sse2.LoadVector128((sbyte*)(tapPtr + 0x10));
                    Vector128<byte> zero = Vector128<byte>.Zero;
                    var ones = Vector128.Create((short)1);

                    while (blocks > 0)
                    {
                        uint n = Nmax / BLOCK_SIZE;  /* The NMAX constraint. */
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

                            localBufferPtr += BLOCK_SIZE;
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
                        s1 %= Base;
                        s2 %= Base;
                    }

                    if (length > 0)
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

                        if (s1 >= Base)
                        {
                            s1 -= Base;
                        }

                        s2 %= Base;
                    }

                    return s1 | (s2 << 16);
                }
            }
        }

#if NET5_0_OR_GREATER

        // Based on: https://github.com/chromium/chromium/blob/master/third_party/zlib/adler32_simd.c
        [MethodImpl(InliningOptions.HotPath | InliningOptions.ShortMethod)]
        private static unsafe uint CalculateArm(uint crc, ReadOnlySpan<byte> buffer)
        {
            // Split Adler-32 into component sums.
            uint s1 = crc & 0xFFFF;
            uint s2 = (crc >> 16) & 0xFFFF;
            int len = buffer.Length;
            int bufferOffset = 0;

            // Process the data in blocks.
            long blocks = len / ArmBlockSize;
            len -= (int)(blocks * ArmBlockSize);
            fixed (byte* bufferPtr = buffer)
            {
                while (blocks != 0)
                {
                    uint n = Nmax / ArmBlockSize;
                    if (n > blocks)
                    {
                        n = (uint)blocks;
                    }

                    blocks -= n;

                    // Process n blocks of data. At most nMax data bytes can be
                    // processed before s2 must be reduced modulo Base.
                    var vs2 = Vector128.Create(0, 0, 0, s1 * n);
                    Vector128<uint> vs1 = Vector128<uint>.Zero;
                    Vector128<ushort> vColumnSum1 = Vector128<ushort>.Zero;
                    Vector128<ushort> vColumnSum2 = Vector128<ushort>.Zero;
                    Vector128<ushort> vColumnSum3 = Vector128<ushort>.Zero;
                    Vector128<ushort> vColumnSum4 = Vector128<ushort>.Zero;

                    do
                    {
                        // Load 32 input bytes.
                        Vector128<ushort> bytes1 = AdvSimd.LoadVector128(bufferPtr + bufferOffset).AsUInt16();
                        Vector128<ushort> bytes2 = AdvSimd.LoadVector128(bufferPtr + bufferOffset + 16).AsUInt16();

                        // Add previous block byte sum to v_s2.
                        vs2 = AdvSimd.Add(vs2, vs1);

                        // Horizontally add the bytes for s1.
                        vs1 = AdvSimd.AddPairwiseWideningAndAdd(
                            vs1.AsUInt32(),
                            AdvSimd.AddPairwiseWideningAndAdd(AdvSimd.AddPairwiseWidening(bytes1.AsByte()).AsUInt16(), bytes2.AsByte()));

                        // Vertically add the bytes for s2.
                        vColumnSum1 = AdvSimd.AddWideningLower(vColumnSum1, bytes1.GetLower().AsByte());
                        vColumnSum2 = AdvSimd.AddWideningLower(vColumnSum2, bytes1.GetUpper().AsByte());
                        vColumnSum3 = AdvSimd.AddWideningLower(vColumnSum3, bytes2.GetLower().AsByte());
                        vColumnSum4 = AdvSimd.AddWideningLower(vColumnSum4, bytes2.GetUpper().AsByte());

                        bufferOffset += (int)ArmBlockSize;
                    } while (--n > 0);

                    vs2 = AdvSimd.ShiftLeftLogical(vs2, 5);

                    // Multiply-add bytes by [ 32, 31, 30, ... ] for s2.
                    vs2 = AdvSimd.MultiplyWideningLowerAndAdd(vs2, vColumnSum1.GetLower(), Vector64.Create((ushort)32, 31, 30, 29));
                    vs2 = AdvSimd.MultiplyWideningLowerAndAdd(vs2, vColumnSum1.GetUpper(), Vector64.Create((ushort)28, 27, 26, 25));
                    vs2 = AdvSimd.MultiplyWideningLowerAndAdd(vs2, vColumnSum2.GetLower(), Vector64.Create((ushort)24, 23, 22, 21));
                    vs2 = AdvSimd.MultiplyWideningLowerAndAdd(vs2, vColumnSum2.GetUpper(), Vector64.Create((ushort)20, 19, 18, 17));
                    vs2 = AdvSimd.MultiplyWideningLowerAndAdd(vs2, vColumnSum3.GetLower(), Vector64.Create((ushort)16, 15, 14, 13));
                    vs2 = AdvSimd.MultiplyWideningLowerAndAdd(vs2, vColumnSum3.GetUpper(), Vector64.Create((ushort)12, 11, 10, 9));
                    vs2 = AdvSimd.MultiplyWideningLowerAndAdd(vs2, vColumnSum4.GetLower(), Vector64.Create((ushort)8, 7, 6, 5));
                    vs2 = AdvSimd.MultiplyWideningLowerAndAdd(vs2, vColumnSum4.GetUpper(), Vector64.Create((ushort)4, 3, 2, 1));

                    // Sum epi32 ints v_s1(s2) and accumulate in s1(s2).
                    Vector64<uint> sum1 = AdvSimd.AddPairwise(vs1.GetLower(), vs1.GetUpper());
                    Vector64<uint> sum2 = AdvSimd.AddPairwise(vs2.GetLower(), vs2.GetUpper());
                    Vector64<uint> s1s2 = AdvSimd.AddPairwise(sum1, sum2);

                    // Store the results.
                    s1 = AdvSimd.Extract(s1s2, 0);
                    s2 = AdvSimd.Extract(s1s2, 1);

                    // Reduce.
                    s1 %= Base;
                    s2 %= Base;
                }
            }

            // Handle leftover data.
            if (len != 0)
            {
                if (len >= 16)
                {
                    s2 += s1 += buffer[bufferOffset++];
                    s2 += s1 += buffer[bufferOffset++];
                    s2 += s1 += buffer[bufferOffset++];
                    s2 += s1 += buffer[bufferOffset++];

                    s2 += s1 += buffer[bufferOffset++];
                    s2 += s1 += buffer[bufferOffset++];
                    s2 += s1 += buffer[bufferOffset++];
                    s2 += s1 += buffer[bufferOffset++];

                    s2 += s1 += buffer[bufferOffset++];
                    s2 += s1 += buffer[bufferOffset++];
                    s2 += s1 += buffer[bufferOffset++];
                    s2 += s1 += buffer[bufferOffset++];

                    s2 += s1 += buffer[bufferOffset++];
                    s2 += s1 += buffer[bufferOffset++];
                    s2 += s1 += buffer[bufferOffset++];
                    s2 += s1 += buffer[bufferOffset++];

                    len -= 16;
                }

                while (len-- > 0)
                {
                    s2 += s1 += buffer[bufferOffset++];
                }

                if (s1 >= Base)
                {
                    s1 -= Base;
                }

                s2 %= Base;
            }

            // Return the recombined sums.
            return s1 | (s2 << 16);
        }

#endif
#endif

        [MethodImpl(InliningOptions.HotPath | InliningOptions.ShortMethod)]
        private static unsafe uint CalculateScalar(uint adler, ReadOnlySpan<byte> buffer)
        {
            uint s1 = adler & 0xFFFF;
            uint s2 = (adler >> 16) & 0xFFFF;
            uint k;

            fixed (byte* bufferPtr = buffer)
            {
                byte* localBufferPtr = bufferPtr;
                uint length = (uint)buffer.Length;

                while (length > 0)
                {
                    k = length < Nmax ? length : Nmax;
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

                    s1 %= Base;
                    s2 %= Base;
                }

                return (s2 << 16) | s1;
            }
        }
    }
}
