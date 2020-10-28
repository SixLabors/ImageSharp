// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp
{
    internal static partial class SimdUtils
    {
        /// <summary>
        /// Shuffle single-precision (32-bit) floating-point elements in <paramref name="source"/>
        /// using the control and store the results in <paramref name="dest"/>.
        /// </summary>
        /// <param name="source">The source span of floats.</param>
        /// <param name="dest">The destination span of floats.</param>
        /// <param name="control">The byte control.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Shuffle4Channel(
            ReadOnlySpan<float> source,
            Span<float> dest,
            byte control)
        {
            VerifyShuffleSpanInput(source, dest);

            // TODO: There doesn't seem to be any APIs for
            // System.Numerics that allow shuffling.
#if SUPPORTS_RUNTIME_INTRINSICS
            HwIntrinsics.Shuffle4ChannelReduce(ref source, ref dest, control);
#endif

            // Deal with the remainder:
            if (source.Length > 0)
            {
                ShuffleRemainder4Channel(source, dest, control);
            }
        }

        /// <summary>
        /// Shuffle 8-bit integers in a within 128-bit lanes in <paramref name="source"/>
        /// using the control and store the results in <paramref name="dest"/>.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        /// <param name="control">The byte control.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Shuffle4Channel(
            ReadOnlySpan<byte> source,
            Span<byte> dest,
            byte control)
        {
            VerifyShuffleSpanInput(source, dest);

            // TODO: There doesn't seem to be any APIs for
            // System.Numerics that allow shuffling.
#if SUPPORTS_RUNTIME_INTRINSICS
            HwIntrinsics.Shuffle4ChannelReduce(ref source, ref dest, control);
#endif

            // Deal with the remainder:
            if (source.Length > 0)
            {
                ShuffleRemainder4Channel(source, dest, control);
            }
        }

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ShuffleRemainder4Channel(
            ReadOnlySpan<float> source,
            Span<float> dest,
            byte control)
        {
            ref float sBase = ref MemoryMarshal.GetReference(source);
            ref float dBase = ref MemoryMarshal.GetReference(dest);
            Shuffle.InverseMmShuffle(control, out int p3, out int p2, out int p1, out int p0);

            for (int i = 0; i < source.Length; i += 4)
            {
                Unsafe.Add(ref dBase, i) = Unsafe.Add(ref sBase, p0 + i);
                Unsafe.Add(ref dBase, i + 1) = Unsafe.Add(ref sBase, p1 + i);
                Unsafe.Add(ref dBase, i + 2) = Unsafe.Add(ref sBase, p2 + i);
                Unsafe.Add(ref dBase, i + 3) = Unsafe.Add(ref sBase, p3 + i);
            }
        }

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ShuffleRemainder4Channel(
            ReadOnlySpan<byte> source,
            Span<byte> dest,
            byte control)
        {
#if NETCOREAPP
            // The JIT can detect and optimize rotation idioms ROTL (Rotate Left)
            // and ROTR (Rotate Right) emitting efficient CPU instructions:
            // https://github.com/dotnet/coreclr/pull/1830
            switch (control)
            {
                case Shuffle.WXYZ:
                    WXYZ(source, dest);
                    return;
                case Shuffle.WZYX:
                    WZYX(source, dest);
                    return;
                case Shuffle.YZWX:
                    YZWX(source, dest);
                    return;
                case Shuffle.ZYXW:
                    ZYXW(source, dest);
                    return;
            }
#endif

            ref byte sBase = ref MemoryMarshal.GetReference(source);
            ref byte dBase = ref MemoryMarshal.GetReference(dest);
            Shuffle.InverseMmShuffle(control, out int p3, out int p2, out int p1, out int p0);

            for (int i = 0; i < source.Length; i += 4)
            {
                Unsafe.Add(ref dBase, i) = Unsafe.Add(ref sBase, p0 + i);
                Unsafe.Add(ref dBase, i + 1) = Unsafe.Add(ref sBase, p1 + i);
                Unsafe.Add(ref dBase, i + 2) = Unsafe.Add(ref sBase, p2 + i);
                Unsafe.Add(ref dBase, i + 3) = Unsafe.Add(ref sBase, p3 + i);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void WXYZ(ReadOnlySpan<byte> source, Span<byte> dest)
        {
            ReadOnlySpan<uint> s = MemoryMarshal.Cast<byte, uint>(source);
            Span<uint> d = MemoryMarshal.Cast<byte, uint>(dest);
            ref uint sBase = ref MemoryMarshal.GetReference(s);
            ref uint dBase = ref MemoryMarshal.GetReference(d);

            for (int i = 0; i < s.Length; i++)
            {
                uint packed = Unsafe.Add(ref sBase, i);

                // packed          = [W Z Y X]
                // ROTL(8, packed) = [Z Y X W]
                Unsafe.Add(ref dBase, i) = (packed << 8) | (packed >> 24);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void ZYXW(ReadOnlySpan<byte> source, Span<byte> dest)
        {
            ReadOnlySpan<uint> s = MemoryMarshal.Cast<byte, uint>(source);
            Span<uint> d = MemoryMarshal.Cast<byte, uint>(dest);
            ref uint sBase = ref MemoryMarshal.GetReference(s);
            ref uint dBase = ref MemoryMarshal.GetReference(d);

            for (int i = 0; i < s.Length; i++)
            {
                uint packed = Unsafe.Add(ref sBase, i);

                // packed              = [W Z Y X]
                // tmp1                = [W 0 Y 0]
                // tmp2                = [0 Z 0 X]
                // tmp3=ROTL(16, tmp2) = [0 X 0 Z]
                // tmp1 + tmp3         = [W X Y Z]
                uint tmp1 = packed & 0xFF00FF00;
                uint tmp2 = packed & 0x00FF00FF;
                uint tmp3 = (tmp2 << 16) | (tmp2 >> 16);

                Unsafe.Add(ref dBase, i) = tmp1 + tmp3;
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void WZYX(ReadOnlySpan<byte> source, Span<byte> dest)
        {
            ReadOnlySpan<uint> s = MemoryMarshal.Cast<byte, uint>(source);
            Span<uint> d = MemoryMarshal.Cast<byte, uint>(dest);
            ref uint sBase = ref MemoryMarshal.GetReference(s);
            ref uint dBase = ref MemoryMarshal.GetReference(d);

            for (int i = 0; i < s.Length; i++)
            {
                uint packed = Unsafe.Add(ref sBase, i);

                // packed              = [W Z Y X]
                // REVERSE(packedArgb) = [X Y Z W]
                Unsafe.Add(ref dBase, i) = BinaryPrimitives.ReverseEndianness(packed);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void YZWX(ReadOnlySpan<byte> source, Span<byte> dest)
        {
            ReadOnlySpan<uint> s = MemoryMarshal.Cast<byte, uint>(source);
            Span<uint> d = MemoryMarshal.Cast<byte, uint>(dest);
            ref uint sBase = ref MemoryMarshal.GetReference(s);
            ref uint dBase = ref MemoryMarshal.GetReference(d);

            for (int i = 0; i < s.Length; i++)
            {
                uint packed = Unsafe.Add(ref sBase, i);

                // packed              = [W Z Y X]
                // ROTR(8, packedArgb) = [Y Z W X]
                Unsafe.Add(ref dBase, i) = (packed >> 8) | (packed << 24);
            }
        }

        [Conditional("DEBUG")]
        private static void VerifyShuffleSpanInput<T>(ReadOnlySpan<T> source, Span<T> dest)
            where T : struct
        {
            DebugGuard.IsTrue(
                source.Length == dest.Length,
                nameof(source),
                "Input spans must be of same length!");

            DebugGuard.IsTrue(
                source.Length % 4 == 0,
                nameof(source),
                "Input spans must be divisiable by 4!");
        }

        public static class Shuffle
        {
            public const byte WXYZ = (2 << 6) | (1 << 4) | (0 << 2) | 3;
            public const byte WZYX = (0 << 6) | (1 << 4) | (2 << 2) | 3;
            public const byte XYZW = (3 << 6) | (2 << 4) | (1 << 2) | 0;
            public const byte YZWX = (0 << 6) | (3 << 4) | (2 << 2) | 1;
            public const byte ZYXW = (3 << 6) | (0 << 4) | (1 << 2) | 2;

            [MethodImpl(InliningOptions.ShortMethod)]
            public static byte MmShuffle(byte p3, byte p2, byte p1, byte p0)
                => (byte)((p3 << 6) | (p2 << 4) | (p1 << 2) | p0);

            [MethodImpl(InliningOptions.ShortMethod)]
            public static void MmShuffleSpan(ref Span<byte> span, byte control)
            {
                InverseMmShuffle(
                     control,
                     out int p3,
                     out int p2,
                     out int p1,
                     out int p0);

                ref byte spanBase = ref MemoryMarshal.GetReference(span);

                for (int i = 0; i < span.Length; i += 4)
                {
                    Unsafe.Add(ref spanBase, i) = (byte)(p0 + i);
                    Unsafe.Add(ref spanBase, i + 1) = (byte)(p1 + i);
                    Unsafe.Add(ref spanBase, i + 2) = (byte)(p2 + i);
                    Unsafe.Add(ref spanBase, i + 3) = (byte)(p3 + i);
                }
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public static void InverseMmShuffle(
                byte control,
                out int p3,
                out int p2,
                out int p1,
                out int p0)
            {
                p3 = control >> 6 & 0x3;
                p2 = control >> 4 & 0x3;
                p1 = control >> 2 & 0x3;
                p0 = control >> 0 & 0x3;
            }
        }
    }
}
