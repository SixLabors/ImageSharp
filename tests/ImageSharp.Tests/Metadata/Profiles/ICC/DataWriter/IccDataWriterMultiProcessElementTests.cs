// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.ICC.DataWriter;

[Trait("Profile", "Icc")]
public class IccDataWriterMultiProcessElementTests
{
    [Theory]
    [MemberData(nameof(IccTestDataMultiProcessElements.MultiProcessElementTestData), MemberType = typeof(IccTestDataMultiProcessElements))]
    internal void WriteMultiProcessElement(byte[] expected, IccMultiProcessElement data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteMultiProcessElement(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataMultiProcessElements.CurveSetTestData), MemberType = typeof(IccTestDataMultiProcessElements))]
    internal void WriteCurveSetProcessElement(byte[] expected, IccCurveSetProcessElement data, int inChannelCount, int outChannelCount)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteCurveSetProcessElement(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataMultiProcessElements.MatrixTestData), MemberType = typeof(IccTestDataMultiProcessElements))]
    internal void WriteMatrixProcessElement(byte[] expected, IccMatrixProcessElement data, int inChannelCount, int outChannelCount)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteMatrixProcessElement(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataMultiProcessElements.ClutTestData), MemberType = typeof(IccTestDataMultiProcessElements))]
    internal void WriteClutProcessElement(byte[] expected, IccClutProcessElement data, int inChannelCount, int outChannelCount)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteClutProcessElement(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    private static IccDataWriter CreateWriter()
    {
        return new();
    }
}
