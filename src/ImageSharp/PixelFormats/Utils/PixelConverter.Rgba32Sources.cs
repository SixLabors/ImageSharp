// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.PixelFormats.Utils;

/// <content>
/// Contains the conversions from luminance and 16-bit layouts to the layouts with 8-bit channels.
/// </content>
/// <remarks>
/// Each conversion goes through <see cref="Rgba32"/> pixels in registers. A source operator loads the source pixels as
/// <see cref="Rgba32"/> bytes. <see cref="Rgba32SourceKernels{TSource}"/> then moves the channels to the destination
/// order and stores them. A pixel is one 32-bit lane. As a little-endian <see cref="uint"/>, red is byte 0, green is byte 1,
/// blue is byte 2 and alpha is byte 3.
/// </remarks>
internal static partial class PixelConverter
{
    /// <summary>
    /// Loads source pixels as <see cref="Rgba32"/> pixels, one in each 32-bit lane.
    /// </summary>
    /// <remarks>
    /// Each form gives the same bytes as the scalar conversion of the pixel type to <see cref="Rgba32"/>. A luminance
    /// value sets red, green and blue. A layout without alpha is opaque. A 16-bit channel scales to 8 bits as
    /// <see cref="ColorNumerics.From16BitTo8Bit(ushort)"/>. Each vector form loads 4 registers of pixels from pixel
    /// <c>x</c>. The caller keeps the last loaded pixel inside the source.
    /// </remarks>
    public interface IRgba32SourceOperator
    {
        /// <summary>
        /// Gets the byte count of one source pixel.
        /// </summary>
        static abstract int BytesPerPixel { get; }

        /// <summary>
        /// Loads pixel <paramref name="x"/>.
        /// </summary>
        /// <param name="row">The first byte of the source pixels.</param>
        /// <param name="x">The pixel index.</param>
        /// <returns>The <see cref="Rgba32"/> pixel as a little-endian <see cref="uint"/>.</returns>
        static abstract uint Load(ref byte row, nuint x);

        /// <summary>
        /// Loads 16 pixels from pixel <paramref name="x"/>.
        /// </summary>
        /// <param name="row">The first byte of the source pixels.</param>
        /// <param name="x">The index of the first pixel.</param>
        /// <param name="pixels0">Pixels 0 to 3, one in each 32-bit lane.</param>
        /// <param name="pixels1">Pixels 4 to 7.</param>
        /// <param name="pixels2">Pixels 8 to 11.</param>
        /// <param name="pixels3">Pixels 12 to 15.</param>
        static abstract void Load128(
            ref byte row,
            nuint x,
            out Vector128<byte> pixels0,
            out Vector128<byte> pixels1,
            out Vector128<byte> pixels2,
            out Vector128<byte> pixels3);

        /// <summary>
        /// Loads 32 pixels from pixel <paramref name="x"/>.
        /// </summary>
        /// <param name="row">The first byte of the source pixels.</param>
        /// <param name="x">The index of the first pixel.</param>
        /// <param name="pixels0">Pixels 0 to 7, one in each 32-bit lane.</param>
        /// <param name="pixels1">Pixels 8 to 15.</param>
        /// <param name="pixels2">Pixels 16 to 23.</param>
        /// <param name="pixels3">Pixels 24 to 31.</param>
        static abstract void Load256(
            ref byte row,
            nuint x,
            out Vector256<byte> pixels0,
            out Vector256<byte> pixels1,
            out Vector256<byte> pixels2,
            out Vector256<byte> pixels3);

        /// <summary>
        /// Loads 64 pixels from pixel <paramref name="x"/>.
        /// </summary>
        /// <param name="row">The first byte of the source pixels.</param>
        /// <param name="x">The index of the first pixel.</param>
        /// <param name="pixels0">Pixels 0 to 15, one in each 32-bit lane.</param>
        /// <param name="pixels1">Pixels 16 to 31.</param>
        /// <param name="pixels2">Pixels 32 to 47.</param>
        /// <param name="pixels3">Pixels 48 to 63.</param>
        static abstract void Load512(
            ref byte row,
            nuint x,
            out Vector512<byte> pixels0,
            out Vector512<byte> pixels1,
            out Vector512<byte> pixels2,
            out Vector512<byte> pixels3);
    }

