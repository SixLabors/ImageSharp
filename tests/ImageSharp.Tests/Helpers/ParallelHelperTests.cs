// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;
using System.Threading;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.ParallelUtils;
using SixLabors.Memory;
using SixLabors.Primitives;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Helpers
{
    public class ParallelHelperTests
    {
        /// <summary>
        /// maxDegreeOfParallelism, minY, maxY, expectedStepLength, expectedLastStepLength
        /// </summary>
        public static TheoryData<int, int, int, int, int> IterateRows_OverMinimumPixelsLimit_Data =
            new TheoryData<int, int, int, int, int>()
                {
                    { 1, 0, 100, -1, 100 },
                    { 2, 0, 9, 5, 4 },
                    { 4, 0, 19, 5, 4 },
                    { 2, 10, 19, 5, 4 },
                    { 4, 0, 200, 50, 50 },
                    { 4, 123, 323, 50, 50 },
                    { 4, 0, 1201, 300, 301 },
                };

        [Theory]
        [MemberData(nameof(IterateRows_OverMinimumPixelsLimit_Data))]
        public void IterateRows_OverMinimumPixelsLimit(
            int maxDegreeOfParallelism,
            int minY,
            int maxY,
            int expectedStepLength,
            int expectedLastStepLength)
        {
            var parallelSettings = new ParallelExecutionSettings(
                maxDegreeOfParallelism,
                1,
                Configuration.Default.MemoryAllocator);

            var rectangle = new Rectangle(0, minY, 10, maxY - minY);

            int actualNumberOfSteps = 0;
            ParallelHelper.IterateRows(
                rectangle,
                parallelSettings,
                rows =>
                    {
                        Assert.True(rows.Min >= minY);
                        Assert.True(rows.Max <= maxY);

                        int step = rows.Max - rows.Min;
                        int expected = rows.Max < maxY ? expectedStepLength : expectedLastStepLength;

                        Interlocked.Increment(ref actualNumberOfSteps);
                        Assert.Equal(expected, step);
                    });

            Assert.Equal(maxDegreeOfParallelism, actualNumberOfSteps);
        }

        [Theory]
        [MemberData(nameof(IterateRows_OverMinimumPixelsLimit_Data))]
        public void IterateRowsWithTempBuffer_OverMinimumPixelsLimit(
            int maxDegreeOfParallelism,
            int minY,
            int maxY,
            int expectedStepLength,
            int expectedLastStepLength)
        {
            var parallelSettings = new ParallelExecutionSettings(
                maxDegreeOfParallelism,
                1,
                Configuration.Default.MemoryAllocator);

            var rectangle = new Rectangle(0, minY, 10, maxY - minY);

            var bufferHashes = new ConcurrentBag<int>();

            int actualNumberOfSteps = 0;
            ParallelHelper.IterateRowsWithTempBuffer(
                rectangle,
                parallelSettings,
                (RowInterval rows, Memory<Vector4> buffer) =>
                    {
                        Assert.True(rows.Min >= minY);
                        Assert.True(rows.Max <= maxY);

                        bufferHashes.Add(buffer.GetHashCode());

                        int step = rows.Max - rows.Min;
                        int expected = rows.Max < maxY ? expectedStepLength : expectedLastStepLength;
                        
                        Interlocked.Increment(ref actualNumberOfSteps);
                        Assert.Equal(expected, step);
                    });

            Assert.Equal(maxDegreeOfParallelism, actualNumberOfSteps);

            int numberOfDifferentBuffers = bufferHashes.Distinct().Count();
            Assert.Equal(actualNumberOfSteps, numberOfDifferentBuffers);
        }

        public static TheoryData<int, int, int, int, int, int, int> IterateRows_WithEffectiveMinimumPixelsLimit_Data =
            new TheoryData<int, int, int, int, int, int, int>()
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

            ParallelHelper.IterateRows(
                rectangle,
                parallelSettings,
                rows =>
                    {
                        Assert.True(rows.Min >= 0);
                        Assert.True(rows.Max <= height);

                        int step = rows.Max - rows.Min;
                        int expected = rows.Max < height ? expectedStepLength : expectedLastStepLength;

                        Interlocked.Increment(ref actualNumberOfSteps);
                        Assert.Equal(expected, step);
                    });

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
            ParallelHelper.IterateRowsWithTempBuffer(
                rectangle,
                parallelSettings,
                (RowInterval rows, Memory<Vector4> buffer) =>
                    {
                        Assert.True(rows.Min >= 0);
                        Assert.True(rows.Max <= height);

                        int step = rows.Max - rows.Min;
                        int expected = rows.Max < height ? expectedStepLength : expectedLastStepLength;

                        Interlocked.Increment(ref actualNumberOfSteps);
                        Assert.Equal(expected, step);
                    });

            Assert.Equal(expectedNumberOfSteps, actualNumberOfSteps);
        }

        public static readonly TheoryData<int, int, int, int, int, int, int> IterateRectangularBuffer_Data =
            new TheoryData<int, int, int, int, int, int, int>()
                {
                    { 8, 582, 453, 10, 10, 291, 226 }, // bounds in DetectEdgesTest.DetectEdges_InBox
                    { 2, 582, 453, 10, 10, 291, 226 },
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

            using (Buffer2D<int> expected = memoryAllocator.Allocate2D<int>(bufferWidth, bufferHeight, AllocationOptions.Clean))
            using (Buffer2D<int> actual = memoryAllocator.Allocate2D<int>(bufferWidth, bufferHeight, AllocationOptions.Clean))
            {
                var rect = new Rectangle(rectX, rectY, rectWidth, rectHeight);

                for (int y = rectY; y < rect.Bottom; y++)
                {
                    for (int x = rect.Left; x < rect.Right; x++)
                    {
                        expected[x, y] = y * 10000 + x;
                    }
                }

                var settings = new ParallelExecutionSettings(maxDegreeOfParallelism, memoryAllocator);

                ParallelHelper.IterateRows(rect, settings,
                    rows =>
                        {
                            for (int y = rows.Min; y < rows.Max; y++)
                            {
                                for (int x = rect.Left; x < rect.Right; x++)
                                {
                                    actual[x, y] = y * 10000 + x;
                                }
                            }
                        });

                TestImageExtensions.CompareBuffers(expected.Span, actual.Span);
            }
        }
    }
}