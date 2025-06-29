// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Memory.Internals;

/// <summary>
/// Allocates and provides an <see cref="IMemoryOwner{T}"/> implementation giving
/// access to unmanaged buffers allocated by <see cref="Marshal.AllocHGlobal(int)"/>.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed unsafe class UnmanagedBuffer<T> : MemoryManager<T>, IRefCounted
    where T : struct
{
    private readonly int lengthInElements;

    private readonly UnmanagedBufferLifetimeGuard lifetimeGuard;

    private int disposed;

    public UnmanagedBuffer(int lengthInElements, UnmanagedBufferLifetimeGuard lifetimeGuard)
    {
        DebugGuard.NotNull(lifetimeGuard, nameof(lifetimeGuard));

        this.lengthInElements = lengthInElements;
        this.lifetimeGuard = lifetimeGuard;
    }

    public void* Pointer => this.lifetimeGuard.Handle.Pointer;

    public override Span<T> GetSpan()
    {
        DebugGuard.NotDisposed(this.disposed == 1, this.GetType().Name);
        DebugGuard.NotDisposed(this.lifetimeGuard.IsDisposed, this.lifetimeGuard.GetType().Name);
        return new Span<T>(this.Pointer, this.lengthInElements);
    }

    /// <inheritdoc />
    public override MemoryHandle Pin(int elementIndex = 0)
    {
        DebugGuard.NotDisposed(this.disposed == 1, this.GetType().Name);
        DebugGuard.NotDisposed(this.lifetimeGuard.IsDisposed, this.lifetimeGuard.GetType().Name);

        // Will be released in Unpin
        this.lifetimeGuard.AddRef();

        void* pbData = Unsafe.Add<T>(this.Pointer, elementIndex);
        return new MemoryHandle(pbData, pinnable: this);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        DebugGuard.IsTrue(disposing, nameof(disposing), "Unmanaged buffers should not have finalizer!");

        if (Interlocked.Exchange(ref this.disposed, 1) == 1)
        {
            // Already disposed
            return;
        }

        this.lifetimeGuard.Dispose();
    }

    /// <inheritdoc />
    public override void Unpin() => this.lifetimeGuard.ReleaseRef();

    public void AddRef() => this.lifetimeGuard.AddRef();

    public void ReleaseRef() => this.lifetimeGuard.ReleaseRef();

    public static UnmanagedBuffer<T> Allocate(int lengthInElements) =>
        new(lengthInElements, new UnmanagedBufferLifetimeGuard.FreeHandle(UnmanagedMemoryHandle.Allocate(lengthInElements * Unsafe.SizeOf<T>())));
}
