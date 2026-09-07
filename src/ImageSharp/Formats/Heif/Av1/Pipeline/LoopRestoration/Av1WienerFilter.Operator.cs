// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

/// <content>
/// Defines the arithmetic contract consumed by the shared restoration-row traversal.
/// </content>
internal static partial class Av1WienerFilter
{
    /// <summary>
    /// Applies a symmetric seven-tap kernel with signed rounding and clipping.
    /// </summary>
    internal interface IAv1WienerOperator
    {
        /// <summary>
        /// Filters one sample with the implicit center weight included in the kernel.
        /// </summary>
        /// <param name="sample0">The samples at tap 0.</param>
        /// <param name="sample1">The samples at tap 1.</param>
        /// <param name="sample2">The samples at tap 2.</param>
        /// <param name="sample3">The samples at tap 3.</param>
        /// <param name="sample4">The samples at tap 4.</param>
        /// <param name="sample5">The samples at tap 5.</param>
        /// <param name="sample6">The samples at tap 6.</param>
        /// <param name="coefficient0">The symmetric tap-pair coefficient 0.</param>
        /// <param name="coefficient1">The symmetric tap-pair coefficient 1.</param>
        /// <param name="coefficient2">The symmetric tap-pair coefficient 2.</param>
        /// <param name="coefficient3">The complete center coefficient.</param>
        /// <param name="bias">The pass offset including the signed rounding term.</param>
        /// <param name="roundBits">The arithmetic right-shift count.</param>
        /// <param name="maximum">The largest permitted output sample.</param>
        /// <returns>The rounded and clipped output samples.</returns>
        public static abstract ushort Filter(
            int sample0,
            int sample1,
            int sample2,
            int sample3,
            int sample4,
            int sample5,
            int sample6,
            int coefficient0,
            int coefficient1,
            int coefficient2,
            int coefficient3,
            int bias,
            int roundBits,
            int maximum);

        /// <summary>
        /// Filters 8 adjacent samples without mixing neighboring output lanes.
        /// </summary>
        /// <param name="sample0">The samples at tap 0.</param>
        /// <param name="sample1">The samples at tap 1.</param>
        /// <param name="sample2">The samples at tap 2.</param>
        /// <param name="sample3">The samples at tap 3.</param>
        /// <param name="sample4">The samples at tap 4.</param>
        /// <param name="sample5">The samples at tap 5.</param>
        /// <param name="sample6">The samples at tap 6.</param>
        /// <param name="coefficient0">The symmetric tap-pair coefficient 0.</param>
        /// <param name="coefficient1">The symmetric tap-pair coefficient 1.</param>
        /// <param name="coefficient2">The symmetric tap-pair coefficient 2.</param>
        /// <param name="coefficient3">The complete center coefficient.</param>
        /// <param name="bias">The pass offset including the signed rounding term.</param>
        /// <param name="roundBits">The arithmetic right-shift count.</param>
        /// <param name="maximum">The largest permitted output sample.</param>
        /// <returns>The rounded and clipped output samples.</returns>
        public static abstract Vector128<ushort> Filter(
            Vector128<ushort> sample0,
            Vector128<ushort> sample1,
            Vector128<ushort> sample2,
            Vector128<ushort> sample3,
            Vector128<ushort> sample4,
            Vector128<ushort> sample5,
            Vector128<ushort> sample6,
            Vector128<int> coefficient0,
            Vector128<int> coefficient1,
            Vector128<int> coefficient2,
            Vector128<int> coefficient3,
            Vector128<int> bias,
            int roundBits,
            Vector128<int> maximum);

        /// <summary>
        /// Filters 16 adjacent samples without mixing neighboring output lanes.
        /// </summary>
        /// <param name="sample0">The samples at tap 0.</param>
        /// <param name="sample1">The samples at tap 1.</param>
        /// <param name="sample2">The samples at tap 2.</param>
        /// <param name="sample3">The samples at tap 3.</param>
        /// <param name="sample4">The samples at tap 4.</param>
        /// <param name="sample5">The samples at tap 5.</param>
        /// <param name="sample6">The samples at tap 6.</param>
        /// <param name="coefficient0">The symmetric tap-pair coefficient 0.</param>
        /// <param name="coefficient1">The symmetric tap-pair coefficient 1.</param>
        /// <param name="coefficient2">The symmetric tap-pair coefficient 2.</param>
        /// <param name="coefficient3">The complete center coefficient.</param>
        /// <param name="bias">The pass offset including the signed rounding term.</param>
        /// <param name="roundBits">The arithmetic right-shift count.</param>
        /// <param name="maximum">The largest permitted output sample.</param>
        /// <returns>The rounded and clipped output samples.</returns>
        public static abstract Vector256<ushort> Filter(
            Vector256<ushort> sample0,
            Vector256<ushort> sample1,
            Vector256<ushort> sample2,
            Vector256<ushort> sample3,
            Vector256<ushort> sample4,
            Vector256<ushort> sample5,
            Vector256<ushort> sample6,
            Vector256<int> coefficient0,
            Vector256<int> coefficient1,
            Vector256<int> coefficient2,
            Vector256<int> coefficient3,
            Vector256<int> bias,
            int roundBits,
            Vector256<int> maximum);

