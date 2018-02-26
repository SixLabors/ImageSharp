using System;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Utility methods for Parallel.For() execution. Use this instead of raw <see cref="Parallel"/> calls!
    /// </summary>
    internal static class ParallelFor
    {
        /// <summary>
        /// Helper method to execute Parallel.For using the settings in <see cref="Configuration.ParallelOptions"/>
        /// </summary>
        public static void WithConfiguration(int fromInclusive, int toExclusive, Configuration configuration, Action<int> body)
        {
            Parallel.For(fromInclusive, toExclusive, configuration.ParallelOptions, body);
        }

        /// <summary>
        /// Helper method to execute Parallel.For with temporal worker buffer in an optimized way.
        /// The buffer will be only instantiated for each worker Task, the contents are not cleaned automatically.
        /// </summary>
        /// <typeparam name="T">The value type of the buffer</typeparam>
        /// <param name="fromInclusive">The start index, inclusive.</param>
        /// <param name="toExclusive">The end index, exclusive.</param>
        /// <param name="configuration">The <see cref="Configuration"/> used for getting the <see cref="MemoryManager"/> and <see cref="ParallelOptions"/></param>
        /// <param name="bufferLength">The length of the requested parallel buffer</param>
        /// <param name="body">The delegate that is invoked once per iteration.</param>
        public static void WithTemporalBuffer<T>(
            int fromInclusive,
            int toExclusive,
            Configuration configuration,
            int bufferLength,
            Action<int, IBuffer<T>> body)
            where T : struct
        {
            MemoryManager memoryManager = configuration.MemoryManager;
            ParallelOptions parallelOptions = configuration.ParallelOptions;

            IBuffer<T> InitBuffer()
            {
                return memoryManager.Allocate<T>(bufferLength);
            }

            void CleanUpBuffer(IBuffer<T> buffer)
            {
                buffer.Dispose();
            }

            IBuffer<T> BodyFunc(int i, ParallelLoopState state, IBuffer<T> buffer)
            {
                body(i, buffer);
                return buffer;
            }

            Parallel.For(fromInclusive, toExclusive, parallelOptions, InitBuffer, BodyFunc, CleanUpBuffer);
        }
    }
}