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
    /// The allocation limit is determined based on the process architecture. For 64-bit processes,
    /// the limit is higher than for 32-bit processes.
    /// </remarks>
    internal long AccumulativeAllocationLimitBytes { get; private protected set; } = Environment.Is64BitProcess ? 8L * OneGigabyte : 2L * OneGigabyte;

    /// <summary>
    /// Gets the maximum size, in bytes, that can be allocated for a single buffer.
    /// </summary>
    /// <remarks>
    /// The single buffer allocation limit is set to 1 GB by default.
    /// </remarks>
    internal int SingleBufferAllocationLimitBytes { get; private protected set; } = OneGigabyte;

    /// <summary>
    /// Gets a value indicating whether change tracking is currently suppressed for this instance.
    /// </summary>
    /// <remarks>
    /// When change tracking is suppressed, modifications to the object will not be recorded or
    /// trigger change notifications. This property is used internally to temporarily disable tracking during
    /// batch updates or initialization.
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
    /// <exception cref="ArgumentOutOfRangeException">When length is zero or negative.</exception>
    /// <exception cref="InvalidMemoryOperationException">When length is over the capacity of the allocator.</exception>
    public abstract IMemoryOwner<T> Allocate<T>(int length, AllocationOptions options = AllocationOptions.None)
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
        this.ReserveAllocation(totalLengthInBytesLong);

        using (this.SuppressTracking())
        {
            try
            {
                MemoryGroup<T> group = this.AllocateGroupCore<T>(totalLength, totalLengthInBytesLong, bufferAlignment, options);
                group.SetAllocationTracking(this, totalLengthInBytesLong);
                return group;
            }
            catch
            {
                this.ReleaseAccumulatedBytes(totalLengthInBytesLong);
                throw;
            }
        }
    }

    internal virtual MemoryGroup<T> AllocateGroupCore<T>(long totalLengthInElements, long totalLengthInBytes, int bufferAlignment, AllocationOptions options)
        where T : struct
        => MemoryGroup<T>.Allocate(this, totalLengthInElements, bufferAlignment, options);

    /// <summary>
    /// Tracks the allocation of an <see cref="IMemoryOwner{T}" /> instance after reserving bytes.
    /// </summary>
    /// <typeparam name="T">Type of the data stored in the buffer.</typeparam>
    /// <param name="owner">The allocation to track.</param>
    /// <param name="lengthInBytes">The allocation size in bytes.</param>
    /// <returns>The tracked allocation.</returns>
    protected IMemoryOwner<T> TrackAllocation<T>(IMemoryOwner<T> owner, ulong lengthInBytes)
        where T : struct
    {
        if (this.IsTrackingSuppressed || lengthInBytes == 0)
        {
            return owner;
        }

        return new TrackingMemoryOwner<T>(owner, this, (long)lengthInBytes);
    }

    /// <summary>
    /// Reserves accumulative allocation bytes before creating the underlying buffer.
    /// </summary>
    /// <param name="lengthInBytes">The number of bytes to reserve.</param>
    protected void ReserveAllocation(long lengthInBytes)
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
    /// <returns>An <see cref="IDisposable"/> that restores tracking when disposed.</returns>
    internal IDisposable SuppressTracking() => new TrackingSuppressionScope(this);

    /// <summary>
    /// Temporarily suppresses accumulative allocation tracking within a scope.
    /// </summary>
    private sealed class TrackingSuppressionScope : IDisposable
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

    /// <summary>
    /// Wraps an <see cref="IMemoryOwner{T}"/> to release accumulative tracking on dispose.
    /// </summary>
    private sealed class TrackingMemoryOwner<T> : IMemoryOwner<T>
        where T : struct
    {
        private IMemoryOwner<T>? owner;
        private readonly MemoryAllocator allocator;
        private readonly long lengthInBytes;

        public TrackingMemoryOwner(IMemoryOwner<T> owner, MemoryAllocator allocator, long lengthInBytes)
        {
            this.owner = owner;
            this.allocator = allocator;
            this.lengthInBytes = lengthInBytes;
        }

        public Memory<T> Memory => this.owner?.Memory ?? Memory<T>.Empty;

        public void Dispose()
        {
            // Ensure only one caller disposes the inner owner and releases the reservation.
            IMemoryOwner<T>? inner = Interlocked.Exchange(ref this.owner, null);
            if (inner != null)
            {
                inner.Dispose();
                this.allocator.ReleaseAccumulatedBytes(this.lengthInBytes);
            }
        }
    }
}
