// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Contains common factory methods and configuration constants.
    /// </summary>
    public partial class ArrayPoolMemoryAllocator
    {
        /// <summary>
        /// The default value for: maximum size of pooled arrays in bytes.
        /// Currently set to 24MB, which is equivalent to 8 megapixels of raw RGBA32 data.
        /// </summary>
        internal const int DefaultMaxPooledBufferSizeInBytes = 24 * 1024 * 1024;

        /// <summary>
        /// The value for: The threshold to pool arrays in <see cref="largeArrayPool"/> which
        /// has less buckets for memory safety.
        /// This matches the upper pooling limit of <see cref="ArrayPool{T}.Shared"/> where T is byte.
        /// </summary>
        private const int SharedPoolThresholdInBytes = 1024 * 1024;

        /// <summary>
        /// The default bucket count for <see cref="largeArrayPool"/>.
        /// </summary>
        private const int DefaultLargePoolBucketCount = 8;

        // TODO: This value should be determined by benchmarking
        // Perhaps base it on the number of cores?
        private const int DefaultBufferCapacityInBytes = int.MaxValue / 4;

        /// <summary>
        /// This is the default. Should be good for most use cases.
        /// </summary>
        /// <returns>The memory manager.</returns>
        public static ArrayPoolMemoryAllocator CreateDefault()
            => new ArrayPoolMemoryAllocator(maxPoolSizeInBytes: DefaultMaxPooledBufferSizeInBytes);

        /// <summary>
        /// For environments with very limited memory capabilities,
        /// only small buffers like image rows are pooled.
        /// </summary>
        /// <returns>The memory manager.</returns>
        public static ArrayPoolMemoryAllocator CreateWithMinimalPooling()
            => new ArrayPoolMemoryAllocator(
                maxArrayLengthInBytes: SharedPoolThresholdInBytes,
                maxArraysPerBucket: 1,
                maxContiguousArrayLengthInBytes: DefaultBufferCapacityInBytes);

        /// <summary>
        /// For environments with limited memory capabilities,
        /// only small array requests are pooled, which can result in reduced throughput.
        /// </summary>
        /// <returns>The memory manager.</returns>
        public static ArrayPoolMemoryAllocator CreateWithModeratePooling()
            => new ArrayPoolMemoryAllocator(
                maxArrayLengthInBytes: 2 * 1024 * 1024,
                maxArraysPerBucket: 16,
                maxContiguousArrayLengthInBytes: DefaultBufferCapacityInBytes);

        /// <summary>
        /// For environments where memory capabilities are not an issue,
        /// the maximum amount of array requests are pooled which results in optimal throughput.
        /// </summary>
        /// <returns>The memory manager.</returns>
        public static ArrayPoolMemoryAllocator CreateWithAggressivePooling()
            => new ArrayPoolMemoryAllocator(
                maxArrayLengthInBytes: 128 * 1024 * 1024,
                maxArraysPerBucket: 16,
                maxContiguousArrayLengthInBytes: DefaultBufferCapacityInBytes);
    }
}
