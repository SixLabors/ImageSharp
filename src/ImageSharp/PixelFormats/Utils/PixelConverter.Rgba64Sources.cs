// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.PixelFormats.Utils;

/// <content>
/// Contains the conversions to the layouts with 16-bit channels.
/// </content>
/// <remarks>
/// Each conversion from a 16-bit source goes through <see cref="Rgba64"/> pixels in registers. A source operator loads
/// the source pixels as <see cref="Rgba64"/> samples, and <see cref="Rgba64SourceKernels{TSource}"/> stores them. A
/// pixel is one 64-bit lane. As a little-endian <see cref="ulong"/>, red is bits 0 to 15 and green is bits 16 to 31.
/// Blue is bits 32 to 47 and alpha is bits 48 to 63. A conversion from an 8-bit source scales each byte with
/// <see cref="ScaleUnorm8To16(ReadOnlySpan{byte}, Span{ushort})"/>.
/// </remarks>
internal static partial class PixelConverter
{
    /// <summary>
    /// Loads 16-bit source pixels as <see cref="Rgba64"/> pixels, one in each 64-bit lane.
    /// </summary>
    /// <remarks>
    /// Each form gives the same samples as the scalar conversion of the pixel type to <see cref="Rgba64"/>. A luminance
    /// value sets red, green and blue. A layout without alpha is opaque. Each vector form loads 4 registers of pixels from
    /// pixel <c>x</c>. The caller keeps the last loaded pixel inside the source.
    /// </remarks>
    public interface IRgba64SourceOperator
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
        /// <returns>The <see cref="Rgba64"/> pixel as a little-endian <see cref="ulong"/>.</returns>
        static abstract ulong Load(ref byte row, nuint x);

        /// <summary>
        /// Loads 8 pixels from pixel <paramref name="x"/>.
        /// </summary>
        /// <param name="row">The first byte of the source pixels.</param>
        /// <param name="x">The index of the first pixel.</param>
        /// <param name="pixels0">Pixels 0 and 1, one in each 64-bit lane.</param>
        /// <param name="pixels1">Pixels 2 and 3.</param>
        /// <param name="pixels2">Pixels 4 and 5.</param>
        /// <param name="pixels3">Pixels 6 and 7.</param>
        static abstract void Load128(
            ref byte row,
            nuint x,
            out Vector128<ushort> pixels0,
            out Vector128<ushort> pixels1,
            out Vector128<ushort> pixels2,
            out Vector128<ushort> pixels3);

        /// <summary>
        /// Loads 16 pixels from pixel <paramref name="x"/>.
        /// </summary>
        /// <param name="row">The first byte of the source pixels.</param>
        /// <param name="x">The index of the first pixel.</param>
        /// <param name="pixels0">Pixels 0 to 3, one in each 64-bit lane.</param>
        /// <param name="pixels1">Pixels 4 to 7.</param>
        /// <param name="pixels2">Pixels 8 to 11.</param>
        /// <param name="pixels3">Pixels 12 to 15.</param>
        static abstract void Load256(
            ref byte row,
            nuint x,
            out Vector256<ushort> pixels0,
            out Vector256<ushort> pixels1,
            out Vector256<ushort> pixels2,
            out Vector256<ushort> pixels3);