        /// <summary>
        /// Filters 32 adjacent samples without mixing neighboring output lanes.
        /// </summary>
        /// <param name="sample0">The samples at tap 0.</param>
        /// <param name="sample1">The samples at tap 1.</param>
        /// <param name="sample2">The samples at tap 2.</param>
        /// <param name="sample3">The samples at tap 3.</param>
        /// <param name="sample4">The samples at tap 4.</param>
        /// <param name="sample5">The samples at tap 5.</param>
        /// <param name="sample6">The samples at tap 6.</param>
        /// <param name="coefficient0">The symmetric tap-pair coefficient 0.</param>
        /// <param name="coefficient1">The symmetric tap-pair coefficient 1.</param>
        /// <param name="coefficient2">The symmetric tap-pair coefficient 2.</param>
        /// <param name="coefficient3">The complete center coefficient.</param>
        /// <param name="bias">The pass offset including the signed rounding term.</param>
        /// <param name="roundBits">The arithmetic right-shift count.</param>
        /// <param name="maximum">The largest permitted output sample.</param>
        /// <returns>The rounded and clipped output samples.</returns>
        public static abstract Vector512<ushort> Filter(
            Vector512<ushort> sample0,
            Vector512<ushort> sample1,
            Vector512<ushort> sample2,
            Vector512<ushort> sample3,
            Vector512<ushort> sample4,
            Vector512<ushort> sample5,
            Vector512<ushort> sample6,
            Vector512<int> coefficient0,
            Vector512<int> coefficient1,
            Vector512<int> coefficient2,
            Vector512<int> coefficient3,
            Vector512<int> bias,
            int roundBits,
            Vector512<int> maximum);
    }

