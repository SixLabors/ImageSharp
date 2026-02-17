// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory.Internals;

namespace SixLabors.ImageSharp.Memory;

internal sealed class UniformUnmanagedMemoryPoolMemoryAllocator : MemoryAllocator
{
    private const int OneMegabyte = 1 << 20;

    // 4 MB seemed to perform slightly better in benchmarks than 2MB or higher values:
    private const int DefaultContiguousPoolBlockSizeBytes = 4 * OneMegabyte;
    private const int DefaultNonPoolBlockSizeBytes = 32 * OneMegabyte;
    private readonly int sharedArrayPoolThresholdInBytes;
    private readonly int poolBufferSizeInBytes;
    private readonly int poolCapacity;
    private readonly UniformUnmanagedMemoryPool.TrimSettings trimSettings;

    private readonly UniformUnmanagedMemoryPool pool;
    private readonly UnmanagedMemoryAllocator nonPoolAllocator;

    public UniformUnmanagedMemoryPoolMemoryAllocator(int? maxPoolSizeMegabytes)
        : this(
            DefaultContiguousPoolBlockSizeBytes,
            maxPoolSizeMegabytes.HasValue ? (long)maxPoolSizeMegabytes.Value * OneMegabyte : GetDefaultMaxPoolSizeBytes(),
            DefaultNonPoolBlockSizeBytes)
    {
    }

    public UniformUnmanagedMemoryPoolMemoryAllocator(
        int poolBufferSizeInBytes,
        long maxPoolSizeInBytes,
        int unmanagedBufferSizeInBytes)
        : this(
            OneMegabyte,
            poolBufferSizeInBytes,
            maxPoolSizeInBytes,
            unmanagedBufferSizeInBytes)
    {
    }

    internal UniformUnmanagedMemoryPoolMemoryAllocator(
        int sharedArrayPoolThresholdInBytes,
        int poolBufferSizeInBytes,
        long maxPoolSizeInBytes,
        int unmanagedBufferSizeInBytes)
        : this(
            sharedArrayPoolThresholdInBytes,
            poolBufferSizeInBytes,
            maxPoolSizeInBytes,
            unmanagedBufferSizeInBytes,
            UniformUnmanagedMemoryPool.TrimSettings.Default)
    {
    }

    internal UniformUnmanagedMemoryPoolMemoryAllocator(
        int sharedArrayPoolThresholdInBytes,
        int poolBufferSizeInBytes,
        long maxPoolSizeInBytes,
        int unmanagedBufferSizeInBytes,
        UniformUnmanagedMemoryPool.TrimSettings trimSettings)
    {
        this.sharedArrayPoolThresholdInBytes = sharedArrayPoolThresholdInBytes;
        this.poolBufferSizeInBytes = poolBufferSizeInBytes;
        this.poolCapacity = (int)(maxPoolSizeInBytes / poolBufferSizeInBytes);
        this.trimSettings = trimSettings;
        this.pool = new UniformUnmanagedMemoryPool(this.poolBufferSizeInBytes, this.poolCapacity, this.trimSettings);
        this.nonPoolAllocator = new UnmanagedMemoryAllocator(unmanagedBufferSizeInBytes);
    }

    /// <inheritdoc />
    protected internal override int GetBufferCapacityInBytes() => this.poolBufferSizeInBytes;

    /// <inheritdoc />
    public override IMemoryOwner<T> Allocate<T>(
        int length,
        AllocationOptions options = AllocationOptions.None)
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

        if (lengthInBytes <= (ulong)this.sharedArrayPoolThresholdInBytes)
        {
            SharedArrayPoolBuffer<T> buffer = new(length);
            if (options.Has(AllocationOptions.Clean))
            {
                buffer.GetSpan().Clear();
            }

            return buffer;
        }

        if (lengthInBytes <= (ulong)this.poolBufferSizeInBytes)
        {
            UnmanagedMemoryHandle mem = this.pool.Rent();
            if (mem.IsValid)
            {
                UnmanagedBuffer<T> buffer = this.pool.CreateGuardedBuffer<T>(mem, length, options.Has(AllocationOptions.Clean));
                return buffer;
            }
        }

        return this.nonPoolAllocator.Allocate<T>(length, options);
    }

    /// <inheritdoc />
    internal override MemoryGroup<T> AllocateGroupCore<T>(
        long totalLengthInElements,
        long totalLengthInBytes,
        int bufferAlignment,
        AllocationOptions options = AllocationOptions.None)
    {
        if (totalLengthInBytes <= this.sharedArrayPoolThresholdInBytes)
        {
            SharedArrayPoolBuffer<T> buffer = new((int)totalLengthInElements);
            return MemoryGroup<T>.CreateContiguous(buffer, options.Has(AllocationOptions.Clean));
        }

        if (totalLengthInBytes <= this.poolBufferSizeInBytes)
        {
            // Optimized path renting single array from the pool
            UnmanagedMemoryHandle mem = this.pool.Rent();
            if (mem.IsValid)
            {
                UnmanagedBuffer<T> buffer = this.pool.CreateGuardedBuffer<T>(mem, (int)totalLengthInElements, options.Has(AllocationOptions.Clean));
                return MemoryGroup<T>.CreateContiguous(buffer, options.Has(AllocationOptions.Clean));
            }
        }

        // Attempt to rent the whole group from the pool, allocate a group of unmanaged buffers if the attempt fails:
        if (MemoryGroup<T>.TryAllocate(this.pool, totalLengthInElements, bufferAlignment, options, out MemoryGroup<T>? poolGroup))
        {
            return poolGroup;
        }

        return MemoryGroup<T>.Allocate(this.nonPoolAllocator, totalLengthInElements, bufferAlignment, options);
    }

    public override void ReleaseRetainedResources() => this.pool.Release();

    private static long GetDefaultMaxPoolSizeBytes()
    {
        if (Environment.Is64BitProcess)
        {
            // On 64 bit set the pool size to a portion of the total available memory.
            GCMemoryInfo info = GC.GetGCMemoryInfo();
            return info.TotalAvailableMemoryBytes / 8;
        }

        // Stick to a conservative value of 128 Megabytes on 32 bit.
        return 128 * OneMegabyte;
    }
}
