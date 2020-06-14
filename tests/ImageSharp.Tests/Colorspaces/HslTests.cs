// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Tests the <see cref="Hsl"/> struct.
    /// </summary>
    public class HslTests
    {
        [Fact]
        public void HslConstructorAssignsFields()
        {
            const float h = 275F;
            const float s = .64F;
            const float l = .87F;
            var hsl = new Hsl(h, s, l);

            Assert.Equal(h, hsl.H);
            Assert.Equal(s, hsl.S);
            Assert.Equal(l, hsl.L);
        }

        [Fact]
        public void HslEquality()
        {
            var x = default(Hsl);
            var y = new Hsl(Vector3.One);

            Assert.True(default(Hsl) == default(Hsl));
            Assert.False(default(Hsl) != default(Hsl));
            Assert.Equal(default(Hsl), default(Hsl));
            Assert.Equal(new Hsl(1, 0, 1), new Hsl(1, 0, 1));
            Assert.Equal(new Hsl(Vector3.One), new Hsl(Vector3.One));
            Assert.False(x.Equals(y));
            Assert.False(x.Equals((object)y));
            Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
        }
    }
}