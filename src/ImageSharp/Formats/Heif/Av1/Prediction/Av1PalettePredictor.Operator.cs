// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <content>
/// Defines the closed scalar/SIMD operator contract and traversal for AV1 palette prediction.
/// </content>
internal static class Av1PalettePredictor
{
    /// <summary>
    /// Multiplies a palette index by two to select the low byte of a high-bit-depth entry.
    /// </summary>
    private const ushort PaletteByteOffsetMultiplier = 0x0202;

    /// <summary>
    /// Adds one to each odd control byte so each shuffled high-bit-depth sample retains both bytes.
    /// </summary>
    private const ushort PaletteHighByteOffset = 0x0100;

    /// <summary>
    /// Defines scalar and SIMD palette-index lookup.
    /// </summary>
    private interface IPaletteOperator
    {
        /// <summary>
        /// Predicts one 8-bit sample.
        /// </summary>
        /// <param name="palette">The first palette entry.</param>
        /// <param name="index">The palette index.</param>
        /// <returns>The selected sample.</returns>
        public static abstract byte Predict(ref byte palette, byte index);

        /// <summary>
        /// Predicts sixteen 8-bit samples.
        /// </summary>
        /// <param name="palette">The palette entries repeated in each 128-bit lane.</param>
        /// <param name="indices">The palette indices.</param>
        /// <returns>The selected samples.</returns>
        public static abstract Vector128<byte> Predict(Vector128<byte> palette, Vector128<byte> indices);

        /// <summary>
        /// Predicts thirty-two 8-bit samples.
        /// </summary>
        /// <param name="palette">The palette entries repeated in each 128-bit lane.</param>
        /// <param name="indices">The palette indices.</param>
        /// <returns>The selected samples.</returns>
        public static abstract Vector256<byte> Predict(Vector256<byte> palette, Vector256<byte> indices);

        /// <summary>
        /// Predicts sixty-four 8-bit samples.
        /// </summary>
        /// <param name="palette">The palette entries repeated in each 128-bit lane.</param>
        /// <param name="indices">The palette indices.</param>
        /// <returns>The selected samples.</returns>
        public static abstract Vector512<byte> Predict(Vector512<byte> palette, Vector512<byte> indices);

        /// <summary>
        /// Predicts one high-bit-depth sample.
        /// </summary>
        /// <param name="palette">The first palette entry.</param>
        /// <param name="index">The palette index.</param>
        /// <returns>The selected sample.</returns>
        public static abstract short Predict(ref ushort palette, byte index);

        /// <summary>
        /// Predicts eight high-bit-depth samples.
        /// </summary>
        /// <param name="palette">The palette bytes repeated in each 128-bit lane.</param>
        /// <param name="indices">The palette indices.</param>
        /// <returns>The selected samples.</returns>
        public static abstract Vector128<short> Predict(Vector128<byte> palette, Vector128<ushort> indices);

        /// <summary>
        /// Predicts sixteen high-bit-depth samples.
        /// </summary>
        /// <param name="palette">The palette bytes repeated in each 128-bit lane.</param>
        /// <param name="indices">The palette indices.</param>
        /// <returns>The selected samples.</returns>
        public static abstract Vector256<short> Predict(Vector256<byte> palette, Vector256<ushort> indices);

        /// <summary>
        /// Predicts thirty-two high-bit-depth samples.
        /// </summary>
        /// <param name="palette">The palette bytes repeated in each 128-bit lane.</param>
        /// <param name="indices">The palette indices.</param>
        /// <returns>The selected samples.</returns>
        public static abstract Vector512<short> Predict(Vector512<byte> palette, Vector512<ushort> indices);
    }

    /// <summary>
    /// Reconstructs an 8-bit palette-predicted block.
    /// </summary>
    public static void Predict(
        ReadOnlySpan<ushort> paletteColors,
        ReadOnlySpan<byte> colorIndexMap,
        int colorIndexMapStride,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height)
        => Predictor<PaletteOperator>.Predict(paletteColors, colorIndexMap, colorIndexMapStride, destination, destinationStride, width, height);

    /// <summary>
    /// Reconstructs a high-bit-depth palette-predicted block.
    /// </summary>
    public static void Predict(
        ReadOnlySpan<ushort> paletteColors,
        ReadOnlySpan<byte> colorIndexMap,
        int colorIndexMapStride,
        Span<short> destination,
        int destinationStride,
        int width,
        int height)
        => Predictor<PaletteOperator>.Predict(paletteColors, colorIndexMap, colorIndexMapStride, destination, destinationStride, width, height);

