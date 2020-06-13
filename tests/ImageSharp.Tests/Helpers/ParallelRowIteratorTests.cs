// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Numerics;
using System.Threading;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Helpers
{
    public class ParallelRowIteratorTests
    {
        public delegate void RowIntervalAction<T>(RowInterval rows, Span<T> span);

        private readonly ITestOutputHelper output;

        public ParallelRowIteratorTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>
        /// maxDegreeOfParallelism, minY, maxY, expectedStepLength, expectedLastStepLength
        /// </summary>
        public static TheoryData<int, int, int, int, int, int> IterateRows_OverMinimumPixelsLimit_Data =
            new TheoryData<int, int, int, int, int, int>
            {
                { 1, 0, 100, -1, 100, 1 },
                { 2, 0, 9, 5, 4, 2 },
                { 4, 0, 19, 5, 4, 4 },
                { 2, 10, 19, 5, 4, 2 },
                { 4, 0, 200, 50, 50, 4 },
                { 4, 123, 323, 50, 50, 4 },
                { 4, 0, 1201, 301, 298, 4 },
                { 8, 10, 236, 29, 23, 8 },
                { 16, 0, 209, 14, 13, 15 },
                { 24, 0, 209, 9, 2, 24 },
                { 32, 0, 209, 7, 6, 30 },
                { 64, 0, 209, 4, 1, 53 },
            };

        [Theory]
        [MemberData(nameof(IterateRows_OverMinimumPixelsLimit_Data))]
        public void IterateRows_OverMinimumPixelsLimit_IntervalsAreCorrect(
            int maxDegreeOfParallelism,
            int minY,
            int maxY,
            int expectedStepLength,
            int expectedLastStepLength,
            int expectedNumberOfSteps)
        {
            var parallelSettings = new ParallelExecutionSettings(
                maxDegreeOfParallelism,
                1,
                Configuration.Default.MemoryAllocator);

            var rectangle = new Rectangle(0, minY, 10, maxY - minY);

            int actualNumberOfSteps = 0;

            void RowAction(RowInterval rows)
            {
                Assert.True(rows.Min >= minY);
                Assert.True(rows.Max <= maxY);

                int step = rows.Max - rows.Min;
                int expected = rows.Max < maxY ? expectedStepLength : expectedLastStepLength;

                Interlocked.Increment(ref actualNumberOfSteps);
                Assert.Equal(expected, step);
            }

            var operation = new TestRowIntervalOperation(RowAction);

            ParallelRowIterator.IterateRowIntervals(
                rectangle,
                in parallelSettings,
                in operation);

            Assert.Equal(expectedNumberOfSteps, actualNumberOfSteps);
        }

        [Theory]
        [MemberData(nameof(IterateRows_OverMinimumPixelsLimit_Data))]
        public void IterateRows_OverMinimumPixelsLimit_ShouldVisitAllRows(
            int maxDegreeOfParallelism,
            int minY,
            int maxY,
            int expectedStepLength,
            int expectedLastStepLength,
            int expectedNumberOfSteps)
        {
            var parallelSettings = new ParallelExecutionSettings(
                maxDegreeOfParallelism,
                1,
                Configuration.Default.MemoryAllocator);

            var rectangle = new Rectangle(0, minY, 10, maxY - minY);

            int[] expectedData = Enumerable.Repeat(0, minY).Concat(Enumerable.Range(minY, maxY - minY)).ToArray();
            var actualData = new int[maxY];

            void RowAction(RowInterval rows)
            {
                for (int y = rows.Min; y < rows.Max; y++)
                {
                    actualData[y] = y;
                }
            }

            var operation = new TestRowIntervalOperation(RowAction);

            ParallelRowIterator.IterateRowIntervals(
                rectangle,
                in parallelSettings,
                in operation);

            Assert.Equal(expectedData, actualData);
        }

        [Theory]
        [MemberData(nameof(IterateRows_OverMinimumPixelsLimit_Data))]
        public void IterateRowsWithTempBuffer_OverMinimumPixelsLimit(
            int maxDegreeOfParallelism,
            int minY,
            int maxY,
            int expectedStepLength,
            int expectedLastStepLength,
            int expectedNumberOfSteps)
        {
            var parallelSettings = new ParallelExecutionSettings(
                maxDegreeOfParallelism,
                1,
                Configuration.Default.MemoryAllocator);

            var rectangle = new Rectangle(0, minY, 10, maxY - minY);

            int actualNumberOfSteps = 0;

            void RowAction(RowInterval rows, Span<Vector4> buffer)
            {
                Assert.True(rows.Min >= minY);
                Assert.True(rows.Max <= maxY);

                int step = rows.Max - rows.Min;
                int expected = rows.Max < maxY ? expectedStepLength : expectedLastStepLength;

                Interlocked.Increment(ref actualNumberOfSteps);
                Assert.Equal(expected, step);
            }

            var operation = new TestRowIntervalOperation<Vector4>(RowAction);

            ParallelRowIterator.IterateRowIntervals<TestRowIntervalOperation<Vector4>, Vector4>(
                rectangle,
                in parallelSettings,
                in operation);

            Assert.Equal(expectedNumberOfSteps, actualNumberOfSteps);
        }

        [Theory]
        [MemberData(nameof(IterateRows_OverMinimumPixelsLimit_Data))]
        public void IterateRowsWithTempBuffer_OverMinimumPixelsLimit_ShouldVisitAllRows(
            int maxDegreeOfParallelism,
            int minY,
            int maxY,
            int expectedStepLength,
            int expectedLastStepLength,
            int expectedNumberOfSteps)
        {
            var parallelSettings = new ParallelExecutionSettings(
                maxDegreeOfParallelism,
                1,
                Configuration.Default.MemoryAllocator);

            var rectangle = new Rectangle(0, minY, 10, maxY - minY);

            int[] expectedData = Enumerable.Repeat(0, minY).Concat(Enumerable.Range(minY, maxY - minY)).ToArray();
            var actualData = new int[maxY];

            void RowAction(RowInterval rows, Span<Vector4> buffer)
            {
                for (int y = rows.Min; y < rows.Max; y++)
                {
                    actualData[y] = y;
                }
            }

            var operation = new TestRowIntervalOperation<Vector4>(RowAction);

            ParallelRowIterator.IterateRowIntervals<TestRowIntervalOperation<Vector4>, Vector4>(
                rectangle,
                in parallelSettings,
                in operation);

            Assert.Equal(expectedData, actualData);
        }

        public static TheoryData<int, int, int, int, int, int, int> IterateRows_WithEffectiveMinimumPixelsLimit_Data =
            new TheoryData<int, int, int, int, int, int, int>
            {
                { 2, 200, 50, 2, 1, -1, 2 },
                { 2, 200, 200, 1, 1, -1, 1 },
                { 4, 200, 100, 4, 2, 2, 2 },
                { 4, 300, 100, 8, 3, 3, 2 },
                { 2, 5000, 1, 4500, 1, -1, 4500 },
                { 2, 5000, 1, 5000, 1, -1, 5000 },
                { 2, 5000, 1, 5001, 2, 2501, 2500 },
            };

        [Theory]
        [MemberData(nameof(IterateRows_WithEffectiveMinimumPixelsLimit_Data))]
        public void IterateRows_WithEffectiveMinimumPixelsLimit(
            int maxDegreeOfParallelism,
            int minimumPixelsProcessedPerTask,
            int width,
            int height,
            int expectedNumberOfSteps,
            int expectedStepLength,
            int expectedLastStepLength)
        {
            var parallelSettings = new ParallelExecutionSettings(
                maxDegreeOfParallelism,
                minimumPixelsProcessedPerTask,
                Configuration.Default.MemoryAllocator);

            var rectangle = new Rectangle(0, 0, width, height);

            int actualNumberOfSteps = 0;

            void RowAction(RowInterval rows)
            {
                Assert.True(rows.Min >= 0);
                Assert.True(rows.Max <= height);

                int step = rows.Max - rows.Min;
                int expected = rows.Max < height ? expectedStepLength : expectedLastStepLength;

                Interlocked.Increment(ref actualNumberOfSteps);
                Assert.Equal(expected, step);
            }

            var operation = new TestRowIntervalOperation(RowAction);

            ParallelRowIterator.IterateRowIntervals(
                rectangle,
                in parallelSettings,
                in operation);

            Assert.Equal(expectedNumberOfSteps, actualNumberOfSteps);
        }

        [Theory]
        [MemberData(nameof(IterateRows_WithEffectiveMinimumPixelsLimit_Data))]
        public void IterateRowsWithTempBuffer_WithEffectiveMinimumPixelsLimit(
            int maxDegreeOfParallelism,
            int minimumPixelsProcessedPerTask,
            int width,
            int height,
            int expectedNumberOfSteps,
            int expectedStepLength,
            int expectedLastStepLength)
        {
            var parallelSettings = new ParallelExecutionSettings(
                maxDegreeOfParallelism,
                minimumPixelsProcessedPerTask,
                Configuration.Default.MemoryAllocator);

            var rectangle = new Rectangle(0, 0, width, height);

            int actualNumberOfSteps = 0;

            void RowAction(RowInterval rows, Span<Vector4> buffer)
            {
                Assert.True(rows.Min >= 0);
                Assert.True(rows.Max <= height);

                int step = rows.Max - rows.Min;
                int expected = rows.Max < height ? expectedStepLength : expectedLastStepLength;

                Interlocked.Increment(ref actualNumberOfSteps);
                Assert.Equal(expected, step);
            }

            var operation = new TestRowIntervalOperation<Vector4>(RowAction);

            ParallelRowIterator.IterateRowIntervals<TestRowIntervalOperation<Vector4>, Vector4>(
                rectangle,
                in parallelSettings,
                in operation);

            Assert.Equal(expectedNumberOfSteps, actualNumberOfSteps);
        }

        public static readonly TheoryData<int, int, int, int, int, int, int> IterateRectangularBuffer_Data =
            new TheoryData<int, int, int, int, int, int, int>
            {
                { 8, 582, 453, 10, 10, 291, 226 }, // boundary data from DetectEdgesTest.DetectEdges_InBox
                { 2, 582, 453, 10, 10, 291, 226 },
                { 16, 582, 453, 10, 10, 291, 226 },
                { 16, 582, 453, 10, 10, 1, 226 },
                { 16, 1, 453, 0, 10, 1, 226 },
            };

        [Theory]
        [MemberData(nameof(IterateRectangularBuffer_Data))]
        public void IterateRectangularBuffer(
            int maxDegreeOfParallelism,
            int bufferWidth,
            int bufferHeight,
            int rectX,
            int rectY,
            int rectWidth,
            int rectHeight)
        {
            MemoryAllocator memoryAllocator = Configuration.Default.MemoryAllocator;

            using (Buffer2D<Point> expected = memoryAllocator.Allocate2D<Point>(bufferWidth, bufferHeight, AllocationOptions.Clean))
            using (Buffer2D<Point> actual = memoryAllocator.Allocate2D<Point>(bufferWidth, bufferHeight, AllocationOptions.Clean))
            {
                var rect = new Rectangle(rectX, rectY, rectWidth, rectHeight);

                void FillRow(int y, Buffer2D<Point> buffer)
                {
                    for (int x = rect.Left; x < rect.Right; x++)
                    {
                        buffer[x, y] = new Point(x, y);
                    }
                }

                // Fill Expected data:
                for (int y = rectY; y < rect.Bottom; y++)
                {
                    FillRow(y, expected);
                }

                // Fill actual data using IterateRows:
                var settings = new ParallelExecutionSettings(maxDegreeOfParallelism, memoryAllocator);

                void RowAction(RowInterval rows)
                {
                    this.output.WriteLine(rows.ToString());
                    for (int y = rows.Min; y < rows.Max; y++)
                    {
                        FillRow(y, actual);
                    }
                }

                var operation = new TestRowIntervalOperation(RowAction);

                ParallelRowIterator.IterateRowIntervals(
                    rect,
                    settings,
                    in operation);

                // Assert:
                TestImageExtensions.CompareBuffers(expected.GetSingleSpan(), actual.GetSingleSpan());
            }
        }

        [Theory]
        [InlineData(0, 10)]
        [InlineData(10, 0)]
        [InlineData(-10, 10)]
        [InlineData(10, -10)]
        public void IterateRowsRequiresValidRectangle(int width, int height)
        {
            var parallelSettings = default(ParallelExecutionSettings);

            var rect = new Rectangle(0, 0, width, height);

            void RowAction(RowInterval rows)
            {
            }

            var operation = new TestRowIntervalOperation(RowAction);

            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => ParallelRowIterator.IterateRowIntervals(rect, in parallelSettings, in operation));

            Assert.Contains(width <= 0 ? "Width" : "Height", ex.Message);
        }

        [Theory]
        [InlineData(0, 10)]
        [InlineData(10, 0)]
        [InlineData(-10, 10)]
        [InlineData(10, -10)]
        public void IterateRowsWithTempBufferRequiresValidRectangle(int width, int height)
        {
            var parallelSettings = default(ParallelExecutionSettings);

            var rect = new Rectangle(0, 0, width, height);

            void RowAction(RowInterval rows, Span<Rgba32> memory)
            {
            }

            var operation = new TestRowIntervalOperation<Rgba32>(RowAction);

            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => ParallelRowIterator.IterateRowIntervals<TestRowIntervalOperation<Rgba32>, Rgba32>(rect, in parallelSettings, in operation));

            Assert.Contains(width <= 0 ? "Width" : "Height", ex.Message);
        }

        private readonly struct TestRowIntervalOperation : IRowIntervalOperation
        {
            private readonly Action<RowInterval> action;

            public TestRowIntervalOperation(Action<RowInterval> action)
                => this.action = action;

            public void Invoke(in RowInterval rows) => this.action(rows);
        }

        private readonly struct TestRowIntervalOperation<TBuffer> : IRowIntervalOperation<TBuffer>
            where TBuffer : unmanaged
        {
            private readonly RowIntervalAction<TBuffer> action;

            public TestRowIntervalOperation(RowIntervalAction<TBuffer> action)
                => this.action = action;

            public void Invoke(in RowInterval rows, Span<TBuffer> span)
                => this.action(rows, span);
        }
    }
}
