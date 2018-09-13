using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using SixLabors.ImageSharp.Memory;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
    internal readonly struct RowInterval
    {
        public RowInterval(int min, int max)
        {
            this.Min = min;
            this.Max = max;
        }

        /// <summary>
        /// Gets the INCLUSIVE minimum
        /// </summary>
        public int Min { get; }

        /// <summary>
        /// Gets the EXCLUSIVE maximum
        /// </summary>
        public int Max { get; }
    }

    internal static class ParallelHelper
    {
        public static void IterateRows(in Rectangle rectangle, Configuration configuration, Action<RowInterval> body)
        {
            int maxSteps = (int)Math.Ceiling(
                (float)(rectangle.Width * rectangle.Height) / configuration.MinimumPixelsProcessedPerTask);

            int numOfSteps = Math.Min(configuration.MaxDegreeOfParallelism, maxSteps);

            int step = rectangle.Height / numOfSteps;

            var parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = numOfSteps };

            int bottom = rectangle.Bottom;

            Parallel.For(
                0,
                numOfSteps,
                parallelOptions,
                i =>
                    {
                        int yMin = i * step;
                        int yMax = Math.Min(yMin + step, bottom);

                        var rowInterval = new RowInterval(yMin, yMax);
                        body(rowInterval);
                    });
        }
    }

    /// <summary>
    /// Utility methods for Parallel.For() execution. Use this instead of raw <see cref="Parallel"/> calls!
    /// </summary>
    internal static class ParallelFor
    {
        /// <summary>
        /// Helper method to execute Parallel.For using the settings in <paramref name="configuration"/>
        /// </summary>
        public static void WithConfiguration(int fromInclusive, int toExclusive, Configuration configuration, Action<int> body)
        {
            Parallel.For(fromInclusive, toExclusive, configuration.GetParallelOptions(), body);
        }

        /// <summary>
        /// Helper method to execute Parallel.For with temporary worker buffer shared between executing tasks.
        /// The buffer is not guaranteed to be clean!
        /// </summary>
        /// <typeparam name="T">The value type of the buffer</typeparam>
        /// <param name="fromInclusive">The start index, inclusive.</param>
        /// <param name="toExclusive">The end index, exclusive.</param>
        /// <param name="configuration">The <see cref="Configuration"/> used for getting the <see cref="MemoryAllocator"/> and <see cref="ParallelOptions"/></param>
        /// <param name="bufferLength">The length of the requested parallel buffer</param>
        /// <param name="body">The delegate that is invoked once per iteration.</param>
        public static void WithTemporaryBuffer<T>(
            int fromInclusive,
            int toExclusive,
            Configuration configuration,
            int bufferLength,
            Action<int, IMemoryOwner<T>> body)
            where T : struct
        {
            MemoryAllocator memoryAllocator = configuration.MemoryAllocator;
            ParallelOptions parallelOptions = configuration.GetParallelOptions();

            IMemoryOwner<T> InitBuffer()
            {
                return memoryAllocator.Allocate<T>(bufferLength);
            }

            void CleanUpBuffer(IMemoryOwner<T> buffer)
            {
                buffer.Dispose();
            }

            IMemoryOwner<T> BodyFunc(int i, ParallelLoopState state, IMemoryOwner<T> buffer)
            {
                body(i, buffer);
                return buffer;
            }

            Parallel.For(fromInclusive, toExclusive, parallelOptions, InitBuffer, BodyFunc, CleanUpBuffer);
        }
    }
}