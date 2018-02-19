namespace SixLabors.ImageSharp.Memory
{
    internal class ManagedByteBuffer : Buffer<byte>, IManagedByteBuffer
    {
        internal ManagedByteBuffer(byte[] array, int length, MemoryManager memoryManager)
            : base(array, length, memoryManager)
        {
        }

        public byte[] Array => this.array;
    }
}