// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Diagnostics;

namespace SixLabors.ImageSharp.Memory.Internals;

/// <summary>
/// Implements reference counting lifetime guard mechanism for memory resources
/// and maintains the value of <see cref="MemoryDiagnostics.TotalUndisposedAllocationCount"/>.
/// </summary>
internal abstract class RefCountedMemoryLifetimeGuard : IDisposable
{
    private AllocationTrackingState allocationTracking;
    private int refCount = 1;
    private int disposed;
    private int released;
    private string? allocationStackTrace;

    protected RefCountedMemoryLifetimeGuard()
    {
        if (MemoryDiagnostics.UndisposedAllocationSubscribed)
        {
            this.allocationStackTrace = Environment.StackTrace;
        }

        MemoryDiagnostics.IncrementTotalUndisposedAllocationCount();
    }

    ~RefCountedMemoryLifetimeGuard()
    {
        Interlocked.Exchange(ref this.disposed, 1);
        this.ReleaseRef(true);
    }

    public bool IsDisposed => this.disposed == 1;

    public void AddRef() => Interlocked.Increment(ref this.refCount);

    public void ReleaseRef() => this.ReleaseRef(false);

    /// <summary>
    /// Attaches allocator reservation tracking to this lifetime guard.
    /// </summary>
    /// <param name="allocator">The allocator that owns the reservation.</param>
    /// <param name="lengthInBytes">The reserved allocation size, in bytes.</param>
    public void AttachAllocationTracking(MemoryAllocator allocator, long lengthInBytes)
        => this.allocationTracking.Attach(allocator, lengthInBytes);

    public void Dispose()
    {
        int wasDisposed = Interlocked.Exchange(ref this.disposed, 1);
        if (wasDisposed == 0)
        {
            this.ReleaseRef();
            GC.SuppressFinalize(this);
        }
    }

    protected abstract void Release();

    private void ReleaseRef(bool finalizing)
    {
        Interlocked.Decrement(ref this.refCount);
        if (this.refCount == 0)
        {
            int wasReleased = Interlocked.Exchange(ref this.released, 1);

            if (wasReleased == 0)
            {
                if (!finalizing)
                {
                    MemoryDiagnostics.DecrementTotalUndisposedAllocationCount();
                }
                else if (this.allocationStackTrace != null)
                {
                    MemoryDiagnostics.RaiseUndisposedMemoryResource(this.allocationStackTrace);
                }

                this.Release();

                // Guard-backed resources can be recovered by finalization, so their allocator
                // reservation must follow the guard's actual release point instead of the owner object.
                this.allocationTracking.Release();
            }
        }
    }
}
