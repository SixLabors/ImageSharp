using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Represents discontinuous group of multiple uniformly-sized memory segments.
    /// The underlying buffers may change with time, therefore it's not safe to expose them directly on
    /// <see cref="Image{TPixel}"/> and <see cref="ImageFrame{TPixel}"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    internal abstract partial class MemoryGroup<T> : IMemoryGroup<T>, IDisposable
        where T : struct
    {
        private static readonly int ElementSize = Unsafe.SizeOf<T>();

        private MemoryGroup(int bufferSize) => this.BufferSize = bufferSize;

        public abstract int Count { get; }

        public int BufferSize { get; }

        public bool IsValid { get; private set; } = true;

        public abstract Memory<T> this[int index] { get; }

        public abstract void Dispose();

        public abstract IEnumerator<Memory<T>> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        // bufferLengthAlignment == image.Width in row-major images
        public static MemoryGroup<T> Allocate(
            MemoryAllocator allocator,
            long totalLength,
            int blockAlignment,
            AllocationOptions allocationOptions = AllocationOptions.None)
        {
            Guard.NotNull(allocator, nameof(allocator));
            Guard.MustBeGreaterThanOrEqualTo(totalLength, 0, nameof(totalLength));
            Guard.MustBeGreaterThan(blockAlignment, 0, nameof(blockAlignment));

            int blockCapacityInElements = allocator.GetBufferCapacity() / ElementSize;
            if (blockAlignment > blockCapacityInElements)
            {
                throw new InvalidMemoryOperationException();
            }

            int numberOfAlignedSegments = blockCapacityInElements / blockAlignment;
            int bufferSize = numberOfAlignedSegments * blockAlignment;
            if (totalLength > 0 && totalLength < bufferSize)
            {
                bufferSize = (int)totalLength;
            }

            int sizeOfLastBuffer = (int)(totalLength % bufferSize);
            long bufferCount = totalLength / bufferSize;

            if (sizeOfLastBuffer == 0)
            {
                sizeOfLastBuffer = bufferSize;
            }
            else
            {
                bufferCount++;
            }

            var buffers = new IMemoryOwner<T>[bufferCount];
            for (int i = 0; i < buffers.Length - 1; i++)
            {
                buffers[i] = allocator.Allocate<T>(bufferSize, allocationOptions);
            }

            if (bufferCount > 0)
            {
                buffers[^1] = allocator.Allocate<T>(sizeOfLastBuffer, allocationOptions);
            }

            return new Owned(buffers, bufferSize);
        }

        public static MemoryGroup<T> Wrap(params Memory<T>[] source) => Wrap(source.AsMemory());

        public static MemoryGroup<T> Wrap(ReadOnlyMemory<Memory<T>> source)
        {
            int bufferSize = source.Length > 0 ? source.Span[0].Length : 0;
            for (int i = 1; i < source.Length - 1; i++)
            {
                if (source.Span[i].Length != bufferSize)
                {
                    throw new InvalidMemoryOperationException("Wrap: buffers should be uniformly sized!");
                }
            }

            if (source.Length > 0 && source.Span[^1].Length > bufferSize)
            {
                throw new InvalidMemoryOperationException("Wrap: the last buffer is too large!");
            }

            return new Consumed(source, bufferSize);
        }

        // Analogous to current MemorySource.SwapOrCopyContent()
        public static void SwapOrCopyContent(MemoryGroup<T> destination, MemoryGroup<T> source)
        {
            throw new NotImplementedException();
        }
    }
}
