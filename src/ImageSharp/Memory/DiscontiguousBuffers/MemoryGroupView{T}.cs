// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;

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
        private readonly Memory.MemoryGroup<T> owner;
        private readonly MemoryOwnerWrapper[] memoryWrappers;

        public MemoryGroupView(Memory.MemoryGroup<T> owner)
        {
            this.IsValid = true;
            this.owner = owner;
            this.memoryWrappers = new MemoryOwnerWrapper[owner.Count];

            for (int i = 0; i < owner.Count; i++)
            {
                this.memoryWrappers[i] = new MemoryOwnerWrapper(this, i);
            }
        }

        public int Count => this.owner.Count;

        public int BufferLength => this.owner.BufferLength;

        public long TotalLength => this.owner.TotalLength;

        public bool IsValid { get; internal set; }

        public Memory<T> this[int index] => throw new NotImplementedException();

        public IEnumerator<Memory<T>> GetEnumerator() => throw new NotImplementedException();

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

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
                if (!this.view.IsValid)
                {
                    throw new InvalidOperationException();
                }

                return this.view[this.index].Span;
            }

            public override MemoryHandle Pin(int elementIndex = 0) => throw new NotImplementedException();

            public override void Unpin() => throw new NotImplementedException();
        }
    }
}
