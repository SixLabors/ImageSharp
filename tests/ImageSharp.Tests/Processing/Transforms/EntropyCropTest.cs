// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    public class EntropyCropTest : BaseImageOperationsExtensionTest
    {

        [Theory]
        [InlineData(0.5f)]
        [InlineData(.2f)]
        public void EntropyCrop_threasholdFloat_EntropyCropProcessorWithThreshold(float threashold)
        {
            this.operations.EntropyCrop(threashold);
            var processor = this.Verify<EntropyCropProcessor<Rgba32>>();

            Assert.Equal(threashold, processor.Threshold);
        }
    }
}