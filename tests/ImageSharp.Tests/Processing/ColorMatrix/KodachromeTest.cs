// <copyright file="KodachromeTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.ColorMatrix
{
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing.Processors;
    using SixLabors.Primitives;
    using Xunit;

    public class KodachromeTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Kodachrome_amount_KodachromeProcessorDefaultsSet()
        {
            this.operations.Kodachrome();
            var processor = this.Verify<KodachromeProcessor<Rgba32>>();
        }

        [Fact]
        public void Kodachrome_amount_rect_KodachromeProcessorDefaultsSet()
        {
            this.operations.Kodachrome(this.rect);
            var processor = this.Verify<KodachromeProcessor<Rgba32>>(this.rect);
        }
    }
}