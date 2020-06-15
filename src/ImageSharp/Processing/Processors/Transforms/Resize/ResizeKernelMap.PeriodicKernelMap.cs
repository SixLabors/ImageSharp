// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    internal partial class ResizeKernelMap
    {
        /// <summary>
        /// Memory-optimized <see cref="ResizeKernelMap"/> where repeating rows are stored only once.
        /// </summary>
        private sealed class PeriodicKernelMap : ResizeKernelMap
        {
            private readonly int period;

            private readonly int cornerInterval;

            public PeriodicKernelMap(
                MemoryAllocator memoryAllocator,
                int sourceLength,
                int destinationLength,
                double ratio,
                double scale,
                int radius,
                int period,
                int cornerInterval)
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
                    double center = ((i + .5) * this.ratio) - .5;
                    int left = (int)TolerantMath.Ceiling(center - this.radius);
                    ResizeKernel kernel = this.kernels[i - this.period];
                    this.kernels[i] = kernel.AlterLeftValue(left);
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
}
