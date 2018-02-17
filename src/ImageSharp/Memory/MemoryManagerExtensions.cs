namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Extension methods for <see cref="MemoryManager"/>.
    /// </summary>
    internal static class MemoryManagerExtensions
    {
        /// <summary>
        /// Allocates a <see cref="Buffer{T}"/> of size <paramref name="size"/>.
        /// Note: Depending on the implementation, the buffer may not cleared before
        /// returning, so it may contain data from an earlier use.
        /// </summary>
        /// <typeparam name="T">Type of the data stored in the buffer</typeparam>
        /// <param name="memoryManager">The <see cref="MemoryManager"/></param>
        /// <param name="size">Size of the buffer to allocate</param>
        /// <returns>A buffer of values of type <typeparamref name="T"/>.</returns>
        public static Buffer<T> Allocate<T>(this MemoryManager memoryManager, int size)
            where T : struct
        {
            return memoryManager.Allocate<T>(size, false);
        }

        public static Buffer<T> AllocateClean<T>(this MemoryManager memoryManager, int size)
            where T : struct
        {
            return memoryManager.Allocate<T>(size, true);
        }

        public static Buffer2D<T> Allocate2D<T>(this MemoryManager memoryManager, int width, int height, bool clear)
            where T : struct
        {
            Buffer<T> buffer = memoryManager.Allocate<T>(width * height, clear);

            return new Buffer2D<T>(buffer, width, height);
        }

        public static Buffer2D<T> Allocate2D<T>(this MemoryManager memoryManager, int width, int height)
            where T : struct =>
            Allocate2D<T>(memoryManager, width, height, false);

        public static Buffer2D<T> AllocateClean2D<T>(this MemoryManager memoryManager, int width, int height)
            where T : struct =>
            Allocate2D<T>(memoryManager, width, height, true);
    }
}