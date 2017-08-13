// <copyright file="ResizeTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Transforms
{
    using System;
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing;
    using SixLabors.Primitives;
    using Xunit;

    public class ResizeTests : BaseImageOperationsExtensionTest
    {
        [Fact(Skip = "Skip resize tests as they need refactoring to be simpler and just pass data into the resize processor.")]
        public void TestMissing()
        {
            //
            throw new NotImplementedException("Write test here");
        }
    }
}