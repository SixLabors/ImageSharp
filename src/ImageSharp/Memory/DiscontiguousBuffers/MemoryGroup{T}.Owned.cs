// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

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

            public Owned(IMemoryOwner<T>[] memoryOwners, int bufferLength, long totalLength, bool swappable)
                : base(bufferLength, totalLength)
            {
                this.memoryOwners = memoryOwners;
                this.Swappable = swappable;
                this.View = new MemoryGroupView<T>(this);
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

            public override void Dispose()
            {
                if (this.IsDisposed)
                {
                    return;
                }

                this.View.Invalidate();

                foreach (IMemoryOwner<T> memoryOwner in this.memoryOwners)
                {
                    memoryOwner.Dispose();
                }

                this.memoryOwners = null;
                this.IsValid = false;
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

                a.memoryOwners = b.memoryOwners;
                a.TotalLength = b.TotalLength;
                a.BufferLength = b.BufferLength;

                b.memoryOwners = tempOwners;
                b.TotalLength = tempTotalLength;
                b.BufferLength = tempBufferLength;

                a.View.Invalidate();
                b.View.Invalidate();
                a.View = new MemoryGroupView<T>(a);
                b.View = new MemoryGroupView<T>(b);
            }
        }
    }
}
