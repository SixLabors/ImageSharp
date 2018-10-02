﻿// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Tests the <see cref="CieXyy"/> struct.
    /// </summary>
    public class CieXyyTests
    {
        [Fact]
        public void CieXyyConstructorAssignsFields()
        {
            const float x = 75F;
            const float y = 64F;
            const float yl = 287F;
            var cieXyy = new CieXyy(x, y, yl);

            Assert.Equal(x, cieXyy.X);
            Assert.Equal(y, cieXyy.Y);
            Assert.Equal(y, cieXyy.Y);
        }

        [Fact]
        public void CieXyyEquality()
        {
            var x = default(CieXyy);
            var y = new CieXyy(Vector3.One);
            Assert.Equal(default(CieXyy), default(CieXyy));
            Assert.Equal(new CieXyy(1, 0, 1), new CieXyy(1, 0, 1));
            Assert.Equal(new CieXyy(Vector3.One), new CieXyy(Vector3.One));
            Assert.False(x.Equals(y));
        }
    }
}
