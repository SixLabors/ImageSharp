// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Defines sample-storage operators for reference-scaled prediction.
/// </content>
internal static partial class Av1InterPredictor
{
    /// <summary>
    /// Supplies sample loading, clipping, and storage for a scaled predictor pipeline.
    /// </summary>
    /// <typeparam name="T">The native sample storage type.</typeparam>
    private interface IScaledSampleOperator<T>
        where T : unmanaged
    {
        /// <summary>
        /// Loads one source sample as a signed accumulator value.
        /// </summary>
        /// <param name="source">The first source sample.</param>
        /// <param name="index">The sample offset.</param>
        /// <returns>The widened sample value.</returns>
        public static abstract int Load(ref T source, int index);

        /// <summary>
        /// Clips and stores eight completed vector lanes.
        /// </summary>
        /// <param name="destination">The first destination sample.</param>
        /// <param name="index">The output offset.</param>
        /// <param name="result0">The first four completed lanes.</param>
        /// <param name="result1">The second four completed lanes.</param>
        /// <param name="bitDepth">The decoded sample precision.</param>
        public static abstract void StoreVector(
            ref T destination,
            int index,
            Vector128<int> result0,
            Vector128<int> result1,
            int bitDepth);

        /// <summary>
        /// Clips and stores one completed scalar value.
        /// </summary>
        /// <param name="destination">The first destination sample.</param>
        /// <param name="index">The output offset.</param>
        /// <param name="value">The completed sample value.</param>
        /// <param name="bitDepth">The decoded sample precision.</param>
        public static abstract void StoreScalar(ref T destination, int index, int value, int bitDepth);
    }

    /// <summary>
    /// Implements scaled prediction storage for 8-bit samples.
    /// </summary>
    private readonly struct ScaledByteOperator : IScaledSampleOperator<byte>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Load(ref byte source, int index) => Unsafe.Add(ref source, index);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreVector(
            ref byte destination,
            int index,
            Vector128<int> result0,
            Vector128<int> result1,
            int bitDepth)
            => PackBytes(result0, result1, Vector128<int>.Zero, Vector128<int>.Zero)
                .GetLower()
                .StoreUnsafe(ref destination, (nuint)index);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreScalar(ref byte destination, int index, int value, int bitDepth)
            => Unsafe.Add(ref destination, index) = (byte)Math.Clamp(value, byte.MinValue, byte.MaxValue);
    }

    /// <summary>
    /// Implements scaled prediction storage for 8-, 10-, and 12-bit samples.
    /// </summary>
    private readonly struct ScaledUInt16Operator : IScaledSampleOperator<ushort>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Load(ref ushort source, int index) => Unsafe.Add(ref source, index);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreVector(
            ref ushort destination,
            int index,
            Vector128<int> result0,
            Vector128<int> result1,
            int bitDepth)
            => PackHighBitDepth(result0, result1, (1 << bitDepth) - 1).StoreUnsafe(ref destination, (nuint)index);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreScalar(ref ushort destination, int index, int value, int bitDepth)
            => Unsafe.Add(ref destination, index) = (ushort)Math.Clamp(value, 0, (1 << bitDepth) - 1);
    }
}
