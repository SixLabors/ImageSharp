// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using Xunit;

namespace SixLabors.Tests.Helpers
{
    public class GeometryUtilitiesTests
    {
        [Fact]
        public void Convert_Degree_To_Radian()
            => Assert.Equal((float)(Math.PI / 2D), GeometryUtilities.DegreeToRadian(90F), new FloatRoundingComparer(6));

        [Fact]
        public void Convert_Radian_To_Degree()
            => Assert.Equal(60F, GeometryUtilities.RadianToDegree((float)(Math.PI / 3D)), new FloatRoundingComparer(5));
    }
}
