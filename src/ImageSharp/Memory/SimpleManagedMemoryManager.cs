namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Implements <see cref="MemoryManager"/> by allocating new buffers on every call.
    /// </summary>
    public class SimpleManagedMemoryManager : MemoryManager
    {
        /// <inheritdoc />
        internal override IBuffer<T> Allocate<T>(int length, bool clear)
        {
            return new BasicArrayBuffer<T>(new T[length]);
        }

        internal override IManagedByteBuffer AllocateManagedByteBuffer(int length, bool clear)
        {
            return new BasicByteBuffer(new byte[length]);
        }
    }
}
