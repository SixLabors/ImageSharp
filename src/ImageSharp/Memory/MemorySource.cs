// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;

using SixLabors.Memory;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Holds a <see cref="System.Memory{T}"/> that is either OWNED or CONSUMED.
    /// When the memory is being owned, the <see cref="IMemoryOwner{T}"/> instance is also known.
    /// Implements content transfer logic in  <see cref="SwapOrCopyContent"/> that depends on the ownership status.
    /// This is needed to transfer the contents of a temporary <see cref="Buffer2D{T}"/>
    /// to a persistent <see cref="SixLabors.ImageSharp.ImageFrame{T}.PixelBuffer"/> without copying the buffer.
    /// </summary>
    /// <remarks>
    /// For a deeper understanding of the owner/consumer model, check out the following docs: <br/>
    /// https://gist.github.com/GrabYourPitchforks/4c3e1935fd4d9fa2831dbfcab35dffc6
    /// https://www.codemag.com/Article/1807051/Introducing-.NET-Core-2.1-Flagship-Types-Span-T-and-Memory-T
    /// </remarks>
    internal struct MemorySource<T> : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MemorySource{T}"/> struct
        /// by wrapping an existing <see cref="IMemoryOwner{T}"/>.
        /// </summary>
        /// <param name="memoryOwner">The <see cref="IMemoryOwner{T}"/> to wrap</param>
        /// <param name="isInternalMemorySource">
        /// A value indicating whether <paramref name="memoryOwner"/> is an internal memory source managed by ImageSharp.
        /// Eg. allocated by a <see cref="MemoryAllocator"/>.
        /// </param>
        public MemorySource(IMemoryOwner<T> memoryOwner, bool isInternalMemorySource)
        {
            this.MemoryOwner = memoryOwner;
            this.Memory = memoryOwner.Memory;
            this.HasSwappableContents = isInternalMemorySource;
        }

        public MemorySource(Memory<T> memory)
        {
            this.Memory = memory;
            this.MemoryOwner = null;
            this.HasSwappableContents = false;
        }

        public IMemoryOwner<T> MemoryOwner { get; private set; }

        public Memory<T> Memory { get; private set; }

        /// <summary>
        /// Gets a value indicating whether we are allowed to swap the contents of this buffer
        /// with an other <see cref="MemorySource{T}"/> instance.
        /// The value is true only and only if <see cref="MemoryOwner"/> is present,
        /// and it's coming from an internal source managed by ImageSharp (<see cref="MemoryAllocator"/>).
        /// </summary>
        public bool HasSwappableContents { get; }

        public Span<T> GetSpan() => this.Memory.Span;

        public void Clear() => this.Memory.Span.Clear();

        /// <summary>
        /// Swaps the contents of 'destination' with 'source' if the buffers are owned (1),
        /// copies the contents of 'source' to 'destination' otherwise (2). Buffers should be of same size in case 2!
        /// </summary>
        public static void SwapOrCopyContent(ref MemorySource<T> destination, ref MemorySource<T> source)
        {
            if (source.HasSwappableContents && destination.HasSwappableContents)
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

        private static void SwapContents(ref MemorySource<T> a, ref MemorySource<T> b)
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