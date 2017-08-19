// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
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