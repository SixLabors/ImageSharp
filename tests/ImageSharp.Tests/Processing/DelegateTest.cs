// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing
{
    using SixLabors.ImageSharp.Processing.Processors;

    public class DelegateTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Run_CreatedDelegateProcessor()
        {
            Action<Image<Rgba32>> action = (i) => { };
            this.operations.Apply(action);

            DelegateProcessor<Rgba32> processor = this.Verify<DelegateProcessor<Rgba32>>();
            Assert.Equal(action, processor.Action);
        }
    }
}
