// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using Xunit;

namespace SixLabors.ImageSharp.Tests.Common
{
    public class ConstantsTests
    {
        [Fact]
        public void Epsilon()
        {
            Assert.Equal(0.001f, Constants.Epsilon);
        }
    }
}