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
        /// <param name="source">The source span of floats</param>
        /// <param name="dest">The destination span of float</param>
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

        [Conditional("DEBUG")]
        private static void VerifyShuffleSpanInput(ReadOnlySpan<float> source, Span<float> dest)
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
            public const byte XYZW = (3 << 6) | (2 << 4) | (1 << 2) | 0;
            public const byte ZYXW = (3 << 6) | (0 << 4) | (1 << 2) | 2;

            public static ReadOnlySpan<byte> WXYZ_128 => MmShuffleByte128(2, 1, 0, 3);

            public static ReadOnlySpan<byte> XYZW_128 => MmShuffleByte128(3, 2, 1, 0);

            public static ReadOnlySpan<byte> ZYXW_128 => MmShuffleByte128(3, 0, 1, 2);

            public static ReadOnlySpan<byte> WXYZ_256 => MmShuffleByte256(2, 1, 0, 3);

            public static ReadOnlySpan<byte> XYZW_256 => MmShuffleByte256(3, 2, 1, 0);

            public static ReadOnlySpan<byte> ZYXW_256 => MmShuffleByte256(3, 0, 1, 2);

            private static byte[] MmShuffleByte128(int p3, int p2, int p1, int p0)
            {
                byte[] result = new byte[16];

                for (int i = 0; i < result.Length; i += 4)
                {
                    result[i] = (byte)(p0 + i);
                    result[i + 1] = (byte)(p1 + i);
                    result[i + 2] = (byte)(p2 + i);
                    result[i + 3] = (byte)(p3 + i);
                }

                return result;
            }

            private static byte[] MmShuffleByte256(int p3, int p2, int p1, int p0)
            {
                byte[] result = new byte[32];

                for (int i = 0; i < result.Length; i += 4)
                {
                    result[i] = (byte)(p0 + i);
                    result[i + 1] = (byte)(p1 + i);
                    result[i + 2] = (byte)(p2 + i);
                    result[i + 3] = (byte)(p3 + i);
                }

                return result;
            }

            public static void InverseMmShuffle(byte control, out int p3, out int p2, out int p1, out int p0)
            {
                p3 = control >> 6 & 0x3;
                p2 = control >> 4 & 0x3;
                p1 = control >> 2 & 0x3;
                p0 = control >> 0 & 0x3;
            }
        }
    }
}
