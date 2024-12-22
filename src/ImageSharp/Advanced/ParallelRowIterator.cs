// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Advanced;

/// <summary>
/// Utility methods for batched processing of pixel row intervals.
/// Parallel execution is optimized for image processing based on values defined
/// <see cref="ParallelExecutionSettings"/> or <see cref="Configuration"/>.
/// Using this class is preferred over direct usage of <see cref="Parallel"/> utility methods.
/// </summary>
public static partial class ParallelRowIterator
{
    /// <summary>
    /// Iterate through the rows of a rectangle in optimized batches.
    /// </summary>
    /// <typeparam name="T">The type of row operation to perform.</typeparam>
    /// <param name="configuration">The <see cref="Configuration"/> to get the parallel settings from.</param>
    /// <param name="rectangle">The <see cref="Rectangle"/>.</param>
    /// <param name="operation">The operation defining the iteration logic on a single row.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    public static void IterateRows<T>(Configuration configuration, Rectangle rectangle, in T operation)
        where T : struct, IRowOperation
    {
        ParallelExecutionSettings parallelSettings = ParallelExecutionSettings.FromConfiguration(configuration);
        IterateRows(rectangle, in parallelSettings, in operation);
    }

    /// <summary>
    /// Iterate through the rows of a rectangle in optimized batches.
    /// </summary>
    /// <typeparam name="T">The type of row operation to perform.</typeparam>
    /// <param name="rectangle">The <see cref="Rectangle"/>.</param>
    /// <param name="parallelSettings">The <see cref="ParallelExecutionSettings"/>.</param>
    /// <param name="operation">The operation defining the iteration logic on a single row.</param>
    public static void IterateRows<T>(
        Rectangle rectangle,
        in ParallelExecutionSettings parallelSettings,
        in T operation)
        where T : struct, IRowOperation
    {
        ValidateRectangle(rectangle);

        int top = rectangle.Top;
        int bottom = rectangle.Bottom;
        int width = rectangle.Width;
        int height = rectangle.Height;

        int maxSteps = DivideCeil(width * (long)height, parallelSettings.MinimumPixelsProcessedPerTask);
        int numOfSteps = Math.Min(parallelSettings.MaxDegreeOfParallelism, maxSteps);

        // Avoid TPL overhead in this trivial case:
        if (numOfSteps == 1)
        {
            for (int y = top; y < bottom; y++)
            {
                Unsafe.AsRef(in operation).Invoke(y);
            }

            return;
        }

        int verticalStep = DivideCeil(rectangle.Height, numOfSteps);
        ParallelOptions? parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = numOfSteps };
        RowOperationWrapper<T> wrappingOperation = new RowOperationWrapper<T>(top, bottom, verticalStep, in operation);

