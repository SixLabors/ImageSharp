// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <content>
    /// Contains <see cref="MosaicKernelMap"/>
    /// </content>
    internal partial class ResizeKernelMap
    {
        /// <summary>
        /// Memory-optimized <see cref="ResizeKernelMap"/> where repeating rows are stored only once.
        /// </summary>
        private sealed class MosaicKernelMap : ResizeKernelMap
        {
            private readonly int period;

            private readonly int cornerInterval;

            public MosaicKernelMap(
                MemoryAllocator memoryAllocator,
                IResampler sampler,
                int sourceSize,
                int destinationSize,
                float ratio,
                float scale,
                int radius,
                int period,
                int cornerInterval)
                : base(
                    memoryAllocator,
                    sampler,
                    sourceSize,
                    destinationSize,
                    (cornerInterval * 2) + period,
                    ratio,
                    scale,
                    radius)
            {
                this.cornerInterval = cornerInterval;
                this.period = period;
            }

            protected override void Initialize()
            {
                base.Initialize();
            }
        }
    }
}