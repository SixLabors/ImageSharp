// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Implements <see cref="IMemoryGroup{T}"/>, defining a view for <see cref="Memory.MemoryGroup{T}"/>
    /// rather than owning the segments.
    /// </summary>
    /// <remarks>
    /// This type provides an indirection, protecting the users of publicly exposed memory API-s
    /// from internal memory-swaps. Whenever an internal swap happens, the <see cref="MemoryGroupView{T}"/>
    /// instance becomes invalid, throwing an exception on all operations.
    /// </remarks>
    /// <typeparam name="T">The element type.</typeparam>
    internal class MemoryGroupView<T> : IMemoryGroup<T>
        where T : struct
    {
        private MemoryGroup<T> owner;
        private readonly MemoryOwnerWrapper[] memoryWrappers;

        public MemoryGroupView(MemoryGroup<T> owner)
        {
            this.owner = owner;
            this.memoryWrappers = new MemoryOwnerWrapper[owner.Count];

            for (int i = 0; i < owner.Count; i++)
            {
                this.memoryWrappers[i] = new MemoryOwnerWrapper(this, i);
            }
        }

        public int Count
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get
            {
                this.EnsureIsValid();
                return this.owner.Count;
            }
        }

        public int BufferLength
        {
            get
            {
                this.EnsureIsValid();
                return this.owner.BufferLength;
            }
        }

        public long TotalLength
        {
            get
            {
                this.EnsureIsValid();
                return this.owner.TotalLength;
            }
        }

        public bool IsValid => this.owner != null;

        public Memory<T> this[int index]
        {
            get
            {
                this.EnsureIsValid();
                return this.memoryWrappers[index].Memory;
            }
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public MemoryGroupEnumerator<T> GetEnumerator()
        {
            return new MemoryGroupEnumerator<T>(this);
        }

        /// <inheritdoc/>
        IEnumerator<Memory<T>> IEnumerable<Memory<T>>.GetEnumerator()
        {
            this.EnsureIsValid();
            for (int i = 0; i < this.Count; i++)
            {
                yield return this.memoryWrappers[i].Memory;
            }
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<Memory<T>>)this).GetEnumerator();

        internal void Invalidate()
        {
            this.owner = null;
        }

        private void EnsureIsValid()
        {
            if (!this.IsValid)
            {
                throw new InvalidMemoryOperationException("Can not access an invalidated MemoryGroupView!");
            }
        }

        private class MemoryOwnerWrapper : MemoryManager<T>
        {
            private readonly MemoryGroupView<T> view;

            private readonly int index;

            public MemoryOwnerWrapper(MemoryGroupView<T> view, int index)
            {
                this.view = view;
                this.index = index;
            }

            protected override void Dispose(bool disposing)
            {
            }

            public override Span<T> GetSpan()
            {
                this.view.EnsureIsValid();
                return this.view.owner[this.index].Span;
            }

            public override MemoryHandle Pin(int elementIndex = 0)
            {
                this.view.EnsureIsValid();
                return this.view.owner[this.index].Pin();
            }

            public override void Unpin()
            {
                throw new NotSupportedException();
            }
        }
    }
}
