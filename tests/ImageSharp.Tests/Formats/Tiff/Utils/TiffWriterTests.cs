// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests
{
    using System.IO;
    using Xunit;

    using ImageSharp.Formats.Tiff;

    [Trait("Category", "Tiff")]
    public class TiffWriterTests
    {
        [Fact]
        public void IsLittleEndian_IsTrueOnWindows()
        {
            MemoryStream stream = new MemoryStream();

            using (TiffWriter writer = new TiffWriter(stream))
            {
                Assert.True(writer.IsLittleEndian);
            }
        }

        [Theory]
        [InlineData(new byte[] {}, 0)]
        [InlineData(new byte[] { 42 }, 1)]
        [InlineData(new byte[] { 1, 2, 3, 4, 5 }, 5)]
        public void Position_EqualsTheStreamPosition(byte[] data, long expectedResult)
        {
            MemoryStream stream = new MemoryStream();

            using (TiffWriter writer = new TiffWriter(stream))
            {
                writer.Write(data);
                Assert.Equal(writer.Position, expectedResult);
            }
        }

        [Fact]
        public void Write_WritesByte()
        {
            MemoryStream stream = new MemoryStream();

            using (TiffWriter writer = new TiffWriter(stream))
            {
                writer.Write((byte)42);
            }

            Assert.Equal(new byte[] { 42 }, stream.ToArray());
        }

        [Fact]
        public void Write_WritesByteArray()
        {
            MemoryStream stream = new MemoryStream();

            using (TiffWriter writer = new TiffWriter(stream))
            {
                writer.Write(new byte[] { 2, 4, 6, 8 });
            }

            Assert.Equal(new byte[] { 2, 4, 6, 8 }, stream.ToArray());
        }

        [Fact]
        public void Write_WritesUInt16()
        {
            MemoryStream stream = new MemoryStream();

            using (TiffWriter writer = new TiffWriter(stream))
            {
                writer.Write((ushort)1234);
            }

            Assert.Equal(new byte[] { 0xD2, 0x04 }, stream.ToArray());
        }

        [Fact]
        public void Write_WritesUInt32()
        {
            MemoryStream stream = new MemoryStream();

            using (TiffWriter writer = new TiffWriter(stream))
            {
                writer.Write((uint)12345678);
            }

            Assert.Equal(new byte[] { 0x4E, 0x61, 0xBC, 0x00 }, stream.ToArray());
        }

        [Theory]
        [InlineData(new byte[] { }, new byte[] { 0, 0, 0, 0 })]
        [InlineData(new byte[] { 2 }, new byte[] { 2, 0, 0, 0 })]
        [InlineData(new byte[] { 2, 4 }, new byte[] { 2, 4, 0, 0 })]
        [InlineData(new byte[] { 2, 4, 6 }, new byte[] { 2, 4, 6, 0 })]
        [InlineData(new byte[] { 2, 4, 6, 8 }, new byte[] { 2, 4, 6, 8 })]
        [InlineData(new byte[] { 2, 4, 6, 8, 10, 12 }, new byte[] { 2, 4, 6, 8, 10, 12 })]
        public void WritePadded_WritesByteArray(byte[] bytes, byte[] expectedResult)
        {
            MemoryStream stream = new MemoryStream();

            using (TiffWriter writer = new TiffWriter(stream))
            {
                writer.WritePadded(bytes);
            }

            Assert.Equal(expectedResult, stream.ToArray());
        }

        [Fact]
        public void WriteMarker_WritesToPlacedPosition()
        {
            MemoryStream stream = new MemoryStream();

            using (TiffWriter writer = new TiffWriter(stream))
            {
                writer.Write((uint)0x11111111);
                long marker = writer.PlaceMarker();
                writer.Write((uint)0x33333333);

                writer.WriteMarker(marker, 0x12345678);

                writer.Write((uint)0x44444444);
            }

            Assert.Equal(new byte[] { 0x11, 0x11, 0x11, 0x11,
                                      0x78, 0x56, 0x34, 0x12,
                                      0x33, 0x33, 0x33, 0x33,
                                      0x44, 0x44, 0x44, 0x44 }, stream.ToArray());
        }
    }
}
