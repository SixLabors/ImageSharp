// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Advanced.ParallelUtils
{
    /// <summary>
    /// Utility methods for batched processing of pixel row intervals.
    /// Parallel execution is optimized for image processing based on values defined
    /// <see cref="ParallelExecutionSettings"/> or <see cref="Configuration"/>.
    /// Using this class is preferred over direct usage of <see cref="Parallel"/> utility methods.
    /// </summary>
    public static class ParallelHelper
    {
        /// <summary>
        /// Iterate through the rows of a rectangle in optimized batches defined by <see cref="RowInterval"/>-s.
        /// </summary>
        /// <param name="rectangle">The <see cref="Rectangle"/>.</param>
        /// <param name="configuration">The <see cref="Configuration"/> to get the parallel settings from.</param>
        /// <param name="body">The method body defining the iteration logic on a single <see cref="RowInterval"/>.</param>
        public static void IterateRows(Rectangle rectangle, Configuration configuration, Action<RowInterval> body)
        {
            var parallelSettings = ParallelExecutionSettings.FromConfiguration(configuration);

            IterateRows(rectangle, in parallelSettings, body);
        }

        /// <summary>
        /// Iterate through the rows of a rectangle in optimized batches defined by <see cref="RowInterval"/>-s.
        /// </summary>
        /// <typeparam name="T">The type of row action to perform.</typeparam>
        /// <param name="rectangle">The <see cref="Rectangle"/>.</param>
        /// <param name="configuration">The <see cref="Configuration"/> to get the parallel settings from.</param>
        /// <param name="body">The method body defining the iteration logic on a single <see cref="RowInterval"/>.</param>
        public static void IterateRowsFast<T>(Rectangle rectangle, Configuration configuration, ref T body)
            where T : struct, IRowAction
        {
            var parallelSettings = ParallelExecutionSettings.FromConfiguration(configuration);

            IterateRowsFast(rectangle, in parallelSettings, ref body);
        }

        internal static void IterateRowsFast<T>(
            Rectangle rectangle,
            in ParallelExecutionSettings parallelSettings,
            ref T body)
            where T : struct, IRowAction
        {
            ValidateRectangle(rectangle);

            int maxSteps = DivideCeil(
                rectangle.Width * rectangle.Height,
                parallelSettings.MinimumPixelsProcessedPerTask);

            int numOfSteps = Math.Min(parallelSettings.MaxDegreeOfParallelism, maxSteps);

            // Avoid TPL overhead in this trivial case:
            if (numOfSteps == 1)
            {
                var rows = new RowInterval(rectangle.Top, rectangle.Bottom);
                body.Invoke(in rows);
                return;
            }

            int verticalStep = DivideCeil(rectangle.Height, numOfSteps);

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = numOfSteps };

            int top = rectangle.Top;
            int bottom = rectangle.Bottom;

            var rowAction = new WrappingRowAction<T>(ref body);

            Parallel.For(
                0,
                numOfSteps,
                parallelOptions,
                i =>
                {
                    int yMin = top + (i * verticalStep);

                    if (yMin >= bottom)
                    {
                        return;
                    }

                    int yMax = Math.Min(yMin + verticalStep, bottom);

                    var rows = new RowInterval(yMin, yMax);

                    rowAction.Invoke(in rows);
                });
        }

        /// <summary>
        /// Iterate through the rows of a rectangle in optimized batches defined by <see cref="RowInterval"/>-s.
        /// </summary>
        /// <param name="rectangle">The <see cref="Rectangle"/>.</param>
        /// <param name="parallelSettings">The <see cref="ParallelExecutionSettings"/>.</param>
        /// <param name="body">The method body defining the iteration logic on a single <see cref="RowInterval"/>.</param>
        public static void IterateRows(
        Rectangle rectangle,
        in ParallelExecutionSettings parallelSettings,
        Action<RowInterval> body)
        {
            ValidateRectangle(rectangle);

            int maxSteps = DivideCeil(
                rectangle.Width * rectangle.Height,
                parallelSettings.MinimumPixelsProcessedPerTask);

            int numOfSteps = Math.Min(parallelSettings.MaxDegreeOfParallelism, maxSteps);

            // Avoid TPL overhead in this trivial case:
            if (numOfSteps == 1)
            {
                var rows = new RowInterval(rectangle.Top, rectangle.Bottom);
                body(rows);
                return;
            }

            int verticalStep = DivideCeil(rectangle.Height, numOfSteps);

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = numOfSteps };

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
        internal static void IterateRowsWithTempBuffer<T>(
            Rectangle rectangle,
            in ParallelExecutionSettings parallelSettings,
            Action<RowInterval, Memory<T>> body)
            where T : unmanaged
        {
            ValidateRectangle(rectangle);

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

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = numOfSteps };

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
        internal static void IterateRowsWithTempBuffer<T>(
            Rectangle rectangle,
            Configuration configuration,
            Action<RowInterval, Memory<T>> body)
            where T : unmanaged
        {
            IterateRowsWithTempBuffer(rectangle, ParallelExecutionSettings.FromConfiguration(configuration), body);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int DivideCeil(int dividend, int divisor) => 1 + ((dividend - 1) / divisor);

        private static void ValidateRectangle(Rectangle rectangle)
        {
            Guard.MustBeGreaterThan(
                rectangle.Width,
                0,
                $"{nameof(rectangle)}.{nameof(rectangle.Width)}");

            Guard.MustBeGreaterThan(
                rectangle.Height,
                0,
                $"{nameof(rectangle)}.{nameof(rectangle.Height)}");
        }
    }
}
