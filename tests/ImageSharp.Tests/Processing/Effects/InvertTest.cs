// <copyright file="InvertTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Effects
{
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing.Processors;
    using SixLabors.Primitives;
    using Xunit;

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