// <copyright file="SkewTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing
{
    using System;
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing;
    using Xunit;

    public class DelegateTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Run_CreatedDelegateProcessor()
        {
            Action<Image<Rgba32>> action = (i) => { };
            this.operations.Run(action);

            DelegateProcessor<Rgba32> processor = this.Verify<DelegateProcessor<Rgba32>>();
            Assert.Equal(action, processor.Action);
        }
    }
}
