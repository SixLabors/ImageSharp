// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Tests the <see cref="YCbCr"/> struct.
    /// </summary>
    public class YCbCrTests
    {
        [Fact]
        public void YCbCrConstructorAssignsFields()
        {
            const float y = 75F;
            const float cb = 64F;
            const float cr = 87F;
            var yCbCr = new YCbCr(y, cb, cr);

            Assert.Equal(y, yCbCr.Y);
            Assert.Equal(cb, yCbCr.Cb);
            Assert.Equal(cr, yCbCr.Cr);
        }

        [Fact]
        public void YCbCrEquality()
        {
            var x = default(YCbCr);
            var y = new YCbCr(Vector3.One);

            Assert.True(default(YCbCr) == default(YCbCr));
            Assert.False(default(YCbCr) != default(YCbCr));
            Assert.Equal(default(YCbCr), default(YCbCr));
            Assert.Equal(new YCbCr(1, 0, 1), new YCbCr(1, 0, 1));
            Assert.Equal(new YCbCr(Vector3.One), new YCbCr(Vector3.One));
            Assert.False(x.Equals(y));
            Assert.False(x.Equals((object)y));
            Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
        }
    }
}
