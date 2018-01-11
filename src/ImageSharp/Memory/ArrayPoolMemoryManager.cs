using System.Buffers;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Implements <see cref="MemoryManager"/> by allocating memory from <see cref="ArrayPool{T}"/>.
    /// </summary>
    public class ArrayPoolMemoryManager : MemoryManager
    {
        /// <inheritdoc />
        internal override Buffer<T> Allocate<T>(int size, bool clear = false)
        {
            var buffer = new Buffer<T>(PixelDataPool<T>.Rent(size), size, this);
            if (clear)
            {
                buffer.Clear();
            }

            return buffer;
        }

        /// <inheritdoc />
        internal override void Release<T>(Buffer<T> buffer)
        {
            PixelDataPool<T>.Return(buffer.Array);
        }
    }
}