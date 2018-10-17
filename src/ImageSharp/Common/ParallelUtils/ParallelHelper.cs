// Copyright(c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using SixLabors.ImageSharp.Memory;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.ParallelUtils
{
    /// <summary>
    /// Utility methods for batched processing of pixel row intervals.
    /// Parallel execution is optimized for image processing.
    /// Use this instead of direct <see cref="Parallel"/> calls!
    /// </summary>
    internal static class ParallelHelper
    {
        /// <summary>
        /// Get the default <see cref="ParallelExecutionSettings"/> for a <see cref="Configuration"/>
        /// </summary>
        public static ParallelExecutionSettings GetParallelSettings(this Configuration configuration)
        {
            return new ParallelExecutionSettings(configuration.MaxDegreeOfParallelism, configuration.MemoryAllocator);
        }

        /// <summary>
        /// Iterate through the rows of a rectangle in optimized batches defined by <see cref="RowInterval"/>-s.
        /// </summary>
        public static void IterateRows(Rectangle rectangle, Configuration configuration, Action<RowInterval> body)
        {
            ParallelExecutionSettings parallelSettings = configuration.GetParallelSettings();

            IterateRows(rectangle, parallelSettings, body);
        }

        /// <summary>
        /// Iterate through the rows of a rectangle in optimized batches defined by <see cref="RowInterval"/>-s.
        /// </summary>
        public static void IterateRows(
            Rectangle rectangle,
            in ParallelExecutionSettings parallelSettings,
            Action<RowInterval> body)
        {
            DebugGuard.MustBeGreaterThan(rectangle.Width, 0, nameof(rectangle));

            int maxSteps = DivideCeil(rectangle.Width * rectangle.Height, parallelSettings.MinimumPixelsProcessedPerTask);

            int numOfSteps = Math.Min(parallelSettings.MaxDegreeOfParallelism, maxSteps);

            // Avoid TPL overhead in this trivial case:
            if (numOfSteps == 1)
            {
                var rows = new RowInterval(rectangle.Top, rectangle.Bottom);
                body(rows);
                return;
            }

            int verticalStep = DivideCeil(rectangle.Height, numOfSteps);

            var parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = numOfSteps };

            Parallel.For(
                0,
                numOfSteps,
                parallelOptions,
                i =>
                    {
                        int yMin = rectangle.Top + (i * verticalStep);
                        int yMax = Math.Min(yMin + verticalStep, rectangle.Bottom);

                        var rows = new RowInterval(yMin, yMax);
                        body(rows);
                    });
        }

        /// <summary>
        /// Iterate through the rows of a rectangle in optimized batches defined by <see cref="RowInterval"/>-s
        /// instantiating a temporary buffer for each <paramref name="body"/> invocation.
        /// </summary>
        public static void IterateRowsWithTempBuffer<T>(
            Rectangle rectangle,
            in ParallelExecutionSettings parallelSettings,
            Action<RowInterval, Memory<T>> body)
            where T : struct
        {
            int maxSteps = DivideCeil(rectangle.Width * rectangle.Height, parallelSettings.MinimumPixelsProcessedPerTask);

            int numOfSteps = Math.Min(parallelSettings.MaxDegreeOfParallelism, maxSteps);

            MemoryAllocator memoryAllocator = parallelSettings.MemoryAllocator;

            // Avoid TPL overhead in this trivial case:
            if (numOfSteps == 1)
            {
                var rows = new RowInterval(rectangle.Top, rectangle.Bottom);
                using (IMemoryOwner<T> buffer = memoryAllocator.Allocate<T>(rectangle.Width))
                {
                    body(rows, buffer.Memory);
                }

                return;
            }

            int verticalStep = DivideCeil(rectangle.Height, numOfSteps);

            var parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = numOfSteps };

            Parallel.For(
                0,
                numOfSteps,
                parallelOptions,
                i =>
                    {
                        int yMin = rectangle.Top + (i * verticalStep);
                        int yMax = Math.Min(yMin + verticalStep, rectangle.Bottom);

                        var rows = new RowInterval(yMin, yMax);

                        using (IMemoryOwner<T> buffer = memoryAllocator.Allocate<T>(rectangle.Width))
                        {
                            body(rows, buffer.Memory);
                        }
                    });
        }

        /// <summary>
        /// Iterate through the rows of a rectangle in optimized batches defined by <see cref="RowInterval"/>-s
        /// instantiating a temporary buffer for each <paramref name="body"/> invocation.
        /// </summary>
        public static void IterateRowsWithTempBuffer<T>(
            Rectangle rectangle,
            Configuration configuration,
            Action<RowInterval, Memory<T>> body)
            where T : struct
        {
            IterateRowsWithTempBuffer(rectangle, configuration.GetParallelSettings(), body);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int DivideCeil(int dividend, int divisor) => 1 + ((dividend - 1) / divisor);
    }
}