namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Implements <see cref="MemoryAllocator"/> by newing up arrays by the GC on every allocation requests.
    /// </summary>
    public class SimpleGcMemoryAllocator : MemoryAllocator
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
