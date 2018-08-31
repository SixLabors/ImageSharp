// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    using SixLabors.ImageSharp.Processing;
    using SixLabors.ImageSharp.Processing.Processors.Filters;

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