// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Memory;

/// <summary>
/// Memory managers are used to allocate memory for image processing operations.
/// </summary>
public abstract class MemoryAllocator
{
    private const int OneGigabyte = 1 << 30;
    private long accumulativeAllocatedBytes;
    private int trackingSuppressionCount;

    /// <summary>
    /// Gets the default platform-specific global <see cref="MemoryAllocator"/> instance that
    /// serves as the default value for <see cref="Configuration.MemoryAllocator"/>.
    /// <para />
    /// This is a get-only property,
    /// you should set <see cref="Configuration.Default"/>'s <see cref="Configuration.MemoryAllocator"/>
    /// to change the default allocator used by <see cref="Image"/> and it's operations.
    /// </summary>
    public static MemoryAllocator Default { get; } = Create();

    /// <summary>
    /// Gets the maximum number of bytes that can be allocated by a memory group.
    /// </summary>
    /// <remarks>
    /// The allocation limit is determined by the process architecture: 4 GB for 64-bit processes and
    /// 1 GB for 32-bit processes.
    /// </remarks>
    internal long MemoryGroupAllocationLimitBytes { get; private protected set; } = Environment.Is64BitProcess ? 4L * OneGigabyte : OneGigabyte;

    /// <summary>
    /// Gets the maximum allowed total allocation size, in bytes, for the current process.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="long.MaxValue"/>, effectively imposing no limit on total allocations.
    /// This property can be set to enforce a cap on total memory usage across all allocations made through this allocator instance, providing
    /// a safeguard against excessive memory consumption.<br/>
    /// When the cumulative size of active allocations exceeds this limit, an <see cref="InvalidMemoryOperationException"/> will be thrown to
    /// prevent further allocations and signal that the limit has been breached.
    /// </remarks>
    internal long AccumulativeAllocationLimitBytes { get; private protected set; } = long.MaxValue;

    /// <summary>
    /// Gets the maximum size, in bytes, that can be allocated for a single buffer.
    /// </summary>
    /// <remarks>
    /// The single buffer allocation limit is set to 1 GB by default.
    /// </remarks>
    internal int SingleBufferAllocationLimitBytes { get; private protected set; } = OneGigabyte;

    /// <summary>
    /// Gets a value indicating whether accumulative allocation tracking is currently suppressed for this instance.
    /// </summary>
    /// <remarks>
    /// This is used internally when an outer allocator or memory group reservation already owns the tracked bytes
    /// and nested allocations must not reserve or release them a second time.
    /// </remarks>
    private bool IsTrackingSuppressed => Volatile.Read(ref this.trackingSuppressionCount) > 0;

    /// <summary>
    /// Gets the length of the largest contiguous buffer that can be handled by this allocator instance in bytes.
    /// </summary>
    /// <returns>The length of the largest contiguous buffer that can be handled by this allocator instance.</returns>
    protected internal abstract int GetBufferCapacityInBytes();

    /// <summary>
    /// Creates a default instance of a <see cref="MemoryAllocator"/> optimized for the executing platform.
    /// </summary>
    /// <returns>The <see cref="MemoryAllocator"/>.</returns>
    public static MemoryAllocator Create() => Create(default);

    /// <summary>
    /// Creates the default <see cref="MemoryAllocator"/> using the provided options.
    /// </summary>
    /// <param name="options">The <see cref="MemoryAllocatorOptions"/>.</param>
    /// <returns>The <see cref="MemoryAllocator"/>.</returns>
    public static MemoryAllocator Create(MemoryAllocatorOptions options)
    {
        UniformUnmanagedMemoryPoolMemoryAllocator allocator = new(options.MaximumPoolSizeMegabytes);
        if (options.AllocationLimitMegabytes.HasValue)
        {
            allocator.MemoryGroupAllocationLimitBytes = options.AllocationLimitMegabytes.Value * 1024L * 1024L;
            allocator.SingleBufferAllocationLimitBytes = (int)Math.Min(allocator.SingleBufferAllocationLimitBytes, allocator.MemoryGroupAllocationLimitBytes);
        }

        if (options.AccumulativeAllocationLimitMegabytes.HasValue)
        {
            allocator.AccumulativeAllocationLimitBytes = options.AccumulativeAllocationLimitMegabytes.Value * 1024L * 1024L;
        }

        return allocator;
    }

