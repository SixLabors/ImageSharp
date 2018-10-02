// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Tests the <see cref="Lms"/> struct.
    /// </summary>
    public class LmsTests
    {
        [Fact]
        public void LmsConstructorAssignsFields()
        {
            const float l = 75F;
            const float m = -64F;
            const float s = 87F;
            var Lms = new Lms(l, m, s);

            Assert.Equal(l, Lms.L);
            Assert.Equal(m, Lms.M);
            Assert.Equal(s, Lms.S);
        }

        [Fact]
        public void LmsEquality()
        {
            var x = default(Lms);
            var y = new Lms(Vector3.One);

            Assert.True(default(Lms) == default(Lms));
            Assert.True(default(Lms) != new Lms(1, 0, 1));
            Assert.False(default(Lms) == new Lms(1, 0, 1));
            Assert.Equal(default(Lms), default(Lms));
            Assert.Equal(new Lms(1, 0, 1), new Lms(1, 0, 1));
            Assert.Equal(new Lms(Vector3.One), new Lms(Vector3.One));
            Assert.False(x.Equals(y));
            Assert.False(x.Equals((object)y));
            Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
        }
    }
}