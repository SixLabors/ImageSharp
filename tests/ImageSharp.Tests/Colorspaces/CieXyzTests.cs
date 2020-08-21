// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Tests the <see cref="CieXyz"/> struct.
    /// </summary>
    public class CieXyzTests
    {
        [Fact]
        public void CieXyzConstructorAssignsFields()
        {
            const float x = 75F;
            const float y = 64F;
            const float z = 287F;
            var cieXyz = new CieXyz(x, y, z);

            Assert.Equal(x, cieXyz.X);
            Assert.Equal(y, cieXyz.Y);
            Assert.Equal(z, cieXyz.Z);
        }

        [Fact]
        public void CieXyzEquality()
        {
            var x = default(CieXyz);
            var y = new CieXyz(Vector3.One);

            Assert.True(default(CieXyz) == default(CieXyz));
            Assert.False(default(CieXyz) != default(CieXyz));
            Assert.Equal(default(CieXyz), default(CieXyz));
            Assert.Equal(new CieXyz(1, 0, 1), new CieXyz(1, 0, 1));
            Assert.Equal(new CieXyz(Vector3.One), new CieXyz(Vector3.One));
            Assert.False(x.Equals(y));
            Assert.False(x.Equals((object)y));
            Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
        }
    }
}
