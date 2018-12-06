// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats.Utils
{
    /// <summary>
    /// Contains optimized implementations for conversion between pixel formats.
    /// </summary>
    /// <remarks>
    /// Implementations are based on ideas in:
    /// https://github.com/dotnet/coreclr/blob/master/src/System.Private.CoreLib/shared/System/Buffers/Binary/Reader.cs#L84
    /// The JIT can detect and optimize rotation idioms ROTL (Rotate Left)
    /// and ROTR (Rotate Right) emitting efficient CPU instructions:
    /// https://github.com/dotnet/coreclr/pull/1830
    /// </remarks>
    internal static class PixelConverter
    {
        public static class FromRgba32
        {
            /// <summary>
            /// Converts a packed <see cref="Rgba32"/> to <see cref="Argb32"/>.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static uint ToArgb32(uint packedRgba)
            {
                // packedRgba          = [aa bb gg rr]
                // ROTL(8, packedRgba) = [bb gg rr aa]
                return (packedRgba << 8) | (packedRgba >> 24);
            }

            /// <summary>
            /// Converts a packed <see cref="Rgba32"/> to <see cref="Bgra32"/>.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static uint ToBgra32(uint packedRgba)
            {
                // packedRgba          = [aa bb gg rr]
                // tmp1                = [aa 00 gg 00]
                // tmp2                = [00 bb 00 rr]
                // tmp3=ROTL(16, tmp2) = [00 rr 00 bb]
                // tmp1 + tmp3         = [aa rr gg bb]
                uint tmp1 = packedRgba & 0xFF00FF00;
                uint tmp2 = packedRgba & 0x00FF00FF;
                uint tmp3 = (tmp2 << 16) | (tmp2 >> 16);
                return tmp1 + tmp3;
            }
        }

        public static class FromArgb32
        {
            /// <summary>
            /// Converts a packed <see cref="Argb32"/> to <see cref="Rgba32"/>.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static uint ToRgba32(uint packedArgb)
            {
                // packedArgb          = [bb gg rr aa]
                // ROTR(8, packedArgb) = [aa bb gg rr]
                return (packedArgb >> 8) | (packedArgb << 24);
            }

            /// <summary>
            /// Converts a packed <see cref="Argb32"/> to <see cref="Bgra32"/>.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static uint ToBgra32(uint packedArgb)
            {
                // packedArgb          = [bb gg rr aa]
                // REVERSE(packedArgb) = [aa rr gg bb]
                return BinaryPrimitives.ReverseEndianness(packedArgb);
            }
        }

        public static class FromBgra32
        {
            /// <summary>
            /// Converts a packed <see cref="Bgra32"/> to <see cref="Argb32"/>.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static uint ToArgb32(uint packedBgra)
            {
                // packedBgra          = [aa rr gg bb]
                // REVERSE(packedBgra) = [bb gg rr aa]
                return BinaryPrimitives.ReverseEndianness(packedBgra);
            }

            /// <summary>
            /// Converts a packed <see cref="Rgba32"/> to <see cref="Bgra32"/>.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static uint ToRgba32(uint packedBgra)
            {
                // packedRgba          = [aa rr gg bb]
                // tmp1                = [aa 00 gg 00]
                // tmp2                = [00 rr 00 bb]
                // tmp3=ROTL(16, tmp2) = [00 bb 00 rr]
                // tmp1 + tmp3         = [aa bb gg rr]
                uint tmp1 = packedBgra & 0xFF00FF00;
                uint tmp2 = packedBgra & 0x00FF00FF;
                uint tmp3 = (tmp2 << 16) | (tmp2 >> 16);
                return tmp1 + tmp3;
            }
        }
    }
}