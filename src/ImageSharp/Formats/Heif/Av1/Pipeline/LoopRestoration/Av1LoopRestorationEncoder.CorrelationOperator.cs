// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

/// <content>
/// Defines the lane arithmetic of the Wiener correlation traversal.
/// </content>
internal static partial class Av1LoopRestorationEncoder
{
    /// <summary>
    /// Accumulates the products of centered samples in 64-bit lane totals.
    /// </summary>
    /// <remarks>
    /// One 16-bit lane holds one sample. A twelve-bit sample less the average stays inside 16 bits. Adjacent products add in pairs into
    /// 32 bits, which hold the sum of two twelve-bit products. The pair sums widen to 64 bits before they join the total. The split of a
    /// total across its lanes has no defined order. Only the sum of all lanes is defined.
    /// </remarks>
    private interface ICorrelationOperator
    {
        /// <summary>
        /// Adds the centered products of thirty-two sample pairs.
        /// </summary>
        /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
        /// <param name="first">The first sample of the first row.</param>
        /// <param name="second">The first sample of the second row.</param>
        /// <param name="average">The common reconstructed-sample average.</param>
        /// <param name="total">The lane totals.</param>
        /// <returns>The updated lane totals.</returns>
        public static abstract Vector512<long> AccumulateProducts<TSample>(ref TSample first, ref TSample second, int average, Vector512<long> total)
            where TSample : unmanaged;

        /// <summary>
        /// Adds the centered products of sixteen sample pairs.
        /// </summary>
        /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
        /// <param name="first">The first sample of the first row.</param>
        /// <param name="second">The first sample of the second row.</param>
        /// <param name="average">The common reconstructed-sample average.</param>
        /// <param name="total">The lane totals.</param>
        /// <returns>The updated lane totals.</returns>
        public static abstract Vector256<long> AccumulateProducts<TSample>(ref TSample first, ref TSample second, int average, Vector256<long> total)
            where TSample : unmanaged;

        /// <summary>
        /// Adds the centered products of eight sample pairs.
        /// </summary>
        /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
        /// <param name="first">The first sample of the first row.</param>
        /// <param name="second">The first sample of the second row.</param>
        /// <param name="average">The common reconstructed-sample average.</param>
        /// <param name="total">The lane totals.</param>
        /// <returns>The updated lane totals.</returns>
        public static abstract Vector128<long> AccumulateProducts<TSample>(ref TSample first, ref TSample second, int average, Vector128<long> total)
            where TSample : unmanaged;

        /// <summary>
        /// Adds the centered product of one sample pair.
        /// </summary>
        /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
        /// <param name="first">The first sample.</param>
        /// <param name="second">The second sample.</param>
        /// <param name="average">The common reconstructed-sample average.</param>
        /// <param name="total">The total.</param>
        /// <returns>The updated total.</returns>
        public static abstract long AccumulateProducts<TSample>(ref TSample first, ref TSample second, int average, long total)
            where TSample : unmanaged;
    }

    /// <summary>
    /// Measures centered products lane by lane.
    /// </summary>
    private readonly struct CorrelationOperator : ICorrelationOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<long> AccumulateProducts<TSample>(ref TSample first, ref TSample second, int average, Vector512<long> total)
            where TSample : unmanaged
        {
            Vector512<short> center = Vector512.Create((short)average);
            Vector512<short> a = Av1RestorationSampleOperations.LoadToUInt16(ref first, Vector512<ushort>.Zero).AsInt16() - center;
            Vector512<short> b = Av1RestorationSampleOperations.LoadToUInt16(ref second, Vector512<ushort>.Zero).AsInt16() - center;

            // The 32 signed 16-bit products add in adjacent pairs into 16 int lanes. Widen splits those lanes into two halves of
            // eight 64-bit lanes, and both halves add into the total. The narrower widths below use the same layout.
            (Vector512<long> lower, Vector512<long> upper) = Vector512.Widen(Vector512_.MultiplyAddAdjacent(a, b));
            return total + lower + upper;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<long> AccumulateProducts<TSample>(ref TSample first, ref TSample second, int average, Vector256<long> total)
            where TSample : unmanaged
        {
            Vector256<short> center = Vector256.Create((short)average);
            Vector256<short> a = Av1RestorationSampleOperations.LoadToUInt16(ref first, Vector256<ushort>.Zero).AsInt16() - center;
            Vector256<short> b = Av1RestorationSampleOperations.LoadToUInt16(ref second, Vector256<ushort>.Zero).AsInt16() - center;
            (Vector256<long> lower, Vector256<long> upper) = Vector256.Widen(Vector256_.MultiplyAddAdjacent(a, b));
            return total + lower + upper;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<long> AccumulateProducts<TSample>(ref TSample first, ref TSample second, int average, Vector128<long> total)
            where TSample : unmanaged
        {
            Vector128<short> center = Vector128.Create((short)average);
            Vector128<short> a = Av1RestorationSampleOperations.LoadToUInt16(ref first, Vector128<ushort>.Zero).AsInt16() - center;
            Vector128<short> b = Av1RestorationSampleOperations.LoadToUInt16(ref second, Vector128<ushort>.Zero).AsInt16() - center;
            (Vector128<long> lower, Vector128<long> upper) = Vector128.Widen(Vector128_.MultiplyAddAdjacent(a, b));
            return total + lower + upper;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long AccumulateProducts<TSample>(ref TSample first, ref TSample second, int average, long total)
            where TSample : unmanaged
            => total + ((long)(Av1RestorationSampleOperations.Load(first) - average) * (Av1RestorationSampleOperations.Load(second) - average));
    }
}
