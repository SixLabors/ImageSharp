// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.ICC.DataWriter;

[Trait("Profile", "Icc")]
public class IccDataWriterCurvesTests
{
    [Theory]
    [MemberData(nameof(IccTestDataCurves.OneDimensionalCurveTestData), MemberType = typeof(IccTestDataCurves))]
    internal void WriteOneDimensionalCurve(byte[] expected, IccOneDimensionalCurve data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteOneDimensionalCurve(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataCurves.ResponseCurveTestData), MemberType = typeof(IccTestDataCurves))]
    internal void WriteResponseCurve(byte[] expected, IccResponseCurve data, int channelCount)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteResponseCurve(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataCurves.ParametricCurveTestData), MemberType = typeof(IccTestDataCurves))]
    internal void WriteParametricCurve(byte[] expected, IccParametricCurve data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteParametricCurve(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataCurves.CurveSegmentTestData), MemberType = typeof(IccTestDataCurves))]
    internal void WriteCurveSegment(byte[] expected, IccCurveSegment data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteCurveSegment(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataCurves.FormulaCurveSegmentTestData), MemberType = typeof(IccTestDataCurves))]
    internal void WriteFormulaCurveElement(byte[] expected, IccFormulaCurveElement data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteFormulaCurveElement(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataCurves.SampledCurveSegmentTestData), MemberType = typeof(IccTestDataCurves))]
    internal void WriteSampledCurveElement(byte[] expected, IccSampledCurveElement data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteSampledCurveElement(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    private static IccDataWriter CreateWriter() => new IccDataWriter();
}
