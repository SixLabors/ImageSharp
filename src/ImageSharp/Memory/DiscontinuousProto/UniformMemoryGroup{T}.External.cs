using System;
using System.Collections.Generic;

namespace SixLabors.ImageSharp.Memory.DiscontinuousProto
{
    public abstract partial class UniformMemoryGroup<T>
    {
        private class External : UniformMemoryGroup<T>
        {
            private readonly ReadOnlyMemory<Memory<T>> source;

            public External(ReadOnlyMemory<Memory<T>> source)
            {
                // TODO: sizes should be uniform, validate!

                this.source = source;
            }

            public override IEnumerator<Memory<T>> GetEnumerator()
            {
                for (int i = 0; i < this.source.Length; i++)
                {
                    yield return this.source.Span[i];
                }
            }

            public override int Count => this.source.Length;

            public override Memory<T> this[int index] => this.source.Span[index];

            public override void Dispose()
            {
                // No ownership nothing to dispose
            }
        }
    }
}
