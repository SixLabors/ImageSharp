// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.ICC.DataReader;

[Trait("Profile", "Icc")]
public class IccDataReaderMultiProcessElementTests
{
    [Theory]
    [MemberData(nameof(IccTestDataMultiProcessElements.MultiProcessElementTestData), MemberType = typeof(IccTestDataMultiProcessElements))]
    internal void ReadMultiProcessElement(byte[] data, IccMultiProcessElement expected)
    {
        IccDataReader reader = CreateReader(data);

        IccMultiProcessElement output = reader.ReadMultiProcessElement();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataMultiProcessElements.CurveSetTestData), MemberType = typeof(IccTestDataMultiProcessElements))]
    internal void ReadCurveSetProcessElement(byte[] data, IccCurveSetProcessElement expected, int inChannelCount, int outChannelCount)
    {
        IccDataReader reader = CreateReader(data);

        IccCurveSetProcessElement output = reader.ReadCurveSetProcessElement(inChannelCount, outChannelCount);

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataMultiProcessElements.MatrixTestData), MemberType = typeof(IccTestDataMultiProcessElements))]
    internal void ReadMatrixProcessElement(byte[] data, IccMatrixProcessElement expected, int inChannelCount, int outChannelCount)
    {
        IccDataReader reader = CreateReader(data);

        IccMatrixProcessElement output = reader.ReadMatrixProcessElement(inChannelCount, outChannelCount);

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataMultiProcessElements.ClutTestData), MemberType = typeof(IccTestDataMultiProcessElements))]
    internal void ReadClutProcessElement(byte[] data, IccClutProcessElement expected, int inChannelCount, int outChannelCount)
    {
        IccDataReader reader = CreateReader(data);

        IccClutProcessElement output = reader.ReadClutProcessElement(inChannelCount, outChannelCount);

        Assert.Equal(expected, output);
    }

    private static IccDataReader CreateReader(byte[] data)
    {
        return new(data);
    }
}
