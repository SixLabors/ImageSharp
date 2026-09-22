// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <content>
/// Defines the arithmetic contract consumed by the shared level-plane traversal.
/// </content>
internal sealed partial class Av1LevelBuffer
{
    /// <summary>
    /// Reduces a coefficient to the saturated magnitude that entropy contexts read.
    /// </summary>
    /// <remarks>
    /// Every overload describes the same lane-wise reduction. A context never distinguishes
    /// magnitudes above 127, so clamping there lets the plane hold one byte per coefficient instead
    /// of four.
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
        /// Reduces four coefficients.
        /// </summary>
        /// <param name="values">The signed coefficients.</param>
        /// <returns>The saturated magnitudes, each still in a thirty-two bit lane.</returns>
        public static abstract Vector128<int> Saturate(Vector128<int> values);

        /// <summary>
        /// Reduces eight coefficients.
        /// </summary>
        /// <param name="values">The signed coefficients.</param>
        /// <returns>The saturated magnitudes, each still in a thirty-two bit lane.</returns>
        public static abstract Vector256<int> Saturate(Vector256<int> values);

        /// <summary>
        /// Reduces sixteen coefficients.
        /// </summary>
        /// <param name="values">The signed coefficients.</param>
        /// <returns>The saturated magnitudes, each still in a thirty-two bit lane.</returns>
        public static abstract Vector512<int> Saturate(Vector512<int> values);
    }

    /// <summary>
    /// Takes the magnitude of a coefficient and clamps it to the largest signed byte.
    /// </summary>
    /// <remarks>
    /// Reference: av1_txb_init_levels_c().
    /// </remarks>
    private readonly struct LevelOperator : IAv1LevelOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Saturate(int value) => (byte)Math.Min(Math.Abs(value), sbyte.MaxValue);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> Saturate(Vector128<int> values)
            => Vector128.Min(Vector128.Abs(values), Vector128.Create((int)sbyte.MaxValue));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> Saturate(Vector256<int> values)
            => Vector256.Min(Vector256.Abs(values), Vector256.Create((int)sbyte.MaxValue));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> Saturate(Vector512<int> values)
            => Vector512.Min(Vector512.Abs(values), Vector512.Create((int)sbyte.MaxValue));
    }

    /// <summary>
    /// Fills the level plane with the arithmetic of a closed reduction operator.
    /// </summary>
    /// <typeparam name="TOperator">The lane-wise reduction.</typeparam>
    /// <remarks>
    /// One lane is one coefficient in row order, so each stage reduces one vector of coefficients
    /// and stores a quarter as many bytes. The magnitudes are below 128 when they are narrowed, so
    /// narrowing against a zero vector and keeping the lowest lanes is exact rather than a
    /// truncation of larger values.
    /// </remarks>
    private static class Levels<TOperator>
        where TOperator : struct, IAv1LevelOperator
    {
        /// <summary>
        /// Fills one row of the level plane.
        /// </summary>
        /// <param name="source">The first coefficient of the row.</param>
        /// <param name="destination">The first level of the row.</param>
        /// <param name="width">The number of coefficients in the row.</param>
        public static void FillRow(ref int source, ref byte destination, int width)
        {
            int x = 0;

            // Descending widths share one column offset. A coded transform is 4, 8, 16 or 32
            // coefficients wide, so every width reaches at least the narrowest vector stage.
            if (Vector512.IsHardwareAccelerated)
            {
                for (; x <= width - Vector512<int>.Count; x += Vector512<int>.Count)
                {
                    Vector512<int> values = TOperator.Saturate(Vector512.LoadUnsafe(ref source, (nuint)x));
                    Vector512<short> narrowed = Vector512.Narrow(values, Vector512<int>.Zero);
                    Vector512.Narrow(narrowed, Vector512<short>.Zero).GetLower().GetLower().AsByte()
                        .StoreUnsafe(ref destination, (nuint)x);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x <= width - Vector256<int>.Count; x += Vector256<int>.Count)
                {
                    Vector256<int> values = TOperator.Saturate(Vector256.LoadUnsafe(ref source, (nuint)x));
                    Vector256<short> narrowed = Vector256.Narrow(values, Vector256<int>.Zero);
                    Vector256.Narrow(narrowed, Vector256<short>.Zero).GetLower().GetLower().AsByte()
                        .StoreUnsafe(ref destination, (nuint)x);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x <= width - Vector128<int>.Count; x += Vector128<int>.Count)
                {
                    Vector128<int> values = TOperator.Saturate(Vector128.LoadUnsafe(ref source, (nuint)x));
                    Vector128<short> narrowed = Vector128.Narrow(values, Vector128<int>.Zero);
                    Unsafe.WriteUnaligned(
                        ref Unsafe.Add(ref destination, x),
                        Vector128.Narrow(narrowed, Vector128<short>.Zero).AsUInt32().ToScalar());
                }
            }

            for (; x < width; x++)
            {
                Unsafe.Add(ref destination, x) = TOperator.Saturate(Unsafe.Add(ref source, x));
            }
        }
    }
}
