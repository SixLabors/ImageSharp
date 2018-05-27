// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colors
{
    /// <summary>
    /// The packed pixel tests.
    /// </summary>
    /// <remarks>
    /// The "ToVector4" tests should now be covered in <see cref="ColorConstructorTests"/>
    /// and at some point they can be safely removed from here.
    /// </remarks>
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

            // Test ToVector4.
            var vector = new Alpha8(.5F).ToVector4();
            Assert.Equal(0, vector.X);
            Assert.Equal(0, vector.Y);
            Assert.Equal(0, vector.Z);
            Assert.Equal(.5F, vector.W, 2);

            // Test ToScaledVector4.
            Vector4 scaled = new Alpha8(.5F).ToScaledVector4();
            Assert.Equal(0, scaled.X);
            Assert.Equal(0, scaled.Y);
            Assert.Equal(0, scaled.Z);
            Assert.Equal(.5F, scaled.W, 2);

            // Test PackFromScaledVector4.
            Alpha8 alpha = default;
            alpha.PackFromScaledVector4(scaled);
            Assert.Equal(128, alpha.PackedValue);

            // Test Rgb conversion
            Rgb24 rgb = default;
            Rgba32 rgba = default;
            Bgr24 bgr = default;
            Bgra32 bgra = default;
            Argb32 argb = default;

            alpha.ToRgb24(ref rgb);
            Assert.Equal(rgb, new Rgb24(0, 0, 0));

            alpha.ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(0, 0, 0, 128));

            alpha.ToBgr24(ref bgr);
            Assert.Equal(bgr, new Bgr24(0, 0, 0));

            alpha.ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(0, 0, 0, 128));

            alpha.ToArgb32(ref argb);
            Assert.Equal(argb, new Argb32(0, 0, 0, 128));
        }

        [Fact]
        public void Argb32()
        {
            // Test the limits.
            Assert.Equal((uint)0x0, new Argb32(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFFFFFF, new Argb32(Vector4.One).PackedValue);

            // Test ToVector4.
            Assert.True(Equal(Vector4.One, new Argb32(Vector4.One).ToVector4()));
            Assert.True(Equal(Vector4.Zero, new Argb32(Vector4.Zero).ToVector4()));
            Assert.True(Equal(Vector4.UnitX, new Argb32(Vector4.UnitX).ToVector4()));
            Assert.True(Equal(Vector4.UnitY, new Argb32(Vector4.UnitY).ToVector4()));
            Assert.True(Equal(Vector4.UnitZ, new Argb32(Vector4.UnitZ).ToVector4()));
            Assert.True(Equal(Vector4.UnitW, new Argb32(Vector4.UnitW).ToVector4()));

            // Test ToScaledVector4.
            Vector4 scaled = new Argb32(Vector4.One).ToScaledVector4();
            Assert.Equal(1, scaled.X);
            Assert.Equal(1, scaled.Y);
            Assert.Equal(1, scaled.Z);
            Assert.Equal(1, scaled.W);

            // Test PackFromScaledVector4.
            var pixel = default(Argb32);
            pixel.PackFromScaledVector4(scaled);
            Assert.Equal(0xFFFFFFFF, pixel.PackedValue);

            // Test clamping.
            Assert.True(Equal(Vector4.Zero, new Argb32(Vector4.One * -1234.0f).ToVector4()));
            Assert.True(Equal(Vector4.One, new Argb32(Vector4.One * +1234.0f).ToVector4()));

            float x = +0.1f;
            float y = -0.3f;
            float z = +0.5f;
            float w = -0.7f;
            var argb = new Argb32(x, y, z, w);
            Assert.Equal(0x80001a00u, argb.PackedValue);

            // Test ordering
            var rgb = default(Rgb24);
            var rgba = default(Rgba32);
            var bgr = default(Bgr24);
            var bgra = default(Bgra32);
            var argb2 = default(Argb32);

            argb.ToRgb24(ref rgb);
            Assert.Equal(rgb, new Rgb24(0x1a, 0, 0x80));

            argb.ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(0x1a, 0, 0x80, 0));
            Assert.Equal(rgba, argb.ToRgba32());

            argb.ToBgr24(ref bgr);
            Assert.Equal(bgr, new Bgr24(0x1a, 0, 0x80));

            argb.ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(0x1a, 0, 0x80, 0));
            Assert.Equal(bgra, argb.ToBgra32());

            argb.ToArgb32(ref argb2);
            Assert.Equal(argb2, new Argb32(0x1a, 0, 0x80, 0));
            Assert.Equal(argb2, argb.ToArgb32());

            var r = default(Argb32);
            r.PackFromRgba32(new Rgba32(0x1a, 0, 0x80, 0));
            r.ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(0x1a, 0, 0x80, 0));

            r = default(Argb32);
            r.PackFromBgra32(new Bgra32(0x1a, 0, 0x80, 0));
            r.ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(0x1a, 0, 0x80, 0));

            r = default(Argb32);
            r.PackFromArgb32(new Argb32(0x1a, 0, 0x80, 0));
            r.ToArgb32(ref argb);
            Assert.Equal(argb, new Argb32(0x1a, 0, 0x80, 0));
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

            // Test ToScaledVector4.
            Vector4 scaled = new Bgr565(Vector3.One).ToScaledVector4();
            Assert.Equal(1, scaled.X);
            Assert.Equal(1, scaled.Y);
            Assert.Equal(1, scaled.Z);
            Assert.Equal(1, scaled.W);

            // Test PackFromScaledVector4.
            var pixel = default(Bgr565);
            pixel.PackFromScaledVector4(scaled);
            Assert.Equal(0xFFFF, pixel.PackedValue);

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
            var rgb = default(Rgb24);
            var rgba = default(Rgba32);
            var bgr = default(Bgr24);
            var bgra = default(Bgra32);
            var argb = default(Argb32);

            new Bgr565(x, y, z).ToRgb24(ref rgb);
            Assert.Equal(rgb, new Rgb24(25, 0, 132));

            new Bgr565(x, y, z).ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(25, 0, 132, 255));

            new Bgr565(x, y, z).ToBgr24(ref bgr);
            Assert.Equal(bgr, new Bgr24(25, 0, 132));

            new Bgr565(x, y, z).ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(25, 0, 132, 255));

            new Bgr565(x, y, z).ToArgb32(ref argb);
            Assert.Equal(argb, new Argb32(25, 0, 132, 255));
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

            // Test ToScaledVector4.
            Vector4 scaled = new Bgra4444(Vector4.One).ToScaledVector4();
            Assert.Equal(1, scaled.X);
            Assert.Equal(1, scaled.Y);
            Assert.Equal(1, scaled.Z);
            Assert.Equal(1, scaled.W);

            // Test PackFromScaledVector4.
            var pixel = default(Bgra4444);
            pixel.PackFromScaledVector4(scaled);
            Assert.Equal(0xFFFF, pixel.PackedValue);

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
            var rgb = default(Rgb24);
            var rgba = default(Rgba32);
            var bgr = default(Bgr24);
            var bgra = default(Bgra32);
            var argb = default(Argb32);

            new Bgra4444(x, y, z, w).ToRgb24(ref rgb);
            Assert.Equal(rgb, new Rgb24(34, 0, 136));

            new Bgra4444(x, y, z, w).ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(34, 0, 136, 0));

            new Bgra4444(x, y, z, w).ToBgr24(ref bgr);
            Assert.Equal(bgr, new Bgr24(34, 0, 136));

            new Bgra4444(x, y, z, w).ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(34, 0, 136, 0));

            new Bgra4444(x, y, z, w).ToArgb32(ref argb);
            Assert.Equal(argb, new Argb32(34, 0, 136, 0));

            var r = default(Bgra4444);
            r.PackFromRgba32(new Rgba32(34, 0, 136, 0));
            r.ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(34, 0, 136, 0));

            r = default(Bgra4444);
            r.PackFromBgra32(new Bgra32(34, 0, 136, 0));
            r.ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(34, 0, 136, 0));

            r = default(Bgra4444);
            r.PackFromArgb32(new Argb32(34, 0, 136, 0));
            r.ToArgb32(ref argb);
            Assert.Equal(argb, new Argb32(34, 0, 136, 0));
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

            // Test ToScaledVector4.
            Vector4 scaled = new Bgra5551(Vector4.One).ToScaledVector4();
            Assert.Equal(1, scaled.X);
            Assert.Equal(1, scaled.Y);
            Assert.Equal(1, scaled.Z);
            Assert.Equal(1, scaled.W);

            // Test PackFromScaledVector4.
            var pixel = default(Bgra5551);
            pixel.PackFromScaledVector4(scaled);
            Assert.Equal(0xFFFF, pixel.PackedValue);

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
            var rgb = default(Rgb24);
            var rgba = default(Rgba32);
            var bgr = default(Bgr24);
            var bgra = default(Bgra32);
            var argb = default(Argb32);

            new Bgra5551(x, y, z, w).ToRgb24(ref rgb);
            Assert.Equal(rgb, new Rgb24(24, 0, 131));

            new Bgra5551(x, y, z, w).ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(24, 0, 131, 0));

            new Bgra5551(x, y, z, w).ToBgr24(ref bgr);
            Assert.Equal(bgr, new Bgr24(24, 0, 131));

            new Bgra5551(x, y, z, w).ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(24, 0, 131, 0));

            new Bgra5551(x, y, z, w).ToArgb32(ref argb);
            Assert.Equal(argb, new Argb32(24, 0, 131, 0));

            var r = default(Bgra5551);
            r.PackFromRgba32(new Rgba32(24, 0, 131, 0));
            r.ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(24, 0, 131, 0));

            r = default(Bgra5551);
            r.PackFromBgra32(new Bgra32(24, 0, 131, 0));
            r.ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(24, 0, 131, 0));

            r = default(Bgra5551);
            r.PackFromArgb32(new Argb32(24, 0, 131, 0));
            r.ToArgb32(ref argb);
            Assert.Equal(argb, new Argb32(24, 0, 131, 0));
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

            // Test ToScaledVector4.
            Vector4 scaled = new Byte4(Vector4.One * 255).ToScaledVector4();
            Assert.Equal(1, scaled.X);
            Assert.Equal(1, scaled.Y);
            Assert.Equal(1, scaled.Z);
            Assert.Equal(1, scaled.W);

            // Test PackFromScaledVector4.
            var pixel = default(Byte4);
            pixel.PackFromScaledVector4(scaled);
            Assert.Equal(0xFFFFFFFF, pixel.PackedValue);

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
            var rgb = default(Rgb24);
            var rgba = default(Rgba32);
            var bgr = default(Bgr24);
            var bgra = default(Bgra32);
            var argb = default(Argb32);

            new Byte4(x, y, z, w).ToRgb24(ref rgb);
            Assert.Equal(rgb, new Rgb24(128, 0, 0));

            new Byte4(x, y, z, w).ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(128, 0, 0, 0));

            new Byte4(x, y, z, w).ToBgr24(ref bgr);
            Assert.Equal(bgr, new Bgr24(128, 0, 0));

            new Byte4(x, y, z, w).ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(128, 0, 0, 0));

            new Byte4(x, y, z, w).ToArgb32(ref argb);
            Assert.Equal(argb, new Argb32(128, 0, 0, 0));

            var r = default(Byte4);
            r.PackFromRgba32(new Rgba32(20, 38, 0, 255));
            r.ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(20, 38, 0, 255));

            r = default(Byte4);
            r.PackFromBgra32(new Bgra32(20, 38, 0, 255));
            r.ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(20, 38, 0, 255));

            r = default(Byte4);
            r.PackFromArgb32(new Argb32(20, 38, 0, 255));
            r.ToArgb32(ref argb);
            Assert.Equal(argb, new Argb32(20, 38, 0, 255));
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

            // Test ToScaledVector4.
            Vector4 scaled = new HalfSingle(-1F).ToScaledVector4();
            Assert.Equal(0, scaled.X);
            Assert.Equal(0, scaled.Y);
            Assert.Equal(0, scaled.Z);
            Assert.Equal(1, scaled.W);

            // Test PackFromScaledVector4.
            var pixel = default(HalfSingle);
            pixel.PackFromScaledVector4(scaled);
            Assert.Equal(48128, pixel.PackedValue);

            var rgb = default(Rgb24);
            var rgba = default(Rgba32);
            var bgr = default(Bgr24);
            var bgra = default(Bgra32);
            var argb = default(Argb32);

            new HalfSingle(x).ToRgb24(ref rgb);
            Assert.Equal(rgb, new Rgb24(128, 0, 0));

            new HalfSingle(x).ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(128, 0, 0, 255));

            new HalfSingle(x).ToBgr24(ref bgr);
            Assert.Equal(bgr, new Bgr24(128, 0, 0));

            new HalfSingle(x).ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(128, 0, 0, 255));

            new HalfSingle(x).ToArgb32(ref argb);
            Assert.Equal(argb, new Argb32(128, 0, 0, 255));
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

            // Test ToScaledVector4.
            Vector4 scaled = new HalfVector2(Vector2.One).ToScaledVector4();
            Assert.Equal(1F, scaled.X);
            Assert.Equal(1F, scaled.Y);
            Assert.Equal(0, scaled.Z);
            Assert.Equal(1, scaled.W);

            // Test PackFromScaledVector4.
            var pixel = default(HalfVector2);
            pixel.PackFromScaledVector4(scaled);
            Assert.Equal(1006648320u, pixel.PackedValue);

            // Test ordering
            float x = .5F;
            float y = .25F;
            Assert.True(Equal(new Vector4(x, y, 0, 1), new HalfVector2(x, y).ToVector4()));

            var rgb = default(Rgb24);
            var rgba = default(Rgba32);
            var bgr = default(Bgr24);
            var bgra = default(Bgra32);
            var argb = default(Argb32);

            new HalfVector2(x, y).ToRgb24(ref rgb);
            Assert.Equal(rgb, new Rgb24(128, 64, 0));

            new HalfVector2(x, y).ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(128, 64, 0, 255));

            new HalfVector2(x, y).ToBgr24(ref bgr);
            Assert.Equal(bgr, new Bgr24(128, 64, 0));

            new HalfVector2(x, y).ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(128, 64, 0, 255));

            new HalfVector2(x, y).ToArgb32(ref argb);
            Assert.Equal(argb, new Argb32(128, 64, 0, 255));
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

            // Test ToScaledVector4.
            Vector4 scaled = new HalfVector4(-Vector4.One).ToScaledVector4();
            Assert.Equal(0, scaled.X);
            Assert.Equal(0, scaled.Y);
            Assert.Equal(0, scaled.Z);
            Assert.Equal(0, scaled.W);

            // Test PackFromScaledVector4.
            var pixel = default(HalfVector4);
            pixel.PackFromScaledVector4(scaled);
            Assert.Equal(13547034390470638592uL, pixel.PackedValue);

            // Test ordering
            float x = .25F;
            float y = .5F;
            float z = .75F;
            float w = 1F;

            var rgb = default(Rgb24);
            var rgba = default(Rgba32);
            var bgr = default(Bgr24);
            var bgra = default(Bgra32);
            var argb = default(Argb32);

            new HalfVector4(x, y, z, w).ToRgb24(ref rgb);
            Assert.Equal(rgb, new Rgb24(64, 128, 191));

            new HalfVector4(x, y, z, w).ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(64, 128, 191, 255));

            new HalfVector4(x, y, z, w).ToBgr24(ref bgr);
            Assert.Equal(bgr, new Bgr24(64, 128, 191));

            new HalfVector4(x, y, z, w).ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(64, 128, 191, 255));

            new HalfVector4(x, y, z, w).ToArgb32(ref argb);
            Assert.Equal(argb, new Argb32(64, 128, 191, 255));

            var r = default(HalfVector4);
            r.PackFromRgba32(new Rgba32(64, 128, 191, 255));
            r.ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(64, 128, 191, 255));

            r = default(HalfVector4);
            r.PackFromBgra32(new Bgra32(64, 128, 191, 255));
            r.ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(64, 128, 191, 255));

            r = default(HalfVector4);
            r.PackFromArgb32(new Argb32(64, 128, 191, 255));
            r.ToArgb32(ref argb);
            Assert.Equal(argb, new Argb32(64, 128, 191, 255));
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

            // Test ToVector4
            Assert.True(Equal(new Vector4(1, 1, 0, 1), new NormalizedByte2(Vector2.One).ToVector4()));
            Assert.True(Equal(new Vector4(0, 0, 0, 1), new NormalizedByte2(Vector2.Zero).ToVector4()));

            // Test ToScaledVector4.
            Vector4 scaled = new NormalizedByte2(-Vector2.One).ToScaledVector4();
            Assert.Equal(0, scaled.X);
            Assert.Equal(0, scaled.Y);
            Assert.Equal(0, scaled.Z);
            Assert.Equal(1F, scaled.W);

            // Test PackFromScaledVector4.
            var pixel = default(NormalizedByte2);
            pixel.PackFromScaledVector4(scaled);
            Assert.Equal(0x8181, pixel.PackedValue);

            // Test Ordering
            float x = 0.1f;
            float y = -0.3f;
            Assert.Equal(0xda0d, new NormalizedByte2(x, y).PackedValue);
            var n = new NormalizedByte2();
            n.PackFromRgba32(new Rgba32(141, 90, 0, 0));
            Assert.Equal(0xda0d, n.PackedValue);

            var rgb = default(Rgb24);
            var rgba = default(Rgba32);
            var bgr = default(Bgr24);
            var bgra = default(Bgra32);
            var argb = default(Argb32);

            new NormalizedByte2(x, y).ToRgb24(ref rgb);
            Assert.Equal(rgb, new Rgb24(141, 90, 0));

            new NormalizedByte2(x, y).ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(141, 90, 0, 255));

            new NormalizedByte2(x, y).ToBgr24(ref bgr);
            Assert.Equal(bgr, new Bgr24(141, 90, 0));

            new NormalizedByte2(x, y).ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(141, 90, 0, 255));

            new NormalizedByte2(x, y).ToArgb32(ref argb);
            Assert.Equal(argb, new Argb32(141, 90, 0, 255));
        }

        [Fact]
        public void NormalizedByte4()
        {
            if (TestEnvironment.IsLinux)
            {
                // Can't decide if these assertions are robust enough to be portable across CPU architectures.
                // Let's just skip it for 32 bits!
                // TODO: Someone should review this!
                // see https://github.com/SixLabors/ImageSharp/issues/594
                return;
            }

            // Test PackedValue
            Assert.Equal((uint)0x0, new NormalizedByte4(Vector4.Zero).PackedValue);
            Assert.Equal((uint)0x7F7F7F7F, new NormalizedByte4(Vector4.One).PackedValue);
            Assert.Equal(0x81818181, new NormalizedByte4(-Vector4.One).PackedValue);

            // Test ToVector4
            Assert.True(Equal(Vector4.One, new NormalizedByte4(Vector4.One).ToVector4()));
            Assert.True(Equal(Vector4.Zero, new NormalizedByte4(Vector4.Zero).ToVector4()));
            Assert.True(Equal(-Vector4.One, new NormalizedByte4(-Vector4.One).ToVector4()));
            Assert.True(Equal(Vector4.One, new NormalizedByte4(Vector4.One * 1234.0f).ToVector4()));
            Assert.True(Equal(-Vector4.One, new NormalizedByte4(Vector4.One * -1234.0f).ToVector4()));

            // Test ToScaledVector4.
            Vector4 scaled = new NormalizedByte4(-Vector4.One).ToScaledVector4();
            Assert.Equal(0, scaled.X);
            Assert.Equal(0, scaled.Y);
            Assert.Equal(0, scaled.Z);
            Assert.Equal(0, scaled.W);

            // Test PackFromScaledVector4.
            var pixel = default(NormalizedByte4);
            pixel.PackFromScaledVector4(scaled);
            Assert.Equal(0x81818181, pixel.PackedValue);

            // Test Ordering
            float x = 0.1f;
            float y = -0.3f;
            float z = 0.5f;
            float w = -0.7f;
            Assert.Equal(0xA740DA0D, new NormalizedByte4(x, y, z, w).PackedValue);
            var n = default(NormalizedByte4);
            n.PackFromRgba32(new Rgba32(141, 90, 192, 39));
            Assert.Equal(0xA740DA0D, n.PackedValue);

            Assert.Equal((uint)958796544, new NormalizedByte4(0.0008f, 0.15f, 0.30f, 0.45f).PackedValue);

            var rgb = default(Rgb24);
            var rgba = default(Rgba32);
            var bgr = default(Bgr24);
            var bgra = default(Bgra32);
            var argb = default(Argb32);

            new NormalizedByte4(x, y, z, w).ToRgb24(ref rgb);
            Assert.Equal(rgb, new Rgb24(141, 90, 192));

            new NormalizedByte4(x, y, z, w).ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(141, 90, 192, 39));

            new NormalizedByte4(x, y, z, w).ToBgr24(ref bgr);
            Assert.Equal(bgr, new Bgr24(141, 90, 192));

            new NormalizedByte4(x, y, z, w).ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(141, 90, 192, 39));

            new NormalizedByte4(x, y, z, w).ToArgb32(ref argb);
            Assert.Equal(argb, new Argb32(141, 90, 192, 39));

            // http://community.monogame.net/t/normalizedbyte4-texture2d-gives-different-results-from-xna/8012/8
            var r = default(NormalizedByte4);
            r.PackFromRgba32(new Rgba32(9, 115, 202, 127));
            r.ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(9, 115, 202, 127));

            r.PackedValue = 0xff4af389;
            r.ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(9, 115, 202, 127));

            r = default(NormalizedByte4);
            r.PackFromArgb32(new Argb32(9, 115, 202, 127));
            r.ToArgb32(ref argb);
            Assert.Equal(argb, new Argb32(9, 115, 202, 127));

            r = default(NormalizedByte4);
            r.PackFromBgra32(new Bgra32(9, 115, 202, 127));
            r.ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(9, 115, 202, 127));
        }

        [Fact]
        public void NormalizedShort2()
        {
            Assert.Equal((uint)0x0, new NormalizedShort2(Vector2.Zero).PackedValue);
            Assert.Equal((uint)0x7FFF7FFF, new NormalizedShort2(Vector2.One).PackedValue);
            Assert.Equal(0x80018001, new NormalizedShort2(-Vector2.One).PackedValue);

            Assert.True(Equal(Vector2.One, new NormalizedShort2(Vector2.One).ToVector2()));
            Assert.True(Equal(Vector2.Zero, new NormalizedShort2(Vector2.Zero).ToVector2()));
            Assert.True(Equal(-Vector2.One, new NormalizedShort2(-Vector2.One).ToVector2()));
            Assert.True(Equal(Vector2.One, new NormalizedShort2(Vector2.One * 1234.0f).ToVector2()));
            Assert.True(Equal(-Vector2.One, new NormalizedShort2(Vector2.One * -1234.0f).ToVector2()));

            Assert.True(Equal(new Vector4(1, 1, 0, 1), (new NormalizedShort2(Vector2.One)).ToVector4()));
            Assert.True(Equal(new Vector4(0, 0, 0, 1), (new NormalizedShort2(Vector2.Zero)).ToVector4()));

            // Test ToScaledVector4.
            Vector4 scaled = new NormalizedShort2(-Vector2.One).ToScaledVector4();
            Assert.Equal(0, scaled.X);
            Assert.Equal(0, scaled.Y);
            Assert.Equal(0, scaled.Z);
            Assert.Equal(1, scaled.W);

            // Test PackFromScaledVector4.
            var pixel = default(NormalizedShort2);
            pixel.PackFromScaledVector4(scaled);
            Assert.Equal(0x80018001, pixel.PackedValue);

            // Test Ordering
            float x = 0.35f;
            float y = -0.2f;
            Assert.Equal(0xE6672CCC, new NormalizedShort2(x, y).PackedValue);
            x = 0.1f;
            y = -0.3f;
            Assert.Equal(3650751693, new NormalizedShort2(x, y).PackedValue);

            var rgb = default(Rgb24);
            var rgba = default(Rgba32);
            var bgr = default(Bgr24);
            var bgra = default(Bgra32);
            var argb = default(Argb32);

            var n = new NormalizedShort2();
            n.PackFromRgba32(new Rgba32(141, 90, 0, 0));
            n.ToRgb24(ref rgb);
            Assert.Equal(rgb, new Rgb24(141, 90, 0));

            // TODO: I don't think this can ever pass since the bytes are already truncated.
            // Assert.Equal(3650751693, n.PackedValue);

            new NormalizedShort2(x, y).ToRgb24(ref rgb);
            Assert.Equal(rgb, new Rgb24(141, 90, 0));

            new NormalizedShort2(x, y).ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(141, 90, 0, 255));

            new NormalizedShort2(x, y).ToBgr24(ref bgr);
            Assert.Equal(bgr, new Bgr24(141, 90, 0));

            new NormalizedShort2(x, y).ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(141, 90, 0, 255));

            new NormalizedShort2(x, y).ToArgb32(ref argb);
            Assert.Equal(argb, new Argb32(141, 90, 0, 255));
        }

        [Fact]
        public void NormalizedShort4()
        {
            if (TestEnvironment.IsLinux)
            {
                // Can't decide if these assertions are robust enough to be portable across CPU architectures.
                // Let's just skip it for 32 bits!
                // TODO: Someone should review this!
                // see https://github.com/SixLabors/ImageSharp/issues/594
                return;
            }

            // Test PackedValue
            Assert.Equal((ulong)0x0, new NormalizedShort4(Vector4.Zero).PackedValue);
            Assert.Equal((ulong)0x7FFF7FFF7FFF7FFF, new NormalizedShort4(Vector4.One).PackedValue);
            Assert.Equal(0x8001800180018001, new NormalizedShort4(-Vector4.One).PackedValue);

            // Test ToVector4
            Assert.True(Equal(Vector4.One, new NormalizedShort4(Vector4.One).ToVector4()));
            Assert.True(Equal(Vector4.Zero, new NormalizedShort4(Vector4.Zero).ToVector4()));
            Assert.True(Equal(-Vector4.One, new NormalizedShort4(-Vector4.One).ToVector4()));
            Assert.True(Equal(Vector4.One, new NormalizedShort4(Vector4.One * 1234.0f).ToVector4()));
            Assert.True(Equal(-Vector4.One, new NormalizedShort4(Vector4.One * -1234.0f).ToVector4()));

            // Test ToScaledVector4.
            Vector4 scaled = new NormalizedShort4(Vector4.One).ToScaledVector4();
            Assert.Equal(1, scaled.X);
            Assert.Equal(1, scaled.Y);
            Assert.Equal(1, scaled.Z);
            Assert.Equal(1, scaled.W);

            // Test PackFromScaledVector4.
            var pixel = default(NormalizedShort4);
            pixel.PackFromScaledVector4(scaled);
            Assert.Equal((ulong)0x7FFF7FFF7FFF7FFF, pixel.PackedValue);

            // Test Ordering
            float x = 0.1f;
            float y = -0.3f;
            float z = 0.5f;
            float w = -0.7f;
            Assert.Equal(0xa6674000d99a0ccd, new NormalizedShort4(x, y, z, w).PackedValue);
            Assert.Equal((ulong)4150390751449251866, new NormalizedShort4(0.0008f, 0.15f, 0.30f, 0.45f).PackedValue);

            var rgb = default(Rgb24);
            var rgba = default(Rgba32);
            var bgr = default(Bgr24);
            var bgra = default(Bgra32);
            var argb = default(Argb32);

            new NormalizedShort4(x, y, z, w).ToRgb24(ref rgb);
            Assert.Equal(rgb, new Rgb24(141, 90, 192));

            new NormalizedShort4(x, y, z, w).ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(141, 90, 192, 39));

            new NormalizedShort4(x, y, z, w).ToBgr24(ref bgr);
            Assert.Equal(bgr, new Bgr24(141, 90, 192));

            new NormalizedShort4(x, y, z, w).ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(141, 90, 192, 39));

            new NormalizedShort4(x, y, z, w).ToArgb32(ref argb);
            Assert.Equal(argb, new Argb32(141, 90, 192, 39));

            var r = default(NormalizedShort4);
            r.PackFromRgba32(new Rgba32(9, 115, 202, 127));
            r.ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(9, 115, 202, 127));

            r = default(NormalizedShort4);
            r.PackFromBgra32(new Bgra32(9, 115, 202, 127));
            r.ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(9, 115, 202, 127));

            r = default(NormalizedShort4);
            r.PackFromArgb32(new Argb32(9, 115, 202, 127));
            r.ToArgb32(ref argb);
            Assert.Equal(argb, new Argb32(9, 115, 202, 127));
        }

        [Fact]
        public void Rg32()
        {
            // Test the limits.
            Assert.Equal((uint)0x0, new Rg32(Vector2.Zero).PackedValue);
            Assert.Equal(0xFFFFFFFF, new Rg32(Vector2.One).PackedValue);

            // Test ToVector2
            Assert.True(Equal(Vector2.Zero, new Rg32(Vector2.Zero).ToVector2()));
            Assert.True(Equal(Vector2.One, new Rg32(Vector2.One).ToVector2()));

            // Test ToScaledVector4.
            Vector4 scaled = new Rg32(Vector2.One).ToScaledVector4();
            Assert.Equal(1, scaled.X);
            Assert.Equal(1, scaled.Y);
            Assert.Equal(0, scaled.Z);
            Assert.Equal(1, scaled.W);

            // Test PackFromScaledVector4.
            var pixel = default(Rg32);
            pixel.PackFromScaledVector4(scaled);
            Assert.Equal(0xFFFFFFFF, pixel.PackedValue);

            // Test clamping.
            Assert.True(Equal(Vector2.Zero, new Rg32(Vector2.One * -1234.0f).ToVector2()));
            Assert.True(Equal(Vector2.One, new Rg32(Vector2.One * 1234.0f).ToVector2()));

            // Test Ordering
            float x = 0xb6dc;
            float y = 0xA59f;
            Assert.Equal(0xa59fb6dc, new Rg32(x / 0xffff, y / 0xffff).PackedValue);
            x = 0.1f;
            y = -0.3f;
            Assert.Equal((uint)6554, new Rg32(x, y).PackedValue);

            // Test ordering
            var rgb = default(Rgb24);
            var rgba = default(Rgba32);
            var bgr = default(Bgr24);
            var bgra = default(Bgra32);
            var argb = default(Argb32);

            new Rg32(x, y).ToRgb24(ref rgb);
            Assert.Equal(rgb, new Rgb24(25, 0, 0));

            new Rg32(x, y).ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(25, 0, 0, 255));

            new Rg32(x, y).ToBgr24(ref bgr);
            Assert.Equal(bgr, new Bgr24(25, 0, 0));

            new Rg32(x, y).ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(25, 0, 0, 255));

            new Rg32(x, y).ToArgb32(ref argb);
            Assert.Equal(argb, new Argb32(25, 0, 0, 255));
        }

        [Fact]
        public void Rgba1010102()
        {
            // Test the limits.
            Assert.Equal((uint)0x0, new Rgba1010102(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFFFFFF, new Rgba1010102(Vector4.One).PackedValue);

            // Test ToVector4
            Assert.True(Equal(Vector4.Zero, new Rgba1010102(Vector4.Zero).ToVector4()));
            Assert.True(Equal(Vector4.One, new Rgba1010102(Vector4.One).ToVector4()));

            // Test ToScaledVector4.
            Vector4 scaled = new Rgba1010102(Vector4.One).ToScaledVector4();
            Assert.Equal(1, scaled.X);
            Assert.Equal(1, scaled.Y);
            Assert.Equal(1, scaled.Z);
            Assert.Equal(1, scaled.W);

            // Test PackFromScaledVector4.
            var pixel = default(Rgba1010102);
            pixel.PackFromScaledVector4(scaled);
            Assert.Equal(0xFFFFFFFF, pixel.PackedValue);

            // Test clamping.
            Assert.True(Equal(Vector4.Zero, new Rgba1010102(Vector4.One * -1234.0f).ToVector4()));
            Assert.True(Equal(Vector4.One, new Rgba1010102(Vector4.One * 1234.0f).ToVector4()));

            // Test Ordering
            float x = 0x2db;
            float y = 0x36d;
            float z = 0x3b7;
            float w = 0x1;
            Assert.Equal((uint)0x7B7DB6DB, new Rgba1010102(x / 0x3ff, y / 0x3ff, z / 0x3ff, w / 3).PackedValue);
            x = 0.1f;
            y = -0.3f;
            z = 0.5f;
            w = -0.7f;
            Assert.Equal((uint)536871014, new Rgba1010102(x, y, z, w).PackedValue);

            var rgb = default(Rgb24);
            var rgba = default(Rgba32);
            var bgr = default(Bgr24);
            var bgra = default(Bgra32);
            var argb = default(Argb32);

            new Rgba1010102(x, y, z, w).ToRgb24(ref rgb);
            Assert.Equal(rgb, new Rgb24(25, 0, 128));

            new Rgba1010102(x, y, z, w).ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(25, 0, 128, 0));

            new Rgba1010102(x, y, z, w).ToBgr24(ref bgr);
            Assert.Equal(bgr, new Bgr24(25, 0, 128));

            new Rgba1010102(x, y, z, w).ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(25, 0, 128, 0));

            // Alpha component accuracy will be awful.
            var r = default(Rgba1010102);
            r.PackFromRgba32(new Rgba32(25, 0, 128, 0));
            r.ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(25, 0, 128, 0));

            r = default(Rgba1010102);
            r.PackFromBgra32(new Bgra32(25, 0, 128, 0));
            r.ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(25, 0, 128, 0));

            r = default(Rgba1010102);
            r.PackFromArgb32(new Argb32(25, 0, 128, 0));
            r.ToArgb32(ref argb);
            Assert.Equal(argb, new Argb32(25, 0, 128, 0));
        }

        [Fact]
        public void Rgba32()
        {
            // Test the limits.
            Assert.Equal((uint)0x0, new Rgba32(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFFFFFF, new Rgba32(Vector4.One).PackedValue);

            // Test ToVector4.
            Assert.True(Equal(Vector4.One, new Rgba32(Vector4.One).ToVector4()));
            Assert.True(Equal(Vector4.Zero, new Rgba32(Vector4.Zero).ToVector4()));
            Assert.True(Equal(Vector4.UnitX, new Rgba32(Vector4.UnitX).ToVector4()));
            Assert.True(Equal(Vector4.UnitY, new Rgba32(Vector4.UnitY).ToVector4()));
            Assert.True(Equal(Vector4.UnitZ, new Rgba32(Vector4.UnitZ).ToVector4()));
            Assert.True(Equal(Vector4.UnitW, new Rgba32(Vector4.UnitW).ToVector4()));

            // Test ToScaledVector4.
            Vector4 scaled = new Rgba32(Vector4.One).ToScaledVector4();
            Assert.Equal(1, scaled.X);
            Assert.Equal(1, scaled.Y);
            Assert.Equal(1, scaled.Z);
            Assert.Equal(1, scaled.W);

            // Test PackFromScaledVector4.
            var pixel = default(Rgba32);
            pixel.PackFromScaledVector4(scaled);
            Assert.Equal(0xFFFFFFFF, pixel.PackedValue);

            // Test clamping.
            Assert.True(Equal(Vector4.Zero, new Rgba32(Vector4.One * -1234.0f).ToVector4()));
            Assert.True(Equal(Vector4.One, new Rgba32(Vector4.One * +1234.0f).ToVector4()));

            float x = +0.1f;
            float y = -0.3f;
            float z = +0.5f;
            float w = -0.7f;
            var rgba32 = new Rgba32(x, y, z, w);
            Assert.Equal(0x80001Au, rgba32.PackedValue);

            // Test ordering
            var rgb = default(Rgb24);
            var rgba = default(Rgba32);
            var bgr = default(Bgr24);
            var bgra = default(Bgra32);
            var argb = default(Argb32);

            rgba32.ToRgb24(ref rgb);
            Assert.Equal(rgb, new Rgb24(0x1a, 0, 0x80));

            rgba32.ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(0x1a, 0, 0x80, 0));
            Assert.Equal(rgba, rgba.ToRgba32());

            rgba32.ToBgr24(ref bgr);
            Assert.Equal(bgr, new Bgr24(0x1a, 0, 0x80));

            rgba32.ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(0x1a, 0, 0x80, 0));
            Assert.Equal(bgra, bgra.ToBgra32());

            rgba32.ToArgb32(ref argb);
            Assert.Equal(argb, new Argb32(0x1a, 0, 0x80, 0));
            Assert.Equal(argb, argb.ToArgb32());

            var r = default(Rgba32);
            r.PackFromRgba32(new Rgba32(0x1a, 0, 0x80, 0));
            r.ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(0x1a, 0, 0x80, 0));

            r = default(Rgba32);
            r.PackFromBgra32(new Bgra32(0x1a, 0, 0x80, 0));
            r.ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(0x1a, 0, 0x80, 0));

            r = default(Rgba32);
            r.PackFromArgb32(new Argb32(0x1a, 0, 0x80, 0));
            r.ToArgb32(ref argb);
            Assert.Equal(argb, new Argb32(0x1a, 0, 0x80, 0));
        }

        [Fact]
        public void Rgba64()
        {
            // Test the limits.
            Assert.Equal((ulong)0x0, new Rgba64(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFFFFFFFFFFFFFF, new Rgba64(Vector4.One).PackedValue);

            // Test ToVector4
            Assert.True(Equal(Vector4.Zero, new Rgba64(Vector4.Zero).ToVector4()));
            Assert.True(Equal(Vector4.One, new Rgba64(Vector4.One).ToVector4()));

            // Test ToScaledVector4.
            Vector4 scaled = new Rgba64(Vector4.One).ToScaledVector4();
            Assert.Equal(1, scaled.X);
            Assert.Equal(1, scaled.Y);
            Assert.Equal(1, scaled.Z);
            Assert.Equal(1, scaled.W);

            // Test PackFromScaledVector4.
            var pixel = default(Rgba64);
            pixel.PackFromScaledVector4(scaled);
            Assert.Equal(0xFFFFFFFFFFFFFFFF, pixel.PackedValue);

            // Test clamping.
            Assert.True(Equal(Vector4.Zero, new Rgba64(Vector4.One * -1234.0f).ToVector4()));
            Assert.True(Equal(Vector4.One, new Rgba64(Vector4.One * 1234.0f).ToVector4()));

            // Test data ordering
            Assert.Equal(0xC7AD8F5C570A1EB8, new Rgba64(((float)0x1EB8) / 0xffff, ((float)0x570A) / 0xffff, ((float)0x8F5C) / 0xffff, ((float)0xC7AD) / 0xffff).PackedValue);
            Assert.Equal(0xC7AD8F5C570A1EB8, new Rgba64(0.12f, 0.34f, 0.56f, 0.78f).PackedValue);

            float x = 0.08f;
            float y = 0.15f;
            float z = 0.30f;
            float w = 0.45f;
            Assert.Equal((ulong)0x73334CCC2666147B, new Rgba64(x, y, z, w).PackedValue);

            var rgb = default(Rgb24);
            var rgba = default(Rgba32);
            var bgr = default(Bgr24);
            var bgra = default(Bgra32);

            new Rgba64(x, y, z, w).ToRgb24(ref rgb);
            Assert.Equal(rgb, new Rgb24(20, 38, 76));

            new Rgba64(x, y, z, w).ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(20, 38, 76, 115));

            new Rgba64(x, y, z, w).ToBgr24(ref bgr);
            Assert.Equal(bgr, new Bgr24(20, 38, 76));

            new Rgba64(x, y, z, w).ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(20, 38, 76, 115));

            var r = default(Rgba64);
            r.PackFromRgba32(new Rgba32(20, 38, 76, 115));
            r.ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(20, 38, 76, 115));
        }

        [Fact]
        public void Short2()
        {
            // Test the limits.
            Assert.Equal((uint)0x0, new Short2(Vector2.Zero).PackedValue);
            Assert.Equal((uint)0x7FFF7FFF, new Short2(Vector2.One * 0x7FFF).PackedValue);
            Assert.Equal(0x80008000, new Short2(Vector2.One * -0x8000).PackedValue);

            // Test ToVector2.
            Assert.True(Equal(Vector2.One * 0x7FFF, new Short2(Vector2.One * 0x7FFF).ToVector2()));
            Assert.True(Equal(Vector2.Zero, new Short2(Vector2.Zero).ToVector2()));
            Assert.True(Equal(Vector2.One * -0x8000, new Short2(Vector2.One * -0x8000).ToVector2()));
            Assert.True(Equal(Vector2.UnitX * 0x7FFF, new Short2(Vector2.UnitX * 0x7FFF).ToVector2()));
            Assert.True(Equal(Vector2.UnitY * 0x7FFF, new Short2(Vector2.UnitY * 0x7FFF).ToVector2()));

            // Test clamping.
            Assert.True(Equal(Vector2.One * 0x7FFF, new Short2(Vector2.One * 1234567.0f).ToVector2()));
            Assert.True(Equal(Vector2.One * -0x8000, new Short2(Vector2.One * -1234567.0f).ToVector2()));

            // Test ToVector4.
            Assert.True(Equal(new Vector4(0x7FFF, 0x7FFF, 0, 1), (new Short2(Vector2.One * 0x7FFF)).ToVector4()));
            Assert.True(Equal(new Vector4(0, 0, 0, 1), (new Short2(Vector2.Zero)).ToVector4()));
            Assert.True(Equal(new Vector4(-0x8000, -0x8000, 0, 1), (new Short2(Vector2.One * -0x8000)).ToVector4()));

            // Test ToScaledVector4.
            Vector4 scaled = new Short2(Vector2.One * 0x7FFF).ToScaledVector4();
            Assert.Equal(1, scaled.X);
            Assert.Equal(1, scaled.Y);
            Assert.Equal(0, scaled.Z);
            Assert.Equal(1, scaled.W);

            // Test PackFromScaledVector4.
            var pixel = default(Short2);
            pixel.PackFromScaledVector4(scaled);
            Assert.Equal((uint)0x7FFF7FFF, pixel.PackedValue);

            // Test ordering
            float x = 0x2db1;
            float y = 0x361d;
            Assert.Equal((uint)0x361d2db1, new Short2(x, y).PackedValue);
            x = 127.5f;
            y = -5.3f;
            Assert.Equal(4294639744, new Short2(x, y).PackedValue);

            var rgb = default(Rgb24);
            var rgba = default(Rgba32);
            var bgr = default(Bgr24);
            var bgra = default(Bgra32);

            new Short2(x, y).ToRgb24(ref rgb);
            Assert.Equal(rgb, new Rgb24(128, 127, 0));

            new Short2(x, y).ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(128, 127, 0, 255));

            new Short2(x, y).ToBgr24(ref bgr);
            Assert.Equal(bgr, new Bgr24(128, 127, 0));

            new Short2(x, y).ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(128, 127, 0, 255));

            var r = default(Short2);
            r.PackFromRgba32(new Rgba32(20, 38, 0, 255));
            r.ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(20, 38, 0, 255));
        }

        [Fact]
        public void Short4_TestPackedValues()
        {
            var shortValue1 = new Short4(11547, 12653, 29623, 193);
            var shortValue2 = new Short4(0.1f, -0.3f, 0.5f, -0.7f);

            ulong expectedPackedValue1 = 0x00c173b7316d2d1b;
            ulong expectedPackedValue2 = 18446462598732840960;
            Assert.Equal(expectedPackedValue1, shortValue1.PackedValue);
            Assert.Equal(expectedPackedValue2, shortValue2.PackedValue);
            Assert.Equal((ulong)0x0, new Short4(Vector4.Zero).PackedValue);
            Assert.Equal((ulong)0x7FFF7FFF7FFF7FFF, new Short4(Vector4.One * 0x7FFF).PackedValue);
            Assert.Equal(0x8000800080008000, new Short4(Vector4.One * -0x8000).PackedValue);
        }

        [Fact]
        public void Short4_ToVector4()
        {
            // Test ToVector4.
            Assert.Equal(Vector4.One * 0x7FFF, new Short4(Vector4.One * 0x7FFF).ToVector4());
            Assert.Equal(Vector4.Zero, new Short4(Vector4.Zero).ToVector4());
            Assert.Equal(Vector4.One * -0x8000, new Short4(Vector4.One * -0x8000).ToVector4());
            Assert.Equal(Vector4.UnitX * 0x7FFF, new Short4(Vector4.UnitX * 0x7FFF).ToVector4());
            Assert.Equal(Vector4.UnitY * 0x7FFF, new Short4(Vector4.UnitY * 0x7FFF).ToVector4());
            Assert.Equal(Vector4.UnitZ * 0x7FFF, new Short4(Vector4.UnitZ * 0x7FFF).ToVector4());
            Assert.Equal(Vector4.UnitW * 0x7FFF, new Short4(Vector4.UnitW * 0x7FFF).ToVector4());
        }

        [Fact]
        public void Short4_ToScaledVector4()
        {
            // Test ToScaledVector4.
            // arrange
            var short4 = new Short4(Vector4.One * 0x7FFF);

            // act
            Vector4 scaled = short4.ToScaledVector4();

            // assert
            Assert.Equal(1, scaled.X);
            Assert.Equal(1, scaled.Y);
            Assert.Equal(1, scaled.Z);
            Assert.Equal(1, scaled.W);
        }

        [Fact]
        public void Short4_PackFromScaledVector4()
        {
            // Test PackFromScaledVector4.
            // arrange
            var short4 = new Short4(Vector4.One * 0x7FFF);
            Vector4 scaled = short4.ToScaledVector4();

            // act
            var pixel = default(Short4);
            pixel.PackFromScaledVector4(scaled);

            // assert
            long expectedPackedValue = 0x7FFF7FFF7FFF7FFF;
            Assert.Equal((ulong)expectedPackedValue, pixel.PackedValue);
        }

        [Fact]
        public void Short4_Clamping()
        {
            // Test clamping.
            // arrange
            var short1 = new Short4(Vector4.One * 1234567.0f);
            var short2 = new Short4(Vector4.One * -1234567.0f);

            // act
           var vector1 = short1.ToVector4();
           var vector2 = short2.ToVector4();

            // assert
            Assert.Equal(Vector4.One * 0x7FFF, vector1);
            Assert.Equal(Vector4.One * -0x8000, vector2);
        }

        [Fact]
        public void Short4_ToRgb24()
        {
            // arrange
            var shortValue = new Short4(11547, 12653, 29623, 193);
            var rgb24 = default(Rgb24);

            // act
            shortValue.ToRgb24(ref rgb24);

            // assert
            var expectedRgb24 = new Rgb24(172, 177, 243);
            Assert.Equal(expectedRgb24, rgb24);
        }

        [Fact]
        public void Short4_ToBgr24()
        {
            // arrange
            var shortValue = new Short4(11547, 12653, 29623, 193);
            var bgr24 = default(Bgr24);

            // act
            shortValue.ToBgr24(ref bgr24);

            // assert
            var expectedBgr24 = new Bgr24(172, 177, 243);
            Assert.Equal(expectedBgr24, bgr24);
        }

        [Fact]
        public void Short4_ToRgba32()
        {
            // arrange
            var shortValue = new Short4(11547, 12653, 29623, 193);
            var rgba32 = default(Rgba32);

            // act
            shortValue.ToRgba32(ref rgba32);

            // assert
            var expectedRgba32 = new Rgba32(172, 177, 243, 128);
            Assert.Equal(expectedRgba32, rgba32);
        }

        [Fact]
        public void Short4_ToBgra32()
        {
            // arrange
            var shortValue = new Short4(11547, 12653, 29623, 193);
            var bgra32 = default(Bgra32);

            // act
            shortValue.ToBgra32(ref bgra32);

            // assert
            var expectedBgra32 = new Bgra32(172, 177, 243, 128);
            Assert.Equal(expectedBgra32, bgra32);
        }

        [Fact]
        public void Short4_ToArgb32()
        {
            // arrange
            var shortValue = new Short4(11547, 12653, 29623, 193);
            var argb32 = default(Argb32);

            // act
            shortValue.ToArgb32(ref argb32);

            // assert
            var expectedArgb32 = new Argb32(172, 177, 243, 128);
            Assert.Equal(expectedArgb32, argb32);            
        }

        [Fact]
        public void Short4_PackFromRgba32_ToRgba32()
        {
            // arrange
            var rgba32 = default(Rgba32);
            var short4 = default(Short4);

            // act 
            short4.PackFromRgba32(new Rgba32(20, 38, 0, 255));
            short4.ToRgba32(ref rgba32);

            // assert
            var expectedRgba32 = new Rgba32(20, 38, 0, 255);
            Assert.Equal(rgba32, expectedRgba32);
        }

        [Fact]
        public void Short4_PackFromBgra32_ToRgba32()
        {
            // arrange
            var bgra32 = default(Bgra32);
            var short4 = default(Short4);

            // act 
            short4.PackFromBgra32(new Bgra32(20, 38, 0, 255));
            short4.ToBgra32(ref bgra32);

            // assert
            var expectedBgra32 = new Bgra32(20, 38, 0, 255);
            Assert.Equal(bgra32, expectedBgra32);
        }

        [Fact]
        public void Short4_PackFromArgb32_ToRgba32()
        {
            // arrange
            var argb32 = default(Argb32);
            var short4 = default(Short4);

            // act 
            short4.PackFromArgb32(new Argb32(20, 38, 0, 255));
            short4.ToArgb32(ref argb32);

            // assert
            var expectedArgb32 = new Argb32(20, 38, 0, 255);
            Assert.Equal(argb32, expectedArgb32);
        }

        [Fact]
        public void Short4()
        {
            if (TestEnvironment.IsLinux)
            {
                // Can't decide if these assertions are robust enough to be portable across CPU architectures.
                // Let's just skip it for 32 bits!
                // TODO: Someone should review this!
                // see https://github.com/SixLabors/ImageSharp/issues/594
                return;
            }

            // Test the limits.
            Assert.Equal((ulong)0x0, new Short4(Vector4.Zero).PackedValue);
            Assert.Equal((ulong)0x7FFF7FFF7FFF7FFF, new Short4(Vector4.One * 0x7FFF).PackedValue);
            Assert.Equal(0x8000800080008000, new Short4(Vector4.One * -0x8000).PackedValue);

            // Test ToVector4.
            Assert.Equal(Vector4.One * 0x7FFF, new Short4(Vector4.One * 0x7FFF).ToVector4());
            Assert.Equal(Vector4.Zero, new Short4(Vector4.Zero).ToVector4());
            Assert.Equal(Vector4.One * -0x8000, new Short4(Vector4.One * -0x8000).ToVector4());
            Assert.Equal(Vector4.UnitX * 0x7FFF, new Short4(Vector4.UnitX * 0x7FFF).ToVector4());
            Assert.Equal(Vector4.UnitY * 0x7FFF, new Short4(Vector4.UnitY * 0x7FFF).ToVector4());
            Assert.Equal(Vector4.UnitZ * 0x7FFF, new Short4(Vector4.UnitZ * 0x7FFF).ToVector4());
            Assert.Equal(Vector4.UnitW * 0x7FFF, new Short4(Vector4.UnitW * 0x7FFF).ToVector4());

            // Test ToScaledVector4.
            Vector4 scaled = new Short4(Vector4.One * 0x7FFF).ToScaledVector4();
            Assert.Equal(1, scaled.X);
            Assert.Equal(1, scaled.Y);
            Assert.Equal(1, scaled.Z);
            Assert.Equal(1, scaled.W);

            // Test PackFromScaledVector4.
            var pixel = default(Short4);
            pixel.PackFromScaledVector4(scaled);
            Assert.Equal((ulong)0x7FFF7FFF7FFF7FFF, pixel.PackedValue);

            // Test clamping.
            Assert.Equal(Vector4.One * 0x7FFF, new Short4(Vector4.One * 1234567.0f).ToVector4());
            Assert.Equal(Vector4.One * -0x8000, new Short4(Vector4.One * -1234567.0f).ToVector4());

            // Test Ordering
            float x = 0.1f;
            float y = -0.3f;
            float z = 0.5f;
            float w = -0.7f;
            Assert.Equal(18446462598732840960, new Short4(x, y, z, w).PackedValue);

            x = 11547;
            y = 12653;
            z = 29623;
            w = 193;
            Assert.Equal((ulong)0x00c173b7316d2d1b, new Short4(x, y, z, w).PackedValue);

            var rgb = default(Rgb24);
            var rgba = default(Rgba32);
            var bgr = default(Bgr24);
            var bgra = default(Bgra32);
            var argb = default(Argb32);

            new Short4(x, y, z, w).ToRgb24(ref rgb);
            Assert.Equal(rgb, new Rgb24(172, 177, 243)); // this seems to be causing the problem #594
            
            new Short4(x, y, z, w).ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(172, 177, 243, 128));

            new Short4(x, y, z, w).ToBgr24(ref bgr);
            Assert.Equal(bgr, new Bgr24(172, 177, 243));

            new Short4(x, y, z, w).ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(172, 177, 243, 128));

            new Short4(x, y, z, w).ToArgb32(ref argb);
            Assert.Equal(argb, new Argb32(172, 177, 243, 128));

            var r = default(Short4);
            r.PackFromRgba32(new Rgba32(20, 38, 0, 255));
            r.ToRgba32(ref rgba);
            Assert.Equal(rgba, new Rgba32(20, 38, 0, 255));

            r = default(Short4);
            r.PackFromBgra32(new Bgra32(20, 38, 0, 255));
            r.ToBgra32(ref bgra);
            Assert.Equal(bgra, new Bgra32(20, 38, 0, 255));

            r = default(Short4);
            r.PackFromArgb32(new Argb32(20, 38, 0, 255));
            r.ToArgb32(ref argb);
            Assert.Equal(argb, new Argb32(20, 38, 0, 255));
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