    /// <summary>
    /// Gets the alpha bytes of opaque <see cref="Rgba32"/> pixels: 0xFF in byte 3 of each 32-bit lane, 0 in the other bytes.
    /// </summary>
    /// <remarks>
    /// As a little-endian <see cref="uint"/>, 0xFF000000 has 0xFF in byte 3, the alpha byte. An OR with a register of
    /// pixels whose alpha bytes are 0 makes each pixel opaque and does not change red, green or blue. The 256-bit and
    /// 512-bit forms use the same 0xFF000000 constant in each 32-bit lane.
    /// </remarks>
    private static Vector128<byte> OpaqueRgba32Alpha
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector128.Create(0xFF000000U).AsByte();
    }

    /// <summary>
    /// Gets the byte shuffle that copies byte 0 of each 32-bit lane to bytes 0, 1 and 2 of the lane. Byte 3 becomes 0.
    /// </summary>
    /// <remarks>
    /// The source lanes hold one 8-bit luminance value in byte 0. The result lanes are <see cref="Rgba32"/> pixels with
    /// red, green and blue equal to the luminance and alpha 0. Index 0x80 sets a byte to 0.
    /// </remarks>
    private static Vector128<byte> LuminanceLaneMask
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector128.Create((byte)0, 0, 0, 0x80, 4, 4, 4, 0x80, 8, 8, 8, 0x80, 12, 12, 12, 0x80);
    }

    /// <summary>
    /// Gets the byte shuffle that copies byte 0 of each 32-bit lane to bytes 0, 1 and 2 of the lane. Byte 1 goes to byte 3.
    /// </summary>
    /// <remarks>
    /// The source lanes hold an 8-bit luminance value in byte 0 and an 8-bit alpha value in byte 1. The result lanes are
    /// <see cref="Rgba32"/> pixels with red, green and blue equal to the luminance.
    /// </remarks>
    private static Vector128<byte> LuminanceAlphaLaneMask
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector128.Create((byte)0, 0, 0, 1, 4, 4, 4, 5, 8, 8, 8, 9, 12, 12, 12, 13);
    }

    /// <summary>
    /// Scales 16-bit values in 32-bit lanes to 8 bits, as <see cref="ColorNumerics.From16BitTo8Bit(ushort)"/>.
    /// </summary>
    /// <remarks>
    /// The scalar form is (v * 255 + 32895) >> 16. The multiply by 255 is (v &lt;&lt; 8) - v, so the vector form has no
    /// multiply. The largest sum is 65535 * 255 + 32895, which is less than 2^24, so a 32-bit lane holds it. The vector
    /// form gives the scalar result for each of the 65536 values.
    /// </remarks>
    /// <param name="value">The values, from 0 to 65535, one in each lane.</param>
    /// <returns>The scaled values, from 0 to 255, one in each lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<uint> ScaleUnorm16(Vector512<uint> value)
        => Vector512.ShiftRightLogical(Vector512.ShiftLeft(value, 8) - value + Vector512.Create(32895U), 16);

    /// <inheritdoc cref="ScaleUnorm16(Vector512{uint})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<uint> ScaleUnorm16(Vector256<uint> value)
        => Vector256.ShiftRightLogical(Vector256.ShiftLeft(value, 8) - value + Vector256.Create(32895U), 16);

    /// <inheritdoc cref="ScaleUnorm16(Vector512{uint})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> ScaleUnorm16(Vector128<uint> value)
        => Vector128.ShiftRightLogical(Vector128.ShiftLeft(value, 8) - value + Vector128.Create(32895U), 16);

    /// <summary>
    /// Scales two registers of 16-bit samples to one register of 8-bit samples, in sample order.
    /// </summary>
    /// <remarks>
    /// Each half of a register widens to 32-bit lanes for <see cref="ScaleUnorm16(Vector512{uint})"/>. Each scaled value
    /// fits 8 bits, so the two truncating narrows lose no bits. Widen and narrow keep the lane order, so sample i of the
    /// source is byte i of the result.
    /// </remarks>
    /// <param name="lower">The first samples.</param>
    /// <param name="upper">The next samples.</param>
    /// <returns>The scaled samples.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<byte> NarrowUnorm16(Vector512<ushort> lower, Vector512<ushort> upper)
        => Vector512.Narrow(
            Vector512.Narrow(ScaleUnorm16(Vector512.WidenLower(lower)), ScaleUnorm16(Vector512.WidenUpper(lower))),
            Vector512.Narrow(ScaleUnorm16(Vector512.WidenLower(upper)), ScaleUnorm16(Vector512.WidenUpper(upper))));

    /// <inheritdoc cref="NarrowUnorm16(Vector512{ushort}, Vector512{ushort})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<byte> NarrowUnorm16(Vector256<ushort> lower, Vector256<ushort> upper)
        => Vector256.Narrow(
            Vector256.Narrow(ScaleUnorm16(Vector256.WidenLower(lower)), ScaleUnorm16(Vector256.WidenUpper(lower))),
            Vector256.Narrow(ScaleUnorm16(Vector256.WidenLower(upper)), ScaleUnorm16(Vector256.WidenUpper(upper))));

    /// <inheritdoc cref="NarrowUnorm16(Vector512{ushort}, Vector512{ushort})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> NarrowUnorm16(Vector128<ushort> lower, Vector128<ushort> upper)
        => Vector128.Narrow(
            Vector128.Narrow(ScaleUnorm16(Vector128.WidenLower(lower)), ScaleUnorm16(Vector128.WidenUpper(lower))),
            Vector128.Narrow(ScaleUnorm16(Vector128.WidenLower(upper)), ScaleUnorm16(Vector128.WidenUpper(upper))));

    /// <summary>
    /// Makes one <see cref="Rgba32"/> pixel from 8-bit luminance and alpha values.
    /// </summary>
    /// <remarks>
    /// Red, green and blue are the luminance, as <see cref="Rgba32.FromL8(L8)"/> and <see cref="Rgba32.FromLa16(La16)"/>.
    /// </remarks>
    /// <param name="luminance">The luminance, from 0 to 255.</param>
    /// <param name="alpha">The alpha, from 0 to 255.</param>
    /// <returns>The pixel as a little-endian <see cref="uint"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint LuminanceToRgba32(uint luminance, uint alpha)
        => luminance | (luminance << 8) | (luminance << 16) | (alpha << 24);

    /// <summary>
    /// Loads <see cref="L8"/> pixels as <see cref="Rgba32"/> pixels.
    /// </summary>
    /// <remarks>
    /// One byte shuffle copies each luminance byte to red, green and blue of its pixel, and an OR with
    /// <see cref="OpaqueRgba32Alpha"/> sets alpha to 255. The 128-bit form shuffles the 16 loaded bytes directly. The
    /// wider forms first zero-extend each luminance byte to a 32-bit lane, because a byte shuffle selects only inside
    /// its 128-bit lane.
    /// </remarks>
    public readonly struct L8Rgba32Operator : IRgba32SourceOperator
    {
        /// <inheritdoc/>
        public static int BytesPerPixel => 1;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Load(ref byte row, nuint x) => LuminanceToRgba32(Unsafe.Add(ref row, x), byte.MaxValue);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load128(
            ref byte row,
            nuint x,
            out Vector128<byte> pixels0,
            out Vector128<byte> pixels1,
            out Vector128<byte> pixels2,
            out Vector128<byte> pixels3)
        {
            // Byte i of the load is the luminance of pixel i. Shuffle k writes pixels 4k to 4k + 3. Its indices select
            // byte 4k + j three times for lane j. Index 0x80 sets the alpha byte to 0 before the OR.
            Vector128<byte> luminance = Vector128.LoadUnsafe(ref row, x);
            Vector128<byte> opaque = OpaqueRgba32Alpha;
            pixels0 = Vector128.ShuffleNative(
                luminance,
                Vector128.Create((byte)0, 0, 0, 0x80, 1, 1, 1, 0x80, 2, 2, 2, 0x80, 3, 3, 3, 0x80)) | opaque;
            pixels1 = Vector128.ShuffleNative(
                luminance,
                Vector128.Create((byte)4, 4, 4, 0x80, 5, 5, 5, 0x80, 6, 6, 6, 0x80, 7, 7, 7, 0x80)) | opaque;
            pixels2 = Vector128.ShuffleNative(
                luminance,
                Vector128.Create((byte)8, 8, 8, 0x80, 9, 9, 9, 0x80, 10, 10, 10, 0x80, 11, 11, 11, 0x80)) | opaque;
            pixels3 = Vector128.ShuffleNative(
                luminance,
                Vector128.Create((byte)12, 12, 12, 0x80, 13, 13, 13, 0x80, 14, 14, 14, 0x80, 15, 15, 15, 0x80)) | opaque;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load256(
            ref byte row,
            nuint x,
            out Vector256<byte> pixels0,
            out Vector256<byte> pixels1,
            out Vector256<byte> pixels2,
            out Vector256<byte> pixels3)
        {
            // Each 8-byte load zero-extends to eight 32-bit lanes, one luminance value in byte 0 of each lane. The lane
            // shuffle then copies byte 0 to bytes 0, 1 and 2.
            Vector128<byte> mask = LuminanceLaneMask;
            Vector256<byte> opaque = Vector256.Create(0xFF000000U).AsByte();
            pixels0 = Vector256_.ShufflePerLane(Vector256_.Widen(Vector64.LoadUnsafe(ref row, x)).AsByte(), mask) | opaque;
            pixels1 = Vector256_.ShufflePerLane(Vector256_.Widen(Vector64.LoadUnsafe(ref row, x + 8)).AsByte(), mask) | opaque;
            pixels2 = Vector256_.ShufflePerLane(Vector256_.Widen(Vector64.LoadUnsafe(ref row, x + 16)).AsByte(), mask) | opaque;
            pixels3 = Vector256_.ShufflePerLane(Vector256_.Widen(Vector64.LoadUnsafe(ref row, x + 24)).AsByte(), mask) | opaque;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load512(
            ref byte row,
            nuint x,
            out Vector512<byte> pixels0,
            out Vector512<byte> pixels1,
            out Vector512<byte> pixels2,
            out Vector512<byte> pixels3)
        {
            // Each 16-byte load zero-extends to sixteen 32-bit lanes, one luminance value in byte 0 of each lane. The lane
            // shuffle then copies byte 0 to bytes 0, 1 and 2.
            Vector128<byte> mask = LuminanceLaneMask;
            Vector512<byte> opaque = Vector512.Create(0xFF000000U).AsByte();
            pixels0 = Vector512_.ShufflePerLane(Vector512_.Widen(Vector128.LoadUnsafe(ref row, x)).AsByte(), mask) | opaque;
            pixels1 = Vector512_.ShufflePerLane(Vector512_.Widen(Vector128.LoadUnsafe(ref row, x + 16)).AsByte(), mask) | opaque;
            pixels2 = Vector512_.ShufflePerLane(Vector512_.Widen(Vector128.LoadUnsafe(ref row, x + 32)).AsByte(), mask) | opaque;
            pixels3 = Vector512_.ShufflePerLane(Vector512_.Widen(Vector128.LoadUnsafe(ref row, x + 48)).AsByte(), mask) | opaque;
        }
    }

    /// <summary>
    /// Loads <see cref="La16"/> pixels as <see cref="Rgba32"/> pixels.
    /// </summary>
    /// <remarks>
    /// A pixel is two bytes: luminance, then alpha. One byte shuffle copies the luminance byte to red, green and blue and
    /// the alpha byte to alpha. The 128-bit form shuffles each 16 loaded bytes directly. The wider forms first
    /// zero-extend each 2-byte pixel to a 32-bit lane, because a byte shuffle selects only inside its 128-bit lane.
    /// </remarks>
    public readonly struct La16Rgba32Operator : IRgba32SourceOperator
    {
        /// <inheritdoc/>
        public static int BytesPerPixel => 2;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Load(ref byte row, nuint x)
        {
            ref byte pixel = ref Unsafe.Add(ref row, x * 2);
            return LuminanceToRgba32(pixel, Unsafe.Add(ref pixel, 1));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load128(
            ref byte row,
            nuint x,
            out Vector128<byte> pixels0,
            out Vector128<byte> pixels1,
            out Vector128<byte> pixels2,
            out Vector128<byte> pixels3)
        {
            // Each 16-byte load holds 8 pixels: bytes 2i and 2i + 1 are the luminance and alpha of pixel i. The low mask
            // writes pixels 0 to 3 of the load, and the high mask writes pixels 4 to 7.
            ref byte source = ref Unsafe.Add(ref row, x * 2);
            Vector128<byte> lowMask = Vector128.Create((byte)0, 0, 0, 1, 2, 2, 2, 3, 4, 4, 4, 5, 6, 6, 6, 7);
            Vector128<byte> highMask = Vector128.Create((byte)8, 8, 8, 9, 10, 10, 10, 11, 12, 12, 12, 13, 14, 14, 14, 15);
            Vector128<byte> lower = Vector128.LoadUnsafe(ref source);
            Vector128<byte> upper = Vector128.LoadUnsafe(ref source, 16);
            pixels0 = Vector128.ShuffleNative(lower, lowMask);
            pixels1 = Vector128.ShuffleNative(lower, highMask);
            pixels2 = Vector128.ShuffleNative(upper, lowMask);
            pixels3 = Vector128.ShuffleNative(upper, highMask);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load256(
            ref byte row,
            nuint x,
            out Vector256<byte> pixels0,
            out Vector256<byte> pixels1,
            out Vector256<byte> pixels2,
            out Vector256<byte> pixels3)
        {
            // Each widen zero-extends 8 pixels to 32-bit lanes: luminance in byte 0 and alpha in byte 1 of each lane.
            ref ushort source = ref Unsafe.As<byte, ushort>(ref row);
            Vector128<byte> mask = LuminanceAlphaLaneMask;
            Vector256<ushort> lower = Vector256.LoadUnsafe(ref source, x);
            Vector256<ushort> upper = Vector256.LoadUnsafe(ref source, x + 16);
            pixels0 = Vector256_.ShufflePerLane(Vector256.WidenLower(lower).AsByte(), mask);
            pixels1 = Vector256_.ShufflePerLane(Vector256.WidenUpper(lower).AsByte(), mask);
            pixels2 = Vector256_.ShufflePerLane(Vector256.WidenLower(upper).AsByte(), mask);
            pixels3 = Vector256_.ShufflePerLane(Vector256.WidenUpper(upper).AsByte(), mask);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load512(
            ref byte row,
            nuint x,
            out Vector512<byte> pixels0,
            out Vector512<byte> pixels1,
            out Vector512<byte> pixels2,
            out Vector512<byte> pixels3)
        {
            // Each widen zero-extends 16 pixels to 32-bit lanes: luminance in byte 0 and alpha in byte 1 of each lane.
            ref ushort source = ref Unsafe.As<byte, ushort>(ref row);
            Vector128<byte> mask = LuminanceAlphaLaneMask;
            Vector512<ushort> lower = Vector512.LoadUnsafe(ref source, x);
            Vector512<ushort> upper = Vector512.LoadUnsafe(ref source, x + 32);
            pixels0 = Vector512_.ShufflePerLane(Vector512.WidenLower(lower).AsByte(), mask);
            pixels1 = Vector512_.ShufflePerLane(Vector512.WidenUpper(lower).AsByte(), mask);
            pixels2 = Vector512_.ShufflePerLane(Vector512.WidenLower(upper).AsByte(), mask);
            pixels3 = Vector512_.ShufflePerLane(Vector512.WidenUpper(upper).AsByte(), mask);
        }
    }

    /// <summary>
    /// Loads <see cref="L16"/> pixels as <see cref="Rgba32"/> pixels.
    /// </summary>
    /// <remarks>
    /// Each widen zero-extends 16-bit luminance values to 32-bit lanes, and <see cref="ScaleUnorm16(Vector512{uint})"/>
    /// scales them to 8 bits in byte 0 of each lane. <see cref="LuminanceLaneMask"/> then copies byte 0 to red, green and
    /// blue, and an OR with <see cref="OpaqueRgba32Alpha"/> sets alpha to 255.
    /// </remarks>
    public readonly struct L16Rgba32Operator : IRgba32SourceOperator
    {
        /// <inheritdoc/>
        public static int BytesPerPixel => 2;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Load(ref byte row, nuint x)
            => LuminanceToRgba32(ColorNumerics.From16BitTo8Bit(Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref row, x * 2))), byte.MaxValue);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load128(
            ref byte row,
            nuint x,
            out Vector128<byte> pixels0,
            out Vector128<byte> pixels1,
            out Vector128<byte> pixels2,
            out Vector128<byte> pixels3)
        {
            ref ushort source = ref Unsafe.As<byte, ushort>(ref row);
            Vector128<byte> mask = LuminanceLaneMask;
            Vector128<byte> opaque = OpaqueRgba32Alpha;
            Vector128<ushort> lower = Vector128.LoadUnsafe(ref source, x);
            Vector128<ushort> upper = Vector128.LoadUnsafe(ref source, x + 8);
            pixels0 = Vector128.ShuffleNative(ScaleUnorm16(Vector128.WidenLower(lower)).AsByte(), mask) | opaque;
            pixels1 = Vector128.ShuffleNative(ScaleUnorm16(Vector128.WidenUpper(lower)).AsByte(), mask) | opaque;
            pixels2 = Vector128.ShuffleNative(ScaleUnorm16(Vector128.WidenLower(upper)).AsByte(), mask) | opaque;
            pixels3 = Vector128.ShuffleNative(ScaleUnorm16(Vector128.WidenUpper(upper)).AsByte(), mask) | opaque;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load256(
            ref byte row,
            nuint x,
            out Vector256<byte> pixels0,
            out Vector256<byte> pixels1,
            out Vector256<byte> pixels2,
            out Vector256<byte> pixels3)
        {
            ref ushort source = ref Unsafe.As<byte, ushort>(ref row);
            Vector128<byte> mask = LuminanceLaneMask;
            Vector256<byte> opaque = Vector256.Create(0xFF000000U).AsByte();
            Vector256<ushort> lower = Vector256.LoadUnsafe(ref source, x);
            Vector256<ushort> upper = Vector256.LoadUnsafe(ref source, x + 16);
            pixels0 = Vector256_.ShufflePerLane(ScaleUnorm16(Vector256.WidenLower(lower)).AsByte(), mask) | opaque;
            pixels1 = Vector256_.ShufflePerLane(ScaleUnorm16(Vector256.WidenUpper(lower)).AsByte(), mask) | opaque;
            pixels2 = Vector256_.ShufflePerLane(ScaleUnorm16(Vector256.WidenLower(upper)).AsByte(), mask) | opaque;
            pixels3 = Vector256_.ShufflePerLane(ScaleUnorm16(Vector256.WidenUpper(upper)).AsByte(), mask) | opaque;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load512(
            ref byte row,
            nuint x,
            out Vector512<byte> pixels0,
            out Vector512<byte> pixels1,
            out Vector512<byte> pixels2,
            out Vector512<byte> pixels3)
        {
            ref ushort source = ref Unsafe.As<byte, ushort>(ref row);
            Vector128<byte> mask = LuminanceLaneMask;
            Vector512<byte> opaque = Vector512.Create(0xFF000000U).AsByte();
            Vector512<ushort> lower = Vector512.LoadUnsafe(ref source, x);
            Vector512<ushort> upper = Vector512.LoadUnsafe(ref source, x + 32);
            pixels0 = Vector512_.ShufflePerLane(ScaleUnorm16(Vector512.WidenLower(lower)).AsByte(), mask) | opaque;
            pixels1 = Vector512_.ShufflePerLane(ScaleUnorm16(Vector512.WidenUpper(lower)).AsByte(), mask) | opaque;
            pixels2 = Vector512_.ShufflePerLane(ScaleUnorm16(Vector512.WidenLower(upper)).AsByte(), mask) | opaque;
            pixels3 = Vector512_.ShufflePerLane(ScaleUnorm16(Vector512.WidenUpper(upper)).AsByte(), mask) | opaque;
        }
    }

    /// <summary>
    /// Loads <see cref="La32"/> pixels as <see cref="Rgba32"/> pixels.
    /// </summary>
    /// <remarks>
    /// A pixel is one 32-bit lane: luminance in bits 0 to 15 and alpha in bits 16 to 31. Each 16-bit value scales to
    /// 8 bits with <see cref="ScaleUnorm16(Vector512{uint})"/>. A shift puts the alpha byte in byte 1 of the lane, next
    /// to the luminance byte, and <see cref="LuminanceAlphaLaneMask"/> then makes the <see cref="Rgba32"/> pixel.
    /// </remarks>
    public readonly struct La32Rgba32Operator : IRgba32SourceOperator
    {
        /// <inheritdoc/>
        public static int BytesPerPixel => 4;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Load(ref byte row, nuint x)
        {
            ref ushort pixel = ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref row, x * 4));
            return LuminanceToRgba32(ColorNumerics.From16BitTo8Bit(pixel), ColorNumerics.From16BitTo8Bit(Unsafe.Add(ref pixel, 1)));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load128(
            ref byte row,
            nuint x,
            out Vector128<byte> pixels0,
            out Vector128<byte> pixels1,
            out Vector128<byte> pixels2,
            out Vector128<byte> pixels3)
        {
            ref uint source = ref Unsafe.As<byte, uint>(ref row);
            pixels0 = Expand(Vector128.LoadUnsafe(ref source, x));
            pixels1 = Expand(Vector128.LoadUnsafe(ref source, x + 4));
            pixels2 = Expand(Vector128.LoadUnsafe(ref source, x + 8));
            pixels3 = Expand(Vector128.LoadUnsafe(ref source, x + 12));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load256(
            ref byte row,
            nuint x,
            out Vector256<byte> pixels0,
            out Vector256<byte> pixels1,
            out Vector256<byte> pixels2,
            out Vector256<byte> pixels3)
        {
            ref uint source = ref Unsafe.As<byte, uint>(ref row);
            pixels0 = Expand(Vector256.LoadUnsafe(ref source, x));
            pixels1 = Expand(Vector256.LoadUnsafe(ref source, x + 8));
            pixels2 = Expand(Vector256.LoadUnsafe(ref source, x + 16));
            pixels3 = Expand(Vector256.LoadUnsafe(ref source, x + 24));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load512(
            ref byte row,
            nuint x,
            out Vector512<byte> pixels0,
            out Vector512<byte> pixels1,
            out Vector512<byte> pixels2,
            out Vector512<byte> pixels3)
        {
            ref uint source = ref Unsafe.As<byte, uint>(ref row);
            pixels0 = Expand(Vector512.LoadUnsafe(ref source, x));
            pixels1 = Expand(Vector512.LoadUnsafe(ref source, x + 16));
            pixels2 = Expand(Vector512.LoadUnsafe(ref source, x + 32));
            pixels3 = Expand(Vector512.LoadUnsafe(ref source, x + 48));
        }

        /// <summary>
        /// Converts <see cref="La32"/> pixels in 32-bit lanes to <see cref="Rgba32"/> pixels.
        /// </summary>
        /// <param name="pixels">The pixels: luminance in bits 0 to 15 and alpha in bits 16 to 31 of each lane.</param>
        /// <returns>The <see cref="Rgba32"/> pixels.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<byte> Expand(Vector512<uint> pixels)
        {
            // Each scaled value is at most 255. The luminance byte stays in byte 0, and the shift by 8 puts the alpha
            // byte in byte 1. Thus the OR gives one luminance and alpha pair in each lane.
            Vector512<uint> luminance = ScaleUnorm16(pixels & Vector512.Create((uint)ushort.MaxValue));
            Vector512<uint> alpha = ScaleUnorm16(Vector512.ShiftRightLogical(pixels, 16));
            return Vector512_.ShufflePerLane((luminance | Vector512.ShiftLeft(alpha, 8)).AsByte(), LuminanceAlphaLaneMask);
        }

        /// <inheritdoc cref="Expand(Vector512{uint})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<byte> Expand(Vector256<uint> pixels)
        {
            Vector256<uint> luminance = ScaleUnorm16(pixels & Vector256.Create((uint)ushort.MaxValue));
            Vector256<uint> alpha = ScaleUnorm16(Vector256.ShiftRightLogical(pixels, 16));
            return Vector256_.ShufflePerLane((luminance | Vector256.ShiftLeft(alpha, 8)).AsByte(), LuminanceAlphaLaneMask);
        }

        /// <inheritdoc cref="Expand(Vector512{uint})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<byte> Expand(Vector128<uint> pixels)
        {
            Vector128<uint> luminance = ScaleUnorm16(pixels & Vector128.Create((uint)ushort.MaxValue));
            Vector128<uint> alpha = ScaleUnorm16(Vector128.ShiftRightLogical(pixels, 16));
            return Vector128.ShuffleNative((luminance | Vector128.ShiftLeft(alpha, 8)).AsByte(), LuminanceAlphaLaneMask);
        }
    }

    /// <summary>
    /// Loads <see cref="Rgb48"/> pixels as <see cref="Rgba32"/> pixels.
    /// </summary>
    /// <remarks>
    /// Six loads read the 16-bit samples of 16, 32 or 64 pixels.
    /// <see cref="NarrowUnorm16(Vector512{ushort}, Vector512{ushort})"/> scales them to three registers of packed 3-byte
    /// pixels in RGB order.
    /// <c>SimdUtils.Pad3</c> with <see cref="Pad3x8Layout"/> pads those to four registers of 32-bit lanes. Byte 3 of
    /// each lane is 0, so an OR with <see cref="OpaqueRgba32Alpha"/> sets alpha to 255.
    /// </remarks>
    public readonly struct Rgb48Rgba32Operator : IRgba32SourceOperator
    {
        /// <inheritdoc/>
        public static int BytesPerPixel => 6;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Load(ref byte row, nuint x)
        {
            ref ushort pixel = ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref row, x * 6));
            return ColorNumerics.From16BitTo8Bit(pixel)
                | ((uint)ColorNumerics.From16BitTo8Bit(Unsafe.Add(ref pixel, 1)) << 8)
                | ((uint)ColorNumerics.From16BitTo8Bit(Unsafe.Add(ref pixel, 2)) << 16)
                | ((uint)byte.MaxValue << 24);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load128(
            ref byte row,
            nuint x,
            out Vector128<byte> pixels0,
            out Vector128<byte> pixels1,
            out Vector128<byte> pixels2,
            out Vector128<byte> pixels3)
        {
            // 16 pixels are 48 samples, six registers of 8 samples. Each narrow gives 16 bytes of packed RGB.
            ref ushort source = ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref row, x * 6));
            SimdUtils.Pad3<Pad3x8Layout>(
                NarrowUnorm16(Vector128.LoadUnsafe(ref source), Vector128.LoadUnsafe(ref source, 8)),
                NarrowUnorm16(Vector128.LoadUnsafe(ref source, 16), Vector128.LoadUnsafe(ref source, 24)),
                NarrowUnorm16(Vector128.LoadUnsafe(ref source, 32), Vector128.LoadUnsafe(ref source, 40)),
                out pixels0,
                out pixels1,
                out pixels2,
                out pixels3);

            Vector128<byte> opaque = OpaqueRgba32Alpha;
            pixels0 |= opaque;
            pixels1 |= opaque;
            pixels2 |= opaque;
            pixels3 |= opaque;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load256(
            ref byte row,
            nuint x,
            out Vector256<byte> pixels0,
            out Vector256<byte> pixels1,
            out Vector256<byte> pixels2,
            out Vector256<byte> pixels3)
        {
            // 32 pixels are 96 samples, six registers of 16 samples. Each narrow gives 32 bytes of packed RGB.
            ref ushort source = ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref row, x * 6));
            SimdUtils.Pad3<Pad3x8Layout>(
                NarrowUnorm16(Vector256.LoadUnsafe(ref source), Vector256.LoadUnsafe(ref source, 16)),
                NarrowUnorm16(Vector256.LoadUnsafe(ref source, 32), Vector256.LoadUnsafe(ref source, 48)),
                NarrowUnorm16(Vector256.LoadUnsafe(ref source, 64), Vector256.LoadUnsafe(ref source, 80)),
                out pixels0,
                out pixels1,
                out pixels2,
                out pixels3);

            Vector256<byte> opaque = Vector256.Create(0xFF000000U).AsByte();
            pixels0 |= opaque;
            pixels1 |= opaque;
            pixels2 |= opaque;
            pixels3 |= opaque;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load512(
            ref byte row,
            nuint x,
            out Vector512<byte> pixels0,
            out Vector512<byte> pixels1,
            out Vector512<byte> pixels2,
            out Vector512<byte> pixels3)
        {
            // 64 pixels are 192 samples, six registers of 32 samples. Each narrow gives 64 bytes of packed RGB.
            ref ushort source = ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref row, x * 6));
            SimdUtils.Pad3<Pad3x8Layout>(
                NarrowUnorm16(Vector512.LoadUnsafe(ref source), Vector512.LoadUnsafe(ref source, 32)),
                NarrowUnorm16(Vector512.LoadUnsafe(ref source, 64), Vector512.LoadUnsafe(ref source, 96)),
                NarrowUnorm16(Vector512.LoadUnsafe(ref source, 128), Vector512.LoadUnsafe(ref source, 160)),
                out pixels0,
                out pixels1,
                out pixels2,
                out pixels3);

            Vector512<byte> opaque = Vector512.Create(0xFF000000U).AsByte();
            pixels0 |= opaque;
            pixels1 |= opaque;
            pixels2 |= opaque;
            pixels3 |= opaque;
        }
    }

    /// <summary>
    /// Loads <see cref="Rgba64"/> pixels as <see cref="Rgba32"/> pixels.
    /// </summary>
    /// <remarks>
    /// The 16-bit samples are in RGBA order, the order of <see cref="Rgba32"/>. <see cref="NarrowUnorm16(Vector512{ushort}, Vector512{ushort})"/>
    /// scales each two registers of samples to one register of 8-bit samples in the same order, which is one register of
    /// <see cref="Rgba32"/> pixels.
    /// </remarks>
    public readonly struct Rgba64Rgba32Operator : IRgba32SourceOperator
    {
        /// <inheritdoc/>
        public static int BytesPerPixel => 8;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Load(ref byte row, nuint x)
        {
            ref ushort pixel = ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref row, x * 8));
            return ColorNumerics.From16BitTo8Bit(pixel)
                | ((uint)ColorNumerics.From16BitTo8Bit(Unsafe.Add(ref pixel, 1)) << 8)
                | ((uint)ColorNumerics.From16BitTo8Bit(Unsafe.Add(ref pixel, 2)) << 16)
                | ((uint)ColorNumerics.From16BitTo8Bit(Unsafe.Add(ref pixel, 3)) << 24);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load128(
            ref byte row,
            nuint x,
            out Vector128<byte> pixels0,
            out Vector128<byte> pixels1,
            out Vector128<byte> pixels2,
            out Vector128<byte> pixels3)
        {
            // 16 pixels are 64 samples, eight registers of 8 samples. Each narrow gives 4 pixels.
            ref ushort source = ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref row, x * 8));
            pixels0 = NarrowUnorm16(Vector128.LoadUnsafe(ref source), Vector128.LoadUnsafe(ref source, 8));
            pixels1 = NarrowUnorm16(Vector128.LoadUnsafe(ref source, 16), Vector128.LoadUnsafe(ref source, 24));
            pixels2 = NarrowUnorm16(Vector128.LoadUnsafe(ref source, 32), Vector128.LoadUnsafe(ref source, 40));
            pixels3 = NarrowUnorm16(Vector128.LoadUnsafe(ref source, 48), Vector128.LoadUnsafe(ref source, 56));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load256(
            ref byte row,
            nuint x,
            out Vector256<byte> pixels0,
            out Vector256<byte> pixels1,
            out Vector256<byte> pixels2,
            out Vector256<byte> pixels3)
        {
            // 32 pixels are 128 samples, eight registers of 16 samples. Each narrow gives 8 pixels.
            ref ushort source = ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref row, x * 8));
            pixels0 = NarrowUnorm16(Vector256.LoadUnsafe(ref source), Vector256.LoadUnsafe(ref source, 16));
            pixels1 = NarrowUnorm16(Vector256.LoadUnsafe(ref source, 32), Vector256.LoadUnsafe(ref source, 48));
            pixels2 = NarrowUnorm16(Vector256.LoadUnsafe(ref source, 64), Vector256.LoadUnsafe(ref source, 80));
            pixels3 = NarrowUnorm16(Vector256.LoadUnsafe(ref source, 96), Vector256.LoadUnsafe(ref source, 112));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load512(
            ref byte row,
            nuint x,
            out Vector512<byte> pixels0,
            out Vector512<byte> pixels1,
            out Vector512<byte> pixels2,
            out Vector512<byte> pixels3)
        {
            // 64 pixels are 256 samples, eight registers of 32 samples. Each narrow gives 16 pixels.
            ref ushort source = ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref row, x * 8));
            pixels0 = NarrowUnorm16(Vector512.LoadUnsafe(ref source), Vector512.LoadUnsafe(ref source, 32));
            pixels1 = NarrowUnorm16(Vector512.LoadUnsafe(ref source, 64), Vector512.LoadUnsafe(ref source, 96));
            pixels2 = NarrowUnorm16(Vector512.LoadUnsafe(ref source, 128), Vector512.LoadUnsafe(ref source, 160));
            pixels3 = NarrowUnorm16(Vector512.LoadUnsafe(ref source, 192), Vector512.LoadUnsafe(ref source, 224));
        }
    }

    /// <summary>
    /// Converts the pixels of a <typeparamref name="TSource"/> layout to layouts with 8-bit channels.
    /// </summary>
    /// <remarks>
    /// Each loop loads 4 registers of <see cref="Rgba32"/> pixels from the source operator. The widest loop runs first,
    /// and each narrower loop continues from the same pixel, so only fewer than 16 pixels stay for the scalar loop.
    /// </remarks>
    /// <typeparam name="TSource">The source operator.</typeparam>
    public static class Rgba32SourceKernels<TSource>
        where TSource : IRgba32SourceOperator
    {
        /// <summary>
        /// Converts the source pixels to 4-byte pixels.
        /// </summary>
        /// <remarks>
        /// <typeparamref name="TShuffle"/> moves the channels of each <see cref="Rgba32"/> pixel to the destination order.
        /// Each register then stores as it is.
        /// </remarks>
        /// <typeparam name="TShuffle">The shuffle from RGBA order to the destination channel order.</typeparam>
        /// <param name="source">The source pixels.</param>
        /// <param name="destination">The destination pixels, 4 bytes each. The caller checks the length.</param>
        public static void To4Bytes<TShuffle>(ReadOnlySpan<byte> source, Span<byte> destination)
            where TShuffle : struct, IShuffle4
        {
            ref byte sourceBase = ref MemoryMarshal.GetReference(source);
            ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
            nuint length = (uint)(source.Length / TSource.BytesPerPixel);
            nuint x = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                for (; x + 64 <= length; x += 64)
                {
                    TSource.Load512(
                        ref sourceBase,
                        x,
                        out Vector512<byte> pixels0,
                        out Vector512<byte> pixels1,
                        out Vector512<byte> pixels2,
                        out Vector512<byte> pixels3);

                    ref byte destinationRow = ref Unsafe.Add(ref destinationBase, x * 4);
                    TShuffle.Invoke(pixels0).StoreUnsafe(ref destinationRow);
                    TShuffle.Invoke(pixels1).StoreUnsafe(ref destinationRow, 64);
                    TShuffle.Invoke(pixels2).StoreUnsafe(ref destinationRow, 128);
                    TShuffle.Invoke(pixels3).StoreUnsafe(ref destinationRow, 192);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x + 32 <= length; x += 32)
                {
                    TSource.Load256(
                        ref sourceBase,
                        x,
                        out Vector256<byte> pixels0,
                        out Vector256<byte> pixels1,
                        out Vector256<byte> pixels2,
                        out Vector256<byte> pixels3);

                    ref byte destinationRow = ref Unsafe.Add(ref destinationBase, x * 4);
                    TShuffle.Invoke(pixels0).StoreUnsafe(ref destinationRow);
                    TShuffle.Invoke(pixels1).StoreUnsafe(ref destinationRow, 32);
                    TShuffle.Invoke(pixels2).StoreUnsafe(ref destinationRow, 64);
                    TShuffle.Invoke(pixels3).StoreUnsafe(ref destinationRow, 96);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x + 16 <= length; x += 16)
                {
                    TSource.Load128(
                        ref sourceBase,
                        x,
                        out Vector128<byte> pixels0,
                        out Vector128<byte> pixels1,
                        out Vector128<byte> pixels2,
                        out Vector128<byte> pixels3);

                    ref byte destinationRow = ref Unsafe.Add(ref destinationBase, x * 4);
                    TShuffle.Invoke(pixels0).StoreUnsafe(ref destinationRow);
                    TShuffle.Invoke(pixels1).StoreUnsafe(ref destinationRow, 16);
                    TShuffle.Invoke(pixels2).StoreUnsafe(ref destinationRow, 32);
                    TShuffle.Invoke(pixels3).StoreUnsafe(ref destinationRow, 48);
                }
            }

            for (; x < length; x++)
            {
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref destinationBase, x * 4), TShuffle.Invoke(TSource.Load(ref sourceBase, x)));
            }
        }

        /// <summary>
        /// Converts the source pixels to 3-byte pixels. The conversion drops alpha.
        /// </summary>
        /// <remarks>
        /// <typeparamref name="TShuffle"/> moves red, green and blue of each <see cref="Rgba32"/> pixel to the destination
        /// order in bytes 0 to 2 of the lane. <c>SimdUtils.Slice3</c> with <see cref="Pad3x8Layout"/> then slices bytes 0
        /// to 2 of each lane of four registers to three full registers of 3-byte pixels.
        /// </remarks>
        /// <typeparam name="TShuffle">The shuffle from RGBA order to the destination channel order, with alpha in byte 3.</typeparam>
        /// <param name="source">The source pixels.</param>
        /// <param name="destination">The destination pixels, 3 bytes each. The caller checks the length.</param>
        public static void To3Bytes<TShuffle>(ReadOnlySpan<byte> source, Span<byte> destination)
            where TShuffle : struct, IShuffle4
        {
            ref byte sourceBase = ref MemoryMarshal.GetReference(source);
            ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
            nuint length = (uint)(source.Length / TSource.BytesPerPixel);
            nuint x = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                for (; x + 64 <= length; x += 64)
                {
                    TSource.Load512(
                        ref sourceBase,
                        x,
                        out Vector512<byte> pixels0,
                        out Vector512<byte> pixels1,
                        out Vector512<byte> pixels2,
                        out Vector512<byte> pixels3);

                    SimdUtils.Slice3<Pad3x8Layout>(
                        TShuffle.Invoke(pixels0),
                        TShuffle.Invoke(pixels1),
                        TShuffle.Invoke(pixels2),
                        TShuffle.Invoke(pixels3),
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
                for (; x + 32 <= length; x += 32)
                {
                    TSource.Load256(
                        ref sourceBase,
                        x,
                        out Vector256<byte> pixels0,
                        out Vector256<byte> pixels1,
                        out Vector256<byte> pixels2,
                        out Vector256<byte> pixels3);

                    SimdUtils.Slice3<Pad3x8Layout>(
                        TShuffle.Invoke(pixels0),
                        TShuffle.Invoke(pixels1),
                        TShuffle.Invoke(pixels2),
                        TShuffle.Invoke(pixels3),
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
                for (; x + 16 <= length; x += 16)
                {
                    TSource.Load128(
                        ref sourceBase,
                        x,
                        out Vector128<byte> pixels0,
                        out Vector128<byte> pixels1,
                        out Vector128<byte> pixels2,
                        out Vector128<byte> pixels3);

                    SimdUtils.Slice3<Pad3x8Layout>(
                        TShuffle.Invoke(pixels0),
                        TShuffle.Invoke(pixels1),
                        TShuffle.Invoke(pixels2),
                        TShuffle.Invoke(pixels3),
                        out Vector128<byte> destination0,
                        out Vector128<byte> destination1,
                        out Vector128<byte> destination2);

                    ref byte destinationRow = ref Unsafe.Add(ref destinationBase, x * 3);
                    destination0.StoreUnsafe(ref destinationRow);
                    destination1.StoreUnsafe(ref destinationRow, 16);
                    destination2.StoreUnsafe(ref destinationRow, 32);
                }
            }

            // A 3-byte store from a register: one 16-bit store writes bytes 0 and 1, and one byte store writes byte 2.
            for (; x < length; x++)
            {
                uint pixel = TShuffle.Invoke(TSource.Load(ref sourceBase, x));
                ref byte pixelBase = ref Unsafe.Add(ref destinationBase, x * 3);
                Unsafe.WriteUnaligned(ref pixelBase, (ushort)pixel);
                Unsafe.Add(ref pixelBase, 2) = (byte)(pixel >> 16);
            }
        }
    }
}
