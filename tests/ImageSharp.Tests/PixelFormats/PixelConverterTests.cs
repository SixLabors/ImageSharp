// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats.Utils;

using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    [Trait("Category", "PixelFormats")]
    public abstract partial class PixelConverterTests
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
                byte[] source = ReferenceImplementations.MakeRgba32ByteArray(r, g, b, a);
                var actual = new byte[source.Length];

                PixelConverter.FromRgba32.ToArgb32(source, actual);

                byte[] expected = ReferenceImplementations.MakeArgb32ByteArray(r, g, b, a);

                Assert.Equal(expected, actual);
            }

            [Theory]
            [MemberData(nameof(RgbaData))]
            public void ToBgra32(byte r, byte g, byte b, byte a)
            {
                byte[] source = ReferenceImplementations.MakeRgba32ByteArray(r, g, b, a);
                var actual = new byte[source.Length];

                PixelConverter.FromRgba32.ToBgra32(source, actual);

                byte[] expected = ReferenceImplementations.MakeBgra32ByteArray(r, g, b, a);

                Assert.Equal(expected, actual);
            }
        }

        public class FromArgb32 : PixelConverterTests
        {
            [Theory]
            [MemberData(nameof(RgbaData))]
            public void ToRgba32(byte r, byte g, byte b, byte a)
            {
                byte[] source = ReferenceImplementations.MakeArgb32ByteArray(r, g, b, a);
                var actual = new byte[source.Length];

                PixelConverter.FromArgb32.ToRgba32(source, actual);

                byte[] expected = ReferenceImplementations.MakeRgba32ByteArray(r, g, b, a);

                Assert.Equal(expected, actual);
            }

            [Theory]
            [MemberData(nameof(RgbaData))]
            public void ToBgra32(byte r, byte g, byte b, byte a)
            {
                byte[] source = ReferenceImplementations.MakeArgb32ByteArray(r, g, b, a);
                var actual = new byte[source.Length];

                PixelConverter.FromArgb32.ToBgra32(source, actual);

                byte[] expected = ReferenceImplementations.MakeBgra32ByteArray(r, g, b, a);

                Assert.Equal(expected, actual);
            }
        }

        public class FromBgra32 : PixelConverterTests
        {
            [Theory]
            [MemberData(nameof(RgbaData))]
            public void ToArgb32(byte r, byte g, byte b, byte a)
            {
                byte[] source = ReferenceImplementations.MakeBgra32ByteArray(r, g, b, a);
                var actual = new byte[source.Length];

                PixelConverter.FromBgra32.ToArgb32(source, actual);

                byte[] expected = ReferenceImplementations.MakeArgb32ByteArray(r, g, b, a);

                Assert.Equal(expected, actual);
            }

            [Theory]
            [MemberData(nameof(RgbaData))]
            public void ToRgba32(byte r, byte g, byte b, byte a)
            {
                byte[] source = ReferenceImplementations.MakeBgra32ByteArray(r, g, b, a);
                var actual = new byte[source.Length];

                PixelConverter.FromBgra32.ToRgba32(source, actual);

                byte[] expected = ReferenceImplementations.MakeRgba32ByteArray(r, g, b, a);

                Assert.Equal(expected, actual);
            }
        }
    }
}
