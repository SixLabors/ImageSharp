// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Tests the <see cref="CieLuv"/> struct.
    /// </summary>
    public class CieLuvTests
    {
        [Fact]
        public void CieLuvConstructorAssignsFields()
        {
            const float l = 75F;
            const float c = -64F;
            const float h = 87F;
            var cieLuv = new CieLuv(l, c, h);

            Assert.Equal(l, cieLuv.L);
            Assert.Equal(c, cieLuv.U);
            Assert.Equal(h, cieLuv.V);
        }

        [Fact]
        public void CieLuvEquality()
        {
            var x = default(CieLuv);
            var y = new CieLuv(Vector3.One);

            Assert.True(default(CieLuv) == default(CieLuv));
            Assert.False(default(CieLuv) != default(CieLuv));
            Assert.Equal(default(CieLuv), default(CieLuv));
            Assert.Equal(new CieLuv(1, 0, 1), new CieLuv(1, 0, 1));
            Assert.Equal(new CieLuv(Vector3.One), new CieLuv(Vector3.One));
            Assert.False(x.Equals(y));
            Assert.False(x.Equals((object)y));
            Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
        }
    }
}
