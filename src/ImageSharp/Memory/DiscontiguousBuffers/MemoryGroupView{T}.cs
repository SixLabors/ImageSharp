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
    internal class MemoryGroupView<T> : IMemoryGroup<T> where T : struct
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

        public IEnumerator<Memory<T>> GetEnumerator() => throw new NotImplementedException();

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public int Count { get; }

        public Memory<T> this[int index] => throw new NotImplementedException();

        public int BufferSize => this.owner.BufferSize;

        public bool IsValid { get; internal set; }

        class MemoryOwnerWrapper : MemoryManager<T>
        {
            private MemoryGroupView<T> view;

            private int index;

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
