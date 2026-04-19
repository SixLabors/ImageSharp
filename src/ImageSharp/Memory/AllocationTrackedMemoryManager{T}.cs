// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;

namespace SixLabors.ImageSharp.Memory;

/// <summary>
/// Provides the tracked memory-owner contract required by <see cref="MemoryAllocator"/>.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <remarks>
/// Custom allocators implement <see cref="MemoryAllocator.AllocateCore{T}(int, AllocationOptions)"/>
/// and return a derived type. The base allocator attaches allocation tracking after the owner has been
/// created so custom implementations cannot forget, duplicate, or mismatch the reservation lifecycle.
/// </remarks>
public abstract class AllocationTrackedMemoryManager<T> : MemoryManager<T>
    where T : struct
{
    private MemoryAllocator? trackingAllocator;
    private long trackingLengthInBytes;
    private int trackingReleased;

    /// <summary>
    /// Releases resources held by the concrete tracked owner.
    /// </summary>
    /// <param name="disposing">
    /// <see langword="true"/> when the owner is being disposed deterministically;
    /// otherwise, <see langword="false"/>.
    /// </param>
    /// <remarks>
    /// Implementations release their own resources here. Allocation tracking is released by the sealed base
    /// dispose path after this method returns.
    /// </remarks>
    protected abstract void DisposeCore(bool disposing);

    /// <inheritdoc />
    protected sealed override void Dispose(bool disposing)
    {
        this.DisposeCore(disposing);
        this.ReleaseAllocationTracking();
    }

    /// <summary>
    /// Attaches allocation tracking to this owner after allocation has succeeded.
    /// </summary>
    /// <param name="allocator">The allocator that owns the reservation for this instance.</param>
    /// <param name="lengthInBytes">The reserved allocation size, in bytes.</param>
    /// <remarks>
    /// <see cref="MemoryAllocator"/> calls this exactly once after <c>AllocateCore</c> returns.
    /// Derived allocators should not call it themselves; they only construct the concrete owner.
    /// </remarks>
    internal void AttachAllocationTracking(MemoryAllocator allocator, long lengthInBytes)
    {
        this.trackingAllocator = allocator;
        this.trackingLengthInBytes = lengthInBytes;
    }

    /// <summary>
    /// Releases any tracked allocation bytes associated with this instance.
    /// </summary>
    /// <remarks>
    /// Calling this more than once is safe; only the first call after tracking has been attached releases bytes.
    /// </remarks>
    private void ReleaseAllocationTracking()
    {
        if (Interlocked.Exchange(ref this.trackingReleased, 1) == 0 && this.trackingAllocator != null)
        {
            this.trackingAllocator.ReleaseAccumulatedBytes(this.trackingLengthInBytes);
            this.trackingAllocator = null;
        }
    }
}
