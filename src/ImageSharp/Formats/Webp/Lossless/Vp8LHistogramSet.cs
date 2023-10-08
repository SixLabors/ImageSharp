// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

#nullable disable

using System.Buffers;
using System.Collections;
using System.Diagnostics;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless;

internal sealed class Vp8LHistogramSet : IEnumerable<Vp8LHistogram>, IDisposable
{
    private readonly IMemoryOwner<uint> buffer;
    private readonly List<Vp8LHistogram> items;
    private bool isDisposed;

    public Vp8LHistogramSet(MemoryAllocator memoryAllocator, int capacity, int cacheBits)
    {
        this.buffer = memoryAllocator.Allocate<uint>(Vp8LHistogram.BufferSize * capacity, AllocationOptions.Clean);

        this.items = new List<Vp8LHistogram>(capacity);
        for (int i = 0; i < capacity; i++)
        {
            Memory<uint> subBuffer = this.buffer.Memory.Slice(Vp8LHistogram.BufferSize * i, Vp8LHistogram.BufferSize);
            this.items.Add(new Vp8LHistogram(subBuffer, cacheBits));
        }
    }

    public Vp8LHistogramSet(MemoryAllocator memoryAllocator, Vp8LBackwardRefs refs, int capacity, int cacheBits)
    {
        this.buffer = memoryAllocator.Allocate<uint>(Vp8LHistogram.BufferSize * capacity, AllocationOptions.Clean);

        this.items = new List<Vp8LHistogram>(capacity);
        for (int i = 0; i < capacity; i++)
        {
            Memory<uint> subBuffer = this.buffer.Memory.Slice(Vp8LHistogram.BufferSize * i, Vp8LHistogram.BufferSize);
            this.items.Add(new Vp8LHistogram(subBuffer, refs, cacheBits));
        }
    }

    public Vp8LHistogramSet(int capacity) => this.items = new(capacity);

    public Vp8LHistogramSet() => this.items = new();

    public int Count => this.items.Count;

    public Vp8LHistogram this[int index]
    {
        get => this.items[index];

        // TODO: Should we check and throw for null?
        set => this.items[index] = value;
    }

    public void DisposeAt(int index)
    {
        this.CheckDisposed();

        Vp8LHistogram item = this.items[index];
        item?.Dispose();
        this.items[index] = null;
    }

    public void RemoveAt(int index)
    {
        this.CheckDisposed();

        Vp8LHistogram item = this.items[index];
        item?.Dispose();
        this.items.RemoveAt(index);
#pragma warning disable IDE0059 // Unnecessary assignment of a value
        item = null;
#pragma warning restore IDE0059 // Unnecessary assignment of a value
    }

    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        foreach (Vp8LHistogram item in this.items)
        {
            // First, make sure to unpin individual sub buffers.
            item?.Dispose();
        }

        this.buffer.Dispose();
        this.items.Clear();
        this.isDisposed = true;
    }

    public IEnumerator<Vp8LHistogram> GetEnumerator() => ((IEnumerable<Vp8LHistogram>)this.items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)this.items).GetEnumerator();

    [Conditional("DEBUG")]
    private void CheckDisposed()
    {
        if (this.isDisposed)
        {
            ThrowDisposed();
        }
    }

    private static void ThrowDisposed() => throw new ObjectDisposedException(nameof(Vp8LHistogramSet));
}
