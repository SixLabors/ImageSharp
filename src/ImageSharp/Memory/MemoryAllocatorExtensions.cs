using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Extension methods for <see cref="MemoryAllocator"/>.
    /// </summary>
    internal static class MemoryAllocatorExtensions
    {
        /// <summary>
        /// Allocates a <see cref="IBuffer{T}"/> of size <paramref name="length"/>.
        /// Note: Depending on the implementation, the buffer may not cleared before
        /// returning, so it may contain data from an earlier use.
        /// </summary>
        /// <typeparam name="T">Type of the data stored in the buffer</typeparam>
        /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/></param>
        /// <param name="length">Size of the buffer to allocate</param>
        /// <returns>A buffer of values of type <typeparamref name="T"/>.</returns>
        public static IBuffer<T> Allocate<T>(this MemoryAllocator memoryAllocator, int length)
            where T : struct
        {
            return memoryAllocator.Allocate<T>(length, false);
        }

        public static IBuffer<T> AllocateClean<T>(this MemoryAllocator memoryAllocator, int length)
            where T : struct
        {
            return memoryAllocator.Allocate<T>(length, true);
        }

        public static IManagedByteBuffer AllocateManagedByteBuffer(this MemoryAllocator memoryAllocator, int length)
        {
            return memoryAllocator.AllocateManagedByteBuffer(length, false);
        }

        public static IManagedByteBuffer AllocateCleanManagedByteBuffer(this MemoryAllocator memoryAllocator, int length)
        {
            return memoryAllocator.AllocateManagedByteBuffer(length, true);
        }

        public static Buffer2D<T> Allocate2D<T>(this MemoryAllocator memoryAllocator, int width, int height, bool clear)
            where T : struct
        {
            IBuffer<T> buffer = memoryAllocator.Allocate<T>(width * height, clear);

            return new Buffer2D<T>(buffer, width, height);
        }

        public static Buffer2D<T> Allocate2D<T>(this MemoryAllocator memoryAllocator, Size size)
            where T : struct =>
            Allocate2D<T>(memoryAllocator, size.Width, size.Height, false);

        public static Buffer2D<T> Allocate2D<T>(this MemoryAllocator memoryAllocator, int width, int height)
            where T : struct =>
            Allocate2D<T>(memoryAllocator, width, height, false);

        public static Buffer2D<T> AllocateClean2D<T>(this MemoryAllocator memoryAllocator, int width, int height)
            where T : struct =>
            Allocate2D<T>(memoryAllocator, width, height, true);

        /// <summary>
        /// Allocates padded buffers for BMP encoder/decoder. (Replacing old PixelRow/PixelArea)
        /// </summary>
        /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/></param>
        /// <param name="width">Pixel count in the row</param>
        /// <param name="pixelSizeInBytes">The pixel size in bytes, eg. 3 for RGB</param>
        /// <param name="paddingInBytes">The padding</param>
        /// <returns>A <see cref="IManagedByteBuffer"/></returns>
        public static IManagedByteBuffer AllocatePaddedPixelRowBuffer(
            this MemoryAllocator memoryAllocator,
            int width,
            int pixelSizeInBytes,
            int paddingInBytes)
        {
            int length = (width * pixelSizeInBytes) + paddingInBytes;
            return memoryAllocator.AllocateManagedByteBuffer(length);
        }
    }
}