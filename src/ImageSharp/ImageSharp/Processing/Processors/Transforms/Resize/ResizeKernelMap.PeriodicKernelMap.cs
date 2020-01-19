// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.Memory;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <content>
    /// Contains <see cref="PeriodicKernelMap"/>
    /// </content>
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
                IResampler sampler,
                int sourceLength,
                int destinationLength,
                double ratio,
                double scale,
                int radius,
                int period,
                int cornerInterval)
                : base(
                    memoryAllocator,
                    sampler,
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

            protected override void Initialize()
            {
                // Build top corner data + one period of the mosaic data:
                int startOfFirstRepeatedMosaic = this.cornerInterval + this.period;

                for (int i = 0; i < startOfFirstRepeatedMosaic; i++)
                {
                    ResizeKernel kernel = this.BuildKernel(i, i);
                    this.kernels[i] = kernel;
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
                    ResizeKernel kernel = this.BuildKernel(bottomStartDest + i, bottomStartData + i);
                    this.kernels[bottomStartDest + i] = kernel;
                }
            }
        }
    }
}