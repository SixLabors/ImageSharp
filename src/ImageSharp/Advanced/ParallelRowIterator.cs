// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Advanced
{
    /// <summary>
    /// Utility methods for batched processing of pixel row intervals.
    /// Parallel execution is optimized for image processing based on values defined
    /// <see cref="ParallelExecutionSettings"/> or <see cref="Configuration"/>.
    /// Using this class is preferred over direct usage of <see cref="Parallel"/> utility methods.
    /// </summary>
    public static class ParallelRowIterator
    {
        /// <summary>
        /// Iterate through the rows of a rectangle in optimized batches.
        /// </summary>
        /// <typeparam name="T">The type of row action to perform.</typeparam>
        /// <param name="rectangle">The <see cref="Rectangle"/>.</param>
        /// <param name="configuration">The <see cref="Configuration"/> to get the parallel settings from.</param>
        /// <param name="body">The method body defining the iteration logic on a single row.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void IterateRows<T>(Rectangle rectangle, Configuration configuration, in T body)
            where T : struct, IRowAction
        {
            var parallelSettings = ParallelExecutionSettings.FromConfiguration(configuration);
            IterateRows(rectangle, in parallelSettings, in body);
        }

        /// <summary>
        /// Iterate through the rows of a rectangle in optimized batches.
        /// </summary>
        /// <typeparam name="T">The type of row action to perform.</typeparam>
        /// <param name="rectangle">The <see cref="Rectangle"/>.</param>
        /// <param name="parallelSettings">The <see cref="ParallelExecutionSettings"/>.</param>
        /// <param name="body">The method body defining the iteration logic on a single row.</param>
        public static void IterateRows<T>(
            Rectangle rectangle,
            in ParallelExecutionSettings parallelSettings,
            in T body)
            where T : struct, IRowAction
        {
            ValidateRectangle(rectangle);

            int top = rectangle.Top;
            int bottom = rectangle.Bottom;
            int width = rectangle.Width;
            int height = rectangle.Height;

            int maxSteps = DivideCeil(width * height, parallelSettings.MinimumPixelsProcessedPerTask);
            int numOfSteps = Math.Min(parallelSettings.MaxDegreeOfParallelism, maxSteps);

            // Avoid TPL overhead in this trivial case:
            if (numOfSteps == 1)
            {
                for (int y = top; y < bottom; y++)
                {
                    Unsafe.AsRef(body).Invoke(y);
                }

                return;
            }

            int verticalStep = DivideCeil(rectangle.Height, numOfSteps);
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = numOfSteps };
            var rowAction = new WrappingRowAction<T>(top, bottom, verticalStep, in body);

            Parallel.For(
                0,
                numOfSteps,
                parallelOptions,
                rowAction.Invoke);
        }

        /// <summary>
        /// Iterate through the rows of a rectangle in optimized batches defined by <see cref="RowInterval"/>-s
        /// instantiating a temporary buffer for each <paramref name="body"/> invocation.
        /// </summary>
        /// <typeparam name="T">The type of row action to perform.</typeparam>
        /// <typeparam name="TBuffer">The type of buffer elements.</typeparam>
        /// <param name="rectangle">The <see cref="Rectangle"/>.</param>
        /// <param name="configuration">The <see cref="Configuration"/> to get the parallel settings from.</param>
        /// <param name="body">The method body defining the iteration logic on a single <see cref="RowInterval"/>.</param>
        public static void IterateRows<T, TBuffer>(Rectangle rectangle, Configuration configuration, in T body)
            where T : struct, IRowIntervalAction<TBuffer>
            where TBuffer : unmanaged
        {
            var parallelSettings = ParallelExecutionSettings.FromConfiguration(configuration);
            IterateRows<T, TBuffer>(rectangle, in parallelSettings, in body);
        }

        /// <summary>
        /// Iterate through the rows of a rectangle in optimized batches defined by <see cref="RowInterval"/>-s
        /// instantiating a temporary buffer for each <paramref name="body"/> invocation.
        /// </summary>
        internal static void IterateRows<T, TBuffer>(
            Rectangle rectangle,
            in ParallelExecutionSettings parallelSettings,
            in T body)
            where T : struct, IRowIntervalAction<TBuffer>
            where TBuffer : unmanaged
        {
            ValidateRectangle(rectangle);

            int top = rectangle.Top;
            int bottom = rectangle.Bottom;
            int width = rectangle.Width;
            int height = rectangle.Height;

            int maxSteps = DivideCeil(width * height, parallelSettings.MinimumPixelsProcessedPerTask);
            int numOfSteps = Math.Min(parallelSettings.MaxDegreeOfParallelism, maxSteps);
            MemoryAllocator allocator = parallelSettings.MemoryAllocator;

            // Avoid TPL overhead in this trivial case:
            if (numOfSteps == 1)
            {
                var rows = new RowInterval(top, bottom);
                using (IMemoryOwner<TBuffer> buffer = allocator.Allocate<TBuffer>(width))
                {
                    Unsafe.AsRef(body).Invoke(rows, buffer.Memory);
                }

                return;
            }

            int verticalStep = DivideCeil(height, numOfSteps);
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = numOfSteps };
            var rowInfo = new WrappingRowIntervalInfo(top, bottom, verticalStep, width);
            var rowAction = new WrappingRowIntervalBufferAction<T, TBuffer>(in rowInfo, allocator, in body);

            Parallel.For(
                0,
                numOfSteps,
                parallelOptions,
                rowAction.Invoke);
        }

        /// <summary>
        /// Iterate through the rows of a rectangle in optimized batches defined by <see cref="RowInterval"/>-s
        /// instantiating a temporary buffer for each <paramref name="body"/> invocation.
        /// </summary>
        /// <typeparam name="T">The type of row action to perform.</typeparam>
        /// <typeparam name="TBuffer">The type of buffer elements.</typeparam>
        /// <param name="rectangle">The <see cref="Rectangle"/>.</param>
        /// <param name="configuration">The <see cref="Configuration"/> to get the parallel settings from.</param>
        /// <param name="body">The method body defining the iteration logic on a single <see cref="RowInterval"/>.</param>
        public static void IterateRows2<T, TBuffer>(Rectangle rectangle, Configuration configuration, in T body)
            where T : struct, IRowAction<TBuffer>
            where TBuffer : unmanaged
        {
            var parallelSettings = ParallelExecutionSettings.FromConfiguration(configuration);
            IterateRows2<T, TBuffer>(rectangle, in parallelSettings, in body);
        }

        /// <summary>
        /// Iterate through the rows of a rectangle in optimized batches defined by <see cref="RowInterval"/>-s
        /// instantiating a temporary buffer for each <paramref name="body"/> invocation.
        /// </summary>
        internal static void IterateRows2<T, TBuffer>(
            Rectangle rectangle,
            in ParallelExecutionSettings parallelSettings,
            in T body)
            where T : struct, IRowAction<TBuffer>
            where TBuffer : unmanaged
        {
            ValidateRectangle(rectangle);

            int top = rectangle.Top;
            int bottom = rectangle.Bottom;
            int width = rectangle.Width;
            int height = rectangle.Height;

            int maxSteps = DivideCeil(width * height, parallelSettings.MinimumPixelsProcessedPerTask);
            int numOfSteps = Math.Min(parallelSettings.MaxDegreeOfParallelism, maxSteps);
            MemoryAllocator allocator = parallelSettings.MemoryAllocator;

            // Avoid TPL overhead in this trivial case:
            if (numOfSteps == 1)
            {
                using (IMemoryOwner<TBuffer> buffer = allocator.Allocate<TBuffer>(width))
                {
                    Span<TBuffer> span = buffer.Memory.Span;

                    for (int y = top; y < bottom; y++)
                    {
                        Unsafe.AsRef(body).Invoke(y, span);
                    }
                }

                return;
            }

            int verticalStep = DivideCeil(height, numOfSteps);
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = numOfSteps };
            var rowAction = new WrappingRowAction<T, TBuffer>(top, bottom, verticalStep, width, allocator, in body);

            Parallel.For(
                0,
                numOfSteps,
                parallelOptions,
                rowAction.Invoke);
        }

        /// <summary>
        /// Iterate through the rows of a rectangle in optimized batches defined by <see cref="RowInterval"/>-s.
        /// </summary>
        /// <param name="rectangle">The <see cref="Rectangle"/>.</param>
        /// <param name="configuration">The <see cref="Configuration"/> to get the parallel settings from.</param>
        /// <param name="body">The method body defining the iteration logic on a single <see cref="RowInterval"/>.</param>
        internal static void IterateRows(Rectangle rectangle, Configuration configuration, Action<RowInterval> body)
        {
            var parallelSettings = ParallelExecutionSettings.FromConfiguration(configuration);
            IterateRows(rectangle, in parallelSettings, body);
        }

        /// <summary>
        /// Iterate through the rows of a rectangle in optimized batches defined by <see cref="RowInterval"/>-s.
        /// </summary>
        /// <param name="rectangle">The <see cref="Rectangle"/>.</param>
        /// <param name="parallelSettings">The <see cref="ParallelExecutionSettings"/>.</param>
        /// <param name="body">The method body defining the iteration logic on a single <see cref="RowInterval"/>.</param>
        internal static void IterateRows(
            Rectangle rectangle,
            in ParallelExecutionSettings parallelSettings,
            Action<RowInterval> body)
        {
            ValidateRectangle(rectangle);

            int top = rectangle.Top;
            int bottom = rectangle.Bottom;
            int width = rectangle.Width;
            int height = rectangle.Height;

            int maxSteps = DivideCeil(width * height, parallelSettings.MinimumPixelsProcessedPerTask);
            int numOfSteps = Math.Min(parallelSettings.MaxDegreeOfParallelism, maxSteps);

            // Avoid TPL overhead in this trivial case:
            if (numOfSteps == 1)
            {
                var rows = new RowInterval(top, bottom);
                body(rows);
                return;
            }

            int verticalStep = DivideCeil(rectangle.Height, numOfSteps);
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = numOfSteps };
            var rowInfo = new WrappingRowIntervalInfo(top, bottom, verticalStep);
            var rowAction = new WrappingRowIntervalAction(in rowInfo, body);

            Parallel.For(
                0,
                numOfSteps,
                parallelOptions,
                rowAction.Invoke);
        }

        /// <summary>
        /// Iterate through the rows of a rectangle in optimized batches defined by <see cref="RowInterval"/>-s
        /// instantiating a temporary buffer for each <paramref name="body"/> invocation.
        /// </summary>
        internal static void IterateRows<TBuffer>(
            Rectangle rectangle,
            Configuration configuration,
            Action<RowInterval, Memory<TBuffer>> body)
            where TBuffer : unmanaged
        {
            var parallelSettings = ParallelExecutionSettings.FromConfiguration(configuration);
            IterateRows(rectangle, in parallelSettings, body);
        }

        /// <summary>
        /// Iterate through the rows of a rectangle in optimized batches defined by <see cref="RowInterval"/>-s
        /// instantiating a temporary buffer for each <paramref name="body"/> invocation.
        /// </summary>
        internal static void IterateRows<TBuffer>(
            Rectangle rectangle,
            in ParallelExecutionSettings parallelSettings,
            Action<RowInterval, Memory<TBuffer>> body)
            where TBuffer : unmanaged
        {
            ValidateRectangle(rectangle);

            int top = rectangle.Top;
            int bottom = rectangle.Bottom;
            int width = rectangle.Width;
            int height = rectangle.Height;

            int maxSteps = DivideCeil(width * height, parallelSettings.MinimumPixelsProcessedPerTask);
            int numOfSteps = Math.Min(parallelSettings.MaxDegreeOfParallelism, maxSteps);
            MemoryAllocator allocator = parallelSettings.MemoryAllocator;

            // Avoid TPL overhead in this trivial case:
            if (numOfSteps == 1)
            {
                var rows = new RowInterval(top, bottom);
                using (IMemoryOwner<TBuffer> buffer = allocator.Allocate<TBuffer>(width))
                {
                    body(rows, buffer.Memory);
                }

                return;
            }

            int verticalStep = DivideCeil(rectangle.Height, numOfSteps);
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = numOfSteps };
            var rowInfo = new WrappingRowIntervalInfo(top, bottom, verticalStep, width);
            var rowAction = new WrappingRowIntervalBufferAction<TBuffer>(in rowInfo, allocator, body);

            Parallel.For(
                0,
                numOfSteps,
                parallelOptions,
                rowAction.Invoke);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
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
