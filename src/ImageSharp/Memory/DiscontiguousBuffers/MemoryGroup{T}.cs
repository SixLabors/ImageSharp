using System;
using System.Collections;
using System.Collections.Generic;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Represents discontinuous group of multiple uniformly-sized memory segments.
    /// The underlying buffers may change with time, therefore it's not safe to expose them directly on
    /// <see cref="Image{TPixel}"/> and <see cref="ImageFrame{TPixel}"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    internal abstract partial class MemoryGroup<T> : IMemoryGroup<T>, IDisposable where T : struct
    {
        public abstract IEnumerator<Memory<T>> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public abstract int Count { get; }

        public abstract Memory<T> this[int index] { get; }

        public abstract void Dispose();

        public int BlockSize { get; }

        public bool IsValid { get; protected set; }

        // bufferLengthAlignment == image.Width in row-major images
        public static MemoryGroup<T> Allocate(MemoryAllocator allocator,
            long totalLength,
            int blockAlignment,
            AllocationOptions allocationOptions = AllocationOptions.None)
        {
            long bufferCount = totalLength / allocator.GetBlockCapacity();

            // TODO: Adjust bufferCount, and calculate the uniform buffer length with respect to bufferLengthAlignment, and allocate bufferCount buffers
            throw new NotImplementedException();
        }

        public static MemoryGroup<T> Wrap(params Memory<T>[] source) => Wrap(source.AsMemory());

        public static MemoryGroup<T> Wrap(ReadOnlyMemory<Memory<T>> source)
        {
            return new Consumed(source);
        }

        // Analogous to current MemorySource.SwapOrCopyContent()
        public static void SwapOrCopyContent(MemoryGroup<T> destination, MemoryGroup<T> source)
        {
            throw new NotImplementedException();
        }
    }
}
