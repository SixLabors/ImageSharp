// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.MetaData.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Icc
{
    public class IccProfileIdTests
    {
        [Fact]
        public void ZeroIsEqualToDefault()
        {
            Assert.True(IccProfileId.Zero.Equals(default));

            Assert.False(default(IccProfileId).IsSet);
        }

        [Fact]
        public void SetIsTrueWhenNonDefaultValue()
        {
            var id = new IccProfileId(1, 2, 3, 4);

            Assert.True(id.IsSet);

            Assert.Equal(1u, id.Part1);
            Assert.Equal(2u, id.Part2);
            Assert.Equal(3u, id.Part3);
            Assert.Equal(4u, id.Part4);
        }
    }
}