    /// <summary>
    /// Allocates an <see cref="IMemoryOwner{T}" />, holding a <see cref="Memory{T}"/> of length <paramref name="length"/>.
    /// </summary>
    /// <typeparam name="T">Type of the data stored in the buffer.</typeparam>
    /// <param name="length">Size of the buffer to allocate.</param>
    /// <param name="options">The allocation options.</param>
    /// <returns>A buffer of values of type <typeparamref name="T"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">When length is negative.</exception>
    /// <exception cref="InvalidMemoryOperationException">When length is over the capacity of the allocator.</exception>
    public IMemoryOwner<T> Allocate<T>(int length, AllocationOptions options = AllocationOptions.None)
        where T : struct
    {
        if (length < 0)
        {
            InvalidMemoryOperationException.ThrowNegativeAllocationException(length);
        }

        ulong lengthInBytes = (ulong)length * (ulong)Unsafe.SizeOf<T>();
        if (lengthInBytes > (ulong)this.SingleBufferAllocationLimitBytes)
        {
            InvalidMemoryOperationException.ThrowAllocationOverLimitException(lengthInBytes, this.SingleBufferAllocationLimitBytes);
        }

        long lengthInBytesLong = (long)lengthInBytes;
        bool shouldTrack = !this.IsTrackingSuppressed && lengthInBytesLong != 0;
        if (shouldTrack)
        {
            this.ReserveAllocation(lengthInBytesLong);
        }

        try
        {
            AllocationTrackedMemoryManager<T> owner = this.AllocateCore<T>(length, options);
            if (shouldTrack)
            {
                owner.AttachAllocationTracking(this, lengthInBytesLong);
            }

            return owner;
        }
        catch
        {
            if (shouldTrack)
            {
                this.ReleaseAccumulatedBytes(lengthInBytesLong);
            }

            throw;
        }
    }

    /// <summary>
    /// Allocates a tracked memory owner for <see cref="Allocate{T}(int, AllocationOptions)"/>.
    /// </summary>
    /// <typeparam name="T">Type of the data stored in the buffer.</typeparam>
    /// <param name="length">Size of the buffer to allocate.</param>
    /// <param name="options">The allocation options.</param>
    /// <returns>A tracked memory owner of values of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// Implementations should only allocate and initialize the concrete owner. The base allocator
    /// reserves bytes, attaches tracking to the returned owner, and releases the reservation if allocation fails.
    /// </remarks>
    protected abstract AllocationTrackedMemoryManager<T> AllocateCore<T>(int length, AllocationOptions options = AllocationOptions.None)
        where T : struct;

    /// <summary>
    /// Releases all retained resources not being in use.
    /// Eg: by resetting array pools and letting GC to free the arrays.
    /// </summary>
    /// <remarks>
    /// This does not dispose active allocations; callers are responsible for disposing all
    /// <see cref="IMemoryOwner{T}"/> instances to release memory.
    /// </remarks>
    public virtual void ReleaseRetainedResources()
    {
    }

    /// <summary>
    /// Allocates a <see cref="MemoryGroup{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of element to allocate.</typeparam>
    /// <param name="totalLength">The total length of the buffer.</param>
    /// <param name="bufferAlignment">The expected alignment (eg. to make sure image rows fit into single buffers).</param>
    /// <param name="options">The <see cref="AllocationOptions"/>.</param>
    /// <returns>A new <see cref="MemoryGroup{T}"/>.</returns>
    /// <exception cref="InvalidMemoryOperationException">Thrown when 'blockAlignment' converted to bytes is greater than the buffer capacity of the allocator.</exception>
    internal MemoryGroup<T> AllocateGroup<T>(
        long totalLength,
        int bufferAlignment,
        AllocationOptions options = AllocationOptions.None)
        where T : struct
    {
        if (totalLength < 0)
        {
            InvalidMemoryOperationException.ThrowNegativeAllocationException(totalLength);
        }

        ulong totalLengthInBytes = (ulong)totalLength * (ulong)Unsafe.SizeOf<T>();
        if (totalLengthInBytes > (ulong)this.MemoryGroupAllocationLimitBytes)
        {
            InvalidMemoryOperationException.ThrowAllocationOverLimitException(totalLengthInBytes, this.MemoryGroupAllocationLimitBytes);
        }

        long totalLengthInBytesLong = (long)totalLengthInBytes;
        bool shouldTrack = !this.IsTrackingSuppressed && totalLengthInBytesLong != 0;
        if (shouldTrack)
        {
            this.ReserveAllocation(totalLengthInBytesLong);
        }

        using (this.SuppressTracking())
        {
            try
            {
                MemoryGroup<T> group = this.AllocateGroupCore<T>(totalLength, totalLengthInBytesLong, bufferAlignment, options);
                if (shouldTrack)
                {
                    group.AttachAllocationTracking(this, totalLengthInBytesLong);
                }

                return group;
            }
            catch
            {
                if (shouldTrack)
                {
                    this.ReleaseAccumulatedBytes(totalLengthInBytesLong);
                }

                throw;
            }
        }
    }

