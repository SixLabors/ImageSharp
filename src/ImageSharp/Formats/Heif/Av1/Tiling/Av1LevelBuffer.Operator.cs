// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <content>
/// Defines the arithmetic contract consumed by the shared level-plane traversal.
/// </content>
internal sealed partial class Av1LevelBuffer
{
    /// <summary>
    /// Reduces coefficients to the saturated magnitudes that entropy contexts read.
    /// </summary>
    /// <remarks>
    /// Every member describes the same lane-wise reduction. A context never distinguishes magnitudes above 127. The clamp at 127
    /// lets the plane hold one byte for each coefficient instead of four.
    /// </remarks>
    internal interface IAv1LevelOperator
    {
        /// <summary>
        /// Reduces one coefficient.
        /// </summary>
        /// <param name="value">The signed coefficient.</param>
        /// <returns>The saturated magnitude.</returns>
        public static abstract byte Saturate(int value);

        /// <summary>
        /// Reduces sixteen consecutive coefficients to sixteen bytes in the same order.
        /// </summary>
        /// <param name="source">The first coefficient.</param>
        /// <returns>The saturated magnitudes.</returns>
        public static abstract Vector128<byte> Pack16(ref int source);

        /// <summary>
        /// Reduces thirty-two consecutive coefficients to thirty-two bytes in the same order.
        /// </summary>
        /// <param name="source">The first coefficient.</param>
        /// <returns>The saturated magnitudes.</returns>
        public static abstract Vector256<byte> Pack32(ref int source);
    }

    /// <summary>
    /// Takes the magnitude of a coefficient and clamps it to the largest signed byte.
    /// </summary>
    /// <remarks>
    /// The vector forms use two saturating packs, from thirty-two bits to sixteen and from sixteen to eight. They take the magnitude
    /// between the packs, then clamp it to 127 with an unsigned minimum before the second pack.
    /// </remarks>
    private readonly struct LevelOperator : IAv1LevelOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Saturate(int value) => (byte)Math.Min(Math.Abs(value), sbyte.MaxValue);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Pack16(ref int source)
        {
            // The 128-bit packs keep the lane order, so the sixteen bytes come out in coefficient order. The magnitude of
            // a lane that saturated to -32768 wraps to itself, so an unsigned minimum clamps every magnitude to 127
            // before the second pack, as the scalar form does.
            Vector128<ushort> limit = Vector128.Create((ushort)sbyte.MaxValue);
            Vector128<short> first = Vector128.Min(
                Vector128.Abs(Vector128_.PackSignedSaturate(Vector128.LoadUnsafe(ref source), Vector128.LoadUnsafe(ref source, 4))).AsUInt16(),
                limit).AsInt16();

            Vector128<short> second = Vector128.Min(
                Vector128.Abs(Vector128_.PackSignedSaturate(Vector128.LoadUnsafe(ref source, 8), Vector128.LoadUnsafe(ref source, 12))).AsUInt16(),
                limit).AsInt16();

            return Vector128_.PackSignedSaturate(first, second).AsByte();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Pack32(ref int source)
        {
            // The 256-bit packs work within each 128-bit lane. With the coefficients as eight groups of four, A0 A1
            // to D0 D1, the two packs leave the groups in the order A0 B0 C0 D0 A1 B1 C1 D1. One permute of the
            // four-byte groups restores A0 A1 B0 B1 C0 C1 D0 D1. The unsigned minimum clamps every magnitude to 127, as
            // in the 128-bit form.
            Vector256<ushort> limit = Vector256.Create((ushort)sbyte.MaxValue);
            Vector256<short> first = Vector256.Min(
                Vector256.Abs(Vector256_.PackSignedSaturate(Vector256.LoadUnsafe(ref source), Vector256.LoadUnsafe(ref source, 8))).AsUInt16(),
                limit).AsInt16();

            Vector256<short> second = Vector256.Min(
                Vector256.Abs(Vector256_.PackSignedSaturate(Vector256.LoadUnsafe(ref source, 16), Vector256.LoadUnsafe(ref source, 24))).AsUInt16(),
                limit).AsInt16();

            Vector256<int> packed = Vector256_.PackSignedSaturate(first, second).AsInt32();
            return Vector256.Shuffle(packed, Vector256.Create(0, 4, 1, 5, 2, 6, 3, 7)).AsByte();
        }
    }

