// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Memory;

/// <summary>
/// Defines options for creating the default <see cref="MemoryAllocator"/>.
/// </summary>
public struct MemoryAllocatorOptions
{
    private int? maximumPoolSizeMegabytes;
    private int? allocationLimitMegabytes;
    private int? accumulativeAllocationLimitMegabytes;

    /// <summary>
    /// Gets or sets a value defining the maximum size of the <see cref="MemoryAllocator"/>'s internal memory pool
    /// in Megabytes. <see langword="null"/> means platform default.
    /// </summary>
    public int? MaximumPoolSizeMegabytes
    {
        readonly get => this.maximumPoolSizeMegabytes;
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
    /// Gets or sets a value defining the maximum (discontiguous) buffer size that can be allocated by the allocator in Megabytes.
    /// <see langword="null"/> means platform default: 1GB on 32-bit processes, 4GB on 64-bit processes.
    /// </summary>
    public int? AllocationLimitMegabytes
    {
        readonly get => this.allocationLimitMegabytes;
        set
        {
            if (value.HasValue)
            {
                Guard.MustBeGreaterThan(value.Value, 0, nameof(this.AllocationLimitMegabytes));
                if (this.AccumulativeAllocationLimitMegabytes.HasValue)
                {
                    Guard.MustBeLessThanOrEqualTo(
                        value.Value,
                        this.AccumulativeAllocationLimitMegabytes.Value,
                        nameof(this.AllocationLimitMegabytes));
                }
            }

            this.allocationLimitMegabytes = value;
        }
    }

    /// <summary>
    /// Gets or sets a value defining the maximum accumulative size, in Megabytes, of all active allocations made
    /// through the created <see cref="MemoryAllocator"/> instance.
    /// <see langword="null"/> (the default) imposes no limit on the accumulative total.
    /// </summary>
    public int? AccumulativeAllocationLimitMegabytes
    {
        readonly get => this.accumulativeAllocationLimitMegabytes;
        set
        {
            if (value.HasValue)
            {
                Guard.MustBeGreaterThan(value.Value, 0, nameof(this.AccumulativeAllocationLimitMegabytes));
                if (this.AllocationLimitMegabytes.HasValue)
                {
                    Guard.MustBeGreaterThanOrEqualTo(
                        value.Value,
                        this.AllocationLimitMegabytes.Value,
                        nameof(this.AccumulativeAllocationLimitMegabytes));
                }
            }

            this.accumulativeAllocationLimitMegabytes = value;
        }
    }
}
