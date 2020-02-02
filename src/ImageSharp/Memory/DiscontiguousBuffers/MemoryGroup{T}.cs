// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

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

        private MemoryGroup(int bufferLength, long totalLength)
        {
            this.BufferLength = bufferLength;
            this.TotalLength = totalLength;
        }

        public abstract int Count { get; }

        public int BufferLength { get; }

        public long TotalLength { get; }

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

            int blockCapacityInElements = allocator.GetBufferCapacityInBytes() / ElementSize;
            if (blockAlignment > blockCapacityInElements)
            {
                throw new InvalidMemoryOperationException();
            }

            int numberOfAlignedSegments = blockCapacityInElements / blockAlignment;
            int bufferLength = numberOfAlignedSegments * blockAlignment;
            if (totalLength > 0 && totalLength < bufferLength)
            {
                bufferLength = (int)totalLength;
            }

            int sizeOfLastBuffer = (int)(totalLength % bufferLength);
            long bufferCount = totalLength / bufferLength;

            if (sizeOfLastBuffer == 0)
            {
                sizeOfLastBuffer = bufferLength;
            }
            else
            {
                bufferCount++;
            }

            var buffers = new IMemoryOwner<T>[bufferCount];
            for (int i = 0; i < buffers.Length - 1; i++)
            {
                buffers[i] = allocator.Allocate<T>(bufferLength, allocationOptions);
            }

            if (bufferCount > 0)
            {
                buffers[^1] = allocator.Allocate<T>(sizeOfLastBuffer, allocationOptions);
            }

            return new Owned(buffers, bufferLength, totalLength);
        }

        public static MemoryGroup<T> Wrap(params Memory<T>[] source) => Wrap(source.AsMemory());

        public static MemoryGroup<T> Wrap(ReadOnlyMemory<Memory<T>> source)
        {
            int bufferLength = source.Length > 0 ? source.Span[0].Length : 0;
            for (int i = 1; i < source.Length - 1; i++)
            {
                if (source.Span[i].Length != bufferLength)
                {
                    throw new InvalidMemoryOperationException("Wrap: buffers should be uniformly sized!");
                }
            }

            if (source.Length > 0 && source.Span[^1].Length > bufferLength)
            {
                throw new InvalidMemoryOperationException("Wrap: the last buffer is too large!");
            }

            long totalLength = bufferLength > 0 ? ((long)bufferLength * (source.Length - 1)) + source.Span[^1].Length : 0;

            return new Consumed(source, bufferLength, totalLength);
        }

        // Analogous to current MemorySource.SwapOrCopyContent()
        public static void SwapOrCopyContent(MemoryGroup<T> destination, MemoryGroup<T> source)
        {
            throw new NotImplementedException();
        }
    }
}
