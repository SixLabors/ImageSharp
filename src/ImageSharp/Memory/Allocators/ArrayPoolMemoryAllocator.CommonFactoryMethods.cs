// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Contains common factory methods and configuration constants.
    /// </summary>
    public partial class ArrayPoolMemoryAllocator
    {
        /// <summary>
        /// This is the default. Should be good for most use cases.
        /// </summary>
        /// <returns>The memory manager.</returns>
        public static ArrayPoolMemoryAllocator CreateDefault()
            => new ArrayPoolMemoryAllocator(maxPooledArrayLengthInBytes: DefaultMaxArrayLengthInBytes);

        /// <summary>
        /// For environments with very limited memory capabilities,
        /// only small buffers like image rows are pooled.
        /// </summary>
        /// <returns>The memory manager.</returns>
        public static ArrayPoolMemoryAllocator CreateWithMinimalPooling()
            => new ArrayPoolMemoryAllocator(
                maxPooledArrayLengthInBytes: SharedPoolThresholdInBytes,
                maxArraysPerPoolBucket: 1,
                maxContiguousArrayLengthInBytes: DefaultMaxContiguousArrayLengthInBytes);

        /// <summary>
        /// For environments with limited memory capabilities,
        /// only small array requests are pooled, which can result in reduced throughput.
        /// </summary>
        /// <returns>The memory manager.</returns>
        public static ArrayPoolMemoryAllocator CreateWithModeratePooling()
            => new ArrayPoolMemoryAllocator(
                maxPooledArrayLengthInBytes: 2 * 1024 * 1024,
                maxArraysPerPoolBucket: 16,
                maxContiguousArrayLengthInBytes: DefaultMaxContiguousArrayLengthInBytes);

        /// <summary>
        /// For environments where memory capabilities are not an issue,
        /// the maximum amount of array requests are pooled which results in optimal throughput.
        /// </summary>
        /// <returns>The memory manager.</returns>
        public static ArrayPoolMemoryAllocator CreateWithAggressivePooling()
            => new ArrayPoolMemoryAllocator(
                maxPooledArrayLengthInBytes: 128 * 1024 * 1024,
                maxArraysPerPoolBucket: 16,
                maxContiguousArrayLengthInBytes: DefaultMaxContiguousArrayLengthInBytes);
    }
}
