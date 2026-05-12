// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
    public SimpleGcMemoryAllocator(MemoryAllocatorOptions options) => this.ApplyOptions(options);

    /// <inheritdoc />
    protected internal override int GetBufferCapacityInBytes() => int.MaxValue;

    /// <inheritdoc />
    protected override AllocationTrackedMemoryManager<T> AllocateCore<T>(int length, AllocationOptions options = AllocationOptions.None)
        => new BasicArrayBuffer<T>(new T[length]);
}
