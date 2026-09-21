// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <content>
/// Defines the arithmetic contract consumed by the shared halving traversal.
/// </content>
internal static partial class Av1PlaneDownsampler
{
    /// <summary>
    /// Applies one eight-tap halving kernel to eight adjacent samples.
    /// </summary>
    /// <remarks>
    /// Every overload describes the same lane-wise kernel. The samples arrive in kernel order, so an
    /// implementation pairs <c>s0</c> with <c>s7</c>, <c>s1</c> with <c>s6</c>, and so on to exploit
    /// the symmetry of the kernel. The traversal presents the samples as unsigned sixteen-bit lanes
    /// because an eight-bit lane cannot hold the weighted sum, and it narrows the result to bytes.
    /// </remarks>
    internal interface IAv1HalfFilterOperator
    {
        /// <summary>
        /// Filters one output sample.
        /// </summary>
        /// <param name="s0">The sample at kernel position 0.</param>
        /// <param name="s1">The sample at kernel position 1.</param>
        /// <param name="s2">The sample at kernel position 2.</param>
        /// <param name="s3">The sample at kernel position 3.</param>
        /// <param name="s4">The sample at kernel position 4.</param>
        /// <param name="s5">The sample at kernel position 5.</param>
        /// <param name="s6">The sample at kernel position 6.</param>
        /// <param name="s7">The sample at kernel position 7.</param>
        /// <returns>The rounded and clipped output sample.</returns>
        public static abstract byte Filter(int s0, int s1, int s2, int s3, int s4, int s5, int s6, int s7);

        /// <summary>
        /// Filters eight output samples.
        /// </summary>
        /// <param name="s0">The samples at kernel position 0.</param>
        /// <param name="s1">The samples at kernel position 1.</param>
        /// <param name="s2">The samples at kernel position 2.</param>
        /// <param name="s3">The samples at kernel position 3.</param>
        /// <param name="s4">The samples at kernel position 4.</param>
        /// <param name="s5">The samples at kernel position 5.</param>
        /// <param name="s6">The samples at kernel position 6.</param>
        /// <param name="s7">The samples at kernel position 7.</param>
        /// <returns>The rounded and clipped output samples, each from zero through 255.</returns>
        public static abstract Vector128<ushort> Filter(
            Vector128<ushort> s0,
            Vector128<ushort> s1,
            Vector128<ushort> s2,
            Vector128<ushort> s3,
            Vector128<ushort> s4,
            Vector128<ushort> s5,
            Vector128<ushort> s6,
            Vector128<ushort> s7);

        /// <summary>
        /// Filters sixteen output samples.
        /// </summary>
        /// <param name="s0">The samples at kernel position 0.</param>
        /// <param name="s1">The samples at kernel position 1.</param>
        /// <param name="s2">The samples at kernel position 2.</param>
        /// <param name="s3">The samples at kernel position 3.</param>
        /// <param name="s4">The samples at kernel position 4.</param>
        /// <param name="s5">The samples at kernel position 5.</param>
        /// <param name="s6">The samples at kernel position 6.</param>
        /// <param name="s7">The samples at kernel position 7.</param>
        /// <returns>The rounded and clipped output samples, each from zero through 255.</returns>
        public static abstract Vector256<ushort> Filter(
            Vector256<ushort> s0,
            Vector256<ushort> s1,
            Vector256<ushort> s2,
            Vector256<ushort> s3,
            Vector256<ushort> s4,
            Vector256<ushort> s5,
            Vector256<ushort> s6,
            Vector256<ushort> s7);

        /// <summary>
        /// Filters thirty-two output samples.
        /// </summary>
        /// <param name="s0">The samples at kernel position 0.</param>
        /// <param name="s1">The samples at kernel position 1.</param>
        /// <param name="s2">The samples at kernel position 2.</param>
        /// <param name="s3">The samples at kernel position 3.</param>
        /// <param name="s4">The samples at kernel position 4.</param>
        /// <param name="s5">The samples at kernel position 5.</param>
        /// <param name="s6">The samples at kernel position 6.</param>
        /// <param name="s7">The samples at kernel position 7.</param>
        /// <returns>The rounded and clipped output samples, each from zero through 255.</returns>
        public static abstract Vector512<ushort> Filter(
            Vector512<ushort> s0,
            Vector512<ushort> s1,
            Vector512<ushort> s2,
            Vector512<ushort> s3,
            Vector512<ushort> s4,
            Vector512<ushort> s5,
            Vector512<ushort> s6,
            Vector512<ushort> s7);
    }

