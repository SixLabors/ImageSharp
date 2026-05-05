// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Memory;

/// <summary>
/// Tracks a single allocator reservation and releases it exactly once.
/// </summary>
/// <remarks>
/// This type is intended to live as a mutable field on the owning object. It should not be copied
/// after tracking has been attached, because the owner relies on a single shared release state.
/// </remarks>
internal struct AllocationTrackingState
{
    private MemoryAllocator? allocator;
    private long lengthInBytes;
    private int released;

    /// <summary>
    /// Attaches allocator reservation tracking to the current owner.
    /// </summary>
    /// <param name="allocator">The allocator that owns the reservation.</param>
    /// <param name="lengthInBytes">The reserved allocation size, in bytes.</param>
    internal void Attach(MemoryAllocator allocator, long lengthInBytes)
    {
        this.allocator = allocator;
        this.lengthInBytes = lengthInBytes;
    }

    /// <summary>
    /// Releases the attached allocator reservation once.
    /// </summary>
    internal void Release()
    {
        if (Interlocked.Exchange(ref this.released, 1) == 0 && this.allocator != null)
        {
            this.allocator.ReleaseAccumulatedBytes(this.lengthInBytes);
            this.allocator = null;
        }
    }
}
