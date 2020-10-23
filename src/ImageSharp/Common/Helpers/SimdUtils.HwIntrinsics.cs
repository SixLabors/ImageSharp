// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

#if SUPPORTS_RUNTIME_INTRINSICS
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp
{
    internal static partial class SimdUtils
    {
        public static class HwIntrinsics
        {
            public static ReadOnlySpan<byte> PermuteMaskDeinterleave8x32 => new byte[] { 0, 0, 0, 0, 4, 0, 0, 0, 1, 0, 0, 0, 5, 0, 0, 0, 2, 0, 0, 0, 6, 0, 0, 0, 3, 0, 0, 0, 7, 0, 0, 0 };

            /// <summary>
            /// <see cref="ByteToNormalizedFloat"/> as many elements as possible, slicing them down (keeping the remainder).
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            internal static void ByteToNormalizedFloatReduce(
                ref ReadOnlySpan<byte> source,
                ref Span<float> dest)
            {
                DebugGuard.IsTrue(source.Length == dest.Length, nameof(source), "Input spans must be of same length!");

                if (Avx2.IsSupported || Sse2.IsSupported)
                {
                    int remainder;
                    if (Avx2.IsSupported)
                    {
                        remainder = ImageMaths.ModuloP2(source.Length, Vector256<byte>.Count);
                    }
                    else
                    {
                        remainder = ImageMaths.ModuloP2(source.Length, Vector128<byte>.Count);
                    }

                    int adjustedCount = source.Length - remainder;

                    if (adjustedCount > 0)
                    {
                        ByteToNormalizedFloat(source.Slice(0, adjustedCount), dest.Slice(0, adjustedCount));

                        source = source.Slice(adjustedCount);
                        dest = dest.Slice(adjustedCount);
                    }
                }
            }

            /// <summary>
            /// Implementation <see cref="SimdUtils.ByteToNormalizedFloat"/>, which is faster on new RyuJIT runtime.
            /// </summary>
            /// <remarks>
            /// Implementation is based on MagicScaler code:
            /// https://github.com/saucecontrol/PhotoSauce/blob/b5811908041200488aa18fdfd17df5fc457415dc/src/MagicScaler/Magic/Processors/ConvertersFloat.cs#L80-L182
            /// </remarks>
            internal static unsafe void ByteToNormalizedFloat(
                ReadOnlySpan<byte> source,
                Span<float> dest)
            {
                if (Avx2.IsSupported)
                {
                    VerifySpanInput(source, dest, Vector256<byte>.Count);

                    int n = dest.Length / Vector256<byte>.Count;

                    byte* sourceBase = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(source));

                    ref Vector256<float> destBase =
                        ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(dest));

                    var scale = Vector256.Create(1 / (float)byte.MaxValue);

                    for (int i = 0; i < n; i++)
                    {
                        int si = Vector256<byte>.Count * i;
                        Vector256<int> i0 = Avx2.ConvertToVector256Int32(sourceBase + si);
                        Vector256<int> i1 = Avx2.ConvertToVector256Int32(sourceBase + si + Vector256<int>.Count);
                        Vector256<int> i2 = Avx2.ConvertToVector256Int32(sourceBase + si + (Vector256<int>.Count * 2));
                        Vector256<int> i3 = Avx2.ConvertToVector256Int32(sourceBase + si + (Vector256<int>.Count * 3));

                        Vector256<float> f0 = Avx.Multiply(scale, Avx.ConvertToVector256Single(i0));
                        Vector256<float> f1 = Avx.Multiply(scale, Avx.ConvertToVector256Single(i1));
                        Vector256<float> f2 = Avx.Multiply(scale, Avx.ConvertToVector256Single(i2));
                        Vector256<float> f3 = Avx.Multiply(scale, Avx.ConvertToVector256Single(i3));

                        ref Vector256<float> d = ref Unsafe.Add(ref destBase, i * 4);

                        d = f0;
                        Unsafe.Add(ref d, 1) = f1;
                        Unsafe.Add(ref d, 2) = f2;
                        Unsafe.Add(ref d, 3) = f3;
                    }
                }
                else
                {
                    // Sse
                    VerifySpanInput(source, dest, Vector128<byte>.Count);

                    int n = dest.Length / Vector128<byte>.Count;

                    byte* sourceBase = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(source));

                    ref Vector128<float> destBase =
                        ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(dest));

                    var scale = Vector128.Create(1 / (float)byte.MaxValue);
                    Vector128<byte> zero = Vector128<byte>.Zero;

                    for (int i = 0; i < n; i++)
                    {
                        int si = Vector128<byte>.Count * i;

                        Vector128<int> i0, i1, i2, i3;
                        if (Sse41.IsSupported)
                        {
                            i0 = Sse41.ConvertToVector128Int32(sourceBase + si);
                            i1 = Sse41.ConvertToVector128Int32(sourceBase + si + Vector128<int>.Count);
                            i2 = Sse41.ConvertToVector128Int32(sourceBase + si + (Vector128<int>.Count * 2));
                            i3 = Sse41.ConvertToVector128Int32(sourceBase + si + (Vector128<int>.Count * 3));
                        }
                        else
                        {
                            Vector128<byte> b = Sse2.LoadVector128(sourceBase + si);
                            Vector128<short> s0 = Sse2.UnpackLow(b, zero).AsInt16();
                            Vector128<short> s1 = Sse2.UnpackHigh(b, zero).AsInt16();

                            i0 = Sse2.UnpackLow(s0, zero.AsInt16()).AsInt32();
                            i1 = Sse2.UnpackHigh(s0, zero.AsInt16()).AsInt32();
                            i2 = Sse2.UnpackLow(s1, zero.AsInt16()).AsInt32();
                            i3 = Sse2.UnpackHigh(s1, zero.AsInt16()).AsInt32();
                        }

                        Vector128<float> f0 = Sse.Multiply(scale, Sse2.ConvertToVector128Single(i0));
                        Vector128<float> f1 = Sse.Multiply(scale, Sse2.ConvertToVector128Single(i1));
                        Vector128<float> f2 = Sse.Multiply(scale, Sse2.ConvertToVector128Single(i2));
                        Vector128<float> f3 = Sse.Multiply(scale, Sse2.ConvertToVector128Single(i3));

                        ref Vector128<float> d = ref Unsafe.Add(ref destBase, i * 4);

                        d = f0;
                        Unsafe.Add(ref d, 1) = f1;
                        Unsafe.Add(ref d, 2) = f2;
                        Unsafe.Add(ref d, 3) = f3;
                    }
                }
            }

            /// <summary>
            /// <see cref="NormalizedFloatToByteSaturate"/> as many elements as possible, slicing them down (keeping the remainder).
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            internal static void NormalizedFloatToByteSaturateReduce(
                ref ReadOnlySpan<float> source,
                ref Span<byte> dest)
            {
                DebugGuard.IsTrue(source.Length == dest.Length, nameof(source), "Input spans must be of same length!");

                if (Avx2.IsSupported || Sse2.IsSupported)
                {
                    int remainder;
                    if (Avx2.IsSupported)
                    {
                        remainder = ImageMaths.ModuloP2(source.Length, Vector256<byte>.Count);
                    }
                    else
                    {
                        remainder = ImageMaths.ModuloP2(source.Length, Vector128<byte>.Count);
                    }

                    int adjustedCount = source.Length - remainder;

                    if (adjustedCount > 0)
                    {
                        NormalizedFloatToByteSaturate(
                            source.Slice(0, adjustedCount),
                            dest.Slice(0, adjustedCount));

                        source = source.Slice(adjustedCount);
                        dest = dest.Slice(adjustedCount);
                    }
                }
            }

            /// <summary>
            /// Implementation of <see cref="SimdUtils.NormalizedFloatToByteSaturate"/>, which is faster on new .NET runtime.
            /// </summary>
            /// <remarks>
            /// Implementation is based on MagicScaler code:
            /// https://github.com/saucecontrol/PhotoSauce/blob/b5811908041200488aa18fdfd17df5fc457415dc/src/MagicScaler/Magic/Processors/ConvertersFloat.cs#L541-L622
            /// </remarks>
            internal static void NormalizedFloatToByteSaturate(
                ReadOnlySpan<float> source,
                Span<byte> dest)
            {
                if (Avx2.IsSupported)
                {
                    VerifySpanInput(source, dest, Vector256<byte>.Count);

                    int n = dest.Length / Vector256<byte>.Count;

                    ref Vector256<float> sourceBase =
                        ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(source));

                    ref Vector256<byte> destBase =
                        ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(dest));

                    var scale = Vector256.Create((float)byte.MaxValue);
                    ref byte maskBase = ref MemoryMarshal.GetReference(PermuteMaskDeinterleave8x32);
                    Vector256<int> mask = Unsafe.As<byte, Vector256<int>>(ref maskBase);

                    for (int i = 0; i < n; i++)
                    {
                        ref Vector256<float> s = ref Unsafe.Add(ref sourceBase, i * 4);

                        Vector256<float> f0 = Avx.Multiply(scale, s);
                        Vector256<float> f1 = Avx.Multiply(scale, Unsafe.Add(ref s, 1));
                        Vector256<float> f2 = Avx.Multiply(scale, Unsafe.Add(ref s, 2));
                        Vector256<float> f3 = Avx.Multiply(scale, Unsafe.Add(ref s, 3));

                        Vector256<int> w0 = Avx.ConvertToVector256Int32(f0);
                        Vector256<int> w1 = Avx.ConvertToVector256Int32(f1);
                        Vector256<int> w2 = Avx.ConvertToVector256Int32(f2);
                        Vector256<int> w3 = Avx.ConvertToVector256Int32(f3);

                        Vector256<short> u0 = Avx2.PackSignedSaturate(w0, w1);
                        Vector256<short> u1 = Avx2.PackSignedSaturate(w2, w3);
                        Vector256<byte> b = Avx2.PackUnsignedSaturate(u0, u1);
                        b = Avx2.PermuteVar8x32(b.AsInt32(), mask).AsByte();

                        Unsafe.Add(ref destBase, i) = b;
                    }
                }
                else
                {
                    // Sse
                    VerifySpanInput(source, dest, Vector128<byte>.Count);

                    int n = dest.Length / Vector128<byte>.Count;

                    ref Vector128<float> sourceBase =
                        ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(source));

                    ref Vector128<byte> destBase =
                        ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(dest));

                    var scale = Vector128.Create((float)byte.MaxValue);

                    for (int i = 0; i < n; i++)
                    {
                        ref Vector128<float> s = ref Unsafe.Add(ref sourceBase, i * 4);

                        Vector128<float> f0 = Sse.Multiply(scale, s);
                        Vector128<float> f1 = Sse.Multiply(scale, Unsafe.Add(ref s, 1));
                        Vector128<float> f2 = Sse.Multiply(scale, Unsafe.Add(ref s, 2));
                        Vector128<float> f3 = Sse.Multiply(scale, Unsafe.Add(ref s, 3));

                        Vector128<int> w0 = Sse2.ConvertToVector128Int32(f0);
                        Vector128<int> w1 = Sse2.ConvertToVector128Int32(f1);
                        Vector128<int> w2 = Sse2.ConvertToVector128Int32(f2);
                        Vector128<int> w3 = Sse2.ConvertToVector128Int32(f3);

                        Vector128<short> u0 = Sse2.PackSignedSaturate(w0, w1);
                        Vector128<short> u1 = Sse2.PackSignedSaturate(w2, w3);

                        Unsafe.Add(ref destBase, i) = Sse2.PackUnsignedSaturate(u0, u1);
                    }
                }
            }
        }
    }
}
#endif
