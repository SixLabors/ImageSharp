// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.ICC.DataWriter;

[Trait("Profile", "Icc")]
public class IccDataWriterPrimitivesTests
{
    [Theory]
    [MemberData(nameof(IccTestDataPrimitives.AsciiWriteTestData), MemberType = typeof(IccTestDataPrimitives))]
    public void WriteAsciiString(byte[] expected, string data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteAsciiString(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataPrimitives.AsciiPaddingTestData), MemberType = typeof(IccTestDataPrimitives))]
    public void WriteAsciiStringPadded(byte[] expected, int length, string data, bool ensureNullTerminator)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteAsciiString(data, length, ensureNullTerminator);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Fact]
    public void WriteAsciiStringWithNullWritesEmpty()
    {
        using IccDataWriter writer = CreateWriter();

        int count = writer.WriteAsciiString(null);
        byte[] output = writer.GetData();

        Assert.Equal(0, count);
        Assert.Equal(Array.Empty<byte>(), output);
    }

    [Fact]
    public void WriteAsciiStringWithNegativeLengthThrowsArgumentException()
    {
        using IccDataWriter writer = CreateWriter();

        Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteAsciiString("abcd", -1, false));
    }

    [Fact]
    public void WriteUnicodeStringWithNullWritesEmpty()
    {
        using IccDataWriter writer = CreateWriter();

        int count = writer.WriteUnicodeString(null);
        byte[] output = writer.GetData();

        Assert.Equal(0, count);
        Assert.Equal(Array.Empty<byte>(), output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataPrimitives.Fix16TestData), MemberType = typeof(IccTestDataPrimitives))]
    public void WriteFix16(byte[] expected, float data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteFix16(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataPrimitives.UFix16TestData), MemberType = typeof(IccTestDataPrimitives))]
    public void WriteUFix16(byte[] expected, float data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteUFix16(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataPrimitives.U1Fix15TestData), MemberType = typeof(IccTestDataPrimitives))]
    public void WriteU1Fix15(byte[] expected, float data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteU1Fix15(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataPrimitives.UFix8TestData), MemberType = typeof(IccTestDataPrimitives))]
    public void WriteUFix8(byte[] expected, float data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteUFix8(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    private static IccDataWriter CreateWriter() => new();
}
