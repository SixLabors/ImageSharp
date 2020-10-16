// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Extensions.Transforms;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    public class SwizzleTests : BaseImageOperationsExtensionTest
    {
        private struct InvertXAndYSwizzler : ISwizzler
        {
            public Point Transform(Point point) => new Point(point.Y, point.X);
        }

        [Fact]
        public void RotateDegreesFloatRotateProcessorWithAnglesSet()
        {
            this.operations.Swizzle(default(InvertXAndYSwizzler));
            SwizzleProcessor<InvertXAndYSwizzler> processor = this.Verify<SwizzleProcessor<InvertXAndYSwizzler>>();

            // assert that pixels have been changed

            this.operations.Swizzle(default(InvertXAndYSwizzler));
            SwizzleProcessor<InvertXAndYSwizzler> processor2 = this.Verify<SwizzleProcessor<InvertXAndYSwizzler>>();

            // assert that pixels have been changed (i.e. back to original)
        }
    }
}