    /// <summary>
    /// Fills the level plane with the arithmetic of a closed reduction operator.
    /// </summary>
    /// <typeparam name="TOperator">The lane-wise reduction.</typeparam>
    /// <remarks>
    /// Each padded row is the row's levels followed by four zero bytes, the right-hand neighbors of its last columns.
    /// One step reduces thirty-two coefficients, or sixteen without 256-bit vectors, and writes as many rows as they
    /// cover, so a narrow transform writes several rows per step.
    /// </remarks>
    private static class Levels<TOperator>
        where TOperator : struct, IAv1LevelOperator
    {
        /// <summary>
        /// Fills the level plane of a transform and the padding after each row.
        /// </summary>
        /// <param name="source">The first coefficient of the transform, in raster order.</param>
        /// <param name="destination">The first level of the first row.</param>
        /// <param name="width">The number of coefficients in a row: 4, 8, 16 or 32.</param>
        /// <param name="widthLog2">The base-two logarithm of <paramref name="width"/>.</param>
        /// <param name="height">The number of rows: 4, 8, 16 or 32.</param>
        public static void Fill(ref int source, ref byte destination, int width, int widthLog2, int height)
        {
            nuint stride = (nuint)(width + Av1Constants.TransformPadHorizontal);
            nuint rowLength = (nuint)width;
            nuint count = (nuint)(width * height);

            // A 4x4 transform holds sixteen coefficients, fewer than one 256-bit step, so it takes the 128-bit path.
            if (Vector256.IsHardwareAccelerated && count >= 32)
            {
                // Thirty-two coefficients are one row of 32, two rows of 16, four rows of 8 or eight rows of 4. The
                // lower sixteen levels cover the first half of those rows and the upper sixteen the second half.
                nuint upperRowOffset = (nuint)(16 >> widthLog2) * stride;
                for (nuint i = 0; i < count; i += 32)
                {
                    Vector256<byte> levels = TOperator.Pack32(ref Unsafe.Add(ref source, i));
                    ref byte rowDestination = ref Unsafe.Add(ref destination, (i >> widthLog2) * stride);
                    if (rowLength == 32)
                    {
                        levels.StoreUnsafe(ref rowDestination);
                        Unsafe.WriteUnaligned(ref Unsafe.Add(ref rowDestination, (nuint)32), 0u);
                    }
                    else
                    {
                        StoreRows(levels.GetLower(), ref rowDestination, rowLength, stride);
                        StoreRows(levels.GetUpper(), ref Unsafe.Add(ref rowDestination, upperRowOffset), rowLength, stride);
                    }
                }

                return;
            }

            if (Vector128.IsHardwareAccelerated)
            {
                // Sixteen coefficients are half a row of 32, one row of 16, two rows of 8 or four rows of 4.
                for (nuint i = 0; i < count; i += 16)
                {
                    Vector128<byte> levels = TOperator.Pack16(ref Unsafe.Add(ref source, i));
                    nuint column = i & (rowLength - 1);
                    ref byte rowDestination = ref Unsafe.Add(ref destination, ((i >> widthLog2) * stride) + column);
                    if (rowLength == 32)
                    {
                        // The padding follows the second half of the row.
                        levels.StoreUnsafe(ref rowDestination);
                        if (column != 0)
                        {
                            Unsafe.WriteUnaligned(ref Unsafe.Add(ref rowDestination, (nuint)16), 0u);
                        }
                    }
                    else
                    {
                        StoreRows(levels, ref rowDestination, rowLength, stride);
                    }
                }

                return;
            }

            for (nuint row = 0; row < (nuint)height; row++)
            {
                ref byte rowDestination = ref Unsafe.Add(ref destination, row * stride);
                for (nuint column = 0; column < rowLength; column++)
                {
                    Unsafe.Add(ref rowDestination, column) = TOperator.Saturate(Unsafe.Add(ref source, (row * rowLength) + column));
                }

                Unsafe.WriteUnaligned(ref Unsafe.Add(ref rowDestination, rowLength), 0u);
            }
        }

        /// <summary>
        /// Stores sixteen levels as the one, two or four rows they cover, each row followed by its four zero padding bytes.
        /// </summary>
        /// <param name="levels">Sixteen levels in raster order.</param>
        /// <param name="destination">The first level of the first row they cover.</param>
        /// <param name="rowLength">The number of coefficients in a row: 4, 8 or 16.</param>
        /// <param name="stride">The number of bytes between rows of the plane.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void StoreRows(Vector128<byte> levels, ref byte destination, nuint rowLength, nuint stride)
        {
            switch (rowLength)
            {
                case 4:
                    // Four rows of four levels, with a stride of eight. Zero-extending each four-byte row to eight bytes
                    // appends its padding, so the four padded rows are two contiguous vectors.
                    Vector128.WidenLower(levels.AsUInt32()).AsByte().StoreUnsafe(ref destination);
                    Vector128.WidenUpper(levels.AsUInt32()).AsByte().StoreUnsafe(ref destination, (nuint)16);
                    break;
                case 8:
                    // Two rows of eight levels, each followed by four zero bytes.
                    Unsafe.WriteUnaligned(ref destination, levels.AsUInt64().ToScalar());
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, (nuint)8), 0u);
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, stride), levels.AsUInt64().GetElement(1));
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, stride + 8), 0u);
                    break;
                default:
                    levels.StoreUnsafe(ref destination);
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, (nuint)16), 0u);
                    break;
            }
        }
    }
}
