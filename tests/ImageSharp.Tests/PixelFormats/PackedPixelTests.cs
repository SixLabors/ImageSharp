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
        public void Alpha8_PackedValue()
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
        }

        [Fact]
        public void Alpha8_ToVector4()
        {
            // arrange
            var alpha = new Alpha8(.5F);

            // act
            var actual = alpha.ToVector4();

            // assert
            Assert.Equal(0, actual.X);
            Assert.Equal(0, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(.5F, actual.W, 2);
        }

        [Fact]
        public void Alpha8_ToScaledVector4()
        {
            // arrange
            var alpha = new Alpha8(.5F);

            // act
            Vector4 actual = alpha.ToScaledVector4();

            // assert
            Assert.Equal(0, actual.X);
            Assert.Equal(0, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(.5F, actual.W, 2);
        }

        [Fact]
        public void Alpha8_PackFromScaledVector4()
        {
            // arrange
            Alpha8 alpha = default;
            int expected = 128;
            Vector4 scaled = new Alpha8(.5F).ToScaledVector4();

            // act
            alpha.PackFromScaledVector4(scaled);
            byte actual = alpha.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Alpha8_PackFromScaledVector4_ToRgb24()
        {
            // arrange
            Rgb24 actual = default;
            Alpha8 alpha = default;
            var expected = new Rgb24(0, 0, 0);
            Vector4 scaled = new Alpha8(.5F).ToScaledVector4();

            // act
            alpha.PackFromScaledVector4(scaled);
            alpha.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Alpha8_PackFromScaledVector4_ToRgba32()
        {
            // arrange
            Rgba32 actual = default;
            Alpha8 alpha = default;
            var expected = new Rgba32(0, 0, 0, 128);
            Vector4 scaled = new Alpha8(.5F).ToScaledVector4();

            // act
            alpha.PackFromScaledVector4(scaled);
            alpha.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Alpha8_PackFromScaledVector4_ToBgr24()
        {
            // arrange
            Bgr24 actual = default;
            Alpha8 alpha = default;
            var expected = new Bgr24(0, 0, 0);
            Vector4 scaled = new Alpha8(.5F).ToScaledVector4();

            // act
            alpha.PackFromScaledVector4(scaled);
            alpha.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Alpha8_PackFromScaledVector4_ToBgra32()
        {
            // arrange
            Bgra32 actual = default;
            Alpha8 alpha = default;
            var expected = new Bgra32(0, 0, 0, 128);
            Vector4 scaled = new Alpha8(.5F).ToScaledVector4();

            // act
            alpha.PackFromScaledVector4(scaled);
            alpha.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Alpha8_PackFromScaledVector4_ToArgb32()
        {
            // arrange
            Alpha8 alpha = default;
            Argb32 actual = default;
            var expected = new Argb32(0, 0, 0, 128);
            Vector4 scaled = new Alpha8(.5F).ToScaledVector4();

            // act
            alpha.PackFromScaledVector4(scaled);
            alpha.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Argb32_PackedValue()
        {
            Assert.Equal(0x80001a00u, new Argb32(+0.1f, -0.3f, +0.5f, -0.7f).PackedValue);
            Assert.Equal((uint)0x0, new Argb32(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFFFFFF, new Argb32(Vector4.One).PackedValue);
        }

        [Fact]
        public void Argb32_ToVector4()
        {
            Assert.Equal(Vector4.One, new Argb32(Vector4.One).ToVector4());
            Assert.Equal(Vector4.Zero, new Argb32(Vector4.Zero).ToVector4());
            Assert.Equal(Vector4.UnitX, new Argb32(Vector4.UnitX).ToVector4());
            Assert.Equal(Vector4.UnitY, new Argb32(Vector4.UnitY).ToVector4());
            Assert.Equal(Vector4.UnitZ, new Argb32(Vector4.UnitZ).ToVector4());
            Assert.Equal(Vector4.UnitW, new Argb32(Vector4.UnitW).ToVector4());
        }

        [Fact]
        public void Argb32_ToScaledVector4()
        {
            // arrange
            var argb = new Argb32(Vector4.One);

            // act
            Vector4 actual = argb.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Argb32_PackFromScaledVector4()
        {
            // arrange
            Vector4 scaled = new Argb32(Vector4.One).ToScaledVector4();
            var pixel = default(Argb32);
            uint expected = 0xFFFFFFFF;

            // act
            pixel.PackFromScaledVector4(scaled);
            uint actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Argb32_Clamping()
        {
            Assert.Equal(Vector4.Zero, new Argb32(Vector4.One * -1234.0f).ToVector4());
            Assert.Equal(Vector4.One, new Argb32(Vector4.One * +1234.0f).ToVector4());
        }

        [Fact]
        public void Argb32_ToRgb24()
        {
            // arrange
            var argb = new Argb32(+0.1f, -0.3f, +0.5f, -0.7f);
            var actual = default(Rgb24);
            var expected = new Rgb24(0x1a, 0, 0x80);

            // act
            argb.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Argb32_ToRgba32()
        {
            // arrange
            var argb = new Argb32(+0.1f, -0.3f, +0.5f, -0.7f);
            var actual = default(Rgba32);
            var expected = new Rgba32(0x1a, 0, 0x80, 0);

            // act
            argb.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Argb32_ToBgr24()
        {
            // arrange
            var argb = new Argb32(+0.1f, -0.3f, +0.5f, -0.7f);
            var actual = default(Bgr24);
            var expected = new Bgr24(0x1a, 0, 0x80);

            // act
            argb.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Argb32_ToBgra32()
        {
            // arrange
            var argb = new Argb32(+0.1f, -0.3f, +0.5f, -0.7f);
            var actual = default(Bgra32);
            var expected = new Bgra32(0x1a, 0, 0x80, 0);

            // act
            argb.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Argb32_ToArgb32()
        {
            // arrange
            var argb = new Argb32(+0.1f, -0.3f, +0.5f, -0.7f);
            var actual = default(Argb32);
            var expected = new Argb32(0x1a, 0, 0x80, 0);

            // act
            argb.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Argb32_PackFromRgba32_ToRgba32()
        {
            // arrange
            var argb = default(Argb32);
            var actual = default(Rgba32);
            var expected = new Rgba32(0x1a, 0, 0x80, 0);

            // act
            argb.PackFromRgba32(expected);
            argb.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Argb32_PackFromBgra32_ToBgra32()
        {
            // arrange
            var argb = default(Argb32);
            var actual = default(Bgra32);
            var expected = new Bgra32(0x1a, 0, 0x80, 0);

            // act
            argb.PackFromBgra32(expected);
            argb.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Argb32_PackFromArgb32_ToArgb32()
        {
            // arrange
            var argb = default(Argb32);
            var actual = default(Argb32);
            var expected = new Argb32(0x1a, 0, 0x80, 0);

            // act
            argb.PackFromArgb32(expected);
            argb.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgr565_PackedValue()
        {
            Assert.Equal(6160, new Bgr565(0.1F, -0.3F, 0.5F).PackedValue);
            Assert.Equal(0x0, new Bgr565(Vector3.Zero).PackedValue);
            Assert.Equal(0xFFFF, new Bgr565(Vector3.One).PackedValue);
            // Make sure the swizzle is correct.
            Assert.Equal(0xF800, new Bgr565(Vector3.UnitX).PackedValue);
            Assert.Equal(0x07E0, new Bgr565(Vector3.UnitY).PackedValue);
            Assert.Equal(0x001F, new Bgr565(Vector3.UnitZ).PackedValue);
        }

        [Fact]
        public void Bgr565_ToVector3()
        {
            Assert.True(Equal(Vector3.One, new Bgr565(Vector3.One).ToVector3()));
            Assert.True(Equal(Vector3.Zero, new Bgr565(Vector3.Zero).ToVector3()));
            Assert.True(Equal(Vector3.UnitX, new Bgr565(Vector3.UnitX).ToVector3()));
            Assert.True(Equal(Vector3.UnitY, new Bgr565(Vector3.UnitY).ToVector3()));
            Assert.True(Equal(Vector3.UnitZ, new Bgr565(Vector3.UnitZ).ToVector3()));
        }

        [Fact]
        public void Bgr565_ToScaledVector4()
        {
            // arrange
            var bgr = new Bgr565(Vector3.One);

            // act
            Vector4 actual = bgr.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Bgr565_PackFromScaledVector4()
        {
            // arrange
            Vector4 scaled = new Bgr565(Vector3.One).ToScaledVector4();
            int expected = 0xFFFF;
            var pixel = default(Bgr565);

            // act
            pixel.PackFromScaledVector4(scaled);
            ushort actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgr565_Clamping()
        {
            Assert.Equal(Vector3.Zero, new Bgr565(Vector3.One * -1234F).ToVector3());
            Assert.Equal(Vector3.One, new Bgr565(Vector3.One * 1234F).ToVector3());
        }

        [Fact]
        public void Bgr565_ToRgb24()
        {
            // arrange
            var bgra = new Bgr565(0.1F, -0.3F, 0.5F);
            var actual = default(Rgb24);
            var expected = new Rgb24(25, 0, 132);

            // act
            bgra.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgr565_ToRgba32()
        {
            // arrange
            var bgra = new Bgr565(0.1F, -0.3F, 0.5F);
            var actual = default(Rgba32);
            var expected = new Rgba32(25, 0, 132, 255);

            // act
            bgra.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgr565_ToBgr24()
        {
            // arrange
            var bgra = new Bgr565(0.1F, -0.3F, 0.5F);
            var actual = default(Bgr24);
            var expected = new Bgr24(25, 0, 132);

            // act
            bgra.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgr565_ToBgra32()
        {
            // arrange
            var bgra = new Bgr565(0.1F, -0.3F, 0.5F);
            var actual = default(Bgra32);
            var expected = new Bgra32(25, 0, 132, 255);

            // act
            bgra.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgr565_ToArgb32()
        {
            // arrange
            var bgra = new Bgr565(0.1F, -0.3F, 0.5F);
            var actual = default(Argb32);
            var expected = new Argb32(25, 0, 132, 255);

            // act
            bgra.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra4444_PackedValue()
        {
            Assert.Equal(520, new Bgra4444(0.1f, -0.3f, 0.5f, -0.7f).PackedValue);
            Assert.Equal(0x0, new Bgra4444(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFF, new Bgra4444(Vector4.One).PackedValue);
            Assert.Equal(0x0F00, new Bgra4444(Vector4.UnitX).PackedValue);
            Assert.Equal(0x00F0, new Bgra4444(Vector4.UnitY).PackedValue);
            Assert.Equal(0x000F, new Bgra4444(Vector4.UnitZ).PackedValue);
            Assert.Equal(0xF000, new Bgra4444(Vector4.UnitW).PackedValue);
        }

        [Fact]
        public void Bgra4444_ToVector4()
        {
            Assert.Equal(Vector4.One, new Bgra4444(Vector4.One).ToVector4());
            Assert.Equal(Vector4.Zero, new Bgra4444(Vector4.Zero).ToVector4());
            Assert.Equal(Vector4.UnitX, new Bgra4444(Vector4.UnitX).ToVector4());
            Assert.Equal(Vector4.UnitY, new Bgra4444(Vector4.UnitY).ToVector4());
            Assert.Equal(Vector4.UnitZ, new Bgra4444(Vector4.UnitZ).ToVector4());
            Assert.Equal(Vector4.UnitW, new Bgra4444(Vector4.UnitW).ToVector4());
        }

        [Fact]
        public void Bgra4444_ToScaledVector4()
        {
            // arrange
            var bgra = new Bgra4444(Vector4.One);

            // act
            Vector4 actual = bgra.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Bgra4444_PackFromScaledVector4()
        {
            // arrange
            Vector4 scaled = new Bgra4444(Vector4.One).ToScaledVector4();
            int expected = 0xFFFF;
            var bgra = default(Bgra4444);

            // act
            bgra.PackFromScaledVector4(scaled);
            ushort actual = bgra.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra4444_Clamping()
        {
            Assert.Equal(Vector4.Zero, new Bgra4444(Vector4.One * -1234.0f).ToVector4());
            Assert.Equal(Vector4.One, new Bgra4444(Vector4.One * 1234.0f).ToVector4());
        }

        [Fact]
        public void Bgra4444_ToRgb24()
        {
            // arrange
            var bgra = new Bgra4444(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Rgb24);
            var expected = new Rgb24(34, 0, 136);

            // act
            bgra.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra4444_ToRgba32()
        {
            // arrange
            var bgra = new Bgra4444(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Rgba32);
            var expected = new Rgba32(34, 0, 136, 0);

            // act
            bgra.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra4444_ToBgr24()
        {
            // arrange
            var bgra = new Bgra4444(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Bgr24);
            var expected = new Bgr24(34, 0, 136);

            // act
            bgra.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra4444_ToBgra32()
        {
            // arrange
            var bgra = new Bgra4444(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Bgra32);
            var expected = new Bgra32(34, 0, 136, 0);

            // act
            bgra.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra4444_ToArgb32()
        {
            // arrange
            var bgra = new Bgra4444(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Argb32);
            var expected = new Argb32(34, 0, 136, 0);

            // act
            bgra.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra4444_PackFromRgba32_ToRgba32()
        {
            // arrange
            var bgra = default(Bgra4444);
            var actual = default(Rgba32);
            var expected = new Rgba32(34, 0, 136, 0);
            
            // act
            bgra.PackFromRgba32(expected);
            bgra.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra4444_PackFromBgra32_ToBgra32()
        {
            // arrange
            var bgra = default(Bgra4444);
            var actual = default(Bgra32);
            var expected = new Bgra32(34, 0, 136, 0);

            // act
            bgra.PackFromBgra32(expected);
            bgra.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra4444_PackFromArgb32_ToArgb32()
        {
            // arrange
            var bgra = default(Bgra4444);
            var actual = default(Argb32);
            var expected = new Argb32(34, 0, 136, 0);

            // act
            bgra.PackFromArgb32(expected);
            bgra.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra5551_PackedValue()
        {
            float x = 0x1a;
            float y = 0x16;
            float z = 0xd;
            float w = 0x1;
            Assert.Equal(0xeacd, new Bgra5551(x / 0x1f, y / 0x1f, z / 0x1f, w).PackedValue);
            Assert.Equal(3088, new Bgra5551(0.1f, -0.3f, 0.5f, -0.7f).PackedValue);
            // Test the limits.
            Assert.Equal(0x0, new Bgra5551(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFF, new Bgra5551(Vector4.One).PackedValue);
        }

        [Fact]
        public void Bgra5551_ToVector4()
        {
            Assert.Equal(Vector4.Zero, new Bgra5551(Vector4.Zero).ToVector4());
            Assert.Equal(Vector4.One, new Bgra5551(Vector4.One).ToVector4());
        }

        [Fact]
        public void Bgra5551_ToScaledVector4()
        {
            // arrange
            var bgra = new Bgra5551(Vector4.One);

            // act 
            Vector4 actual = bgra.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Bgra5551_PackFromScaledVector4()
        {
            // arrange
            Vector4 scaled = new Bgra5551(Vector4.One).ToScaledVector4();
            int expected = 0xFFFF;
            var pixel = default(Bgra5551);
            pixel.PackFromScaledVector4(scaled);

            // act
            pixel.PackFromScaledVector4(scaled);
            ushort actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra5551_Clamping()
        {
            Assert.Equal(Vector4.Zero, new Bgra5551(Vector4.One * -1234.0f).ToVector4());
            Assert.Equal(Vector4.One, new Bgra5551(Vector4.One * 1234.0f).ToVector4());
        }

        [Fact]
        public void Bgra5551_ToRgb24()
        {
            // arrange
            var bgra = new Bgra5551(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Rgb24);
            var expected = new Rgb24(24, 0, 131);

            // act
            bgra.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra5551_Rgba32()
        {
            // arrange
            var bgra = new Bgra5551(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Rgba32);
            var expected = new Rgba32(24, 0, 131, 0);

            // act
            bgra.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra5551_ToBgr24()
        {
            // arrange
            var bgra = new Bgra5551(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Bgr24);
            var expected = new Bgr24(24, 0, 131);

            // act
            bgra.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra5551_Bgra32()
        {
            // arrange
            var bgra = new Bgra5551(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Bgra32);
            var expected = new Bgra32(24, 0, 131, 0);

            // act
            bgra.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra5551_ToArgb32()
        {
            // arrange
            var bgra = new Bgra5551(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Argb32);
            var expected = new Argb32(24, 0, 131, 0);

            // act
            bgra.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra5551_PackFromRgba32_ToRgba32()
        {
            // arrange
            var bgra = default(Bgra5551);
            var expected = new Rgba32(24, 0, 131, 0);
            var actual = default(Rgba32);

            // act
            bgra.PackFromRgba32(expected);
            bgra.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra5551_PackFromBgra32_ToBgra32()
        {
            // arrange
            var bgra = default(Bgra5551);
            var expected = new Bgra32(24, 0, 131, 0);
            var actual = default(Bgra32);

            // act
            bgra.PackFromBgra32(expected);
            bgra.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra5551_PackFromArgb32_ToArgb32()
        {
            // arrange
            var bgra = default(Bgra5551);
            var expected = new Argb32(24, 0, 131, 0);
            var actual = default(Argb32);

            // act
            bgra.PackFromArgb32(expected);
            bgra.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Byte4_PackedValue()
        {
            Assert.Equal((uint)128, new Byte4(127.5f, -12.3f, 0.5f, -0.7f).PackedValue);
            Assert.Equal((uint)0x1a7b362d, new Byte4(0x2d, 0x36, 0x7b, 0x1a).PackedValue);
            Assert.Equal((uint)0x0, new Byte4(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFFFFFF, new Byte4(Vector4.One * 255).PackedValue);
        }

        [Fact]
        public void Byte4_ToVector4()
        {
            Assert.Equal(Vector4.One * 255, new Byte4(Vector4.One * 255).ToVector4());
            Assert.Equal(Vector4.Zero, new Byte4(Vector4.Zero).ToVector4());
            Assert.Equal(Vector4.UnitX * 255, new Byte4(Vector4.UnitX * 255).ToVector4());
            Assert.Equal(Vector4.UnitY * 255, new Byte4(Vector4.UnitY * 255).ToVector4());
            Assert.Equal(Vector4.UnitZ * 255, new Byte4(Vector4.UnitZ * 255).ToVector4());
            Assert.Equal(Vector4.UnitW * 255, new Byte4(Vector4.UnitW * 255).ToVector4());
        }

        [Fact]
        public void Byte4_ToScaledVector4()
        {
            // arrange
            var byte4 = new Byte4(Vector4.One * 255);

            // act
            Vector4 actual = byte4.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Byte4_PackFromScaledVector4()
        {
            // arrange
            Vector4 scaled = new Byte4(Vector4.One * 255).ToScaledVector4();
            var pixel = default(Byte4);
            uint expected = 0xFFFFFFFF;

            // act
            pixel.PackFromScaledVector4(scaled);
            uint actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Byte4_Clamping()
        {
            Assert.True(Equal(Vector4.Zero, new Byte4(Vector4.One * -1234.0f).ToVector4()));
            Assert.True(Equal(Vector4.One * 255, new Byte4(Vector4.One * 1234.0f).ToVector4()));
        }

        [Fact]
        public void Byte4_ToRgb24()
        {
            // arrange
            var byte4 = new Byte4(127.5f, -12.3f, 0.5f, -0.7f);
            var actual = default(Rgb24);
            var expected = new Rgb24(128, 0, 0);

            // act
            byte4.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Byte4_Rgba32()
        {
            // arrange
            var byte4 = new Byte4(127.5f, -12.3f, 0.5f, -0.7f);
            var actual = default(Rgba32);
            var expected = new Rgba32(128, 0, 0, 0);

            // act
            byte4.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Byte4_ToBgr24()
        {
            // arrange
            var byte4 = new Byte4(127.5f, -12.3f, 0.5f, -0.7f);
            var actual = default(Bgr24);
            var expected = new Bgr24(128, 0, 0);

            // act
            byte4.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Byte4_Bgra32()
        {
            // arrange
            var byte4 = new Byte4(127.5f, -12.3f, 0.5f, -0.7f);
            var actual = default(Bgra32);
            var expected = new Bgra32(128, 0, 0, 0);

            // act
            byte4.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Byte4_Argb32()
        {
            // arrange
            var byte4 = new Byte4(127.5f, -12.3f, 0.5f, -0.7f);
            var actual = default(Argb32);
            var expected = new Argb32(128, 0, 0, 0);

            // act
            byte4.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Byte4_PackFromRgba32_ToRgba32()
        {
            // arrange
            var byte4 = default(Byte4);
            var actual = default(Rgba32);
            var expected = new Rgba32(20, 38, 0, 255);

            // act
            byte4.PackFromRgba32(expected);
            byte4.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Byte4_PackFromBgra32_ToBgra32()
        {
            // arrange
            var byte4 = default(Byte4);
            var actual = default(Bgra32);
            var expected = new Bgra32(20, 38, 0, 255);

            // act
            byte4.PackFromBgra32(expected);
            byte4.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Byte4_PackFromArgb32_ToArgb32()
        {
            // arrange
            var byte4 = default(Byte4);
            var actual = default(Argb32);
            var expected = new Argb32(20, 38, 0, 255);

            // act
            byte4.PackFromArgb32(expected);
            byte4.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfSingle_PackedValue()
        {
            Assert.Equal(11878, new HalfSingle(0.1F).PackedValue);
            Assert.Equal(46285, new HalfSingle(-0.3F).PackedValue);

            // Test limits
            Assert.Equal(15360, new HalfSingle(1F).PackedValue);
            Assert.Equal(0, new HalfSingle(0F).PackedValue);
            Assert.Equal(48128, new HalfSingle(-1F).PackedValue);
        }

        [Fact]
        public void HalfSingle_ToVector4()
        {
            // arrange
            var halfSingle = new HalfSingle(0.5f);
            var expected = new Vector4(0.5f, 0, 0, 1);

            // act
            var actual = halfSingle.ToVector4();

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfSingle_ToScaledVector4()
        {
            // arrange
            var halfSingle = new HalfSingle(-1F);

            // act 
            Vector4 actual = halfSingle.ToScaledVector4();

            // assert
            Assert.Equal(0, actual.X);
            Assert.Equal(0, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void HalfSingle_PackFromScaledVector4()
        {
            // arrange 
            Vector4 scaled = new HalfSingle(-1F).ToScaledVector4();
            int expected = 48128;
            var halfSingle = default(HalfSingle);

            // act
            halfSingle.PackFromScaledVector4(scaled);
            ushort actual = halfSingle.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfSingle_ToRgb24()
        {
            // arrange
            var halfVector = new HalfSingle(.5F);
            var actual = default(Rgb24);
            var expected = new Rgb24(128, 0, 0);

            // act
            halfVector.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfSingle_Rgba32()
        {
            // arrange
            var halfVector = new HalfSingle(.5F);
            var actual = default(Rgba32);
            var expected = new Rgba32(128, 0, 0, 255);

            // act
            halfVector.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfSingle_ToBgr24()
        {
            // arrange
            var halfVector = new HalfSingle(.5F);
            var actual = default(Bgr24);
            var expected = new Bgr24(128, 0, 0);

            // act
            halfVector.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfSingle_Bgra32()
        {
            // arrange
            var halfVector = new HalfSingle(.5F);
            var actual = default(Bgra32);
            var expected = new Bgra32(128, 0, 0, 255);

            // act
            halfVector.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfSingle_Argb32()
        {
            // arrange
            var halfVector = new HalfSingle(.5F);
            var actual = default(Argb32);
            var expected = new Argb32(128, 0, 0, 255);

            // act
            halfVector.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector2_PackedValue()
        {
            Assert.Equal(0u, new HalfVector2(Vector2.Zero).PackedValue);
            Assert.Equal(1006648320u, new HalfVector2(Vector2.One).PackedValue);
            Assert.Equal(3033345638u, new HalfVector2(0.1f, -0.3f).PackedValue);
        }

        [Fact]
        public void HalfVector2_ToVector2()
        {
            Assert.True(Equal(Vector2.Zero, new HalfVector2(Vector2.Zero).ToVector2()));
            Assert.True(Equal(Vector2.One, new HalfVector2(Vector2.One).ToVector2()));
            Assert.True(Equal(Vector2.UnitX, new HalfVector2(Vector2.UnitX).ToVector2()));
            Assert.True(Equal(Vector2.UnitY, new HalfVector2(Vector2.UnitY).ToVector2()));
        }

        [Fact]
        public void HalfVector2_ToScaledVector4()
        {
            // arrange
            var halfVector = new HalfVector2(Vector2.One);

            // act
            Vector4 actual = halfVector.ToScaledVector4();

            // assert
            Assert.Equal(1F, actual.X);
            Assert.Equal(1F, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void HalfVector2_PackFromScaledVector4()
        {
            // arrange
            Vector4 scaled = new HalfVector2(Vector2.One).ToScaledVector4();
            uint expected = 1006648320u;
            var halfVector = default(HalfVector2);

            // act
            halfVector.PackFromScaledVector4(scaled);
            uint actual = halfVector.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector2_ToVector4()
        {
            // arrange
            var halfVector = new HalfVector2(.5F, .25F);
            var expected = new Vector4(0.5f, .25F, 0, 1);

            // act
            var actual = halfVector.ToVector4();

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector2_ToRgb24()
        {
            // arrange
            var halfVector = new HalfVector2(.5F, .25F);
            var actual = default(Rgb24);
            var expected = new Rgb24(128, 64, 0);

            // act
            halfVector.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector2_Rgba32()
        {
            // arrange
            var halfVector = new HalfVector2(.5F, .25F);
            var actual = default(Rgba32);
            var expected = new Rgba32(128, 64, 0, 255);

            // act
            halfVector.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector2_ToBgr24()
        {
            // arrange
            var halfVector = new HalfVector2(.5F, .25F);
            var actual = default(Bgr24);
            var expected = new Bgr24(128, 64, 0);

            // act
            halfVector.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector2_Bgra32()
        {
            // arrange
            var halfVector = new HalfVector2(.5F, .25F);
            var actual = default(Bgra32);
            var expected = new Bgra32(128, 64, 0, 255);

            // act
            halfVector.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector2_Argb32()
        {
            // arrange
            var halfVector = new HalfVector2(.5F, .25F);
            var actual = default(Argb32);
            var expected = new Argb32(128, 64, 0, 255);

            // act
            halfVector.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector4_PackedValue()
        {
            Assert.Equal(0uL, new HalfVector4(Vector4.Zero).PackedValue);
            Assert.Equal(4323521613979991040uL, new HalfVector4(Vector4.One).PackedValue);
            Assert.Equal(13547034390470638592uL, new HalfVector4(-Vector4.One).PackedValue);
            Assert.Equal(15360uL, new HalfVector4(Vector4.UnitX).PackedValue);
            Assert.Equal(1006632960uL, new HalfVector4(Vector4.UnitY).PackedValue);
            Assert.Equal(65970697666560uL, new HalfVector4(Vector4.UnitZ).PackedValue);
            Assert.Equal(4323455642275676160uL, new HalfVector4(Vector4.UnitW).PackedValue);
            Assert.Equal(4035285078724390502uL, new HalfVector4(0.1f, 0.3f, 0.4f, 0.5f).PackedValue);
        }

        [Fact]
        public void HalfVector4_ToVector4()
        {
            Assert.Equal(Vector4.Zero, new HalfVector4(Vector4.Zero).ToVector4());
            Assert.Equal(Vector4.One, new HalfVector4(Vector4.One).ToVector4());
            Assert.Equal(-Vector4.One, new HalfVector4(-Vector4.One).ToVector4());
            Assert.Equal(Vector4.UnitX, new HalfVector4(Vector4.UnitX).ToVector4());
            Assert.Equal(Vector4.UnitY, new HalfVector4(Vector4.UnitY).ToVector4());
            Assert.Equal(Vector4.UnitZ, new HalfVector4(Vector4.UnitZ).ToVector4());
            Assert.Equal(Vector4.UnitW, new HalfVector4(Vector4.UnitW).ToVector4());
        }

        [Fact]
        public void HalfVector4_ToScaledVector4()
        {
            // arrange
            var halfVector4 = new HalfVector4(-Vector4.One);

            // act 
            Vector4 actual = halfVector4.ToScaledVector4();

            // assert
            Assert.Equal(0, actual.X);
            Assert.Equal(0, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(0, actual.W);
        }

        [Fact]
        public void HalfVector4_PackFromScaledVector4()
        {
            // arrange
            var halfVector4 = default(HalfVector4);
            Vector4 scaled = new HalfVector4(-Vector4.One).ToScaledVector4();
            ulong expected = 13547034390470638592uL;

            // act 
            halfVector4.PackFromScaledVector4(scaled);
            ulong actual = halfVector4.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector4_ToRgb24()
        {
            // arrange
            var halfVector = new HalfVector4(.25F, .5F, .75F, 1F);
            var actual = default(Rgb24);
            var expected = new Rgb24(64, 128, 191);

            // act
            halfVector.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector4_Rgba32()
        {
            // arrange
            var halfVector = new HalfVector4(.25F, .5F, .75F, 1F);
            var actual = default(Rgba32);
            var expected = new Rgba32(64, 128, 191, 255);

            // act
            halfVector.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector4_ToBgr24()
        {
            // arrange
            var halfVector = new HalfVector4(.25F, .5F, .75F, 1F);
            var actual = default(Bgr24);
            var expected = new Bgr24(64, 128, 191);

            // act
            halfVector.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector4_Bgra32()
        {
            // arrange
            var halfVector = new HalfVector4(.25F, .5F, .75F, 1F);
            var actual = default(Bgra32);
            var expected = new Bgra32(64, 128, 191, 255);

            // act
            halfVector.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector4_Argb32()
        {
            // arrange
            var halfVector = new HalfVector4(.25F, .5F, .75F, 1F);
            var actual = default(Argb32);
            var expected = new Argb32(64, 128, 191, 255);

            // act
            halfVector.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector4_PackFromRgba32_ToRgba32()
        {
            // arrange
            var halVector = default(HalfVector4);
            var actual = default(Rgba32);
            var expected = new Rgba32(64, 128, 191, 255);

            // act
            halVector.PackFromRgba32(expected);
            halVector.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector4_PackFromBgra32_ToBgra32()
        {
            // arrange
            var halVector = default(HalfVector4);
            var actual = default(Bgra32);
            var expected = new Bgra32(64, 128, 191, 255);

            // act
            halVector.PackFromBgra32(expected);
            halVector.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector4_PackFromArgb32_ToArgb32()
        {
            // arrange
            var halVector = default(HalfVector4);
            var actual = default(Argb32);
            var expected = new Argb32(64, 128, 191, 255);

            // act
            halVector.PackFromArgb32(expected);
            halVector.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte2_PackedValue()
        {
            Assert.Equal(0xda0d, new NormalizedByte2(0.1f, -0.3f).PackedValue);
            Assert.Equal(0x0, new NormalizedByte2(Vector2.Zero).PackedValue);
            Assert.Equal(0x7F7F, new NormalizedByte2(Vector2.One).PackedValue);
            Assert.Equal(0x8181, new NormalizedByte2(-Vector2.One).PackedValue);
        }

        [Fact]
        public void NormalizedByte2_ToVector2()
        {
            Assert.Equal(Vector2.One, new NormalizedByte2(Vector2.One).ToVector2());
            Assert.Equal(Vector2.Zero, new NormalizedByte2(Vector2.Zero).ToVector2());
            Assert.Equal(-Vector2.One, new NormalizedByte2(-Vector2.One).ToVector2());
            Assert.Equal(Vector2.One, new NormalizedByte2(Vector2.One * 1234.0f).ToVector2());
            Assert.Equal(-Vector2.One, new NormalizedByte2(Vector2.One * -1234.0f).ToVector2());
        }

        [Fact]
        public void NormalizedByte2_ToVector4()
        {
            Assert.Equal(new Vector4(1, 1, 0, 1), new NormalizedByte2(Vector2.One).ToVector4());
            Assert.Equal(new Vector4(0, 0, 0, 1), new NormalizedByte2(Vector2.Zero).ToVector4());
        }

        [Fact]
        public void NormalizedByte2_ToScaledVector4()
        {
            // arrange
            var byte2 = new NormalizedByte2(-Vector2.One);

            // act
            Vector4 actual = byte2.ToScaledVector4();

            // assert
            Assert.Equal(0, actual.X);
            Assert.Equal(0, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(1F, actual.W);
        }

        [Fact]
        public void NormalizedByte2_PackFromScaledVector4()
        {
            // arrange
            Vector4 scaled = new NormalizedByte2(-Vector2.One).ToScaledVector4();
            var byte2 = default(NormalizedByte2);
            uint expected = 0x8181;

            // act
            byte2.PackFromScaledVector4(scaled);
            uint actual = byte2.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte2_PackFromRgba32()
        {
            // arrange
            var byte2 = new NormalizedByte2();
            var rgba = new Rgba32(141, 90, 0, 0);
            int expected = 0xda0d;

            // act
            byte2.PackFromRgba32(rgba);
            ushort actual = byte2.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte2_ToRgb24()
        {
            // arrange
            var short4 = new NormalizedByte2(0.1f, -0.3f);
            var actual = default(Rgb24);
            var expected = new Rgb24(141, 90, 0);

            // act
            short4.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte2_ToRgba32()
        {
            // arrange
            var short4 = new NormalizedByte2(0.1f, -0.3f);
            var actual = default(Rgba32);
            var expected = new Rgba32(141, 90, 0, 255);

            // act
            short4.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte2_ToBgr24()
        {
            // arrange
            var short4 = new NormalizedByte2(0.1f, -0.3f);
            var actual = default(Bgr24);
            var expected = new Bgr24(141, 90, 0);

            // act
            short4.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte2_ToBgra32()
        {
            // arrange
            var short4 = new NormalizedByte2(0.1f, -0.3f);
            var actual = default(Bgra32);
            var expected = new Bgra32(141, 90, 0, 255);

            // act
            short4.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte2_ToArgb32()
        {
            // arrange
            var short4 = new NormalizedByte2(0.1f, -0.3f);
            var actual = default(Argb32);
            var expected = new Argb32(141, 90, 0, 255);

            // act
            short4.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte4_PackedValues()
        {
            Assert.Equal(0xA740DA0D, new NormalizedByte4(0.1f, -0.3f, 0.5f, -0.7f).PackedValue);
            Assert.Equal((uint)958796544, new NormalizedByte4(0.0008f, 0.15f, 0.30f, 0.45f).PackedValue);
            Assert.Equal((uint)0x0, new NormalizedByte4(Vector4.Zero).PackedValue);
            Assert.Equal((uint)0x7F7F7F7F, new NormalizedByte4(Vector4.One).PackedValue);
            Assert.Equal(0x81818181, new NormalizedByte4(-Vector4.One).PackedValue);
        }

        [Fact]
        public void NormalizedByte4_ToVector4()
        {
            Assert.Equal(Vector4.One, new NormalizedByte4(Vector4.One).ToVector4());
            Assert.Equal(Vector4.Zero, new NormalizedByte4(Vector4.Zero).ToVector4());
            Assert.Equal(-Vector4.One, new NormalizedByte4(-Vector4.One).ToVector4());
            Assert.Equal(Vector4.One, new NormalizedByte4(Vector4.One * 1234.0f).ToVector4());
            Assert.Equal(-Vector4.One, new NormalizedByte4(Vector4.One * -1234.0f).ToVector4());
        }

        [Fact]
        public void NormalizedByte4_ToScaledVector4()
        {
            // arrange
            var short4 = new NormalizedByte4(-Vector4.One);

            // act
            Vector4 actual = short4.ToScaledVector4();

            // assert
            Assert.Equal(0, actual.X);
            Assert.Equal(0, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(0, actual.W);
        }

        [Fact]
        public void NormalizedByte4_PackFromScaledVector4()
        {
            // arrange
            var pixel = default(NormalizedByte4);
            Vector4 scaled = new NormalizedByte4(-Vector4.One).ToScaledVector4();
            uint expected = 0x81818181;

            // act 
            pixel.PackFromScaledVector4(scaled);
            uint actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte4_ToRgb24()
        {
            // arrange
            var short4 = new NormalizedByte4(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Rgb24);
            var expected = new Rgb24(141, 90, 192);

            // act
            short4.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte4_ToRgba32()
        {
            // arrange
            var short4 = new NormalizedByte4(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Rgba32);
            var expected = new Rgba32(141, 90, 192, 39);

            // act
            short4.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte4_ToBgr24()
        {
            // arrange
            var short4 = new NormalizedByte4(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Bgr24);
            var expected = new Bgr24(141, 90, 192);

            // act
            short4.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte4_ToArgb32()
        {
            // arrange
            var short4 = new NormalizedByte4(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Argb32);
            var expected = new Argb32(141, 90, 192, 39);

            // act
            short4.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte4_PackFromRgba32_ToRgba32()
        {
            // arrange
            var short4 = default(NormalizedByte4);
            var actual = default(Rgba32);
            var expected = new Rgba32(9, 115, 202, 127);

            // act 
            short4.PackFromRgba32(new Rgba32(9, 115, 202, 127));
            short4.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte4_PackFromBgra32_ToRgba32()
        {
            // arrange
            var actual = default(Bgra32);
            var short4 = default(NormalizedByte4);
            var expected = new Bgra32(9, 115, 202, 127);

            // act 
            short4.PackFromBgra32(expected);
            short4.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedByte4_PackFromArgb32_ToRgba32()
        {
            // arrange
            var short4 = default(NormalizedByte4);
            var actual = default(Argb32);
            var expected = new Argb32(9, 115, 202, 127);

            // act 
            short4.PackFromArgb32(expected);
            short4.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort2_PackedValue()
        {
            Assert.Equal(0xE6672CCC, new NormalizedShort2(0.35f, -0.2f).PackedValue);
            Assert.Equal(3650751693, new NormalizedShort2(0.1f, -0.3f).PackedValue);
            Assert.Equal((uint)0x0, new NormalizedShort2(Vector2.Zero).PackedValue);
            Assert.Equal((uint)0x7FFF7FFF, new NormalizedShort2(Vector2.One).PackedValue);
            Assert.Equal(0x80018001, new NormalizedShort2(-Vector2.One).PackedValue);
            // TODO: I don't think this can ever pass since the bytes are already truncated.
            // Assert.Equal(3650751693, n.PackedValue);
        }

        [Fact]
        public void NormalizedShort2_ToVector2()
        {
            Assert.True(Equal(Vector2.One, new NormalizedShort2(Vector2.One).ToVector2()));
            Assert.True(Equal(Vector2.Zero, new NormalizedShort2(Vector2.Zero).ToVector2()));
            Assert.True(Equal(-Vector2.One, new NormalizedShort2(-Vector2.One).ToVector2()));
            Assert.True(Equal(Vector2.One, new NormalizedShort2(Vector2.One * 1234.0f).ToVector2()));
            Assert.True(Equal(-Vector2.One, new NormalizedShort2(Vector2.One * -1234.0f).ToVector2()));
        }

        [Fact]
        public void NormalizedShort2_ToVector4()
        {
            Assert.True(Equal(new Vector4(1, 1, 0, 1), (new NormalizedShort2(Vector2.One)).ToVector4()));
            Assert.True(Equal(new Vector4(0, 0, 0, 1), (new NormalizedShort2(Vector2.Zero)).ToVector4()));
        }

        [Fact]
        public void NormalizedShort2_ToScaledVector4()
        {
            // arrange
            var short2 = new NormalizedShort2(-Vector2.One);

            // act
            Vector4 actual = short2.ToScaledVector4();

            // assert
            Assert.Equal(0, actual.X);
            Assert.Equal(0, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void NormalizedShort2_PackFromScaledVector4()
        {
            // arrange
            Vector4 scaled = new NormalizedShort2(-Vector2.One).ToScaledVector4();
            var short2 = default(NormalizedShort2);
            uint expected = 0x80018001;

            // act
            short2.PackFromScaledVector4(scaled);
            uint actual = short2.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort2_PackFromRgba32_ToRgb24()
        {
            // arrange
            var actual = default(Rgb24);
            var short2 = new NormalizedShort2();
            var rgba = new Rgba32(141, 90, 0, 0);
            var expected = new Rgb24(141, 90, 0);

            // act
            short2.PackFromRgba32(rgba);
            short2.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort2_ToRgb24()
        {
            // arrange
            var short2 = new NormalizedShort2(0.1f, -0.3f);
            var actual = default(Rgb24);
            var expected = new Rgb24(141, 90, 0);

            // act
            short2.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort2_ToRgba32()
        {
            // arrange
            var short2 = new NormalizedShort2(0.1f, -0.3f);
            var actual = default(Rgba32);
            var expected = new Rgba32(141, 90, 0, 255);

            // act
            short2.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort2_ToBgr24()
        {
            // arrange
            var short2 = new NormalizedShort2(0.1f, -0.3f);
            var actual = default(Bgr24);
            var expected = new Bgr24(141, 90, 0);

            // act
            short2.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort2_ToBgra32()
        {
            // arrange
            var short2 = new NormalizedShort2(0.1f, -0.3f);
            var actual = default(Bgra32);
            var expected = new Bgra32(141, 90, 0, 255);

            // act
            short2.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort2_ToArgb32()
        {
            // arrange
            var short2 = new NormalizedShort2(0.1f, -0.3f);
            var actual = default(Argb32);
            var expected = new Argb32(141, 90, 0, 255);

            // act
            short2.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort4_PackedValues()
        {
            Assert.Equal(0xa6674000d99a0ccd, new NormalizedShort4(0.1f, -0.3f, 0.5f, -0.7f).PackedValue);
            Assert.Equal((ulong)4150390751449251866, new NormalizedShort4(0.0008f, 0.15f, 0.30f, 0.45f).PackedValue);
            Assert.Equal((ulong)0x0, new NormalizedShort4(Vector4.Zero).PackedValue);
            Assert.Equal((ulong)0x7FFF7FFF7FFF7FFF, new NormalizedShort4(Vector4.One).PackedValue);
            Assert.Equal(0x8001800180018001, new NormalizedShort4(-Vector4.One).PackedValue);
        }

        [Fact]
        public void NormalizedShort4_ToVector4()
        {
            // Test ToVector4
            Assert.True(Equal(Vector4.One, new NormalizedShort4(Vector4.One).ToVector4()));
            Assert.True(Equal(Vector4.Zero, new NormalizedShort4(Vector4.Zero).ToVector4()));
            Assert.True(Equal(-Vector4.One, new NormalizedShort4(-Vector4.One).ToVector4()));
            Assert.True(Equal(Vector4.One, new NormalizedShort4(Vector4.One * 1234.0f).ToVector4()));
            Assert.True(Equal(-Vector4.One, new NormalizedShort4(Vector4.One * -1234.0f).ToVector4()));
        }

        [Fact]
        public void NormalizedShort4_ToScaledVector4()
        {
            // arrange
            var short4 = new NormalizedShort4(Vector4.One);

            // act
            Vector4 actual = short4.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void NormalizedShort4_PackFromScaledVector4()
        {
            // arrange
            var pixel = default(NormalizedShort4);
            Vector4 scaled = new NormalizedShort4(Vector4.One).ToScaledVector4();
            ulong expected = (ulong)0x7FFF7FFF7FFF7FFF;

            // act 
            pixel.PackFromScaledVector4(scaled);
            ulong actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort4_ToRgb24()
        {
            // arrange
            var short4 = new NormalizedShort4(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Rgb24);
            var expected = new Rgb24(141, 90, 192);

            // act
            short4.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort4_ToRgba32()
        {
            // arrange
            var short4 = new NormalizedShort4(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Rgba32);
            var expected = new Rgba32(141, 90, 192, 39);

            // act
            short4.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort4_ToBgr24()
        {
            // arrange
            var short4 = new NormalizedShort4(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Bgr24);
            var expected = new Bgr24(141, 90, 192);

            // act
            short4.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort4_ToArgb32()
        {
            // arrange
            var short4 = new NormalizedShort4(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Argb32);
            var expected = new Argb32(141, 90, 192, 39);

            // act
            short4.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort4_PackFromRgba32_ToRgba32()
        {
            // arrange
            var short4 = default(NormalizedShort4);
            var expected = new Rgba32(9, 115, 202, 127);
            var actual = default(Rgba32);

            // act 
            short4.PackFromRgba32(expected);
            short4.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort4_PackFromBgra32_ToRgba32()
        {
            // arrange
            var short4 = default(NormalizedShort4);
            var actual = default(Bgra32);
            var expected = new Bgra32(9, 115, 202, 127);

            // act 
            short4.PackFromBgra32(expected);
            short4.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort4_PackFromArgb32_ToRgba32()
        {
            // arrange
            var short4 = default(NormalizedShort4);
            var actual = default(Argb32);
            var expected = new Argb32(9, 115, 202, 127);

            // act 
            short4.PackFromArgb32(expected);
            short4.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rg32_PackedValues()
        {
            float x = 0xb6dc;
            float y = 0xA59f;
            Assert.Equal(0xa59fb6dc, new Rg32(x / 0xffff, y / 0xffff).PackedValue);
            Assert.Equal((uint)6554, new Rg32(0.1f, -0.3f).PackedValue);

            // Test the limits.
            Assert.Equal((uint)0x0, new Rg32(Vector2.Zero).PackedValue);
            Assert.Equal(0xFFFFFFFF, new Rg32(Vector2.One).PackedValue);
        }

        [Fact]
        public void Rg32_ToVector2()
        {
            Assert.Equal(Vector2.Zero, new Rg32(Vector2.Zero).ToVector2());
            Assert.Equal(Vector2.One, new Rg32(Vector2.One).ToVector2());
        }

        [Fact]
        public void Rg32_ToScaledVector4()
        {
            // arrange
            var rg32 = new Rg32(Vector2.One);

            // act
            Vector4 actual = rg32.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Rg32_PackFromScaledVector4()
        {
            // arrange
            var rg32 = new Rg32(Vector2.One);
            var pixel = default(Rg32);
            uint expected = 0xFFFFFFFF;

            // act
            Vector4 scaled = rg32.ToScaledVector4();
            pixel.PackFromScaledVector4(scaled);
            uint actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rg32_Clamping()
        {
            Assert.True(Equal(Vector2.Zero, new Rg32(Vector2.One * -1234.0f).ToVector2()));
            Assert.True(Equal(Vector2.One, new Rg32(Vector2.One * 1234.0f).ToVector2()));
        }

        [Fact]
        public void Rg32_ToRgb24()
        {
            // arrange
            var rg32 = new Rg32(0.1f, -0.3f);
            var actual = default(Rgb24);
            var expected = new Rgb24(25, 0, 0);

            // act
            rg32.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rg32_ToRgba32()
        {
            // arrange
            var rg32 = new Rg32(0.1f, -0.3f);
            var actual = default(Rgba32);
            var expected = new Rgba32(25, 0, 0, 255);

            // act
            rg32.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rg32_ToBgr24()
        {
            // arrange
            var rg32 = new Rg32(0.1f, -0.3f);
            var actual = default(Bgr24);
            var expected = new Bgr24(25, 0, 0);

            // act
            rg32.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rg32_ToArgb32()
        {
            // arrange
            var rg32 = new Rg32(0.1f, -0.3f);
            var actual = default(Argb32);
            var expected = new Argb32(25, 0, 0, 255);

            // act
            rg32.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba1010102_PackedValue()
        {
            float x = 0x2db;
            float y = 0x36d;
            float z = 0x3b7;
            float w = 0x1;
            Assert.Equal((uint)0x7B7DB6DB, new Rgba1010102(x / 0x3ff, y / 0x3ff, z / 0x3ff, w / 3).PackedValue);

            Assert.Equal((uint)536871014, new Rgba1010102(0.1f, -0.3f, 0.5f, -0.7f).PackedValue);

            // Test the limits.
            Assert.Equal((uint)0x0, new Rgba1010102(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFFFFFF, new Rgba1010102(Vector4.One).PackedValue);
        }

        [Fact]
        public void Rgba1010102_ToVector4()
        {
            Assert.Equal(Vector4.Zero, new Rgba1010102(Vector4.Zero).ToVector4());
            Assert.Equal(Vector4.One, new Rgba1010102(Vector4.One).ToVector4());
        }

        [Fact]
        public void Rgba1010102_ToScaledVector4()
        {
            // arrange
            var rgba = new Rgba1010102(Vector4.One);

            // act
            Vector4 actual = rgba.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Rgba1010102_PackFromScaledVector4()
        {
            // arrange
            var rgba = new Rgba1010102(Vector4.One);
            var actual = default(Rgba1010102);
            uint expected = 0xFFFFFFFF;

            // act
            Vector4 scaled = rgba.ToScaledVector4();
            actual.PackFromScaledVector4(scaled);

            // assert
            Assert.Equal(expected, actual.PackedValue);
        }

        [Fact]
        public void Rgba1010102_Clamping()
        {
            Assert.True(Equal(Vector4.Zero, new Rgba1010102(Vector4.One * -1234.0f).ToVector4()));
            Assert.True(Equal(Vector4.One, new Rgba1010102(Vector4.One * 1234.0f).ToVector4()));
        }

        [Fact]
        public void Rgba1010102_ToRgb24()
        {
            // arrange
            var rgba = new Rgba1010102(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Rgb24);
            var expected = new Rgb24(25, 0, 128);

            // act
            rgba.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba1010102_ToRgba32()
        {
            // arrange
            var rgba = new Rgba1010102(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Rgba32);
            var expected = new Rgba32(25, 0, 128, 0);

            // act
            rgba.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba1010102_ToBgr24()
        {
            // arrange
            var rgba = new Rgba1010102(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Bgr24);
            var expected = new Bgr24(25, 0, 128);

            // act
            rgba.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba1010102_ToBgra32()
        {
            // arrange
            var rgba = new Rgba1010102(0.1f, -0.3f, 0.5f, -0.7f);
            var actual = default(Bgra32);
            var expected = new Bgra32(25, 0, 128, 0);

            // act
            rgba.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba1010102_PackFromRgba32_ToRgba32()
        {
            // arrange
            var rgba = default(Rgba1010102);
            var expected = new Rgba32(25, 0, 128, 0);
            var actual = default(Rgba32);

            // act
            rgba.PackFromRgba32(expected);
            rgba.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba1010102_PackFromBgra32_ToBgra32()
        {
            // arrange
            var rgba = default(Rgba1010102);
            var expected = new Bgra32(25, 0, 128, 0);
            var actual = default(Bgra32);

            // act
            rgba.PackFromBgra32(expected);
            rgba.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba1010102_PackFromArgb32_ToArgb32()
        {
            // arrange
            var rgba = default(Rgba1010102);
            var expected = new Argb32(25, 0, 128, 0);
            var actual = default(Argb32);

            // act
            rgba.PackFromArgb32(expected);
            rgba.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba32_PackedValues()
        {
            Assert.Equal(0x80001Au, new Rgba32(+0.1f, -0.3f, +0.5f, -0.7f).PackedValue);
            // Test the limits.
            Assert.Equal((uint)0x0, new Rgba32(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFFFFFF, new Rgba32(Vector4.One).PackedValue);
        }

        [Fact]
        public void Rgba32_ToVector4()
        {
            Assert.True(Equal(Vector4.One, new Rgba32(Vector4.One).ToVector4()));
            Assert.True(Equal(Vector4.Zero, new Rgba32(Vector4.Zero).ToVector4()));
            Assert.True(Equal(Vector4.UnitX, new Rgba32(Vector4.UnitX).ToVector4()));
            Assert.True(Equal(Vector4.UnitY, new Rgba32(Vector4.UnitY).ToVector4()));
            Assert.True(Equal(Vector4.UnitZ, new Rgba32(Vector4.UnitZ).ToVector4()));
            Assert.True(Equal(Vector4.UnitW, new Rgba32(Vector4.UnitW).ToVector4()));
        }

        [Fact]
        public void Rgba32_ToScaledVector4()
        {
            // arrange
            var rgba = new Rgba32(Vector4.One);

            // act
            Vector4 actual = rgba.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Rgba32_PackFromScaledVector4()
        {
            // arrange
            var rgba = new Rgba32(Vector4.One);
            var actual = default(Rgba32);
            uint expected = 0xFFFFFFFF;

            // act
            Vector4 scaled = rgba.ToScaledVector4();
            actual.PackFromScaledVector4(scaled);

            // assert
            Assert.Equal(expected, actual.PackedValue);
        }

        [Fact]
        public void Rgba32_Clamping()
        {
            Assert.Equal(Vector4.Zero, new Rgba32(Vector4.One * -1234.0f).ToVector4());
            Assert.Equal(Vector4.One, new Rgba32(Vector4.One * +1234.0f).ToVector4());
        }

        [Fact]
        public void Rgba32_ToRgb24()
        {
            // arrange
            var rgba = new Rgba32(+0.1f, -0.3f, +0.5f, -0.7f);
            var actual = default(Rgb24);
            var expected = new Rgb24(0x1a, 0, 0x80);

            // act
            rgba.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba32_ToRgba32()
        {
            // arrange
            var rgba = new Rgba32(+0.1f, -0.3f, +0.5f, -0.7f);
            var actual = default(Rgba32);
            var expected = new Rgba32(0x1a, 0, 0x80, 0);

            // act
            rgba.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba32_ToBgr24()
        {
            // arrange
            var rgba = new Rgba32(+0.1f, -0.3f, +0.5f, -0.7f);
            var actual = default(Bgr24);
            var expected = new Bgr24(0x1a, 0, 0x80);

            // act
            rgba.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba32_ToBgra32()
        {
            // arrange
            var rgba = new Rgba32(+0.1f, -0.3f, +0.5f, -0.7f);
            var actual = default(Bgra32);
            var expected = new Bgra32(0x1a, 0, 0x80, 0);

            // act
            rgba.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba32_ToArgb32()
        {
            // arrange
            var rgba = new Rgba32(+0.1f, -0.3f, +0.5f, -0.7f);
            var actual = default(Argb32);
            var expected = new Argb32(0x1a, 0, 0x80, 0);

            // act
            rgba.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba32_PackFromRgba32_ToRgba32()
        {
            // arrange
            var rgba = default(Rgba32);
            var actual = default(Rgba32);
            var expected = new Rgba32(0x1a, 0, 0x80, 0);

            // act 
            rgba.PackFromRgba32(expected);
            rgba.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba32_PackFromBgra32_ToRgba32()
        {
            // arrange
            var rgba = default(Rgba32);
            var actual = default(Bgra32);
            var expected = new Bgra32(0x1a, 0, 0x80, 0);

            // act 
            rgba.PackFromBgra32(expected);
            rgba.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba32_PackFromArgb32_ToArgb32()
        {
            // arrange
            var rgba = default(Rgba32);
            var actual = default(Argb32);
            var expected = new Argb32(0x1a, 0, 0x80, 0);

            // act 
            rgba.PackFromArgb32(expected);
            rgba.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba64_PackedValues()
        {
            Assert.Equal((ulong)0x73334CCC2666147B, new Rgba64(0.08f, 0.15f, 0.30f, 0.45f).PackedValue);
            // Test the limits.
            Assert.Equal((ulong)0x0, new Rgba64(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFFFFFFFFFFFFFF, new Rgba64(Vector4.One).PackedValue);
            // Test data ordering
            Assert.Equal(0xC7AD8F5C570A1EB8, new Rgba64(((float)0x1EB8) / 0xffff, ((float)0x570A) / 0xffff, ((float)0x8F5C) / 0xffff, ((float)0xC7AD) / 0xffff).PackedValue);
            Assert.Equal(0xC7AD8F5C570A1EB8, new Rgba64(0.12f, 0.34f, 0.56f, 0.78f).PackedValue);
        }

        [Fact]
        public void Rgba64_ToVector4()
        {
            Assert.True(Equal(Vector4.Zero, new Rgba64(Vector4.Zero).ToVector4()));
            Assert.True(Equal(Vector4.One, new Rgba64(Vector4.One).ToVector4()));
        }

        [Fact]
        public void Rgba64_ToScaledVector4()
        {
            // arrange
            var short2 = new Rgba64(Vector4.One);

            // act
            Vector4 actual = short2.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Rgba64_PackFromScaledVector4()
        {
            // arrange
            var pixel = default(Rgba64);
            var short4 = new Rgba64(Vector4.One);
            ulong expected = 0xFFFFFFFFFFFFFFFF;

            // act 
            Vector4 scaled = short4.ToScaledVector4();
            pixel.PackFromScaledVector4(scaled);
            ulong actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba64_Clamping()
        {
            Assert.True(Equal(Vector4.Zero, new Rgba64(Vector4.One * -1234.0f).ToVector4()));
            Assert.True(Equal(Vector4.One, new Rgba64(Vector4.One * 1234.0f).ToVector4()));
        }

        [Fact]
        public void Rgba64_ToRgb24()
        {
            // arrange
            var rgba64 = new Rgba64(0.08f, 0.15f, 0.30f, 0.45f);
            var actual = default(Rgb24);
            var expected = new Rgb24(20, 38, 76);

            // act
            rgba64.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba64_ToRgba32()
        {
            // arrange
            var rgba64 = new Rgba64(0.08f, 0.15f, 0.30f, 0.45f);
            var actual = default(Rgba32);
            var expected = new Rgba32(20, 38, 76, 115);

            // act
            rgba64.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba64_ToBgr24()
        {
            // arrange
            var rgba64 = new Rgba64(0.08f, 0.15f, 0.30f, 0.45f);
            var actual = default(Bgr24);
            var expected = new Bgr24(20, 38, 76);

            // act
            rgba64.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba64_ToBgra32()
        {
            // arrange
            var rgba64 = new Rgba64(0.08f, 0.15f, 0.30f, 0.45f);
            var actual = default(Bgra32);
            var expected = new Bgra32(20, 38, 76, 115);

            // act
            rgba64.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba64_PackFromRgba32_ToRgba32()
        {
            // arrange
            var rgba64 = default(Rgba64);
            var actual = default(Rgba32);
            var expected = new Rgba32(20, 38, 76, 115);

            // act 
            rgba64.PackFromRgba32(expected);
            rgba64.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short2_PackedValues()
        {
            // Test ordering
            Assert.Equal((uint)0x361d2db1, new Short2(0x2db1, 0x361d).PackedValue);
            Assert.Equal(4294639744, new Short2(127.5f, -5.3f).PackedValue);
            // Test the limits.
            Assert.Equal((uint)0x0, new Short2(Vector2.Zero).PackedValue);
            Assert.Equal((uint)0x7FFF7FFF, new Short2(Vector2.One * 0x7FFF).PackedValue);
            Assert.Equal(0x80008000, new Short2(Vector2.One * -0x8000).PackedValue);
        }

        [Fact]
        public void Short2_ToVector2()
        {
            Assert.True(Equal(Vector2.One * 0x7FFF, new Short2(Vector2.One * 0x7FFF).ToVector2()));
            Assert.True(Equal(Vector2.Zero, new Short2(Vector2.Zero).ToVector2()));
            Assert.True(Equal(Vector2.One * -0x8000, new Short2(Vector2.One * -0x8000).ToVector2()));
            Assert.True(Equal(Vector2.UnitX * 0x7FFF, new Short2(Vector2.UnitX * 0x7FFF).ToVector2()));
            Assert.True(Equal(Vector2.UnitY * 0x7FFF, new Short2(Vector2.UnitY * 0x7FFF).ToVector2()));
        }

        [Fact]
        public void Short2_ToVector4()
        {
            Assert.True(Equal(new Vector4(0x7FFF, 0x7FFF, 0, 1), (new Short2(Vector2.One * 0x7FFF)).ToVector4()));
            Assert.True(Equal(new Vector4(0, 0, 0, 1), (new Short2(Vector2.Zero)).ToVector4()));
            Assert.True(Equal(new Vector4(-0x8000, -0x8000, 0, 1), (new Short2(Vector2.One * -0x8000)).ToVector4()));
        }

        [Fact]
        public void Short2_Clamping()
        {
            Assert.True(Equal(Vector2.One * 0x7FFF, new Short2(Vector2.One * 1234567.0f).ToVector2()));
            Assert.True(Equal(Vector2.One * -0x8000, new Short2(Vector2.One * -1234567.0f).ToVector2()));
        }

        [Fact]
        public void Short2_ToScaledVector4()
        {
            // arrange
            var short2 = new Short2(Vector2.One * 0x7FFF);

            // act
            Vector4 actual = short2.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Short2_PackFromScaledVector4()
        {
            // arrange
            var pixel = default(Short2);
            var short2 = new Short2(Vector2.One * 0x7FFF);
            ulong expected = 0x7FFF7FFF;

            // act 
            Vector4 scaled = short2.ToScaledVector4();
            pixel.PackFromScaledVector4(scaled);
            uint actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short2_ToRgb24()
        {
            // arrange
            var short2 = new Short2(127.5f, -5.3f);
            var actual = default(Rgb24);
            var expected = new Rgb24(128, 127, 0);

            // act
            short2.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short2_ToRgba32()
        {
            // arrange
            var short2 = new Short2(127.5f, -5.3f);
            var actual = default(Rgba32);
            var expected = new Rgba32(128, 127, 0, 255);

            // act
            short2.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short2_ToBgr24()
        {
            // arrange
            var short2 = new Short2(127.5f, -5.3f);
            var actual = default(Bgr24);
            var expected = new Bgr24(128, 127, 0);

            // act
            short2.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short2_ToArgb32()
        {
            // arrange
            var short2 = new Short2(127.5f, -5.3f);
            var actual = default(Argb32);
            var expected = new Argb32(128, 127, 0, 255);

            // act
            short2.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short2_ToBgra32()
        {
            // arrange
            var short2 = new Short2(127.5f, -5.3f);
            var actual = default(Bgra32);
            var expected = new Bgra32(128, 127, 0, 255);

            // act
            short2.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short2_PackFromRgba32_ToRgba32()
        {
            // arrange
            var short2 = default(Short2);
            var actual = default(Rgba32);
            var expected = new Rgba32(20, 38, 0, 255);

            // act 
            short2.PackFromRgba32(expected);
            short2.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }
        
        [Fact]
        public void Short4_PackedValues()
        {
            var shortValue1 = new Short4(11547, 12653, 29623, 193);
            var shortValue2 = new Short4(0.1f, -0.3f, 0.5f, -0.7f);

            Assert.Equal((ulong)0x00c173b7316d2d1b, shortValue1.PackedValue);
            Assert.Equal(18446462598732840960, shortValue2.PackedValue);
            Assert.Equal((ulong)0x0, new Short4(Vector4.Zero).PackedValue);
            Assert.Equal((ulong)0x7FFF7FFF7FFF7FFF, new Short4(Vector4.One * 0x7FFF).PackedValue);
            Assert.Equal(0x8000800080008000, new Short4(Vector4.One * -0x8000).PackedValue);
        }

        [Fact]
        public void Short4_ToVector4()
        {
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
            // arrange
            var short4 = new Short4(Vector4.One * 0x7FFF);

            // act
            Vector4 actual = short4.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Short4_PackFromScaledVector4()
        {
            // arrange
            var short4 = new Short4(Vector4.One * 0x7FFF);
            Vector4 scaled = short4.ToScaledVector4();
            long expected = 0x7FFF7FFF7FFF7FFF;

            // act
            var pixel = default(Short4);
            pixel.PackFromScaledVector4(scaled);
            ulong actual = pixel.PackedValue;

            // assert
            Assert.Equal((ulong)expected, pixel.PackedValue);
        }

        [Fact]
        public void Short4_Clamping()
        {
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
            var actual = default(Rgb24);
            var expected = new Rgb24(172, 177, 243);

            // act
            shortValue.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short4_ToBgr24()
        {
            // arrange
            var shortValue = new Short4(11547, 12653, 29623, 193);
            var actual = default(Bgr24);
            var expected = new Bgr24(172, 177, 243);

            // act
            shortValue.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short4_ToRgba32()
        {
            // arrange
            var shortValue = new Short4(11547, 12653, 29623, 193);
            var actual = default(Rgba32);
            var expected = new Rgba32(172, 177, 243, 128);

            // act
            shortValue.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short4_ToBgra32()
        {
            // arrange
            var shortValue = new Short4(11547, 12653, 29623, 193);
            var actual = default(Bgra32);
            var expected = new Bgra32(172, 177, 243, 128);

            // act
            shortValue.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short4_ToArgb32()
        {
            // arrange
            var shortValue = new Short4(11547, 12653, 29623, 193);
            var actual = default(Argb32);
            var expected = new Argb32(172, 177, 243, 128);

            // act
            shortValue.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);            
        }

        [Fact]
        public void Short4_PackFromRgba32_ToRgba32()
        {
            // arrange
            var short4 = default(Short4);
            var actual = default(Rgba32);
            var expected = new Rgba32(20, 38, 0, 255);

            // act 
            short4.PackFromRgba32(expected);
            short4.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short4_PackFromBgra32_ToRgba32()
        {
            // arrange
            var short4 = default(Short4);
            var actual = default(Bgra32);
            var expected = new Bgra32(20, 38, 0, 255);

            // act 
            short4.PackFromBgra32(expected);
            short4.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Short4_PackFromArgb32_ToRgba32()
        {
            // arrange
            var short4 = default(Short4);
            var actual = default(Argb32);
            var expected = new Argb32(20, 38, 0, 255);

            // act 
            short4.PackFromArgb32(expected);
            short4.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
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