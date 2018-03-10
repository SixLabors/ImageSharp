// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    using SixLabors.ImageSharp.Processing.Transforms;
    using SixLabors.ImageSharp.Processing.Transforms.Processors;

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