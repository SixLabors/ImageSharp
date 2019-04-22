// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Transforms;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms
{
    public class ResizeHelperTests
    {
        
        [Theory]
        [InlineData(20, 100, 1, 2)]
        [InlineData(20, 100, 20*100*16, 2)]
        [InlineData(20, 100, 40*100*16, 2)]
        [InlineData(20, 100, 59*100*16, 2)]
        [InlineData(20, 100, 60*100*16, 3)]
        [InlineData(17, 63, 5*17*63*16, 5)]
        [InlineData(17, 63, 5*17*63*16+1, 5)]
        [InlineData(17, 63, 6*17*63*16-1, 5)]
        [InlineData(33, 400, 1*1024*1024, 4)]
        [InlineData(33, 400, 8*1024*1024, 39)]
        [InlineData(50, 300, 1*1024*1024, 4)]
        public void CalculateResizeWorkerHeightInWindowBands(
            int windowDiameter,
            int width,
            int sizeLimitHintInBytes,
            int expectedCount)
        {
            int actualCount = ResizeHelper.CalculateResizeWorkerHeightInWindowBands(windowDiameter, width, sizeLimitHintInBytes);
            Assert.Equal(expectedCount, actualCount);
        }
    }
}