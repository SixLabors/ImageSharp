// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Helpers
{
    public class ColorNumericsTests
    {
        [Theory]
        [InlineData(0.2f, 0.7f, 0.1f, 256, 140)]
        [InlineData(0.5f, 0.5f, 0.5f, 256, 128)]
        [InlineData(0.5f, 0.5f, 0.5f, 65536, 32768)]
        [InlineData(0.2f, 0.7f, 0.1f, 65536, 36069)]
        public void GetBT709Luminance_WithVector4(float x, float y, float z, int luminanceLevels, int expected)
        {
            // arrange
            var vector = new Vector4(x, y, z, 0.0f);

            // act
            int actual = ColorNumerics.GetBT709Luminance(ref vector, luminanceLevels);

            // assert
            Assert.Equal(expected, actual);
        }

        // TODO: We need to test all ColorNumerics methods!
    }
}
