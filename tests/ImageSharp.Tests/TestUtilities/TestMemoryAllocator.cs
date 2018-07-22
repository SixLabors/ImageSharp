using System;
using System.Runtime.InteropServices;

using SixLabors.Memory;

namespace SixLabors.ImageSharp.Tests.Memory
{
    internal class TestMemoryAllocator : MemoryAllocator
    {
        public TestMemoryAllocator(byte dirtyValue = 42)
        {
            this.DirtyValue = dirtyValue;
        }

        /// <summary>
        /// The value to initilazie the result buffer with, with non-clean options (<see cref="AllocationOptions.None"/>)
        /// </summary>
        public byte DirtyValue { get; }

        internal override IBuffer<T> Allocate<T>(int length, AllocationOptions options = AllocationOptions.None)
        {
            T[] array = this.AllocateArray<T>(length, options);

            return new BasicArrayBuffer<T>(array, length);
        }

        internal override IManagedByteBuffer AllocateManagedByteBuffer(int length, AllocationOptions options = AllocationOptions.None)
        {
            byte[] array = this.AllocateArray<byte>(length, options);
            return new ManagedByteBuffer(array);
        }
        
        private T[] AllocateArray<T>(int length, AllocationOptions options)
            where T : struct
        {
            var array = new T[length + 42];

            if (options == AllocationOptions.None)
            {
                Span<byte> data = MemoryMarshal.Cast<T, byte>(array.AsSpan());
                data.Fill(this.DirtyValue);
            }

            return array;
        }

        private class ManagedByteBuffer : BasicArrayBuffer<byte>, IManagedByteBuffer
        {
            public ManagedByteBuffer(byte[] array)
                : base(array)
            {
            }
        }
    }
}