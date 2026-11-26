// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

internal sealed partial class JxlVarDctDecoder
{
    /// <summary>
    /// Defines one AC metadata range rule independently of integer-to-byte row traversal.
    /// </summary>
    private interface IAcMetadataOperator
    {
        /// <summary>
        /// Gets the bits that are clear in each valid value, or zero if the rule saturates each value into its range.
        /// </summary>
        /// <remarks>
        /// The row converter combines all values of a row with bitwise OR and checks these bits once after the row.
        /// </remarks>
        static abstract int InvalidBits { get; }

        /// <summary>
        /// Establishes the permitted range for one reconstructed integer.
        /// </summary>
        /// <param name="value">The reconstructed metadata value.</param>
        /// <returns>The value to narrow to eight bits.</returns>
        static abstract int Invoke(int value);

        /// <summary>
        /// Establishes the permitted range for four independent metadata lanes.
        /// </summary>
        /// <param name="value">Four reconstructed metadata values.</param>
        /// <returns>The values to narrow, in the same lane order.</returns>
        static abstract Vector128<int> Invoke(Vector128<int> value);

        /// <inheritdoc cref="Invoke(Vector128{int})" />
        static abstract Vector256<int> Invoke(Vector256<int> value);

        /// <inheritdoc cref="Invoke(Vector128{int})" />
        static abstract Vector512<int> Invoke(Vector512<int> value);
    }

    /// <summary>
    /// Narrows four values to bytes. The method does not write the later destination columns.
    /// </summary>
    /// <param name="value">
    /// Four values. A byte is exact for a value in [-128,127]. A sharpness value outside [0,7] gives an unspecified byte, and the row converter rejects that
    /// row after its loop.
    /// </param>
    /// <param name="destination">The first of four output bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreMetadata(Vector128<int> value, ref byte destination)
    {
        // This method uses the existing native saturating pack helpers. The correlation operator clamped its range before this call. The packs keep negative
        // correlation values as their signed eight-bit representation.
        Vector128<short> words = Vector128_.PackSignedSaturate(value, Vector128<int>.Zero);
        Vector128<sbyte> bytes = Vector128_.PackSignedSaturate(words, Vector128<short>.Zero);
        Unsafe.WriteUnaligned(ref destination, bytes.AsUInt32().ToScalar());
    }

    /// <summary>
    /// Narrows eight values to bytes in their original column order.
    /// </summary>
    /// <param name="value">Eight values. A byte is exact for a value in [-128,127]. The row converter rejects other sharpness values.</param>
    /// <param name="destination">The first of eight output bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreMetadata(Vector256<int> value, ref byte destination)
    {
        // Native AVX2 pack instructions group lanes by 128-bit halves. Vector.Narrow preserves logical order across
        // those halves. Only the populated lower half is retained after each narrowing step; no padding reaches output.
        Vector128<short> words = Vector256.Narrow(value, Vector256<int>.Zero).GetLower();
        Vector128_.PackSignedSaturate(words, Vector128<short>.Zero).GetLower().AsByte().StoreUnsafe(ref destination);
    }

    /// <summary>
    /// Narrows sixteen values to bytes in their original column order.
    /// </summary>
    /// <param name="value">Sixteen values. A byte is exact for a value in [-128,127]. The row converter rejects other sharpness values.</param>
    /// <param name="destination">The first of sixteen output bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreMetadata(Vector512<int> value, ref byte destination)
    {
        Vector256<short> words = Vector512.Narrow(value, Vector512<int>.Zero).GetLower();
        Vector256.Narrow(words, Vector256<short>.Zero).GetLower().AsByte().StoreUnsafe(ref destination);
    }

    /// <summary>
    /// Saturates correlation entries to the format's signed eight-bit map storage.
    /// </summary>
    private readonly struct CorrelationOperator : IAcMetadataOperator
    {
        /// <inheritdoc/>
        public static int InvalidBits => 0;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Invoke(int value) => Math.Clamp(value, sbyte.MinValue, sbyte.MaxValue);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> Invoke(Vector128<int> value)
            => Vector128.Clamp(value, Vector128.Create((int)sbyte.MinValue), Vector128.Create((int)sbyte.MaxValue));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> Invoke(Vector256<int> value)
            => Vector256.Clamp(value, Vector256.Create((int)sbyte.MinValue), Vector256.Create((int)sbyte.MaxValue));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> Invoke(Vector512<int> value)
            => Vector512.Clamp(value, Vector512.Create((int)sbyte.MinValue), Vector512.Create((int)sbyte.MaxValue));
    }

    /// <summary>
    /// Keeps the sharpness indices unchanged. The row converter rejects out-of-range indices, because saturation changes their filter meaning.
    /// </summary>
    private readonly struct SharpnessOperator : IAcMetadataOperator
    {
        /// <inheritdoc/>
        /// <remarks>
        /// SharpnessCount is a power of two. Thus a value is in [0,7] when all of its other bits are clear, and only then. This test also rejects negative
        /// signed values.
        /// </remarks>
        public static int InvalidBits => ~(SharpnessCount - 1);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Invoke(int value) => value;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> Invoke(Vector128<int> value) => value;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> Invoke(Vector256<int> value) => value;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> Invoke(Vector512<int> value) => value;
    }
}
