// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp;

internal static partial class SimdUtils
{
    public static partial class HwIntrinsics
    {
#pragma warning disable SA1117 // Parameters should be on same line or separate lines
#pragma warning disable SA1137 // Elements should have the same indentation
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<uint> PermuteMaskSwitchInnerDWords8x32()
            => Vector256.Create(0, 0, 0, 0, 1, 0, 0, 0, 4, 0, 0, 0, 5, 0, 0, 0, 2, 0, 0, 0, 3, 0, 0, 0, 6, 0, 0, 0, 7, 0, 0, 0).AsUInt32();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<byte> ShuffleMaskPad4Nx16() => Vector128.Create(0, 1, 2, 0x80, 3, 4, 5, 0x80, 6, 7, 8, 0x80, 9, 10, 11, 0x80);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<byte> ShuffleMaskSlice4Nx16() => Vector128.Create(0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14, 0x80, 0x80, 0x80, 0x80);

#pragma warning restore SA1137 // Elements should have the same indentation
#pragma warning restore SA1117 // Parameters should be on same line or separate lines

        /// <summary>
        /// Shuffle single-precision (32-bit) floating-point elements in <paramref name="source"/>
        /// using the control and store the results in <paramref name="destination"/>.
        /// </summary>
        /// <param name="source">The source span of floats.</param>
        /// <param name="destination">The destination span of floats.</param>
        /// <param name="control">The byte control.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Shuffle4Reduce(
            ref ReadOnlySpan<float> source,
            ref Span<float> destination,
            [ConstantExpected] byte control)
        {
            if (Vector512.IsHardwareAccelerated ||
                Vector256.IsHardwareAccelerated ||
                Vector128.IsHardwareAccelerated)
            {
                int remainder = 0;
                if (Vector512.IsHardwareAccelerated)
                {
                    remainder = Numerics.ModuloP2(source.Length, Vector512<float>.Count);
                }
                else if (Vector256.IsHardwareAccelerated)
                {
                    remainder = Numerics.ModuloP2(source.Length, Vector256<float>.Count);
                }
                else if (Vector128.IsHardwareAccelerated)
                {
                    remainder = Numerics.ModuloP2(source.Length, Vector128<float>.Count);
                }

                int adjustedCount = source.Length - remainder;

                if (adjustedCount > 0)
                {
                    Shuffle4(
                        source[..adjustedCount],
                        destination[..adjustedCount],
                        control);

                    source = source[adjustedCount..];
                    destination = destination[adjustedCount..];
                }
            }
        }

