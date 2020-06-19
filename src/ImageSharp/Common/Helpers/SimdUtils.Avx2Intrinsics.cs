// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

#if SUPPORTS_RUNTIME_INTRINSICS

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp
{
    internal static partial class SimdUtils
    {
        public static class Avx2Intrinsics
        {
            private static ReadOnlySpan<byte> PermuteMaskDeinterleave8x32 => new byte[] { 0, 0, 0, 0, 4, 0, 0, 0, 1, 0, 0, 0, 5, 0, 0, 0, 2, 0, 0, 0, 6, 0, 0, 0, 3, 0, 0, 0, 7, 0, 0, 0 };

            /// <summary>
            /// <see cref="NormalizedFloatToByteSaturate"/> as many elements as possible, slicing them down (keeping the remainder).
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            internal static void NormalizedFloatToByteSaturateReduce(
                ref ReadOnlySpan<float> source,
                ref Span<byte> dest)
            {
                DebugGuard.IsTrue(source.Length == dest.Length, nameof(source), "Input spans must be of same length!");

                if (Avx2.IsSupported)
                {
                    int remainder = ImageMaths.ModuloP2(source.Length, Vector256<byte>.Count);
                    int adjustedCount = source.Length - remainder;

                    if (adjustedCount > 0)
                    {
                        source = source.Slice(adjustedCount);
                        dest = dest.Slice(adjustedCount);
                        NormalizedFloatToByteSaturate(source, dest);
                    }
                }
            }

            /// <summary>
            /// Implementation of <see cref="SimdUtils.NormalizedFloatToByteSaturate"/>, which is faster on new .NET runtime.
            /// </summary>
            /// <remarks>
            /// Implementation is based on MagicScaler code:
            /// https://github.com/saucecontrol/PhotoSauce/blob/a9bd6e5162d2160419f0cf743fd4f536c079170b/src/MagicScaler/Magic/Processors/ConvertersFloat.cs#L453-L477
            /// </remarks>
            internal static void NormalizedFloatToByteSaturate(
                ReadOnlySpan<float> source,
                Span<byte> dest)
            {
                VerifySpanInput(source, dest, Vector256<byte>.Count);

                int n = dest.Length / Vector256<byte>.Count;

                ref Vector256<float> sourceBase =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(source));
                ref Vector256<byte> destBase = ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(dest));

                var maxBytes = Vector256.Create(255f);
                ref byte maskBase = ref MemoryMarshal.GetReference(PermuteMaskDeinterleave8x32);
                Vector256<int> mask = Unsafe.As<byte, Vector256<int>>(ref maskBase);

                for (int i = 0; i < n; i++)
                {
                    ref Vector256<float> s = ref Unsafe.Add(ref sourceBase, i * 4);

                    Vector256<float> f0 = s;
                    Vector256<float> f1 = Unsafe.Add(ref s, 1);
                    Vector256<float> f2 = Unsafe.Add(ref s, 2);
                    Vector256<float> f3 = Unsafe.Add(ref s, 3);

                    Vector256<int> w0 = ConvertToInt32(f0, maxBytes);
                    Vector256<int> w1 = ConvertToInt32(f1, maxBytes);
                    Vector256<int> w2 = ConvertToInt32(f2, maxBytes);
                    Vector256<int> w3 = ConvertToInt32(f3, maxBytes);

                    Vector256<short> u0 = Avx2.PackSignedSaturate(w0, w1);
                    Vector256<short> u1 = Avx2.PackSignedSaturate(w2, w3);
                    Vector256<byte> b = Avx2.PackUnsignedSaturate(u0, u1);
                    b = Avx2.PermuteVar8x32(b.AsInt32(), mask).AsByte();

                    Unsafe.Add(ref destBase, i) = b;
                }
            }

            internal static void PackBytesToUInt32SaturateChannel4Reduce(
                ref ReadOnlySpan<byte> channel0,
                ref ReadOnlySpan<byte> channel1,
                ref ReadOnlySpan<byte> channel2,
                ref Span<byte> dest)
            {
                DebugGuard.IsTrue(channel0.Length == dest.Length, nameof(channel0), "Input spans must be of same length!");
                DebugGuard.IsTrue(channel1.Length == dest.Length, nameof(channel1), "Input spans must be of same length!");
                DebugGuard.IsTrue(channel2.Length == dest.Length, nameof(channel2), "Input spans must be of same length!");

                if (Avx2.IsSupported)
                {
                    int remainder = ImageMaths.ModuloP2(channel1.Length, Vector256<byte>.Count);
                    int adjustedCount = channel1.Length - remainder;

                    if (adjustedCount > 0)
                    {
                        channel0 = channel0.Slice(adjustedCount);
                        channel1 = channel1.Slice(adjustedCount);
                        channel2 = channel2.Slice(adjustedCount);
                        dest = dest.Slice(adjustedCount);

                        PackBytesToUInt32SaturateChannel4(
                            channel0,
                            channel1,
                            channel2,
                            dest);

                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static void PackBytesToUInt32SaturateChannel4(
                ReadOnlySpan<byte> channel0,
                ReadOnlySpan<byte> channel1,
                ReadOnlySpan<byte> channel2,
                Span<byte> dest)
            {
                int n = dest.Length / Vector256<byte>.Count;

                ref Vector256<byte> source0Base =
                    ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(channel0));
                ref Vector256<byte> source1Base =
                    ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(channel1));
                ref Vector256<byte> source2Base =
                    ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(channel2));

                ref Vector256<byte> destBase = ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(dest));

                Vector256<byte> allOnes = Avx2.CompareEqual(Vector256<byte>.Zero, Vector256<byte>.Zero);

                for (int i = 0, j = 0; j < n; i += 1, j += 4)
                {
                    Vector256<byte> s0 = Unsafe.Add(ref source0Base, i);
                    Vector256<byte> s1 = Unsafe.Add(ref source1Base, i);
                    Vector256<byte> s2 = Unsafe.Add(ref source2Base, i);

                    s0 = Avx2.Permute4x64(s0.AsUInt64(), 0b_11_01_10_00).AsByte();
                    s1 = Avx2.Permute4x64(s1.AsUInt64(), 0b_11_01_10_00).AsByte();
                    s2 = Avx2.Permute4x64(s2.AsUInt64(), 0b_11_01_10_00).AsByte();

                    Vector256<ushort> s01Lo = Avx2.UnpackLow(s0, s1).AsUInt16();
                    Vector256<ushort> s01Hi = Avx2.UnpackHigh(s0, s1).AsUInt16();

                    s01Lo = Avx2.Permute4x64(s01Lo.AsUInt64(), 0b_11_01_10_00).AsUInt16();
                    s01Hi = Avx2.Permute4x64(s01Hi.AsUInt64(), 0b_11_01_10_00).AsUInt16();

                    Vector256<ushort> s23Lo = Avx2.UnpackLow(s2, allOnes).AsUInt16();
                    Vector256<ushort> s23Hi = Avx2.UnpackHigh(s2, allOnes).AsUInt16();

                    s23Lo = Avx2.Permute4x64(s23Lo.AsUInt64(), 0b_11_01_10_00).AsUInt16();
                    s23Hi = Avx2.Permute4x64(s23Hi.AsUInt64(), 0b_11_01_10_00).AsUInt16();

                    Vector256<byte> b0 = Avx2.UnpackLow(s01Lo, s23Lo).AsByte();
                    Vector256<byte> b1 = Avx2.UnpackHigh(s01Lo, s23Lo).AsByte();
                    Vector256<byte> b2 = Avx2.UnpackLow(s01Hi, s23Hi).AsByte();
                    Vector256<byte> b3 = Avx2.UnpackHigh(s01Hi, s23Hi).AsByte();

                    Unsafe.Add(ref destBase, j) = b0;
                    Unsafe.Add(ref destBase, j + 1) = b1;
                    Unsafe.Add(ref destBase, j + 2) = b2;
                    Unsafe.Add(ref destBase, j + 3) = b3;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static void PackBytesToUInt24Reduce(
                    ref ReadOnlySpan<byte> channel0,
                    ref ReadOnlySpan<byte> channel1,
                    ref ReadOnlySpan<byte> channel2,
                    ref Span<byte> dest)
            {
                DebugGuard.IsTrue(channel0.Length == dest.Length, nameof(channel0), "Input spans must be of same length!");
                DebugGuard.IsTrue(channel1.Length == dest.Length, nameof(channel1), "Input spans must be of same length!");
                DebugGuard.IsTrue(channel2.Length == dest.Length, nameof(channel2), "Input spans must be of same length!");

                if (Avx2.IsSupported)
                {
                    int remainder = ImageMaths.ModuloP2(channel0.Length, Vector256<byte>.Count);
                    int adjustedCount = channel0.Length - remainder;

                    if (adjustedCount > 0)
                    {
                        channel0 = channel0.Slice(adjustedCount);
                        channel1 = channel0.Slice(adjustedCount);
                        channel2 = channel0.Slice(adjustedCount);
                        dest = dest.Slice(adjustedCount);

                        PackBytesToUInt24(
                            channel0,
                            channel1,
                            channel2,
                            dest);

                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static void PackBytesToUInt24(
                    ReadOnlySpan<byte> channel0,
                    ReadOnlySpan<byte> channel1,
                    ReadOnlySpan<byte> channel2,
                    Span<byte> dest)
            {
                VerifySpanInput(channel0, dest, Vector256<byte>.Count);
                VerifySpanInput(channel1, dest, Vector256<byte>.Count);
                VerifySpanInput(channel2, dest, Vector256<byte>.Count);

                int n = dest.Length / Vector256<byte>.Count;

                ref Vector256<byte> source0Base =
                    ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(channel0));
                ref Vector256<byte> source1Base =
                    ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(channel1));
                ref Vector256<byte> source2Base =
                    ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(channel2));

                ref Vector256<byte> destBase =
                    ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(dest));

                Vector256<byte> s0Mask0 = Vector256.Create(0, -1, -1, 1, -1, -1, 2, -1, -1, 3, -1, -1, 4, -1, -1, 5, -1, -1, 6, -1, -1, 7, -1, -1, 8, -1, -1, 9, -1, -1, 10, -1).AsByte();
                Vector256<byte> s0Mask1 = Vector256.Create(-1, 11, -1, -1, 12, -1, -1, 13, -1, -1, 14, -1, -1, 15, -1, -1, 0, -1, -1, 1, -1, -1, 2, -1, -1, 3, -1, -1, 4, -1, -1, 5).AsByte();
                Vector256<byte> s0Mask2 = Vector256.Create(-1, -1, 6, -1, -1, 7, -1, -1, 8, -1, -1, 9, -1, -1, 10, -1, -1, 11, -1, -1, 12, -1, -1, 13, -1, -1, 14, -1, -1, 15, -1, -1).AsByte();

                Vector256<byte> s1Mask0 = Vector256.Create(-1, 0, -1, -1, 1, -1, -1, 2, -1, -1, 3, -1, -1, 4, -1, -1, 5, -1, -1, 6, -1, -1, 7, -1, -1, 8, -1, -1, 9, -1, -1, 10).AsByte();
                Vector256<byte> s1Mask1 = Vector256.Create(-1, -1, 11, -1, -1, 12, -1, -1, 13, -1, -1, 14, -1, -1, 15, -1, -1, 0, -1, -1, 1, -1, -1, 2, -1, -1, 3, -1, -1, 4, -1, -1).AsByte();
                Vector256<byte> s1Mask2 = Vector256.Create(5, -1, -1, 6, -1, -1, 7, -1, -1, 8, -1, -1, 9, -1, -1, 10, -1, -1, 11, -1, -1, 12, -1, -1, 13, -1, -1, 14, -1, -1, 15, -1).AsByte();

                Vector256<byte> s2Mask0 = Vector256.Create(-1, -1, 0, -1, -1, 1, -1, -1, 2, -1, -1, 3, -1, -1, 4, -1, -1, 5, -1, -1, 6, -1, -1, 7, -1, -1, 8, -1, -1, 9, -1, -1).AsByte();
                Vector256<byte> s2Mask1 = Vector256.Create(10, -1, -1, 11, -1, -1, 12, -1, -1, 13, -1, -1, 14, -1, -1, 15, -1, -1, 0, -1, -1, 1, -1, -1, 2, -1, -1, 3, -1, -1, 4, -1).AsByte();
                Vector256<byte> s2Mask2 = Vector256.Create(-1, 5, -1, -1, 6, -1, -1, 7, -1, -1, 8, -1, -1, 9, -1, -1, 10, -1, -1, 11, -1, -1, 12, -1, -1, 13, -1, -1, 14, -1, -1, 15).AsByte();

                for (int i = 0, j = 0; j < n; i += 1, j += 3)
                {
                    Vector256<byte> s0 = Unsafe.Add(ref source0Base, i);
                    Vector256<byte> s1 = Unsafe.Add(ref source1Base, i);
                    Vector256<byte> s2 = Unsafe.Add(ref source2Base, i);

                    Vector256<byte> loS0 = Avx2.Permute2x128(s0, s0, 0);
                    Vector256<byte> loS1 = Avx2.Permute2x128(s1, s1, 0);
                    Vector256<byte> loS2 = Avx2.Permute2x128(s2, s2, 0);

                    Vector256<byte> b0 = Avx2.Shuffle(loS0, s0Mask0);
                    b0 = Avx2.Or(b0, Avx2.Shuffle(loS1, s1Mask0));
                    b0 = Avx2.Or(b0, Avx2.Shuffle(loS2, s2Mask0));

                    Vector256<byte> b1 = Avx2.Shuffle(s0, s0Mask1);
                    b1 = Avx2.Or(b1, Avx2.Shuffle(s1, s1Mask1));
                    b1 = Avx2.Or(b1, Avx2.Shuffle(s2, s2Mask1));

                    Vector256<byte> hiS0 = Avx2.Permute2x128(s0, s0, 0b_0001_0001);
                    Vector256<byte> hiS1 = Avx2.Permute2x128(s1, s1, 0b_0001_0001);
                    Vector256<byte> hiS2 = Avx2.Permute2x128(s2, s2, 0b_0001_0001);

                    Vector256<byte> b2 = Avx2.Shuffle(hiS0, s0Mask2);
                    b2 = Avx2.Or(b2, Avx2.Shuffle(hiS1, s1Mask2));
                    b2 = Avx2.Or(b2, Avx2.Shuffle(hiS2, s2Mask2));

                    Unsafe.Add(ref destBase, j + 0) = b0;
                    Unsafe.Add(ref destBase, j + 1) = b1;
                    Unsafe.Add(ref destBase, j + 2) = b2;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Vector256<int> ConvertToInt32(Vector256<float> vf, Vector256<float> scale)
            {
                vf = Avx.Multiply(vf, scale);
                return Avx.ConvertToVector256Int32(vf);
            }
        }
    }
}
#endif
