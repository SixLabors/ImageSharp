using System;
using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Issues
{
    public class Issue594
    {
        // This test fails for unknown reason in Release mode on linux and is meant to help reproducing the issue
        // see https://github.com/SixLabors/ImageSharp/issues/594
        [Fact(Skip = "Skipped because of issue #594")]
        public void NormalizedByte4()
        {
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
            Assert.Equal(bgra, new Bgra32(141, 90, 192, 39));  // this assert fails in Release build on linux (#594)

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

        // This test fails for unknown reason in Release mode on linux and is meant to help reproducing the issue
        // see https://github.com/SixLabors/ImageSharp/issues/594
        [Fact(Skip = "Skipped because of issue #594")]
        public void NormalizedShort4()
        {
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
            Assert.Equal(rgba, new Rgba32(141, 90, 192, 39)); // this assert fails in Release build on linux (#594)

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

        // This test fails for unknown reason in Release mode on linux and is meant to help reproducing the issue
        // see https://github.com/SixLabors/ImageSharp/issues/594
        [Fact(Skip = "Skipped because of issue #594")]
        public void Short4()
        {
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
            Assert.Equal(rgb, new Rgb24(172, 177, 243)); // this assert fails in Release build on linux (#594)

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

        public static bool Equal(Vector4 a, Vector4 b)
        {
            return Equal(a.X, b.X) && Equal(a.Y, b.Y) && Equal(a.Z, b.Z) && Equal(a.W, b.W);
        }
    }
}
