// <copyright file="LomographTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;

    using ImageSharp.PixelFormats;
    using ImageSharp.Processing.Processors;
    using SixLabors.Primitives;
    using Xunit;

    public class LomographTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Lomograph_amount_LomographProcessorDefaultsSet()
        {
            this.operations.Lomograph();
            var processor = this.Verify<LomographProcessor<Rgba32>>();
        }

        [Fact]
        public void Lomograph_amount_rect_LomographProcessorDefaultsSet()
        {
            this.operations.Lomograph(this.rect);
            var processor = this.Verify<LomographProcessor<Rgba32>>(this.rect);
        }
    }
}