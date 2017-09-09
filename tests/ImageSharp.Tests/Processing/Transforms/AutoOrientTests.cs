// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    public class AutoOrientTests : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void AutoOrient_AutoOrientProcessor()
        {
            this.operations.AutoOrient();
            this.Verify<AutoOrientProcessor<Rgba32>>();
        }
    }
}