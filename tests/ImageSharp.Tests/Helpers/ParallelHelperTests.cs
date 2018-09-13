// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Threading;

using SixLabors.ImageSharp.Memory;
using SixLabors.Primitives;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Helpers
{
    public class ParallelHelperTests
    {
        [Theory]
        [InlineData(1, 0, 100, -1, 100)]
        [InlineData(2, 0, 9, 5, 4)]
        [InlineData(2, 10, 19, 5, 4)]
        [InlineData(4, 0, 200, 50, 50)]
        [InlineData(4, 123, 323, 50, 50)]
        public void IterateRows_OverMinimumPixelsLimit(
            int maxDegreeOfParallelism,
            int minY,
            int maxY,
            int expectedStep,
            int expectedLastStep)
        {
            Configuration cfg = Configuration.Default.ShallowCopy();
            cfg.MinimumPixelsProcessedPerTask = 1;
            cfg.MaxDegreeOfParallelism = maxDegreeOfParallelism;

            var rectangle = new Rectangle(0, minY, 10, maxY);

            int actualNumberOfSteps = 0;
            ParallelHelper.IterateRows(rectangle, cfg,
                rows =>
                    {
                        Assert.True(rows.Min >= minY);
                        int step = rows.Max - rows.Min;
                        int expected = rows.Max < maxY ? expectedStep : expectedLastStep;

                        Interlocked.Increment(ref actualNumberOfSteps);
                        Assert.Equal(expected, step);
                    });

            Assert.Equal(maxDegreeOfParallelism, actualNumberOfSteps);
        }

        [Theory]
        [InlineData(2, 200, 50, 2, 1, -1, 2)]
        [InlineData(2, 200, 200, 1, 1, -1, 1)]
        [InlineData(4, 200, 100, 4, 2, 1, 1)]
        [InlineData(4, 300, 100, 8, 3, 3, 2)]
        public void IterateRows_WithEffectiveMinimumPixelsLimit(
            int maxDegreeOfParallelism,
            int minimumPixelsProcessedPerTask,
            int width,
            int height,
            int expectedNumberOfSteps,
            int expectedStep,
            int expectedLastStep)
        {
            Configuration cfg = Configuration.Default.ShallowCopy();
            cfg.MinimumPixelsProcessedPerTask = minimumPixelsProcessedPerTask;
            cfg.MaxDegreeOfParallelism = maxDegreeOfParallelism;

            var rectangle = new Rectangle(0, 0, width, height);

            int actualNumberOfSteps = 0;
            ParallelHelper.IterateRows(rectangle, cfg,
                rows =>
                    {
                        int step = rows.Max - rows.Min;
                        int expected = rows.Max < height ? expectedStep : expectedLastStep;

                        Interlocked.Increment(ref actualNumberOfSteps);
                        Assert.Equal(expected, step);
                    });

            Assert.Equal(expectedNumberOfSteps, actualNumberOfSteps);
        }
    }
}