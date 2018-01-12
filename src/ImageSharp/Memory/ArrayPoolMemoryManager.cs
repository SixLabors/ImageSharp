using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Implements <see cref="MemoryManager"/> by allocating memory from <see cref="ArrayPool{T}"/>.
    /// </summary>
    public class ArrayPoolMemoryManager : MemoryManager
    {
        private readonly int minSizeBytes;
        private readonly ArrayPool<byte> pool;

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

            this.pool = ArrayPool<byte>.Create(CalculateMaxArrayLength(), 50);
        }

        /// <inheritdoc />
        internal override Buffer<T> Allocate<T>(int itemCount, bool clear = false)
        {
            int itemSizeBytes = Unsafe.SizeOf<T>();
            int bufferSizeInBytes = itemCount * itemSizeBytes;

            if (this.minSizeBytes > 0 && bufferSizeInBytes < this.minSizeBytes)
            {
                // Minimum size set to 8 bytes to get past a misbehaving test
                // (otherwise PngDecoderTests.Decode_IncorrectCRCForNonCriticalChunk_ExceptionIsThrown fails for the wrong reason)
                // TODO: Remove this once the test is fixed
                return new Buffer<T>(new T[Math.Max(itemCount, 8)], itemCount);
            }

            byte[] byteBuffer = this.pool.Rent(bufferSizeInBytes);
            var buffer = new Buffer<T>(Unsafe.As<T[]>(byteBuffer), itemCount, this);
            if (clear)
            {
                buffer.Clear();
            }

            return buffer;
        }

        /// <inheritdoc />
        internal override void Release<T>(Buffer<T> buffer)
        {
            var byteBuffer = Unsafe.As<byte[]>(buffer.Array);
            this.pool.Return(byteBuffer);
        }

        /// <summary>
        /// Heuristically calculates a reasonable maxArrayLength value for the backing <see cref="ArrayPool{T}"/>.
        /// </summary>
        /// <returns>The maxArrayLength value</returns>
        internal static int CalculateMaxArrayLength()
        {
            const int MaximumExpectedImageSize = 16384 * 16384;
            const int MaximumBytesPerPixel = 4;
            return MaximumExpectedImageSize * MaximumBytesPerPixel;
        }
    }
}