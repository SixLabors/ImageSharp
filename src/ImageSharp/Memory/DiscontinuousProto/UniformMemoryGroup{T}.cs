using System;
using System.Collections;
using System.Collections.Generic;

namespace SixLabors.ImageSharp.Memory.DiscontinuousProto
{
    /// <summary>
    /// Represents a group of one or more uniformly-sized discontinuous memory segments, owned by this instance.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    public abstract partial class UniformMemoryGroup<T> : IUniformMemoryGroup<T>, IDisposable where T : struct
    {
        public abstract IEnumerator<Memory<T>> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public abstract int Count { get; }

        public abstract Memory<T> this[int index] { get; }

        public abstract void Dispose();

        public bool IsValid { get; protected set; }

        public static UniformMemoryGroup<T> Allocate(MemoryAllocator allocator, long length)
        {
            long bufferCount = length / allocator.GetMaximumContiguousBufferLength();

            // TODO: Allocate bufferCount buffers
            throw new NotImplementedException();
        }

        public static UniformMemoryGroup<T> Wrap(params Memory<T>[] source) => Wrap(source.AsMemory());

        public static UniformMemoryGroup<T> Wrap(ReadOnlyMemory<Memory<T>> source)
        {
            return new Consumed(source);
        }

        // Analogous to current MemorySource.SwapOrCopyContent()
        public static void SwapOrCopyContent(UniformMemoryGroup<T> destination, UniformMemoryGroup<T> source)
        {
            throw new NotImplementedException();
        }
    }
}
