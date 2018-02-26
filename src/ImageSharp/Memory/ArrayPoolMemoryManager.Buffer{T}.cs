// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Contains <see cref="Buffer{T}"/> and <see cref="ManagedByteBuffer"/>
    /// </summary>
    public partial class ArrayPoolMemoryManager
    {
        /// <summary>
        /// The buffer implementation of <see cref="ArrayPoolMemoryManager"/>
        /// </summary>
        private class Buffer<T> : IBuffer<T>
            where T : struct
        {
            /// <summary>
            /// The length of the buffer
            /// </summary>
            private readonly int length;

            /// <summary>
            /// A weak reference to the source pool.
            /// </summary>
            /// <remarks>
            /// By using a weak reference here, we are making sure that array pools and their retained arrays are always GC-ed
            /// after a call to <see cref="ArrayPoolMemoryManager.ReleaseRetainedResources"/>, regardless of having buffer instances still being in use.
            /// </remarks>
            private WeakReference<ArrayPool<byte>> sourcePoolReference;

            public Buffer(byte[] data, int length, ArrayPool<byte> sourcePool)
            {
                this.Data = data;
                this.length = length;
                this.sourcePoolReference = new WeakReference<ArrayPool<byte>>(sourcePool);
            }

            /// <summary>
            /// Gets the buffer as a byte array.
            /// </summary>
            protected byte[] Data { get; private set; }

            /// <inheritdoc />
            public Span<T> Span => this.Data.AsSpan().NonPortableCast<byte, T>().Slice(0, this.length);

            /// <inheritdoc />
            public void Dispose()
            {
                if (this.Data == null || this.sourcePoolReference == null)
                {
                    return;
                }

                if (this.sourcePoolReference.TryGetTarget(out ArrayPool<byte> pool))
                {
                    pool.Return(this.Data);
                }

                this.sourcePoolReference = null;
                this.Data = null;
            }
        }

        /// <summary>
        /// The <see cref="IManagedByteBuffer"/> implementation of <see cref="ArrayPoolMemoryManager"/>.
        /// </summary>
        private class ManagedByteBuffer : Buffer<byte>, IManagedByteBuffer
        {
            public ManagedByteBuffer(byte[] data, int length, ArrayPool<byte> sourcePool)
                : base(data, length, sourcePool)
            {
            }

            public byte[] Array => this.Data;
        }
    }
}