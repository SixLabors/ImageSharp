// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <content>
/// Implements the two directions of the corner threshold.
/// </content>
internal static partial class Av1CornerDetector
{
    /// <summary>
    /// Passes a circle sample that is brighter than the centre by the barrier.
    /// </summary>
    /// <remarks>
    /// The threshold is <c>centre + barrier</c>. A byte lane cannot hold every such sum. Thus the vector overloads clamp the centre to
    /// <c>255 - barrier</c> before they add. A clamped lane yields a threshold of 255, and no sample is above 255. Thus the comparison is false
    /// there, as it is for the unclamped sum.
    /// </remarks>
    private readonly struct BrighterOperator : IAv1CornerThresholdOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Threshold(int centre, int barrier) => centre + barrier;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Exceeds(int sample, int threshold) => sample > threshold;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Threshold(Vector128<byte> centre, Vector128<byte> barrier)
            => Vector128.Min(centre, Vector128<byte>.AllBitsSet - barrier) + barrier;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Exceeds(Vector128<byte> sample, Vector128<byte> threshold)
            => Vector128.GreaterThan(sample, threshold);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Threshold(Vector256<byte> centre, Vector256<byte> barrier)
            => Vector256.Min(centre, Vector256<byte>.AllBitsSet - barrier) + barrier;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Exceeds(Vector256<byte> sample, Vector256<byte> threshold)
            => Vector256.GreaterThan(sample, threshold);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> Threshold(Vector512<byte> centre, Vector512<byte> barrier)
            => Vector512.Min(centre, Vector512<byte>.AllBitsSet - barrier) + barrier;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> Exceeds(Vector512<byte> sample, Vector512<byte> threshold)
            => Vector512.GreaterThan(sample, threshold);
    }

    /// <summary>
    /// Passes a circle sample that is darker than the centre by the barrier.
    /// </summary>
    /// <remarks>
    /// The threshold is <c>centre - barrier</c>. A byte lane cannot hold a negative difference. Thus the vector overloads raise the centre to the
    /// barrier before they subtract. A raised lane yields a threshold of zero, and no sample is below zero. Thus the comparison is false there, as
    /// it is for the unraised difference.
    /// </remarks>
    private readonly struct DarkerOperator : IAv1CornerThresholdOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Threshold(int centre, int barrier) => centre - barrier;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Exceeds(int sample, int threshold) => sample < threshold;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Threshold(Vector128<byte> centre, Vector128<byte> barrier)
            => Vector128.Max(centre, barrier) - barrier;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Exceeds(Vector128<byte> sample, Vector128<byte> threshold)
            => Vector128.LessThan(sample, threshold);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Threshold(Vector256<byte> centre, Vector256<byte> barrier)
            => Vector256.Max(centre, barrier) - barrier;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Exceeds(Vector256<byte> sample, Vector256<byte> threshold)
            => Vector256.LessThan(sample, threshold);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> Threshold(Vector512<byte> centre, Vector512<byte> barrier)
            => Vector512.Max(centre, barrier) - barrier;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> Exceeds(Vector512<byte> sample, Vector512<byte> threshold)
            => Vector512.LessThan(sample, threshold);
    }
}
