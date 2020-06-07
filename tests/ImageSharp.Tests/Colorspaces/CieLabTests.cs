// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Tests the <see cref="CieLab"/> struct.
    /// </summary>
    public class CieLabTests
    {
        [Fact]
        public void CieLabConstructorAssignsFields()
        {
            const float l = 75F;
            const float a = -64F;
            const float b = 87F;
            var cieLab = new CieLab(l, a, b);

            Assert.Equal(l, cieLab.L);
            Assert.Equal(a, cieLab.A);
            Assert.Equal(b, cieLab.B);
        }

        [Fact]
        public void CieLabEquality()
        {
            var x = default(CieLab);
            var y = new CieLab(Vector3.One);

            Assert.True(default(CieLab) == default(CieLab));
            Assert.True(new CieLab(1, 0, 1) != default(CieLab));
            Assert.False(new CieLab(1, 0, 1) == default(CieLab));
            Assert.Equal(default(CieLab), default(CieLab));
            Assert.Equal(new CieLab(1, 0, 1), new CieLab(1, 0, 1));
            Assert.Equal(new CieLab(Vector3.One), new CieLab(Vector3.One));
            Assert.False(x.Equals(y));
            Assert.False(new CieLab(1, 0, 1) == default(CieLab));
            Assert.False(x.Equals((object)y));
            Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
        }
    }
}
