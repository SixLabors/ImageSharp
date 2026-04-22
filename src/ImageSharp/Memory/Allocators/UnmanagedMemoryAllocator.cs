// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
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

    protected override AllocationTrackedMemoryManager<T> AllocateCore<T>(int length, AllocationOptions options = AllocationOptions.None)
        where T : struct
        => AllocateBuffer<T>(length, options);

    internal override IMemoryOwner<T> AllocateGroupBuffer<T>(int length, AllocationOptions options = AllocationOptions.None)
        where T : struct
        => AllocateBuffer<T>(length, options);

    // The pooled allocator uses this internal entry point when it needs a raw unmanaged owner without
    // nesting another allocator-level reservation cycle around the fallback allocation.
    internal static UnmanagedBuffer<T> AllocateBuffer<T>(int length, AllocationOptions options = AllocationOptions.None)
        where T : struct
    {
        UnmanagedBuffer<T> buffer = UnmanagedBuffer<T>.Allocate(length);
        if (options.Has(AllocationOptions.Clean))
        {
            buffer.GetSpan().Clear();
        }

        return buffer;
    }
}
