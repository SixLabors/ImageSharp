// Copyright(c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;

using SixLabors.Memory;

namespace SixLabors.ImageSharp.ParallelUtils
{
    /// <summary>
    /// Defines execution settings for methods in <see cref="ParallelHelper"/>.
    /// </summary>
    internal readonly struct ParallelExecutionSettings
    {
        /// <summary>
        /// Default value for <see cref="MinimumPixelsProcessedPerTask"/>.
        /// </summary>
        public const int DefaultMinimumPixelsProcessedPerTask = 4096;

        /// <summary>
        /// Initializes a new instance of the <see cref="ParallelExecutionSettings"/> struct.
        /// </summary>
        public ParallelExecutionSettings(
            int maxDegreeOfParallelism,
            int minimumPixelsProcessedPerTask,
            MemoryAllocator memoryAllocator)
        {
            this.MaxDegreeOfParallelism = maxDegreeOfParallelism;
            this.MinimumPixelsProcessedPerTask = minimumPixelsProcessedPerTask;
            this.MemoryAllocator = memoryAllocator;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ParallelExecutionSettings"/> struct.
        /// </summary>
        public ParallelExecutionSettings(int maxDegreeOfParallelism, MemoryAllocator memoryAllocator)
            : this(maxDegreeOfParallelism, DefaultMinimumPixelsProcessedPerTask, memoryAllocator)
        {
        }

        /// <summary>
        /// Gets the MemoryAllocator
        /// </summary>
        public MemoryAllocator MemoryAllocator { get; }

        /// <summary>
        /// Gets the value used for initializing <see cref="ParallelOptions.MaxDegreeOfParallelism"/> when using TPL.
        /// </summary>
        public int MaxDegreeOfParallelism { get; }

        /// <summary>
        /// Gets the minimum number of pixels being processed by a single task when parallelizing operations with TPL.
        /// Launching tasks for pixel regions below this limit is not worth the overhead.
        /// Initialized with <see cref="DefaultMinimumPixelsProcessedPerTask"/> by default,
        /// the optimum value is operation specific. (The cheaper the operation, the larger the value is.)
        /// </summary>
        public int MinimumPixelsProcessedPerTask { get; }

        /// <summary>
        /// Creates a new instance of <see cref="ParallelExecutionSettings"/>
        /// having <see cref="MinimumPixelsProcessedPerTask"/> multiplied by <paramref name="multiplier"/>
        /// </summary>
        public ParallelExecutionSettings MultiplyMinimumPixelsPerTask(int multiplier)
        {
            return new ParallelExecutionSettings(
                this.MaxDegreeOfParallelism,
                this.MinimumPixelsProcessedPerTask * multiplier,
                this.MemoryAllocator);
        }
    }
}