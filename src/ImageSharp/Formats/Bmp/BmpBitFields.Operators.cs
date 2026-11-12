// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Bmp;

/// <content>
/// Contains the stored sample operators that <see cref="Unpack{TSample}"/> and <see cref="UnpackScaled{TSample}"/> reads a row with.
/// </content>
internal readonly partial struct BmpBitFields
{
    /// <summary>
    /// Loads stored little-endian samples of one width, zero-extended to 32-bit lanes.
    /// </summary>
    /// <remarks>
    /// Each form loads the samples of pixels <c>x</c> to <c>x + N - 1</c>, with <c>N</c> the lane count. The caller
    /// keeps <c>x + N</c> at or below the row width, so a load never reads past the stored samples of the row.
    /// </remarks>
    internal interface ISampleOperator
    {
        /// <summary>
        /// Gets the byte count of one stored sample.
        /// </summary>
        static abstract int BytesPerSample { get; }

        /// <summary>
        /// Loads the sample of pixel <paramref name="x"/>.
        /// </summary>
        /// <param name="row">The first byte of the stored row.</param>
        /// <param name="x">The pixel index.</param>
        /// <returns>The sample.</returns>
        static abstract uint Load(ref byte row, nuint x);

        /// <summary>
        /// Loads the samples of 4 pixels from pixel <paramref name="x"/>.
        /// </summary>
        /// <param name="row">The first byte of the stored row.</param>
        /// <param name="x">The index of the first pixel.</param>
        /// <returns>The samples, one in each lane.</returns>
        static abstract Vector128<uint> Load128(ref byte row, nuint x);

        /// <summary>
        /// Loads the samples of 8 pixels from pixel <paramref name="x"/>.
        /// </summary>
        /// <param name="row">The first byte of the stored row.</param>
        /// <param name="x">The index of the first pixel.</param>
        /// <returns>The samples, one in each lane.</returns>
        static abstract Vector256<uint> Load256(ref byte row, nuint x);

        /// <summary>
        /// Loads the samples of 16 pixels from pixel <paramref name="x"/>.
        /// </summary>
        /// <param name="row">The first byte of the stored row.</param>
        /// <param name="x">The index of the first pixel.</param>
        /// <returns>The samples, one in each lane.</returns>
        static abstract Vector512<uint> Load512(ref byte row, nuint x);
    }

    /// <summary>
    /// Loads 16-bit samples.
    /// </summary>
    /// <remarks>
    /// Each form loads half a register of 16-bit samples, then widens the low half to 32-bit lanes. The widen
    /// keeps the sample order, so lane <c>i</c> holds the sample of pixel <c>x + i</c>.
    /// </remarks>
    internal readonly struct Sample16Operator : ISampleOperator
    {
        /// <inheritdoc/>
        public static int BytesPerSample => 2;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Load(ref byte row, nuint x)
            => Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref row, x * 2));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<uint> Load128(ref byte row, nuint x)
        {
            // 4 samples are 8 bytes: one 64-bit load into the low half of the register.
            ulong samples = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref row, x * 2));
            return Vector128.WidenLower(Vector128.CreateScalarUnsafe(samples).AsUInt16());
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<uint> Load256(ref byte row, nuint x)
        {
            // 8 samples are 16 bytes: one 128-bit load.
            Vector128<ushort> samples = Vector128.LoadUnsafe(ref Unsafe.As<byte, ushort>(ref row), x);
            return Vector256.WidenLower(samples.ToVector256Unsafe());
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<uint> Load512(ref byte row, nuint x)
        {
            // 16 samples are 32 bytes: one 256-bit load.
            Vector256<ushort> samples = Vector256.LoadUnsafe(ref Unsafe.As<byte, ushort>(ref row), x);
            return Vector512.WidenLower(samples.ToVector512Unsafe());
        }
    }

    /// <summary>
    /// Loads 32-bit samples.
    /// </summary>
    internal readonly struct Sample32Operator : ISampleOperator
    {
        /// <inheritdoc/>
        public static int BytesPerSample => 4;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Load(ref byte row, nuint x)
            => Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref row, x * 4));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<uint> Load128(ref byte row, nuint x)
            => Vector128.LoadUnsafe(ref Unsafe.As<byte, uint>(ref row), x);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<uint> Load256(ref byte row, nuint x)
            => Vector256.LoadUnsafe(ref Unsafe.As<byte, uint>(ref row), x);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<uint> Load512(ref byte row, nuint x)
            => Vector512.LoadUnsafe(ref Unsafe.As<byte, uint>(ref row), x);
    }
}
