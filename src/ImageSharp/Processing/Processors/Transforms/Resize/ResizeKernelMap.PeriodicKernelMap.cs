// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms;

internal partial class ResizeKernelMap
{
    /// <summary>
    /// Memory-optimized <see cref="ResizeKernelMap"/> where repeating rows are stored only once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The kernels in the middle of the map repeat: kernel i + period has the same weights as kernel i, and starts
    /// sourcePeriod samples later. The map calculates the weights of the corner kernels and of one period with the
    /// sampler. The other kernels use the rows of the period.
    /// </para>
    /// <para>
    /// Weight row layout: [top corner rows | period rows of shift variant 0 | ... | of the last variant |
    /// bottom corner rows]. Without aligned starts, or if sourcePeriod is a multiple of 4, there is one variant.
    /// </para>
    /// </remarks>
    private sealed class PeriodicKernelMap : ResizeKernelMap
    {
        /// <summary>
        /// The number of destination samples after which the kernels repeat.
        /// </summary>
        private readonly int period;

        /// <summary>
        /// The number of kernels at each end that have their own weight rows.
        /// </summary>
        private readonly int cornerInterval;

        /// <summary>
        /// The number of source samples between a kernel and its copy one period later.
        /// </summary>
        private readonly int sourcePeriod;

        /// <summary>
        /// The number of weight rows for each kernel of the period, one for each alignment shift of its copies.
        /// </summary>
        private readonly int shiftVariants;

        /// <summary>
        /// Initializes a new instance of the <see cref="PeriodicKernelMap"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The allocator of the weight buffer.</param>
        /// <param name="sourceLength">The length of the source.</param>
        /// <param name="destinationLength">The length of the destination.</param>
        /// <param name="ratio">The ratio of the source length to the destination length.</param>
        /// <param name="scale">The scale of the sampler, 1 or more.</param>
        /// <param name="radius">The radius of the kernel in source samples.</param>
        /// <param name="kernelLength">The number of source samples of each kernel before alignment.</param>
        /// <param name="paddedLength">The number of taps in each weight row, a multiple of 4.</param>
        /// <param name="alignKernelStarts">Whether each kernel starts at a multiple of 4 source samples.</param>
        /// <param name="period">The number of destination samples after which the kernels repeat.</param>
        /// <param name="cornerInterval">The number of kernels at each end that have their own weight rows.</param>
        /// <param name="sourcePeriod">The number of source samples between a kernel and its copy one period later.</param>
        /// <param name="shiftVariants">
        /// The number of weight rows for each kernel of the period: 1, or with aligned starts 2 or 4 if
        /// <paramref name="sourcePeriod"/> is not a multiple of 4.
        /// </param>
        public PeriodicKernelMap(
            MemoryAllocator memoryAllocator,
            int sourceLength,
            int destinationLength,
            double ratio,
            double scale,
            double radius,
            int kernelLength,
            int paddedLength,
            bool alignKernelStarts,
            int period,
            int cornerInterval,
            int sourcePeriod,
            int shiftVariants)
            : base(
                memoryAllocator,
                sourceLength,
                destinationLength,
                (cornerInterval * 2) + (period * shiftVariants),
                ratio,
                scale,
                radius,
                kernelLength,
                paddedLength,
                alignKernelStarts)
        {
            this.cornerInterval = cornerInterval;
            this.period = period;
            this.sourcePeriod = sourcePeriod;
            this.shiftVariants = shiftVariants;
        }

        /// <inheritdoc/>
        public override string Info => base.Info + $"|period:{this.period}|cornerInterval:{this.cornerInterval}|shiftVariants:{this.shiftVariants}";

        /// <inheritdoc/>
        protected override void Initialize<TResampler>(in TResampler sampler)
        {
            // Build top corner data + one period of the mosaic data. These are the only middle kernels whose weights
            // the sampler calculates.
            int startOfFirstRepeatedMosaic = this.cornerInterval + this.period;

            for (int i = 0; i < startOfFirstRepeatedMosaic; i++)
            {
                this.kernels[i] = this.BuildKernel(in sampler, i, i);
            }

            // Copy the mosaics:
            int bottomStartDest = this.DestinationLength - this.cornerInterval;
            for (int i = startOfFirstRepeatedMosaic; i < bottomStartDest; i++)
            {
                ResizeKernel kernel = this.kernels[i - this.period];

                // Shift the kernel start index by the source-side period so the same weights align to the
                // next repeated sampling window in the source image. With one variant, the copy has the same alignment
                // shift as the kernel it copies, so it uses the same weight row.
                this.kernels[i] = this.shiftVariants == 1
                    ? kernel.AlterLeftValue(kernel.StartIndex + this.sourcePeriod)
                    : this.CopyKernel(kernel, i);
            }

            // Build bottom corner data. Its rows come after the rows of all shift variants.
            int bottomStartData = this.cornerInterval + (this.period * this.shiftVariants);
            for (int i = 0; i < this.cornerInterval; i++)
            {
                this.kernels[bottomStartDest + i] = this.BuildKernel(in sampler, bottomStartDest + i, bottomStartData + i);
            }
        }

        /// <summary>
        /// Makes the kernel of <paramref name="destinationIndex"/> from the kernel one period before it, when the
        /// alignment shift changes from one period to the next.
        /// </summary>
        /// <param name="previous">The kernel one period before <paramref name="destinationIndex"/>.</param>
        /// <param name="destinationIndex">The destination sample of the new kernel, in the middle of the map.</param>
        /// <returns>The new kernel.</returns>
        private ResizeKernel CopyKernel(in ResizeKernel previous, int destinationIndex)
        {
            // An aligned kernel covers its shift plus MaxDiameter samples, so the shift is Length - MaxDiameter.
            // The first real sample of the new kernel is sourcePeriod samples after the first real sample of the
            // previous kernel. Its new shift aligns it to a multiple of 4.
            int start = previous.StartIndex + (previous.Length - this.MaxDiameter) + this.sourcePeriod;
            int shift = start & 3;

            // Period j of the middle kernels uses variant j % shiftVariants. The shift of period j is
            // (s + j * sourcePeriod) % 4, and it repeats after shiftVariants periods, so all periods with one variant
            // have one shift. The row of the variant is in the block of that variant, at the position in the period.
            int middleIndex = destinationIndex - this.cornerInterval;
            int periodIndex = middleIndex / this.period;
            int position = middleIndex % this.period;
            int dataRowIndex = this.cornerInterval + ((periodIndex % this.shiftVariants) * this.period) + position;

            // The first period that uses a variant fills its row. The row gets the weights of the kernel of the first
            // period, which the sampler calculated, after shift zeros. This is a copy, not a new calculation. The buffer
            // is clean, so the zeros in front and the padding after the weights are already zero.
            if (periodIndex < this.shiftVariants)
            {
                int firstRowIndex = this.cornerInterval + position;
                int firstShift = this.kernels[firstRowIndex].Length - this.MaxDiameter;
                Span<float> weights = this.data.DangerousGetRowSpan(firstRowIndex).Slice(firstShift * 4, this.MaxDiameter * 4);
                weights.CopyTo(this.data.DangerousGetRowSpan(dataRowIndex)[(shift * 4)..]);
            }

            return this.CreateKernel(dataRowIndex, start - shift, this.MaxDiameter + shift);
        }
    }
}