        Parallel.For(
            0,
            numOfSteps,
            parallelOptions,
            wrappingOperation.Invoke);
    }

    /// <summary>
    /// Iterate through the rows of a rectangle in optimized batches.
    /// instantiating a temporary buffer for each <paramref name="operation"/> invocation.
    /// </summary>
    /// <typeparam name="T">The type of row operation to perform.</typeparam>
    /// <typeparam name="TBuffer">The type of buffer elements.</typeparam>
    /// <param name="configuration">The <see cref="Configuration"/> to get the parallel settings from.</param>
    /// <param name="rectangle">The <see cref="Rectangle"/>.</param>
    /// <param name="operation">The operation defining the iteration logic on a single row.</param>
    public static void IterateRows<T, TBuffer>(Configuration configuration, Rectangle rectangle, in T operation)
        where T : struct, IRowOperation<TBuffer>
        where TBuffer : unmanaged
    {
        ParallelExecutionSettings parallelSettings = ParallelExecutionSettings.FromConfiguration(configuration);
        IterateRows<T, TBuffer>(rectangle, in parallelSettings, in operation);
    }

    /// <summary>
    /// Iterate through the rows of a rectangle in optimized batches.
    /// instantiating a temporary buffer for each <paramref name="operation"/> invocation.
    /// </summary>
    /// <typeparam name="T">The type of row operation to perform.</typeparam>
    /// <typeparam name="TBuffer">The type of buffer elements.</typeparam>
    /// <param name="rectangle">The <see cref="Rectangle"/>.</param>
    /// <param name="parallelSettings">The <see cref="ParallelExecutionSettings"/>.</param>
    /// <param name="operation">The operation defining the iteration logic on a single row.</param>
    public static void IterateRows<T, TBuffer>(
        Rectangle rectangle,
        in ParallelExecutionSettings parallelSettings,
        in T operation)
        where T : struct, IRowOperation<TBuffer>
        where TBuffer : unmanaged
    {
        ValidateRectangle(rectangle);

        int top = rectangle.Top;
        int bottom = rectangle.Bottom;
        int width = rectangle.Width;
        int height = rectangle.Height;

        int maxSteps = DivideCeil(width * (long)height, parallelSettings.MinimumPixelsProcessedPerTask);
        int numOfSteps = Math.Min(parallelSettings.MaxDegreeOfParallelism, maxSteps);
        MemoryAllocator allocator = parallelSettings.MemoryAllocator;
        int bufferLength = Unsafe.AsRef(in operation).GetRequiredBufferLength(rectangle);

        // Avoid TPL overhead in this trivial case:
        if (numOfSteps == 1)
        {
            using IMemoryOwner<TBuffer> buffer = allocator.Allocate<TBuffer>(bufferLength);
            Span<TBuffer> span = buffer.Memory.Span;

            for (int y = top; y < bottom; y++)
            {
                Unsafe.AsRef(in operation).Invoke(y, span);
            }

            return;
        }

        int verticalStep = DivideCeil(height, numOfSteps);
        ParallelOptions? parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = numOfSteps };
        RowOperationWrapper<T, TBuffer> wrappingOperation = new RowOperationWrapper<T, TBuffer>(top, bottom, verticalStep, bufferLength, allocator, in operation);

        Parallel.For(
            0,
            numOfSteps,
            parallelOptions,
            wrappingOperation.Invoke);
    }

    /// <summary>
    /// Iterate through the rows of a rectangle in optimized batches defined by <see cref="RowInterval"/>-s.
    /// </summary>
    /// <typeparam name="T">The type of row operation to perform.</typeparam>
    /// <param name="configuration">The <see cref="Configuration"/> to get the parallel settings from.</param>
    /// <param name="rectangle">The <see cref="Rectangle"/>.</param>
    /// <param name="operation">The operation defining the iteration logic on a single <see cref="RowInterval"/>.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    public static void IterateRowIntervals<T>(Configuration configuration, Rectangle rectangle, in T operation)
        where T : struct, IRowIntervalOperation
    {
        ParallelExecutionSettings parallelSettings = ParallelExecutionSettings.FromConfiguration(configuration);
        IterateRowIntervals(rectangle, in parallelSettings, in operation);
    }

    /// <summary>
    /// Iterate through the rows of a rectangle in optimized batches defined by <see cref="RowInterval"/>-s.
    /// </summary>
    /// <typeparam name="T">The type of row operation to perform.</typeparam>
    /// <param name="rectangle">The <see cref="Rectangle"/>.</param>
    /// <param name="parallelSettings">The <see cref="ParallelExecutionSettings"/>.</param>
    /// <param name="operation">The operation defining the iteration logic on a single <see cref="RowInterval"/>.</param>
    public static void IterateRowIntervals<T>(
        Rectangle rectangle,
        in ParallelExecutionSettings parallelSettings,
        in T operation)
        where T : struct, IRowIntervalOperation
    {
        ValidateRectangle(rectangle);

        int top = rectangle.Top;
        int bottom = rectangle.Bottom;
        int width = rectangle.Width;
        int height = rectangle.Height;

        int maxSteps = DivideCeil(width * (long)height, parallelSettings.MinimumPixelsProcessedPerTask);
        int numOfSteps = Math.Min(parallelSettings.MaxDegreeOfParallelism, maxSteps);

        // Avoid TPL overhead in this trivial case:
        if (numOfSteps == 1)
        {
            RowInterval rows = new RowInterval(top, bottom);
            Unsafe.AsRef(in operation).Invoke(in rows);
            return;
        }

        int verticalStep = DivideCeil(rectangle.Height, numOfSteps);
        ParallelOptions? parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = numOfSteps };
        RowIntervalOperationWrapper<T> wrappingOperation = new RowIntervalOperationWrapper<T>(top, bottom, verticalStep, in operation);

        Parallel.For(
            0,
            numOfSteps,
            parallelOptions,
            wrappingOperation.Invoke);
    }

    /// <summary>
    /// Iterate through the rows of a rectangle in optimized batches defined by <see cref="RowInterval"/>-s
    /// instantiating a temporary buffer for each <paramref name="operation"/> invocation.
    /// </summary>
    /// <typeparam name="T">The type of row operation to perform.</typeparam>
    /// <typeparam name="TBuffer">The type of buffer elements.</typeparam>
    /// <param name="configuration">The <see cref="Configuration"/> to get the parallel settings from.</param>
    /// <param name="rectangle">The <see cref="Rectangle"/>.</param>
    /// <param name="operation">The operation defining the iteration logic on a single <see cref="RowInterval"/>.</param>
    public static void IterateRowIntervals<T, TBuffer>(Configuration configuration, Rectangle rectangle, in T operation)
        where T : struct, IRowIntervalOperation<TBuffer>
        where TBuffer : unmanaged
    {
        ParallelExecutionSettings parallelSettings = ParallelExecutionSettings.FromConfiguration(configuration);
        IterateRowIntervals<T, TBuffer>(rectangle, in parallelSettings, in operation);
    }

    /// <summary>
    /// Iterate through the rows of a rectangle in optimized batches defined by <see cref="RowInterval"/>-s
    /// instantiating a temporary buffer for each <paramref name="operation"/> invocation.
    /// </summary>
    /// <typeparam name="T">The type of row operation to perform.</typeparam>
    /// <typeparam name="TBuffer">The type of buffer elements.</typeparam>
    /// <param name="rectangle">The <see cref="Rectangle"/>.</param>
    /// <param name="parallelSettings">The <see cref="ParallelExecutionSettings"/>.</param>
    /// <param name="operation">The operation defining the iteration logic on a single <see cref="RowInterval"/>.</param>
    public static void IterateRowIntervals<T, TBuffer>(
        Rectangle rectangle,
        in ParallelExecutionSettings parallelSettings,
        in T operation)
        where T : struct, IRowIntervalOperation<TBuffer>
        where TBuffer : unmanaged
    {
        ValidateRectangle(rectangle);

        int top = rectangle.Top;
        int bottom = rectangle.Bottom;
        int width = rectangle.Width;
        int height = rectangle.Height;

        int maxSteps = DivideCeil(width * (long)height, parallelSettings.MinimumPixelsProcessedPerTask);
        int numOfSteps = Math.Min(parallelSettings.MaxDegreeOfParallelism, maxSteps);
        MemoryAllocator allocator = parallelSettings.MemoryAllocator;
        int bufferLength = Unsafe.AsRef(in operation).GetRequiredBufferLength(rectangle);

        // Avoid TPL overhead in this trivial case:
        if (numOfSteps == 1)
        {
            RowInterval rows = new RowInterval(top, bottom);
            using IMemoryOwner<TBuffer> buffer = allocator.Allocate<TBuffer>(bufferLength);

            Unsafe.AsRef(in operation).Invoke(in rows, buffer.Memory.Span);

            return;
        }

        int verticalStep = DivideCeil(height, numOfSteps);
        ParallelOptions? parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = numOfSteps };
        RowIntervalOperationWrapper<T, TBuffer> wrappingOperation = new RowIntervalOperationWrapper<T, TBuffer>(top, bottom, verticalStep, bufferLength, allocator, in operation);

        Parallel.For(
            0,
            numOfSteps,
            parallelOptions,
            wrappingOperation.Invoke);
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static int DivideCeil(long dividend, int divisor) => (int)Math.Min(1 + ((dividend - 1) / divisor), int.MaxValue);

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
