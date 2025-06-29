// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff.Writers;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff.Utils;

[Trait("Format", "Tiff")]
public class TiffWriterTests
{
    [Fact]
    public void IsLittleEndian_IsTrueOnWindows()
    {
        using MemoryStream stream = new();
        using TiffStreamWriter writer = new(stream);
        Assert.True(TiffStreamWriter.IsLittleEndian);
    }

    [Theory]
    [InlineData(new byte[] { }, 0)]
    [InlineData(new byte[] { 42 }, 1)]
    [InlineData(new byte[] { 1, 2, 3, 4, 5 }, 5)]
    public void Position_EqualsTheStreamPosition(byte[] data, long expectedResult)
    {
        using MemoryStream stream = new();
        using TiffStreamWriter writer = new(stream);
        writer.Write(data);
        Assert.Equal(writer.Position, expectedResult);
    }

    [Fact]
    public void Write_WritesByte()
    {
        using MemoryStream stream = new();
        using TiffStreamWriter writer = new(stream);
        writer.Write(42);

        Assert.Equal(new byte[] { 42 }, stream.ToArray());
    }

    [Fact]
    public void Write_WritesByteArray()
    {
        using MemoryStream stream = new();
        using TiffStreamWriter writer = new(stream);
        writer.Write(new byte[] { 2, 4, 6, 8 });

        Assert.Equal(new byte[] { 2, 4, 6, 8 }, stream.ToArray());
    }

    [Fact]
    public void Write_WritesUInt16()
    {
        using MemoryStream stream = new();
        using TiffStreamWriter writer = new(stream);
        writer.Write(1234, stackalloc byte[2]);

        Assert.Equal(new byte[] { 0xD2, 0x04 }, stream.ToArray());
    }

    [Fact]
    public void Write_WritesUInt32()
    {
        using MemoryStream stream = new();
        using TiffStreamWriter writer = new(stream);
        writer.Write(12345678U, stackalloc byte[4]);

        Assert.Equal(new byte[] { 0x4E, 0x61, 0xBC, 0x00 }, stream.ToArray());
    }

    [Theory]
    [InlineData(new byte[] { }, new byte[] { })]
    [InlineData(new byte[] { 2 }, new byte[] { 2, 0, 0, 0 })]
    [InlineData(new byte[] { 2, 4 }, new byte[] { 2, 4, 0, 0 })]
    [InlineData(new byte[] { 2, 4, 6 }, new byte[] { 2, 4, 6, 0 })]
    [InlineData(new byte[] { 2, 4, 6, 8 }, new byte[] { 2, 4, 6, 8 })]
    [InlineData(new byte[] { 2, 4, 6, 8, 10, 12 }, new byte[] { 2, 4, 6, 8, 10, 12, 0, 0 })]

    public void WritePadded_WritesByteArray(byte[] bytes, byte[] expectedResult)
    {
        using MemoryStream stream = new();
        using TiffStreamWriter writer = new(stream);
        writer.WritePadded(bytes);

        Assert.Equal(expectedResult, stream.ToArray());
    }

    [Fact]
    public void WriteMarker_WritesToPlacedPosition()
    {
        using MemoryStream stream = new();
        Span<byte> buffer = stackalloc byte[4];

        using (TiffStreamWriter writer = new(stream))
        {
            writer.Write(0x11111111, buffer);
            long marker = writer.PlaceMarker(buffer);
            writer.Write(0x33333333, buffer);

            writer.WriteMarker(marker, 0x12345678, buffer);

            writer.Write(0x44444444, buffer);
        }

        Assert.Equal(
            new byte[]
            {
                0x11, 0x11, 0x11, 0x11,
                0x78, 0x56, 0x34, 0x12,
                0x33, 0x33, 0x33, 0x33,
                0x44, 0x44, 0x44, 0x44
            },
            stream.ToArray());
    }
}
