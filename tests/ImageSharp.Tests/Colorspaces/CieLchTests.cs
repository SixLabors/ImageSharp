// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Tests the <see cref="CieLch"/> struct.
    /// </summary>
    public class CieLchTests
    {
        [Fact]
        public void CieLchConstructorAssignsFields()
        {
            const float l = 75F;
            const float c = 64F;
            const float h = 287F;
            var cieLch = new CieLch(l, c, h);

            Assert.Equal(l, cieLch.L);
            Assert.Equal(c, cieLch.C);
            Assert.Equal(h, cieLch.H);
        }

        [Fact]
        public void CieLchEquality()
        {
            var x = default(CieLch);
            var y = new CieLch(Vector3.One);

            Assert.True(default(CieLch) == default(CieLch));
            Assert.False(default(CieLch) != default(CieLch));
            Assert.Equal(default(CieLch), default(CieLch));
            Assert.Equal(new CieLch(1, 0, 1), new CieLch(1, 0, 1));
            Assert.Equal(new CieLch(Vector3.One), new CieLch(Vector3.One));
            Assert.False(x.Equals(y));
            Assert.False(x.Equals((object)y));
            Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
        }
    }
}
