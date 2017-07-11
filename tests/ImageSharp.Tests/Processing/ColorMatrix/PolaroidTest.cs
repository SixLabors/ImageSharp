// <copyright file="PolaroidTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.ColorMatrix
{
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing.Processors;
    using SixLabors.Primitives;
    using Xunit;

    public class PolaroidTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Polaroid_amount_PolaroidProcessorDefaultsSet()
        {
            this.operations.Polaroid();
            var processor = this.Verify<PolaroidProcessor<Rgba32>>();
        }

        [Fact]
        public void Polaroid_amount_rect_PolaroidProcessorDefaultsSet()
        {
            this.operations.Polaroid(this.rect);
            var processor = this.Verify<PolaroidProcessor<Rgba32>>(this.rect);
        }
    }
}