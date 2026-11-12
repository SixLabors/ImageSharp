// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.PixelFormats.Utils;

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
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToArgb32(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Shuffle4<WXYZShuffle4>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Rgba32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgra32"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgra32(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Shuffle4<ZYXWShuffle4>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Argb32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Abgr32"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToAbgr32(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Shuffle4<WZYXShuffle4>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Rgba32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Rgb24"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToRgb24(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Shuffle4Slice3<XYZWShuffle4Slice3>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Rgba32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgr24"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgr24(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Shuffle4Slice3<ZYXWShuffle4Slice3>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Rgba32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgra5551"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgra5551(ReadOnlySpan<byte> source, Span<byte> dest)
            => Bgra5551Kernels.PackFrom4Bytes(source, dest, 0, 8, 16, 24);
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
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToRgba32(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Shuffle4<YZWXShuffle4>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Argb32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgra32"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgra32(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Shuffle4<WZYXShuffle4>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Argb32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Abgr32"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToAbgr32(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Shuffle4<XWZYShuffle4>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Argb32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Rgb24"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToRgb24(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Shuffle4Slice3<YZWXShuffle4Slice3>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Argb32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgr24"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgr24(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Shuffle4Slice3<WZYXShuffle4Slice3>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Argb32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgra5551"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgra5551(ReadOnlySpan<byte> source, Span<byte> dest)
            => Bgra5551Kernels.PackFrom4Bytes(source, dest, 8, 16, 24, 0);
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
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToArgb32(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Shuffle4<WZYXShuffle4>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgra32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Rgba32"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToRgba32(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Shuffle4<ZYXWShuffle4>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgra32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Abgr32"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToAbgr32(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Shuffle4<WXYZShuffle4>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Argb32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Rgb24"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToRgb24(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Shuffle4Slice3<ZYXWShuffle4Slice3>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Argb32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgr24"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgr24(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Shuffle4Slice3<XYZWShuffle4Slice3>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgra32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgra5551"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgra5551(ReadOnlySpan<byte> source, Span<byte> dest)
            => Bgra5551Kernels.PackFrom4Bytes(source, dest, 16, 8, 0, 24);
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
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToArgb32(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Shuffle4<XWZYShuffle4>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Abgr32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgra32"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToRgba32(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Shuffle4<WZYXShuffle4>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Abgr32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgra32"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgra32(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Shuffle4<YZWXShuffle4>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Abgr32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Rgb24"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToRgb24(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Shuffle4Slice3<WZYXShuffle4Slice3>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Abgr32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgr24"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgr24(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Shuffle4Slice3<YZWXShuffle4Slice3>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Abgr32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgra5551"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgra5551(ReadOnlySpan<byte> source, Span<byte> dest)
            => Bgra5551Kernels.PackFrom4Bytes(source, dest, 24, 16, 8, 0);
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
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToRgba32(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Pad3Shuffle4<XYZWPad3Shuffle4>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Rgba32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Argb32"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToArgb32(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Pad3Shuffle4<WXYZPad3Shuffle4>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Rgba32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgra32"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgra32(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Pad3Shuffle4<ZYXWPad3Shuffle4>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Rgba32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgra32"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToAbgr32(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Pad3Shuffle4<WZYXPad3Shuffle4>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Rgb24"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgr24"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgr24(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Shuffle3<ZYXShuffle3>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Rgb24"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgra5551"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgra5551(ReadOnlySpan<byte> source, Span<byte> dest)
            => Bgra5551Kernels.PackFrom3Bytes(source, dest, 0, 8, 16);
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
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToArgb32(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Pad3Shuffle4<WZYXPad3Shuffle4>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgr24"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgra32"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToRgba32(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Pad3Shuffle4<ZYXWPad3Shuffle4>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgr24"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgra32"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgra32(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Pad3Shuffle4<XYZWPad3Shuffle4>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgr24"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Abgr32"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToAbgr32(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Pad3Shuffle4<WXYZPad3Shuffle4>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgr24"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Rgb24"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToRgb24(ReadOnlySpan<byte> source, Span<byte> dest)
            => SimdUtils.Shuffle3<ZYXShuffle3>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgr24"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgra5551"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgra5551(ReadOnlySpan<byte> source, Span<byte> dest)
            => Bgra5551Kernels.PackFrom3Bytes(source, dest, 16, 8, 0);
    }

    /// <summary>
    /// Optimized converters from <see cref="Bgra5551"/>.
    /// </summary>
    public static class FromBgra5551
    {
        // Input pixels have blue in bits 0..4, green in bits 5..9, red in bits 10..14 and alpha in bit 15.

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgra5551"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Rgba32"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToRgba32(ReadOnlySpan<byte> source, Span<byte> dest)
            => Bgra5551Kernels.UnpackTo4Bytes(source, dest, 0, 8, 16, 24);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgra5551"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Argb32"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToArgb32(ReadOnlySpan<byte> source, Span<byte> dest)
            => Bgra5551Kernels.UnpackTo4Bytes(source, dest, 8, 16, 24, 0);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgra5551"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgra32"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgra32(ReadOnlySpan<byte> source, Span<byte> dest)
            => Bgra5551Kernels.UnpackTo4Bytes(source, dest, 16, 8, 0, 24);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgra5551"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Abgr32"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToAbgr32(ReadOnlySpan<byte> source, Span<byte> dest)
            => Bgra5551Kernels.UnpackTo4Bytes(source, dest, 24, 16, 8, 0);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgra5551"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Rgb24"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToRgb24(ReadOnlySpan<byte> source, Span<byte> dest)
            => Bgra5551Kernels.UnpackTo3Bytes(source, dest, 0, 8, 16);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgra5551"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgr24"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgr24(ReadOnlySpan<byte> source, Span<byte> dest)
            => Bgra5551Kernels.UnpackTo3Bytes(source, dest, 16, 8, 0);
    }

    /// <summary>
    /// Converts between <see cref="Bgra5551"/> pixels and the 4-byte and 3-byte layouts, with SIMD.
    /// </summary>
    private static class Bgra5551Kernels
    {
        // The Bgra5551 lanes divide by the constant 255 or 31 with one multiply and one shift: (x * ceil(2^s / d)) >> s.
        // A vector integer division does more work. On x86, the JIT checks the divisor for zero, converts the lanes to
        // double, divides and converts back.
        // (x * ceil(2^23 / 255)) >> 23 equals x / 255 for each x below 66052. The largest x is 255 * 31 + 127 = 8032.
        // (x * ceil(2^20 / 31)) >> 20 equals x / 31 for each x below 34952. The largest x is 31 * 255 + 15 = 7920.

        /// <summary>
        /// The shift of the vector division by 255.
        /// </summary>
        private const int Divide255Shift = 23;

        /// <summary>
        /// The multiplier of the vector division by 255: <c>ceil(2^23 / 255)</c>.
        /// </summary>
        private const uint Divide255Multiplier = ((1U << Divide255Shift) + 254) / 255;

        /// <summary>
        /// The shift of the vector division by 31.
        /// </summary>
        private const int Divide31Shift = 20;

        /// <summary>
        /// The multiplier of the vector division by 31: <c>ceil(2^20 / 31)</c>.
        /// </summary>
        private const uint Divide31Multiplier = ((1U << Divide31Shift) + 30) / 31;

        /// <summary>
        /// Packs pixels with four 8-bit channels to <see cref="Bgra5551"/> pixels, as <see cref="Bgra5551.FromRgba32(Rgba32)"/>.
        /// </summary>
        /// <remarks>
        /// Each packed value fits 16 bits. Thus a narrow of two 32-bit registers keeps the pixel order, and one store
        /// writes the pixels of two loads.
        /// </remarks>
        /// <param name="source">The source pixels, 4 bytes each.</param>
        /// <param name="destination">The destination pixels, 2 bytes each. The caller checks the length.</param>
        /// <param name="redShift">The bit position of red in a source pixel read as a little-endian uint.</param>
        /// <param name="greenShift">The bit position of green in a source pixel.</param>
        /// <param name="blueShift">The bit position of blue in a source pixel.</param>
        /// <param name="alphaShift">The bit position of alpha in a source pixel.</param>
        public static void PackFrom4Bytes(
            ReadOnlySpan<byte> source,
            Span<byte> destination,
            int redShift,
            int greenShift,
            int blueShift,
            int alphaShift)
        {
            ref uint sourceBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(source));
            ref ushort destinationBase = ref Unsafe.As<byte, ushort>(ref MemoryMarshal.GetReference(destination));
            nuint length = (uint)source.Length / 4;
            nuint x = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                for (; x + (uint)Vector512<ushort>.Count <= length; x += (uint)Vector512<ushort>.Count)
                {
                    Vector512<uint> lower = PackLanes(Vector512.LoadUnsafe(ref sourceBase, x), redShift, greenShift, blueShift, alphaShift);
                    Vector512<uint> upper = PackLanes(
                        Vector512.LoadUnsafe(ref sourceBase, x + (uint)Vector512<uint>.Count),
                        redShift,
                        greenShift,
                        blueShift,
                        alphaShift);
                    Vector512.Narrow(lower, upper).StoreUnsafe(ref destinationBase, x);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x + (uint)Vector256<ushort>.Count <= length; x += (uint)Vector256<ushort>.Count)
                {
                    Vector256<uint> lower = PackLanes(Vector256.LoadUnsafe(ref sourceBase, x), redShift, greenShift, blueShift, alphaShift);
                    Vector256<uint> upper = PackLanes(
                        Vector256.LoadUnsafe(ref sourceBase, x + (uint)Vector256<uint>.Count),
                        redShift,
                        greenShift,
                        blueShift,
                        alphaShift);
                    Vector256.Narrow(lower, upper).StoreUnsafe(ref destinationBase, x);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x + (uint)Vector128<ushort>.Count <= length; x += (uint)Vector128<ushort>.Count)
                {
                    Vector128<uint> lower = PackLanes(Vector128.LoadUnsafe(ref sourceBase, x), redShift, greenShift, blueShift, alphaShift);
                    Vector128<uint> upper = PackLanes(
                        Vector128.LoadUnsafe(ref sourceBase, x + (uint)Vector128<uint>.Count),
                        redShift,
                        greenShift,
                        blueShift,
                        alphaShift);
                    Vector128.Narrow(lower, upper).StoreUnsafe(ref destinationBase, x);
                }
            }

            for (; x < length; x++)
            {
                uint pixel = Unsafe.Add(ref sourceBase, x);
                Rgba32 rgba = new((byte)(pixel >> redShift), (byte)(pixel >> greenShift), (byte)(pixel >> blueShift), (byte)(pixel >> alphaShift));
                Unsafe.Add(ref destinationBase, x) = Bgra5551.FromRgba32(rgba).PackedValue;
            }
        }

        /// <summary>
        /// Packs pixels with three 8-bit channels to opaque <see cref="Bgra5551"/> pixels, as
        /// <see cref="Bgra5551.FromRgba32(Rgba32)"/>.
        /// </summary>
        /// <remarks>
        /// Each iteration loads three full registers of pixels. <c>SimdUtils.Spread3To4</c> spreads them to four
        /// registers of 32-bit lanes, and the 4-byte lane arithmetic packs them.
        /// </remarks>
        /// <param name="source">The source pixels, 3 bytes each.</param>
        /// <param name="destination">The destination pixels, 2 bytes each. The caller checks the length.</param>
        /// <param name="redShift">The bit position of red in a source pixel read as a little-endian uint.</param>
        /// <param name="greenShift">The bit position of green in a source pixel.</param>
        /// <param name="blueShift">The bit position of blue in a source pixel.</param>
        public static void PackFrom3Bytes(ReadOnlySpan<byte> source, Span<byte> destination, int redShift, int greenShift, int blueShift)
        {
            ref byte sourceBase = ref MemoryMarshal.GetReference(source);
            ref ushort destinationBase = ref Unsafe.As<byte, ushort>(ref MemoryMarshal.GetReference(destination));
            nuint length = (uint)source.Length / 3;
            nuint x = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                Vector512<uint> opaque = Vector512.Create(0xFF000000U);

                // Three full loads take 64 pixels. The spread gives four registers of 16 pixels in 32-bit lanes,
                // and each narrow joins two packed registers to one register of 32 pixels.
                for (; (x * 3) + 192 <= (uint)source.Length; x += 64)
                {
                    ref byte source0 = ref Unsafe.Add(ref sourceBase, x * 3);
                    SimdUtils.Spread3To4(
                        Vector512.LoadUnsafe(ref source0),
                        Vector512.LoadUnsafe(ref source0, 64),
                        Vector512.LoadUnsafe(ref source0, 128),
                        out Vector512<byte> pixels0,
                        out Vector512<byte> pixels1,
                        out Vector512<byte> pixels2,
                        out Vector512<byte> pixels3);

                    Vector512<uint> packed0 = PackLanes(pixels0.AsUInt32() | opaque, redShift, greenShift, blueShift, 24);
                    Vector512<uint> packed1 = PackLanes(pixels1.AsUInt32() | opaque, redShift, greenShift, blueShift, 24);
                    Vector512<uint> packed2 = PackLanes(pixels2.AsUInt32() | opaque, redShift, greenShift, blueShift, 24);
                    Vector512<uint> packed3 = PackLanes(pixels3.AsUInt32() | opaque, redShift, greenShift, blueShift, 24);
                    Vector512.Narrow(packed0, packed1).StoreUnsafe(ref destinationBase, x);
                    Vector512.Narrow(packed2, packed3).StoreUnsafe(ref destinationBase, x + 32);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                Vector256<uint> opaque = Vector256.Create(0xFF000000U);

                // Three full loads take 32 pixels. The spread gives four registers of 8 pixels in 32-bit lanes,
                // and each narrow joins two packed registers to one register of 16 pixels.
                for (; (x * 3) + 96 <= (uint)source.Length; x += 32)
                {
                    ref byte source0 = ref Unsafe.Add(ref sourceBase, x * 3);
                    SimdUtils.Spread3To4(
                        Vector256.LoadUnsafe(ref source0),
                        Vector256.LoadUnsafe(ref source0, 32),
                        Vector256.LoadUnsafe(ref source0, 64),
                        out Vector256<byte> pixels0,
                        out Vector256<byte> pixels1,
                        out Vector256<byte> pixels2,
                        out Vector256<byte> pixels3);

                    Vector256<uint> packed0 = PackLanes(pixels0.AsUInt32() | opaque, redShift, greenShift, blueShift, 24);
                    Vector256<uint> packed1 = PackLanes(pixels1.AsUInt32() | opaque, redShift, greenShift, blueShift, 24);
                    Vector256<uint> packed2 = PackLanes(pixels2.AsUInt32() | opaque, redShift, greenShift, blueShift, 24);
                    Vector256<uint> packed3 = PackLanes(pixels3.AsUInt32() | opaque, redShift, greenShift, blueShift, 24);
                    Vector256.Narrow(packed0, packed1).StoreUnsafe(ref destinationBase, x);
                    Vector256.Narrow(packed2, packed3).StoreUnsafe(ref destinationBase, x + 16);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<uint> opaque = Vector128.Create(0xFF000000U);

                // Three full loads take 16 pixels. The spread gives four registers of 4 pixels in 32-bit lanes,
                // and each narrow joins two packed registers to one register of 8 pixels.
                for (; (x * 3) + 48 <= (uint)source.Length; x += 16)
                {
                    ref byte source0 = ref Unsafe.Add(ref sourceBase, x * 3);
                    SimdUtils.Spread3To4(
                        Vector128.LoadUnsafe(ref source0),
                        Vector128.LoadUnsafe(ref source0, 16),
                        Vector128.LoadUnsafe(ref source0, 32),
                        out Vector128<byte> pixels0,
                        out Vector128<byte> pixels1,
                        out Vector128<byte> pixels2,
                        out Vector128<byte> pixels3);

                    Vector128<uint> packed0 = PackLanes(pixels0.AsUInt32() | opaque, redShift, greenShift, blueShift, 24);
                    Vector128<uint> packed1 = PackLanes(pixels1.AsUInt32() | opaque, redShift, greenShift, blueShift, 24);
                    Vector128<uint> packed2 = PackLanes(pixels2.AsUInt32() | opaque, redShift, greenShift, blueShift, 24);
                    Vector128<uint> packed3 = PackLanes(pixels3.AsUInt32() | opaque, redShift, greenShift, blueShift, 24);
                    Vector128.Narrow(packed0, packed1).StoreUnsafe(ref destinationBase, x);
                    Vector128.Narrow(packed2, packed3).StoreUnsafe(ref destinationBase, x + 8);
                }
            }

            for (; x < length; x++)
            {
                ref byte pixelBase = ref Unsafe.Add(ref sourceBase, x * 3);
                uint pixel = pixelBase | ((uint)Unsafe.Add(ref pixelBase, 1) << 8) | ((uint)Unsafe.Add(ref pixelBase, 2) << 16);
                Rgba32 rgba = new((byte)(pixel >> redShift), (byte)(pixel >> greenShift), (byte)(pixel >> blueShift), byte.MaxValue);
                Unsafe.Add(ref destinationBase, x) = Bgra5551.FromRgba32(rgba).PackedValue;
            }
        }

        /// <summary>
        /// Unpacks <see cref="Bgra5551"/> pixels to pixels with four 8-bit channels, as <see cref="Bgra5551.ToRgba32()"/>.
        /// </summary>
        /// <remarks>
        /// One load reads a register of 16-bit pixels. Its two halves widen to 32-bit lanes in pixel order, and each half
        /// fills one register of destination pixels.
        /// </remarks>
        /// <param name="source">The source pixels, 2 bytes each.</param>
        /// <param name="destination">The destination pixels, 4 bytes each. The caller checks the length.</param>
        /// <param name="redShift">The bit position of red in a destination pixel read as a little-endian uint.</param>
        /// <param name="greenShift">The bit position of green in a destination pixel.</param>
        /// <param name="blueShift">The bit position of blue in a destination pixel.</param>
        /// <param name="alphaShift">The bit position of alpha in a destination pixel.</param>
        public static void UnpackTo4Bytes(
            ReadOnlySpan<byte> source,
            Span<byte> destination,
            int redShift,
            int greenShift,
            int blueShift,
            int alphaShift)
        {
            ref ushort sourceBase = ref Unsafe.As<byte, ushort>(ref MemoryMarshal.GetReference(source));
            ref uint destinationBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(destination));
            nuint length = (uint)source.Length / 2;
            nuint x = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                for (; x + (uint)Vector512<ushort>.Count <= length; x += (uint)Vector512<ushort>.Count)
                {
                    Vector512<ushort> packed = Vector512.LoadUnsafe(ref sourceBase, x);
                    UnpackLanes(Vector512.WidenLower(packed), redShift, greenShift, blueShift, alphaShift).StoreUnsafe(ref destinationBase, x);
                    UnpackLanes(Vector512.WidenUpper(packed), redShift, greenShift, blueShift, alphaShift)
                        .StoreUnsafe(ref destinationBase, x + (uint)Vector512<uint>.Count);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x + (uint)Vector256<ushort>.Count <= length; x += (uint)Vector256<ushort>.Count)
                {
                    Vector256<ushort> packed = Vector256.LoadUnsafe(ref sourceBase, x);
                    UnpackLanes(Vector256.WidenLower(packed), redShift, greenShift, blueShift, alphaShift).StoreUnsafe(ref destinationBase, x);
                    UnpackLanes(Vector256.WidenUpper(packed), redShift, greenShift, blueShift, alphaShift)
                        .StoreUnsafe(ref destinationBase, x + (uint)Vector256<uint>.Count);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x + (uint)Vector128<ushort>.Count <= length; x += (uint)Vector128<ushort>.Count)
                {
                    Vector128<ushort> packed = Vector128.LoadUnsafe(ref sourceBase, x);
                    UnpackLanes(Vector128.WidenLower(packed), redShift, greenShift, blueShift, alphaShift).StoreUnsafe(ref destinationBase, x);
                    UnpackLanes(Vector128.WidenUpper(packed), redShift, greenShift, blueShift, alphaShift)
                        .StoreUnsafe(ref destinationBase, x + (uint)Vector128<uint>.Count);
                }
            }

            for (; x < length; x++)
            {
                Rgba32 rgba = new Bgra5551(Unsafe.Add(ref sourceBase, x)).ToRgba32();
                Unsafe.Add(ref destinationBase, x) = ((uint)rgba.R << redShift)
                    | ((uint)rgba.G << greenShift)
                    | ((uint)rgba.B << blueShift)
                    | ((uint)rgba.A << alphaShift);
            }
        }

        /// <summary>
        /// Unpacks <see cref="Bgra5551"/> pixels to pixels with three 8-bit channels, as <see cref="Bgra5551.ToRgba32()"/>.
        /// Alpha is not kept.
        /// </summary>
        /// <remarks>
        /// The 4-byte lane arithmetic unpacks the pixels to four registers of 32-bit lanes. <c>SimdUtils.Join4To3</c>
        /// joins them to three full registers of 3-byte pixels.
        /// </remarks>
        /// <param name="source">The source pixels, 2 bytes each.</param>
        /// <param name="destination">The destination pixels, 3 bytes each. The caller checks the length.</param>
        /// <param name="redShift">The bit position of red in a destination pixel read as a little-endian uint.</param>
        /// <param name="greenShift">The bit position of green in a destination pixel.</param>
        /// <param name="blueShift">The bit position of blue in a destination pixel.</param>
        public static void UnpackTo3Bytes(ReadOnlySpan<byte> source, Span<byte> destination, int redShift, int greenShift, int blueShift)
        {
            ref ushort sourceBase = ref Unsafe.As<byte, ushort>(ref MemoryMarshal.GetReference(source));
            ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
            nuint length = (uint)source.Length / 2;
            nuint x = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                // Two loads take 64 pixels. Each widens to two registers of 16 pixels in 32-bit lanes, and the join
                // gives three full registers of 3-byte pixels.
                for (; x + 64 <= length; x += 64)
                {
                    Vector512<ushort> packed0 = Vector512.LoadUnsafe(ref sourceBase, x);
                    Vector512<ushort> packed1 = Vector512.LoadUnsafe(ref sourceBase, x + 32);
                    SimdUtils.Join4To3(
                        UnpackLanes(Vector512.WidenLower(packed0), redShift, greenShift, blueShift, 24).AsByte(),
                        UnpackLanes(Vector512.WidenUpper(packed0), redShift, greenShift, blueShift, 24).AsByte(),
                        UnpackLanes(Vector512.WidenLower(packed1), redShift, greenShift, blueShift, 24).AsByte(),
                        UnpackLanes(Vector512.WidenUpper(packed1), redShift, greenShift, blueShift, 24).AsByte(),
                        out Vector512<byte> destination0,
                        out Vector512<byte> destination1,
                        out Vector512<byte> destination2);

                    ref byte destinationRow = ref Unsafe.Add(ref destinationBase, x * 3);
                    destination0.StoreUnsafe(ref destinationRow);
                    destination1.StoreUnsafe(ref destinationRow, 64);
                    destination2.StoreUnsafe(ref destinationRow, 128);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                // Two loads take 32 pixels. Each widens to two registers of 8 pixels in 32-bit lanes, and the join
                // gives three full registers of 3-byte pixels.
                for (; x + 32 <= length; x += 32)
                {
                    Vector256<ushort> packed0 = Vector256.LoadUnsafe(ref sourceBase, x);
                    Vector256<ushort> packed1 = Vector256.LoadUnsafe(ref sourceBase, x + 16);
                    SimdUtils.Join4To3(
                        UnpackLanes(Vector256.WidenLower(packed0), redShift, greenShift, blueShift, 24).AsByte(),
                        UnpackLanes(Vector256.WidenUpper(packed0), redShift, greenShift, blueShift, 24).AsByte(),
                        UnpackLanes(Vector256.WidenLower(packed1), redShift, greenShift, blueShift, 24).AsByte(),
                        UnpackLanes(Vector256.WidenUpper(packed1), redShift, greenShift, blueShift, 24).AsByte(),
                        out Vector256<byte> destination0,
                        out Vector256<byte> destination1,
                        out Vector256<byte> destination2);

                    ref byte destinationRow = ref Unsafe.Add(ref destinationBase, x * 3);
                    destination0.StoreUnsafe(ref destinationRow);
                    destination1.StoreUnsafe(ref destinationRow, 32);
                    destination2.StoreUnsafe(ref destinationRow, 64);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                // Two loads take 16 pixels. Each widens to two registers of 4 pixels in 32-bit lanes, and the join
                // gives three full registers of 3-byte pixels.
                for (; x + 16 <= length; x += 16)
                {
                    Vector128<ushort> packed0 = Vector128.LoadUnsafe(ref sourceBase, x);
                    Vector128<ushort> packed1 = Vector128.LoadUnsafe(ref sourceBase, x + 8);
                    SimdUtils.Join4To3(
                        UnpackLanes(Vector128.WidenLower(packed0), redShift, greenShift, blueShift, 24).AsByte(),
                        UnpackLanes(Vector128.WidenUpper(packed0), redShift, greenShift, blueShift, 24).AsByte(),
                        UnpackLanes(Vector128.WidenLower(packed1), redShift, greenShift, blueShift, 24).AsByte(),
                        UnpackLanes(Vector128.WidenUpper(packed1), redShift, greenShift, blueShift, 24).AsByte(),
                        out Vector128<byte> destination0,
                        out Vector128<byte> destination1,
                        out Vector128<byte> destination2);

                    ref byte destinationRow = ref Unsafe.Add(ref destinationBase, x * 3);
                    destination0.StoreUnsafe(ref destinationRow);
                    destination1.StoreUnsafe(ref destinationRow, 16);
                    destination2.StoreUnsafe(ref destinationRow, 32);
                }
            }

            for (; x < length; x++)
            {
                Rgba32 rgba = new Bgra5551(Unsafe.Add(ref sourceBase, x)).ToRgba32();
                uint pixel = ((uint)rgba.R << redShift) | ((uint)rgba.G << greenShift) | ((uint)rgba.B << blueShift);

                ref byte pixelBase = ref Unsafe.Add(ref destinationBase, x * 3);
                pixelBase = (byte)pixel;
                Unsafe.Add(ref pixelBase, 1) = (byte)(pixel >> 8);
                Unsafe.Add(ref pixelBase, 2) = (byte)(pixel >> 16);
            }
        }

        /// <summary>
        /// Packs the 4-byte pixel of each lane to a <see cref="Bgra5551"/> value in the low 16 bits of the lane.
        /// </summary>
        /// <param name="pixels">The source pixels, one in each lane.</param>
        /// <param name="redShift">The bit position of red.</param>
        /// <param name="greenShift">The bit position of green.</param>
        /// <param name="blueShift">The bit position of blue.</param>
        /// <param name="alphaShift">The bit position of alpha.</param>
        /// <returns>The packed values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<uint> PackLanes(Vector512<uint> pixels, int redShift, int greenShift, int blueShift, int alphaShift)
        {
            // Shift each channel to bits 0..7 and mask it. Alpha bit 7 is the 1-bit alpha.
            Vector512<uint> mask = Vector512.Create(0xFFU);
            return Vector512.ShiftLeft(Scale8To5(Vector512.ShiftRightLogical(pixels, redShift) & mask), 10)
                | Vector512.ShiftLeft(Scale8To5(Vector512.ShiftRightLogical(pixels, greenShift) & mask), 5)
                | Scale8To5(Vector512.ShiftRightLogical(pixels, blueShift) & mask)
                | Vector512.ShiftLeft(Vector512.ShiftRightLogical(pixels, alphaShift + 7) & Vector512<uint>.One, 15);
        }

        /// <inheritdoc cref="PackLanes(Vector512{uint}, int, int, int, int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<uint> PackLanes(Vector256<uint> pixels, int redShift, int greenShift, int blueShift, int alphaShift)
        {
            Vector256<uint> mask = Vector256.Create(0xFFU);
            return Vector256.ShiftLeft(Scale8To5(Vector256.ShiftRightLogical(pixels, redShift) & mask), 10)
                | Vector256.ShiftLeft(Scale8To5(Vector256.ShiftRightLogical(pixels, greenShift) & mask), 5)
                | Scale8To5(Vector256.ShiftRightLogical(pixels, blueShift) & mask)
                | Vector256.ShiftLeft(Vector256.ShiftRightLogical(pixels, alphaShift + 7) & Vector256<uint>.One, 15);
        }

        /// <inheritdoc cref="PackLanes(Vector512{uint}, int, int, int, int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<uint> PackLanes(Vector128<uint> pixels, int redShift, int greenShift, int blueShift, int alphaShift)
        {
            Vector128<uint> mask = Vector128.Create(0xFFU);
            return Vector128.ShiftLeft(Scale8To5(Vector128.ShiftRightLogical(pixels, redShift) & mask), 10)
                | Vector128.ShiftLeft(Scale8To5(Vector128.ShiftRightLogical(pixels, greenShift) & mask), 5)
                | Scale8To5(Vector128.ShiftRightLogical(pixels, blueShift) & mask)
                | Vector128.ShiftLeft(Vector128.ShiftRightLogical(pixels, alphaShift + 7) & Vector128<uint>.One, 15);
        }

        /// <summary>
        /// Unpacks the <see cref="Bgra5551"/> value in the low 16 bits of each lane to a 4-byte pixel.
        /// </summary>
        /// <param name="packed">The packed values, one in each lane.</param>
        /// <param name="redShift">The bit position of red.</param>
        /// <param name="greenShift">The bit position of green.</param>
        /// <param name="blueShift">The bit position of blue.</param>
        /// <param name="alphaShift">The bit position of alpha.</param>
        /// <returns>The 4-byte pixels.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<uint> UnpackLanes(Vector512<uint> packed, int redShift, int greenShift, int blueShift, int alphaShift)
        {
            // Each color is 5 bits at bit 10, 5 or 0. Bit 15 is alpha, which becomes 0 or 255.
            Vector512<uint> mask = Vector512.Create(0x1FU);
            return Vector512.ShiftLeft(Scale5To8(Vector512.ShiftRightLogical(packed, 10) & mask), redShift)
                | Vector512.ShiftLeft(Scale5To8(Vector512.ShiftRightLogical(packed, 5) & mask), greenShift)
                | Vector512.ShiftLeft(Scale5To8(packed & mask), blueShift)
                | Vector512.ShiftLeft((Vector512.ShiftRightLogical(packed, 15) & Vector512<uint>.One) * 255, alphaShift);
        }

        /// <inheritdoc cref="UnpackLanes(Vector512{uint}, int, int, int, int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<uint> UnpackLanes(Vector256<uint> packed, int redShift, int greenShift, int blueShift, int alphaShift)
        {
            Vector256<uint> mask = Vector256.Create(0x1FU);
            return Vector256.ShiftLeft(Scale5To8(Vector256.ShiftRightLogical(packed, 10) & mask), redShift)
                | Vector256.ShiftLeft(Scale5To8(Vector256.ShiftRightLogical(packed, 5) & mask), greenShift)
                | Vector256.ShiftLeft(Scale5To8(packed & mask), blueShift)
                | Vector256.ShiftLeft((Vector256.ShiftRightLogical(packed, 15) & Vector256<uint>.One) * 255, alphaShift);
        }

        /// <inheritdoc cref="UnpackLanes(Vector512{uint}, int, int, int, int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<uint> UnpackLanes(Vector128<uint> packed, int redShift, int greenShift, int blueShift, int alphaShift)
        {
            Vector128<uint> mask = Vector128.Create(0x1FU);
            return Vector128.ShiftLeft(Scale5To8(Vector128.ShiftRightLogical(packed, 10) & mask), redShift)
                | Vector128.ShiftLeft(Scale5To8(Vector128.ShiftRightLogical(packed, 5) & mask), greenShift)
                | Vector128.ShiftLeft(Scale5To8(packed & mask), blueShift)
                | Vector128.ShiftLeft((Vector128.ShiftRightLogical(packed, 15) & Vector128<uint>.One) * 255, alphaShift);
        }

        /// <summary>
        /// Scales 8-bit values to 5-bit values: <c>round(value * 31 / 255)</c>, the same rounding as
        /// <see cref="Bgra5551.FromRgba32(Rgba32)"/>.
        /// </summary>
        /// <param name="value">The 8-bit values, one in each lane.</param>
        /// <returns>The 5-bit values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<uint> Scale8To5(Vector512<uint> value)
            => Vector512.ShiftRightLogical(((value * 31) + Vector512.Create(127U)) * Divide255Multiplier, Divide255Shift);

        /// <inheritdoc cref="Scale8To5(Vector512{uint})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<uint> Scale8To5(Vector256<uint> value)
            => Vector256.ShiftRightLogical(((value * 31) + Vector256.Create(127U)) * Divide255Multiplier, Divide255Shift);

        /// <inheritdoc cref="Scale8To5(Vector512{uint})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<uint> Scale8To5(Vector128<uint> value)
            => Vector128.ShiftRightLogical(((value * 31) + Vector128.Create(127U)) * Divide255Multiplier, Divide255Shift);

        /// <summary>
        /// Scales 5-bit values to 8-bit values: <c>round(value * 255 / 31)</c>, the same rounding as
        /// <see cref="Bgra5551.ToRgba32()"/>.
        /// </summary>
        /// <param name="value">The 5-bit values, one in each lane.</param>
        /// <returns>The 8-bit values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<uint> Scale5To8(Vector512<uint> value)
            => Vector512.ShiftRightLogical(((value * 255) + Vector512.Create(15U)) * Divide31Multiplier, Divide31Shift);

        /// <inheritdoc cref="Scale5To8(Vector512{uint})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<uint> Scale5To8(Vector256<uint> value)
            => Vector256.ShiftRightLogical(((value * 255) + Vector256.Create(15U)) * Divide31Multiplier, Divide31Shift);

        /// <inheritdoc cref="Scale5To8(Vector512{uint})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<uint> Scale5To8(Vector128<uint> value)
            => Vector128.ShiftRightLogical(((value * 255) + Vector128.Create(15U)) * Divide31Multiplier, Divide31Shift);
    }
}
