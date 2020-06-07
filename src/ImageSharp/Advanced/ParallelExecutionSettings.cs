// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Advanced
{
    /// <summary>
    /// Defines execution settings for methods in <see cref="ParallelRowIterator"/>.
    /// </summary>
    public readonly struct ParallelExecutionSettings
    {
        /// <summary>
        /// Default value for <see cref="MinimumPixelsProcessedPerTask"/>.
        /// </summary>
        public const int DefaultMinimumPixelsProcessedPerTask = 4096;

        /// <summary>
        /// Initializes a new instance of the <see cref="ParallelExecutionSettings"/> struct.
        /// </summary>
        /// <param name="maxDegreeOfParallelism">The value used for initializing <see cref="ParallelOptions.MaxDegreeOfParallelism"/> when using TPL.</param>
        /// <param name="minimumPixelsProcessedPerTask">The value for <see cref="MinimumPixelsProcessedPerTask"/>.</param>
        /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/>.</param>
        public ParallelExecutionSettings(
            int maxDegreeOfParallelism,
            int minimumPixelsProcessedPerTask,
            MemoryAllocator memoryAllocator)
        {
            // Shall be compatible with ParallelOptions.MaxDegreeOfParallelism:
            // https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.paralleloptions.maxdegreeofparallelism
            if (maxDegreeOfParallelism == 0 || maxDegreeOfParallelism < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));
            }

            Guard.MustBeGreaterThan(minimumPixelsProcessedPerTask, 0, nameof(minimumPixelsProcessedPerTask));
            Guard.NotNull(memoryAllocator, nameof(memoryAllocator));

            this.MaxDegreeOfParallelism = maxDegreeOfParallelism;
            this.MinimumPixelsProcessedPerTask = minimumPixelsProcessedPerTask;
            this.MemoryAllocator = memoryAllocator;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ParallelExecutionSettings"/> struct.
        /// </summary>
        /// <param name="maxDegreeOfParallelism">The value used for initializing <see cref="ParallelOptions.MaxDegreeOfParallelism"/> when using TPL.</param>
        /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/>.</param>
        public ParallelExecutionSettings(int maxDegreeOfParallelism, MemoryAllocator memoryAllocator)
            : this(maxDegreeOfParallelism, DefaultMinimumPixelsProcessedPerTask, memoryAllocator)
        {
        }

        /// <summary>
        /// Gets the <see cref="MemoryAllocator"/>.
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
        /// <param name="multiplier">The value to multiply <see cref="MinimumPixelsProcessedPerTask"/> with.</param>
        /// <returns>The modified <see cref="ParallelExecutionSettings"/>.</returns>
        public ParallelExecutionSettings MultiplyMinimumPixelsPerTask(int multiplier)
        {
            Guard.MustBeGreaterThan(multiplier, 0, nameof(multiplier));

            return new ParallelExecutionSettings(
                this.MaxDegreeOfParallelism,
                this.MinimumPixelsProcessedPerTask * multiplier,
                this.MemoryAllocator);
        }

        /// <summary>
        /// Get the default <see cref="SixLabors.ImageSharp.Advanced.ParallelExecutionSettings"/> for a <see cref="SixLabors.ImageSharp.Configuration"/>
        /// </summary>
        /// <param name="configuration">The <see cref="Configuration"/>.</param>
        /// <returns>The <see cref="ParallelExecutionSettings"/>.</returns>
        public static ParallelExecutionSettings FromConfiguration(Configuration configuration)
        {
            return new ParallelExecutionSettings(configuration.MaxDegreeOfParallelism, configuration.MemoryAllocator);
        }
    }
}