        /// <summary>
        /// Shuffle 8-bit integers <paramref name="source"/>
        /// using the control and store the results in <paramref name="destination"/>.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="destination">The destination span of bytes.</param>
        /// <param name="control">The byte control.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Shuffle4Reduce(
            ref ReadOnlySpan<byte> source,
            ref Span<byte> destination,
            [ConstantExpected] byte control)
        {
            if (Vector512.IsHardwareAccelerated ||
                Vector256.IsHardwareAccelerated ||
                Vector128.IsHardwareAccelerated)
            {
                int remainder = 0;
                if (Vector512.IsHardwareAccelerated)
                {
                    remainder = Numerics.ModuloP2(source.Length, Vector512<byte>.Count);
                }
                else if (Vector256.IsHardwareAccelerated)
                {
                    remainder = Numerics.ModuloP2(source.Length, Vector256<byte>.Count);
                }
                else if (Vector128.IsHardwareAccelerated)
                {
                    remainder = Numerics.ModuloP2(source.Length, Vector128<byte>.Count);
                }

                int adjustedCount = source.Length - remainder;

                if (adjustedCount > 0)
                {
                    Shuffle4(
                        source[..adjustedCount],
                        destination[..adjustedCount],
                        control);

                    source = source[adjustedCount..];
                    destination = destination[adjustedCount..];
                }
            }
        }

        /// <summary>
        /// Shuffles 8-bit integer triplets in <paramref name="source"/>
        /// using the control and store the results in <paramref name="destination"/>.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="destination">The destination span of bytes.</param>
        /// <param name="control">The byte control.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Shuffle3Reduce(
            ref ReadOnlySpan<byte> source,
            ref Span<byte> destination,
            [ConstantExpected] byte control)
        {
            if (Vector128.IsHardwareAccelerated)
            {
                int remainder = source.Length % (Vector128<byte>.Count * 3);

                int adjustedCount = source.Length - remainder;

                if (adjustedCount > 0)
                {
                    Shuffle3(
                        source[..adjustedCount],
                        destination[..adjustedCount],
                        control);

                    source = source[adjustedCount..];
                    destination = destination[adjustedCount..];
                }
            }
        }

        /// <summary>
        /// Pads then shuffles 8-bit integers in <paramref name="source"/>
        /// using the control and store the results in <paramref name="destination"/>.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="destination">The destination span of bytes.</param>
        /// <param name="control">The byte control.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Pad3Shuffle4Reduce(
            ref ReadOnlySpan<byte> source,
            ref Span<byte> destination,
            [ConstantExpected] byte control)
        {
            if (Vector128.IsHardwareAccelerated)
            {
                int remainder = source.Length % (Vector128<byte>.Count * 3);

                int sourceCount = source.Length - remainder;
                int destinationCount = (int)((uint)sourceCount * 4 / 3);

                if (sourceCount > 0)
                {
                    Pad3Shuffle4(
                        source[..sourceCount],
                        destination[..destinationCount],
                        control);

                    source = source[sourceCount..];
                    destination = destination[destinationCount..];
                }
            }
        }

        /// <summary>
        /// Shuffles then slices 8-bit integers in <paramref name="source"/>
        /// using the control and store the results in <paramref name="destination"/>.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="destination">The destination span of bytes.</param>
        /// <param name="control">The byte control.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Shuffle4Slice3Reduce(
            ref ReadOnlySpan<byte> source,
            ref Span<byte> destination,
            [ConstantExpected] byte control)
        {
            if (Vector128.IsHardwareAccelerated)
            {
                int remainder = source.Length & ((Vector128<byte>.Count * 4) - 1);    // bit-hack for modulo

                int sourceCount = source.Length - remainder;
                int destinationCount = (int)((uint)sourceCount * 3 / 4);

                if (sourceCount > 0)
                {
                    Shuffle4Slice3(
                        source[..sourceCount],
                        destination[..destinationCount],
                        control);

                    source = source[sourceCount..];
                    destination = destination[destinationCount..];
                }
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void Shuffle4(
            ReadOnlySpan<float> source,
            Span<float> destination,
            [ConstantExpected] byte control)
        {
            if (Vector512.IsHardwareAccelerated)
            {
                ref Vector512<float> sourceBase = ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(source));
                ref Vector512<float> destinationBase = ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(destination));

                nuint n = (uint)destination.Length / (uint)Vector512<float>.Count;
                nuint m = Numerics.Modulo4(n);
                nuint u = n - m;

                for (nuint i = 0; i < u; i += 4)
                {
                    ref Vector512<float> vs0 = ref Unsafe.Add(ref sourceBase, i);
                    ref Vector512<float> vd0 = ref Unsafe.Add(ref destinationBase, i);

                    vd0 = Vector512_.ShuffleNative(vs0, control);
                    Unsafe.Add(ref vd0, (nuint)1) = Vector512_.ShuffleNative(Unsafe.Add(ref vs0, (nuint)1), control);
                    Unsafe.Add(ref vd0, (nuint)2) = Vector512_.ShuffleNative(Unsafe.Add(ref vs0, (nuint)2), control);
                    Unsafe.Add(ref vd0, (nuint)3) = Vector512_.ShuffleNative(Unsafe.Add(ref vs0, (nuint)3), control);
                }

                if (m > 0)
                {
                    for (nuint i = u; i < n; i++)
                    {
                        Unsafe.Add(ref destinationBase, i) = Vector512_.ShuffleNative(Unsafe.Add(ref sourceBase, i), control);
                    }
                }
            }
            else if (Vector256.IsHardwareAccelerated)
            {
                ref Vector256<float> sourceBase = ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(source));
                ref Vector256<float> destinationBase = ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(destination));

                nuint n = (uint)destination.Length / (uint)Vector256<float>.Count;
                nuint m = Numerics.Modulo4(n);
                nuint u = n - m;

                for (nuint i = 0; i < u; i += 4)
                {
                    ref Vector256<float> vs0 = ref Unsafe.Add(ref sourceBase, i);
                    ref Vector256<float> vd0 = ref Unsafe.Add(ref destinationBase, i);

                    vd0 = Vector256_.ShuffleNative(vs0, control);
                    Unsafe.Add(ref vd0, (nuint)1) = Vector256_.ShuffleNative(Unsafe.Add(ref vs0, (nuint)1), control);
                    Unsafe.Add(ref vd0, (nuint)2) = Vector256_.ShuffleNative(Unsafe.Add(ref vs0, (nuint)2), control);
                    Unsafe.Add(ref vd0, (nuint)3) = Vector256_.ShuffleNative(Unsafe.Add(ref vs0, (nuint)3), control);
                }

                if (m > 0)
                {
                    for (nuint i = u; i < n; i++)
                    {
                        Unsafe.Add(ref destinationBase, i) = Vector256_.ShuffleNative(Unsafe.Add(ref sourceBase, i), control);
                    }
                }
            }
            else if (Vector128.IsHardwareAccelerated)
            {
                ref Vector128<float> sourceBase = ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(source));
                ref Vector128<float> destinationBase = ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(destination));

                nuint n = (uint)destination.Length / (uint)Vector128<float>.Count;
                nuint m = Numerics.Modulo4(n);
                nuint u = n - m;

                for (nuint i = 0; i < u; i += 4)
                {
                    ref Vector128<float> vs0 = ref Unsafe.Add(ref sourceBase, i);
                    ref Vector128<float> vd0 = ref Unsafe.Add(ref destinationBase, i);

                    vd0 = Vector128_.ShuffleNative(vs0, control);
                    Unsafe.Add(ref vd0, (nuint)1) = Vector128_.ShuffleNative(Unsafe.Add(ref vs0, (nuint)1), control);
                    Unsafe.Add(ref vd0, (nuint)2) = Vector128_.ShuffleNative(Unsafe.Add(ref vs0, (nuint)2), control);
                    Unsafe.Add(ref vd0, (nuint)3) = Vector128_.ShuffleNative(Unsafe.Add(ref vs0, (nuint)3), control);
                }

                if (m > 0)
                {
                    for (nuint i = u; i < n; i++)
                    {
                        Unsafe.Add(ref destinationBase, i) = Vector128_.ShuffleNative(Unsafe.Add(ref sourceBase, i), control);
                    }
                }
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void Shuffle4(
            ReadOnlySpan<byte> source,
            Span<byte> destination,
            [ConstantExpected] byte control)
        {
            if (Vector512.IsHardwareAccelerated)
            {
                Span<byte> temp = stackalloc byte[Vector512<byte>.Count];
                Shuffle.MMShuffleSpan(ref temp, control);
                Vector512<byte> mask = Unsafe.As<byte, Vector512<byte>>(ref MemoryMarshal.GetReference(temp));

                ref Vector512<byte> sourceBase = ref Unsafe.As<byte, Vector512<byte>>(ref MemoryMarshal.GetReference(source));
                ref Vector512<byte> destinationBase = ref Unsafe.As<byte, Vector512<byte>>(ref MemoryMarshal.GetReference(destination));

                nuint n = (uint)destination.Length / (uint)Vector512<byte>.Count;
                nuint m = Numerics.Modulo4(n);
                nuint u = n - m;

                for (nuint i = 0; i < u; i += 4)
                {
                    ref Vector512<byte> vs0 = ref Unsafe.Add(ref sourceBase, i);
                    ref Vector512<byte> vd0 = ref Unsafe.Add(ref destinationBase, i);

                    vd0 = Vector512.ShuffleNative(vs0, mask);
                    Unsafe.Add(ref vd0, (nuint)1) = Vector512.ShuffleNative(Unsafe.Add(ref vs0, (nuint)1), mask);
                    Unsafe.Add(ref vd0, (nuint)2) = Vector512.ShuffleNative(Unsafe.Add(ref vs0, (nuint)2), mask);
                    Unsafe.Add(ref vd0, (nuint)3) = Vector512.ShuffleNative(Unsafe.Add(ref vs0, (nuint)3), mask);
                }

                if (m > 0)
                {
                    for (nuint i = u; i < n; i++)
                    {
                        Unsafe.Add(ref destinationBase, i) = Vector512.ShuffleNative(Unsafe.Add(ref sourceBase, i), mask);
                    }
                }
            }
            else if (Vector256.IsHardwareAccelerated)
            {
                // ShufflePerLane performs per-128-bit-lane shuffling using Avx2.Shuffle (vpshufb).
                // MMShuffleSpan generates indices in the range [0, 31] and never sets bit 7 in any byte,
                // so the shuffle will not zero elements. Because vpshufb uses only the low 4 bits (b[i] & 0x0F)
                // for indexing within each lane, and ignores the upper bits unless bit 7 is set,
                // this usage is guaranteed to remain within-lane and non-zeroing.
                Span<byte> temp = stackalloc byte[Vector256<byte>.Count];
                Shuffle.MMShuffleSpan(ref temp, control);
                Vector256<byte> mask = Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(temp));

                ref Vector256<byte> sourceBase = ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(source));
                ref Vector256<byte> destinationBase = ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(destination));

                nuint n = (uint)destination.Length / (uint)Vector256<byte>.Count;
                nuint m = Numerics.Modulo4(n);
                nuint u = n - m;

                for (nuint i = 0; i < u; i += 4)
                {
                    ref Vector256<byte> vs0 = ref Unsafe.Add(ref sourceBase, i);
                    ref Vector256<byte> vd0 = ref Unsafe.Add(ref destinationBase, i);

                    vd0 = Vector256_.ShufflePerLane(vs0, mask);
                    Unsafe.Add(ref vd0, (nuint)1) = Vector256_.ShufflePerLane(Unsafe.Add(ref vs0, (nuint)1), mask);
                    Unsafe.Add(ref vd0, (nuint)2) = Vector256_.ShufflePerLane(Unsafe.Add(ref vs0, (nuint)2), mask);
                    Unsafe.Add(ref vd0, (nuint)3) = Vector256_.ShufflePerLane(Unsafe.Add(ref vs0, (nuint)3), mask);
                }

                if (m > 0)
                {
                    for (nuint i = u; i < n; i++)
                    {
                        Unsafe.Add(ref destinationBase, i) = Vector256_.ShufflePerLane(Unsafe.Add(ref sourceBase, i), mask);
                    }
                }
            }
            else if (Vector128.IsHardwareAccelerated)
            {
                Span<byte> temp = stackalloc byte[Vector128<byte>.Count];
                Shuffle.MMShuffleSpan(ref temp, control);
                Vector128<byte> mask = Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(temp));

                ref Vector128<byte> sourceBase = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(source));
                ref Vector128<byte> destinationBase = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(destination));

                nuint n = (uint)destination.Length / (uint)Vector128<byte>.Count;
                nuint m = Numerics.Modulo4(n);
                nuint u = n - m;

                for (nuint i = 0; i < u; i += 4)
                {
                    ref Vector128<byte> vs0 = ref Unsafe.Add(ref sourceBase, i);
                    ref Vector128<byte> vd0 = ref Unsafe.Add(ref destinationBase, i);

                    vd0 = Vector128.ShuffleNative(vs0, mask);
                    Unsafe.Add(ref vd0, (nuint)1) = Vector128.ShuffleNative(Unsafe.Add(ref vs0, (nuint)1), mask);
                    Unsafe.Add(ref vd0, (nuint)2) = Vector128.ShuffleNative(Unsafe.Add(ref vs0, (nuint)2), mask);
                    Unsafe.Add(ref vd0, (nuint)3) = Vector128.ShuffleNative(Unsafe.Add(ref vs0, (nuint)3), mask);
                }

                if (m > 0)
                {
                    for (nuint i = u; i < n; i++)
                    {
                        Unsafe.Add(ref destinationBase, i) = Vector128.ShuffleNative(Unsafe.Add(ref sourceBase, i), mask);
                    }
                }
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void Shuffle3(
            ReadOnlySpan<byte> source,
            Span<byte> destination,
            [ConstantExpected] byte control)
        {
            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<byte> maskPad4Nx16 = ShuffleMaskPad4Nx16();
                Vector128<byte> maskSlice4Nx16 = ShuffleMaskSlice4Nx16();
                Vector128<byte> maskE = Vector128_.AlignRight(maskSlice4Nx16, maskSlice4Nx16, 12);

                Span<byte> bytes = stackalloc byte[Vector128<byte>.Count];
                Shuffle.MMShuffleSpan(ref bytes, control);
                Vector128<byte> mask = Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(bytes));

                ref Vector128<byte> sourceBase = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(source));
                ref Vector128<byte> destinationBase = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(destination));

                nuint n = source.Vector128Count<byte>();

                for (nuint i = 0; i < n; i += 3)
                {
                    ref Vector128<byte> vs = ref Unsafe.Add(ref sourceBase, i);

                    Vector128<byte> v0 = vs;
                    Vector128<byte> v1 = Unsafe.Add(ref vs, (nuint)1);
                    Vector128<byte> v2 = Unsafe.Add(ref vs, (nuint)2);
                    Vector128<byte> v3 = Vector128_.ShiftRightBytesInVector(v2, 4);

                    v2 = Vector128_.AlignRight(v2, v1, 8);
                    v1 = Vector128_.AlignRight(v1, v0, 12);

                    v0 = Vector128.ShuffleNative(Vector128.ShuffleNative(v0, maskPad4Nx16), mask);
                    v1 = Vector128.ShuffleNative(Vector128.ShuffleNative(v1, maskPad4Nx16), mask);
                    v2 = Vector128.ShuffleNative(Vector128.ShuffleNative(v2, maskPad4Nx16), mask);
                    v3 = Vector128.ShuffleNative(Vector128.ShuffleNative(v3, maskPad4Nx16), mask);

                    v0 = Vector128.ShuffleNative(v0, maskE);
                    v1 = Vector128.ShuffleNative(v1, maskSlice4Nx16);
                    v2 = Vector128.ShuffleNative(v2, maskE);
                    v3 = Vector128.ShuffleNative(v3, maskSlice4Nx16);

                    v0 = Vector128_.AlignRight(v1, v0, 4);
                    v3 = Vector128_.AlignRight(v3, v2, 12);

                    v1 = Vector128_.ShiftLeftBytesInVector(v1, 4);
                    v2 = Vector128_.ShiftRightBytesInVector(v2, 4);

                    v1 = Vector128_.AlignRight(v2, v1, 8);

                    ref Vector128<byte> vd = ref Unsafe.Add(ref destinationBase, i);

                    vd = v0;
                    Unsafe.Add(ref vd, (nuint)1) = v1;
                    Unsafe.Add(ref vd, (nuint)2) = v3;
                }
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void Pad3Shuffle4(
            ReadOnlySpan<byte> source,
            Span<byte> destination,
            [ConstantExpected] byte control)
        {
            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<byte> maskPad4Nx16 = ShuffleMaskPad4Nx16();
                Vector128<byte> fill = Vector128.Create(0xff000000ff000000ul).AsByte();

                Span<byte> temp = stackalloc byte[Vector128<byte>.Count];
                Shuffle.MMShuffleSpan(ref temp, control);
                Vector128<byte> mask = Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(temp));

                ref Vector128<byte> sourceBase =
                    ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(source));

                ref Vector128<byte> destinationBase =
                    ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(destination));

                nuint n = source.Vector128Count<byte>();

                for (nuint i = 0, j = 0; i < n; i += 3, j += 4)
                {
                    ref Vector128<byte> v0 = ref Unsafe.Add(ref sourceBase, i);
                    Vector128<byte> v1 = Unsafe.Add(ref v0, 1);
                    Vector128<byte> v2 = Unsafe.Add(ref v0, 2);
                    Vector128<byte> v3 = Vector128_.ShiftRightBytesInVector(v2, 4);

                    v2 = Vector128_.AlignRight(v2, v1, 8);
                    v1 = Vector128_.AlignRight(v1, v0, 12);

                    ref Vector128<byte> vd = ref Unsafe.Add(ref destinationBase, j);

                    vd = Vector128.ShuffleNative(Vector128.ShuffleNative(v0, maskPad4Nx16) | fill, mask);
                    Unsafe.Add(ref vd, 1) = Vector128.ShuffleNative(Vector128.ShuffleNative(v1, maskPad4Nx16) | fill, mask);
                    Unsafe.Add(ref vd, 2) = Vector128.ShuffleNative(Vector128.ShuffleNative(v2, maskPad4Nx16) | fill, mask);
                    Unsafe.Add(ref vd, 3) = Vector128.ShuffleNative(Vector128.ShuffleNative(v3, maskPad4Nx16) | fill, mask);
                }
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void Shuffle4Slice3(
            ReadOnlySpan<byte> source,
            Span<byte> destination,
            [ConstantExpected] byte control)
        {
            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<byte> maskSlice4Nx16 = ShuffleMaskSlice4Nx16();
                Vector128<byte> maskE = Vector128_.AlignRight(maskSlice4Nx16, maskSlice4Nx16, 12);

                Span<byte> temp = stackalloc byte[Vector128<byte>.Count];
                Shuffle.MMShuffleSpan(ref temp, control);
                Vector128<byte> mask = Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(temp));

                ref Vector128<byte> sourceBase =
                    ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(source));

                ref Vector128<byte> destinationBase =
                    ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(destination));

                nuint n = source.Vector128Count<byte>();

                for (nuint i = 0, j = 0; i < n; i += 4, j += 3)
                {
                    ref Vector128<byte> vs = ref Unsafe.Add(ref sourceBase, i);

                    Vector128<byte> v0 = vs;
                    Vector128<byte> v1 = Unsafe.Add(ref vs, 1);
                    Vector128<byte> v2 = Unsafe.Add(ref vs, 2);
                    Vector128<byte> v3 = Unsafe.Add(ref vs, 3);

                    v0 = Vector128.ShuffleNative(Vector128.ShuffleNative(v0, mask), maskE);
                    v1 = Vector128.ShuffleNative(Vector128.ShuffleNative(v1, mask), maskSlice4Nx16);
                    v2 = Vector128.ShuffleNative(Vector128.ShuffleNative(v2, mask), maskE);
                    v3 = Vector128.ShuffleNative(Vector128.ShuffleNative(v3, mask), maskSlice4Nx16);

                    v0 = Vector128_.AlignRight(v1, v0, 4);
                    v3 = Vector128_.AlignRight(v3, v2, 12);

                    v1 = Vector128_.ShiftLeftBytesInVector(v1, 4);
                    v2 = Vector128_.ShiftRightBytesInVector(v2, 4);

                    v1 = Vector128_.AlignRight(v2, v1, 8);

                    ref Vector128<byte> vd = ref Unsafe.Add(ref destinationBase, j);

                    vd = v0;
                    Unsafe.Add(ref vd, 1) = v1;
                    Unsafe.Add(ref vd, 2) = v3;
                }
            }
        }

        /// <summary>
        /// Blend packed 8-bit integers from <paramref name="left"/> and <paramref name="right"/> using <paramref name="mask"/>.
        /// The high bit of each corresponding <paramref name="mask"/> byte determines the selection.
        /// If the high bit is set the element of <paramref name="left"/> is selected.
        /// The element of <paramref name="right"/> is selected otherwise.
        /// </summary>
        /// <param name="left">The left vector.</param>
        /// <param name="right">The right vector.</param>
        /// <param name="mask">The mask vector.</param>
        /// <returns>The <see cref="Vector256{T}"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> BlendVariable(Vector128<byte> left, Vector128<byte> right, Vector128<byte> mask)
        {
            if (Sse41.IsSupported)
            {
                return Sse41.BlendVariable(left, right, mask);
            }
            else if (Sse2.IsSupported)
            {
                return Sse2.Or(Sse2.And(right, mask), Sse2.AndNot(mask, left));
            }

            // Use a signed shift right to create a mask with the sign bit.
            Vector128<short> signedMask = AdvSimd.ShiftRightArithmetic(mask.AsInt16(), 7);
            return AdvSimd.BitwiseSelect(signedMask, right.AsInt16(), left.AsInt16()).AsByte();
        }

        /// <summary>
        /// Blend packed 32-bit unsigned integers from <paramref name="left"/> and <paramref name="right"/> using <paramref name="mask"/>.
        /// The high bit of each corresponding <paramref name="mask"/> byte determines the selection.
        /// If the high bit is set the element of <paramref name="left"/> is selected.
        /// The element of <paramref name="right"/> is selected otherwise.
        /// </summary>
        /// <param name="left">The left vector.</param>
        /// <param name="right">The right vector.</param>
        /// <param name="mask">The mask vector.</param>
        /// <returns>The <see cref="Vector256{T}"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<uint> BlendVariable(Vector128<uint> left, Vector128<uint> right, Vector128<uint> mask)
            => BlendVariable(left.AsByte(), right.AsByte(), mask.AsByte()).AsUInt32();

        /// <summary>
        /// Count the number of leading zero bits in a mask.
        /// Similar in behavior to the x86 instruction LZCNT.
        /// </summary>
        /// <param name="value">The value.</param>
        public static ushort LeadingZeroCount(ushort value)
            => (ushort)(BitOperations.LeadingZeroCount(value) - 16);

        /// <summary>
        /// Count the number of trailing zero bits in an integer value.
        /// Similar in behavior to the x86 instruction TZCNT.
        /// </summary>
        /// <param name="value">The value.</param>
        public static ushort TrailingZeroCount(ushort value)
            => (ushort)(BitOperations.TrailingZeroCount(value << 16) - 16);

        /// <summary>
        /// <see cref="ByteToNormalizedFloat"/> as many elements as possible, slicing them down (keeping the remainder).
        /// </summary>
        /// <param name="source">The source buffer.</param>
        /// <param name="destination">The destination buffer.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        internal static void ByteToNormalizedFloatReduce(
            ref ReadOnlySpan<byte> source,
            ref Span<float> destination)
        {
            DebugGuard.IsTrue(source.Length == destination.Length, nameof(source), "Input spans must be of same length!");

            if (Vector128.IsHardwareAccelerated)
            {
                int remainder;
                if (Vector512.IsHardwareAccelerated)
                {
                    remainder = Numerics.ModuloP2(source.Length, Vector512<byte>.Count);
                }
                else if (Vector256.IsHardwareAccelerated)
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
                    ByteToNormalizedFloat(source[..adjustedCount], destination[..adjustedCount]);

                    source = source[adjustedCount..];
                    destination = destination[adjustedCount..];
                }
            }
        }

        /// <summary>
        /// Implementation <see cref="SimdUtils.ByteToNormalizedFloat"/>, which is faster on new RyuJIT runtime.
        /// </summary>
        /// <param name="source">The source buffer.</param>
        /// <param name="destination">The destination buffer.</param>
        /// <remarks>
        /// Implementation is based on MagicScaler code:
        /// https://github.com/saucecontrol/PhotoSauce/blob/b5811908041200488aa18fdfd17df5fc457415dc/src/MagicScaler/Magic/Processors/ConvertersFloat.cs#L80-L182
        /// </remarks>
        internal static void ByteToNormalizedFloat(
            ReadOnlySpan<byte> source,
            Span<float> destination)
        {
            const double reciprocal = 1D / byte.MaxValue;
            const float reciprocalHigh = (float)reciprocal;
            const float reciprocalLow = (float)(reciprocal - reciprocalHigh);

            if (Vector512.IsHardwareAccelerated)
            {
                DebugVerifySpanInput(source, destination, Vector512<byte>.Count);

                nuint n = destination.Vector512Count<byte>();

                ref byte sourceBase = ref MemoryMarshal.GetReference(source);
                ref Vector512<float> destinationBase = ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(destination));
                Vector512<float> high = Vector512.Create(reciprocalHigh);
                Vector512<float> low = Vector512.Create(reciprocalLow);

                for (nuint i = 0; i < n; i++)
                {
                    nuint si = (uint)Vector512<byte>.Count * i;

                    // Each 16-byte group widens to sixteen 32-bit lanes.
                    Vector512<int> i0 = Vector512_.Widen(Vector128.LoadUnsafe(ref sourceBase, si));
                    Vector512<int> i1 = Vector512_.Widen(Vector128.LoadUnsafe(ref sourceBase, si + (nuint)Vector512<int>.Count));
                    Vector512<int> i2 = Vector512_.Widen(Vector128.LoadUnsafe(ref sourceBase, si + (nuint)(Vector512<int>.Count * 2)));
                    Vector512<int> i3 = Vector512_.Widen(Vector128.LoadUnsafe(ref sourceBase, si + (nuint)(Vector512<int>.Count * 3)));

                    Vector512<float> f0 = Vector512.ConvertToSingle(i0);
                    Vector512<float> f1 = Vector512.ConvertToSingle(i1);
                    Vector512<float> f2 = Vector512.ConvertToSingle(i2);
                    Vector512<float> f3 = Vector512.ConvertToSingle(i3);

                    // The residual term restores the correctly rounded byte / 255F result without paying for vector division.
                    f0 = Vector512.FusedMultiplyAdd(f0, high, f0 * low);
                    f1 = Vector512.FusedMultiplyAdd(f1, high, f1 * low);
                    f2 = Vector512.FusedMultiplyAdd(f2, high, f2 * low);
                    f3 = Vector512.FusedMultiplyAdd(f3, high, f3 * low);

                    ref Vector512<float> d = ref Unsafe.Add(ref destinationBase, i * 4);

                    d = f0;
                    Unsafe.Add(ref d, 1) = f1;
                    Unsafe.Add(ref d, 2) = f2;
                    Unsafe.Add(ref d, 3) = f3;
                }
            }
            else if (Vector256.IsHardwareAccelerated)
            {
                DebugVerifySpanInput(source, destination, Vector256<byte>.Count);

                nuint n = destination.Vector256Count<byte>();

                ref byte sourceBase = ref MemoryMarshal.GetReference(source);
                ref Vector256<float> destinationBase = ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(destination));
                Vector256<float> high = Vector256.Create(reciprocalHigh);
                Vector256<float> low = Vector256.Create(reciprocalLow);

                for (nuint i = 0; i < n; i++)
                {
                    nuint si = (uint)Vector256<byte>.Count * i;

                    // Each 8-byte group widens to eight 32-bit lanes. The 8-byte loads do not read past the source.
                    Vector256<int> i0 = Vector256_.Widen(Vector64.LoadUnsafe(ref sourceBase, si));
                    Vector256<int> i1 = Vector256_.Widen(Vector64.LoadUnsafe(ref sourceBase, si + (nuint)Vector256<int>.Count));
                    Vector256<int> i2 = Vector256_.Widen(Vector64.LoadUnsafe(ref sourceBase, si + (nuint)(Vector256<int>.Count * 2)));
                    Vector256<int> i3 = Vector256_.Widen(Vector64.LoadUnsafe(ref sourceBase, si + (nuint)(Vector256<int>.Count * 3)));

                    Vector256<float> f0 = Vector256.ConvertToSingle(i0);
                    Vector256<float> f1 = Vector256.ConvertToSingle(i1);
                    Vector256<float> f2 = Vector256.ConvertToSingle(i2);
                    Vector256<float> f3 = Vector256.ConvertToSingle(i3);

                    f0 = Vector256.FusedMultiplyAdd(f0, high, f0 * low);
                    f1 = Vector256.FusedMultiplyAdd(f1, high, f1 * low);
                    f2 = Vector256.FusedMultiplyAdd(f2, high, f2 * low);
                    f3 = Vector256.FusedMultiplyAdd(f3, high, f3 * low);

                    ref Vector256<float> d = ref Unsafe.Add(ref destinationBase, i * 4);

                    d = f0;
                    Unsafe.Add(ref d, 1) = f1;
                    Unsafe.Add(ref d, 2) = f2;
                    Unsafe.Add(ref d, 3) = f3;
                }
            }
            else if (Vector128.IsHardwareAccelerated)
            {
                DebugVerifySpanInput(source, destination, Vector128<byte>.Count);

                nuint n = destination.Vector128Count<byte>();

                ref byte sourceBase = ref MemoryMarshal.GetReference(source);
                ref Vector128<float> destinationBase = ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(destination));

                Vector128<float> high = Vector128.Create(reciprocalHigh);
                Vector128<float> low = Vector128.Create(reciprocalLow);

                for (nuint i = 0; i < n; i++)
                {
                    nuint si = (uint)Vector128<byte>.Count * i;

                    // The 16 bytes widen to four vectors of four 32-bit lanes.
                    Vector128_.Widen(
                        Vector128.LoadUnsafe(ref sourceBase, si),
                        out Vector128<int> i0,
                        out Vector128<int> i1,
                        out Vector128<int> i2,
                        out Vector128<int> i3);

                    Vector128<float> f0 = Vector128.ConvertToSingle(i0);
                    Vector128<float> f1 = Vector128.ConvertToSingle(i1);
                    Vector128<float> f2 = Vector128.ConvertToSingle(i2);
                    Vector128<float> f3 = Vector128.ConvertToSingle(i3);

                    f0 = Vector128.FusedMultiplyAdd(f0, high, f0 * low);
                    f1 = Vector128.FusedMultiplyAdd(f1, high, f1 * low);
                    f2 = Vector128.FusedMultiplyAdd(f2, high, f2 * low);
                    f3 = Vector128.FusedMultiplyAdd(f3, high, f3 * low);

                    ref Vector128<float> d = ref Unsafe.Add(ref destinationBase, i * 4);

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
        /// <param name="source">The source buffer.</param>
        /// <param name="destination">The destination buffer.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        internal static void NormalizedFloatToByteSaturateReduce(
            ref ReadOnlySpan<float> source,
            ref Span<byte> destination)
            => FloatToByteSaturateReduce(ref source, ref destination, byte.MaxValue);

        /// <summary>
        /// Converts as many scaled floating-point values as possible to bytes and retains the unconverted remainder.
        /// </summary>
        /// <param name="source">The source buffer.</param>
        /// <param name="destination">The destination buffer.</param>
        /// <param name="scaleFactor">The factor applied before conversion.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        internal static void FloatToByteSaturateReduce(
            ref ReadOnlySpan<float> source,
            ref Span<byte> destination,
            float scaleFactor)
        {
            DebugGuard.IsTrue(source.Length == destination.Length, nameof(source), "Input spans must be of same length!");

            if (Vector128.IsHardwareAccelerated)
            {
                // One step of FloatToByteSaturate makes one byte vector of the widest accelerated width.
                int remainder;
                if (Vector512.IsHardwareAccelerated)
                {
                    remainder = Numerics.ModuloP2(source.Length, Vector512<byte>.Count);
                }
                else if (Vector256.IsHardwareAccelerated)
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
                    FloatToByteSaturate(
                        source[..adjustedCount],
                        destination[..adjustedCount],
                        scaleFactor);

                    source = source[adjustedCount..];
                    destination = destination[adjustedCount..];
                }
            }
        }

        /// <summary>
        /// Implementation of <see cref="SimdUtils.NormalizedFloatToByteSaturate"/>, which is faster on new .NET runtime.
        /// </summary>
        /// <param name="source">The source buffer.</param>
        /// <param name="destination">The destination buffer.</param>
        /// <remarks>
        /// Implementation is based on MagicScaler code:
        /// https://github.com/saucecontrol/PhotoSauce/blob/b5811908041200488aa18fdfd17df5fc457415dc/src/MagicScaler/Magic/Processors/ConvertersFloat.cs#L541-L622
        /// </remarks>
        internal static void NormalizedFloatToByteSaturate(
            ReadOnlySpan<float> source,
            Span<byte> destination)
            => FloatToByteSaturate(source, destination, byte.MaxValue);

        /// <summary>
        /// Converts scaled floating-point values to bytes.
        /// Each byte is the scaled value plus one half, truncated toward zero and saturated to [0, 255].
        /// </summary>
        /// <param name="source">
        /// The source buffer. Its length is a multiple of the byte vector length of the widest accelerated width.
        /// </param>
        /// <param name="destination">The destination buffer.</param>
        /// <param name="scaleFactor">The factor applied before conversion.</param>
        internal static void FloatToByteSaturate(
            ReadOnlySpan<float> source,
            Span<byte> destination,
            float scaleFactor)
        {
            // The source can hold infinity, NaN, or values outside the int range. The clamp maps NaN to 0 and the
            // other values into [0, 255 / scaleFactor]. Then all values are finite. The vector conversion requires finite values.
            if (Vector512.IsHardwareAccelerated)
            {
                DebugVerifySpanInput(source, destination, Vector512<byte>.Count);

                nuint n = destination.Vector512Count<byte>();

                ref Vector512<float> sourceBase = ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(source));
                ref Vector512<byte> destinationBase = ref Unsafe.As<byte, Vector512<byte>>(ref MemoryMarshal.GetReference(destination));

                Vector512<float> scale = Vector512.Create(scaleFactor);
                Vector512<float> upperBound = Vector512.Create(byte.MaxValue / scaleFactor);

                for (nuint i = 0; i < n; i++)
                {
                    // Four float vectors give the 64 bytes of one destination vector.
                    ref Vector512<float> s = ref Unsafe.Add(ref sourceBase, i * 4);
                    Unsafe.Add(ref destinationBase, i) = FloatToByteSaturate(
                        ColorNumerics.ClampToFinite(s, Vector512<float>.Zero, upperBound),
                        ColorNumerics.ClampToFinite(Unsafe.Add(ref s, 1), Vector512<float>.Zero, upperBound),
                        ColorNumerics.ClampToFinite(Unsafe.Add(ref s, 2), Vector512<float>.Zero, upperBound),
                        ColorNumerics.ClampToFinite(Unsafe.Add(ref s, 3), Vector512<float>.Zero, upperBound),
                        scale);
                }
            }
            else if (Vector256.IsHardwareAccelerated)
            {
                DebugVerifySpanInput(source, destination, Vector256<byte>.Count);

                nuint n = destination.Vector256Count<byte>();

                ref Vector256<float> sourceBase = ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(source));
                ref Vector256<byte> destinationBase = ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(destination));

                Vector256<float> scale = Vector256.Create(scaleFactor);
                Vector256<float> upperBound = Vector256.Create(byte.MaxValue / scaleFactor);

                for (nuint i = 0; i < n; i++)
                {
                    // Four float vectors give the 32 bytes of one destination vector.
                    ref Vector256<float> s = ref Unsafe.Add(ref sourceBase, i * 4);
                    Unsafe.Add(ref destinationBase, i) = FloatToByteSaturate(
                        ColorNumerics.ClampToFinite(s, Vector256<float>.Zero, upperBound),
                        ColorNumerics.ClampToFinite(Unsafe.Add(ref s, 1), Vector256<float>.Zero, upperBound),
                        ColorNumerics.ClampToFinite(Unsafe.Add(ref s, 2), Vector256<float>.Zero, upperBound),
                        ColorNumerics.ClampToFinite(Unsafe.Add(ref s, 3), Vector256<float>.Zero, upperBound),
                        scale);
                }
            }
            else if (Vector128.IsHardwareAccelerated)
            {
                DebugVerifySpanInput(source, destination, Vector128<byte>.Count);

                nuint n = destination.Vector128Count<byte>();

                ref Vector128<float> sourceBase = ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(source));
                ref Vector128<byte> destinationBase = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(destination));

                Vector128<float> scale = Vector128.Create(scaleFactor);
                Vector128<float> upperBound = Vector128.Create(byte.MaxValue / scaleFactor);

                for (nuint i = 0; i < n; i++)
                {
                    // Four float vectors give the 16 bytes of one destination vector.
                    ref Vector128<float> s = ref Unsafe.Add(ref sourceBase, i * 4);
                    Unsafe.Add(ref destinationBase, i) = FloatToByteSaturate(
                        ColorNumerics.ClampToFinite(s, Vector128<float>.Zero, upperBound),
                        ColorNumerics.ClampToFinite(Unsafe.Add(ref s, 1), Vector128<float>.Zero, upperBound),
                        ColorNumerics.ClampToFinite(Unsafe.Add(ref s, 2), Vector128<float>.Zero, upperBound),
                        ColorNumerics.ClampToFinite(Unsafe.Add(ref s, 3), Vector128<float>.Zero, upperBound),
                        scale);
                }
            }
        }

        /// <summary>
        /// Converts four vectors of finite scaled floating-point values to one vector of bytes.
        /// Each byte is the scaled value plus one half, truncated toward zero and saturated to [0, 255].
        /// </summary>
        /// <remarks>
        /// Each value must be finite, and each scaled value plus one half must be in the <see cref="int"/> range.
        /// The span overload and the JPEG color converters meet this condition. A scaled value outside [0, 255] saturates to 0 or 255.
        /// </remarks>
        /// <param name="f0">The values for bytes 0 to 15.</param>
        /// <param name="f1">The values for bytes 16 to 31.</param>
        /// <param name="f2">The values for bytes 32 to 47.</param>
        /// <param name="f3">The values for bytes 48 to 63.</param>
        /// <param name="scale">The factor applied before conversion.</param>
        /// <returns>The 64 bytes in source order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> FloatToByteSaturate(
            Vector512<float> f0,
            Vector512<float> f1,
            Vector512<float> f2,
            Vector512<float> f3,
            Vector512<float> scale)
        {
            // Each lane is truncate(x * scale + 0.5), the rule of the scalar remainder in SimdUtils. The native
            // conversion truncates toward zero on every platform, so all widths and platforms give the same bytes.
            // The narrowing saturates negative results to 0 and results above 255 to 255.
            Vector512<float> half = Vector512.Create(.5F);
            return Vector512_.NarrowToByteWithSaturation(
                Vector512.ConvertToInt32Native((f0 * scale) + half),
                Vector512.ConvertToInt32Native((f1 * scale) + half),
                Vector512.ConvertToInt32Native((f2 * scale) + half),
                Vector512.ConvertToInt32Native((f3 * scale) + half));
        }

        /// <summary>
        /// Converts four vectors of finite scaled floating-point values to one vector of bytes.
        /// Each byte is the scaled value plus one half, truncated toward zero and saturated to [0, 255].
        /// </summary>
        /// <remarks>
        /// Each value must be finite, and each scaled value plus one half must be in the <see cref="int"/> range.
        /// The span overload and the JPEG color converters meet this condition. A scaled value outside [0, 255] saturates to 0 or 255.
        /// </remarks>
        /// <param name="f0">The values for bytes 0 to 7.</param>
        /// <param name="f1">The values for bytes 8 to 15.</param>
        /// <param name="f2">The values for bytes 16 to 23.</param>
        /// <param name="f3">The values for bytes 24 to 31.</param>
        /// <param name="scale">The factor applied before conversion.</param>
        /// <returns>The 32 bytes in source order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> FloatToByteSaturate(
            Vector256<float> f0,
            Vector256<float> f1,
            Vector256<float> f2,
            Vector256<float> f3,
            Vector256<float> scale)
        {
            // See the Vector512 overload.
            Vector256<float> half = Vector256.Create(.5F);
            return Vector256_.NarrowToByteWithSaturation(
                Vector256.ConvertToInt32Native((f0 * scale) + half),
                Vector256.ConvertToInt32Native((f1 * scale) + half),
                Vector256.ConvertToInt32Native((f2 * scale) + half),
                Vector256.ConvertToInt32Native((f3 * scale) + half));
        }

        /// <summary>
        /// Converts four vectors of finite scaled floating-point values to one vector of bytes.
        /// Each byte is the scaled value plus one half, truncated toward zero and saturated to [0, 255].
        /// </summary>
        /// <remarks>
        /// Each value must be finite, and each scaled value plus one half must be in the <see cref="int"/> range.
        /// The span overload and the JPEG color converters meet this condition. A scaled value outside [0, 255] saturates to 0 or 255.
        /// </remarks>
        /// <param name="f0">The values for bytes 0 to 3.</param>
        /// <param name="f1">The values for bytes 4 to 7.</param>
        /// <param name="f2">The values for bytes 8 to 11.</param>
        /// <param name="f3">The values for bytes 12 to 15.</param>
        /// <param name="scale">The factor applied before conversion.</param>
        /// <returns>The 16 bytes in source order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> FloatToByteSaturate(
            Vector128<float> f0,
            Vector128<float> f1,
            Vector128<float> f2,
            Vector128<float> f3,
            Vector128<float> scale)
        {
            // See the Vector512 overload.
            Vector128<float> half = Vector128.Create(.5F);
            return Vector128_.NarrowToByteWithSaturation(
                Vector128.ConvertToInt32Native((f0 * scale) + half),
                Vector128.ConvertToInt32Native((f1 * scale) + half),
                Vector128.ConvertToInt32Native((f2 * scale) + half),
                Vector128.ConvertToInt32Native((f3 * scale) + half));
        }
    }
}
