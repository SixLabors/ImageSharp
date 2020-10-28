// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
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
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Rgba32"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Argb32"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToArgb32(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle4Channel(source, dest, SimdUtils.Shuffle.WXYZ);

            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Rgba32"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Bgra32"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToBgra32(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle4Channel(source, dest, SimdUtils.Shuffle.ZYXW);
        }

        public static class FromArgb32
        {
            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Argb32"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Rgba32"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToRgba32(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle4Channel(source, dest, SimdUtils.Shuffle.YZWX);

            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Argb32"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Bgra32"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToBgra32(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle4Channel(source, dest, SimdUtils.Shuffle.WZYX);
        }

        public static class FromBgra32
        {
            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Bgra32"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Argb32"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToArgb32(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle4Channel(source, dest, SimdUtils.Shuffle.WZYX);

            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Bgra32"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Bgra32"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToRgba32(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle4Channel(source, dest, SimdUtils.Shuffle.ZYXW);
        }
    }
}