    /// <summary>
    /// Traverses one line of outputs with the arithmetic of a closed halving operator.
    /// </summary>
    /// <typeparam name="TOperator">The kernel arithmetic.</typeparam>
    /// <remarks>
    /// Both separable passes reduce to the same shape: eight tap streams, one output stream, and a
    /// count. The horizontal pass supplies eight offsets into the two streams that a source row
    /// separates into. The vertical pass supplies eight rows of the intermediate plane. Neither pass
    /// gathers a column, and neither tests a boundary, because both prepare clamped streams first.
    /// </remarks>
    private static class Filter<TOperator>
        where TOperator : struct, IAv1HalfFilterOperator
    {
        /// <summary>
        /// Filters one line of outputs from eight tap streams.
        /// </summary>
        /// <param name="s0">The first sample of the stream at kernel position 0.</param>
        /// <param name="s1">The first sample of the stream at kernel position 1.</param>
        /// <param name="s2">The first sample of the stream at kernel position 2.</param>
        /// <param name="s3">The first sample of the stream at kernel position 3.</param>
        /// <param name="s4">The first sample of the stream at kernel position 4.</param>
        /// <param name="s5">The first sample of the stream at kernel position 5.</param>
        /// <param name="s6">The first sample of the stream at kernel position 6.</param>
        /// <param name="s7">The first sample of the stream at kernel position 7.</param>
        /// <param name="destination">The first output sample.</param>
        /// <param name="count">The number of output samples.</param>
        /// <remarks>
        /// Every stream is positioned so that lane <c>i</c> of each stream belongs to output
        /// <c>i</c>. A stage therefore loads one contiguous vector from each stream at the shared
        /// offset. That keeps eight sequential reads and one sequential write per stage, and it
        /// leaves no shuffle in the inner loop.
        /// </remarks>
        public static void Apply(
            ref byte s0,
            ref byte s1,
            ref byte s2,
            ref byte s3,
            ref byte s4,
            ref byte s5,
            ref byte s6,
            ref byte s7,
            ref byte destination,
            int count)
        {
            int i = 0;

            // Descending widths share one offset. A machine with AVX-512 therefore finishes a
            // sixteen-sample or eight-sample remainder in vectors instead of in the scalar loop.
            if (Vector512.IsHardwareAccelerated)
            {
                int vectorEnd = count - Vector512<ushort>.Count;
                for (; i <= vectorEnd; i += Vector512<ushort>.Count)
                {
                    // Thirty-two bytes of each stream widen into thirty-two unsigned sixteen-bit
                    // lanes. Pairing the load with a zero vector and widening the lower half is the
                    // established byte-to-lane form in this repository, and it needs no permute.
                    Vector512<ushort> v0 = WidenLower512(ref s0, i);
                    Vector512<ushort> v1 = WidenLower512(ref s1, i);
                    Vector512<ushort> v2 = WidenLower512(ref s2, i);
                    Vector512<ushort> v3 = WidenLower512(ref s3, i);
                    Vector512<ushort> v4 = WidenLower512(ref s4, i);
                    Vector512<ushort> v5 = WidenLower512(ref s5, i);
                    Vector512<ushort> v6 = WidenLower512(ref s6, i);
                    Vector512<ushort> v7 = WidenLower512(ref s7, i);

                    // The operator clips to zero through 255. Narrowing against a zero vector and
                    // keeping the lower half is therefore exact, not a truncation of larger values.
                    Vector512<ushort> filtered = TOperator.Filter(v0, v1, v2, v3, v4, v5, v6, v7);
                    Vector512.Narrow(filtered, Vector512<ushort>.Zero).GetLower().StoreUnsafe(ref destination, (nuint)i);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int vectorEnd = count - Vector256<ushort>.Count;
                for (; i <= vectorEnd; i += Vector256<ushort>.Count)
                {
                    Vector256<ushort> v0 = WidenLower256(ref s0, i);
                    Vector256<ushort> v1 = WidenLower256(ref s1, i);
                    Vector256<ushort> v2 = WidenLower256(ref s2, i);
                    Vector256<ushort> v3 = WidenLower256(ref s3, i);
                    Vector256<ushort> v4 = WidenLower256(ref s4, i);
                    Vector256<ushort> v5 = WidenLower256(ref s5, i);
                    Vector256<ushort> v6 = WidenLower256(ref s6, i);
                    Vector256<ushort> v7 = WidenLower256(ref s7, i);

                    Vector256<ushort> filtered = TOperator.Filter(v0, v1, v2, v3, v4, v5, v6, v7);
                    Vector256.Narrow(filtered, Vector256<ushort>.Zero).GetLower().StoreUnsafe(ref destination, (nuint)i);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int vectorEnd = count - Vector128<ushort>.Count;
                for (; i <= vectorEnd; i += Vector128<ushort>.Count)
                {
                    Vector128<ushort> v0 = WidenLower128(ref s0, i);
                    Vector128<ushort> v1 = WidenLower128(ref s1, i);
                    Vector128<ushort> v2 = WidenLower128(ref s2, i);
                    Vector128<ushort> v3 = WidenLower128(ref s3, i);
                    Vector128<ushort> v4 = WidenLower128(ref s4, i);
                    Vector128<ushort> v5 = WidenLower128(ref s5, i);
                    Vector128<ushort> v6 = WidenLower128(ref s6, i);
                    Vector128<ushort> v7 = WidenLower128(ref s7, i);

                    Vector128<ushort> filtered = TOperator.Filter(v0, v1, v2, v3, v4, v5, v6, v7);
                    Vector128.Narrow(filtered, Vector128<ushort>.Zero).GetLower().StoreUnsafe(ref destination, (nuint)i);
                }
            }

            // Fewer than eight outputs remain. A short plane edge and an unaccelerated runtime both
            // arrive here, and both must produce the samples that the vector stages produce.
            for (; i < count; i++)
            {
                Unsafe.Add(ref destination, i) = TOperator.Filter(
                    Unsafe.Add(ref s0, i),
                    Unsafe.Add(ref s1, i),
                    Unsafe.Add(ref s2, i),
                    Unsafe.Add(ref s3, i),
                    Unsafe.Add(ref s4, i),
                    Unsafe.Add(ref s5, i),
                    Unsafe.Add(ref s6, i),
                    Unsafe.Add(ref s7, i));
            }
        }

        /// <summary>
        /// Loads eight bytes of one tap stream as unsigned sixteen-bit lanes.
        /// </summary>
        /// <param name="stream">The first sample of the tap stream.</param>
        /// <param name="offset">The output offset shared by every stream.</param>
        /// <returns>The widened samples.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<ushort> WidenLower128(ref byte stream, int offset)
            => Vector128.WidenLower(Vector128.Create(Vector64.LoadUnsafe(ref stream, (nuint)offset), Vector64<byte>.Zero));

        /// <summary>
        /// Loads sixteen bytes of one tap stream as unsigned sixteen-bit lanes.
        /// </summary>
        /// <param name="stream">The first sample of the tap stream.</param>
        /// <param name="offset">The output offset shared by every stream.</param>
        /// <returns>The widened samples.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<ushort> WidenLower256(ref byte stream, int offset)
            => Vector256.WidenLower(Vector256.Create(Vector128.LoadUnsafe(ref stream, (nuint)offset), Vector128<byte>.Zero));

        /// <summary>
        /// Loads thirty-two bytes of one tap stream as unsigned sixteen-bit lanes.
        /// </summary>
        /// <param name="stream">The first sample of the tap stream.</param>
        /// <param name="offset">The output offset shared by every stream.</param>
        /// <returns>The widened samples.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<ushort> WidenLower512(ref byte stream, int offset)
            => Vector512.WidenLower(Vector512.Create(Vector256.LoadUnsafe(ref stream, (nuint)offset), Vector256<byte>.Zero));
    }
}
