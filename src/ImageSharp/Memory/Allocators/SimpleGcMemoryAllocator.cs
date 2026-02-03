// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory.Internals;

namespace SixLabors.ImageSharp.Memory;

/// <summary>
/// Implements <see cref="MemoryAllocator"/> by newing up managed arrays on every allocation request.
/// </summary>
public sealed class SimpleGcMemoryAllocator : MemoryAllocator
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleGcMemoryAllocator"/> class with default limits.
    /// </summary>
    public SimpleGcMemoryAllocator()
        : this(default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleGcMemoryAllocator"/> class with custom limits.
    /// </summary>
    /// <param name="options">The <see cref="MemoryAllocatorOptions"/> to apply.</param>
    public SimpleGcMemoryAllocator(MemoryAllocatorOptions options)
    {
        if (options.AllocationLimitMegabytes.HasValue)
        {
            this.MemoryGroupAllocationLimitBytes = options.AllocationLimitMegabytes.Value * 1024L * 1024L;
            this.SingleBufferAllocationLimitBytes = (int)Math.Min(this.SingleBufferAllocationLimitBytes, this.MemoryGroupAllocationLimitBytes);
        }

        if (options.AccumulativeAllocationLimitMegabytes.HasValue)
        {
            this.AccumulativeAllocationLimitBytes = options.AccumulativeAllocationLimitMegabytes.Value * 1024L * 1024L;
        }
    }

    /// <inheritdoc />
    protected internal override int GetBufferCapacityInBytes() => int.MaxValue;

    /// <inheritdoc />
    public override IMemoryOwner<T> Allocate<T>(int length, AllocationOptions options = AllocationOptions.None)
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
        this.ReserveAllocation(lengthInBytesLong);

        try
        {
            IMemoryOwner<T> buffer = new BasicArrayBuffer<T>(new T[length]);
            return this.TrackAllocation(buffer, lengthInBytes);
        }
        catch
        {
            this.ReleaseAccumulatedBytes(lengthInBytesLong);
            throw;
        }
    }
}
