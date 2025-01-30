// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.ICC.DataReader;

[Trait("Profile", "Icc")]
public class IccDataReaderCurvesTests
{
    [Theory]
    [MemberData(nameof(IccTestDataCurves.OneDimensionalCurveTestData), MemberType = typeof(IccTestDataCurves))]
    internal void ReadOneDimensionalCurve(byte[] data, IccOneDimensionalCurve expected)
    {
        IccDataReader reader = CreateReader(data);

        IccOneDimensionalCurve output = reader.ReadOneDimensionalCurve();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataCurves.ResponseCurveTestData), MemberType = typeof(IccTestDataCurves))]
    internal void ReadResponseCurve(byte[] data, IccResponseCurve expected, int channelCount)
    {
        IccDataReader reader = CreateReader(data);

        IccResponseCurve output = reader.ReadResponseCurve(channelCount);

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataCurves.ParametricCurveTestData), MemberType = typeof(IccTestDataCurves))]
    internal void ReadParametricCurve(byte[] data, IccParametricCurve expected)
    {
        IccDataReader reader = CreateReader(data);

        IccParametricCurve output = reader.ReadParametricCurve();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataCurves.CurveSegmentTestData), MemberType = typeof(IccTestDataCurves))]
    internal void ReadCurveSegment(byte[] data, IccCurveSegment expected)
    {
        IccDataReader reader = CreateReader(data);

        IccCurveSegment output = reader.ReadCurveSegment();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataCurves.FormulaCurveSegmentTestData), MemberType = typeof(IccTestDataCurves))]
    internal void ReadFormulaCurveElement(byte[] data, IccFormulaCurveElement expected)
    {
        IccDataReader reader = CreateReader(data);

        IccFormulaCurveElement output = reader.ReadFormulaCurveElement();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataCurves.SampledCurveSegmentTestData), MemberType = typeof(IccTestDataCurves))]
    internal void ReadSampledCurveElement(byte[] data, IccSampledCurveElement expected)
    {
        IccDataReader reader = CreateReader(data);

        IccSampledCurveElement output = reader.ReadSampledCurveElement();

        Assert.Equal(expected, output);
    }

    private static IccDataReader CreateReader(byte[] data)
    {
        return new(data);
    }
}
