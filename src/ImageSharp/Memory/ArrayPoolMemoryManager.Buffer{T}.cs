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
        private class Buffer<T> : IBuffer<T>
            where T : struct
        {
            private readonly int length;

            private WeakReference<ArrayPool<byte>> sourcePoolReference;

            public Buffer(byte[] data, int length, ArrayPool<byte> sourcePool)
            {
                this.Data = data;
                this.length = length;
                this.sourcePoolReference = new WeakReference<ArrayPool<byte>>(sourcePool);
            }

            protected byte[] Data { get; private set; }

            public Span<T> Span => this.Data.AsSpan().NonPortableCast<byte, T>().Slice(0, this.length);

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