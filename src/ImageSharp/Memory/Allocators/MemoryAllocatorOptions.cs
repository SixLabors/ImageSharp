// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Defines options for creating the default <see cref="MemoryAllocator"/>.
    /// </summary>
    public class MemoryAllocatorOptions
    {
        private int? maximumPoolSizeMegabytes;
        private int? minimumContiguousBlockSizeBytes;

        /// <summary>
        /// Gets or sets a value defining the maximum size of the <see cref="MemoryAllocator"/>'s internal memory pool
        /// in Megabytes. <see langword="null"/> means platform default.
        /// </summary>
        public int? MaximumPoolSizeMegabytes
        {
            get => this.maximumPoolSizeMegabytes;
            set
            {
                if (value.HasValue)
                {
                    Guard.MustBeGreaterThanOrEqualTo(value.Value, 0, nameof(this.MaximumPoolSizeMegabytes));
                }

                this.maximumPoolSizeMegabytes = value;
            }
        }

        /// <summary>
        /// Gets or sets a value defining the minimum contiguous block size when allocating buffers for
        /// <see cref="MemoryGroup{T}"/>, <see cref="Buffer2D{T}"/> or <see cref="Image{TPixel}"/>.
        /// <see langword="null"/> means platform default.
        /// </summary>
        /// <remarks>
        /// Overriding this value is useful for interop scenarios
        /// ensuring <see cref="Image{TPixel}.TryGetSinglePixelSpan"/> succeeds.
        /// </remarks>
        public int? MinimumContiguousBlockSizeBytes
        {
            get => this.minimumContiguousBlockSizeBytes;
            set
            {
                if (value.HasValue)
                {
                    // It doesn't make sense to set this to small values in practice.
                    // Defining an arbitrary minimum of 65536.
                    Guard.MustBeGreaterThanOrEqualTo(value.Value, 65536, nameof(this.MaximumPoolSizeMegabytes));
                }

                this.minimumContiguousBlockSizeBytes = value;
            }
        }
    }
}