    /// <summary>
    /// Traverses source rows with seven-tap context and writes each output sample exactly once.
    /// </summary>
    /// <typeparam name="TSource">The physical source sample type.</typeparam>
    /// <typeparam name="TDestination">The physical destination sample type.</typeparam>
    /// <typeparam name="TOperator">The symmetric filter arithmetic.</typeparam>
    /// <param name="source">The source beginning at the first tap of the first output sample.</param>
    /// <param name="sourceStride">The sample distance between source rows.</param>
    /// <param name="destination">The first destination sample.</param>
    /// <param name="destinationStride">The sample distance between destination rows.</param>
    /// <param name="width">The number of output samples per row.</param>
    /// <param name="height">The number of output rows.</param>
    /// <param name="tapStride">One for horizontal filtering, or the intermediate row stride for vertical filtering.</param>
    /// <param name="filter">The complete symmetric kernel, including its implicit center weight.</param>
    /// <param name="bias">The pass offset including half-unit rounding.</param>
    /// <param name="roundBits">The arithmetic right-shift count.</param>
    /// <param name="maximum">The output clipping ceiling.</param>
    private static void FilterRows<TSource, TDestination, TOperator>(
        ReadOnlySpan<TSource> source,
        int sourceStride,
        Span<TDestination> destination,
        int destinationStride,
        int width,
        int height,
        int tapStride,
        ReadOnlySpan<short> filter,
        int bias,
        int roundBits,
        int maximum)
        where TSource : unmanaged
        where TDestination : unmanaged
        where TOperator : struct, IAv1WienerOperator
    {
        ref TSource sourceBase = ref MemoryMarshal.GetReference(source);
        ref TDestination destinationBase = ref MemoryMarshal.GetReference(destination);
        int column = 0;

        // Each batch visits a vertical strip of independent output columns. Complete the wider strips
        // first, then narrower strips and a scalar tail. Coefficients are broadcast once per used width,
        // and the horizontal intermediate is complete before the vertical pass can consume it.
        if (Vector512.IsHardwareAccelerated && width - column >= Vector512<ushort>.Count)
        {
            Vector512<int> coefficient0 = Vector512.Create((int)filter[0]);
            Vector512<int> coefficient1 = Vector512.Create((int)filter[1]);
            Vector512<int> coefficient2 = Vector512.Create((int)filter[2]);
            Vector512<int> coefficient3 = Vector512.Create((int)filter[3]);
            Vector512<int> offset = Vector512.Create(bias);
            Vector512<int> ceiling = Vector512.Create(maximum);
            int lastStart = width - Vector512<ushort>.Count;
            for (; column <= lastStart; column += Vector512<ushort>.Count)
            {
                for (int row = 0; row < height; row++)
                {
                    ref TSource firstTap = ref Unsafe.Add(ref sourceBase, (row * sourceStride) + column);

                    // The complete output batch and all six following taps fit in caller-provided context.
                    // Loads remain unaligned so pooled-memory addresses do not impose an alignment contract.
                    Vector512<ushort> result = TOperator.Filter(
                        Av1RestorationSampleOperations.LoadToUInt16(ref Unsafe.Add(ref firstTap, 0 * tapStride), Vector512<ushort>.Zero),
                        Av1RestorationSampleOperations.LoadToUInt16(ref Unsafe.Add(ref firstTap, 1 * tapStride), Vector512<ushort>.Zero),
                        Av1RestorationSampleOperations.LoadToUInt16(ref Unsafe.Add(ref firstTap, 2 * tapStride), Vector512<ushort>.Zero),
                        Av1RestorationSampleOperations.LoadToUInt16(ref Unsafe.Add(ref firstTap, 3 * tapStride), Vector512<ushort>.Zero),
                        Av1RestorationSampleOperations.LoadToUInt16(ref Unsafe.Add(ref firstTap, 4 * tapStride), Vector512<ushort>.Zero),
                        Av1RestorationSampleOperations.LoadToUInt16(ref Unsafe.Add(ref firstTap, 5 * tapStride), Vector512<ushort>.Zero),
                        Av1RestorationSampleOperations.LoadToUInt16(ref Unsafe.Add(ref firstTap, 6 * tapStride), Vector512<ushort>.Zero),
                        coefficient0,
                        coefficient1,
                        coefficient2,
                        coefficient3,
                        offset,
                        roundBits,
                        ceiling);

                    Av1RestorationSampleOperations.Store(result, ref Unsafe.Add(ref destinationBase, (row * destinationStride) + column));
                }
            }
        }

        if (Vector256.IsHardwareAccelerated && width - column >= Vector256<ushort>.Count)
        {
            Vector256<int> coefficient0 = Vector256.Create((int)filter[0]);
            Vector256<int> coefficient1 = Vector256.Create((int)filter[1]);
            Vector256<int> coefficient2 = Vector256.Create((int)filter[2]);
            Vector256<int> coefficient3 = Vector256.Create((int)filter[3]);
            Vector256<int> offset = Vector256.Create(bias);
            Vector256<int> ceiling = Vector256.Create(maximum);
            int lastStart = width - Vector256<ushort>.Count;
            for (; column <= lastStart; column += Vector256<ushort>.Count)
            {
                for (int row = 0; row < height; row++)
                {
                    ref TSource firstTap = ref Unsafe.Add(ref sourceBase, (row * sourceStride) + column);

                    // The complete output batch and all six following taps fit in caller-provided context.
                    // Loads remain unaligned so pooled-memory addresses do not impose an alignment contract.
                    Vector256<ushort> result = TOperator.Filter(
                        Av1RestorationSampleOperations.LoadToUInt16(ref Unsafe.Add(ref firstTap, 0 * tapStride), Vector256<ushort>.Zero),
                        Av1RestorationSampleOperations.LoadToUInt16(ref Unsafe.Add(ref firstTap, 1 * tapStride), Vector256<ushort>.Zero),
                        Av1RestorationSampleOperations.LoadToUInt16(ref Unsafe.Add(ref firstTap, 2 * tapStride), Vector256<ushort>.Zero),
                        Av1RestorationSampleOperations.LoadToUInt16(ref Unsafe.Add(ref firstTap, 3 * tapStride), Vector256<ushort>.Zero),
                        Av1RestorationSampleOperations.LoadToUInt16(ref Unsafe.Add(ref firstTap, 4 * tapStride), Vector256<ushort>.Zero),
                        Av1RestorationSampleOperations.LoadToUInt16(ref Unsafe.Add(ref firstTap, 5 * tapStride), Vector256<ushort>.Zero),
                        Av1RestorationSampleOperations.LoadToUInt16(ref Unsafe.Add(ref firstTap, 6 * tapStride), Vector256<ushort>.Zero),
                        coefficient0,
                        coefficient1,
                        coefficient2,
                        coefficient3,
                        offset,
                        roundBits,
                        ceiling);

                    Av1RestorationSampleOperations.Store(result, ref Unsafe.Add(ref destinationBase, (row * destinationStride) + column));
                }
            }
        }

        if (Vector128.IsHardwareAccelerated && width - column >= Vector128<ushort>.Count)
        {
            Vector128<int> coefficient0 = Vector128.Create((int)filter[0]);
            Vector128<int> coefficient1 = Vector128.Create((int)filter[1]);
            Vector128<int> coefficient2 = Vector128.Create((int)filter[2]);
            Vector128<int> coefficient3 = Vector128.Create((int)filter[3]);
            Vector128<int> offset = Vector128.Create(bias);
            Vector128<int> ceiling = Vector128.Create(maximum);
            int lastStart = width - Vector128<ushort>.Count;
            for (; column <= lastStart; column += Vector128<ushort>.Count)
            {
                for (int row = 0; row < height; row++)
                {
                    ref TSource firstTap = ref Unsafe.Add(ref sourceBase, (row * sourceStride) + column);

                    // The complete output batch and all six following taps fit in caller-provided context.
                    // Loads remain unaligned so pooled-memory addresses do not impose an alignment contract.
                    Vector128<ushort> result = TOperator.Filter(
                        Av1RestorationSampleOperations.LoadToUInt16(ref Unsafe.Add(ref firstTap, 0 * tapStride), Vector128<ushort>.Zero),
                        Av1RestorationSampleOperations.LoadToUInt16(ref Unsafe.Add(ref firstTap, 1 * tapStride), Vector128<ushort>.Zero),
                        Av1RestorationSampleOperations.LoadToUInt16(ref Unsafe.Add(ref firstTap, 2 * tapStride), Vector128<ushort>.Zero),
                        Av1RestorationSampleOperations.LoadToUInt16(ref Unsafe.Add(ref firstTap, 3 * tapStride), Vector128<ushort>.Zero),
                        Av1RestorationSampleOperations.LoadToUInt16(ref Unsafe.Add(ref firstTap, 4 * tapStride), Vector128<ushort>.Zero),
                        Av1RestorationSampleOperations.LoadToUInt16(ref Unsafe.Add(ref firstTap, 5 * tapStride), Vector128<ushort>.Zero),
                        Av1RestorationSampleOperations.LoadToUInt16(ref Unsafe.Add(ref firstTap, 6 * tapStride), Vector128<ushort>.Zero),
                        coefficient0,
                        coefficient1,
                        coefficient2,
                        coefficient3,
                        offset,
                        roundBits,
                        ceiling);

                    Av1RestorationSampleOperations.Store(result, ref Unsafe.Add(ref destinationBase, (row * destinationStride) + column));
                }
            }
        }

        for (; column < width; column++)
        {
            for (int row = 0; row < height; row++)
            {
                ref TSource firstTap = ref Unsafe.Add(ref sourceBase, (row * sourceStride) + column);
                ushort result = TOperator.Filter(
                    Av1RestorationSampleOperations.Load(firstTap),
                    Av1RestorationSampleOperations.Load(Unsafe.Add(ref firstTap, tapStride)),
                    Av1RestorationSampleOperations.Load(Unsafe.Add(ref firstTap, 2 * tapStride)),
                    Av1RestorationSampleOperations.Load(Unsafe.Add(ref firstTap, 3 * tapStride)),
                    Av1RestorationSampleOperations.Load(Unsafe.Add(ref firstTap, 4 * tapStride)),
                    Av1RestorationSampleOperations.Load(Unsafe.Add(ref firstTap, 5 * tapStride)),
                    Av1RestorationSampleOperations.Load(Unsafe.Add(ref firstTap, 6 * tapStride)),
                    filter[0],
                    filter[1],
                    filter[2],
                    filter[3],
                    bias,
                    roundBits,
                    maximum);

                Unsafe.Add(ref destinationBase, (row * destinationStride) + column) = Av1RestorationSampleOperations.FromInt32<TDestination>(result);
            }
        }
    }
}
