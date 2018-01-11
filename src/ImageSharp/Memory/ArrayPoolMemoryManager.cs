using System.Buffers;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Implements <see cref="MemoryManager"/> by allocating memory from <see cref="ArrayPool{T}"/>.
    /// </summary>
    public class ArrayPoolMemoryManager : MemoryManager
    {
        private readonly int minSizeBytes;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArrayPoolMemoryManager"/> class.
        /// By passing an integer greater than 0 as <paramref name="minSizeBytes"/>, a
        /// minimum threshold for pooled allocations is set. Any allocation requests that
        /// would require less size than the threshold will not be managed within the array pool.
        /// </summary>
        /// <param name="minSizeBytes">
        /// Minimum size, in bytes, before an array pool is used to satisfy the request.
        /// </param>
        public ArrayPoolMemoryManager(int minSizeBytes = 0)
        {
            this.minSizeBytes = minSizeBytes;
        }

        /// <inheritdoc />
        internal override Buffer<T> Allocate<T>(int size, bool clear = false)
        {
            if (this.minSizeBytes > 0 && size < this.minSizeBytes * SizeHelper<T>.Size)
            {
                return new Buffer<T>(new T[size], size);
            }

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

        internal static class SizeHelper<T>
        {
            static SizeHelper()
            {
                #if NETSTANDARD1_1
                Size = Marshal.SizeOf(typeof(T));
                #else
                Size = Marshal.SizeOf<T>();
                #endif
            }

            public static int Size { get; }
        }
    }
}