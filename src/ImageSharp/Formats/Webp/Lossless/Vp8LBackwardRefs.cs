// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless;

internal class Vp8LBackwardRefs : IDisposable
{
    private readonly IMemoryOwner<PixOrCopy> refs;

    public Vp8LBackwardRefs(MemoryAllocator memoryAllocator, int pixels)
    {
        this.refs = memoryAllocator.Allocate<PixOrCopy>(pixels);
        this.Count = 0;
    }

    public int Count { get; private set; }

    public ref PixOrCopy this[int index] => ref this.refs.Memory.Span[index];

    public void Add(PixOrCopy pixOrCopy) => this.refs.Memory.Span[this.Count++] = pixOrCopy;

    public void Clear() => this.Count = 0;

    /// <inheritdoc/>
    public void Dispose() => this.refs.Dispose();
}