    /// <summary>
    /// Maps decoded palette indices to reconstructed samples.
    /// </summary>
    private readonly struct PaletteOperator : IPaletteOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Predict(ref byte palette, byte index) => Unsafe.Add(ref palette, index);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Predict(Vector128<byte> palette, Vector128<byte> indices)
            => Vector128.ShuffleNative(palette, indices);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Predict(Vector256<byte> palette, Vector256<byte> indices)
            => Vector256.ShuffleNative(palette, indices);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> Predict(Vector512<byte> palette, Vector512<byte> indices)
            => Vector512.ShuffleNative(palette, indices);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short Predict(ref ushort palette, byte index) => (short)Unsafe.Add(ref palette, index);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> Predict(Vector128<byte> palette, Vector128<ushort> indices)
        {
            Vector128<ushort> controls = (indices * Vector128.Create(PaletteByteOffsetMultiplier)) + Vector128.Create(PaletteHighByteOffset);

            return Vector128.ShuffleNative(palette, controls.AsByte()).AsInt16();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<short> Predict(Vector256<byte> palette, Vector256<ushort> indices)
        {
            Vector256<ushort> controls = (indices * Vector256.Create(PaletteByteOffsetMultiplier)) + Vector256.Create(PaletteHighByteOffset);

            return Vector256.ShuffleNative(palette, controls.AsByte()).AsInt16();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<short> Predict(Vector512<byte> palette, Vector512<ushort> indices)
        {
            Vector512<ushort> controls = (indices * Vector512.Create(PaletteByteOffsetMultiplier)) + Vector512.Create(PaletteHighByteOffset);

            return Vector512.ShuffleNative(palette, controls.AsByte()).AsInt16();
        }
    }

    /// <summary>
    /// Traverses palette blocks through one closed lookup operator.
    /// </summary>
    /// <typeparam name="TOperator">The palette lookup arithmetic.</typeparam>
    private static class Predictor<TOperator>
        where TOperator : struct, IPaletteOperator
    {
        /// <summary>
        /// Reconstructs an 8-bit palette block.
        /// </summary>
        public static void Predict(
            ReadOnlySpan<ushort> paletteColors,
            ReadOnlySpan<byte> colorIndexMap,
            int colorIndexMapStride,
            Span<byte> destination,
            int destinationStride,
            int width,
            int height)
        {
            ref byte mapBase = ref MemoryMarshal.GetReference(colorIndexMap);
            ref byte destinationBase = ref MemoryMarshal.GetReference(destination);

            // AV1 palettes contain at most eight colors. Repeating all eight entries in every 128-bit lane keeps native
            // table lookup lane-local at every SIMD width and removes palette bounds work from the reconstruction loop.
            ulong packedPalette = 0;
            for (int index = 0; index < paletteColors.Length; index++)
            {
                packedPalette |= (ulong)(byte)paletteColors[index] << (index * 8);
            }

            ref byte paletteBase = ref Unsafe.As<ulong, byte>(ref packedPalette);
            Vector128<byte> palette128 = Vector128.Create(packedPalette, packedPalette).AsByte();

            for (int row = 0; row < height; row++)
            {
                ref byte mapRow = ref Unsafe.Add(ref mapBase, row * colorIndexMapStride);
                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
                int column = 0;

                if (Vector512.IsHardwareAccelerated)
                {
                    Vector256<byte> palette256 = Vector256.Create(palette128, palette128);
                    Vector512<byte> palette512 = Vector512.Create(palette256, palette256);
                    int oneVectorFromEnd = width - Vector512<byte>.Count;

                    for (; column <= oneVectorFromEnd; column += Vector512<byte>.Count)
                    {
                        Vector512<byte> indices = Vector512.LoadUnsafe(ref mapRow, (nuint)column);
                        TOperator.Predict(palette512, indices).StoreUnsafe(ref destinationRow, (nuint)column);
                    }
                }

                if (Vector256.IsHardwareAccelerated)
                {
                    Vector256<byte> palette256 = Vector256.Create(palette128, palette128);
                    int oneVectorFromEnd = width - Vector256<byte>.Count;

                    for (; column <= oneVectorFromEnd; column += Vector256<byte>.Count)
                    {
                        Vector256<byte> indices = Vector256.LoadUnsafe(ref mapRow, (nuint)column);
                        TOperator.Predict(palette256, indices).StoreUnsafe(ref destinationRow, (nuint)column);
                    }
                }

                if (Vector128.IsHardwareAccelerated)
                {
                    int oneVectorFromEnd = width - Vector128<byte>.Count;
                    for (; column <= oneVectorFromEnd; column += Vector128<byte>.Count)
                    {
                        Vector128<byte> indices = Vector128.LoadUnsafe(ref mapRow, (nuint)column);
                        TOperator.Predict(palette128, indices).StoreUnsafe(ref destinationRow, (nuint)column);
                    }

                    int remaining = width - column;
                    if (remaining >= 8)
                    {
                        ulong packedIndices = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref mapRow, column));
                        Vector128<byte> prediction = TOperator.Predict(palette128, Vector128.CreateScalarUnsafe(packedIndices).AsByte());
                        Unsafe.WriteUnaligned(ref Unsafe.Add(ref destinationRow, column), prediction.AsUInt64().ToScalar());
                        column += 8;
                        remaining -= 8;
                    }

                    if (remaining >= 4)
                    {
                        uint packedIndices = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref mapRow, column));
                        Vector128<byte> prediction = TOperator.Predict(palette128, Vector128.CreateScalarUnsafe(packedIndices).AsByte());
                        Unsafe.WriteUnaligned(ref Unsafe.Add(ref destinationRow, column), prediction.AsUInt32().ToScalar());
                        column += 4;
                    }
                }

