// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.ICC.DataWriter;

[Trait("Profile", "Icc")]
public class IccDataWriterLutTests
{
    [Theory]
    [MemberData(nameof(IccTestDataLut.ClutTestData), MemberType = typeof(IccTestDataLut))]
    internal void WriteClutAll(byte[] expected, IccClut data, int inChannelCount, int outChannelCount, bool isFloat)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteClut(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataLut.Clut8TestData), MemberType = typeof(IccTestDataLut))]
    internal void WriteClut8(byte[] expected, IccClut data, int inChannelCount, int outChannelCount, byte[] gridPointCount)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteClut8(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataLut.Clut16TestData), MemberType = typeof(IccTestDataLut))]
    internal void WriteClut16(byte[] expected, IccClut data, int inChannelCount, int outChannelCount, byte[] gridPointCount)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteClut16(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataLut.ClutF32TestData), MemberType = typeof(IccTestDataLut))]
    internal void WriteClutF32(byte[] expected, IccClut data, int inChannelCount, int outChannelCount, byte[] gridPointCount)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteClutF32(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataLut.Lut8TestData), MemberType = typeof(IccTestDataLut))]
    internal void WriteLut8(byte[] expected, IccLut data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteLut8(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataLut.Lut16TestData), MemberType = typeof(IccTestDataLut))]
    internal void WriteLut16(byte[] expected, IccLut data, int count)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteLut16(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    private static IccDataWriter CreateWriter()
    {
        return new();
    }
}
