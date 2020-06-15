// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Effects
{
    public class InvertTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Invert_InvertProcessorDefaultsSet()
        {
            this.operations.Invert();
            this.Verify<InvertProcessor>();
        }

        [Fact]
        public void Pixelate_rect_PixelateProcessorDefaultsSet()
        {
            this.operations.Invert(this.rect);
            this.Verify<InvertProcessor>(this.rect);
        }
    }
}
