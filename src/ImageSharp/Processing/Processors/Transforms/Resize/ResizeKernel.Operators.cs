// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms;

// The resize is separable. The horizontal pass convolves each source row and makes one first-pass row of destination
// width. The vertical pass convolves the first-pass rows and makes one destination row.
//
// Data layout:
// - A pixel is 4 floats, RGBA. A Vector128<float> holds 1 pixel, a Vector256<float> 2 pixels, a Vector512<float> 4 pixels.
// - ResizeKernelMap stores each weight 4 times: [w0 w0 w0 w0 | w1 w1 w1 w1 | ...]. Then a load of N expanded weights
//   has the same layout as a load of N pixels, and one multiply gives w[t] * pixel[t] for each channel of N taps.
// - The map pads the weights of each kernel to PaddedLength, a multiple of 4, with the weight zero.
//
// Each operator returns full registers of destination pixels: 4 pixels for Vector512, 2 for Vector256, 1 for Vector128.
// The traversals ConvolveRows and ConvolveColumns use the widest accelerated register for most pixels, then each
// narrower register for the pixels that remain. The Vector128 operators also serve the path without hardware
// acceleration, because the runtime emulates Vector128 in software there. Thus, no scalar loop is necessary.
//
// What limits the horizontal loop:
// Each multiply-add of the horizontal loop needs one load of weights and one load of pixels. Measurements of the
// horizontal pass of a 1280 to 150 pixel bicubic resize show that these loads limit the loop. More independent
// multiply-add chains gave no gain, and 11% more multiply-adds with fewer cache accesses was faster. Thus the
// horizontal operators reduce the cache accesses in two ways:
// - Two rows: all source rows use the same kernels. The horizontal operators convolve 2 source rows at the same time,
//   so one load of weights serves 2 multiply-adds. This changes 2 loads for each multiply-add to 1.5 loads.
// - Aligned starts: ResizeKernelMap can start each horizontal kernel at a multiple of 4 pixels. If the source row
//   starts on a 64-byte boundary, each 4-pixel load then starts on a cache line, and costs 1 cache access, not 2.
//   The workers align their source rows. See ResizeKernelMap.Calculate.
//
// Loads and stores:
// All loads and stores are unaligned (LoadUnsafe/StoreUnsafe). The buffers have no alignment guarantee. On current x64
// and Arm64 hardware, an unaligned load that does not cross a cache line has the same cost as an aligned load.
//
// Scalar equivalence:
// For each destination pixel, the result is sum(w[t] * pixel[t]) over the taps of its kernel. The operators add the
// products in this fixed order, for all register widths:
// - Horizontal: partial sum l (l = 0..3) adds the taps t with t % 4 == l, in increasing t. The result is
//   (p0 + p2) + (p1 + p3). The two rows use separate accumulators, so each row gets the same result as alone.
// - Vertical: the taps are added in increasing t.
// MultiplyAddEstimate can fuse the multiply and the add, or not, as the hardware permits. The choice is the same for
// all widths on one machine, so all widths give the same bits on that machine.
//
// Aligned starts do not change the results:
// An aligned kernel has s = 0 to 3 weights of zero in front, so real tap t is at position t + s. Partial sum l then
// adds the real taps with (t + s) % 4 == l. These are the same 4 sums as without alignment, in a rotated order, and
// each sum adds the same taps in the same order. The products of the zero weights are +0 or -0, and they come before
// the real taps or after them. An addition of a zero to a sum that is not zero does not change the sum.
// The reduction adds lanes 0 and 2, and lanes 1 and 3. With any rotation, these are the pairs {p0, p2} and {p1, p3},
// with the operands in a different order:
// - s = 2: (p2 + p0) + (p3 + p1).
// - s = 1 or 3: (p3 + p1) + (p0 + p2), or (p1 + p3) + (p2 + p0).
// Float addition is commutative, so the result has the same bits. The only possible difference is the sign of a
// result that is zero, and both signs give the same pixel.

