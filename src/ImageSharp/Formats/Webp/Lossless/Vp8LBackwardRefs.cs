// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless;

internal class Vp8LBackwardRefs : IDisposable
{
    private readonly IMemoryOwner<PixOrCopy> refs;
    private int count;

    public Vp8LBackwardRefs(MemoryAllocator memoryAllocator, int pixels)
    {
        this.refs = memoryAllocator.Allocate<PixOrCopy>(pixels);
        this.count = 0;
    }

    public void Add(PixOrCopy pixOrCopy) => this.refs.Memory.Span[this.count++] = pixOrCopy;

    public void Clear() => this.count = 0;

    public Span<PixOrCopy>.Enumerator GetEnumerator() => this.refs.Slice(0, this.count).GetEnumerator();

    /// <inheritdoc/>
    public void Dispose() => this.refs.Dispose();
}
