// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory.Allocators.Internals;

namespace SixLabors.ImageSharp.Memory.Internals;

internal class SharedArrayPoolBuffer<T> : ManagedBufferBase<T>, IRefCounted
    where T : struct
{
    private readonly int lengthInBytes;
    private readonly int alignedOffsetElements;

#pragma warning disable IDE0044 // Add readonly modifier
    private LifetimeGuard lifetimeGuard;
#pragma warning restore IDE0044 // Add readonly modifier

    public SharedArrayPoolBuffer(int lengthInElements)
    {
        this.lengthInBytes = checked(lengthInElements * Unsafe.SizeOf<T>());
        nuint alignment = MemoryUtilities.GetAlignment();

        // Rent slack so we can advance the exposed span start to the next aligned address.
        this.Array = ArrayPool<byte>.Shared.Rent(checked(this.lengthInBytes + (int)alignment - 1));

        int offsetBytes = MemoryUtilities.GetAlignedOffsetBytes<T>(this.Array);
        this.alignedOffsetElements = offsetBytes / Unsafe.SizeOf<T>();

        this.lifetimeGuard = new LifetimeGuard(this.Array);
    }

    public byte[]? Array { get; private set; }

    public int AlignedOffsetBytes => this.alignedOffsetElements * Unsafe.SizeOf<T>();

    protected override void Dispose(bool disposing)
    {
        if (this.Array == null)
        {
            return;
        }

        this.lifetimeGuard.Dispose();
        this.Array = null;
    }

    public override Span<T> GetSpan()
    {
        this.CheckDisposed();

        // Expose only the aligned slice, never the full rented buffer.
        // Use the stored offset so the span base does not depend on recomputing alignment at call time.
        int offsetBytes = this.AlignedOffsetBytes;

        Span<byte> bytes = this.Array.AsSpan(offsetBytes, this.lengthInBytes);
        return MemoryMarshal.Cast<byte, T>(bytes);
    }

    protected override int GetPinnableElementOffset() => this.alignedOffsetElements;

    protected override object GetPinnableObject()
    {
        this.CheckDisposed();
        return this.Array;
    }

    public void AddRef()
    {
        this.CheckDisposed();
        this.lifetimeGuard.AddRef();
    }

    public void ReleaseRef() => this.lifetimeGuard.ReleaseRef();

    [Conditional("DEBUG")]
    [MemberNotNull(nameof(Array))]
    private void CheckDisposed() => ObjectDisposedException.ThrowIf(this.Array == null, this.Array);

    private sealed class LifetimeGuard : RefCountedMemoryLifetimeGuard
    {
        private byte[]? array;

        public LifetimeGuard(byte[] array) => this.array = array;

        protected override void Release()
        {
            ArrayPool<byte>.Shared.Return(this.array!);
            this.array = null;
        }
    }
}
