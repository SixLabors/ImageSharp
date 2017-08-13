// <copyright file="AlphaTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Effects
{
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing.Processors;
    using SixLabors.Primitives;
    using Xunit;

    public class AlphaTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Alpha_amount_AlphaProcessorDefaultsSet()
        {
            this.operations.Alpha(0.2f);
            var processor = this.Verify<AlphaProcessor<Rgba32>>();

            Assert.Equal(.2f, processor.Value);
        }

        [Fact]
        public void Alpha_amount_rect_AlphaProcessorDefaultsSet()
        {
            this.operations.Alpha(0.6f, this.rect);
            var processor = this.Verify<AlphaProcessor<Rgba32>>(this.rect);

            Assert.Equal(.6f, processor.Value);
        }
    }
}