namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Implements <see cref="MemoryManager"/> by allocating new buffers on every call.
    /// </summary>
    public class SimpleManagedMemoryManager : MemoryManager
    {
        /// <inheritdoc />
        internal override Buffer<T> Allocate<T>(int size, bool clear)
        {
            return new Buffer<T>(new T[size], size);
        }

        /// <inheritdoc />
        internal override void Release<T>(Buffer<T> buffer)
        {
        }
    }
}
