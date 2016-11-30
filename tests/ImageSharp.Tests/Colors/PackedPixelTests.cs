// <copyright file="PackedPixelTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Colors
{
    using System;
    using System.Numerics;

    using Xunit;

    /// <summary>
    /// The packed pixel tests.
    /// </summary>
    public class PackedPixelTests
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

        [Fact]
        public void Bgr565()
        {
            // Test the limits.
            Assert.Equal(0x0, new Bgr565(Vector3.Zero).PackedValue);
            Assert.Equal(0xFFFF, new Bgr565(Vector3.One).PackedValue);

            // Test ToVector3.
            Assert.True(Equal(Vector3.One, new Bgr565(Vector3.One).ToVector3()));
            Assert.True(Equal(Vector3.Zero, new Bgr565(Vector3.Zero).ToVector3()));
            Assert.True(Equal(Vector3.UnitX, new Bgr565(Vector3.UnitX).ToVector3()));
            Assert.True(Equal(Vector3.UnitY, new Bgr565(Vector3.UnitY).ToVector3()));
            Assert.True(Equal(Vector3.UnitZ, new Bgr565(Vector3.UnitZ).ToVector3()));

            // Test clamping.
            Assert.True(Equal(Vector3.Zero, new Bgr565(Vector3.One * -1234F).ToVector3()));
            Assert.True(Equal(Vector3.One, new Bgr565(Vector3.One * 1234F).ToVector3()));

            // Make sure the swizzle is correct.
            Assert.Equal(0xF800, new Bgr565(Vector3.UnitX).PackedValue);
            Assert.Equal(0x07E0, new Bgr565(Vector3.UnitY).PackedValue);
            Assert.Equal(0x001F, new Bgr565(Vector3.UnitZ).PackedValue);

            float x = 0.1F;
            float y = -0.3F;
            float z = 0.5F;
            Assert.Equal(6160, new Bgr565(x, y, z).PackedValue);


            // Test ordering
            byte[] rgb = new byte[3];
            byte[] rgba = new byte[4];
            byte[] bgr = new byte[3];
            byte[] bgra = new byte[4];

            new Bgr565(x, y, z).ToBytes(rgb, 0, ComponentOrder.XYZ);
            Assert.Equal(rgb, new byte[] { 24, 0, 131 });

            new Bgr565(x, y, z).ToBytes(rgba, 0, ComponentOrder.XYZW);
            Assert.Equal(rgba, new byte[] { 24, 0, 131, 255 });

            new Bgr565(x, y, z).ToBytes(bgr, 0, ComponentOrder.ZYX);
            Assert.Equal(bgr, new byte[] { 131, 0, 24 });

            new Bgr565(x, y, z).ToBytes(bgra, 0, ComponentOrder.ZYXW);
            Assert.Equal(bgra, new byte[] { 131, 0, 24, 255 });
        }

        [Fact]
        public void Bgra4444()
        {
            // Test the limits.
            Assert.Equal(0x0, new Bgra4444(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFF, new Bgra4444(Vector4.One).PackedValue);

            // Test ToVector4.
            Assert.True(Equal(Vector4.One, new Bgra4444(Vector4.One).ToVector4()));
            Assert.True(Equal(Vector4.Zero, new Bgra4444(Vector4.Zero).ToVector4()));
            Assert.True(Equal(Vector4.UnitX, new Bgra4444(Vector4.UnitX).ToVector4()));
            Assert.True(Equal(Vector4.UnitY, new Bgra4444(Vector4.UnitY).ToVector4()));
            Assert.True(Equal(Vector4.UnitZ, new Bgra4444(Vector4.UnitZ).ToVector4()));
            Assert.True(Equal(Vector4.UnitW, new Bgra4444(Vector4.UnitW).ToVector4()));

            // Test clamping.
            Assert.True(Equal(Vector4.Zero, new Bgra4444(Vector4.One * -1234.0f).ToVector4()));
            Assert.True(Equal(Vector4.One, new Bgra4444(Vector4.One * 1234.0f).ToVector4()));

            // Make sure the swizzle is correct.
            Assert.Equal(0x0F00, new Bgra4444(Vector4.UnitX).PackedValue);
            Assert.Equal(0x00F0, new Bgra4444(Vector4.UnitY).PackedValue);
            Assert.Equal(0x000F, new Bgra4444(Vector4.UnitZ).PackedValue);
            Assert.Equal(0xF000, new Bgra4444(Vector4.UnitW).PackedValue);

            float x = 0.1f;
            float y = -0.3f;
            float z = 0.5f;
            float w = -0.7f;
            Assert.Equal(520, new Bgra4444(x, y, z, w).PackedValue);

            // Test ordering
            byte[] rgb = new byte[3];
            byte[] rgba = new byte[4];
            byte[] bgr = new byte[3];
            byte[] bgra = new byte[4];

            new Bgra4444(x, y, z, w).ToBytes(rgb, 0, ComponentOrder.XYZ);
            Assert.Equal(rgb, new byte[] { 34, 0, 136 });

            new Bgra4444(x, y, z, w).ToBytes(rgba, 0, ComponentOrder.XYZW);
            Assert.Equal(rgba, new byte[] { 34, 0, 136, 0 });

            new Bgra4444(x, y, z, w).ToBytes(bgr, 0, ComponentOrder.ZYX);
            Assert.Equal(bgr, new byte[] { 136, 0, 34 });

            new Bgra4444(x, y, z, w).ToBytes(bgra, 0, ComponentOrder.ZYXW);
            Assert.Equal(bgra, new byte[] { 136, 0, 34, 0 });
        }

        [Fact]
        public void Bgra5551()
        {
            // Test the limits.
            Assert.Equal(0x0, new Bgra5551(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFF, new Bgra5551(Vector4.One).PackedValue);

            // Test ToVector4
            Assert.True(Equal(Vector4.Zero, new Bgra5551(Vector4.Zero).ToVector4()));
            Assert.True(Equal(Vector4.One, new Bgra5551(Vector4.One).ToVector4()));

            // Test clamping.
            Assert.Equal(Vector4.Zero, new Bgra5551(Vector4.One * -1234.0f).ToVector4());
            Assert.Equal(Vector4.One, new Bgra5551(Vector4.One * 1234.0f).ToVector4());

            // Test Ordering
            float x = 0x1a;
            float y = 0x16;
            float z = 0xd;
            float w = 0x1;
            Assert.Equal(0xeacd, new Bgra5551(x / 0x1f, y / 0x1f, z / 0x1f, w).PackedValue);
            x = 0.1f;
            y = -0.3f;
            z = 0.5f;
            w = -0.7f;
            Assert.Equal(3088, new Bgra5551(x, y, z, w).PackedValue);

            // Test ordering
            byte[] rgb = new byte[3];
            byte[] rgba = new byte[4];
            byte[] bgr = new byte[3];
            byte[] bgra = new byte[4];

            new Bgra5551(x, y, z, w).ToBytes(rgb, 0, ComponentOrder.XYZ);
            Assert.Equal(rgb, new byte[] { 24, 0, 131 });

            new Bgra5551(x, y, z, w).ToBytes(rgba, 0, ComponentOrder.XYZW);
            Assert.Equal(rgba, new byte[] { 24, 0, 131, 0 });

            new Bgra5551(x, y, z, w).ToBytes(bgr, 0, ComponentOrder.ZYX);
            Assert.Equal(bgr, new byte[] { 131, 0, 24 });

            new Bgra5551(x, y, z, w).ToBytes(bgra, 0, ComponentOrder.ZYXW);
            Assert.Equal(bgra, new byte[] { 131, 0, 24, 0 });
        }

        [Fact]
        public void Byte4()
        {
            // Test the limits.
            Assert.Equal((uint)0x0, new Byte4(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFFFFFF, new Byte4(Vector4.One * 255).PackedValue);

            // Test ToVector4.
            Assert.True(Equal(Vector4.One * 255, new Byte4(Vector4.One * 255).ToVector4()));
            Assert.True(Equal(Vector4.Zero, new Byte4(Vector4.Zero).ToVector4()));
            Assert.True(Equal(Vector4.UnitX * 255, new Byte4(Vector4.UnitX * 255).ToVector4()));
            Assert.True(Equal(Vector4.UnitY * 255, new Byte4(Vector4.UnitY * 255).ToVector4()));
            Assert.True(Equal(Vector4.UnitZ * 255, new Byte4(Vector4.UnitZ * 255).ToVector4()));
            Assert.True(Equal(Vector4.UnitW * 255, new Byte4(Vector4.UnitW * 255).ToVector4()));

            // Test clamping.
            Assert.True(Equal(Vector4.Zero, new Byte4(Vector4.One * -1234.0f).ToVector4()));
            Assert.True(Equal(Vector4.One * 255, new Byte4(Vector4.One * 1234.0f).ToVector4()));

            // Test ordering
            float x = 0x2d;
            float y = 0x36;
            float z = 0x7b;
            float w = 0x1a;
            Assert.Equal((uint)0x1a7b362d, new Byte4(x, y, z, w).PackedValue);

            x = 127.5f;
            y = -12.3f;
            z = 0.5f;
            w = -0.7f;
            Assert.Equal((uint)128, new Byte4(x, y, z, w).PackedValue);

            // Test ordering
            byte[] rgb = new byte[3];
            byte[] rgba = new byte[4];
            byte[] bgr = new byte[3];
            byte[] bgra = new byte[4];

            new Byte4(x, y, z, w).ToBytes(rgb, 0, ComponentOrder.XYZ);
            Assert.Equal(rgb, new byte[] { 128, 0, 0 });

            new Byte4(x, y, z, w).ToBytes(rgba, 0, ComponentOrder.XYZW);
            Assert.Equal(rgba, new byte[] { 128, 0, 0, 0 });

            new Byte4(x, y, z, w).ToBytes(bgr, 0, ComponentOrder.ZYX);
            Assert.Equal(bgr, new byte[] { 0, 0, 128 });

            new Byte4(x, y, z, w).ToBytes(bgra, 0, ComponentOrder.ZYXW);
            Assert.Equal(bgra, new byte[] { 0, 0, 128, 0 });
        }

        [Fact]
        public void HalfSingle()
        {
            // Test limits
            Assert.Equal(15360, new HalfSingle(1F).PackedValue);
            Assert.Equal(0, new HalfSingle(0F).PackedValue);
            Assert.Equal(48128, new HalfSingle(-1F).PackedValue);

            // Test values
            Assert.Equal(11878, new HalfSingle(0.1F).PackedValue);
            Assert.Equal(46285, new HalfSingle(-0.3F).PackedValue);

            // Test ordering
            float x = .5F;
            Assert.True(Equal(new Vector4(x, 0, 0, 1), new HalfSingle(x).ToVector4()));

            byte[] rgb = new byte[3];
            byte[] rgba = new byte[4];
            byte[] bgr = new byte[3];
            byte[] bgra = new byte[4];

            new HalfSingle(x).ToBytes(rgb, 0, ComponentOrder.XYZ);
            Assert.Equal(rgb, new byte[] { 128, 0, 0 });

            new HalfSingle(x).ToBytes(rgba, 0, ComponentOrder.XYZW);
            Assert.Equal(rgba, new byte[] { 128, 0, 0, 255 });

            new HalfSingle(x).ToBytes(bgr, 0, ComponentOrder.ZYX);
            Assert.Equal(bgr, new byte[] { 0, 0, 128 });

            new HalfSingle(x).ToBytes(bgra, 0, ComponentOrder.ZYXW);
            Assert.Equal(bgra, new byte[] { 0, 0, 128, 255 });
        }

        [Fact]
        public void HalfVector2()
        {
            // Test PackedValue
            Assert.Equal(0u, new HalfVector2(Vector2.Zero).PackedValue);
            Assert.Equal(1006648320u, new HalfVector2(Vector2.One).PackedValue);
            Assert.Equal(3033345638u, new HalfVector2(0.1f, -0.3f).PackedValue);

            // Test ToVector2
            Assert.True(Equal(Vector2.Zero, new HalfVector2(Vector2.Zero).ToVector2()));
            Assert.True(Equal(Vector2.One, new HalfVector2(Vector2.One).ToVector2()));
            Assert.True(Equal(Vector2.UnitX, new HalfVector2(Vector2.UnitX).ToVector2()));
            Assert.True(Equal(Vector2.UnitY, new HalfVector2(Vector2.UnitY).ToVector2()));

            // Test ordering
            float x = .5F;
            float y = .25F;
            Assert.True(Equal(new Vector4(x, y, 0, 1), new HalfVector2(x, y).ToVector4()));

            byte[] rgb = new byte[3];
            byte[] rgba = new byte[4];
            byte[] bgr = new byte[3];
            byte[] bgra = new byte[4];

            new HalfVector2(x, y).ToBytes(rgb, 0, ComponentOrder.XYZ);
            Assert.Equal(rgb, new byte[] { 128, 64, 0 });

            new HalfVector2(x, y).ToBytes(rgba, 0, ComponentOrder.XYZW);
            Assert.Equal(rgba, new byte[] { 128, 64, 0, 255 });

            new HalfVector2(x, y).ToBytes(bgr, 0, ComponentOrder.ZYX);
            Assert.Equal(bgr, new byte[] { 0, 64, 128 });

            new HalfVector2(x, y).ToBytes(bgra, 0, ComponentOrder.ZYXW);
            Assert.Equal(bgra, new byte[] { 0, 64, 128, 255 });
        }

        [Fact]
        public void HalfVector4()
        {
            // Test PackedValue
            Assert.Equal(0uL, new HalfVector4(Vector4.Zero).PackedValue);
            Assert.Equal(4323521613979991040uL, new HalfVector4(Vector4.One).PackedValue);
            Assert.Equal(13547034390470638592uL, new HalfVector4(-Vector4.One).PackedValue);
            Assert.Equal(15360uL, new HalfVector4(Vector4.UnitX).PackedValue);
            Assert.Equal(1006632960uL, new HalfVector4(Vector4.UnitY).PackedValue);
            Assert.Equal(65970697666560uL, new HalfVector4(Vector4.UnitZ).PackedValue);
            Assert.Equal(4323455642275676160uL, new HalfVector4(Vector4.UnitW).PackedValue);
            Assert.Equal(4035285078724390502uL, new HalfVector4(0.1f, 0.3f, 0.4f, 0.5f).PackedValue);

            // Test ToVector4
            Assert.True(Equal(Vector4.Zero, new HalfVector4(Vector4.Zero).ToVector4()));
            Assert.True(Equal(Vector4.One, new HalfVector4(Vector4.One).ToVector4()));
            Assert.True(Equal(-Vector4.One, new HalfVector4(-Vector4.One).ToVector4()));
            Assert.True(Equal(Vector4.UnitX, new HalfVector4(Vector4.UnitX).ToVector4()));
            Assert.True(Equal(Vector4.UnitY, new HalfVector4(Vector4.UnitY).ToVector4()));
            Assert.True(Equal(Vector4.UnitZ, new HalfVector4(Vector4.UnitZ).ToVector4()));
            Assert.True(Equal(Vector4.UnitW, new HalfVector4(Vector4.UnitW).ToVector4()));

            // Test ordering
            float x = .25F;
            float y = .5F;
            float z = .75F;
            float w = 1F;

            byte[] rgb = new byte[3];
            byte[] rgba = new byte[4];
            byte[] bgr = new byte[3];
            byte[] bgra = new byte[4];

            new HalfVector4(x, y, z, w).ToBytes(rgb, 0, ComponentOrder.XYZ);
            Assert.Equal(rgb, new byte[] { 64, 128, 191 });

            new HalfVector4(x, y, z, w).ToBytes(rgba, 0, ComponentOrder.XYZW);
            Assert.Equal(rgba, new byte[] { 64, 128, 191, 255 });

            new HalfVector4(x, y, z, w).ToBytes(bgr, 0, ComponentOrder.ZYX);
            Assert.Equal(bgr, new byte[] { 191, 128, 64 });

            new HalfVector4(x, y, z, w).ToBytes(bgra, 0, ComponentOrder.ZYXW);
            Assert.Equal(bgra, new byte[] { 191, 128, 64, 255 });
        }

        [Fact]
        public void NormalizedByte2()
        {
            // Test PackedValue
            Assert.Equal(0x0, new NormalizedByte2(Vector2.Zero).PackedValue);
            Assert.Equal(0x7F7F, new NormalizedByte2(Vector2.One).PackedValue);
            Assert.Equal(0x8181, new NormalizedByte2(-Vector2.One).PackedValue);

            // Test ToVector2
            Assert.True(Equal(Vector2.One, new NormalizedByte2(Vector2.One).ToVector2()));
            Assert.True(Equal(Vector2.Zero, new NormalizedByte2(Vector2.Zero).ToVector2()));
            Assert.True(Equal(-Vector2.One, new NormalizedByte2(-Vector2.One).ToVector2()));
            Assert.True(Equal(Vector2.One, new NormalizedByte2(Vector2.One * 1234.0f).ToVector2()));
            Assert.True(Equal(-Vector2.One, new NormalizedByte2(Vector2.One * -1234.0f).ToVector2()));

            // Test ToVector2
            Assert.True(Equal(new Vector4(1, 1, 0, 1), new NormalizedByte2(Vector2.One).ToVector4()));
            Assert.True(Equal(new Vector4(0, 0, 0, 1), new NormalizedByte2(Vector2.Zero).ToVector4()));

            // Test Ordering
            float x = 0.1f;
            float y = -0.3f;
            Assert.Equal(0xda0d, new NormalizedByte2(x, y).PackedValue);

            byte[] rgb = new byte[3];
            byte[] rgba = new byte[4];
            byte[] bgr = new byte[3];
            byte[] bgra = new byte[4];

            new NormalizedByte2(x, y).ToBytes(rgb, 0, ComponentOrder.XYZ);
            Assert.Equal(rgb, new byte[] { 26, 0, 0 });

            new NormalizedByte2(x, y).ToBytes(rgba, 0, ComponentOrder.XYZW);
            Assert.Equal(rgba, new byte[] { 26, 0, 0, 255 });

            new NormalizedByte2(x, y).ToBytes(bgr, 0, ComponentOrder.ZYX);
            Assert.Equal(bgr, new byte[] { 0, 0, 26 });

            new NormalizedByte2(x, y).ToBytes(bgra, 0, ComponentOrder.ZYXW);
            Assert.Equal(bgra, new byte[] { 0, 0, 26, 255 });
        }

        // Comparison helpers with small tolerance to allow for floating point rounding during computations.
        public static bool Equal(float a, float b)
        {
            return Math.Abs(a - b) < 1e-5;
        }

        public static bool Equal(Vector2 a, Vector2 b)
        {
            return Equal(a.X, b.X) && Equal(a.Y, b.Y);
        }

        public static bool Equal(Vector3 a, Vector3 b)
        {
            return Equal(a.X, b.X) && Equal(a.Y, b.Y) && Equal(a.Z, b.Z);
        }

        public static bool Equal(Vector4 a, Vector4 b)
        {
            return Equal(a.X, b.X) && Equal(a.Y, b.Y) && Equal(a.Z, b.Z) && Equal(a.W, b.W);
        }
    }
}