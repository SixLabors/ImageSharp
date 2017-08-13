// <copyright file="AutoOrientTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Transforms
{
    using System;
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing;
    using ImageSharp.Processing.Processors;
    using Xunit;

    public class AutoOrientTests : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void AutoOrient_AutoRotateProcessor()
        {
            this.operations.AutoOrient();
            this.Verify<AutoRotateProcessor<Rgba32>>();
        }
    }
}