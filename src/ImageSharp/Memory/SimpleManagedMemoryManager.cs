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
            return new Buffer<T>(new T[length], length, this);
        }

        internal override IManagedByteBuffer AllocateManagedByteBuffer(int length, bool clear)
        {
            return new ManagedByteBuffer(new byte[length], length, this);
        }

        /// <inheritdoc />
        internal override void Release<T>(Buffer<T> buffer)
        {
        }
    }
}
