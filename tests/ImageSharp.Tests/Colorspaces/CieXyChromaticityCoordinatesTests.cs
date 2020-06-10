// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Tests the <see cref="CieXyChromaticityCoordinates"/> struct.
    /// </summary>
    public class CieXyChromaticityCoordinatesTests
    {
        [Fact]
        public void CieXyChromaticityCoordinatesConstructorAssignsFields()
        {
            const float x = .75F;
            const float y = .64F;
            var coordinates = new CieXyChromaticityCoordinates(x, y);

            Assert.Equal(x, coordinates.X);
            Assert.Equal(y, coordinates.Y);
        }

        [Fact]
        public void CieXyChromaticityCoordinatesEquality()
        {
            var x = default(CieXyChromaticityCoordinates);
            var y = new CieXyChromaticityCoordinates(1, 1);

            Assert.True(default(CieXyChromaticityCoordinates) == default(CieXyChromaticityCoordinates));
            Assert.True(new CieXyChromaticityCoordinates(1, 0) != default(CieXyChromaticityCoordinates));
            Assert.False(new CieXyChromaticityCoordinates(1, 0) == default(CieXyChromaticityCoordinates));
            Assert.Equal(default(CieXyChromaticityCoordinates), default(CieXyChromaticityCoordinates));
            Assert.Equal(new CieXyChromaticityCoordinates(1, 0), new CieXyChromaticityCoordinates(1, 0));
            Assert.Equal(new CieXyChromaticityCoordinates(1, 1), new CieXyChromaticityCoordinates(1, 1));
            Assert.False(x.Equals(y));
            Assert.False(new CieXyChromaticityCoordinates(1, 0) == default(CieXyChromaticityCoordinates));
            Assert.False(x.Equals((object)y));
            Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
        }
    }
}
