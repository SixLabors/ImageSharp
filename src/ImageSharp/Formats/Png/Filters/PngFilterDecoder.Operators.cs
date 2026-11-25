// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Png.Filters;

/// <content>
/// Contains the pixel operators that <see cref="Decode{TOperator, TPixelOperator}"/> reads and writes a scanline with.
/// </content>
internal static partial class PngFilterDecoder
{
    /// <summary>
    /// Loads and stores the bytes of one pixel in the low byte lanes of a vector.
    /// </summary>
    /// <remarks>
    /// Each form reads or writes exactly the bytes of one pixel. Thus the load of the last pixel does not read past the
    /// row. The store does not change the next pixel, which the next load reads.
    /// </remarks>
    private interface IPixelOperator
    {
        /// <summary>
        /// Gets the byte count of one pixel.
        /// </summary>
        static abstract int BytesPerPixel { get; }

        /// <summary>
        /// Loads the bytes of one pixel to the low lanes. The other lanes are zero.
        /// </summary>
        /// <param name="pixel">The first byte of the pixel.</param>
        /// <returns>The pixel bytes.</returns>
        static abstract Vector128<byte> Load(ref byte pixel);

        /// <summary>
        /// Stores the low lanes as the bytes of one pixel. No other byte changes.
        /// </summary>
        /// <param name="pixel">The first byte of the pixel.</param>
        /// <param name="value">The pixel bytes.</param>
        static abstract void Store(ref byte pixel, Vector128<byte> value);
    }

    /// <summary>
    /// Loads and stores pixels of 1 byte: one byte lane.
    /// </summary>
    private readonly struct Pixel8Operator : IPixelOperator
    {
        /// <inheritdoc/>
        public static int BytesPerPixel => 1;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Load(ref byte pixel)
            => Vector128.CreateScalar(pixel);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(ref byte pixel, Vector128<byte> value)
            => pixel = value.ToScalar();
    }

    /// <summary>
    /// Loads and stores pixels of 2 bytes: one 16-bit lane.
    /// </summary>
    private readonly struct Pixel16Operator : IPixelOperator
    {
        /// <inheritdoc/>
        public static int BytesPerPixel => 2;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Load(ref byte pixel)
            => Vector128.CreateScalar(Unsafe.ReadUnaligned<ushort>(ref pixel)).AsByte();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(ref byte pixel, Vector128<byte> value)
            => Unsafe.WriteUnaligned(ref pixel, value.AsUInt16().ToScalar());
    }

    /// <summary>
    /// Loads and stores pixels of 3 bytes: one 16-bit lane, then byte lane 2.
    /// </summary>
    private readonly struct Pixel24Operator : IPixelOperator
    {
        /// <inheritdoc/>
        public static int BytesPerPixel => 3;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Load(ref byte pixel)
        {
            // A 4-byte load reads past the end of the row at the last pixel. Thus the third byte is a separate lane insert.
            return Vector128.CreateScalar(Unsafe.ReadUnaligned<ushort>(ref pixel)).AsByte().WithElement(2, Unsafe.Add(ref pixel, (nuint)2));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(ref byte pixel, Vector128<byte> value)
        {
            Unsafe.WriteUnaligned(ref pixel, value.AsUInt16().ToScalar());
            Unsafe.Add(ref pixel, (nuint)2) = value.GetElement(2);
        }
    }

    /// <summary>
    /// Loads and stores pixels of 4 bytes: one 32-bit lane.
    /// </summary>
    private readonly struct Pixel32Operator : IPixelOperator
    {
        /// <inheritdoc/>
        public static int BytesPerPixel => 4;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Load(ref byte pixel)
            => Vector128.CreateScalar(Unsafe.ReadUnaligned<uint>(ref pixel)).AsByte();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(ref byte pixel, Vector128<byte> value)
            => Unsafe.WriteUnaligned(ref pixel, value.AsUInt32().ToScalar());
    }

    /// <summary>
    /// Loads and stores pixels of 6 bytes: one 32-bit lane, then 16-bit lane 2.
    /// </summary>
    private readonly struct Pixel48Operator : IPixelOperator
    {
        /// <inheritdoc/>
        public static int BytesPerPixel => 6;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Load(ref byte pixel)
        {
            // An 8-byte load reads past the end of the row at the last pixel. Thus bytes 4 and 5 are a separate lane insert.
            return Vector128.CreateScalar(Unsafe.ReadUnaligned<uint>(ref pixel)).AsUInt16()
                .WithElement(2, Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref pixel, (nuint)4))).AsByte();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(ref byte pixel, Vector128<byte> value)
        {
            Unsafe.WriteUnaligned(ref pixel, value.AsUInt32().ToScalar());
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref pixel, (nuint)4), value.AsUInt16().GetElement(2));
        }
    }

    /// <summary>
    /// Loads and stores pixels of 8 bytes: one 64-bit lane.
    /// </summary>
    private readonly struct Pixel64Operator : IPixelOperator
    {
        /// <inheritdoc/>
        public static int BytesPerPixel => 8;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Load(ref byte pixel)
            => Vector128.CreateScalar(Unsafe.ReadUnaligned<ulong>(ref pixel)).AsByte();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(ref byte pixel, Vector128<byte> value)
            => Unsafe.WriteUnaligned(ref pixel, value.AsUInt64().ToScalar());
    }
}
