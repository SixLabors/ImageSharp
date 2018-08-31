// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
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
            BlackWhiteProcessor<Rgba32> p = this.Verify<BlackWhiteProcessor<Rgba32>>();
        }

        [Fact]
        public void BlackWhite_rect_CorrectProcessor()
        {
            this.operations.BlackWhite(this.rect);
            BlackWhiteProcessor<Rgba32> p = this.Verify<BlackWhiteProcessor<Rgba32>>(this.rect);
        }
    }
}