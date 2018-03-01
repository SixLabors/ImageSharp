namespace SixLabors.ImageSharp.Memory
{
    internal class BasicByteBuffer : BasicArrayBuffer<byte>, IManagedByteBuffer
    {
        internal BasicByteBuffer(byte[] array)
            : base(array)
        {
        }
    }
}