// <copyright file="PackedVectorTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Colors
{
    using System.Numerics;

    using Xunit;

    /// <summary>
    /// The packed vector tests.
    /// </summary>
    public class PackedVectorTests
    {
        [Fact]
        public void Alpha8()
        {
            // Test the limits.
            Assert.Equal(0x0, new Alpha8(0F).PackedValue);
            Assert.Equal(0xFF, new Alpha8(1F).PackedValue);

            // Test clamping.
            Assert.Equal(0x0, new Alpha8(-1234F).PackedValue);
            Assert.Equal(0xFF, new Alpha8(1234F).PackedValue);

            // Test ordering
            Assert.Equal(124, new Alpha8(124F / 0xFF).PackedValue);
            Assert.Equal(26, new Alpha8(0.1F).PackedValue);

            // Test ordering
            Vector4 vector = new Alpha8(.5F).ToVector4();
            Assert.Equal(vector.X, 0);
            Assert.Equal(vector.Y, 0);
            Assert.Equal(vector.Z, 0);
            Assert.Equal(vector.W, .5F, 2);

            byte[] rgb = new byte[3];
            byte[] rgba = new byte[4];
            byte[] bgr = new byte[3];
            byte[] bgra = new byte[4];

            new Alpha8(.5F).ToBytes(rgb, 0, ComponentOrder.XYZ);
            Assert.Equal(rgb, new byte[] { 0, 0, 0 });

            new Alpha8(.5F).ToBytes(rgba, 0, ComponentOrder.XYZW);
            Assert.Equal(rgba, new byte[] { 0, 0, 0, 128 });

            new Alpha8(.5F).ToBytes(rgb, 0, ComponentOrder.ZYX);
            Assert.Equal(bgr, new byte[] { 0, 0, 0 });

            new Alpha8(.5F).ToBytes(rgb, 0, ComponentOrder.ZYXW);
            Assert.Equal(bgra, new byte[] { 0, 0, 0, 128 });
        }
    }
}