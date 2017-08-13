// <copyright file="EntropyCropTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Transforms
{
    using System;
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing.Processors;
    using Xunit;

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