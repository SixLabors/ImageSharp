// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
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
        public static void Shuffle4(
            ReadOnlySpan<float> source,
            Span<float> dest,
            byte control)
        {
            VerifyShuffleSpanInput(source, dest);

#if SUPPORTS_RUNTIME_INTRINSICS
            HwIntrinsics.Shuffle4Reduce(ref source, ref dest, control);
#endif

            // Deal with the remainder:
            if (source.Length > 0)
            {
                Shuffle4Remainder(source, dest, control);
            }
        }

        /// <summary>
        /// Shuffle 8-bit integers within 128-bit lanes in <paramref name="source"/>
        /// using the control and store the results in <paramref name="dest"/>.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        /// <param name="shuffle">The type of shuffle to perform.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Shuffle4<TShuffle>(
            ReadOnlySpan<byte> source,
            Span<byte> dest,
            TShuffle shuffle)
            where TShuffle : struct, IComponentShuffle
        {
            VerifyShuffleSpanInput(source, dest);

#if SUPPORTS_RUNTIME_INTRINSICS
            HwIntrinsics.Shuffle4Reduce(ref source, ref dest, shuffle.Control);
#endif

            // Deal with the remainder:
            if (source.Length > 0)
            {
                shuffle.RunFallbackShuffle(source, dest);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Pad3Shuffle4(
            ReadOnlySpan<byte> source,
            Span<byte> dest,
            byte control)
        {
            VerifyPad3Shuffle4SpanInput(source, dest);

#if SUPPORTS_RUNTIME_INTRINSICS
            HwIntrinsics.Pad3Shuffle4Reduce(ref source, ref dest, control);
#endif

            // Deal with the remainder:
            if (source.Length > 0)
            {
                Pad3Shuffle4Remainder(source, dest, control);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Shuffle4Slice3(
            ReadOnlySpan<byte> source,
            Span<byte> dest,
            byte control)
        {
            VerifyShuffle4Slice3SpanInput(source, dest);

#if SUPPORTS_RUNTIME_INTRINSICS
            HwIntrinsics.Shuffle4Slice3Reduce(ref source, ref dest, control);
#endif

            // Deal with the remainder:
            if (source.Length > 0)
            {
                Shuffle4Slice3Remainder(source, dest, control);
            }
        }

        public static void Shuffle4Remainder(
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

        public static void Pad3Shuffle4Remainder(
            ReadOnlySpan<byte> source,
            Span<byte> dest,
            byte control)
        {
            ref byte sBase = ref MemoryMarshal.GetReference(source);
            ref byte dBase = ref MemoryMarshal.GetReference(dest);
            Shuffle.InverseMmShuffle(control, out int p3, out int p2, out int p1, out int p0);

            for (int i = 0, j = 0; i < dest.Length; i += 4, j += 3)
            {
                Unsafe.Add(ref dBase, p0 + i) = Unsafe.Add(ref sBase, j);
                Unsafe.Add(ref dBase, p1 + i) = Unsafe.Add(ref sBase, j + 1);
                Unsafe.Add(ref dBase, p2 + i) = Unsafe.Add(ref sBase, j + 2);
                Unsafe.Add(ref dBase, p3 + i) = byte.MaxValue;
            }
        }

        public static void Shuffle4Slice3Remainder(
            ReadOnlySpan<byte> source,
            Span<byte> dest,
            byte control)
        {
            ref byte sBase = ref MemoryMarshal.GetReference(source);
            ref byte dBase = ref MemoryMarshal.GetReference(dest);
            Shuffle.InverseMmShuffle(control, out int _, out int p2, out int p1, out int p0);

            for (int i = 0, j = 0; i < dest.Length; i += 3, j += 4)
            {
                Unsafe.Add(ref dBase, i) = Unsafe.Add(ref sBase, p0 + j);
                Unsafe.Add(ref dBase, i + 1) = Unsafe.Add(ref sBase, p1 + j);
                Unsafe.Add(ref dBase, i + 2) = Unsafe.Add(ref sBase, p2 + j);
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
                "Input spans must be divisable by 4!");
        }

        [Conditional("DEBUG")]
        private static void VerifyPad3Shuffle4SpanInput(ReadOnlySpan<byte> source, Span<byte> dest)
        {
            DebugGuard.IsTrue(
                source.Length % 3 == 0,
                nameof(source),
                "Input span must be divisable by 3!");

            DebugGuard.IsTrue(
                dest.Length % 4 == 0,
                nameof(dest),
                "Output span must be divisable by 4!");

            DebugGuard.IsTrue(
                source.Length == dest.Length * 3 / 4,
                nameof(source),
                "Input span must be 3/4 the length of the output span!");
        }

        [Conditional("DEBUG")]
        private static void VerifyShuffle4Slice3SpanInput(ReadOnlySpan<byte> source, Span<byte> dest)
        {
            DebugGuard.IsTrue(
                source.Length % 4 == 0,
                nameof(source),
                "Input span must be divisable by 4!");

            DebugGuard.IsTrue(
                dest.Length % 3 == 0,
                nameof(dest),
                "Output span must be divisable by 3!");

            DebugGuard.IsTrue(
                source.Length == dest.Length * 4 / 3,
                nameof(source),
                "Output span must be 3/4 the length of the input span!");
        }

        public static class Shuffle
        {
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