    internal virtual MemoryGroup<T> AllocateGroupCore<T>(long totalLengthInElements, long totalLengthInBytes, int bufferAlignment, AllocationOptions options)
        where T : struct
        => MemoryGroup<T>.Allocate(this, totalLengthInElements, bufferAlignment, options);

    /// <summary>
    /// Allocates a single segment for <see cref="MemoryGroup{T}"/> construction.
    /// </summary>
    /// <typeparam name="T">Type of the data stored in the buffer.</typeparam>
    /// <param name="length">Size of the segment to allocate.</param>
    /// <param name="options">The allocation options.</param>
    /// <returns>A segment owner for the requested buffer length.</returns>
    /// <remarks>
    /// The default implementation uses <see cref="Allocate{T}(int, AllocationOptions)"/>. Built-in allocators
    /// can override this to supply raw segment owners when group construction must bypass nested tracking.
    /// </remarks>
    internal virtual IMemoryOwner<T> AllocateGroupBuffer<T>(int length, AllocationOptions options = AllocationOptions.None)
        where T : struct
        => this.Allocate<T>(length, options);

    /// <summary>
    /// Reserves accumulative allocation bytes before creating the underlying buffer.
    /// </summary>
    /// <param name="lengthInBytes">The number of bytes to reserve.</param>
    private void ReserveAllocation(long lengthInBytes)
    {
        if (this.IsTrackingSuppressed || lengthInBytes <= 0)
        {
            return;
        }

        long total = Interlocked.Add(ref this.accumulativeAllocatedBytes, lengthInBytes);
        if (total > this.AccumulativeAllocationLimitBytes)
        {
            _ = Interlocked.Add(ref this.accumulativeAllocatedBytes, -lengthInBytes);
            InvalidMemoryOperationException.ThrowAllocationOverLimitException((ulong)lengthInBytes, this.AccumulativeAllocationLimitBytes);
        }
    }

    /// <summary>
    /// Releases accumulative allocation bytes previously tracked by this allocator.
    /// </summary>
    /// <param name="lengthInBytes">The number of bytes to release.</param>
    internal void ReleaseAccumulatedBytes(long lengthInBytes)
    {
        if (lengthInBytes <= 0)
        {
            return;
        }

        _ = Interlocked.Add(ref this.accumulativeAllocatedBytes, -lengthInBytes);
    }

    /// <summary>
    /// Suppresses accumulative allocation tracking for the lifetime of the returned scope.
    /// </summary>
    /// <returns>A scope that restores tracking when disposed.</returns>
    /// <remarks>
    /// Returning the concrete scope type keeps nested allocator calls allocation-free on the hot path
    /// while preserving the same using-pattern at call sites.
    /// </remarks>
    private TrackingSuppressionScope SuppressTracking() => new(this);

    /// <summary>
    /// Temporarily suppresses accumulative allocation tracking within a scope.
    /// </summary>
    private struct TrackingSuppressionScope : IDisposable
    {
        private MemoryAllocator? allocator;

        public TrackingSuppressionScope(MemoryAllocator allocator)
        {
            this.allocator = allocator;
            _ = Interlocked.Increment(ref allocator.trackingSuppressionCount);
        }

        public void Dispose()
        {
            if (this.allocator != null)
            {
                _ = Interlocked.Decrement(ref this.allocator.trackingSuppressionCount);
                this.allocator = null;
            }
        }
    }
}
