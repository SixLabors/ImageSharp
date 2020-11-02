// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
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
                => SimdUtils.Shuffle4<WXYZShuffle4>(source, dest, default);

            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Rgba32"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Bgra32"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToBgra32(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle4<ZYXWShuffle4>(source, dest, default);

            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Rgba32"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Rgb24"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToRgb24(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle4Slice3<XYZWShuffle4Slice3>(source, dest, default);

            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Rgba32"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Bgr24"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToBgr24(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle4Slice3(source, dest, new DefaultShuffle4Slice3(3, 0, 1, 2));
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
                => SimdUtils.Shuffle4<YZWXShuffle4>(source, dest, default);

            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Argb32"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Bgra32"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToBgra32(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle4<WZYXShuffle4>(source, dest, default);

            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Argb32"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Rgb24"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToRgb24(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle4Slice3(source, dest, new DefaultShuffle4Slice3(0, 3, 2, 1));

            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Argb32"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Bgr24"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToBgr24(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle4Slice3(source, dest, new DefaultShuffle4Slice3(0, 1, 2, 3));
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
                => SimdUtils.Shuffle4<WZYXShuffle4>(source, dest, default);

            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Bgra32"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Bgra32"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToRgba32(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle4<ZYXWShuffle4>(source, dest, default);

            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Argb32"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Rgb24"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToRgb24(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle4Slice3(source, dest, new DefaultShuffle4Slice3(3, 0, 1, 2));

            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Argb32"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Bgr24"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToBgr24(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle4Slice3<XYZWShuffle4Slice3>(source, dest, default);
        }

        public static class FromRgb24
        {
            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Rgb24"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Rgba32"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToRgba32(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Pad3Shuffle4<XYZWPad3Shuffle4>(source, dest, default);

            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Rgba32"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Argb32"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToArgb32(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Pad3Shuffle4(source, dest, new DefaultPad3Shuffle4(2, 1, 0, 3));

            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Rgba32"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Bgra32"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToBgra32(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Pad3Shuffle4(source, dest, new DefaultPad3Shuffle4(3, 0, 1, 2));

            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Rgb24"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Bgr24"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToBgr24(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle3(source, dest, new DefaultShuffle3(0, 1, 2));
        }

        public static class FromBgr24
        {
            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Bgr24"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Argb32"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToArgb32(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Pad3Shuffle4(source, dest, new DefaultPad3Shuffle4(0, 1, 2, 3));

            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Bgr24"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Bgra32"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToRgba32(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Pad3Shuffle4(source, dest, new DefaultPad3Shuffle4(3, 0, 1, 2));

            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Bgr24"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Bgra32"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToBgra32(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Pad3Shuffle4<XYZWPad3Shuffle4>(source, dest, default);

            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Bgr24"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Rgb24"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToRgb24(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle3(source, dest, new DefaultShuffle3(0, 1, 2));
        }
    }
}
