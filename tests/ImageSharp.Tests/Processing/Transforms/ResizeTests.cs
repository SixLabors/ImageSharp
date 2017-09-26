// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    public class ResizeTests : BaseImageOperationsExtensionTest
    {
#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "Skip resize tests as they need refactoring to be simpler and just pass data into the resize processor.")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void TestMissing()
        {
            //
            throw new NotImplementedException("Write test here");
        }
    }
}