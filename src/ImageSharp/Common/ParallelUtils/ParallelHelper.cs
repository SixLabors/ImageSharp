// Copyright(c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using SixLabors.ImageSharp.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.ParallelUtils
{
    /// <summary>
    /// Utility methods wrapping Parallel.For() execution optimized for image processing.
    /// Use this instead of direct <see cref="Parallel"/> calls!
    /// </summary>
    internal static class ParallelHelper
    {
        /// <summary>
        /// Get the default <see cref="ParallelExecutionSettings"/> for a <see cref="Configuration"/>
        /// </summary>
        public static ParallelExecutionSettings GetParallelSettings(this Configuration configuration)
        {
            return new ParallelExecutionSettings(configuration.MaxDegreeOfParallelism);
        }

        /// <summary>
        /// Gets a span for all the pixels in <paramref name="buffer"/> defined by <paramref name="rows"/>
        /// </summary>
        public static Span<T> GetMultiRowSpan<T>(this Buffer2D<T> buffer, in RowInterval rows)
            where T : struct
        {
            return buffer.Span.Slice(rows.Min * buffer.Width, rows.Height * buffer.Width);
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
        public static void IterateRows(Rectangle rectangle, in ParallelExecutionSettings parallelSettings, Action<RowInterval> body)
        {
            int maxSteps = DivideCeil(rectangle.Width * rectangle.Height, parallelSettings.MinimumPixelsProcessedPerTask);

            int numOfSteps = Math.Min(parallelSettings.MaxDegreeOfParallelism, maxSteps);

            // Avoid TPL overhead in this trivial case:
            if (numOfSteps == 1)
            {
                var rows = new RowInterval(rectangle.Top, rectangle.Bottom);
                body(rows);
                return;
            }

            int verticalStep = DivideRound(rectangle.Height, numOfSteps);

            var parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = numOfSteps };

            Parallel.For(
                0,
                numOfSteps,
                parallelOptions,
                i =>
                    {
                        int yMin = rectangle.Top + (i * verticalStep);
                        int yMax = Math.Min(yMin + verticalStep, rectangle.Bottom);

                        var rowInterval = new RowInterval(yMin, yMax);
                        body(rowInterval);
                    });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int DivideCeil(int dividend, int divisor)
        {
            // TODO: Is there a more efficient way to calculate this?
            int result = dividend / divisor;
            return dividend % divisor == 0 ? result : result + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int DivideRound(int dividend, int divisor)
        {
            return (dividend + (divisor / 2)) / divisor;
        }
    }
}