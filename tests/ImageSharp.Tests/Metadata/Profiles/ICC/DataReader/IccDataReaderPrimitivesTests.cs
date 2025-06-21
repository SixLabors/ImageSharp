// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.ICC.DataReader;

[Trait("Profile", "Icc")]
public class IccDataReaderPrimitivesTests
{
    [Theory]
    [MemberData(nameof(IccTestDataPrimitives.AsciiTestData), MemberType = typeof(IccTestDataPrimitives))]
    public void ReadAsciiString(byte[] textBytes, int length, string expected)
    {
        IccDataReader reader = CreateReader(textBytes);

        string output = reader.ReadAsciiString(length);

        Assert.Equal(expected, output);
    }

    [Fact]
    public void ReadAsciiStringWithNegativeLengthThrowsArgumentException()
    {
        IccDataReader reader = CreateReader(new byte[4]);

        Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadAsciiString(-1));
    }

    [Fact]
    public void ReadUnicodeStringWithNegativeLengthThrowsArgumentException()
    {
        IccDataReader reader = CreateReader(new byte[4]);

        Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadUnicodeString(-1));
    }

    [Theory]
    [MemberData(nameof(IccTestDataPrimitives.Fix16TestData), MemberType = typeof(IccTestDataPrimitives))]
    public void ReadFix16(byte[] data, float expected)
    {
        IccDataReader reader = CreateReader(data);

        float output = reader.ReadFix16();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataPrimitives.UFix16TestData), MemberType = typeof(IccTestDataPrimitives))]
    public void ReadUFix16(byte[] data, float expected)
    {
        IccDataReader reader = CreateReader(data);

        float output = reader.ReadUFix16();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataPrimitives.U1Fix15TestData), MemberType = typeof(IccTestDataPrimitives))]
    public void ReadU1Fix15(byte[] data, float expected)
    {
        IccDataReader reader = CreateReader(data);

        float output = reader.ReadU1Fix15();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataPrimitives.UFix8TestData), MemberType = typeof(IccTestDataPrimitives))]
    public void ReadUFix8(byte[] data, float expected)
    {
        IccDataReader reader = CreateReader(data);

        float output = reader.ReadUFix8();

        Assert.Equal(expected, output);
    }

    private static IccDataReader CreateReader(byte[] data)
    {
        return new(data);
    }
}
