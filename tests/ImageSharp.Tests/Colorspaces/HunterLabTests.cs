// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Tests the <see cref="HunterLab"/> struct.
    /// </summary>
    public class HunterLabTests
    {
        [Fact]
        public void HunterLabConstructorAssignsFields()
        {
            const float l = 75F;
            const float a = -64F;
            const float b = 87F;
            var hunterLab = new HunterLab(l, a, b);

            Assert.Equal(l, hunterLab.L);
            Assert.Equal(a, hunterLab.A);
            Assert.Equal(b, hunterLab.B);
        }

        [Fact]
        public void HunterLabEquality()
        {
            var x = default(HunterLab);
            var y = new HunterLab(Vector3.One);

            Assert.True(default(HunterLab) == default(HunterLab));
            Assert.True(new HunterLab(1, 0, 1) != default(HunterLab));
            Assert.False(new HunterLab(1, 0, 1) == default(HunterLab));
            Assert.Equal(default(HunterLab), default(HunterLab));
            Assert.Equal(new HunterLab(1, 0, 1), new HunterLab(1, 0, 1));
            Assert.Equal(new HunterLab(Vector3.One), new HunterLab(Vector3.One));
            Assert.False(x.Equals(y));
            Assert.False(x.Equals((object)y));
            Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
        }
    }
}
