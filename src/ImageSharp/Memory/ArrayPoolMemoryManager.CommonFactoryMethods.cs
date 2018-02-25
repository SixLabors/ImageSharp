namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Contains common factory methods and configuration constants.
    /// </summary>
    public partial class ArrayPoolMemoryManager
    {
        /// <summary>
        /// The default value for: maximum size of pooled arrays in bytes.
        /// Currently set to 24MB, which is equivalent to 8 megapixels of raw <see cref="Rgba32"/> data.
        /// </summary>
        internal const int DefaultMaxPooledBufferSizeInBytes = 24 * 1024 * 1024;

        /// <summary>
        /// The value for: The threshold to pool arrays in <see cref="largeArrayPool"/> which has less buckets for memory safety.
        /// </summary>
        private const int DefaultBufferSelectorThresholdInBytes = 8 * 1024 * 1024;

        /// <summary>
        /// The default bucket count for <see cref="largeArrayPool"/>.
        /// </summary>
        private const int DefaultLargePoolBucketCount = 6;

        /// <summary>
        /// The default bucket count for <see cref="normalArrayPool"/>.
        /// </summary>
        private const int DefaultNormalPoolBucketCount = 16;

        /// <summary>
        /// This is the default. Should be good for most use cases.
        /// </summary>
        /// <returns>The memory manager</returns>
        public static ArrayPoolMemoryManager CreateDefault()
        {
            return new ArrayPoolMemoryManager(
                DefaultMaxPooledBufferSizeInBytes,
                DefaultBufferSelectorThresholdInBytes,
                DefaultLargePoolBucketCount,
                DefaultNormalPoolBucketCount);
        }

        /// <summary>
        /// For environments with limited memory capabilities. Only small images are pooled, which can result in reduced througput.
        /// </summary>
        /// <returns>The memory manager</returns>
        public static ArrayPoolMemoryManager CreateWithModeratePooling()
        {
            return new ArrayPoolMemoryManager(1024 * 1024, 32 * 1024, 16, 24);
        }

        /// <summary>
        /// Only pool small buffers like image rows.
        /// </summary>
        /// <returns>The memory manager</returns>
        public static ArrayPoolMemoryManager CreateWithMinimalPooling()
        {
            return new ArrayPoolMemoryManager(64 * 1024, 32 * 1024, 8, 24);
        }

        /// <summary>
        /// RAM is not an issue for me, gimme maximum througput!
        /// </summary>
        /// <returns>The memory manager</returns>
        public static ArrayPoolMemoryManager CreateWithAggressivePooling()
        {
            return new ArrayPoolMemoryManager(128 * 1024 * 1024, 32 * 1024 * 1024, 16, 32);
        }
    }
}