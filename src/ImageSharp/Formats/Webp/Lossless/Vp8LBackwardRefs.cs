// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless;

internal class Vp8LBackwardRefs : IDisposable
{
    private readonly IMemoryOwner<PixOrCopy> owner;
    private readonly MemoryHandle handle;
    private int count;

    public Vp8LBackwardRefs(MemoryAllocator memoryAllocator, int pixels)
    {
        this.owner = memoryAllocator.Allocate<PixOrCopy>(pixels);
        this.handle = this.owner.Memory.Pin();
        this.count = 0;
    }

    public void Add(PixOrCopy pixOrCopy)
    {
        unsafe
        {
            ((PixOrCopy*)this.handle.Pointer)[this.count++] = pixOrCopy;
        }
    }

    public void Clear() => this.count = 0;

    public Span<PixOrCopy>.Enumerator GetEnumerator() => this.owner.Slice(0, this.count).GetEnumerator();

    /// <inheritdoc/>
    public void Dispose()
    {
        this.handle.Dispose();
        this.owner.Dispose();
    }
}
