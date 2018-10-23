using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public abstract class PixelConverterTests
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

        public class FromRgba32 : PixelConverterTests
        {
            [Theory]
            [MemberData(nameof(RgbaData))]
            public void ToArgb32(byte r, byte g, byte b, byte a)
            {
                Rgba32 s = ReferenceImplementations.MakeRgba32(r, g, b, a);

                // Act:
                uint actualPacked = PixelConverter.FromRgba32.ToArgb32(s.PackedValue);

                // Assert:
                uint expectedPacked = ReferenceImplementations.MakeArgb32(r, g, b, a).PackedValue;

                Assert.Equal(expectedPacked, actualPacked);
            }

            [Theory]
            [MemberData(nameof(RgbaData))]
            public void ToBgra32(byte r, byte g, byte b, byte a)
            {
                Rgba32 s = ReferenceImplementations.MakeRgba32(r, g, b, a);

                // Act:
                uint actualPacked = PixelConverter.FromRgba32.ToBgra32(s.PackedValue);

                // Assert:
                uint expectedPacked = ReferenceImplementations.MakeBgra32(r, g, b, a).PackedValue;

                Assert.Equal(expectedPacked, actualPacked);
            }
        }

        public class FromArgb32 : PixelConverterTests
        {
            [Theory]
            [MemberData(nameof(RgbaData))]
            public void ToRgba32(byte r, byte g, byte b, byte a)
            {
                Argb32 s = ReferenceImplementations.MakeArgb32(r, g, b, a);

                // Act:
                uint actualPacked = PixelConverter.FromArgb32.ToRgba32(s.PackedValue);

                // Assert:
                uint expectedPacked = ReferenceImplementations.MakeRgba32(r, g, b, a).PackedValue;

                Assert.Equal(expectedPacked, actualPacked);
            }

            [Theory]
            [MemberData(nameof(RgbaData))]
            public void ToBgra32(byte r, byte g, byte b, byte a)
            {
                Argb32 s = ReferenceImplementations.MakeArgb32(r, g, b, a);

                // Act:
                uint actualPacked = PixelConverter.FromArgb32.ToBgra32(s.PackedValue);

                // Assert:
                uint expectedPacked = ReferenceImplementations.MakeBgra32(r, g, b, a).PackedValue;

                Assert.Equal(expectedPacked, actualPacked);
            }
        }

        public class FromBgra32 : PixelConverterTests
        {
            [Theory]
            [MemberData(nameof(RgbaData))]
            public void ToArgb32(byte r, byte g, byte b, byte a)
            {
                Bgra32 s = ReferenceImplementations.MakeBgra32(r, g, b, a);

                // Act:
                uint actualPacked = PixelConverter.FromBgra32.ToArgb32(s.PackedValue);

                // Assert:
                uint expectedPacked = ReferenceImplementations.MakeArgb32(r, g, b, a).PackedValue;

                Assert.Equal(expectedPacked, actualPacked);
            }

            [Theory]
            [MemberData(nameof(RgbaData))]
            public void ToRgba32(byte r, byte g, byte b, byte a)
            {
                Bgra32 s = ReferenceImplementations.MakeBgra32(r, g, b, a);

                // Act:
                uint actualPacked = PixelConverter.FromBgra32.ToRgba32(s.PackedValue);

                // Assert:
                uint expectedPacked = ReferenceImplementations.MakeRgba32(r, g, b, a).PackedValue;

                Assert.Equal(expectedPacked, actualPacked);
            }
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
        }
    }
}