/// <content>
/// Contains the convolution operators of the resize kernels.
/// </content>
internal readonly partial struct ResizeKernel
{
    /// <summary>
    /// Gets the number of floats from the start of <paramref name="buffer"/> to the first 64-byte boundary in it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The memory allocator does not align its buffers. A worker allocates 15 floats more than it needs, and starts its
    /// source rows at this offset, so that the aligned kernel starts of the horizontal map give loads that start on a
    /// cache line.
    /// </para>
    /// <para>
    /// The value is correct only for the current address of the buffer. The garbage collector can move a managed
    /// buffer. Then the rows are not aligned, and the loads are slower, but the results do not change. Thus, a worker
    /// gets the offset again each time it starts to fill its rows.
    /// </para>
    /// </remarks>
    /// <param name="buffer">The buffer. A float buffer always starts at a multiple of 4 bytes.</param>
    /// <returns>The offset in floats, 0 to 15.</returns>
    public static unsafe int GetCacheLineOffset(Span<float> buffer)
    {
        // -address & 63 is the number of bytes to the next multiple of 64, or 0 if the address is a multiple of 64.
        // The address is a multiple of 4, so the number of bytes divides by 4 with no remainder.
        nuint address = (nuint)Unsafe.AsPointer(ref MemoryMarshal.GetReference(buffer));
        return (int)((0 - address) & 63) / sizeof(float);
    }

    /// <summary>
    /// Convolves consecutive kernels on two source rows, and stores one result for each kernel and row.
    /// </summary>
    /// <remarks>
    /// To convolve one row only, pass the same row and the same destination for both rows. The operators then
    /// calculate the same values twice and store them to the same place.
    /// </remarks>
    /// <param name="kernels">The first kernel.</param>
    /// <param name="weights">The first float of the weight buffer of the kernel map.</param>
    /// <param name="pixels0">The first float of the first source row.</param>
    /// <param name="pixels1">The first float of the second source row.</param>
    /// <param name="length">The padded kernel length, a multiple of 4.</param>
    /// <param name="count">The number of kernels.</param>
    /// <param name="destination0">The first float of the results of the first row. The results are consecutive pixels.</param>
    /// <param name="destination1">The first float of the results of the second row. The results are consecutive pixels.</param>
    public static void ConvolveRows(
        ref ResizeKernel kernels,
        ref float weights,
        ref float pixels0,
        ref float pixels1,
        int length,
        int count,
        ref float destination0,
        ref float destination1)
    {
        nuint total = (uint)count;
        nuint x = 0;

        // IsHardwareAccelerated is a JIT constant, so the JIT removes the loops that the hardware cannot use.
        // Pixel x starts at float x * 4 of each destination row.
        if (Vector512.IsHardwareAccelerated)
        {
            // 4 kernels in each step.
            for (; x + 4 <= total; x += 4)
            {
                Vector512HorizontalOperator.Apply(
                    ref Unsafe.Add(ref kernels, x),
                    ref weights,
                    ref pixels0,
                    ref pixels1,
                    length,
                    out Vector512<float> result0,
                    out Vector512<float> result1);

                result0.StoreUnsafe(ref destination0, x * 4);
                result1.StoreUnsafe(ref destination1, x * 4);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            // 2 kernels in each step. With Vector512, this loop does at most 1 step, for the remainder of 2 or 3 kernels.
            for (; x + 2 <= total; x += 2)
            {
                Vector256HorizontalOperator.Apply(
                    ref Unsafe.Add(ref kernels, x),
                    ref weights,
                    ref pixels0,
                    ref pixels1,
                    length,
                    out Vector256<float> result0,
                    out Vector256<float> result1);

                result0.StoreUnsafe(ref destination0, x * 4);
                result1.StoreUnsafe(ref destination1, x * 4);
            }
        }

        // 1 kernel in each step. With Vector256, this loop does at most 1 step, for the last odd kernel.
        for (; x < total; x++)
        {
            Vector128HorizontalOperator.Apply(
                ref Unsafe.Add(ref kernels, x),
                ref weights,
                ref pixels0,
                ref pixels1,
                length,
                out Vector128<float> result0,
                out Vector128<float> result1);

            result0.StoreUnsafe(ref destination0, x * 4);
            result1.StoreUnsafe(ref destination1, x * 4);
        }
    }

    /// <summary>
    /// Convolves one kernel on the consecutive pixels of the first-pass rows, and stores one result for each pixel.
    /// </summary>
    /// <param name="weights">The first expanded weight of the kernel.</param>
    /// <param name="rows">The first float of the first pixel in the first row of the kernel.</param>
    /// <param name="rowStride">The distance in floats from one row to the next.</param>
    /// <param name="length">The number of taps of the kernel.</param>
    /// <param name="count">The number of pixels.</param>
    /// <param name="destination">The first float of the result of the first pixel. The results are consecutive pixels.</param>
    public static void ConvolveColumns(ref float weights, ref float rows, nuint rowStride, int length, int count, ref float destination)
    {
        nuint total = (uint)count;
        nuint x = 0;

        // All pixels of one destination row use the same kernel. Pixel x starts at float x * 4 of each first-pass row
        // and of the destination row. The remainders are the same as in ConvolveRows.
        if (Vector512.IsHardwareAccelerated)
        {
            for (; x + 4 <= total; x += 4)
            {
                Vector512VerticalOperator.Apply(ref weights, ref Unsafe.Add(ref rows, x * 4), rowStride, length).StoreUnsafe(ref destination, x * 4);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; x + 2 <= total; x += 2)
            {
                Vector256VerticalOperator.Apply(ref weights, ref Unsafe.Add(ref rows, x * 4), rowStride, length).StoreUnsafe(ref destination, x * 4);
            }
        }

        for (; x < total; x++)
        {
            Vector128VerticalOperator.Apply(ref weights, ref Unsafe.Add(ref rows, x * 4), rowStride, length).StoreUnsafe(ref destination, x * 4);
        }
    }

    /// <summary>
    /// Convolves 4 kernels on 2 source rows, and returns 4 destination pixels of each row in one <see cref="Vector512{T}"/>.
    /// </summary>
    public readonly struct Vector512HorizontalOperator
    {
        /// <summary>
        /// Convolves the 4 kernels that start at <paramref name="kernels"/> on both source rows.
        /// </summary>
        /// <param name="kernels">The first of the 4 kernels.</param>
        /// <param name="weights">The first float of the weight buffer of the kernel map.</param>
        /// <param name="pixels0">The first float of the first source row.</param>
        /// <param name="pixels1">The first float of the second source row.</param>
        /// <param name="length">The padded kernel length, a multiple of 4.</param>
        /// <param name="result0">The 4 destination pixels of the first row.</param>
        /// <param name="result1">The 4 destination pixels of the second row.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(
            ref ResizeKernel kernels,
            ref float weights,
            ref float pixels0,
            ref float pixels1,
            int length,
            out Vector512<float> result0,
            out Vector512<float> result1)
        {
            // Each kernel has its own weights and its own first source pixel. A pixel is 4 floats. The two rows use the
            // same kernels, so the pixel offset of a kernel is the same in both rows.
            ref ResizeKernel kernel1 = ref Unsafe.Add(ref kernels, 1);
            ref ResizeKernel kernel2 = ref Unsafe.Add(ref kernels, 2);
            ref ResizeKernel kernel3 = ref Unsafe.Add(ref kernels, 3);
            ref float weights0 = ref Unsafe.Add(ref weights, (uint)kernels.WeightsOffset);
            ref float weights1 = ref Unsafe.Add(ref weights, (uint)kernel1.WeightsOffset);
            ref float weights2 = ref Unsafe.Add(ref weights, (uint)kernel2.WeightsOffset);
            ref float weights3 = ref Unsafe.Add(ref weights, (uint)kernel3.WeightsOffset);
            nuint start0 = (nuint)(uint)kernels.StartIndex * 4;
            nuint start1 = (nuint)(uint)kernel1.StartIndex * 4;
            nuint start2 = (nuint)(uint)kernel2.StartIndex * 4;
            nuint start3 = (nuint)(uint)kernel3.StartIndex * 4;
            ref float row0Pixels0 = ref Unsafe.Add(ref pixels0, start0);
            ref float row0Pixels1 = ref Unsafe.Add(ref pixels0, start1);
            ref float row0Pixels2 = ref Unsafe.Add(ref pixels0, start2);
            ref float row0Pixels3 = ref Unsafe.Add(ref pixels0, start3);
            ref float row1Pixels0 = ref Unsafe.Add(ref pixels1, start0);
            ref float row1Pixels1 = ref Unsafe.Add(ref pixels1, start1);
            ref float row1Pixels2 = ref Unsafe.Add(ref pixels1, start2);
            ref float row1Pixels3 = ref Unsafe.Add(ref pixels1, start3);

            // One step reads 16 floats: taps t to t + 3 of one kernel. The 128-bit lane l of the product holds
            // w[t + l] * pixel[t + l], so lane l of an accumulator holds partial sum l of its kernel and row:
            // [pK0 | pK1 | pK2 | pK3], where pKl is the RGBA sum of the taps t with t % 4 == l.
            // Each step loads the weights of the 4 kernels once and uses each load for both rows. So the step does
            // 8 multiply-adds with 4 weight loads and 8 pixel loads, not 16 loads.
            // The 8 accumulators are 8 independent chains of multiply-adds, so the CPU can run them in parallel.
            // length is a multiple of 4, so the loop has no remainder.
            Vector512<float> sum00 = Vector512<float>.Zero;
            Vector512<float> sum01 = Vector512<float>.Zero;
            Vector512<float> sum02 = Vector512<float>.Zero;
            Vector512<float> sum03 = Vector512<float>.Zero;
            Vector512<float> sum10 = Vector512<float>.Zero;
            Vector512<float> sum11 = Vector512<float>.Zero;
            Vector512<float> sum12 = Vector512<float>.Zero;
            Vector512<float> sum13 = Vector512<float>.Zero;
            for (nuint offset = 0; offset < (uint)length * 4; offset += 16)
            {
                Vector512<float> w0 = Vector512.LoadUnsafe(ref weights0, offset);
                Vector512<float> w1 = Vector512.LoadUnsafe(ref weights1, offset);
                Vector512<float> w2 = Vector512.LoadUnsafe(ref weights2, offset);
                Vector512<float> w3 = Vector512.LoadUnsafe(ref weights3, offset);
                sum00 = Vector512.MultiplyAddEstimate(w0, Vector512.LoadUnsafe(ref row0Pixels0, offset), sum00);
                sum01 = Vector512.MultiplyAddEstimate(w1, Vector512.LoadUnsafe(ref row0Pixels1, offset), sum01);
                sum02 = Vector512.MultiplyAddEstimate(w2, Vector512.LoadUnsafe(ref row0Pixels2, offset), sum02);
                sum03 = Vector512.MultiplyAddEstimate(w3, Vector512.LoadUnsafe(ref row0Pixels3, offset), sum03);
                sum10 = Vector512.MultiplyAddEstimate(w0, Vector512.LoadUnsafe(ref row1Pixels0, offset), sum10);
                sum11 = Vector512.MultiplyAddEstimate(w1, Vector512.LoadUnsafe(ref row1Pixels1, offset), sum11);
                sum12 = Vector512.MultiplyAddEstimate(w2, Vector512.LoadUnsafe(ref row1Pixels2, offset), sum12);
                sum13 = Vector512.MultiplyAddEstimate(w3, Vector512.LoadUnsafe(ref row1Pixels3, offset), sum13);
            }

            result0 = Reduce(sum00, sum01, sum02, sum03);
            result1 = Reduce(sum10, sum11, sum12, sum13);
        }

        /// <summary>
        /// Adds the 4 partial sums of each of 4 kernels, and puts the pixel of kernel K in 128-bit lane K.
        /// </summary>
        /// <param name="sum0">The partial sums [p0 | p1 | p2 | p3] of kernel 0.</param>
        /// <param name="sum1">The partial sums of kernel 1.</param>
        /// <param name="sum2">The partial sums of kernel 2.</param>
        /// <param name="sum3">The partial sums of kernel 3.</param>
        /// <returns>The 4 destination pixels, (p0 + p2) + (p1 + p3) of each kernel.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<float> Reduce(Vector512<float> sum0, Vector512<float> sum1, Vector512<float> sum2, Vector512<float> sum3)
        {
            // Reduce and transpose in 2 stages. Shuffle4x128(a, b, control) takes destination lanes 0 and 1 from a,
            // and lanes 2 and 3 from b. Each 2-bit field of control selects one source lane.
            //
            // Stage 1. MMShuffle1010 selects lanes [0, 1] of each source, and MMShuffle3232 selects lanes [2, 3]:
            //   [p00 | p01 | p10 | p11] + [p02 | p03 | p12 | p13]
            //   sum01 = [p00 + p02 | p01 + p03 | p10 + p12 | p11 + p13]
            // sum23 is the same for kernels 2 and 3.
            Vector512<float> sum01 = Vector512_.Shuffle4x128(sum0.AsInt32(), sum1.AsInt32(), SimdUtils.Shuffle.MMShuffle1010).AsSingle()
                + Vector512_.Shuffle4x128(sum0.AsInt32(), sum1.AsInt32(), SimdUtils.Shuffle.MMShuffle3232).AsSingle();
            Vector512<float> sum23 = Vector512_.Shuffle4x128(sum2.AsInt32(), sum3.AsInt32(), SimdUtils.Shuffle.MMShuffle1010).AsSingle()
                + Vector512_.Shuffle4x128(sum2.AsInt32(), sum3.AsInt32(), SimdUtils.Shuffle.MMShuffle3232).AsSingle();

            // Stage 2. MMShuffle2020 selects lanes [0, 2] of each source, and MMShuffle3131 selects lanes [1, 3]:
            //   [k0 (p0 + p2) | k1 (p0 + p2) | k2 (p0 + p2) | k3 (p0 + p2)]
            // + [k0 (p1 + p3) | k1 (p1 + p3) | k2 (p1 + p3) | k3 (p1 + p3)]
            // Lane K of the result is destination pixel K, (p0 + p2) + (p1 + p3) of kernel K.
            return Vector512_.Shuffle4x128(sum01.AsInt32(), sum23.AsInt32(), SimdUtils.Shuffle.MMShuffle2020).AsSingle()
                + Vector512_.Shuffle4x128(sum01.AsInt32(), sum23.AsInt32(), SimdUtils.Shuffle.MMShuffle3131).AsSingle();
        }
    }

    /// <summary>
    /// Convolves 2 kernels on 2 source rows, and returns 2 destination pixels of each row in one <see cref="Vector256{T}"/>.
    /// </summary>
    public readonly struct Vector256HorizontalOperator
    {
        /// <summary>
        /// Convolves the 2 kernels that start at <paramref name="kernels"/> on both source rows.
        /// </summary>
        /// <param name="kernels">The first of the 2 kernels.</param>
        /// <param name="weights">The first float of the weight buffer of the kernel map.</param>
        /// <param name="pixels0">The first float of the first source row.</param>
        /// <param name="pixels1">The first float of the second source row.</param>
        /// <param name="length">The padded kernel length, a multiple of 4.</param>
        /// <param name="result0">The 2 destination pixels of the first row.</param>
        /// <param name="result1">The 2 destination pixels of the second row.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(
            ref ResizeKernel kernels,
            ref float weights,
            ref float pixels0,
            ref float pixels1,
            int length,
            out Vector256<float> result0,
            out Vector256<float> result1)
        {
            ref ResizeKernel kernel1 = ref Unsafe.Add(ref kernels, 1);
            ref float weights0 = ref Unsafe.Add(ref weights, (uint)kernels.WeightsOffset);
            ref float weights1 = ref Unsafe.Add(ref weights, (uint)kernel1.WeightsOffset);
            nuint start0 = (nuint)(uint)kernels.StartIndex * 4;
            nuint start1 = (nuint)(uint)kernel1.StartIndex * 4;
            ref float row0Pixels0 = ref Unsafe.Add(ref pixels0, start0);
            ref float row0Pixels1 = ref Unsafe.Add(ref pixels0, start1);
            ref float row1Pixels0 = ref Unsafe.Add(ref pixels1, start0);
            ref float row1Pixels1 = ref Unsafe.Add(ref pixels1, start1);

            // One register holds 2 taps. One step reads taps t to t + 3 of each kernel in 2 registers. For row R and
            // kernel K, sumRK0 lanes [0 | 1] accumulate [pK0 | pK1], and sumRK1 lanes [0 | 1] accumulate [pK2 | pK3].
            // These are the same partial sums, in the same order, as the 512-bit operator.
            // Each weight load serves both rows: 4 weight loads and 8 pixel loads for 8 multiply-adds.
            Vector256<float> sum000 = Vector256<float>.Zero;
            Vector256<float> sum001 = Vector256<float>.Zero;
            Vector256<float> sum010 = Vector256<float>.Zero;
            Vector256<float> sum011 = Vector256<float>.Zero;
            Vector256<float> sum100 = Vector256<float>.Zero;
            Vector256<float> sum101 = Vector256<float>.Zero;
            Vector256<float> sum110 = Vector256<float>.Zero;
            Vector256<float> sum111 = Vector256<float>.Zero;
            for (nuint offset = 0; offset < (uint)length * 4; offset += 16)
            {
                Vector256<float> w00 = Vector256.LoadUnsafe(ref weights0, offset);
                Vector256<float> w01 = Vector256.LoadUnsafe(ref weights0, offset + 8);
                Vector256<float> w10 = Vector256.LoadUnsafe(ref weights1, offset);
                Vector256<float> w11 = Vector256.LoadUnsafe(ref weights1, offset + 8);
                sum000 = Vector256.MultiplyAddEstimate(w00, Vector256.LoadUnsafe(ref row0Pixels0, offset), sum000);
                sum001 = Vector256.MultiplyAddEstimate(w01, Vector256.LoadUnsafe(ref row0Pixels0, offset + 8), sum001);
                sum010 = Vector256.MultiplyAddEstimate(w10, Vector256.LoadUnsafe(ref row0Pixels1, offset), sum010);
                sum011 = Vector256.MultiplyAddEstimate(w11, Vector256.LoadUnsafe(ref row0Pixels1, offset + 8), sum011);
                sum100 = Vector256.MultiplyAddEstimate(w00, Vector256.LoadUnsafe(ref row1Pixels0, offset), sum100);
                sum101 = Vector256.MultiplyAddEstimate(w01, Vector256.LoadUnsafe(ref row1Pixels0, offset + 8), sum101);
                sum110 = Vector256.MultiplyAddEstimate(w10, Vector256.LoadUnsafe(ref row1Pixels1, offset), sum110);
                sum111 = Vector256.MultiplyAddEstimate(w11, Vector256.LoadUnsafe(ref row1Pixels1, offset + 8), sum111);
            }

            result0 = Reduce(sum000 + sum001, sum010 + sum011);
            result1 = Reduce(sum100 + sum101, sum110 + sum111);
        }

        /// <summary>
        /// Adds the partial sums of each of 2 kernels, and puts the pixel of kernel K in 128-bit lane K.
        /// </summary>
        /// <param name="kernel0Sum">The partial sums [p0 + p2 | p1 + p3] of kernel 0.</param>
        /// <param name="kernel1Sum">The partial sums [p0 + p2 | p1 + p3] of kernel 1.</param>
        /// <returns>The 2 destination pixels, (p0 + p2) + (p1 + p3) of each kernel.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<float> Reduce(Vector256<float> kernel0Sum, Vector256<float> kernel1Sum)
        {
            // The input sums are the same as stage 1 of the 512-bit operator.
            // The lower lanes make [k0 (p0 + p2) | k1 (p0 + p2)], and the upper lanes make [k0 (p1 + p3) | k1 (p1 + p3)].
            // Their sum gives lane K = destination pixel K, (p0 + p2) + (p1 + p3) of kernel K.
            return Vector256.Create(kernel0Sum.GetLower(), kernel1Sum.GetLower()) + Vector256.Create(kernel0Sum.GetUpper(), kernel1Sum.GetUpper());
        }
    }

    /// <summary>
    /// Convolves 1 kernel on 2 source rows, and returns 1 destination pixel of each row in one <see cref="Vector128{T}"/>.
    /// </summary>
    /// <remarks>
    /// This operator also serves the path without hardware acceleration.
    /// </remarks>
    public readonly struct Vector128HorizontalOperator
    {
        /// <summary>
        /// Convolves <paramref name="kernel"/> on both source rows.
        /// </summary>
        /// <param name="kernel">The kernel.</param>
        /// <param name="weights">The first float of the weight buffer of the kernel map.</param>
        /// <param name="pixels0">The first float of the first source row.</param>
        /// <param name="pixels1">The first float of the second source row.</param>
        /// <param name="length">The padded kernel length, a multiple of 4.</param>
        /// <param name="result0">The destination pixel of the first row.</param>
        /// <param name="result1">The destination pixel of the second row.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(
            ref ResizeKernel kernel,
            ref float weights,
            ref float pixels0,
            ref float pixels1,
            int length,
            out Vector128<float> result0,
            out Vector128<float> result1)
        {
            ref float kernelWeights = ref Unsafe.Add(ref weights, (uint)kernel.WeightsOffset);
            nuint start = (nuint)(uint)kernel.StartIndex * 4;
            ref float row0Pixels = ref Unsafe.Add(ref pixels0, start);
            ref float row1Pixels = ref Unsafe.Add(ref pixels1, start);

            // One register holds 1 tap. One step reads taps t to t + 3, and accumulator l of each row adds tap t + l.
            // Thus accumulator l holds pl, the same partial sum as lane l of the 512-bit operator.
            // Each weight load serves both rows: 4 weight loads and 8 pixel loads for 8 multiply-adds.
            Vector128<float> sum00 = Vector128<float>.Zero;
            Vector128<float> sum01 = Vector128<float>.Zero;
            Vector128<float> sum02 = Vector128<float>.Zero;
            Vector128<float> sum03 = Vector128<float>.Zero;
            Vector128<float> sum10 = Vector128<float>.Zero;
            Vector128<float> sum11 = Vector128<float>.Zero;
            Vector128<float> sum12 = Vector128<float>.Zero;
            Vector128<float> sum13 = Vector128<float>.Zero;
            for (nuint offset = 0; offset < (uint)length * 4; offset += 16)
            {
                ref float tapWeights = ref Unsafe.Add(ref kernelWeights, offset);
                ref float tapRow0 = ref Unsafe.Add(ref row0Pixels, offset);
                ref float tapRow1 = ref Unsafe.Add(ref row1Pixels, offset);
                Vector128<float> w0 = Vector128.LoadUnsafe(ref tapWeights);
                Vector128<float> w1 = Vector128.LoadUnsafe(ref tapWeights, 4);
                Vector128<float> w2 = Vector128.LoadUnsafe(ref tapWeights, 8);
                Vector128<float> w3 = Vector128.LoadUnsafe(ref tapWeights, 12);
                sum00 = Vector128.MultiplyAddEstimate(w0, Vector128.LoadUnsafe(ref tapRow0), sum00);
                sum01 = Vector128.MultiplyAddEstimate(w1, Vector128.LoadUnsafe(ref tapRow0, 4), sum01);
                sum02 = Vector128.MultiplyAddEstimate(w2, Vector128.LoadUnsafe(ref tapRow0, 8), sum02);
                sum03 = Vector128.MultiplyAddEstimate(w3, Vector128.LoadUnsafe(ref tapRow0, 12), sum03);
                sum10 = Vector128.MultiplyAddEstimate(w0, Vector128.LoadUnsafe(ref tapRow1), sum10);
                sum11 = Vector128.MultiplyAddEstimate(w1, Vector128.LoadUnsafe(ref tapRow1, 4), sum11);
                sum12 = Vector128.MultiplyAddEstimate(w2, Vector128.LoadUnsafe(ref tapRow1, 8), sum12);
                sum13 = Vector128.MultiplyAddEstimate(w3, Vector128.LoadUnsafe(ref tapRow1, 12), sum13);
            }

            // The same order of additions as the wider operators.
            result0 = (sum00 + sum02) + (sum01 + sum03);
            result1 = (sum10 + sum12) + (sum11 + sum13);
        }
    }

    /// <summary>
    /// Convolves one kernel on 4 neighboring pixels of the first-pass rows, and returns them in one <see cref="Vector512{T}"/>.
    /// </summary>
    public readonly struct Vector512VerticalOperator
    {
        /// <summary>
        /// Convolves the kernel on the 4 pixels that start at <paramref name="rows"/>.
        /// </summary>
        /// <param name="weights">The first expanded weight of the kernel.</param>
        /// <param name="rows">The first float of the first pixel in the first row of the kernel.</param>
        /// <param name="rowStride">The distance in floats from one row to the next.</param>
        /// <param name="length">The number of taps of the kernel.</param>
        /// <returns>The 4 destination pixels.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<float> Apply(ref float weights, ref float rows, nuint rowStride, int length)
        {
            // Tap t is row t of the kernel. One load reads 4 neighboring pixels of that row, and all 4 pixels use the
            // same weight. The expanded weight of tap t starts at float t * 4. Its first copy is broadcast to all 16 lanes.
            // Lane x of the sum is destination pixel x, with the taps added in increasing t. The loop reads only the real
            // taps, so it does not use PaddedLength.
            Vector512<float> sum = Vector512<float>.Zero;
            for (nuint tap = 0; tap < (uint)length; tap++)
            {
                sum = Vector512.MultiplyAddEstimate(Vector512.Create(Unsafe.Add(ref weights, tap * 4)), Vector512.LoadUnsafe(ref rows, tap * rowStride), sum);
            }

            return sum;
        }
    }

    /// <summary>
    /// Convolves one kernel on 2 neighboring pixels of the first-pass rows, and returns them in one <see cref="Vector256{T}"/>.
    /// </summary>
    public readonly struct Vector256VerticalOperator
    {
        /// <summary>
        /// Convolves the kernel on the 2 pixels that start at <paramref name="rows"/>.
        /// </summary>
        /// <param name="weights">The first expanded weight of the kernel.</param>
        /// <param name="rows">The first float of the first pixel in the first row of the kernel.</param>
        /// <param name="rowStride">The distance in floats from one row to the next.</param>
        /// <param name="length">The number of taps of the kernel.</param>
        /// <returns>The 2 destination pixels.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> Apply(ref float weights, ref float rows, nuint rowStride, int length)
        {
            // The same steps as the 512-bit operator, for 2 pixels.
            Vector256<float> sum = Vector256<float>.Zero;
            for (nuint tap = 0; tap < (uint)length; tap++)
            {
                sum = Vector256.MultiplyAddEstimate(Vector256.Create(Unsafe.Add(ref weights, tap * 4)), Vector256.LoadUnsafe(ref rows, tap * rowStride), sum);
            }

            return sum;
        }
    }

    /// <summary>
    /// Convolves one kernel on 1 pixel of the first-pass rows, and returns it in one <see cref="Vector128{T}"/>.
    /// </summary>
    /// <remarks>
    /// This operator also serves the path without hardware acceleration.
    /// </remarks>
    public readonly struct Vector128VerticalOperator
    {
        /// <summary>
        /// Convolves the kernel on the pixel at <paramref name="rows"/>.
        /// </summary>
        /// <param name="weights">The first expanded weight of the kernel.</param>
        /// <param name="rows">The first float of the pixel in the first row of the kernel.</param>
        /// <param name="rowStride">The distance in floats from one row to the next.</param>
        /// <param name="length">The number of taps of the kernel.</param>
        /// <returns>The destination pixel.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Apply(ref float weights, ref float rows, nuint rowStride, int length)
        {
            // The expanded weight of tap t is 4 copies, so one load gives the broadcast weight with no shuffle.
            // The taps are added in increasing t, as in the wider operators.
            Vector128<float> sum = Vector128<float>.Zero;
            for (nuint tap = 0; tap < (uint)length; tap++)
            {
                sum = Vector128.MultiplyAddEstimate(Vector128.LoadUnsafe(ref weights, tap * 4), Vector128.LoadUnsafe(ref rows, tap * rowStride), sum);
            }

            return sum;
        }
    }
}
