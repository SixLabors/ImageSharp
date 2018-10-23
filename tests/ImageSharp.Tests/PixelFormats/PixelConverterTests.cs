using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class PixelConverterTests
    {
        public static readonly TheoryData<byte, byte, byte, byte> RgbaData =
            new TheoryData<byte, byte, byte, byte>
                {
                    { 0, 0, 0, 0 },
                    { 0, 0, 0, 255 },
                    { 0, 0, 255, 0 },
                    { 0, 255, 0, 0 },
                    { 255, 0, 0, 0 },
                    { 255, 255, 255, 255 },
                    { 0, 0, 0, 1 },
                    { 0, 0, 1, 0 },
                    { 0, 1, 0, 0 },
                    { 1, 0, 0, 0 },
                    { 3, 5, 7, 11 },
                    { 67, 71, 101, 109 }
                };

        [Theory]
        [MemberData(nameof(RgbaData))]
        public void Rgba32ToArgb32(byte r, byte g, byte b, byte a)
        {
            Rgba32 s = ReferenceImplementations.MakeRgba32(r, g, b, a);

            // Act:
            uint actualPacked = PixelConverter.Rgba32.ToArgb32(s.PackedValue);

            // Assert:
            uint expectedPacked = ReferenceImplementations.ToArgb32(s).PackedValue;

            Assert.Equal(expectedPacked, actualPacked);
        }

        [Theory]
        [MemberData(nameof(RgbaData))]
        public void Argb32ToRgba32(byte r, byte g, byte b, byte a)
        {
            Argb32 s = ReferenceImplementations.MakeArgb32(r, g, b, a);

            // Act:
            uint actualPacked = PixelConverter.Argb32.ToRgba32(s.PackedValue);

            // Assert:
            uint expectedPacked = ReferenceImplementations.ToRgba32(s).PackedValue;

            Assert.Equal(expectedPacked, actualPacked);
        }


        private static class ReferenceImplementations
        {
            public static Rgba32 MakeRgba32(byte r, byte g, byte b, byte a)
            {
                Rgba32 d = default;
                d.R = r;
                d.G = g;
                d.B = b;
                d.A = a;
                return d;
            }

            public static Argb32 MakeArgb32(byte r, byte g, byte b, byte a)
            {
                Argb32 d = default;
                d.R = r;
                d.G = g;
                d.B = b;
                d.A = a;
                return d;
            }

            public static Bgra32 MakeBgra32(byte r, byte g, byte b, byte a)
            {
                Bgra32 d = default;
                d.R = r;
                d.G = g;
                d.B = b;
                d.A = a;
                return d;
            }

            public static Argb32 ToArgb32(Rgba32 s)
            {
                Argb32 d = default;
                d.R = s.R;
                d.G = s.G;
                d.B = s.B;
                d.A = s.A;
                return d;
            }

            public static Argb32 ToArgb32(Bgra32 s)
            {
                Argb32 d = default;
                d.R = s.R;
                d.G = s.G;
                d.B = s.B;
                d.A = s.A;
                return d;
            }

            public static Rgba32 ToRgba32(Argb32 s)
            {
                Rgba32 d = default;
                d.R = s.R;
                d.G = s.G;
                d.B = s.B;
                d.A = s.A;
                return d;
            }

            public static Rgba32 ToRgba32(Bgra32 s)
            {
                Rgba32 d = default;
                d.R = s.R;
                d.G = s.G;
                d.B = s.B;
                d.A = s.A;
                return d;
            }

            public static Bgra32 ToBgra32(Rgba32 s)
            {
                Bgra32 d = default;
                d.R = s.R;
                d.G = s.G;
                d.B = s.B;
                d.A = s.A;
                return d;
            }

            public static Bgra32 ToBgra32(Argb32 s)
            {
                Bgra32 d = default;
                d.R = s.R;
                d.G = s.G;
                d.B = s.B;
                d.A = s.A;
                return d;
            }
        }
    }
}