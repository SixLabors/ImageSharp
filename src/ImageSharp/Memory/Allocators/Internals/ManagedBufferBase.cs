// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Memory.Internals;

/// <summary>
/// Provides a base class for <see cref="IMemoryOwner{T}"/> implementations by implementing pinning logic for <see cref="MemoryManager{T}"/> adaption.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal abstract class ManagedBufferBase<T> : MemoryManager<T>
    where T : struct
{
    private GCHandle pinHandle;

    /// <summary>
    /// Gets the element offset (in <typeparamref name="T"/> units) from the pinned object start to the first element
    /// of the span returned by <see cref="MemoryManager{T}.GetSpan"/>.
    /// </summary>
    /// <remarks>
    /// This exists to support buffers that return a trimmed/sliced span (eg, for alignment).
    /// </remarks>
    protected virtual int GetPinnableElementOffset() => 0;

    /// <inheritdoc />
    public override unsafe MemoryHandle Pin(int elementIndex = 0)
    {
        if (!this.pinHandle.IsAllocated)
        {
            this.pinHandle = GCHandle.Alloc(this.GetPinnableObject(), GCHandleType.Pinned);
        }

        int baseIndex = this.GetPinnableElementOffset();
        void* ptr = Unsafe.Add<T>((void*)this.pinHandle.AddrOfPinnedObject(), baseIndex + elementIndex);

        // We should only pass pinnable:this, when GCHandle lifetime is managed by the MemoryManager<T> instance.
        return new MemoryHandle(ptr, pinnable: this);
    }

    /// <inheritdoc />
    public override void Unpin()
    {
        if (this.pinHandle.IsAllocated)
        {
            this.pinHandle.Free();
        }
    }

    /// <summary>
    /// Gets the object that should be pinned.
    /// </summary>
    /// <returns>The pinnable <see cref="object"/>.</returns>
    protected abstract object GetPinnableObject();
}
