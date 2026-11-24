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
    // The kernels divide by the constant 31, 63 or 255 with one multiply and one shift: (x * ceil(2^23 / d)) >> 23.
    // A vector integer division does more work. On x86, the JIT checks the divisor for zero, converts the lanes to
    // double, divides and converts back.
    // (x * ceil(2^23 / d)) >> 23 equals x / d for each x below 364745 (d = 31), 270647 (d = 63) and 66299 (d = 255).
    // x is value * destinationMax + d / 2. Its largest value is 31 * 255 + 15 = 7920 for d = 31. It is
    // 63 * 255 + 31 = 16096 for d = 63, and 255 * 63 + 127 = 16192 for d = 255. Thus each division is exact. The
    // largest products are 7920 * 270601, 16096 * 133153 and 16192 * 32897. Each is less than 2^32, so the multiply
    // does not overflow.

    /// <summary>
    /// The shift of the vector division in <see cref="ScaleUnorm(Vector512{uint}, uint, uint)"/>.
    /// </summary>
    private const int UnormShift = 23;

    /// <summary>
    /// Defines how a 16-bit layout stores the channels of a pixel, for <see cref="Packed16Kernels{TLayout}"/>.
    /// </summary>
    /// <remarks>
    /// The vector forms hold one pixel in each 32-bit lane. A packed value uses the low 16 bits of its lane, and the
    /// other bits are 0. Each vector form gives the same values as the scalar form, which the pixel type owns.
    /// </remarks>
    private interface IPacked16Layout
    {
        /// <summary>
        /// Packs 8-bit channel values to the layout.
        /// </summary>
        /// <param name="red">The red values, from 0 to 255.</param>
        /// <param name="green">The green values, from 0 to 255.</param>
        /// <param name="blue">The blue values, from 0 to 255.</param>
        /// <param name="alpha">The alpha values, from 0 to 255.</param>
        /// <returns>The packed values.</returns>
        static abstract Vector512<uint> Pack(Vector512<uint> red, Vector512<uint> green, Vector512<uint> blue, Vector512<uint> alpha);

        /// <inheritdoc cref="Pack(Vector512{uint}, Vector512{uint}, Vector512{uint}, Vector512{uint})"/>
        static abstract Vector256<uint> Pack(Vector256<uint> red, Vector256<uint> green, Vector256<uint> blue, Vector256<uint> alpha);

        /// <inheritdoc cref="Pack(Vector512{uint}, Vector512{uint}, Vector512{uint}, Vector512{uint})"/>
        static abstract Vector128<uint> Pack(Vector128<uint> red, Vector128<uint> green, Vector128<uint> blue, Vector128<uint> alpha);

        /// <summary>
        /// Unpacks the layout to 4-byte pixels.
        /// </summary>
        /// <typeparam name="TOrder">The channel order of the 4-byte pixels.</typeparam>
        /// <param name="packed">The packed values.</param>
        /// <returns>The 4-byte pixels.</returns>
        static abstract Vector512<uint> Unpack<TOrder>(Vector512<uint> packed)
            where TOrder : IChannelOrder;

        /// <inheritdoc cref="Unpack{TOrder}(Vector512{uint})"/>
        static abstract Vector256<uint> Unpack<TOrder>(Vector256<uint> packed)
            where TOrder : IChannelOrder;

        /// <inheritdoc cref="Unpack{TOrder}(Vector512{uint})"/>
        static abstract Vector128<uint> Unpack<TOrder>(Vector128<uint> packed)
            where TOrder : IChannelOrder;

        /// <summary>
        /// Packs one pixel to the layout.
        /// </summary>
        /// <param name="source">The pixel.</param>
        /// <returns>The packed value.</returns>
        static abstract ushort Pack(Rgba32 source);

        /// <summary>
        /// Unpacks one pixel of the layout.
        /// </summary>
        /// <param name="packed">The packed value.</param>
        /// <returns>The pixel.</returns>
        static abstract Rgba32 Unpack(ushort packed);
    }

    /// <summary>
    /// Defines the bit positions of the channels in a 4-byte pixel read as a little-endian <see cref="uint"/>, for
    /// <see cref="Packed16Kernels{TLayout}"/>.
    /// </summary>
    /// <remarks>
    /// Each position is a constant, so the JIT encodes each shift of a kernel loop as an immediate operand. A 3-byte pixel
    /// uses the order of its first three bytes. The kernels spread it to a 4-byte lane with alpha in the fourth byte.
    /// The name of each order gives the channel at each byte, from byte 0 to byte 3. X, Y, Z and W are red, green, blue
    /// and alpha.
    /// </remarks>
    private interface IChannelOrder
    {
        /// <summary>
        /// Gets the bit position of red.
        /// </summary>
        static abstract int RedShift { get; }

        /// <summary>
        /// Gets the bit position of green.
        /// </summary>
        static abstract int GreenShift { get; }

        /// <summary>
        /// Gets the bit position of blue.
        /// </summary>
        static abstract int BlueShift { get; }

        /// <summary>
        /// Gets the bit position of alpha.
        /// </summary>
        static abstract int AlphaShift { get; }
    }

    /// <summary>
    /// Scales unsigned normalized values to another bit depth, as <see cref="ColorNumerics.ScaleUnorm(uint, uint, uint)"/>.
    /// Each maximum is 31, 63 or 255, and one of them is 255.
    /// </summary>
    /// <param name="value">The values, from 0 to <paramref name="sourceMax"/>, one in each lane.</param>
    /// <param name="sourceMax">The maximum value at the source bit depth.</param>
    /// <param name="destinationMax">The maximum value at the destination bit depth.</param>
    /// <returns>The scaled values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<uint> ScaleUnorm(Vector512<uint> value, uint sourceMax, uint destinationMax)
        => Vector512.ShiftRightLogical(
            ((value * destinationMax) + Vector512.Create(sourceMax / 2)) * (((1U << UnormShift) + sourceMax - 1) / sourceMax),
            UnormShift);

    /// <inheritdoc cref="ScaleUnorm(Vector512{uint}, uint, uint)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<uint> ScaleUnorm(Vector256<uint> value, uint sourceMax, uint destinationMax)
        => Vector256.ShiftRightLogical(
            ((value * destinationMax) + Vector256.Create(sourceMax / 2)) * (((1U << UnormShift) + sourceMax - 1) / sourceMax),
            UnormShift);

    /// <inheritdoc cref="ScaleUnorm(Vector512{uint}, uint, uint)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> ScaleUnorm(Vector128<uint> value, uint sourceMax, uint destinationMax)
        => Vector128.ShiftRightLogical(
            ((value * destinationMax) + Vector128.Create(sourceMax / 2)) * (((1U << UnormShift) + sourceMax - 1) / sourceMax),
            UnormShift);

    /// <summary>
    /// The <see cref="Bgra5551"/> layout: blue in bits 0..4, green in bits 5..9, red in bits 10..14 and alpha in bit 15.
    /// </summary>
    private readonly struct Bgra5551Layout : IPacked16Layout
    {
        /// <inheritdoc/>
        /// <remarks>
        /// The 1-bit alpha is <c>round(a / 255)</c>, which is 1 for 128 or more. Thus bit 7 of alpha moves to bit 15.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<uint> Pack(Vector512<uint> red, Vector512<uint> green, Vector512<uint> blue, Vector512<uint> alpha)
            => Vector512.ShiftLeft(ScaleUnorm(red, 255, 31), 10)
                | Vector512.ShiftLeft(ScaleUnorm(green, 255, 31), 5)
                | ScaleUnorm(blue, 255, 31)
                | Vector512.ShiftLeft(alpha & Vector512.Create(0x80U), 8);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<uint> Pack(Vector256<uint> red, Vector256<uint> green, Vector256<uint> blue, Vector256<uint> alpha)
            => Vector256.ShiftLeft(ScaleUnorm(red, 255, 31), 10)
                | Vector256.ShiftLeft(ScaleUnorm(green, 255, 31), 5)
                | ScaleUnorm(blue, 255, 31)
                | Vector256.ShiftLeft(alpha & Vector256.Create(0x80U), 8);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<uint> Pack(Vector128<uint> red, Vector128<uint> green, Vector128<uint> blue, Vector128<uint> alpha)
            => Vector128.ShiftLeft(ScaleUnorm(red, 255, 31), 10)
                | Vector128.ShiftLeft(ScaleUnorm(green, 255, 31), 5)
                | ScaleUnorm(blue, 255, 31)
                | Vector128.ShiftLeft(alpha & Vector128.Create(0x80U), 8);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<uint> Unpack<TOrder>(Vector512<uint> packed)
            where TOrder : IChannelOrder
        {
            // Each color is 5 bits at bit 10, 5 or 0. Bit 15 is alpha, which becomes 0 or 255.
            Vector512<uint> mask = Vector512.Create(0x1FU);
            return Vector512.ShiftLeft(ScaleUnorm(Vector512.ShiftRightLogical(packed, 10) & mask, 31, 255), TOrder.RedShift)
                | Vector512.ShiftLeft(ScaleUnorm(Vector512.ShiftRightLogical(packed, 5) & mask, 31, 255), TOrder.GreenShift)
                | Vector512.ShiftLeft(ScaleUnorm(packed & mask, 31, 255), TOrder.BlueShift)
                | Vector512.ShiftLeft((Vector512.ShiftRightLogical(packed, 15) & Vector512<uint>.One) * 255, TOrder.AlphaShift);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<uint> Unpack<TOrder>(Vector256<uint> packed)
            where TOrder : IChannelOrder
        {
            Vector256<uint> mask = Vector256.Create(0x1FU);
            return Vector256.ShiftLeft(ScaleUnorm(Vector256.ShiftRightLogical(packed, 10) & mask, 31, 255), TOrder.RedShift)
                | Vector256.ShiftLeft(ScaleUnorm(Vector256.ShiftRightLogical(packed, 5) & mask, 31, 255), TOrder.GreenShift)
                | Vector256.ShiftLeft(ScaleUnorm(packed & mask, 31, 255), TOrder.BlueShift)
                | Vector256.ShiftLeft((Vector256.ShiftRightLogical(packed, 15) & Vector256<uint>.One) * 255, TOrder.AlphaShift);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<uint> Unpack<TOrder>(Vector128<uint> packed)
            where TOrder : IChannelOrder
        {
            Vector128<uint> mask = Vector128.Create(0x1FU);
            return Vector128.ShiftLeft(ScaleUnorm(Vector128.ShiftRightLogical(packed, 10) & mask, 31, 255), TOrder.RedShift)
                | Vector128.ShiftLeft(ScaleUnorm(Vector128.ShiftRightLogical(packed, 5) & mask, 31, 255), TOrder.GreenShift)
                | Vector128.ShiftLeft(ScaleUnorm(packed & mask, 31, 255), TOrder.BlueShift)
                | Vector128.ShiftLeft((Vector128.ShiftRightLogical(packed, 15) & Vector128<uint>.One) * 255, TOrder.AlphaShift);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort Pack(Rgba32 source) => Bgra5551.FromRgba32(source).PackedValue;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rgba32 Unpack(ushort packed) => new Bgra5551(packed).ToRgba32();
    }

    /// <summary>
    /// The <see cref="Bgr565"/> layout: blue in bits 0..4, green in bits 5..10 and red in bits 11..15.
    /// </summary>
    private readonly struct Bgr565Layout : IPacked16Layout
    {
        /// <inheritdoc/>
        /// <remarks>
        /// The layout does not store alpha.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<uint> Pack(Vector512<uint> red, Vector512<uint> green, Vector512<uint> blue, Vector512<uint> alpha)
            => Vector512.ShiftLeft(ScaleUnorm(red, 255, 31), 11)
                | Vector512.ShiftLeft(ScaleUnorm(green, 255, 63), 5)
                | ScaleUnorm(blue, 255, 31);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<uint> Pack(Vector256<uint> red, Vector256<uint> green, Vector256<uint> blue, Vector256<uint> alpha)
            => Vector256.ShiftLeft(ScaleUnorm(red, 255, 31), 11)
                | Vector256.ShiftLeft(ScaleUnorm(green, 255, 63), 5)
                | ScaleUnorm(blue, 255, 31);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<uint> Pack(Vector128<uint> red, Vector128<uint> green, Vector128<uint> blue, Vector128<uint> alpha)
            => Vector128.ShiftLeft(ScaleUnorm(red, 255, 31), 11)
                | Vector128.ShiftLeft(ScaleUnorm(green, 255, 63), 5)
                | ScaleUnorm(blue, 255, 31);

        /// <inheritdoc/>
        /// <remarks>
        /// Red is the 5 bits at bit 11. These are the highest bits of the packed value, so red needs no mask.
        /// Green is 6 bits at bit 5 and blue is 5 bits at bit 0. The layout does not store alpha, so each pixel is
        /// opaque.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<uint> Unpack<TOrder>(Vector512<uint> packed)
            where TOrder : IChannelOrder
            => Vector512.ShiftLeft(ScaleUnorm(Vector512.ShiftRightLogical(packed, 11), 31, 255), TOrder.RedShift)
                | Vector512.ShiftLeft(ScaleUnorm(Vector512.ShiftRightLogical(packed, 5) & Vector512.Create(0x3FU), 63, 255), TOrder.GreenShift)
                | Vector512.ShiftLeft(ScaleUnorm(packed & Vector512.Create(0x1FU), 31, 255), TOrder.BlueShift)
                | Vector512.ShiftLeft(Vector512.Create(255U), TOrder.AlphaShift);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<uint> Unpack<TOrder>(Vector256<uint> packed)
            where TOrder : IChannelOrder
            => Vector256.ShiftLeft(ScaleUnorm(Vector256.ShiftRightLogical(packed, 11), 31, 255), TOrder.RedShift)
                | Vector256.ShiftLeft(ScaleUnorm(Vector256.ShiftRightLogical(packed, 5) & Vector256.Create(0x3FU), 63, 255), TOrder.GreenShift)
                | Vector256.ShiftLeft(ScaleUnorm(packed & Vector256.Create(0x1FU), 31, 255), TOrder.BlueShift)
                | Vector256.ShiftLeft(Vector256.Create(255U), TOrder.AlphaShift);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<uint> Unpack<TOrder>(Vector128<uint> packed)
            where TOrder : IChannelOrder
            => Vector128.ShiftLeft(ScaleUnorm(Vector128.ShiftRightLogical(packed, 11), 31, 255), TOrder.RedShift)
                | Vector128.ShiftLeft(ScaleUnorm(Vector128.ShiftRightLogical(packed, 5) & Vector128.Create(0x3FU), 63, 255), TOrder.GreenShift)
                | Vector128.ShiftLeft(ScaleUnorm(packed & Vector128.Create(0x1FU), 31, 255), TOrder.BlueShift)
                | Vector128.ShiftLeft(Vector128.Create(255U), TOrder.AlphaShift);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort Pack(Rgba32 source) => Bgr565.FromRgba32(source).PackedValue;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rgba32 Unpack(ushort packed) => new Bgr565(packed).ToRgba32();
    }

    /// <summary>
    /// The channel order of <see cref="Rgba32"/> and <see cref="Rgb24"/>.
    /// </summary>
    private readonly struct XYZWChannelOrder : IChannelOrder
    {
        /// <inheritdoc/>
        public static int RedShift => 0;

        /// <inheritdoc/>
        public static int GreenShift => 8;

        /// <inheritdoc/>
        public static int BlueShift => 16;

        /// <inheritdoc/>
        public static int AlphaShift => 24;
    }

    /// <summary>
    /// The channel order of <see cref="Argb32"/>.
    /// </summary>
    private readonly struct WXYZChannelOrder : IChannelOrder
    {
        /// <inheritdoc/>
        public static int RedShift => 8;

        /// <inheritdoc/>
        public static int GreenShift => 16;

        /// <inheritdoc/>
        public static int BlueShift => 24;

        /// <inheritdoc/>
        public static int AlphaShift => 0;
    }

    /// <summary>
    /// The channel order of <see cref="Bgra32"/> and <see cref="Bgr24"/>.
    /// </summary>
    private readonly struct ZYXWChannelOrder : IChannelOrder
    {
        /// <inheritdoc/>
        public static int RedShift => 16;

        /// <inheritdoc/>
        public static int GreenShift => 8;

        /// <inheritdoc/>
        public static int BlueShift => 0;

        /// <inheritdoc/>
        public static int AlphaShift => 24;
    }

    /// <summary>
    /// The channel order of <see cref="Abgr32"/>.
    /// </summary>
    private readonly struct WZYXChannelOrder : IChannelOrder
    {
        /// <inheritdoc/>
        public static int RedShift => 24;

        /// <inheritdoc/>
        public static int GreenShift => 16;

        /// <inheritdoc/>
        public static int BlueShift => 8;

        /// <inheritdoc/>
        public static int AlphaShift => 0;
    }

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
            => Packed16Kernels<Bgra5551Layout>.PackFrom4Bytes<XYZWChannelOrder>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Rgba32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgr565"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgr565(ReadOnlySpan<byte> source, Span<byte> dest)
            => Packed16Kernels<Bgr565Layout>.PackFrom4Bytes<XYZWChannelOrder>(source, dest);
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
            => Packed16Kernels<Bgra5551Layout>.PackFrom4Bytes<WXYZChannelOrder>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Argb32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgr565"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgr565(ReadOnlySpan<byte> source, Span<byte> dest)
            => Packed16Kernels<Bgr565Layout>.PackFrom4Bytes<WXYZChannelOrder>(source, dest);
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
            => Packed16Kernels<Bgra5551Layout>.PackFrom4Bytes<ZYXWChannelOrder>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgra32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgr565"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgr565(ReadOnlySpan<byte> source, Span<byte> dest)
            => Packed16Kernels<Bgr565Layout>.PackFrom4Bytes<ZYXWChannelOrder>(source, dest);
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
            => Packed16Kernels<Bgra5551Layout>.PackFrom4Bytes<WZYXChannelOrder>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Abgr32"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgr565"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgr565(ReadOnlySpan<byte> source, Span<byte> dest)
            => Packed16Kernels<Bgr565Layout>.PackFrom4Bytes<WZYXChannelOrder>(source, dest);
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
            => Packed16Kernels<Bgra5551Layout>.PackFrom3Bytes<XYZWChannelOrder>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Rgb24"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgr565"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgr565(ReadOnlySpan<byte> source, Span<byte> dest)
            => Packed16Kernels<Bgr565Layout>.PackFrom3Bytes<XYZWChannelOrder>(source, dest);
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
            => Packed16Kernels<Bgra5551Layout>.PackFrom3Bytes<ZYXWChannelOrder>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgr24"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgr565"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgr565(ReadOnlySpan<byte> source, Span<byte> dest)
            => Packed16Kernels<Bgr565Layout>.PackFrom3Bytes<ZYXWChannelOrder>(source, dest);
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
            => Packed16Kernels<Bgra5551Layout>.UnpackTo4Bytes<XYZWChannelOrder>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgra5551"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Argb32"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToArgb32(ReadOnlySpan<byte> source, Span<byte> dest)
            => Packed16Kernels<Bgra5551Layout>.UnpackTo4Bytes<WXYZChannelOrder>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgra5551"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgra32"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgra32(ReadOnlySpan<byte> source, Span<byte> dest)
            => Packed16Kernels<Bgra5551Layout>.UnpackTo4Bytes<ZYXWChannelOrder>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgra5551"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Abgr32"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToAbgr32(ReadOnlySpan<byte> source, Span<byte> dest)
            => Packed16Kernels<Bgra5551Layout>.UnpackTo4Bytes<WZYXChannelOrder>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgra5551"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Rgb24"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToRgb24(ReadOnlySpan<byte> source, Span<byte> dest)
            => Packed16Kernels<Bgra5551Layout>.UnpackTo3Bytes<XYZWChannelOrder>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgra5551"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgr24"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgr24(ReadOnlySpan<byte> source, Span<byte> dest)
            => Packed16Kernels<Bgra5551Layout>.UnpackTo3Bytes<ZYXWChannelOrder>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgra5551"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgr565"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgr565(ReadOnlySpan<byte> source, Span<byte> dest)
            => Packed16Kernels<Bgr565Layout>.PackFrom16<Bgra5551Layout>(source, dest);
    }

    /// <summary>
    /// Optimized converters from <see cref="Bgr565"/>.
    /// </summary>
    public static class FromBgr565
    {
        // Input pixels have blue in bits 0..4, green in bits 5..10 and red in bits 11..15.

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgr565"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Rgba32"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToRgba32(ReadOnlySpan<byte> source, Span<byte> dest)
            => Packed16Kernels<Bgr565Layout>.UnpackTo4Bytes<XYZWChannelOrder>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgr565"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Argb32"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToArgb32(ReadOnlySpan<byte> source, Span<byte> dest)
            => Packed16Kernels<Bgr565Layout>.UnpackTo4Bytes<WXYZChannelOrder>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgr565"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgra32"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgra32(ReadOnlySpan<byte> source, Span<byte> dest)
            => Packed16Kernels<Bgr565Layout>.UnpackTo4Bytes<ZYXWChannelOrder>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgr565"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Abgr32"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToAbgr32(ReadOnlySpan<byte> source, Span<byte> dest)
            => Packed16Kernels<Bgr565Layout>.UnpackTo4Bytes<WZYXChannelOrder>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgr565"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Rgb24"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToRgb24(ReadOnlySpan<byte> source, Span<byte> dest)
            => Packed16Kernels<Bgr565Layout>.UnpackTo3Bytes<XYZWChannelOrder>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgr565"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgr24"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgr24(ReadOnlySpan<byte> source, Span<byte> dest)
            => Packed16Kernels<Bgr565Layout>.UnpackTo3Bytes<ZYXWChannelOrder>(source, dest);

        /// <summary>
        /// Converts a <see cref="ReadOnlySpan{Byte}"/> representing a collection of
        /// <see cref="Bgr565"/> pixels to a <see cref="Span{Byte}"/> representing
        /// a collection of <see cref="Bgra5551"/> pixels.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToBgra5551(ReadOnlySpan<byte> source, Span<byte> dest)
            => Packed16Kernels<Bgra5551Layout>.PackFrom16<Bgr565Layout>(source, dest);
    }

    /// <summary>
    /// Converts pixels of a 16-bit layout to and from the 4-byte layouts, the 3-byte layouts and the other 16-bit layout,
    /// with SIMD.
    /// </summary>
    /// <typeparam name="TLayout">The 16-bit layout.</typeparam>
    private static class Packed16Kernels<TLayout>
        where TLayout : IPacked16Layout
    {
        /// <summary>
        /// Packs pixels with four 8-bit channels to <typeparamref name="TLayout"/> pixels, as
        /// <see cref="IPacked16Layout.Pack(Rgba32)"/>.
        /// </summary>
        /// <remarks>
        /// Each packed value fits 16 bits. Thus the truncating narrow of two 32-bit registers loses no bits. The narrow
        /// keeps the lane order, so one store writes the pixels of two loads.
        /// </remarks>
        /// <typeparam name="TOrder">The channel order of the 4-byte pixels.</typeparam>
        /// <param name="source">The source pixels, 4 bytes each.</param>
        /// <param name="destination">The destination pixels, 2 bytes each. The caller checks the length.</param>
        public static void PackFrom4Bytes<TOrder>(ReadOnlySpan<byte> source, Span<byte> destination)
            where TOrder : IChannelOrder
        {
            ref uint sourceBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(source));
            ref ushort destinationBase = ref Unsafe.As<byte, ushort>(ref MemoryMarshal.GetReference(destination));
            nuint length = (uint)source.Length / 4;
            nuint x = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                for (; x + (uint)Vector512<ushort>.Count <= length; x += (uint)Vector512<ushort>.Count)
                {
                    Vector512<uint> lower = PackLanes<TOrder>(Vector512.LoadUnsafe(ref sourceBase, x));
                    Vector512<uint> upper = PackLanes<TOrder>(Vector512.LoadUnsafe(ref sourceBase, x + (uint)Vector512<uint>.Count));
                    Vector512.Narrow(lower, upper).StoreUnsafe(ref destinationBase, x);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x + (uint)Vector256<ushort>.Count <= length; x += (uint)Vector256<ushort>.Count)
                {
                    Vector256<uint> lower = PackLanes<TOrder>(Vector256.LoadUnsafe(ref sourceBase, x));
                    Vector256<uint> upper = PackLanes<TOrder>(Vector256.LoadUnsafe(ref sourceBase, x + (uint)Vector256<uint>.Count));
                    Vector256.Narrow(lower, upper).StoreUnsafe(ref destinationBase, x);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x + (uint)Vector128<ushort>.Count <= length; x += (uint)Vector128<ushort>.Count)
                {
                    Vector128<uint> lower = PackLanes<TOrder>(Vector128.LoadUnsafe(ref sourceBase, x));
                    Vector128<uint> upper = PackLanes<TOrder>(Vector128.LoadUnsafe(ref sourceBase, x + (uint)Vector128<uint>.Count));
                    Vector128.Narrow(lower, upper).StoreUnsafe(ref destinationBase, x);
                }
            }

            for (; x < length; x++)
            {
                uint pixel = Unsafe.Add(ref sourceBase, x);
                Rgba32 rgba = new(
                    (byte)(pixel >> TOrder.RedShift),
                    (byte)(pixel >> TOrder.GreenShift),
                    (byte)(pixel >> TOrder.BlueShift),
                    (byte)(pixel >> TOrder.AlphaShift));

                Unsafe.Add(ref destinationBase, x) = TLayout.Pack(rgba);
            }
        }

        /// <summary>
        /// Packs pixels with three 8-bit channels to opaque <typeparamref name="TLayout"/> pixels, as
        /// <see cref="IPacked16Layout.Pack(Rgba32)"/>.
        /// </summary>
        /// <remarks>
        /// Each iteration loads three full registers of pixels. <c>SimdUtils.Spread3To4</c> spreads them to four
        /// registers of 32-bit lanes, and the 4-byte lane arithmetic packs them.
        /// </remarks>
        /// <typeparam name="TOrder">The channel order of the 3-byte pixels.</typeparam>
        /// <param name="source">The source pixels, 3 bytes each.</param>
        /// <param name="destination">The destination pixels, 2 bytes each. The caller checks the length.</param>
        public static void PackFrom3Bytes<TOrder>(ReadOnlySpan<byte> source, Span<byte> destination)
            where TOrder : IChannelOrder
        {
            ref byte sourceBase = ref MemoryMarshal.GetReference(source);
            ref ushort destinationBase = ref Unsafe.As<byte, ushort>(ref MemoryMarshal.GetReference(destination));
            nuint length = (uint)source.Length / 3;
            nuint x = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                Vector512<uint> opaque = Vector512.Create(0xFFU << TOrder.AlphaShift);

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

                    Vector512<uint> packed0 = PackLanes<TOrder>(pixels0.AsUInt32() | opaque);
                    Vector512<uint> packed1 = PackLanes<TOrder>(pixels1.AsUInt32() | opaque);
                    Vector512<uint> packed2 = PackLanes<TOrder>(pixels2.AsUInt32() | opaque);
                    Vector512<uint> packed3 = PackLanes<TOrder>(pixels3.AsUInt32() | opaque);
                    Vector512.Narrow(packed0, packed1).StoreUnsafe(ref destinationBase, x);
                    Vector512.Narrow(packed2, packed3).StoreUnsafe(ref destinationBase, x + 32);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                Vector256<uint> opaque = Vector256.Create(0xFFU << TOrder.AlphaShift);

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

                    Vector256<uint> packed0 = PackLanes<TOrder>(pixels0.AsUInt32() | opaque);
                    Vector256<uint> packed1 = PackLanes<TOrder>(pixels1.AsUInt32() | opaque);
                    Vector256<uint> packed2 = PackLanes<TOrder>(pixels2.AsUInt32() | opaque);
                    Vector256<uint> packed3 = PackLanes<TOrder>(pixels3.AsUInt32() | opaque);
                    Vector256.Narrow(packed0, packed1).StoreUnsafe(ref destinationBase, x);
                    Vector256.Narrow(packed2, packed3).StoreUnsafe(ref destinationBase, x + 16);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<uint> opaque = Vector128.Create(0xFFU << TOrder.AlphaShift);

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

                    Vector128<uint> packed0 = PackLanes<TOrder>(pixels0.AsUInt32() | opaque);
                    Vector128<uint> packed1 = PackLanes<TOrder>(pixels1.AsUInt32() | opaque);
                    Vector128<uint> packed2 = PackLanes<TOrder>(pixels2.AsUInt32() | opaque);
                    Vector128<uint> packed3 = PackLanes<TOrder>(pixels3.AsUInt32() | opaque);
                    Vector128.Narrow(packed0, packed1).StoreUnsafe(ref destinationBase, x);
                    Vector128.Narrow(packed2, packed3).StoreUnsafe(ref destinationBase, x + 8);
                }
            }

            for (; x < length; x++)
            {
                ref byte pixelBase = ref Unsafe.Add(ref sourceBase, x * 3);
                uint pixel = pixelBase | ((uint)Unsafe.Add(ref pixelBase, 1) << 8) | ((uint)Unsafe.Add(ref pixelBase, 2) << 16);
                Rgba32 rgba = new((byte)(pixel >> TOrder.RedShift), (byte)(pixel >> TOrder.GreenShift), (byte)(pixel >> TOrder.BlueShift), byte.MaxValue);
                Unsafe.Add(ref destinationBase, x) = TLayout.Pack(rgba);
            }
        }

        /// <summary>
        /// Unpacks <typeparamref name="TLayout"/> pixels to pixels with four 8-bit channels, as
        /// <see cref="IPacked16Layout.Unpack(ushort)"/>.
        /// </summary>
        /// <remarks>
        /// One load reads a register of 16-bit pixels. Its two halves widen to 32-bit lanes in pixel order, and each half
        /// fills one register of destination pixels.
        /// </remarks>
        /// <typeparam name="TOrder">The channel order of the 4-byte pixels.</typeparam>
        /// <param name="source">The source pixels, 2 bytes each.</param>
        /// <param name="destination">The destination pixels, 4 bytes each. The caller checks the length.</param>
        public static void UnpackTo4Bytes<TOrder>(ReadOnlySpan<byte> source, Span<byte> destination)
            where TOrder : IChannelOrder
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
                    TLayout.Unpack<TOrder>(Vector512.WidenLower(packed)).StoreUnsafe(ref destinationBase, x);
                    TLayout.Unpack<TOrder>(Vector512.WidenUpper(packed))
                        .StoreUnsafe(ref destinationBase, x + (uint)Vector512<uint>.Count);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x + (uint)Vector256<ushort>.Count <= length; x += (uint)Vector256<ushort>.Count)
                {
                    Vector256<ushort> packed = Vector256.LoadUnsafe(ref sourceBase, x);
                    TLayout.Unpack<TOrder>(Vector256.WidenLower(packed)).StoreUnsafe(ref destinationBase, x);
                    TLayout.Unpack<TOrder>(Vector256.WidenUpper(packed))
                        .StoreUnsafe(ref destinationBase, x + (uint)Vector256<uint>.Count);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x + (uint)Vector128<ushort>.Count <= length; x += (uint)Vector128<ushort>.Count)
                {
                    Vector128<ushort> packed = Vector128.LoadUnsafe(ref sourceBase, x);
                    TLayout.Unpack<TOrder>(Vector128.WidenLower(packed)).StoreUnsafe(ref destinationBase, x);
                    TLayout.Unpack<TOrder>(Vector128.WidenUpper(packed))
                        .StoreUnsafe(ref destinationBase, x + (uint)Vector128<uint>.Count);
                }
            }

            for (; x < length; x++)
            {
                Rgba32 rgba = TLayout.Unpack(Unsafe.Add(ref sourceBase, x));
                Unsafe.Add(ref destinationBase, x) = ((uint)rgba.R << TOrder.RedShift)
                    | ((uint)rgba.G << TOrder.GreenShift)
                    | ((uint)rgba.B << TOrder.BlueShift)
                    | ((uint)rgba.A << TOrder.AlphaShift);
            }
        }

        /// <summary>
        /// Unpacks <typeparamref name="TLayout"/> pixels to pixels with three 8-bit channels, as
        /// <see cref="IPacked16Layout.Unpack(ushort)"/>.
        /// The conversion drops alpha.
        /// </summary>
        /// <remarks>
        /// The 4-byte lane arithmetic unpacks the pixels to four registers of 32-bit lanes. <c>SimdUtils.Join4To3</c>
        /// joins them to three full registers of 3-byte pixels.
        /// </remarks>
        /// <typeparam name="TOrder">The channel order of the 3-byte pixels.</typeparam>
        /// <param name="source">The source pixels, 2 bytes each.</param>
        /// <param name="destination">The destination pixels, 3 bytes each. The caller checks the length.</param>
        public static void UnpackTo3Bytes<TOrder>(ReadOnlySpan<byte> source, Span<byte> destination)
            where TOrder : IChannelOrder
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
                        TLayout.Unpack<TOrder>(Vector512.WidenLower(packed0)).AsByte(),
                        TLayout.Unpack<TOrder>(Vector512.WidenUpper(packed0)).AsByte(),
                        TLayout.Unpack<TOrder>(Vector512.WidenLower(packed1)).AsByte(),
                        TLayout.Unpack<TOrder>(Vector512.WidenUpper(packed1)).AsByte(),
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
                        TLayout.Unpack<TOrder>(Vector256.WidenLower(packed0)).AsByte(),
                        TLayout.Unpack<TOrder>(Vector256.WidenUpper(packed0)).AsByte(),
                        TLayout.Unpack<TOrder>(Vector256.WidenLower(packed1)).AsByte(),
                        TLayout.Unpack<TOrder>(Vector256.WidenUpper(packed1)).AsByte(),
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
                        TLayout.Unpack<TOrder>(Vector128.WidenLower(packed0)).AsByte(),
                        TLayout.Unpack<TOrder>(Vector128.WidenUpper(packed0)).AsByte(),
                        TLayout.Unpack<TOrder>(Vector128.WidenLower(packed1)).AsByte(),
                        TLayout.Unpack<TOrder>(Vector128.WidenUpper(packed1)).AsByte(),
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
                Rgba32 rgba = TLayout.Unpack(Unsafe.Add(ref sourceBase, x));
                uint pixel = ((uint)rgba.R << TOrder.RedShift) | ((uint)rgba.G << TOrder.GreenShift) | ((uint)rgba.B << TOrder.BlueShift);

                ref byte pixelBase = ref Unsafe.Add(ref destinationBase, x * 3);
                pixelBase = (byte)pixel;
                Unsafe.Add(ref pixelBase, 1) = (byte)(pixel >> 8);
                Unsafe.Add(ref pixelBase, 2) = (byte)(pixel >> 16);
            }
        }

        /// <summary>
        /// Converts pixels of the 16-bit layout <typeparamref name="TSourceLayout"/> to <typeparamref name="TLayout"/>
        /// pixels, as <see cref="IPacked16Layout.Pack(Rgba32)"/> of <see cref="IPacked16Layout.Unpack(ushort)"/>.
        /// </summary>
        /// <remarks>
        /// Each lane unpacks to 8-bit channels and packs again. For the 5-bit and 6-bit channels, this gives the same
        /// value as one UNORM scale from the source bit depth to the destination bit depth. One load reads a register of
        /// 16-bit pixels. Its two halves widen to 32-bit lanes in pixel order, and a narrow joins the two packed halves
        /// for one store.
        /// </remarks>
        /// <typeparam name="TSourceLayout">The 16-bit layout of the source pixels.</typeparam>
        /// <param name="source">The source pixels, 2 bytes each.</param>
        /// <param name="destination">The destination pixels, 2 bytes each. The caller checks the length.</param>
        public static void PackFrom16<TSourceLayout>(ReadOnlySpan<byte> source, Span<byte> destination)
            where TSourceLayout : IPacked16Layout
        {
            ref ushort sourceBase = ref Unsafe.As<byte, ushort>(ref MemoryMarshal.GetReference(source));
            ref ushort destinationBase = ref Unsafe.As<byte, ushort>(ref MemoryMarshal.GetReference(destination));
            nuint length = (uint)source.Length / 2;
            nuint x = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                for (; x + (uint)Vector512<ushort>.Count <= length; x += (uint)Vector512<ushort>.Count)
                {
                    Vector512<ushort> packed = Vector512.LoadUnsafe(ref sourceBase, x);
                    Vector512<uint> lower = PackLanes<XYZWChannelOrder>(TSourceLayout.Unpack<XYZWChannelOrder>(Vector512.WidenLower(packed)));
                    Vector512<uint> upper = PackLanes<XYZWChannelOrder>(TSourceLayout.Unpack<XYZWChannelOrder>(Vector512.WidenUpper(packed)));
                    Vector512.Narrow(lower, upper).StoreUnsafe(ref destinationBase, x);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x + (uint)Vector256<ushort>.Count <= length; x += (uint)Vector256<ushort>.Count)
                {
                    Vector256<ushort> packed = Vector256.LoadUnsafe(ref sourceBase, x);
                    Vector256<uint> lower = PackLanes<XYZWChannelOrder>(TSourceLayout.Unpack<XYZWChannelOrder>(Vector256.WidenLower(packed)));
                    Vector256<uint> upper = PackLanes<XYZWChannelOrder>(TSourceLayout.Unpack<XYZWChannelOrder>(Vector256.WidenUpper(packed)));
                    Vector256.Narrow(lower, upper).StoreUnsafe(ref destinationBase, x);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x + (uint)Vector128<ushort>.Count <= length; x += (uint)Vector128<ushort>.Count)
                {
                    Vector128<ushort> packed = Vector128.LoadUnsafe(ref sourceBase, x);
                    Vector128<uint> lower = PackLanes<XYZWChannelOrder>(TSourceLayout.Unpack<XYZWChannelOrder>(Vector128.WidenLower(packed)));
                    Vector128<uint> upper = PackLanes<XYZWChannelOrder>(TSourceLayout.Unpack<XYZWChannelOrder>(Vector128.WidenUpper(packed)));
                    Vector128.Narrow(lower, upper).StoreUnsafe(ref destinationBase, x);
                }
            }

            for (; x < length; x++)
            {
                Unsafe.Add(ref destinationBase, x) = TLayout.Pack(TSourceLayout.Unpack(Unsafe.Add(ref sourceBase, x)));
            }
        }

        /// <summary>
        /// Packs the 4-byte pixel of each lane to a <typeparamref name="TLayout"/> value in the low 16 bits of the lane.
        /// </summary>
        /// <typeparam name="TOrder">The channel order of the source pixels.</typeparam>
        /// <param name="pixels">The source pixels, one in each lane.</param>
        /// <returns>The packed values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<uint> PackLanes<TOrder>(Vector512<uint> pixels)
            where TOrder : IChannelOrder
        {
            // Each channel moves to bits 0..7, and the mask clears the other bits. The layout scales and places the
            // channels. A layout that does not store a channel does not use its value, and the JIT removes that work.
            Vector512<uint> mask = Vector512.Create(0xFFU);
            return TLayout.Pack(
                Vector512.ShiftRightLogical(pixels, TOrder.RedShift) & mask,
                Vector512.ShiftRightLogical(pixels, TOrder.GreenShift) & mask,
                Vector512.ShiftRightLogical(pixels, TOrder.BlueShift) & mask,
                Vector512.ShiftRightLogical(pixels, TOrder.AlphaShift) & mask);
        }

        /// <inheritdoc cref="PackLanes{TOrder}(Vector512{uint})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<uint> PackLanes<TOrder>(Vector256<uint> pixels)
            where TOrder : IChannelOrder
        {
            Vector256<uint> mask = Vector256.Create(0xFFU);
            return TLayout.Pack(
                Vector256.ShiftRightLogical(pixels, TOrder.RedShift) & mask,
                Vector256.ShiftRightLogical(pixels, TOrder.GreenShift) & mask,
                Vector256.ShiftRightLogical(pixels, TOrder.BlueShift) & mask,
                Vector256.ShiftRightLogical(pixels, TOrder.AlphaShift) & mask);
        }

        /// <inheritdoc cref="PackLanes{TOrder}(Vector512{uint})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<uint> PackLanes<TOrder>(Vector128<uint> pixels)
            where TOrder : IChannelOrder
        {
            Vector128<uint> mask = Vector128.Create(0xFFU);
            return TLayout.Pack(
                Vector128.ShiftRightLogical(pixels, TOrder.RedShift) & mask,
                Vector128.ShiftRightLogical(pixels, TOrder.GreenShift) & mask,
                Vector128.ShiftRightLogical(pixels, TOrder.BlueShift) & mask,
                Vector128.ShiftRightLogical(pixels, TOrder.AlphaShift) & mask);
        }
    }
}
