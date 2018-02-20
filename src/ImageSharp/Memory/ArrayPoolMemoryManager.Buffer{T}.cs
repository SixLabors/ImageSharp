using System;

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
            private readonly ArrayPoolMemoryManager memoryManager;

            private readonly int length;

            public Buffer(byte[] data, int length, ArrayPoolMemoryManager memoryManager)
            {
                this.memoryManager = memoryManager;
                this.Data = data;
                this.length = length;
            }

            protected byte[] Data { get; private set; }

            public Span<T> Span => this.Data.AsSpan().NonPortableCast<byte, T>().Slice(0, this.length);

            public void Dispose()
            {
                if (this.Data == null)
                {
                    return;
                }

                this.memoryManager.pool.Return(this.Data);
                this.Data = null;
            }
        }

        private class ManagedByteBuffer : Buffer<byte>, IManagedByteBuffer
        {
            public ManagedByteBuffer(byte[] data, int length, ArrayPoolMemoryManager memoryManager)
                : base(data, length, memoryManager)
            {
            }

            public byte[] Array => this.Data;
        }
    }
}