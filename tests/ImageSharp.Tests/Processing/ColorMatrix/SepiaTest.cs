// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.ColorMatrix
{
    public class SepiaTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Sepia_amount_SepiaProcessorDefaultsSet()
        {
            this.operations.Sepia();
            var processor = this.Verify<SepiaProcessor<Rgba32>>();
        }

        [Fact]
        public void Sepia_amount_rect_SepiaProcessorDefaultsSet()
        {
            this.operations.Sepia(this.rect);
            var processor = this.Verify<SepiaProcessor<Rgba32>>(this.rect);
        }
    }
}