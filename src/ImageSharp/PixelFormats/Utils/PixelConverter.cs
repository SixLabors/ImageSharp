// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
        /// <summary>
        /// Optimized converters from <see cref="Rgba32"/>.
        /// </summary>
        public static class FromRgba32
        {
            // Input pixels have: X = R, Y = G, Z = B and W = A.

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
            /// <see cref="Argb32"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Abgr32"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToAbgr32(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle4<WZYXShuffle4>(source, dest, default);

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

        /// <summary>
        /// Optimized converters from <see cref="Argb32"/>.
        /// </summary>
        public static class FromArgb32
        {
            // Input pixels have: X = A, Y = R, Z = G and W = B.

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
            /// a collection of <see cref="Abgr32"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToAbgr32(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle4<XWZYShuffle4>(source, dest, default);

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

        /// <summary>
        /// Optimized converters from <see cref="Bgra32"/>.
        /// </summary>
        public static class FromBgra32
        {
            // Input pixels have: X = B, Y = G, Z = R and W = A.

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
            /// a collection of <see cref="Rgba32"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToRgba32(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle4<ZYXWShuffle4>(source, dest, default);

            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Bgra32"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Abgr32"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToAbgr32(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle4<WXYZShuffle4>(source, dest, default);

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

        /// <summary>
        /// Optimized converters from <see cref="Abgr32"/>.
        /// </summary>
        public static class FromAbgr32
        {
            // Input pixels have: X = A, Y = B, Z = G and W = R.

            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Abgr32"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Argb32"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToArgb32(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle4<XWZYShuffle4>(source, dest, default);

            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Abgr32"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Bgra32"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToRgba32(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle4<WZYXShuffle4>(source, dest, default);

            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Abgr32"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Bgra32"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToBgra32(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle4<YZWXShuffle4>(source, dest, default);

            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Abgr32"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Rgb24"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToRgb24(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle4Slice3(source, dest, new DefaultShuffle4Slice3(0, 1, 2, 3));

            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Abgr32"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Bgr24"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToBgr24(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle4Slice3(source, dest, new DefaultShuffle4Slice3(0, 3, 2, 1));
        }

        /// <summary>
        /// Optimized converters from <see cref="Rgb24"/>.
        /// </summary>
        public static class FromRgb24
        {
            // Input pixels have: X = R, Y = G and Z = B.

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
            /// <see cref="Rgba32"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Bgra32"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToAbgr32(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Pad3Shuffle4(source, dest, new DefaultPad3Shuffle4(0, 1, 2, 3));

            /// <summary>
            /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
            /// <see cref="Rgb24"/> pixels to a <see cref="Span{Byte}"/> representing
            /// a collection of <see cref="Bgr24"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToBgr24(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Shuffle3(source, dest, new DefaultShuffle3(0, 1, 2));
        }

        /// <summary>
        /// Optimized converters from <see cref="Bgr24"/>.
        /// </summary>
        public static class FromBgr24
        {
            // Input pixels have: X = B, Y = G and Z = R.

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
            /// a collection of <see cref="Abgr32"/> pixels.
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToAbgr32(ReadOnlySpan<byte> source, Span<byte> dest)
                => SimdUtils.Pad3Shuffle4(source, dest, new DefaultPad3Shuffle4(2, 1, 0, 3));

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
