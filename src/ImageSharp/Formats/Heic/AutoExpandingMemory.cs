// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;

namespace SixLabors.ImageSharp.Formats.Heic;

/// <summary>
/// Memory class that will expand dynamically when full.
/// </summary>
internal sealed class AutoExpandingMemory<T> : IDisposable
    where T : unmanaged
{
    private readonly Configuration configuration;
    private IMemoryOwner<T> allocation;

    public AutoExpandingMemory(Configuration configuration, int initialSize)
    {
        Guard.MustBeGreaterThan(initialSize, 0, nameof(initialSize));

        this.configuration = configuration;
        this.allocation = this.configuration.MemoryAllocator.Allocate<T>(initialSize);
    }

    public Span<T> GetSpan(int requestedSize)
    {
        Guard.MustBeGreaterThan(requestedSize, 0, nameof(requestedSize));
        this.EnsureCapacity(requestedSize);

        return this.allocation.Memory.Span[..requestedSize];
    }

    public Span<T> GetSpan(int offset, int requestedSize)
    {
        Guard.MustBeGreaterThan(offset, 0, nameof(offset));
        Guard.MustBeGreaterThan(requestedSize, 0, nameof(requestedSize));
        this.EnsureCapacity(offset + requestedSize);

        return this.allocation.Memory.Span.Slice(offset, requestedSize);
    }

    public void Dispose() => this.allocation.Dispose();

    private void EnsureCapacity(int requestedSize)
    {
        if (requestedSize > this.allocation.Memory.Length)
        {
            int newSize = requestedSize + (requestedSize / 5);
            IMemoryOwner<T> newAllocation = this.configuration.MemoryAllocator.Allocate<T>(newSize);
            this.allocation.Memory.CopyTo(newAllocation.Memory);
            this.allocation.Dispose();
            this.allocation = newAllocation;
        }
    }
}
