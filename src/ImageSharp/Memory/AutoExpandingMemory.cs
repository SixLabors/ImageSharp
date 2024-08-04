// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;

namespace SixLabors.ImageSharp.Memory;

/// <summary>
/// Memory class that will expand dynamically when full.
/// </summary>
internal sealed class AutoExpandingMemory<T> : IDisposable
    where T : unmanaged
{
    private const int IncreaseFactor = 5;
    private readonly Configuration configuration;
    private IMemoryOwner<T> allocation;

    public AutoExpandingMemory(Configuration configuration, int initialSize)
    {
        Guard.MustBeGreaterThan(initialSize, 0, nameof(initialSize));

        this.configuration = configuration;
        this.allocation = this.configuration.MemoryAllocator.Allocate<T>(initialSize);
    }

    public int Capacity => this.allocation.Memory.Length;

    public Span<T> GetSpan(int requestedSize)
    {
        Guard.MustBeGreaterThanOrEqualTo(requestedSize, 0, nameof(requestedSize));
        this.EnsureCapacity(requestedSize);

        return this.allocation.Memory.Span[..requestedSize];
    }

    public Span<T> GetSpan(int offset, int requestedSize)
    {
        Guard.MustBeGreaterThanOrEqualTo(offset, 0, nameof(offset));
        Guard.MustBeGreaterThanOrEqualTo(requestedSize, 0, nameof(requestedSize));
        this.EnsureCapacity(offset + requestedSize);

        return this.allocation.Memory.Span.Slice(offset, requestedSize);
    }

    public Span<T> GetEntireSpan()
        => this.GetSpan(this.Capacity);

    public void Dispose() => this.allocation.Dispose();

    private void EnsureCapacity(int requestedSize)
    {
        if (requestedSize > this.allocation.Memory.Length)
        {
            int newSize = requestedSize + (requestedSize / IncreaseFactor);
            IMemoryOwner<T> newAllocation = this.configuration.MemoryAllocator.Allocate<T>(newSize);
            this.allocation.Memory.CopyTo(newAllocation.Memory);
            this.allocation.Dispose();
            this.allocation = newAllocation;
        }
    }
}
