// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Threading;

using SixLabors.ImageSharp.ParallelUtils;
using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Helpers
{
    public class ParallelHelperTests
    {
        [Theory]
        [InlineData(1, 0, 100, -1, 100)]
        [InlineData(2, 0, 9, 5, 4)]
        [InlineData(4, 0, 19, 5, 4)]
        [InlineData(2, 10, 19, 5, 4)]
        [InlineData(4, 0, 200, 50, 50)]
        [InlineData(4, 123, 323, 50, 50)]
        [InlineData(4, 0, 1201, 300, 301)]
        public void IterateRows_OverMinimumPixelsLimit(
            int maxDegreeOfParallelism,
            int minY,
            int maxY,
            int expectedStepLength,
            int expectedLastStepLength)
        {
            var parallelSettings = new ParallelExecutionSettings(maxDegreeOfParallelism, 1);

            var rectangle = new Rectangle(0, minY, 10, maxY - minY);

            int actualNumberOfSteps = 0;
            ParallelHelper.IterateRows(rectangle, parallelSettings,
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
        [InlineData(2, 200, 50, 2, 1, -1, 2)]
        [InlineData(2, 200, 200, 1, 1, -1, 1)]
        [InlineData(4, 200, 100, 4, 2, 2, 2)]
        [InlineData(4, 300, 100, 8, 3, 3, 2)]
        [InlineData(2, 5000, 1, 4500, 1, -1, 4500)]
        [InlineData(2, 5000, 1, 5000, 1, -1, 5000)]
        [InlineData(2, 5000, 1, 5001, 2, 2501, 2500)]
        public void IterateRows_WithEffectiveMinimumPixelsLimit(
            int maxDegreeOfParallelism,
            int minimumPixelsProcessedPerTask,
            int width,
            int height,
            int expectedNumberOfSteps,
            int expectedStepLength,
            int expectedLastStepLength)
        {
            var parallelSettings = new ParallelExecutionSettings(maxDegreeOfParallelism, minimumPixelsProcessedPerTask);

            var rectangle = new Rectangle(0, 0, width, height);

            int actualNumberOfSteps = 0;
            ParallelHelper.IterateRows(rectangle, parallelSettings,
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
    }
}