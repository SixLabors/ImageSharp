// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Defines options for creating the default <see cref="MemoryAllocator"/>.
    /// </summary>
    public struct MemoryAllocatorOptions
    {
        private int? maximumPoolSizeMegabytes;

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
    }
}
