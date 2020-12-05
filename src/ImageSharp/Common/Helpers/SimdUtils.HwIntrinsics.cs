// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

#if SUPPORTS_RUNTIME_INTRINSICS
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    internal static partial class SimdUtils
    {
        public static class HwIntrinsics
        {
            public static ReadOnlySpan<byte> PermuteMaskDeinterleave8x32 => new byte[] { 0, 0, 0, 0, 4, 0, 0, 0, 1, 0, 0, 0, 5, 0, 0, 0, 2, 0, 0, 0, 6, 0, 0, 0, 3, 0, 0, 0, 7, 0, 0, 0 };

            public static ReadOnlySpan<byte> PermuteMaskEvenOdd8x32 => new byte[] { 0, 0, 0, 0, 2, 0, 0, 0, 4, 0, 0, 0, 6, 0, 0, 0, 1, 0, 0, 0, 3, 0, 0, 0, 5, 0, 0, 0, 7, 0, 0, 0 };

            private static ReadOnlySpan<byte> ShuffleMaskPad4Nx16 => new byte[] { 0, 1, 2, 0x80, 3, 4, 5, 0x80, 6, 7, 8, 0x80, 9, 10, 11, 0x80 };

            private static ReadOnlySpan<byte> ShuffleMaskSlice4Nx16 => new byte[] { 0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14, 0x80, 0x80, 0x80, 0x80 };

            private static ReadOnlySpan<byte> ShuffleMaskShiftAlpha =>
                new byte[]
                {
                    0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14, 3, 7, 11, 15,
                    0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14, 3, 7, 11, 15
                };

            public static ReadOnlySpan<byte> PermuteMaskShiftAlpha8x32 =>
                new byte[]
                {
                    0, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 4, 0, 0, 0,
                    5, 0, 0, 0, 6, 0, 0, 0, 3, 0, 0, 0, 7, 0, 0, 0
                };

            /// <summary>
            /// Shuffle single-precision (32-bit) floating-point elements in <paramref name="source"/>
            /// using the control and store the results in <paramref name="dest"/>.
            /// </summary>
            /// <param name="source">The source span of floats.</param>
            /// <param name="dest">The destination span of floats.</param>
            /// <param name="control">The byte control.</param>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void Shuffle4Reduce(
                ref ReadOnlySpan<float> source,
                ref Span<float> dest,
                byte control)
            {
                if (Avx.IsSupported || Sse.IsSupported)
                {
                    int remainder = Avx.IsSupported
                        ? Numerics.ModuloP2(source.Length, Vector256<float>.Count)
                        : Numerics.ModuloP2(source.Length, Vector128<float>.Count);

                    int adjustedCount = source.Length - remainder;

                    if (adjustedCount > 0)
                    {
                        Shuffle4(
                            source.Slice(0, adjustedCount),
                            dest.Slice(0, adjustedCount),
                            control);

                        source = source.Slice(adjustedCount);
                        dest = dest.Slice(adjustedCount);
                    }
                }
            }

            /// <summary>
            /// Shuffle 8-bit integers within 128-bit lanes in <paramref name="source"/>
            /// using the control and store the results in <paramref name="dest"/>.
            /// </summary>
            /// <param name="source">The source span of bytes.</param>
            /// <param name="dest">The destination span of bytes.</param>
            /// <param name="control">The byte control.</param>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void Shuffle4Reduce(
                ref ReadOnlySpan<byte> source,
                ref Span<byte> dest,
                byte control)
            {
                if (Avx2.IsSupported || Ssse3.IsSupported)
                {
                    int remainder = Avx2.IsSupported
                        ? Numerics.ModuloP2(source.Length, Vector256<byte>.Count)
                        : Numerics.ModuloP2(source.Length, Vector128<byte>.Count);

                    int adjustedCount = source.Length - remainder;

                    if (adjustedCount > 0)
                    {
                        Shuffle4(
                            source.Slice(0, adjustedCount),
                            dest.Slice(0, adjustedCount),
                            control);

                        source = source.Slice(adjustedCount);
                        dest = dest.Slice(adjustedCount);
                    }
                }
            }

            /// <summary>
            /// Shuffles 8-bit integer triplets within 128-bit lanes in <paramref name="source"/>
            /// using the control and store the results in <paramref name="dest"/>.
            /// </summary>
            /// <param name="source">The source span of bytes.</param>
            /// <param name="dest">The destination span of bytes.</param>
            /// <param name="control">The byte control.</param>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void Shuffle3Reduce(
                ref ReadOnlySpan<byte> source,
                ref Span<byte> dest,
                byte control)
            {
                if (Ssse3.IsSupported)
                {
                    int remainder = source.Length % (Vector128<byte>.Count * 3);

                    int adjustedCount = source.Length - remainder;

                    if (adjustedCount > 0)
                    {
                        Shuffle3(
                            source.Slice(0, adjustedCount),
                            dest.Slice(0, adjustedCount),
                            control);

                        source = source.Slice(adjustedCount);
                        dest = dest.Slice(adjustedCount);
                    }
                }
            }

            /// <summary>
            /// Pads then shuffles 8-bit integers within 128-bit lanes in <paramref name="source"/>
            /// using the control and store the results in <paramref name="dest"/>.
            /// </summary>
            /// <param name="source">The source span of bytes.</param>
            /// <param name="dest">The destination span of bytes.</param>
            /// <param name="control">The byte control.</param>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void Pad3Shuffle4Reduce(
                ref ReadOnlySpan<byte> source,
                ref Span<byte> dest,
                byte control)
            {
                if (Ssse3.IsSupported)
                {
                    int remainder = source.Length % (Vector128<byte>.Count * 3);

                    int sourceCount = source.Length - remainder;
                    int destCount = sourceCount * 4 / 3;

                    if (sourceCount > 0)
                    {
                        Pad3Shuffle4(
                            source.Slice(0, sourceCount),
                            dest.Slice(0, destCount),
                            control);

                        source = source.Slice(sourceCount);
                        dest = dest.Slice(destCount);
                    }
                }
            }

            /// <summary>
            /// Shuffles then slices 8-bit integers within 128-bit lanes in <paramref name="source"/>
            /// using the control and store the results in <paramref name="dest"/>.
            /// </summary>
            /// <param name="source">The source span of bytes.</param>
            /// <param name="dest">The destination span of bytes.</param>
            /// <param name="control">The byte control.</param>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void Shuffle4Slice3Reduce(
                ref ReadOnlySpan<byte> source,
                ref Span<byte> dest,
                byte control)
            {
                if (Ssse3.IsSupported)
                {
                    int remainder = source.Length % (Vector128<byte>.Count * 4);

                    int sourceCount = source.Length - remainder;
                    int destCount = sourceCount * 3 / 4;

                    if (sourceCount > 0)
                    {
                        Shuffle4Slice3(
                            source.Slice(0, sourceCount),
                            dest.Slice(0, destCount),
                            control);

                        source = source.Slice(sourceCount);
                        dest = dest.Slice(destCount);
                    }
                }
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            private static void Shuffle4(
                ReadOnlySpan<float> source,
                Span<float> dest,
                byte control)
            {
                if (Avx.IsSupported)
                {
                    ref Vector256<float> sourceBase =
                        ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(source));

                    ref Vector256<float> destBase =
                        ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(dest));

                    int n = dest.Length / Vector256<float>.Count;
                    int m = Numerics.Modulo4(n);
                    int u = n - m;

                    for (int i = 0; i < u; i += 4)
                    {
                        ref Vector256<float> vd0 = ref Unsafe.Add(ref destBase, i);
                        ref Vector256<float> vs0 = ref Unsafe.Add(ref sourceBase, i);

                        vd0 = Avx.Permute(vs0, control);
                        Unsafe.Add(ref vd0, 1) = Avx.Permute(Unsafe.Add(ref vs0, 1), control);
                        Unsafe.Add(ref vd0, 2) = Avx.Permute(Unsafe.Add(ref vs0, 2), control);
                        Unsafe.Add(ref vd0, 3) = Avx.Permute(Unsafe.Add(ref vs0, 3), control);
                    }

                    if (m > 0)
                    {
                        for (int i = u; i < n; i++)
                        {
                            Unsafe.Add(ref destBase, i) = Avx.Permute(Unsafe.Add(ref sourceBase, i), control);
                        }
                    }
                }
                else
                {
                    // Sse
                    ref Vector128<float> sourceBase =
                        ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(source));

                    ref Vector128<float> destBase =
                        ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(dest));

                    int n = dest.Length / Vector128<float>.Count;
                    int m = Numerics.Modulo4(n);
                    int u = n - m;

                    for (int i = 0; i < u; i += 4)
                    {
                        ref Vector128<float> vd0 = ref Unsafe.Add(ref destBase, i);
                        ref Vector128<float> vs0 = ref Unsafe.Add(ref sourceBase, i);

                        vd0 = Sse.Shuffle(vs0, vs0, control);

                        Vector128<float> vs1 = Unsafe.Add(ref vs0, 1);
                        Unsafe.Add(ref vd0, 1) = Sse.Shuffle(vs1, vs1, control);

                        Vector128<float> vs2 = Unsafe.Add(ref vs0, 2);
                        Unsafe.Add(ref vd0, 2) = Sse.Shuffle(vs2, vs2, control);

                        Vector128<float> vs3 = Unsafe.Add(ref vs0, 3);
                        Unsafe.Add(ref vd0, 3) = Sse.Shuffle(vs3, vs3, control);
                    }

                    if (m > 0)
                    {
                        for (int i = u; i < n; i++)
                        {
                            Vector128<float> vs = Unsafe.Add(ref sourceBase, i);
                            Unsafe.Add(ref destBase, i) = Sse.Shuffle(vs, vs, control);
                        }
                    }
                }
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            private static void Shuffle4(
                ReadOnlySpan<byte> source,
                Span<byte> dest,
                byte control)
            {
                if (Avx2.IsSupported)
                {
                    // I've chosen to do this for convenience while we determine what
                    // shuffle controls to add to the library.
                    // We can add static ROS instances if need be in the future.
                    Span<byte> bytes = stackalloc byte[Vector256<byte>.Count];
                    Shuffle.MmShuffleSpan(ref bytes, control);
                    Vector256<byte> vshuffle = Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(bytes));

                    ref Vector256<byte> sourceBase =
                        ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(source));

                    ref Vector256<byte> destBase =
                        ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(dest));

                    int n = dest.Length / Vector256<byte>.Count;
                    int m = Numerics.Modulo4(n);
                    int u = n - m;

                    for (int i = 0; i < u; i += 4)
                    {
                        ref Vector256<byte> vs0 = ref Unsafe.Add(ref sourceBase, i);
                        ref Vector256<byte> vd0 = ref Unsafe.Add(ref destBase, i);

                        vd0 = Avx2.Shuffle(vs0, vshuffle);
                        Unsafe.Add(ref vd0, 1) = Avx2.Shuffle(Unsafe.Add(ref vs0, 1), vshuffle);
                        Unsafe.Add(ref vd0, 2) = Avx2.Shuffle(Unsafe.Add(ref vs0, 2), vshuffle);
                        Unsafe.Add(ref vd0, 3) = Avx2.Shuffle(Unsafe.Add(ref vs0, 3), vshuffle);
                    }

                    if (m > 0)
                    {
                        for (int i = u; i < n; i++)
                        {
                            Unsafe.Add(ref destBase, i) = Avx2.Shuffle(Unsafe.Add(ref sourceBase, i), vshuffle);
                        }
                    }
                }
                else
                {
                    // Ssse3
                    Span<byte> bytes = stackalloc byte[Vector128<byte>.Count];
                    Shuffle.MmShuffleSpan(ref bytes, control);
                    Vector128<byte> vshuffle = Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(bytes));

                    ref Vector128<byte> sourceBase =
                        ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(source));

                    ref Vector128<byte> destBase =
                        ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(dest));

                    int n = dest.Length / Vector128<byte>.Count;
                    int m = Numerics.Modulo4(n);
                    int u = n - m;

                    for (int i = 0; i < u; i += 4)
                    {
                        ref Vector128<byte> vs0 = ref Unsafe.Add(ref sourceBase, i);
                        ref Vector128<byte> vd0 = ref Unsafe.Add(ref destBase, i);

                        vd0 = Ssse3.Shuffle(vs0, vshuffle);
                        Unsafe.Add(ref vd0, 1) = Ssse3.Shuffle(Unsafe.Add(ref vs0, 1), vshuffle);
                        Unsafe.Add(ref vd0, 2) = Ssse3.Shuffle(Unsafe.Add(ref vs0, 2), vshuffle);
                        Unsafe.Add(ref vd0, 3) = Ssse3.Shuffle(Unsafe.Add(ref vs0, 3), vshuffle);
                    }

                    if (m > 0)
                    {
                        for (int i = u; i < n; i++)
                        {
                            Unsafe.Add(ref destBase, i) = Ssse3.Shuffle(Unsafe.Add(ref sourceBase, i), vshuffle);
                        }
                    }
                }
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            private static void Shuffle3(
                ReadOnlySpan<byte> source,
                Span<byte> dest,
                byte control)
            {
                if (Ssse3.IsSupported)
                {
                    ref byte vmaskBase = ref MemoryMarshal.GetReference(ShuffleMaskPad4Nx16);
                    Vector128<byte> vmask = Unsafe.As<byte, Vector128<byte>>(ref vmaskBase);
                    ref byte vmaskoBase = ref MemoryMarshal.GetReference(ShuffleMaskSlice4Nx16);
                    Vector128<byte> vmasko = Unsafe.As<byte, Vector128<byte>>(ref vmaskoBase);
                    Vector128<byte> vmaske = Ssse3.AlignRight(vmasko, vmasko, 12);

                    Span<byte> bytes = stackalloc byte[Vector128<byte>.Count];
                    Shuffle.MmShuffleSpan(ref bytes, control);
                    Vector128<byte> vshuffle = Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(bytes));

                    ref Vector128<byte> sourceBase =
                        ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(source));

                    ref Vector128<byte> destBase =
                        ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(dest));

                    int n = source.Length / Vector128<byte>.Count;

                    for (int i = 0; i < n; i += 3)
                    {
                        ref Vector128<byte> vs = ref Unsafe.Add(ref sourceBase, i);

                        Vector128<byte> v0 = vs;
                        Vector128<byte> v1 = Unsafe.Add(ref vs, 1);
                        Vector128<byte> v2 = Unsafe.Add(ref vs, 2);
                        Vector128<byte> v3 = Sse2.ShiftRightLogical128BitLane(v2, 4);

                        v2 = Ssse3.AlignRight(v2, v1, 8);
                        v1 = Ssse3.AlignRight(v1, v0, 12);

                        v0 = Ssse3.Shuffle(Ssse3.Shuffle(v0, vmask), vshuffle);
                        v1 = Ssse3.Shuffle(Ssse3.Shuffle(v1, vmask), vshuffle);
                        v2 = Ssse3.Shuffle(Ssse3.Shuffle(v2, vmask), vshuffle);
                        v3 = Ssse3.Shuffle(Ssse3.Shuffle(v3, vmask), vshuffle);

                        v0 = Ssse3.Shuffle(v0, vmaske);
                        v1 = Ssse3.Shuffle(v1, vmasko);
                        v2 = Ssse3.Shuffle(v2, vmaske);
                        v3 = Ssse3.Shuffle(v3, vmasko);

                        v0 = Ssse3.AlignRight(v1, v0, 4);
                        v3 = Ssse3.AlignRight(v3, v2, 12);

                        v1 = Sse2.ShiftLeftLogical128BitLane(v1, 4);
                        v2 = Sse2.ShiftRightLogical128BitLane(v2, 4);

                        v1 = Ssse3.AlignRight(v2, v1, 8);

                        ref Vector128<byte> vd = ref Unsafe.Add(ref destBase, i);

                        vd = v0;
                        Unsafe.Add(ref vd, 1) = v1;
                        Unsafe.Add(ref vd, 2) = v3;
                    }
                }
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            private static void Pad3Shuffle4(
                ReadOnlySpan<byte> source,
                Span<byte> dest,
                byte control)
            {
                if (Ssse3.IsSupported)
                {
                    ref byte vmaskBase = ref MemoryMarshal.GetReference(ShuffleMaskPad4Nx16);
                    Vector128<byte> vmask = Unsafe.As<byte, Vector128<byte>>(ref vmaskBase);
                    Vector128<byte> vfill = Vector128.Create(0xff000000ff000000ul).AsByte();

                    Span<byte> bytes = stackalloc byte[Vector128<byte>.Count];
                    Shuffle.MmShuffleSpan(ref bytes, control);
                    Vector128<byte> vshuffle = Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(bytes));

                    ref Vector128<byte> sourceBase =
                        ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(source));

                    ref Vector128<byte> destBase =
                        ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(dest));

                    int n = source.Length / Vector128<byte>.Count;

                    for (int i = 0, j = 0; i < n; i += 3, j += 4)
                    {
                        ref Vector128<byte> v0 = ref Unsafe.Add(ref sourceBase, i);
                        Vector128<byte> v1 = Unsafe.Add(ref v0, 1);
                        Vector128<byte> v2 = Unsafe.Add(ref v0, 2);
                        Vector128<byte> v3 = Sse2.ShiftRightLogical128BitLane(v2, 4);

                        v2 = Ssse3.AlignRight(v2, v1, 8);
                        v1 = Ssse3.AlignRight(v1, v0, 12);

                        ref Vector128<byte> vd = ref Unsafe.Add(ref destBase, j);

                        vd = Ssse3.Shuffle(Sse2.Or(Ssse3.Shuffle(v0, vmask), vfill), vshuffle);
                        Unsafe.Add(ref vd, 1) = Ssse3.Shuffle(Sse2.Or(Ssse3.Shuffle(v1, vmask), vfill), vshuffle);
                        Unsafe.Add(ref vd, 2) = Ssse3.Shuffle(Sse2.Or(Ssse3.Shuffle(v2, vmask), vfill), vshuffle);
                        Unsafe.Add(ref vd, 3) = Ssse3.Shuffle(Sse2.Or(Ssse3.Shuffle(v3, vmask), vfill), vshuffle);
                    }
                }
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            private static void Shuffle4Slice3(
                ReadOnlySpan<byte> source,
                Span<byte> dest,
                byte control)
            {
                if (Ssse3.IsSupported)
                {
                    ref byte vmaskoBase = ref MemoryMarshal.GetReference(ShuffleMaskSlice4Nx16);
                    Vector128<byte> vmasko = Unsafe.As<byte, Vector128<byte>>(ref vmaskoBase);
                    Vector128<byte> vmaske = Ssse3.AlignRight(vmasko, vmasko, 12);

                    Span<byte> bytes = stackalloc byte[Vector128<byte>.Count];
                    Shuffle.MmShuffleSpan(ref bytes, control);
                    Vector128<byte> vshuffle = Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(bytes));

                    ref Vector128<byte> sourceBase =
                        ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(source));

                    ref Vector128<byte> destBase =
                        ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(dest));

                    int n = source.Length / Vector128<byte>.Count;

                    for (int i = 0, j = 0; i < n; i += 4, j += 3)
                    {
                        ref Vector128<byte> vs = ref Unsafe.Add(ref sourceBase, i);

                        Vector128<byte> v0 = vs;
                        Vector128<byte> v1 = Unsafe.Add(ref vs, 1);
                        Vector128<byte> v2 = Unsafe.Add(ref vs, 2);
                        Vector128<byte> v3 = Unsafe.Add(ref vs, 3);

                        v0 = Ssse3.Shuffle(Ssse3.Shuffle(v0, vshuffle), vmaske);
                        v1 = Ssse3.Shuffle(Ssse3.Shuffle(v1, vshuffle), vmasko);
                        v2 = Ssse3.Shuffle(Ssse3.Shuffle(v2, vshuffle), vmaske);
                        v3 = Ssse3.Shuffle(Ssse3.Shuffle(v3, vshuffle), vmasko);

                        v0 = Ssse3.AlignRight(v1, v0, 4);
                        v3 = Ssse3.AlignRight(v3, v2, 12);

                        v1 = Sse2.ShiftLeftLogical128BitLane(v1, 4);
                        v2 = Sse2.ShiftRightLogical128BitLane(v2, 4);

                        v1 = Ssse3.AlignRight(v2, v1, 8);

                        ref Vector128<byte> vd = ref Unsafe.Add(ref destBase, j);

                        vd = v0;
                        Unsafe.Add(ref vd, 1) = v1;
                        Unsafe.Add(ref vd, 2) = v3;
                    }
                }
            }

            /// <summary>
            /// Performs a multiplication and an addition of the <see cref="Vector256{T}"/>.
            /// </summary>
            /// <param name="va">The vector to add to the intermediate result.</param>
            /// <param name="vm0">The first vector to multiply.</param>
            /// <param name="vm1">The second vector to multiply.</param>
            /// <returns>The <see cref="Vector256{T}"/>.</returns>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static Vector256<float> MultiplyAdd(
                in Vector256<float> va,
                in Vector256<float> vm0,
                in Vector256<float> vm1)
            {
                if (Fma.IsSupported)
                {
                    return Fma.MultiplyAdd(vm1, vm0, va);
                }
                else
                {
                    return Avx.Add(Avx.Multiply(vm0, vm1), va);
                }
            }

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
                        remainder = Numerics.ModuloP2(source.Length, Vector256<byte>.Count);
                    }
                    else
                    {
                        remainder = Numerics.ModuloP2(source.Length, Vector128<byte>.Count);
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
                        remainder = Numerics.ModuloP2(source.Length, Vector256<byte>.Count);
                    }
                    else
                    {
                        remainder = Numerics.ModuloP2(source.Length, Vector128<byte>.Count);
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

            internal static void PackFromRgbPlanesAvx2Reduce(
                ref ReadOnlySpan<byte> redChannel,
                ref ReadOnlySpan<byte> greenChannel,
                ref ReadOnlySpan<byte> blueChannel,
                ref Span<Rgb24> destination)
            {
                ref Vector256<byte> rBase = ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(redChannel));
                ref Vector256<byte> gBase = ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(greenChannel));
                ref Vector256<byte> bBase = ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(blueChannel));
                ref byte dBase = ref Unsafe.As<Rgb24, byte>(ref MemoryMarshal.GetReference(destination));

                int count = redChannel.Length / Vector256<byte>.Count;

                ref byte control1Bytes = ref MemoryMarshal.GetReference(SimdUtils.HwIntrinsics.PermuteMaskEvenOdd8x32);
                Vector256<uint> control1 = Unsafe.As<byte, Vector256<uint>>(ref control1Bytes);

                ref byte control2Bytes = ref MemoryMarshal.GetReference(PermuteMaskShiftAlpha8x32);
                Vector256<uint> control2 = Unsafe.As<byte, Vector256<uint>>(ref control2Bytes);

                Vector256<byte> a = Vector256.Create((byte)255);

                Vector256<byte> shuffleAlpha = Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(ShuffleMaskShiftAlpha));

                for (int i = 0; i < count; i++)
                {
                    Vector256<byte> r0 = Unsafe.Add(ref rBase, i);
                    Vector256<byte> g0 = Unsafe.Add(ref gBase, i);
                    Vector256<byte> b0 = Unsafe.Add(ref bBase, i);

                    r0 = Avx2.PermuteVar8x32(r0.AsUInt32(), control1).AsByte();
                    g0 = Avx2.PermuteVar8x32(g0.AsUInt32(), control1).AsByte();
                    b0 = Avx2.PermuteVar8x32(b0.AsUInt32(), control1).AsByte();

                    Vector256<byte> rg = Avx2.UnpackLow(r0, g0);
                    Vector256<byte> b1 = Avx2.UnpackLow(b0, a);

                    Vector256<byte> rgb1 = Avx2.UnpackLow(rg.AsUInt16(), b1.AsUInt16()).AsByte();
                    Vector256<byte> rgb2 = Avx2.UnpackHigh(rg.AsUInt16(), b1.AsUInt16()).AsByte();

                    rg = Avx2.UnpackHigh(r0, g0);
                    b1 = Avx2.UnpackHigh(b0, a);

                    Vector256<byte> rgb3 = Avx2.UnpackLow(rg.AsUInt16(), b1.AsUInt16()).AsByte();
                    Vector256<byte> rgb4 = Avx2.UnpackHigh(rg.AsUInt16(), b1.AsUInt16()).AsByte();

                    rgb1 = Avx2.Shuffle(rgb1, shuffleAlpha);
                    rgb2 = Avx2.Shuffle(rgb2, shuffleAlpha);
                    rgb3 = Avx2.Shuffle(rgb3, shuffleAlpha);
                    rgb4 = Avx2.Shuffle(rgb4, shuffleAlpha);

                    rgb1 = Avx2.PermuteVar8x32(rgb1.AsUInt32(), control2).AsByte();
                    rgb2 = Avx2.PermuteVar8x32(rgb2.AsUInt32(), control2).AsByte();
                    rgb3 = Avx2.PermuteVar8x32(rgb3.AsUInt32(), control2).AsByte();
                    rgb4 = Avx2.PermuteVar8x32(rgb4.AsUInt32(), control2).AsByte();

                    ref byte d1 = ref Unsafe.Add(ref dBase, 24 * 4 * i);
                    ref byte d2 = ref Unsafe.Add(ref d1, 24);
                    ref byte d3 = ref Unsafe.Add(ref d2, 24);
                    ref byte d4 = ref Unsafe.Add(ref d3, 24);

                    Unsafe.As<byte, Vector256<byte>>(ref d1) = rgb1;
                    Unsafe.As<byte, Vector256<byte>>(ref d2) = rgb2;
                    Unsafe.As<byte, Vector256<byte>>(ref d3) = rgb3;
                    Unsafe.As<byte, Vector256<byte>>(ref d4) = rgb4;
                }

                int slice = count * Vector256<byte>.Count;
                redChannel = redChannel.Slice(slice);
                greenChannel = greenChannel.Slice(slice);
                blueChannel = blueChannel.Slice(slice);
                destination = destination.Slice(slice);
            }

            internal static void PackFromRgbPlanesAvx2Reduce(
                ref ReadOnlySpan<byte> redChannel,
                ref ReadOnlySpan<byte> greenChannel,
                ref ReadOnlySpan<byte> blueChannel,
                ref Span<Rgba32> destination)
            {
                ref Vector256<byte> rBase = ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(redChannel));
                ref Vector256<byte> gBase = ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(greenChannel));
                ref Vector256<byte> bBase = ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(blueChannel));
                ref Vector256<byte> dBase = ref Unsafe.As<Rgba32, Vector256<byte>>(ref MemoryMarshal.GetReference(destination));

                int count = redChannel.Length / Vector256<byte>.Count;

                ref byte control1Bytes = ref MemoryMarshal.GetReference(SimdUtils.HwIntrinsics.PermuteMaskEvenOdd8x32);
                Vector256<uint> control1 = Unsafe.As<byte, Vector256<uint>>(ref control1Bytes);

                ref byte control2Bytes = ref MemoryMarshal.GetReference(PermuteMaskShiftAlpha8x32);
                Vector256<uint> control2 = Unsafe.As<byte, Vector256<uint>>(ref control2Bytes);

                Vector256<byte> a = Vector256.Create((byte)255);

                Vector256<byte> shuffleAlpha = Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(ShuffleMaskShiftAlpha));

                for (int i = 0; i < count; i++)
                {
                    Vector256<byte> r0 = Unsafe.Add(ref rBase, i);
                    Vector256<byte> g0 = Unsafe.Add(ref gBase, i);
                    Vector256<byte> b0 = Unsafe.Add(ref bBase, i);

                    r0 = Avx2.PermuteVar8x32(r0.AsUInt32(), control1).AsByte();
                    g0 = Avx2.PermuteVar8x32(g0.AsUInt32(), control1).AsByte();
                    b0 = Avx2.PermuteVar8x32(b0.AsUInt32(), control1).AsByte();

                    Vector256<byte> rg = Avx2.UnpackLow(r0, g0);
                    Vector256<byte> b1 = Avx2.UnpackLow(b0, a);

                    Vector256<byte> rgb1 = Avx2.UnpackLow(rg.AsUInt16(), b1.AsUInt16()).AsByte();
                    Vector256<byte> rgb2 = Avx2.UnpackHigh(rg.AsUInt16(), b1.AsUInt16()).AsByte();

                    rg = Avx2.UnpackHigh(r0, g0);
                    b1 = Avx2.UnpackHigh(b0, a);

                    Vector256<byte> rgb3 = Avx2.UnpackLow(rg.AsUInt16(), b1.AsUInt16()).AsByte();
                    Vector256<byte> rgb4 = Avx2.UnpackHigh(rg.AsUInt16(), b1.AsUInt16()).AsByte();

                    ref Vector256<byte> d0 = ref Unsafe.Add(ref dBase, i * 4);
                    d0 = rgb1;
                    Unsafe.Add(ref d0, 1) = rgb2;
                    Unsafe.Add(ref d0, 2) = rgb3;
                    Unsafe.Add(ref d0, 3) = rgb4;
                }

                int slice = count * Vector256<byte>.Count;
                redChannel = redChannel.Slice(slice);
                greenChannel = greenChannel.Slice(slice);
                blueChannel = blueChannel.Slice(slice);
                destination = destination.Slice(slice);
            }
        }
    }
}
#endif
