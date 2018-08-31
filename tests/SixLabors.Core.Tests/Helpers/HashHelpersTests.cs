// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using Xunit;

namespace SixLabors.Tests.Helpers
{
    public class HashHelpersTests
    {
        [Fact]
        public void CanCombineTwoValues()
        {
            Assert.Equal(35, HashHelpers.Combine(1, 2));
        }

        [Fact]
        public void CanCombineThreeValues()
        {
            Assert.Equal(1152, HashHelpers.Combine(1, 2, 3));
        }

        [Fact]
        public void CanCombineFourValues()
        {
            Assert.Equal(38020, HashHelpers.Combine(1, 2, 3, 4));
        }
    }
}