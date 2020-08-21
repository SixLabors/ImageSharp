// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Tests the <see cref="Cmyk"/> struct.
    /// </summary>
    public class CmykTests
    {
        [Fact]
        public void CmykConstructorAssignsFields()
        {
            const float c = .75F;
            const float m = .64F;
            const float y = .87F;
            const float k = .334F;
            var cmyk = new Cmyk(c, m, y, k);

            Assert.Equal(c, cmyk.C);
            Assert.Equal(m, cmyk.M);
            Assert.Equal(y, cmyk.Y);
            Assert.Equal(k, cmyk.K);
        }

        [Fact]
        public void CmykEquality()
        {
            var x = default(Cmyk);
            var y = new Cmyk(Vector4.One);

            Assert.True(default(Cmyk) == default(Cmyk));
            Assert.False(default(Cmyk) != default(Cmyk));
            Assert.Equal(default(Cmyk), default(Cmyk));
            Assert.Equal(new Cmyk(1, 0, 1, 0), new Cmyk(1, 0, 1, 0));
            Assert.Equal(new Cmyk(Vector4.One), new Cmyk(Vector4.One));
            Assert.False(x.Equals(y));
            Assert.False(x.Equals((object)y));
            Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
        }
    }
}