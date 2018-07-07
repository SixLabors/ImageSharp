// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Filters
{
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