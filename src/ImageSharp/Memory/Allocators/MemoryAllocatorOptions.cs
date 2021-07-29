// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Defines options for creating the default <see cref="MemoryAllocator"/>.
    /// </summary>
    public class MemoryAllocatorOptions
    {
        /// <summary>
        /// Gets or sets a value defining the maximum size of the pool in Megabytes, null means platform default.
        /// </summary>
        public int? MaxPoolSizeMegabytes { get; set; }

        /// <summary>
        /// Gets or sets a value defining the minimum contiguous block size, null means platform default.
        /// </summary>
        public int? MinimumContiguousBlockBytes { get; set; }
    }
}
