// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory.Internals;

namespace SixLabors.ImageSharp.Memory;

/// <summary>
/// A <see cref="MemoryAllocator"/> implementation that allocates memory on the unmanaged heap
/// without any pooling.
/// </summary>
internal class UnmanagedMemoryAllocator : MemoryAllocator
{
    private readonly int bufferCapacityInBytes;

    public UnmanagedMemoryAllocator(int bufferCapacityInBytes) => this.bufferCapacityInBytes = bufferCapacityInBytes;

    protected internal override int GetBufferCapacityInBytes() => this.bufferCapacityInBytes;

    public override IMemoryOwner<T> Allocate<T>(int length, AllocationOptions options = AllocationOptions.None)
        where T : struct
    {
        ulong lengthInBytes = (ulong)length * (ulong)Unsafe.SizeOf<T>();
        long lengthInBytesLong = (long)lengthInBytes;
        this.ReserveAllocation(lengthInBytesLong);

        try
        {
            UnmanagedBuffer<T> buffer = UnmanagedBuffer<T>.Allocate(length);
            if (options.Has(AllocationOptions.Clean))
            {
                buffer.GetSpan().Clear();
            }

            return this.TrackAllocation(buffer, lengthInBytes);
        }
        catch
        {
            this.ReleaseAccumulatedBytes(lengthInBytesLong);
            throw;
        }
    }
}
