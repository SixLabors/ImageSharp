// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;

namespace SixLabors.ImageSharp.Memory
{
    internal abstract partial class MemoryGroup<T>
    {
        // Analogous to the "consumed" variant of MemorySource
        private sealed class Consumed : MemoryGroup<T>
        {
            private readonly Memory<T>[] source;

            public Consumed(Memory<T>[] source, int bufferLength, long totalLength)
                : base(bufferLength, totalLength)
            {
                this.source = source;
                this.View = new MemoryGroupView<T>(this);
            }

            public override int Count => this.source.Length;

            public override Memory<T> this[int index] => this.source[index];

            public override IEnumerator<Memory<T>> GetEnumerator()
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