                for (; column < width; column++)
                {
                    Unsafe.Add(ref destinationRow, column) = TOperator.Predict(ref paletteBase, Unsafe.Add(ref mapRow, column));
                }
            }
        }

        /// <summary>
        /// Reconstructs a high-bit-depth palette block.
        /// </summary>
        public static void Predict(
            ReadOnlySpan<ushort> paletteColors,
            ReadOnlySpan<byte> colorIndexMap,
            int colorIndexMapStride,
            Span<short> destination,
            int destinationStride,
            int width,
            int height)
        {
            ref byte mapBase = ref MemoryMarshal.GetReference(colorIndexMap);
            ref short destinationBase = ref MemoryMarshal.GetReference(destination);
            InlineArray8<ushort> paletteStorage = default;
            paletteColors.CopyTo(paletteStorage);

            ref ushort paletteBase = ref paletteStorage[0];
            Vector128<byte> palette128 = Vector128.LoadUnsafe(ref paletteBase).AsByte();

            for (int row = 0; row < height; row++)
            {
                ref byte mapRow = ref Unsafe.Add(ref mapBase, row * colorIndexMapStride);
                ref short destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
                int column = 0;

                if (Vector512.IsHardwareAccelerated)
                {
                    Vector256<byte> palette256 = Vector256.Create(palette128, palette128);
                    Vector512<byte> palette512 = Vector512.Create(palette256, palette256);
                    int oneVectorFromEnd = width - Vector512<short>.Count;

                    for (; column <= oneVectorFromEnd; column += Vector512<short>.Count)
                    {
                        (Vector256<ushort> lower, Vector256<ushort> upper) = Vector256.Widen(Vector256.LoadUnsafe(ref mapRow, (nuint)column));
                        Vector512<ushort> indices = Vector512.Create(lower, upper);
                        TOperator.Predict(palette512, indices).StoreUnsafe(ref destinationRow, (nuint)column);
                    }
                }

                if (Vector256.IsHardwareAccelerated)
                {
                    Vector256<byte> palette256 = Vector256.Create(palette128, palette128);
                    int oneVectorFromEnd = width - Vector256<short>.Count;

                    for (; column <= oneVectorFromEnd; column += Vector256<short>.Count)
                    {
                        (Vector128<ushort> lower, Vector128<ushort> upper) = Vector128.Widen(Vector128.LoadUnsafe(ref mapRow, (nuint)column));
                        Vector256<ushort> indices = Vector256.Create(lower, upper);
                        TOperator.Predict(palette256, indices).StoreUnsafe(ref destinationRow, (nuint)column);
                    }
                }

                if (Vector128.IsHardwareAccelerated)
                {
                    int oneVectorFromEnd = width - Vector128<short>.Count;
                    for (; column <= oneVectorFromEnd; column += Vector128<short>.Count)
                    {
                        ulong packedIndices = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref mapRow, column));
                        Vector128<ushort> indices = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packedIndices).AsByte());
                        TOperator.Predict(palette128, indices).StoreUnsafe(ref destinationRow, (nuint)column);
                    }

                    if (width - column >= 4)
                    {
                        uint packedIndices = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref mapRow, column));
                        Vector128<ushort> indices = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packedIndices).AsByte());
                        Vector128<short> prediction = TOperator.Predict(palette128, indices);
                        Unsafe.WriteUnaligned(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref destinationRow, column)), prediction.AsUInt64().ToScalar());
                        column += 4;
                    }
                }

                for (; column < width; column++)
                {
                    Unsafe.Add(ref destinationRow, column) = TOperator.Predict(ref paletteBase, Unsafe.Add(ref mapRow, column));
                }
            }
        }
    }
}
