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
    private MemoryHandle bufferHandle;
    private readonly List<Vp8LHistogram> items;
    private bool isDisposed;

    public Vp8LHistogramSet(MemoryAllocator memoryAllocator, int capacity, int cacheBits)
    {
        this.buffer = memoryAllocator.Allocate<uint>(Vp8LHistogram.BufferSize * capacity, AllocationOptions.Clean);
        this.bufferHandle = this.buffer.Memory.Pin();

        unsafe
        {
            uint* basePointer = (uint*)this.bufferHandle.Pointer;
            this.items = new(capacity);
            for (int i = 0; i < capacity; i++)
            {
                this.items.Add(new MemberVp8LHistogram(basePointer + (Vp8LHistogram.BufferSize * i), cacheBits));
            }
        }
    }

    public Vp8LHistogramSet(MemoryAllocator memoryAllocator, Vp8LBackwardRefs refs, int capacity, int cacheBits)
    {
        this.buffer = memoryAllocator.Allocate<uint>(Vp8LHistogram.BufferSize * capacity, AllocationOptions.Clean);
        this.bufferHandle = this.buffer.Memory.Pin();

        unsafe
        {
            uint* basePointer = (uint*)this.bufferHandle.Pointer;
            this.items = new(capacity);
            for (int i = 0; i < capacity; i++)
            {
                this.items.Add(new MemberVp8LHistogram(basePointer + (Vp8LHistogram.BufferSize * i), refs, cacheBits));
            }
        }
    }

    public Vp8LHistogramSet(int capacity) => this.items = new(capacity);

    public Vp8LHistogramSet() => this.items = new();

    public int Count => this.items.Count;

    public Vp8LHistogram this[int index]
    {
        get => this.items[index];
        set => this.items[index] = value;
    }

    public void RemoveAt(int index)
    {
        this.CheckDisposed();
        this.items.RemoveAt(index);
    }

    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.buffer.Dispose();
        this.bufferHandle.Dispose();
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

    private sealed unsafe class MemberVp8LHistogram : Vp8LHistogram
    {
        public MemberVp8LHistogram(uint* basePointer, int paletteCodeBits)
            : base(basePointer, paletteCodeBits)
        {
        }

        public MemberVp8LHistogram(uint* basePointer, Vp8LBackwardRefs refs, int paletteCodeBits)
            : base(basePointer, refs, paletteCodeBits)
        {
        }
    }
}
