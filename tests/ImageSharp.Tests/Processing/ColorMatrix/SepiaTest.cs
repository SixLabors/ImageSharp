// <copyright file="SepiaTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.ColorMatrix
{
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing.Processors;
    using SixLabors.Primitives;
    using Xunit;

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