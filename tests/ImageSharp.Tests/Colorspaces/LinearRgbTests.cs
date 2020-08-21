// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Tests the <see cref="LinearRgb"/> struct.
    /// </summary>
    public class LinearRgbTests
    {
        [Fact]
        public void LinearRgbConstructorAssignsFields()
        {
            const float r = .75F;
            const float g = .64F;
            const float b = .87F;
            var rgb = new LinearRgb(r, g, b);

            Assert.Equal(r, rgb.R);
            Assert.Equal(g, rgb.G);
            Assert.Equal(b, rgb.B);
        }

        [Fact]
        public void LinearRgbEquality()
        {
            var x = default(LinearRgb);
            var y = new LinearRgb(Vector3.One);

            Assert.True(default(LinearRgb) == default(LinearRgb));
            Assert.False(default(LinearRgb) != default(LinearRgb));
            Assert.Equal(default(LinearRgb), default(LinearRgb));
            Assert.Equal(new LinearRgb(1, 0, 1), new LinearRgb(1, 0, 1));
            Assert.Equal(new LinearRgb(Vector3.One), new LinearRgb(Vector3.One));
            Assert.False(x.Equals(y));
            Assert.False(x.Equals((object)y));
            Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
        }
    }
}