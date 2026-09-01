// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1TranslationalInterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Defines equal-weight compound prediction arithmetic.
/// </content>
internal static partial class Av1CompoundAveragePredictor
{
    /// <summary>
    /// Defines equal-weight compound averaging for scalar and SIMD lane groups.
    /// </summary>
    private interface IAv1CompoundAverageOperator
    {
        /// <summary>
        /// Averages two 8-bit samples.
        /// </summary>
        /// <param name="first">The first sample.</param>
        /// <param name="second">The second sample.</param>
        /// <returns>The rounded average.</returns>
        public static abstract byte Blend(byte first, byte second);

        /// <summary>
        /// Averages two high-bit-depth samples.
        /// </summary>
        /// <param name="first">The first sample.</param>
        /// <param name="second">The second sample.</param>
        /// <returns>The rounded average.</returns>
        public static abstract ushort Blend(ushort first, ushort second);

        /// <summary>
        /// Averages two 128-bit vectors of 8-bit samples.
        /// </summary>
        /// <param name="first">The first samples.</param>
        /// <param name="second">The second samples.</param>
        /// <returns>The rounded averages.</returns>
        public static abstract Vector128<byte> Blend(Vector128<byte> first, Vector128<byte> second);

        /// <summary>
        /// Averages two 256-bit vectors of 8-bit samples.
        /// </summary>
        /// <param name="first">The first samples.</param>
        /// <param name="second">The second samples.</param>
        /// <returns>The rounded averages.</returns>
        public static abstract Vector256<byte> Blend(Vector256<byte> first, Vector256<byte> second);

        /// <summary>
        /// Averages two 512-bit vectors of 8-bit samples.
        /// </summary>
        /// <param name="first">The first samples.</param>
        /// <param name="second">The second samples.</param>
        /// <returns>The rounded averages.</returns>
        public static abstract Vector512<byte> Blend(Vector512<byte> first, Vector512<byte> second);

        /// <summary>
        /// Averages two 128-bit vectors of high-bit-depth samples.
        /// </summary>
        /// <param name="first">The first samples.</param>
        /// <param name="second">The second samples.</param>
        /// <returns>The rounded averages.</returns>
        public static abstract Vector128<ushort> Blend(Vector128<ushort> first, Vector128<ushort> second);

        /// <summary>
        /// Averages two 256-bit vectors of high-bit-depth samples.
        /// </summary>
        /// <param name="first">The first samples.</param>
        /// <param name="second">The second samples.</param>
        /// <returns>The rounded averages.</returns>
        public static abstract Vector256<ushort> Blend(Vector256<ushort> first, Vector256<ushort> second);

        /// <summary>
        /// Averages two 512-bit vectors of high-bit-depth samples.
        /// </summary>
        /// <param name="first">The first samples.</param>
        /// <param name="second">The second samples.</param>
        /// <returns>The rounded averages.</returns>
        public static abstract Vector512<ushort> Blend(Vector512<ushort> first, Vector512<ushort> second);
    }

    /// <summary>
    /// Implements equal-weight rounded averaging for scalar and SIMD lane groups.
    /// </summary>
    private readonly struct CompoundAverageOperator : IAv1CompoundAverageOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Blend(byte first, byte second) => (byte)((first + second + 1) >> 1);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort Blend(ushort first, ushort second) => (ushort)((first + second + 1) >> 1);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Blend(Vector128<byte> first, Vector128<byte> second)
            => (first | second) - ((first ^ second) >> 1);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Blend(Vector256<byte> first, Vector256<byte> second)
            => (first | second) - ((first ^ second) >> 1);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> Blend(Vector512<byte> first, Vector512<byte> second)
        {
            // This identity is exactly (a + b + 1) >> 1 but cannot overflow unsigned lanes at any SIMD width.
            return (first | second) - ((first ^ second) >> 1);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ushort> Blend(Vector128<ushort> first, Vector128<ushort> second)
            => (first | second) - ((first ^ second) >> 1);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ushort> Blend(Vector256<ushort> first, Vector256<ushort> second)
            => (first | second) - ((first ^ second) >> 1);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<ushort> Blend(Vector512<ushort> first, Vector512<ushort> second)
            => (first | second) - ((first ^ second) >> 1);
    }
}
