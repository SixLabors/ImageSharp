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

            private readonly ArrayPool<byte> sourcePool;

            public Buffer(byte[] data, int length, ArrayPool<byte> sourcePool)
            {
                this.Data = data;
                this.length = length;
                this.sourcePool = sourcePool;
            }

            protected byte[] Data { get; private set; }

            public Span<T> Span => this.Data.AsSpan().NonPortableCast<byte, T>().Slice(0, this.length);

            public void Dispose()
            {
                if (this.Data == null)
                {
                    return;
                }

                this.sourcePool.Return(this.Data);
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