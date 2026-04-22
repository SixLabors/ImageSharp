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

        return new BasicArrayBuffer<T>(new T[length]);
    }
}
