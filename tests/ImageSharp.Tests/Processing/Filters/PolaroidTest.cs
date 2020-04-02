// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Filters;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Filters
{
    public class PolaroidTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Polaroid_amount_PolaroidProcessorDefaultsSet()
        {
            this.operations.Polaroid();
            this.Verify<PolaroidProcessor>();
        }

        [Fact]
        public void Polaroid_amount_rect_PolaroidProcessorDefaultsSet()
        {
            this.operations.Polaroid(this.rect);
            this.Verify<PolaroidProcessor>(this.rect);
        }
    }
}
