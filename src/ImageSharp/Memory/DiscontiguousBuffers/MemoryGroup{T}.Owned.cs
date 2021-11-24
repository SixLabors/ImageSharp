// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory.Internals;

namespace SixLabors.ImageSharp.Memory
{
    internal abstract partial class MemoryGroup<T>
    {
        /// <summary>
        /// A <see cref="MemoryGroup{T}"/> implementation that owns the underlying memory buffers.
        /// </summary>
        public sealed class Owned : MemoryGroup<T>, IEnumerable<Memory<T>>
        {
            private IMemoryOwner<T>[] memoryOwners;
            private RefCountedLifetimeGuard groupLifetimeGuard;

            public Owned(IMemoryOwner<T>[] memoryOwners, int bufferLength, long totalLength, bool swappable)
                : base(bufferLength, totalLength)
            {
                this.memoryOwners = memoryOwners;
                this.Swappable = swappable;
                this.View = new MemoryGroupView<T>(this);
            }

            public Owned(
                UniformUnmanagedMemoryPool pool,
                UnmanagedMemoryHandle[] pooledHandles,
                int bufferLength,
                long totalLength,
                int sizeOfLastBuffer,
                AllocationOptions options)
                : this(CreateBuffers(pooledHandles, bufferLength, sizeOfLastBuffer, options), bufferLength, totalLength, true) =>
                this.groupLifetimeGuard = pool.CreateGroupLifetimeGuard(pooledHandles);

            public bool Swappable { get; }

            private bool IsDisposed => this.memoryOwners == null;

            public override int Count
            {
                [MethodImpl(InliningOptions.ShortMethod)]
                get
                {
                    this.EnsureNotDisposed();
                    return this.memoryOwners.Length;
                }
            }

            public override Memory<T> this[int index]
            {
                get
                {
                    this.EnsureNotDisposed();
                    return this.memoryOwners[index].Memory;
                }
            }

            private static IMemoryOwner<T>[] CreateBuffers(
                UnmanagedMemoryHandle[] pooledBuffers,
                int bufferLength,
                int sizeOfLastBuffer,
                AllocationOptions options)
            {
                var result = new IMemoryOwner<T>[pooledBuffers.Length];
                for (int i = 0; i < pooledBuffers.Length - 1; i++)
                {
                    var currentBuffer = ObservedBuffer.Create(pooledBuffers[i], bufferLength, options);
                    result[i] = currentBuffer;
                }

                var lastBuffer = ObservedBuffer.Create(pooledBuffers[pooledBuffers.Length - 1], sizeOfLastBuffer, options);
                result[result.Length - 1] = lastBuffer;
                return result;
            }

            /// <inheritdoc/>
            [MethodImpl(InliningOptions.ShortMethod)]
            public override MemoryGroupEnumerator<T> GetEnumerator() => new(this);

            public override void IncreaseRefCounts()
            {
                this.EnsureNotDisposed();

                if (this.groupLifetimeGuard != null)
                {
                    this.groupLifetimeGuard.AddRef();
                }
                else
                {
                    foreach (IMemoryOwner<T> memoryOwner in this.memoryOwners)
                    {
                        if (memoryOwner is IRefCounted unmanagedBuffer)
                        {
                            unmanagedBuffer.AddRef();
                        }
                    }
                }
            }

            public override void DecreaseRefCounts()
            {
                this.EnsureNotDisposed();
                if (this.groupLifetimeGuard != null)
                {
                    this.groupLifetimeGuard.ReleaseRef();
                }
                else
                {
                    foreach (IMemoryOwner<T> memoryOwner in this.memoryOwners)
                    {
                        if (memoryOwner is IRefCounted unmanagedBuffer)
                        {
                            unmanagedBuffer.ReleaseRef();
                        }
                    }
                }
            }

            /// <inheritdoc/>
            IEnumerator<Memory<T>> IEnumerable<Memory<T>>.GetEnumerator()
            {
                this.EnsureNotDisposed();
                return this.memoryOwners.Select(mo => mo.Memory).GetEnumerator();
            }

            protected override void Dispose(bool disposing)
            {
                if (this.IsDisposed || !disposing)
                {
                    return;
                }

                this.View.Invalidate();

                if (this.groupLifetimeGuard != null)
                {
                    this.groupLifetimeGuard.Dispose();
                }
                else
                {
                    foreach (IMemoryOwner<T> memoryOwner in this.memoryOwners)
                    {
                        memoryOwner.Dispose();
                    }
                }

                this.memoryOwners = null;
                this.IsValid = false;
                this.groupLifetimeGuard = null;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            private void EnsureNotDisposed()
            {
                if (this.memoryOwners is null)
                {
                    ThrowObjectDisposedException();
                }
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private static void ThrowObjectDisposedException() => throw new ObjectDisposedException(nameof(MemoryGroup<T>));

            internal static void SwapContents(Owned a, Owned b)
            {
                a.EnsureNotDisposed();
                b.EnsureNotDisposed();

                IMemoryOwner<T>[] tempOwners = a.memoryOwners;
                long tempTotalLength = a.TotalLength;
                int tempBufferLength = a.BufferLength;
                RefCountedLifetimeGuard tempGroupOwner = a.groupLifetimeGuard;

                a.memoryOwners = b.memoryOwners;
                a.TotalLength = b.TotalLength;
                a.BufferLength = b.BufferLength;
                a.groupLifetimeGuard = b.groupLifetimeGuard;

                b.memoryOwners = tempOwners;
                b.TotalLength = tempTotalLength;
                b.BufferLength = tempBufferLength;
                b.groupLifetimeGuard = tempGroupOwner;

                a.View.Invalidate();
                b.View.Invalidate();
                a.View = new MemoryGroupView<T>(a);
                b.View = new MemoryGroupView<T>(b);
            }

            // No-ownership
            private sealed class ObservedBuffer : MemoryManager<T>
            {
                private readonly UnmanagedMemoryHandle handle;
                private readonly int lengthInElements;

                private ObservedBuffer(UnmanagedMemoryHandle handle, int lengthInElements)
                {
                    this.handle = handle;
                    this.lengthInElements = lengthInElements;
                }

                public static ObservedBuffer Create(
                    UnmanagedMemoryHandle handle,
                    int lengthInElements,
                    AllocationOptions options)
                {
                    var buffer = new ObservedBuffer(handle, lengthInElements);
                    if (options.Has(AllocationOptions.Clean))
                    {
                        buffer.GetSpan().Clear();
                    }

                    return buffer;
                }

                protected override void Dispose(bool disposing)
                {
                    // No-op.
                }

                public override unsafe Span<T> GetSpan() => new(this.handle.Pointer, this.lengthInElements);

                public override unsafe MemoryHandle Pin(int elementIndex = 0)
                {
                    void* pbData = Unsafe.Add<T>(this.handle.Pointer, elementIndex);
                    return new MemoryHandle(pbData);
                }

                public override void Unpin()
                {
                }
            }
        }
    }
}
