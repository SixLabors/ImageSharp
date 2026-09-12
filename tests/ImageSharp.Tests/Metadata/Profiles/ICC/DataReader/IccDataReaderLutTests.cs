// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.ICC.DataReader;

[Trait("Profile", "Icc")]
public class IccDataReaderLutTests
{
    [Theory]
    [MemberData(nameof(IccTestDataLut.ClutTestData), MemberType = typeof(IccTestDataLut))]
    internal void ReadClut(byte[] data, IccClut expected, int inChannelCount, int outChannelCount, bool isFloat)
    {
        IccDataReader reader = CreateReader(data);

        IccClut output = reader.ReadClut(inChannelCount, outChannelCount, isFloat);

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataLut.Clut8TestData), MemberType = typeof(IccTestDataLut))]
    internal void ReadClut8(byte[] data, IccClut expected, int inChannelCount, int outChannelCount, byte[] gridPointCount)
    {
        IccDataReader reader = CreateReader(data);

        IccClut output = reader.ReadClut8(inChannelCount, outChannelCount, gridPointCount);

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataLut.Clut16TestData), MemberType = typeof(IccTestDataLut))]
    internal void ReadClut16(byte[] data, IccClut expected, int inChannelCount, int outChannelCount, byte[] gridPointCount)
    {
        IccDataReader reader = CreateReader(data);

        IccClut output = reader.ReadClut16(inChannelCount, outChannelCount, gridPointCount);

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataLut.ClutF32TestData), MemberType = typeof(IccTestDataLut))]
    internal void ReadClutF32(byte[] data, IccClut expected, int inChannelCount, int outChannelCount, byte[] gridPointCount)
    {
        IccDataReader reader = CreateReader(data);

        IccClut output = reader.ReadClutF32(inChannelCount, outChannelCount, gridPointCount);

        Assert.Equal(expected, output);
    }

    [Fact]
    public void ReadClut_WithOversizedDimensions_ThrowsInvalidIccProfileException()
    {
        byte[] gridPointCount = Enumerable.Repeat((byte)3, 15).ToArray();

        Assert.Throws<InvalidIccProfileException>(() => CreateReader(new byte[8]).ReadClut8(15, 15, gridPointCount));
        Assert.Throws<InvalidIccProfileException>(() => CreateReader(new byte[8]).ReadClut16(15, 15, gridPointCount));
        Assert.Throws<InvalidIccProfileException>(() => CreateReader(new byte[8]).ReadClutF32(15, 15, gridPointCount));
    }

    /// <summary>
    /// A complete profile header and element table do not make absent CLUT values readable.
    /// </summary>
    [Fact]
    public void ReadTagDataEntry_WithTruncatedClut_RejectsMissingValues()
    {
        // A2B0 starts at byte 144; its element at byte 168 declares a 15-channel, three-point grid.
        byte[] data = Convert.FromHexString(
            "000000C874657374040000006D6E74725247422058595A200000000000000000000000006163737000000000000000000000" +
            "0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
            "00000000000000000000000000000000000000000000000000000000000000014132423000000090000000386D7065740000" +
            "000000000000000000010000001800000020636C7574000F000F030303030303030303030303030303000000000000000000");

        IccDataReader reader = new(data);
        IccTagTableEntry tag = new(IccProfileTag.AToB0, 144, 56);

        Assert.Throws<InvalidIccProfileException>(() => reader.ReadTagDataEntry(tag));
        Assert.Empty(new IccProfile(data).Entries);
    }

    [Theory]
    [MemberData(nameof(IccTestDataLut.Lut8TestData), MemberType = typeof(IccTestDataLut))]
    internal void ReadLut8(byte[] data, IccLut expected)
    {
        IccDataReader reader = CreateReader(data);

        IccLut output = reader.ReadLut8();

        Assert.Equal(expected, output);
    }

    [Theory]
    [MemberData(nameof(IccTestDataLut.Lut16TestData), MemberType = typeof(IccTestDataLut))]
    internal void ReadLut16(byte[] data, IccLut expected, int count)
    {
        IccDataReader reader = CreateReader(data);

        IccLut output = reader.ReadLut16(count);

        Assert.Equal(expected, output);
    }

    private static IccDataReader CreateReader(byte[] data)
    {
        return new IccDataReader(data);
    }
}
