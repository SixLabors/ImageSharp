// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.ICC.DataWriter;

[Trait("Profile", "Icc")]
public class IccDataWriterTests
{
    [Fact]
    public void WriteEmpty()
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteEmpty(4);
        byte[] output = writer.GetData();

        Assert.Equal(new byte[4], output);
    }

    [Theory]
    [InlineData(1, 4)]
    [InlineData(4, 4)]
    public void WritePadding(int writePosition, int expectedLength)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteEmpty(writePosition);
        writer.WritePadding();
        byte[] output = writer.GetData();

        Assert.Equal(new byte[expectedLength], output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataArray.UInt8TestData), MemberType = typeof(IccTestDataArray))]
    public void WriteArrayUInt8(byte[] data, byte[] expected)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteArray(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataArray.UInt16TestData), MemberType = typeof(IccTestDataArray))]
    public void WriteArrayUInt16(byte[] expected, ushort[] data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteArray(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataArray.Int16TestData), MemberType = typeof(IccTestDataArray))]
    public void WriteArrayInt16(byte[] expected, short[] data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteArray(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataArray.UInt32TestData), MemberType = typeof(IccTestDataArray))]
    public void WriteArrayUInt32(byte[] expected, uint[] data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteArray(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataArray.Int32TestData), MemberType = typeof(IccTestDataArray))]
    public void WriteArrayInt32(byte[] expected, int[] data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteArray(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataArray.UInt64TestData), MemberType = typeof(IccTestDataArray))]
    public void WriteArrayUInt64(byte[] expected, ulong[] data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteArray(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    private static IccDataWriter CreateWriter() => new();
}
