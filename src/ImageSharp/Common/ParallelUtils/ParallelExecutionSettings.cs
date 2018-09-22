// Copyright(c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;

using SixLabors.Memory;

namespace SixLabors.ImageSharp.ParallelUtils
{
    /// <summary>
    /// Defines execution settings for methods in <see cref="ParallelHelper"/>.
    /// </summary>
    internal struct ParallelExecutionSettings
    {
        /// <summary>
        /// Default value for <see cref="MinimumPixelsProcessedPerTask"/>.
        /// </summary>
        public const int DefaultMinimumPixelsProcessedPerTask = 2048;

        public ParallelExecutionSettings(
            int maxDegreeOfParallelism,
            int minimumPixelsProcessedPerTask,
            MemoryAllocator memoryAllocator)
        {
            this.MaxDegreeOfParallelism = maxDegreeOfParallelism;
            this.MinimumPixelsProcessedPerTask = minimumPixelsProcessedPerTask;
            this.MemoryAllocator = memoryAllocator;
        }

        public ParallelExecutionSettings(int maxDegreeOfParallelism, MemoryAllocator memoryAllocator)
            : this(maxDegreeOfParallelism, DefaultMinimumPixelsProcessedPerTask, memoryAllocator)
        {
        }

        public MemoryAllocator MemoryAllocator { get; }

        /// <summary>
        /// Gets the value used for initializing <see cref="ParallelOptions.MaxDegreeOfParallelism"/> when using TPL.
        /// </summary>
        public int MaxDegreeOfParallelism { get; }

        /// <summary>
        /// Gets the minimum number of pixels being processed by a single task when parallelizing operations with TPL.
        /// Launching tasks for pixel regions below this limit is not worth the overhead.
        /// Initialized with 2048 by default, the optimum value is operation specific.
        /// </summary>
        public int MinimumPixelsProcessedPerTask { get; }

        public ParallelExecutionSettings MultiplyMinimumPixelsPerTask(int multiplier)
        {
            return new ParallelExecutionSettings(
                this.MaxDegreeOfParallelism,
                this.MinimumPixelsProcessedPerTask * multiplier,
                this.MemoryAllocator);
        }
    }
}