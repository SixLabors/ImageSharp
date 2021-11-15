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
            private byte[][] pooledArrays;
            private UniformUnmanagedMemoryPool unmanagedMemoryPool;
            private UnmanagedMemoryHandle[] pooledHandles;

            public Owned(IMemoryOwner<T>[] memoryOwners, int bufferLength, long totalLength, bool swappable)
                : base(bufferLength, totalLength)
            {
                this.memoryOwners = memoryOwners;
                this.Swappable = swappable;
                this.View = new MemoryGroupView<T>(this);
            }

            public Owned(UniformUnmanagedMemoryPool pool, UnmanagedMemoryHandle[] pooledArrays, int bufferLength, long totalLength, int sizeOfLastBuffer, AllocationOptions options)
                : this(CreateBuffers(pool, pooledArrays, bufferLength, sizeOfLastBuffer, options), bufferLength, totalLength, true)
            {
                this.pooledHandles = pooledArrays;
                this.unmanagedMemoryPool = pool;
            }

            ~Owned() => this.Dispose(false);

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
                UniformUnmanagedMemoryPool pool,
                UnmanagedMemoryHandle[] pooledBuffers,
                int bufferLength,
                int sizeOfLastBuffer,
                AllocationOptions options)
            {
                var result = new IMemoryOwner<T>[pooledBuffers.Length];
                for (int i = 0; i < pooledBuffers.Length - 1; i++)
                {
                    pooledBuffers[i].AssignedToNewOwner();
                    result[i] = new UniformUnmanagedMemoryPool.Buffer<T>(pool, pooledBuffers[i], bufferLength);
                    if (options.Has(AllocationOptions.Clean))
                    {
                        result[i].Clear();
                    }
                }

                result[result.Length - 1] = new UniformUnmanagedMemoryPool.Buffer<T>(pool, pooledBuffers[pooledBuffers.Length - 1], sizeOfLastBuffer);
                return result;
            }

            /// <inheritdoc/>
            [MethodImpl(InliningOptions.ShortMethod)]
            public override MemoryGroupEnumerator<T> GetEnumerator()
            {
                return new MemoryGroupEnumerator<T>(this);
            }

            public override void IncreaseRefCounts()
            {
                this.EnsureNotDisposed();
                bool dummy = default;
                foreach (IMemoryOwner<T> memoryOwner in this.memoryOwners)
                {
                    if (memoryOwner is UnmanagedBuffer<T> unmanagedBuffer)
                    {
                        unmanagedBuffer.BufferHandle?.DangerousAddRef(ref dummy);
                    }
                }
            }

            public override void DecreaseRefCounts()
            {
                this.EnsureNotDisposed();
                foreach (IMemoryOwner<T> memoryOwner in this.memoryOwners)
                {
                    if (memoryOwner is UnmanagedBuffer<T> unmanagedBuffer)
                    {
                        unmanagedBuffer.BufferHandle?.DangerousRelease();
                    }
                }
            }

            public override void VerifyMemorySentinel()
            {
                foreach (IMemoryOwner<T> memoryOwner in this.memoryOwners)
                {
                    if (memoryOwner is UnmanagedBuffer<T> unmanagedBuffer)
                    {
                        unmanagedBuffer.VerifyMemorySentinel();
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
                if (this.IsDisposed)
                {
                    return;
                }

                this.View.Invalidate();

                if (this.unmanagedMemoryPool != null)
                {
                    this.unmanagedMemoryPool.Return(this.pooledHandles);
                    if (!disposing)
                    {
                        foreach (UnmanagedMemoryHandle handle in this.pooledHandles)
                        {
                            // We need to prevent handle finalization here.
                            // See comments on UnmanagedMemoryHandle.Resurrect()
                            handle.Resurrect();
                        }
                    }

                    foreach (IMemoryOwner<T> memoryOwner in this.memoryOwners)
                    {
                        ((UniformUnmanagedMemoryPool.Buffer<T>)memoryOwner).MarkDisposed();
                    }
                }
                else if (disposing)
                {
                    foreach (IMemoryOwner<T> memoryOwner in this.memoryOwners)
                    {
                        memoryOwner.Dispose();
                    }
                }

                this.memoryOwners = null;
                this.IsValid = false;
                this.pooledArrays = null;
                this.unmanagedMemoryPool = null;
                this.pooledHandles = null;

                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }
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
                byte[][] tempPooledArrays = a.pooledArrays;
                UniformUnmanagedMemoryPool tempUnmangedPool = a.unmanagedMemoryPool;
                UnmanagedMemoryHandle[] tempPooledHandles = a.pooledHandles;

                a.memoryOwners = b.memoryOwners;
                a.TotalLength = b.TotalLength;
                a.BufferLength = b.BufferLength;
                a.pooledArrays = b.pooledArrays;
                a.unmanagedMemoryPool = b.unmanagedMemoryPool;
                a.pooledHandles = b.pooledHandles;

                b.memoryOwners = tempOwners;
                b.TotalLength = tempTotalLength;
                b.BufferLength = tempBufferLength;
                b.pooledArrays = tempPooledArrays;
                b.unmanagedMemoryPool = tempUnmangedPool;
                b.pooledHandles = tempPooledHandles;

                a.View.Invalidate();
                b.View.Invalidate();
                a.View = new MemoryGroupView<T>(a);
                b.View = new MemoryGroupView<T>(b);
            }
        }
    }
}
