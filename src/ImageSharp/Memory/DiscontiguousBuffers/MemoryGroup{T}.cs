// Copyright (c) Six Labors.
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

        /// <inheritdoc />
        public abstract int Count { get; }

        /// <inheritdoc />
        public int BufferLength { get; private set; }

        /// <inheritdoc />
        public long TotalLength { get; private set; }

        /// <inheritdoc />
        public bool IsValid { get; private set; } = true;

        public MemoryGroupView<T> View { get; private set; }

        /// <inheritdoc />
        public abstract Memory<T> this[int index] { get; }

        /// <inheritdoc />
        public abstract void Dispose();

        /// <inheritdoc />
        public abstract MemoryGroupEnumerator<T> GetEnumerator();

        /// <inheritdoc />
        IEnumerator<Memory<T>> IEnumerable<Memory<T>>.GetEnumerator()
        {
            /* This method is implemented in each derived class.
             * Implementing the method here as non-abstract and throwing,
             * then reimplementing it explicitly in each derived class, is
             * a workaround for the lack of support for abstract explicit
             * interface method implementations in C#. */
            throw new NotImplementedException($"The type {this.GetType()} needs to override IEnumerable<Memory<T>>.GetEnumerator()");
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<Memory<T>>)this).GetEnumerator();

        /// <summary>
        /// Creates a new memory group, allocating it's buffers with the provided allocator.
        /// </summary>
        /// <param name="allocator">The <see cref="MemoryAllocator"/> to use.</param>
        /// <param name="totalLength">The total length of the buffer.</param>
        /// <param name="bufferAlignment">The expected alignment (eg. to make sure image rows fit into single buffers).</param>
        /// <param name="options">The <see cref="AllocationOptions"/>.</param>
        /// <returns>A new <see cref="MemoryGroup{T}"/>.</returns>
        /// <exception cref="InvalidMemoryOperationException">Thrown when 'blockAlignment' converted to bytes is greater than the buffer capacity of the allocator.</exception>
        public static MemoryGroup<T> Allocate(
            MemoryAllocator allocator,
            long totalLength,
            int bufferAlignment,
            AllocationOptions options = AllocationOptions.None)
        {
            Guard.NotNull(allocator, nameof(allocator));
            Guard.MustBeGreaterThanOrEqualTo(totalLength, 0, nameof(totalLength));
            Guard.MustBeGreaterThanOrEqualTo(bufferAlignment, 0, nameof(bufferAlignment));

            int blockCapacityInElements = allocator.GetBufferCapacityInBytes() / ElementSize;

            if (bufferAlignment > blockCapacityInElements)
            {
                throw new InvalidMemoryOperationException(
                    $"The buffer capacity of the provided MemoryAllocator is insufficient for the requested buffer alignment: {bufferAlignment}.");
            }

            if (totalLength == 0)
            {
                var buffers0 = new IMemoryOwner<T>[1] { allocator.Allocate<T>(0, options) };
                return new Owned(buffers0, 0, 0, true);
            }

            int numberOfAlignedSegments = blockCapacityInElements / bufferAlignment;
            int bufferLength = numberOfAlignedSegments * bufferAlignment;
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
                buffers[i] = allocator.Allocate<T>(bufferLength, options);
            }

            if (bufferCount > 0)
            {
                buffers[buffers.Length - 1] = allocator.Allocate<T>(sizeOfLastBuffer, options);
            }

            return new Owned(buffers, bufferLength, totalLength, true);
        }

        public static MemoryGroup<T> Wrap(params Memory<T>[] source)
        {
            int bufferLength = source.Length > 0 ? source[0].Length : 0;
            for (int i = 1; i < source.Length - 1; i++)
            {
                if (source[i].Length != bufferLength)
                {
                    throw new InvalidMemoryOperationException("Wrap: buffers should be uniformly sized!");
                }
            }

            if (source.Length > 0 && source[source.Length - 1].Length > bufferLength)
            {
                throw new InvalidMemoryOperationException("Wrap: the last buffer is too large!");
            }

            long totalLength = bufferLength > 0 ? ((long)bufferLength * (source.Length - 1)) + source[source.Length - 1].Length : 0;

            return new Consumed(source, bufferLength, totalLength);
        }

        public static MemoryGroup<T> Wrap(params IMemoryOwner<T>[] source)
        {
            int bufferLength = source.Length > 0 ? source[0].Memory.Length : 0;
            for (int i = 1; i < source.Length - 1; i++)
            {
                if (source[i].Memory.Length != bufferLength)
                {
                    throw new InvalidMemoryOperationException("Wrap: buffers should be uniformly sized!");
                }
            }

            if (source.Length > 0 && source[source.Length - 1].Memory.Length > bufferLength)
            {
                throw new InvalidMemoryOperationException("Wrap: the last buffer is too large!");
            }

            long totalLength = bufferLength > 0 ? ((long)bufferLength * (source.Length - 1)) + source[source.Length - 1].Memory.Length : 0;

            return new Owned(source, bufferLength, totalLength, false);
        }

        /// <summary>
        /// Swaps the contents of 'target' with 'source' if the buffers are allocated (1),
        /// copies the contents of 'source' to 'target' otherwise (2).
        /// Groups should be of same TotalLength in case 2.
        /// </summary>
        public static bool SwapOrCopyContent(MemoryGroup<T> target, MemoryGroup<T> source)
        {
            if (source is Owned ownedSrc && ownedSrc.Swappable &&
                target is Owned ownedTarget && ownedTarget.Swappable)
            {
                Owned.SwapContents(ownedTarget, ownedSrc);
                return true;
            }
            else
            {
                if (target.TotalLength != source.TotalLength)
                {
                    throw new InvalidMemoryOperationException(
                        "Trying to copy/swap incompatible buffers. This is most likely caused by applying an unsupported processor to wrapped-memory images.");
                }

                source.CopyTo(target);
                return false;
            }
        }
    }
}
