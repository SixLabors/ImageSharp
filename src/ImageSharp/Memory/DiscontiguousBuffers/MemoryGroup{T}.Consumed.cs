// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Memory
{
    internal abstract partial class MemoryGroup<T>
    {
        /// <summary>
        /// A <see cref="MemoryGroup{T}"/> implementation that consumes the underlying memory buffers.
        /// </summary>
        public sealed class Consumed : MemoryGroup<T>, IEnumerable<Memory<T>>
        {
            private readonly Memory<T>[] source;

            public Consumed(Memory<T>[] source, int bufferLength, long totalLength)
                : base(bufferLength, totalLength)
            {
                this.source = source;
                this.View = new MemoryGroupView<T>(this);
            }

            public override int Count
            {
                [MethodImpl(InliningOptions.ShortMethod)]
                get => this.source.Length;
            }

            public override Memory<T> this[int index] => this.source[index];

            /// <inheritdoc/>
            [MethodImpl(InliningOptions.ShortMethod)]
            public override MemoryGroupEnumerator<T> GetEnumerator()
            {
                return new MemoryGroupEnumerator<T>(this);
            }

            /// <inheritdoc/>
            IEnumerator<Memory<T>> IEnumerable<Memory<T>>.GetEnumerator()
            {
                /* The runtime sees the Array class as if it implemented the
                 * type-generic collection interfaces explicitly, so here we
                 * can just cast the source array to IList<Memory<T>> (or to
                 * an equivalent type), and invoke the generic GetEnumerator
                 * method directly from that interface reference. This saves
                 * having to create our own iterator block here. */
                return ((IList<Memory<T>>)this.source).GetEnumerator();
            }

            public override void Dispose()
            {
                this.View.Invalidate();
            }
        }
    }
}
