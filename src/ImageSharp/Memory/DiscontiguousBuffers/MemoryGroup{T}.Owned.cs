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

            // When user calls DefaultMemoryAllocator.ReleaseRetainedResources(), we want
            // UniformByteArrayPool to be released and GC-d ASAP. We need to prevent existing Image-s and buffers
            // to keep it alive, so we use WeakReference instead of directly referencing the pool.
            private WeakReference<UniformByteArrayPool> arrayPoolReference;
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

            public Owned(UniformByteArrayPool pool, byte[][] pooledArrays, int bufferLength, long totalLength, int sizeOfLastBuffer)
                : this(CreateBuffers(pool, pooledArrays, bufferLength, sizeOfLastBuffer), bufferLength, totalLength, true)
            {
                this.pooledArrays = pooledArrays;
                this.arrayPoolReference = new WeakReference<UniformByteArrayPool>(pool);

                // Track all WeakReference's to make sure they are not finalized before ~FinalizableBuffer<T>()
                WeakReferenceTracker.Add(this.arrayPoolReference);
            }

            public Owned(UniformUnmanagedMemoryPool pool, UnmanagedMemoryHandle[] pooledArrays, int bufferLength, long totalLength, int sizeOfLastBuffer)
                : this(CreateBuffers(pool, pooledArrays, bufferLength, sizeOfLastBuffer), bufferLength, totalLength, true)
            {
                this.pooledHandles = pooledArrays;
                this.unmanagedMemoryPool = pool;
            }

            ~Owned()
            {
                this.Dispose(false);
            }

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

            private static IMemoryOwner<T>[] CreateBuffers(UniformByteArrayPool pool, byte[][] pooledArrays, int bufferLength, int sizeOfLastBuffer)
            {
                var result = new IMemoryOwner<T>[pooledArrays.Length];
                for (int i = 0; i < pooledArrays.Length - 1; i++)
                {
                    result[i] = new UniformByteArrayPool.Buffer<T>(pooledArrays[i], bufferLength, pool);
                }

                result[result.Length - 1] = new UniformByteArrayPool.Buffer<T>(pooledArrays[pooledArrays.Length - 1], sizeOfLastBuffer, pool);
                return result;
            }

            private static IMemoryOwner<T>[] CreateBuffers(UniformUnmanagedMemoryPool pool, UnmanagedMemoryHandle[] pooledArrays, int bufferLength, int sizeOfLastBuffer)
            {
                var result = new IMemoryOwner<T>[pooledArrays.Length];
                for (int i = 0; i < pooledArrays.Length - 1; i++)
                {
                    pooledArrays[i].UnResurrect();
                    result[i] = new UniformUnmanagedMemoryPool.Buffer<T>(pool, pooledArrays[i], bufferLength);
                }

                result[result.Length - 1] = new UniformUnmanagedMemoryPool.Buffer<T>(pool, pooledArrays[pooledArrays.Length - 1], sizeOfLastBuffer);
                return result;
            }

            /// <inheritdoc/>
            [MethodImpl(InliningOptions.ShortMethod)]
            public override MemoryGroupEnumerator<T> GetEnumerator()
            {
                return new MemoryGroupEnumerator<T>(this);
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
                    if (!disposing)
                    {
                        foreach (UnmanagedMemoryHandle handle in this.pooledHandles)
                        {
                            handle.Resurrect();
                        }
                    }

                    this.unmanagedMemoryPool.Return(this.pooledHandles);
                    foreach (IMemoryOwner<T> memoryOwner in this.memoryOwners)
                    {
                        ((UniformUnmanagedMemoryPool.Buffer<T>)memoryOwner).MarkDisposed();
                    }
                }
                else if (this.arrayPoolReference != null && this.arrayPoolReference.TryGetTarget(out UniformByteArrayPool pool))
                {
                    // Dispose(false) could be called from a finalizer, so we can return the rented arrays,
                    // even if user code is leaking.
                    // We are fine to do this, since byte[][] and UniformByteArrayPool are not finalizable.
                    WeakReferenceTracker.Remove(this.arrayPoolReference);
                    pool.Return(this.pooledArrays);
                    foreach (IMemoryOwner<T> memoryOwner in this.memoryOwners)
                    {
                        ((UniformByteArrayPool.Buffer<T>)memoryOwner).MarkDisposed();
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
                this.arrayPoolReference = null;
                this.pooledArrays = null;
                this.unmanagedMemoryPool = null;
                this.pooledHandles = null;
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
            private static void ThrowObjectDisposedException()
            {
                throw new ObjectDisposedException(nameof(MemoryGroup<T>));
            }

            internal static void SwapContents(Owned a, Owned b)
            {
                a.EnsureNotDisposed();
                b.EnsureNotDisposed();

                IMemoryOwner<T>[] tempOwners = a.memoryOwners;
                long tempTotalLength = a.TotalLength;
                int tempBufferLength = a.BufferLength;
                WeakReference<UniformByteArrayPool> tempPoolReference = a.arrayPoolReference;
                byte[][] tempPooledArrays = a.pooledArrays;
                UniformUnmanagedMemoryPool tempUnmangedPool = a.unmanagedMemoryPool;
                UnmanagedMemoryHandle[] tempPooledHandles = a.pooledHandles;

                a.memoryOwners = b.memoryOwners;
                a.TotalLength = b.TotalLength;
                a.BufferLength = b.BufferLength;
                a.arrayPoolReference = b.arrayPoolReference;
                a.pooledArrays = b.pooledArrays;
                a.unmanagedMemoryPool = b.unmanagedMemoryPool;
                a.pooledHandles = b.pooledHandles;

                b.memoryOwners = tempOwners;
                b.TotalLength = tempTotalLength;
                b.BufferLength = tempBufferLength;
                b.arrayPoolReference = tempPoolReference;
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
