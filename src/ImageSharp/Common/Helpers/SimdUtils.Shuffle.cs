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
            VerifyShuffle4SpanInput(source, dest);

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
            where TShuffle : struct, IShuffle4
        {
            VerifyShuffle4SpanInput(source, dest);

#if SUPPORTS_RUNTIME_INTRINSICS
            HwIntrinsics.Shuffle4Reduce(ref source, ref dest, shuffle.Control);
#endif

            // Deal with the remainder:
            if (source.Length > 0)
            {
                shuffle.RunFallbackShuffle(source, dest);
            }
        }

        /// <summary>
        /// Shuffle 8-bit integer triplets within 128-bit lanes in <paramref name="source"/>
        /// using the control and store the results in <paramref name="dest"/>.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        /// <param name="shuffle">The type of shuffle to perform.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Shuffle3<TShuffle>(
            ReadOnlySpan<byte> source,
            Span<byte> dest,
            TShuffle shuffle)
            where TShuffle : struct, IShuffle3
        {
            VerifyShuffle3SpanInput(source, dest);

#if SUPPORTS_RUNTIME_INTRINSICS
            HwIntrinsics.Shuffle3Reduce(ref source, ref dest, shuffle.Control);
#endif

            // Deal with the remainder:
            if (source.Length > 0)
            {
                shuffle.RunFallbackShuffle(source, dest);
            }
        }

        /// <summary>
        /// Pads then shuffles 8-bit integers within 128-bit lanes in <paramref name="source"/>
        /// using the control and store the results in <paramref name="dest"/>.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        /// <param name="shuffle">The type of shuffle to perform.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Pad3Shuffle4<TShuffle>(
            ReadOnlySpan<byte> source,
            Span<byte> dest,
            TShuffle shuffle)
            where TShuffle : struct, IPad3Shuffle4
        {
            VerifyPad3Shuffle4SpanInput(source, dest);

#if SUPPORTS_RUNTIME_INTRINSICS
            HwIntrinsics.Pad3Shuffle4Reduce(ref source, ref dest, shuffle.Control);
#endif

            // Deal with the remainder:
            if (source.Length > 0)
            {
                shuffle.RunFallbackShuffle(source, dest);
            }
        }

        /// <summary>
        /// Shuffles then slices 8-bit integers within 128-bit lanes in <paramref name="source"/>
        /// using the control and store the results in <paramref name="dest"/>.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        /// <param name="shuffle">The type of shuffle to perform.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Shuffle4Slice3<TShuffle>(
            ReadOnlySpan<byte> source,
            Span<byte> dest,
            TShuffle shuffle)
            where TShuffle : struct, IShuffle4Slice3
        {
            VerifyShuffle4Slice3SpanInput(source, dest);

#if SUPPORTS_RUNTIME_INTRINSICS
            HwIntrinsics.Shuffle4Slice3Reduce(ref source, ref dest, shuffle.Control);
#endif

            // Deal with the remainder:
            if (source.Length > 0)
            {
                shuffle.RunFallbackShuffle(source, dest);
            }
        }

        private static void Shuffle4Remainder(
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

        [Conditional("DEBUG")]
        private static void VerifyShuffle4SpanInput<T>(ReadOnlySpan<T> source, Span<T> dest)
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
        private static void VerifyShuffle3SpanInput<T>(ReadOnlySpan<T> source, Span<T> dest)
            where T : struct
        {
            DebugGuard.IsTrue(
                source.Length == dest.Length,
                nameof(source),
                "Input spans must be of same length!");

            DebugGuard.IsTrue(
                source.Length % 3 == 0,
                nameof(source),
                "Input spans must be divisable by 3!");
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
                dest.Length >= source.Length * 3 / 4,
                nameof(source),
                "Output span must be at least 3/4 the length of the input span!");
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
