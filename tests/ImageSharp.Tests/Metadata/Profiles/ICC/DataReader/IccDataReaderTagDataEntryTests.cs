// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Icc
{
    public class IccDataReaderTagDataEntryTests
    {
        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.UnknownTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadUnknownTagDataEntry(byte[] data, IccUnknownTagDataEntry expected, uint size)
        {
            IccDataReader reader = this.CreateReader(data);

            IccUnknownTagDataEntry output = reader.ReadUnknownTagDataEntry(size);

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.ChromaticityTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadChromaticityTagDataEntry(byte[] data, IccChromaticityTagDataEntry expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccChromaticityTagDataEntry output = reader.ReadChromaticityTagDataEntry();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.ColorantOrderTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadColorantOrderTagDataEntry(byte[] data, IccColorantOrderTagDataEntry expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccColorantOrderTagDataEntry output = reader.ReadColorantOrderTagDataEntry();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.ColorantTableTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadColorantTableTagDataEntry(byte[] data, IccColorantTableTagDataEntry expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccColorantTableTagDataEntry output = reader.ReadColorantTableTagDataEntry();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.CurveTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadCurveTagDataEntry(byte[] data, IccCurveTagDataEntry expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccCurveTagDataEntry output = reader.ReadCurveTagDataEntry();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.DataTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadDataTagDataEntry(byte[] data, IccDataTagDataEntry expected, uint size)
        {
            IccDataReader reader = this.CreateReader(data);

            IccDataTagDataEntry output = reader.ReadDataTagDataEntry(size);

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.DateTimeTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadDateTimeTagDataEntry(byte[] data, IccDateTimeTagDataEntry expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccDateTimeTagDataEntry output = reader.ReadDateTimeTagDataEntry();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.Lut16TagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadLut16TagDataEntry(byte[] data, IccLut16TagDataEntry expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccLut16TagDataEntry output = reader.ReadLut16TagDataEntry();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.Lut8TagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadLut8TagDataEntry(byte[] data, IccLut8TagDataEntry expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccLut8TagDataEntry output = reader.ReadLut8TagDataEntry();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.LutAToBTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadLutAToBTagDataEntry(byte[] data, IccLutAToBTagDataEntry expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccLutAToBTagDataEntry output = reader.ReadLutAtoBTagDataEntry();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.LutBToATagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadLutBToATagDataEntry(byte[] data, IccLutBToATagDataEntry expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccLutBToATagDataEntry output = reader.ReadLutBtoATagDataEntry();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.MeasurementTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadMeasurementTagDataEntry(byte[] data, IccMeasurementTagDataEntry expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccMeasurementTagDataEntry output = reader.ReadMeasurementTagDataEntry();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.MultiLocalizedUnicodeTagDataEntryTestData_Read),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadMultiLocalizedUnicodeTagDataEntry(byte[] data, IccMultiLocalizedUnicodeTagDataEntry expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccMultiLocalizedUnicodeTagDataEntry output = reader.ReadMultiLocalizedUnicodeTagDataEntry();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.MultiProcessElementsTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadMultiProcessElementsTagDataEntry(byte[] data, IccMultiProcessElementsTagDataEntry expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccMultiProcessElementsTagDataEntry output = reader.ReadMultiProcessElementsTagDataEntry();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.NamedColor2TagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadNamedColor2TagDataEntry(byte[] data, IccNamedColor2TagDataEntry expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccNamedColor2TagDataEntry output = reader.ReadNamedColor2TagDataEntry();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.ParametricCurveTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadParametricCurveTagDataEntry(byte[] data, IccParametricCurveTagDataEntry expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccParametricCurveTagDataEntry output = reader.ReadParametricCurveTagDataEntry();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.ProfileSequenceDescTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadProfileSequenceDescTagDataEntry(byte[] data, IccProfileSequenceDescTagDataEntry expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccProfileSequenceDescTagDataEntry output = reader.ReadProfileSequenceDescTagDataEntry();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.ProfileSequenceIdentifierTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadProfileSequenceIdentifierTagDataEntry(
            byte[] data,
            IccProfileSequenceIdentifierTagDataEntry expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccProfileSequenceIdentifierTagDataEntry output = reader.ReadProfileSequenceIdentifierTagDataEntry();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.ResponseCurveSet16TagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadResponseCurveSet16TagDataEntry(byte[] data, IccResponseCurveSet16TagDataEntry expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccResponseCurveSet16TagDataEntry output = reader.ReadResponseCurveSet16TagDataEntry();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.Fix16ArrayTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadFix16ArrayTagDataEntry(byte[] data, IccFix16ArrayTagDataEntry expected, uint size)
        {
            IccDataReader reader = this.CreateReader(data);

            IccFix16ArrayTagDataEntry output = reader.ReadFix16ArrayTagDataEntry(size);

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.SignatureTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadSignatureTagDataEntry(byte[] data, IccSignatureTagDataEntry expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccSignatureTagDataEntry output = reader.ReadSignatureTagDataEntry();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.TextTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadTextTagDataEntry(byte[] data, IccTextTagDataEntry expected, uint size)
        {
            IccDataReader reader = this.CreateReader(data);

            IccTextTagDataEntry output = reader.ReadTextTagDataEntry(size);

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.UFix16ArrayTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadUFix16ArrayTagDataEntry(byte[] data, IccUFix16ArrayTagDataEntry expected, uint size)
        {
            IccDataReader reader = this.CreateReader(data);

            IccUFix16ArrayTagDataEntry output = reader.ReadUFix16ArrayTagDataEntry(size);

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.UInt16ArrayTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadUInt16ArrayTagDataEntry(byte[] data, IccUInt16ArrayTagDataEntry expected, uint size)
        {
            IccDataReader reader = this.CreateReader(data);

            IccUInt16ArrayTagDataEntry output = reader.ReadUInt16ArrayTagDataEntry(size);

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.UInt32ArrayTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadUInt32ArrayTagDataEntry(byte[] data, IccUInt32ArrayTagDataEntry expected, uint size)
        {
            IccDataReader reader = this.CreateReader(data);

            IccUInt32ArrayTagDataEntry output = reader.ReadUInt32ArrayTagDataEntry(size);

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.UInt64ArrayTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadUInt64ArrayTagDataEntry(byte[] data, IccUInt64ArrayTagDataEntry expected, uint size)
        {
            IccDataReader reader = this.CreateReader(data);

            IccUInt64ArrayTagDataEntry output = reader.ReadUInt64ArrayTagDataEntry(size);

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.UInt8ArrayTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadUInt8ArrayTagDataEntry(byte[] data, IccUInt8ArrayTagDataEntry expected, uint size)
        {
            IccDataReader reader = this.CreateReader(data);

            IccUInt8ArrayTagDataEntry output = reader.ReadUInt8ArrayTagDataEntry(size);

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.ViewingConditionsTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadViewingConditionsTagDataEntry(byte[] data, IccViewingConditionsTagDataEntry expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccViewingConditionsTagDataEntry output = reader.ReadViewingConditionsTagDataEntry();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.XYZTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadXyzTagDataEntry(byte[] data, IccXyzTagDataEntry expected, uint size)
        {
            IccDataReader reader = this.CreateReader(data);

            IccXyzTagDataEntry output = reader.ReadXyzTagDataEntry(size);

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.TextDescriptionTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadTextDescriptionTagDataEntry(byte[] data, IccTextDescriptionTagDataEntry expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccTextDescriptionTagDataEntry output = reader.ReadTextDescriptionTagDataEntry();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.CrdInfoTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadCrdInfoTagDataEntry(byte[] data, IccCrdInfoTagDataEntry expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccCrdInfoTagDataEntry output = reader.ReadCrdInfoTagDataEntry();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.ScreeningTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadScreeningTagDataEntry(byte[] data, IccScreeningTagDataEntry expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccScreeningTagDataEntry output = reader.ReadScreeningTagDataEntry();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(
            nameof(IccTestDataTagDataEntry.UcrBgTagDataEntryTestData),
            MemberType = typeof(IccTestDataTagDataEntry))]
        internal void ReadUcrBgTagDataEntry(byte[] data, IccUcrBgTagDataEntry expected, uint size)
        {
            IccDataReader reader = this.CreateReader(data);

            IccUcrBgTagDataEntry output = reader.ReadUcrBgTagDataEntry(size);

            Assert.Equal(expected, output);
        }

        private IccDataReader CreateReader(byte[] data)
        {
            return new IccDataReader(data);
        }
    }
}