        /// <summary>
        /// Loads 32 pixels from pixel <paramref name="x"/>.
        /// </summary>
        /// <param name="row">The first byte of the source pixels.</param>
        /// <param name="x">The index of the first pixel.</param>
        /// <param name="pixels0">Pixels 0 to 7, one in each 64-bit lane.</param>
        /// <param name="pixels1">Pixels 8 to 15.</param>
        /// <param name="pixels2">Pixels 16 to 23.</param>
        /// <param name="pixels3">Pixels 24 to 31.</param>
        static abstract void Load512(
            ref byte row,
            nuint x,
            out Vector512<ushort> pixels0,
            out Vector512<ushort> pixels1,
            out Vector512<ushort> pixels2,
            out Vector512<ushort> pixels3);
    }

    /// <summary>
    /// Gets the alpha samples of opaque <see cref="Rgba64"/> pixels: 0xFFFF in 16-bit lane 3 of each 64-bit lane, 0 in
    /// the other 16-bit lanes.
    /// </summary>
    /// <remarks>
    /// As a little-endian <see cref="ulong"/>, 0xFFFF000000000000 has 0xFFFF in bits 48 to 63, the alpha sample. An OR
    /// with a register of pixels whose alpha samples are 0 makes each pixel opaque and does not change red, green or
    /// blue. The 256-bit and 512-bit forms use the same constant in each 64-bit lane.
    /// </remarks>
    private static Vector128<ushort> OpaqueRgba64Alpha
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector128.Create(0xFFFF000000000000UL).AsUInt16();
    }

    /// <summary>
    /// Gets the byte shuffle that copies 16-bit lane 0 of each 64-bit lane to 16-bit lanes 0, 1 and 2. The 16-bit lane 3
    /// becomes 0.
    /// </summary>
    /// <remarks>
    /// The source lanes hold one 16-bit luminance value in bytes 0 and 1. The result lanes are <see cref="Rgba64"/>
    /// pixels with red, green and blue equal to the luminance and alpha 0. Index 0x80 sets a byte to 0.
    /// </remarks>
    private static Vector128<byte> Luminance16LaneMask
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector128.Create((byte)0, 1, 0, 1, 0, 1, 0x80, 0x80, 8, 9, 8, 9, 8, 9, 0x80, 0x80);
    }

    /// <summary>
    /// Gets the byte shuffle that copies 16-bit lane 0 of each 64-bit lane to 16-bit lanes 0, 1 and 2. The 16-bit lane 1
    /// goes to 16-bit lane 3.
    /// </summary>
    /// <remarks>
    /// The source lanes hold a 16-bit luminance value in bytes 0 and 1 and a 16-bit alpha value in bytes 2 and 3. The
    /// result lanes are <see cref="Rgba64"/> pixels with red, green and blue equal to the luminance.
    /// </remarks>
    private static Vector128<byte> LuminanceAlpha16LaneMask
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector128.Create((byte)0, 1, 0, 1, 0, 1, 2, 3, 8, 9, 8, 9, 8, 9, 10, 11);
    }

    /// <summary>
    /// Scales 8-bit samples to 16-bit samples, as <see cref="ColorNumerics.From8BitTo16Bit(byte)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The scalar form is v * 257. For a byte, that is (v &lt;&lt; 8) | v, which puts the byte in both halves of the
    /// 16-bit sample. Each loop loads one register of bytes, widens its halves to 16-bit lanes in sample order, and stores
    /// two registers of samples.
    /// </para>
    /// <para>
    /// The source can be the last <c>source.Length</c> bytes of the destination storage. Each loop step loads its bytes
    /// before it stores, and each step stores 2 bytes for each byte that it loads. Thus the stores of a step end at or
    /// before the first byte of the next step.
    /// </para>
    /// </remarks>
    /// <param name="source">The 8-bit samples.</param>
    /// <param name="destination">The 16-bit samples. The caller checks the length.</param>
    public static void ScaleUnorm8To16(ReadOnlySpan<byte> source, Span<ushort> destination)
    {
        ref byte sourceBase = ref MemoryMarshal.GetReference(source);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        nuint length = (uint)source.Length;
        nuint x = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            for (; x + (uint)Vector512<byte>.Count <= length; x += (uint)Vector512<byte>.Count)
            {
                Vector512<byte> samples = Vector512.LoadUnsafe(ref sourceBase, x);
                Vector512<ushort> lower = Vector512.WidenLower(samples);
                Vector512<ushort> upper = Vector512.WidenUpper(samples);
                (Vector512.ShiftLeft(lower, 8) | lower).StoreUnsafe(ref destinationBase, x);
                (Vector512.ShiftLeft(upper, 8) | upper).StoreUnsafe(ref destinationBase, x + (uint)Vector512<ushort>.Count);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; x + (uint)Vector256<byte>.Count <= length; x += (uint)Vector256<byte>.Count)
            {
                Vector256<byte> samples = Vector256.LoadUnsafe(ref sourceBase, x);
                Vector256<ushort> lower = Vector256.WidenLower(samples);
                Vector256<ushort> upper = Vector256.WidenUpper(samples);
                (Vector256.ShiftLeft(lower, 8) | lower).StoreUnsafe(ref destinationBase, x);
                (Vector256.ShiftLeft(upper, 8) | upper).StoreUnsafe(ref destinationBase, x + (uint)Vector256<ushort>.Count);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; x + (uint)Vector128<byte>.Count <= length; x += (uint)Vector128<byte>.Count)
            {
                Vector128<byte> samples = Vector128.LoadUnsafe(ref sourceBase, x);
                Vector128<ushort> lower = Vector128.WidenLower(samples);
                Vector128<ushort> upper = Vector128.WidenUpper(samples);
                (Vector128.ShiftLeft(lower, 8) | lower).StoreUnsafe(ref destinationBase, x);
                (Vector128.ShiftLeft(upper, 8) | upper).StoreUnsafe(ref destinationBase, x + (uint)Vector128<ushort>.Count);
            }
        }

        for (; x < length; x++)
        {
            Unsafe.Add(ref destinationBase, x) = ColorNumerics.From8BitTo16Bit(Unsafe.Add(ref sourceBase, x));
        }
    }

    /// <summary>
    /// Loads <see cref="L16"/> pixels as <see cref="Rgba64"/> pixels.
    /// </summary>
    /// <remarks>
    /// Two widens zero-extend each 16-bit luminance value to a 64-bit lane, in pixel order. <see cref="Luminance16LaneMask"/>
    /// then copies the luminance to red, green and blue, and an OR with <see cref="OpaqueRgba64Alpha"/> sets alpha to
    /// 65535.
    /// </remarks>
    public readonly struct L16Rgba64Operator : IRgba64SourceOperator
    {
        /// <inheritdoc/>
        public static int BytesPerPixel => 2;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Load(ref byte row, nuint x)
        {
            ulong luminance = Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref row, x * 2));
            return luminance | (luminance << 16) | (luminance << 32) | ((ulong)ushort.MaxValue << 48);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load128(
            ref byte row,
            nuint x,
            out Vector128<ushort> pixels0,
            out Vector128<ushort> pixels1,
            out Vector128<ushort> pixels2,
            out Vector128<ushort> pixels3)
        {
            Vector128<ushort> samples = Vector128.LoadUnsafe(ref Unsafe.As<byte, ushort>(ref row), x);
            Vector128<uint> lower = Vector128.WidenLower(samples);
            Vector128<uint> upper = Vector128.WidenUpper(samples);
            Vector128<byte> mask = Luminance16LaneMask;
            Vector128<ushort> opaque = OpaqueRgba64Alpha;
            pixels0 = Vector128.ShuffleNative(Vector128.WidenLower(lower).AsByte(), mask).AsUInt16() | opaque;
            pixels1 = Vector128.ShuffleNative(Vector128.WidenUpper(lower).AsByte(), mask).AsUInt16() | opaque;
            pixels2 = Vector128.ShuffleNative(Vector128.WidenLower(upper).AsByte(), mask).AsUInt16() | opaque;
            pixels3 = Vector128.ShuffleNative(Vector128.WidenUpper(upper).AsByte(), mask).AsUInt16() | opaque;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load256(
            ref byte row,
            nuint x,
            out Vector256<ushort> pixels0,
            out Vector256<ushort> pixels1,
            out Vector256<ushort> pixels2,
            out Vector256<ushort> pixels3)
        {
            Vector256<ushort> samples = Vector256.LoadUnsafe(ref Unsafe.As<byte, ushort>(ref row), x);
            Vector256<uint> lower = Vector256.WidenLower(samples);
            Vector256<uint> upper = Vector256.WidenUpper(samples);
            Vector128<byte> mask = Luminance16LaneMask;
            Vector256<ushort> opaque = Vector256.Create(0xFFFF000000000000UL).AsUInt16();
            pixels0 = Vector256_.ShufflePerLane(Vector256.WidenLower(lower).AsByte(), mask).AsUInt16() | opaque;
            pixels1 = Vector256_.ShufflePerLane(Vector256.WidenUpper(lower).AsByte(), mask).AsUInt16() | opaque;
            pixels2 = Vector256_.ShufflePerLane(Vector256.WidenLower(upper).AsByte(), mask).AsUInt16() | opaque;
            pixels3 = Vector256_.ShufflePerLane(Vector256.WidenUpper(upper).AsByte(), mask).AsUInt16() | opaque;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load512(
            ref byte row,
            nuint x,
            out Vector512<ushort> pixels0,
            out Vector512<ushort> pixels1,
            out Vector512<ushort> pixels2,
            out Vector512<ushort> pixels3)
        {
            Vector512<ushort> samples = Vector512.LoadUnsafe(ref Unsafe.As<byte, ushort>(ref row), x);
            Vector512<uint> lower = Vector512.WidenLower(samples);
            Vector512<uint> upper = Vector512.WidenUpper(samples);
            Vector128<byte> mask = Luminance16LaneMask;
            Vector512<ushort> opaque = Vector512.Create(0xFFFF000000000000UL).AsUInt16();
            pixels0 = Vector512_.ShufflePerLane(Vector512.WidenLower(lower).AsByte(), mask).AsUInt16() | opaque;
            pixels1 = Vector512_.ShufflePerLane(Vector512.WidenUpper(lower).AsByte(), mask).AsUInt16() | opaque;
            pixels2 = Vector512_.ShufflePerLane(Vector512.WidenLower(upper).AsByte(), mask).AsUInt16() | opaque;
            pixels3 = Vector512_.ShufflePerLane(Vector512.WidenUpper(upper).AsByte(), mask).AsUInt16() | opaque;
        }
    }

    /// <summary>
    /// Loads <see cref="La32"/> pixels as <see cref="Rgba64"/> pixels.
    /// </summary>
    /// <remarks>
    /// A pixel is two 16-bit samples: luminance, then alpha. One widen zero-extends each pixel to a 64-bit lane, in pixel
    /// order. <see cref="LuminanceAlpha16LaneMask"/> then copies the luminance to red, green and blue and the alpha to
    /// alpha.
    /// </remarks>
    public readonly struct La32Rgba64Operator : IRgba64SourceOperator
    {
        /// <inheritdoc/>
        public static int BytesPerPixel => 4;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Load(ref byte row, nuint x)
        {
            ref ushort pixel = ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref row, x * 4));
            ulong luminance = pixel;
            ulong alpha = Unsafe.Add(ref pixel, (nuint)1);
            return luminance | (luminance << 16) | (luminance << 32) | (alpha << 48);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load128(
            ref byte row,
            nuint x,
            out Vector128<ushort> pixels0,
            out Vector128<ushort> pixels1,
            out Vector128<ushort> pixels2,
            out Vector128<ushort> pixels3)
        {
            ref uint source = ref Unsafe.As<byte, uint>(ref row);
            Vector128<uint> lower = Vector128.LoadUnsafe(ref source, x);
            Vector128<uint> upper = Vector128.LoadUnsafe(ref source, x + 4);
            Vector128<byte> mask = LuminanceAlpha16LaneMask;
            pixels0 = Vector128.ShuffleNative(Vector128.WidenLower(lower).AsByte(), mask).AsUInt16();
            pixels1 = Vector128.ShuffleNative(Vector128.WidenUpper(lower).AsByte(), mask).AsUInt16();
            pixels2 = Vector128.ShuffleNative(Vector128.WidenLower(upper).AsByte(), mask).AsUInt16();
            pixels3 = Vector128.ShuffleNative(Vector128.WidenUpper(upper).AsByte(), mask).AsUInt16();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load256(
            ref byte row,
            nuint x,
            out Vector256<ushort> pixels0,
            out Vector256<ushort> pixels1,
            out Vector256<ushort> pixels2,
            out Vector256<ushort> pixels3)
        {
            ref uint source = ref Unsafe.As<byte, uint>(ref row);
            Vector256<uint> lower = Vector256.LoadUnsafe(ref source, x);
            Vector256<uint> upper = Vector256.LoadUnsafe(ref source, x + 8);
            Vector128<byte> mask = LuminanceAlpha16LaneMask;
            pixels0 = Vector256_.ShufflePerLane(Vector256.WidenLower(lower).AsByte(), mask).AsUInt16();
            pixels1 = Vector256_.ShufflePerLane(Vector256.WidenUpper(lower).AsByte(), mask).AsUInt16();
            pixels2 = Vector256_.ShufflePerLane(Vector256.WidenLower(upper).AsByte(), mask).AsUInt16();
            pixels3 = Vector256_.ShufflePerLane(Vector256.WidenUpper(upper).AsByte(), mask).AsUInt16();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load512(
            ref byte row,
            nuint x,
            out Vector512<ushort> pixels0,
            out Vector512<ushort> pixels1,
            out Vector512<ushort> pixels2,
            out Vector512<ushort> pixels3)
        {
            ref uint source = ref Unsafe.As<byte, uint>(ref row);
            Vector512<uint> lower = Vector512.LoadUnsafe(ref source, x);
            Vector512<uint> upper = Vector512.LoadUnsafe(ref source, x + 16);
            Vector128<byte> mask = LuminanceAlpha16LaneMask;
            pixels0 = Vector512_.ShufflePerLane(Vector512.WidenLower(lower).AsByte(), mask).AsUInt16();
            pixels1 = Vector512_.ShufflePerLane(Vector512.WidenUpper(lower).AsByte(), mask).AsUInt16();
            pixels2 = Vector512_.ShufflePerLane(Vector512.WidenLower(upper).AsByte(), mask).AsUInt16();
            pixels3 = Vector512_.ShufflePerLane(Vector512.WidenUpper(upper).AsByte(), mask).AsUInt16();
        }
    }

    /// <summary>
    /// Loads <see cref="Rgb48"/> pixels as <see cref="Rgba64"/> pixels.
    /// </summary>
    /// <remarks>
    /// A pixel is three 16-bit samples in RGB order. Three loads read the pixels, and <c>SimdUtils.Pad3</c> with
    /// <see cref="Pad3x16Layout"/> pads each pixel to a 64-bit lane with 16-bit lane 3 set to 0. An OR with
    /// <see cref="OpaqueRgba64Alpha"/> then sets alpha to 65535.
    /// </remarks>
    public readonly struct Rgb48Rgba64Operator : IRgba64SourceOperator
    {
        /// <inheritdoc/>
        public static int BytesPerPixel => 6;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Load(ref byte row, nuint x)
        {
            ref byte pixel = ref Unsafe.Add(ref row, x * 6);
            ulong redGreen = Unsafe.ReadUnaligned<uint>(ref pixel);
            ulong blue = Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref pixel, (nuint)4));
            return redGreen | (blue << 32) | ((ulong)ushort.MaxValue << 48);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load128(
            ref byte row,
            nuint x,
            out Vector128<ushort> pixels0,
            out Vector128<ushort> pixels1,
            out Vector128<ushort> pixels2,
            out Vector128<ushort> pixels3)
        {
            // 8 pixels are 48 bytes, three full registers.
            ref byte source = ref Unsafe.Add(ref row, x * 6);
            SimdUtils.Pad3<Pad3x16Layout>(
                Vector128.LoadUnsafe(ref source),
                Vector128.LoadUnsafe(ref source, 16),
                Vector128.LoadUnsafe(ref source, 32),
                out Vector128<byte> padded0,
                out Vector128<byte> padded1,
                out Vector128<byte> padded2,
                out Vector128<byte> padded3);

            Vector128<ushort> opaque = OpaqueRgba64Alpha;
            pixels0 = padded0.AsUInt16() | opaque;
            pixels1 = padded1.AsUInt16() | opaque;
            pixels2 = padded2.AsUInt16() | opaque;
            pixels3 = padded3.AsUInt16() | opaque;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load256(
            ref byte row,
            nuint x,
            out Vector256<ushort> pixels0,
            out Vector256<ushort> pixels1,
            out Vector256<ushort> pixels2,
            out Vector256<ushort> pixels3)
        {
            // 16 pixels are 96 bytes, three full registers.
            ref byte source = ref Unsafe.Add(ref row, x * 6);
            SimdUtils.Pad3<Pad3x16Layout>(
                Vector256.LoadUnsafe(ref source),
                Vector256.LoadUnsafe(ref source, 32),
                Vector256.LoadUnsafe(ref source, 64),
                out Vector256<byte> padded0,
                out Vector256<byte> padded1,
                out Vector256<byte> padded2,
                out Vector256<byte> padded3);

            Vector256<ushort> opaque = Vector256.Create(0xFFFF000000000000UL).AsUInt16();
            pixels0 = padded0.AsUInt16() | opaque;
            pixels1 = padded1.AsUInt16() | opaque;
            pixels2 = padded2.AsUInt16() | opaque;
            pixels3 = padded3.AsUInt16() | opaque;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load512(
            ref byte row,
            nuint x,
            out Vector512<ushort> pixels0,
            out Vector512<ushort> pixels1,
            out Vector512<ushort> pixels2,
            out Vector512<ushort> pixels3)
        {
            // 32 pixels are 192 bytes, three full registers.
            ref byte source = ref Unsafe.Add(ref row, x * 6);
            SimdUtils.Pad3<Pad3x16Layout>(
                Vector512.LoadUnsafe(ref source),
                Vector512.LoadUnsafe(ref source, 64),
                Vector512.LoadUnsafe(ref source, 128),
                out Vector512<byte> padded0,
                out Vector512<byte> padded1,
                out Vector512<byte> padded2,
                out Vector512<byte> padded3);

            Vector512<ushort> opaque = Vector512.Create(0xFFFF000000000000UL).AsUInt16();
            pixels0 = padded0.AsUInt16() | opaque;
            pixels1 = padded1.AsUInt16() | opaque;
            pixels2 = padded2.AsUInt16() | opaque;
            pixels3 = padded3.AsUInt16() | opaque;
        }
    }

    /// <summary>
    /// Loads <see cref="Rgba64"/> pixels.
    /// </summary>
    /// <remarks>
    /// The source layout is the <see cref="Rgba64"/> lane layout, so each register loads as it is. The operator is the
    /// source of the conversion to <see cref="Rgb48"/>.
    /// </remarks>
    public readonly struct Rgba64Rgba64Operator : IRgba64SourceOperator
    {
        /// <inheritdoc/>
        public static int BytesPerPixel => 8;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Load(ref byte row, nuint x) => Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref row, x * 8));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load128(
            ref byte row,
            nuint x,
            out Vector128<ushort> pixels0,
            out Vector128<ushort> pixels1,
            out Vector128<ushort> pixels2,
            out Vector128<ushort> pixels3)
        {
            ref ushort source = ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref row, x * 8));
            pixels0 = Vector128.LoadUnsafe(ref source);
            pixels1 = Vector128.LoadUnsafe(ref source, 8);
            pixels2 = Vector128.LoadUnsafe(ref source, 16);
            pixels3 = Vector128.LoadUnsafe(ref source, 24);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load256(
            ref byte row,
            nuint x,
            out Vector256<ushort> pixels0,
            out Vector256<ushort> pixels1,
            out Vector256<ushort> pixels2,
            out Vector256<ushort> pixels3)
        {
            ref ushort source = ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref row, x * 8));
            pixels0 = Vector256.LoadUnsafe(ref source);
            pixels1 = Vector256.LoadUnsafe(ref source, 16);
            pixels2 = Vector256.LoadUnsafe(ref source, 32);
            pixels3 = Vector256.LoadUnsafe(ref source, 48);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load512(
            ref byte row,
            nuint x,
            out Vector512<ushort> pixels0,
            out Vector512<ushort> pixels1,
            out Vector512<ushort> pixels2,
            out Vector512<ushort> pixels3)
        {
            ref ushort source = ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref row, x * 8));
            pixels0 = Vector512.LoadUnsafe(ref source);
            pixels1 = Vector512.LoadUnsafe(ref source, 32);
            pixels2 = Vector512.LoadUnsafe(ref source, 64);
            pixels3 = Vector512.LoadUnsafe(ref source, 96);
        }
    }

    /// <summary>
    /// Converts the pixels of a <typeparamref name="TSource"/> layout to the layouts with 16-bit channels.
    /// </summary>
    /// <remarks>
    /// Each loop loads 4 registers of <see cref="Rgba64"/> pixels from the source operator. The widest loop runs first,
    /// and each narrower loop continues from the same pixel, so only fewer than 8 pixels stay for the scalar loop.
    /// </remarks>
    /// <typeparam name="TSource">The source operator.</typeparam>
    public static class Rgba64SourceKernels<TSource>
        where TSource : IRgba64SourceOperator
    {
        /// <summary>
        /// Converts the source pixels to <see cref="Rgba64"/> pixels.
        /// </summary>
        /// <param name="source">The source pixels.</param>
        /// <param name="destination">The destination pixels, 8 bytes each. The caller checks the length.</param>
        public static void ToRgba64(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            ref byte sourceBase = ref MemoryMarshal.GetReference(source);
            ref ushort destinationBase = ref Unsafe.As<byte, ushort>(ref MemoryMarshal.GetReference(destination));
            nuint length = (uint)(source.Length / TSource.BytesPerPixel);
            nuint x = 0;

            // Pixel x starts at sample 4x of the destination.
            if (Vector512.IsHardwareAccelerated)
            {
                for (; x + 32 <= length; x += 32)
                {
                    TSource.Load512(
                        ref sourceBase,
                        x,
                        out Vector512<ushort> pixels0,
                        out Vector512<ushort> pixels1,
                        out Vector512<ushort> pixels2,
                        out Vector512<ushort> pixels3);

                    ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, x * 4);
                    pixels0.StoreUnsafe(ref destinationRow);
                    pixels1.StoreUnsafe(ref destinationRow, 32);
                    pixels2.StoreUnsafe(ref destinationRow, 64);
                    pixels3.StoreUnsafe(ref destinationRow, 96);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x + 16 <= length; x += 16)
                {
                    TSource.Load256(
                        ref sourceBase,
                        x,
                        out Vector256<ushort> pixels0,
                        out Vector256<ushort> pixels1,
                        out Vector256<ushort> pixels2,
                        out Vector256<ushort> pixels3);

                    ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, x * 4);
                    pixels0.StoreUnsafe(ref destinationRow);
                    pixels1.StoreUnsafe(ref destinationRow, 16);
                    pixels2.StoreUnsafe(ref destinationRow, 32);
                    pixels3.StoreUnsafe(ref destinationRow, 48);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x + 8 <= length; x += 8)
                {
                    TSource.Load128(
                        ref sourceBase,
                        x,
                        out Vector128<ushort> pixels0,
                        out Vector128<ushort> pixels1,
                        out Vector128<ushort> pixels2,
                        out Vector128<ushort> pixels3);

                    ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, x * 4);
                    pixels0.StoreUnsafe(ref destinationRow);
                    pixels1.StoreUnsafe(ref destinationRow, 8);
                    pixels2.StoreUnsafe(ref destinationRow, 16);
                    pixels3.StoreUnsafe(ref destinationRow, 24);
                }
            }

            for (; x < length; x++)
            {
                Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref destinationBase, x * 4)), TSource.Load(ref sourceBase, x));
            }
        }

        /// <summary>
        /// Converts the source pixels to <see cref="Rgb48"/> pixels. The conversion drops alpha.
        /// </summary>
        /// <remarks>
        /// <c>SimdUtils.Slice3</c> with <see cref="Pad3x16Layout"/> slices 16-bit lanes 0 to 2 of each 64-bit lane of four
        /// registers to three full registers of <see cref="Rgb48"/> pixels.
        /// </remarks>
        /// <param name="source">The source pixels.</param>
        /// <param name="destination">The destination pixels, 6 bytes each. The caller checks the length.</param>
        public static void ToRgb48(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            ref byte sourceBase = ref MemoryMarshal.GetReference(source);
            ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
            nuint length = (uint)(source.Length / TSource.BytesPerPixel);
            nuint x = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                for (; x + 32 <= length; x += 32)
                {
                    TSource.Load512(
                        ref sourceBase,
                        x,
                        out Vector512<ushort> pixels0,
                        out Vector512<ushort> pixels1,
                        out Vector512<ushort> pixels2,
                        out Vector512<ushort> pixels3);

                    SimdUtils.Slice3<Pad3x16Layout>(
                        pixels0.AsByte(),
                        pixels1.AsByte(),
                        pixels2.AsByte(),
                        pixels3.AsByte(),
                        out Vector512<byte> destination0,
                        out Vector512<byte> destination1,
                        out Vector512<byte> destination2);

                    ref byte destinationRow = ref Unsafe.Add(ref destinationBase, x * 6);
                    destination0.StoreUnsafe(ref destinationRow);
                    destination1.StoreUnsafe(ref destinationRow, 64);
                    destination2.StoreUnsafe(ref destinationRow, 128);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x + 16 <= length; x += 16)
                {
                    TSource.Load256(
                        ref sourceBase,
                        x,
                        out Vector256<ushort> pixels0,
                        out Vector256<ushort> pixels1,
                        out Vector256<ushort> pixels2,
                        out Vector256<ushort> pixels3);

                    SimdUtils.Slice3<Pad3x16Layout>(
                        pixels0.AsByte(),
                        pixels1.AsByte(),
                        pixels2.AsByte(),
                        pixels3.AsByte(),
                        out Vector256<byte> destination0,
                        out Vector256<byte> destination1,
                        out Vector256<byte> destination2);

                    ref byte destinationRow = ref Unsafe.Add(ref destinationBase, x * 6);
                    destination0.StoreUnsafe(ref destinationRow);
                    destination1.StoreUnsafe(ref destinationRow, 32);
                    destination2.StoreUnsafe(ref destinationRow, 64);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x + 8 <= length; x += 8)
                {
                    TSource.Load128(
                        ref sourceBase,
                        x,
                        out Vector128<ushort> pixels0,
                        out Vector128<ushort> pixels1,
                        out Vector128<ushort> pixels2,
                        out Vector128<ushort> pixels3);

                    SimdUtils.Slice3<Pad3x16Layout>(
                        pixels0.AsByte(),
                        pixels1.AsByte(),
                        pixels2.AsByte(),
                        pixels3.AsByte(),
                        out Vector128<byte> destination0,
                        out Vector128<byte> destination1,
                        out Vector128<byte> destination2);

                    ref byte destinationRow = ref Unsafe.Add(ref destinationBase, x * 6);
                    destination0.StoreUnsafe(ref destinationRow);
                    destination1.StoreUnsafe(ref destinationRow, 16);
                    destination2.StoreUnsafe(ref destinationRow, 32);
                }
            }

            // A 6-byte store from a register: one 32-bit store writes red and green, and one 16-bit store writes blue.
            for (; x < length; x++)
            {
                ulong pixel = TSource.Load(ref sourceBase, x);
                ref byte pixelBase = ref Unsafe.Add(ref destinationBase, x * 6);
                Unsafe.WriteUnaligned(ref pixelBase, (uint)pixel);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref pixelBase, (nuint)4), (ushort)(pixel >> 32));
            }
        }
    }
}
