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
    /// Writes four established byte-range values without touching later destination columns.
    /// </summary>
    /// <param name="value">Four values in [-128,127], including valid sharpness indices.</param>
    /// <param name="destination">The first of four output bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreMetadata(Vector128<int> value, ref byte destination)
    {
        // Use the existing native saturating pack helpers. The operator has already established the narrower range;
        // the packs retain negative correlation values as their signed eight-bit representation.
        Vector128<short> words = Vector128_.PackSignedSaturate(value, Vector128<int>.Zero);
        Vector128<sbyte> bytes = Vector128_.PackSignedSaturate(words, Vector128<short>.Zero);
        Unsafe.WriteUnaligned(ref destination, bytes.AsUInt32().ToScalar());
    }

    /// <summary>
    /// Writes eight established byte-range values in their original column order.
    /// </summary>
    /// <param name="value">Eight values in [-128,127].</param>
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
    /// Writes sixteen established byte-range values in their original column order.
    /// </summary>
    /// <param name="value">Sixteen values in [-128,127].</param>
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
    /// Rejects out-of-range sharpness indices instead of changing their filter meaning by saturation.
    /// </summary>
    private readonly struct SharpnessOperator : IAcMetadataOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Invoke(int value)
        {
            if ((uint)value >= SharpnessCount)
            {
                throw new InvalidImageContentException("The JPEG XL EPF sharpness index is outside its permitted range.");
            }

            return value;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> Invoke(Vector128<int> value)
        {
            // The unsigned comparison also rejects negative signed samples. Every lane must be a valid filter index.
            if (!Vector128.LessThanAll(value.AsUInt32(), Vector128.Create((uint)SharpnessCount)))
            {
                throw new InvalidImageContentException("The JPEG XL EPF sharpness index is outside its permitted range.");
            }

            return value;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> Invoke(Vector256<int> value)
        {
            if (!Vector256.LessThanAll(value.AsUInt32(), Vector256.Create((uint)SharpnessCount)))
            {
                throw new InvalidImageContentException("The JPEG XL EPF sharpness index is outside its permitted range.");
            }

            return value;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> Invoke(Vector512<int> value)
        {
            if (!Vector512.LessThanAll(value.AsUInt32(), Vector512.Create((uint)SharpnessCount)))
            {
                throw new InvalidImageContentException("The JPEG XL EPF sharpness index is outside its permitted range.");
            }

            return value;
        }
    }
}
