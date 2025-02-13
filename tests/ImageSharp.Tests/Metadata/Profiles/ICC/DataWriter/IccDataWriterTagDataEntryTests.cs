// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.ICC.DataWriter;

[Trait("Profile", "Icc")]
public class IccDataWriterTagDataEntryTests
{
    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.UnknownTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteUnknownTagDataEntry(byte[] expected, IccUnknownTagDataEntry data, uint size)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteUnknownTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.ChromaticityTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteChromaticityTagDataEntry(byte[] expected, IccChromaticityTagDataEntry data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteChromaticityTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.ColorantOrderTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteColorantOrderTagDataEntry(byte[] expected, IccColorantOrderTagDataEntry data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteColorantOrderTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.ColorantTableTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteColorantTableTagDataEntry(byte[] expected, IccColorantTableTagDataEntry data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteColorantTableTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.CurveTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteCurveTagDataEntry(byte[] expected, IccCurveTagDataEntry data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteCurveTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.DataTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteDataTagDataEntry(byte[] expected, IccDataTagDataEntry data, uint size)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteDataTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.DateTimeTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteDateTimeTagDataEntry(byte[] expected, IccDateTimeTagDataEntry data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteDateTimeTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.Lut16TagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteLut16TagDataEntry(byte[] expected, IccLut16TagDataEntry data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteLut16TagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.Lut8TagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteLut8TagDataEntry(byte[] expected, IccLut8TagDataEntry data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteLut8TagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.LutAToBTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteLutAToBTagDataEntry(byte[] expected, IccLutAToBTagDataEntry data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteLutAtoBTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.LutBToATagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteLutBToATagDataEntry(byte[] expected, IccLutBToATagDataEntry data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteLutBtoATagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.MeasurementTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteMeasurementTagDataEntry(byte[] expected, IccMeasurementTagDataEntry data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteMeasurementTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.MultiLocalizedUnicodeTagDataEntryTestDataWrite), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteMultiLocalizedUnicodeTagDataEntry(byte[] expected, IccMultiLocalizedUnicodeTagDataEntry data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteMultiLocalizedUnicodeTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.MultiProcessElementsTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteMultiProcessElementsTagDataEntry(byte[] expected, IccMultiProcessElementsTagDataEntry data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteMultiProcessElementsTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.NamedColor2TagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteNamedColor2TagDataEntry(byte[] expected, IccNamedColor2TagDataEntry data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteNamedColor2TagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.ParametricCurveTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteParametricCurveTagDataEntry(byte[] expected, IccParametricCurveTagDataEntry data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteParametricCurveTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.ProfileSequenceDescTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteProfileSequenceDescTagDataEntry(byte[] expected, IccProfileSequenceDescTagDataEntry data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteProfileSequenceDescTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.ProfileSequenceIdentifierTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteProfileSequenceIdentifierTagDataEntry(byte[] expected, IccProfileSequenceIdentifierTagDataEntry data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteProfileSequenceIdentifierTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.ResponseCurveSet16TagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteResponseCurveSet16TagDataEntry(byte[] expected, IccResponseCurveSet16TagDataEntry data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteResponseCurveSet16TagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.Fix16ArrayTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteFix16ArrayTagDataEntry(byte[] expected, IccFix16ArrayTagDataEntry data, uint size)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteFix16ArrayTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.SignatureTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteSignatureTagDataEntry(byte[] expected, IccSignatureTagDataEntry data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteSignatureTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.TextTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteTextTagDataEntry(byte[] expected, IccTextTagDataEntry data, uint size)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteTextTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.UFix16ArrayTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteUFix16ArrayTagDataEntry(byte[] expected, IccUFix16ArrayTagDataEntry data, uint size)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteUFix16ArrayTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.UInt16ArrayTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteUInt16ArrayTagDataEntry(byte[] expected, IccUInt16ArrayTagDataEntry data, uint size)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteUInt16ArrayTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.UInt32ArrayTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteUInt32ArrayTagDataEntry(byte[] expected, IccUInt32ArrayTagDataEntry data, uint size)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteUInt32ArrayTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.UInt64ArrayTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteUInt64ArrayTagDataEntry(byte[] expected, IccUInt64ArrayTagDataEntry data, uint size)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteUInt64ArrayTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.UInt8ArrayTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteUInt8ArrayTagDataEntry(byte[] expected, IccUInt8ArrayTagDataEntry data, uint size)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteUInt8ArrayTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.ViewingConditionsTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteViewingConditionsTagDataEntry(byte[] expected, IccViewingConditionsTagDataEntry data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteViewingConditionsTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.XYZTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteXyzTagDataEntry(byte[] expected, IccXyzTagDataEntry data, uint size)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteXyzTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.TextDescriptionTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteTextDescriptionTagDataEntry(byte[] expected, IccTextDescriptionTagDataEntry data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteTextDescriptionTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.CrdInfoTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteCrdInfoTagDataEntry(byte[] expected, IccCrdInfoTagDataEntry data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteCrdInfoTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.ScreeningTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteScreeningTagDataEntry(byte[] expected, IccScreeningTagDataEntry data)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteScreeningTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataTagDataEntry.UcrBgTagDataEntryTestData), MemberType = typeof(IccTestDataTagDataEntry))]
    internal void WriteUcrBgTagDataEntry(byte[] expected, IccUcrBgTagDataEntry data, uint size)
    {
        using IccDataWriter writer = CreateWriter();

        writer.WriteUcrBgTagDataEntry(data);
        byte[] output = writer.GetData();

        Assert.Equal(expected, output);
    }

    private static IccDataWriter CreateWriter() => new();
}
