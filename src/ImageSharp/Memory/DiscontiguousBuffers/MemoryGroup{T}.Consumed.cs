using System;
using System.Collections.Generic;

namespace SixLabors.ImageSharp.Memory
{
    internal abstract partial class MemoryGroup<T>
    {
        // Analogous to the "consumed" variant of MemorySource
        private class Consumed : MemoryGroup<T>
        {
            private readonly ReadOnlyMemory<Memory<T>> source;

            public Consumed(ReadOnlyMemory<Memory<T>> source, int bufferSize)
                : base(bufferSize)
            {
                this.source = source;
            }

            public override int Count => this.source.Length;

            public override Memory<T> this[int index] => this.source.Span[index];

            public override IEnumerator<Memory<T>> GetEnumerator()
            {
                for (int i = 0; i < this.source.Length; i++)
                {
                    yield return this.source.Span[i];
                }
            }

            public override void Dispose()
            {
                // No ownership nothing to dispose
            }
        }
    }
}
