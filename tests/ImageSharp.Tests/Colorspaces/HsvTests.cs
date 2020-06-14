// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Tests the <see cref="Hsv"/> struct.
    /// </summary>
    public class HsvTests
    {
        [Fact]
        public void HsvConstructorAssignsFields()
        {
            const float h = 275F;
            const float s = .64F;
            const float v = .87F;
            var hsv = new Hsv(h, s, v);

            Assert.Equal(h, hsv.H);
            Assert.Equal(s, hsv.S);
            Assert.Equal(v, hsv.V);
        }

        [Fact]
        public void HsvEquality()
        {
            var x = default(Hsv);
            var y = new Hsv(Vector3.One);

            Assert.True(default(Hsv) == default(Hsv));
            Assert.False(default(Hsv) != default(Hsv));
            Assert.Equal(default(Hsv), default(Hsv));
            Assert.Equal(new Hsv(1, 0, 1), new Hsv(1, 0, 1));
            Assert.Equal(new Hsv(Vector3.One), new Hsv(Vector3.One));
            Assert.False(x.Equals(y));
            Assert.False(x.Equals((object)y));
            Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
        }
    }
}