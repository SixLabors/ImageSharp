// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Tests the <see cref="CieLchuv"/> struct.
    /// </summary>
    public class CieLchuvTests
    {
        [Fact]
        public void CieLchuvConstructorAssignsFields()
        {
            const float l = 75F;
            const float c = 64F;
            const float h = 287F;
            var cieLchuv = new CieLchuv(l, c, h);

            Assert.Equal(l, cieLchuv.L);
            Assert.Equal(c, cieLchuv.C);
            Assert.Equal(h, cieLchuv.H);
        }

        [Fact]
        public void CieLchuvEquality()
        {
            var x = default(CieLchuv);
            var y = new CieLchuv(Vector3.One);

            Assert.True(default(CieLchuv) == default(CieLchuv));
            Assert.False(default(CieLchuv) != default(CieLchuv));
            Assert.Equal(default(CieLchuv), default(CieLchuv));
            Assert.Equal(new CieLchuv(1, 0, 1), new CieLchuv(1, 0, 1));
            Assert.Equal(new CieLchuv(Vector3.One), new CieLchuv(Vector3.One));
            Assert.False(x.Equals(y));
            Assert.False(x.Equals((object)y));
            Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
        }
    }
}
