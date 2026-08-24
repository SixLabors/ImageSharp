// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms;

internal partial class ResizeKernelMap
{
    /// <summary>
    /// Memory-optimized <see cref="ResizeKernelMap"/> where repeating rows are stored only once.
    /// </summary>
    private sealed class PeriodicKernelMap : ResizeKernelMap
    {
        private readonly int period;

        private readonly int cornerInterval;

        private readonly int sourcePeriod;

        public PeriodicKernelMap(
            MemoryAllocator memoryAllocator,
            int sourceLength,
            int destinationLength,
            double ratio,
            double scale,
            int radius,
            int period,
            int cornerInterval,
            int sourcePeriod)
            : base(
                memoryAllocator,
                sourceLength,
                destinationLength,
                (cornerInterval * 2) + period,
                ratio,
                scale,
                radius)
        {
            this.cornerInterval = cornerInterval;
            this.period = period;
            this.sourcePeriod = sourcePeriod;
        }

        internal override string Info => base.Info + $"|period:{this.period}|cornerInterval:{this.cornerInterval}";

        protected internal override void Initialize<TResampler>(in TResampler sampler)
        {
            // Build top corner data + one period of the mosaic data:
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
                // next repeated sampling window in the source image.
                this.kernels[i] = kernel.AlterLeftValue(kernel.StartIndex + this.sourcePeriod);
            }

            // Build bottom corner data:
            int bottomStartData = this.cornerInterval + this.period;
            for (int i = 0; i < this.cornerInterval; i++)
            {
                this.kernels[bottomStartDest + i] = this.BuildKernel(in sampler, bottomStartDest + i, bottomStartData + i);
            }
        }
    }
}
