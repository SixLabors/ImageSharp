// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

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
            this.Verify<KodachromeProcessor>();
        }

        [Fact]
        public void Kodachrome_amount_rect_KodachromeProcessorDefaultsSet()
        {
            this.operations.Kodachrome(this.rect);
            this.Verify<KodachromeProcessor>(this.rect);
        }
    }
}
