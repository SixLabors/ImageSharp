// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;

namespace SixLabors.Memory
{
    /// <summary>
    /// Holds a <see cref="System.Memory{T}"/> that is either OWNED or CONSUMED.
    /// Implements content transfer logic in  <see cref="SwapOrCopyContent"/> that depends on the ownership status.
    /// This is needed to transfer the contents of a temporary <see cref="Buffer2D{T}"/> to a persistent <see cref="SixLabors.ImageSharp.ImageFrame{T}.PixelBuffer"/>
    /// </summary>
    internal struct BufferManager<T> : IDisposable
    {
        public BufferManager(IMemoryOwner<T> memoryOwner)
        {
            this.MemoryOwner = memoryOwner;
            this.Memory = memoryOwner.Memory;
        }

        public BufferManager(Memory<T> memory)
        {
            this.Memory = memory;
            this.MemoryOwner = null;
        }

        public IMemoryOwner<T> MemoryOwner { get; private set; }

        public Memory<T> Memory { get; private set; }

        public bool OwnsMemory => this.MemoryOwner != null;

        /// <summary>
        /// Swaps the contents of 'destination' with 'source' if the buffers are owned (1),
        /// copies the contents of 'source' to 'destination' otherwise (2). Buffers should be of same size in case 2!
        /// </summary>
        public static void SwapOrCopyContent(ref BufferManager<T> destination, ref BufferManager<T> source)
        {
            if (source.OwnsMemory && destination.OwnsMemory)
            {
                SwapContents(ref destination, ref source);
            }
            else
            {
                if (destination.Memory.Length != source.Memory.Length)
                {
                    throw new InvalidOperationException("SwapOrCopyContents(): buffers should both owned or the same size!");
                }

                source.Memory.CopyTo(destination.Memory);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.MemoryOwner?.Dispose();
        }

        private static void SwapContents(ref BufferManager<T> a, ref BufferManager<T> b)
        {
            IMemoryOwner<T> tempOwner = a.MemoryOwner;
            Memory<T> tempMemory = a.Memory;

            a.MemoryOwner = b.MemoryOwner;
            a.Memory = b.Memory;

            b.MemoryOwner = tempOwner;
            b.Memory = tempMemory;
        }
    }
}