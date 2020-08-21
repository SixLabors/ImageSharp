// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Filters
{
    public class BlackWhiteTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void BlackWhite_CorrectProcessor()
        {
            this.operations.BlackWhite();
            this.Verify<BlackWhiteProcessor>();
        }

        [Fact]
        public void BlackWhite_rect_CorrectProcessor()
        {
            this.operations.BlackWhite(this.rect);
            this.Verify<BlackWhiteProcessor>(this.rect);
        }
    }
}
