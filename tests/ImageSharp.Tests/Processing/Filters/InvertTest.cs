// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
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
            var processor = this.Verify<InvertProcessor<Rgba32>>();
        }

        [Fact]
        public void Pixelate_rect_PixelateProcessorDefaultsSet()
        {
            this.operations.Invert(this.rect);
            var processor = this.Verify<InvertProcessor<Rgba32>>(this.rect);
        }
    }
}