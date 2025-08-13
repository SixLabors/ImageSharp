// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.ICC.DataWriter;

[Trait("Profile", "Icc")]
public class IccDataWriterNonPrimitivesTests
{
    [Theory]
    [MemberData(nameof(IccTestDataNonPrimitives.DateTimeTestData), MemberType = typeof(IccTestDataNonPrimitives))]
    public void WriteDateTime(byte[] expected, DateTime data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteDateTime(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataNonPrimitives.VersionNumberTestData), MemberType = typeof(IccTestDataNonPrimitives))]
    public void WriteVersionNumber(byte[] expected, IccVersion data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteVersionNumber(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataNonPrimitives.XyzNumberTestData), MemberType = typeof(IccTestDataNonPrimitives))]
    public void WriteXyzNumber(byte[] expected, Vector3 data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteXyzNumber(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataNonPrimitives.ProfileIdTestData), MemberType = typeof(IccTestDataNonPrimitives))]
    internal void WriteProfileId(byte[] expected, IccProfileId data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteProfileId(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataNonPrimitives.PositionNumberTestData), MemberType = typeof(IccTestDataNonPrimitives))]
    internal void WritePositionNumber(byte[] expected, IccPositionNumber data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WritePositionNumber(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataNonPrimitives.ResponseNumberTestData), MemberType = typeof(IccTestDataNonPrimitives))]
    internal void WriteResponseNumber(byte[] expected, IccResponseNumber data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteResponseNumber(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataNonPrimitives.NamedColorTestData), MemberType = typeof(IccTestDataNonPrimitives))]
    internal void WriteNamedColor(byte[] expected, IccNamedColor data, uint coordinateCount)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteNamedColor(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataNonPrimitives.ProfileDescriptionWriteTestData), MemberType = typeof(IccTestDataNonPrimitives))]
    internal void WriteProfileDescription(byte[] expected, IccProfileDescription data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteProfileDescription(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataNonPrimitives.ScreeningChannelTestData), MemberType = typeof(IccTestDataNonPrimitives))]
    internal void WriteScreeningChannel(byte[] expected, IccScreeningChannel data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteScreeningChannel(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    private static IccDataWriter CreateWriter() => new();
}
