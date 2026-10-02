// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Tests.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif;

/// <summary>
/// Counts the allocations of an encode and cancels a token at a chosen allocation, so a test cancels at the same point
/// of the work on every run.
/// </summary>
internal sealed class CancellingMemoryAllocator : TestMemoryAllocator
{
    /// <summary>
    /// The source to cancel, or <see langword="null"/> while the allocator only counts.
    /// </summary>
    private CancellationTokenSource source;

    /// <summary>
    /// The number of allocations since the last call to <see cref="CancelAt"/> or <see cref="ResetCount"/>.
    /// </summary>
    private int allocationCount;

    /// <summary>
    /// The allocation at which the source is canceled.
    /// </summary>
    private int cancelAllocation;

    /// <summary>
    /// Gets the number of allocations since the last call to <see cref="CancelAt"/> or <see cref="ResetCount"/>.
    /// </summary>
    public int AllocationCount => Volatile.Read(ref this.allocationCount);

    /// <summary>
    /// Restarts the count without canceling anything.
    /// </summary>
    public void ResetCount()
    {
        this.source = null;
        this.allocationCount = 0;
    }

    /// <summary>
    /// Restarts the count and cancels a source when it reaches an allocation.
    /// </summary>
    /// <param name="cancellationSource">The source to cancel.</param>
    /// <param name="allocation">The one-based allocation at which to cancel.</param>
    public void CancelAt(CancellationTokenSource cancellationSource, int allocation)
    {
        this.allocationCount = 0;
        this.cancelAllocation = allocation;
        this.source = cancellationSource;
    }

    /// <inheritdoc/>
    protected override AllocationTrackedMemoryManager<T> AllocateCore<T>(int length, AllocationOptions options = AllocationOptions.None)
    {
        // The atomic count keeps exactly one allocation matching, even if allocations come from several threads.
        if (Interlocked.Increment(ref this.allocationCount) == this.cancelAllocation)
        {
            this.source?.Cancel();
        }

        return base.AllocateCore<T>(length, options);
    }
}
