// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1BitStreamTests
{
    [Fact]
    public void ReadLiteral32BitsWithMsbSet()
    {
        // arrange
        // Three 32-bit values with MSB set.
        byte[] buffer =
            [
                0xff, 0xff, 0xff, 0xff, // 4294967295
                0x80, 0xff, 0xee, 0xdd, // 2164256477
                0xa0, 0xaa, 0xbb, 0xcc // 2695543756
            ];
        uint expected0 = 4294967295;
        uint expected1 = 2164256477;
        uint expected2 = 2695543756;
        Av1BitStreamReader reader = new(buffer);

        // act
        uint actual0 = reader.ReadLiteral(32);
        uint actual1 = reader.ReadLiteral(32);
        uint actual2 = reader.ReadLiteral(32);

        // assert
        Assert.Equal(expected0, actual0);
        Assert.Equal(expected1, actual1);
        Assert.Equal(expected2, actual2);
    }

    [Theory]
    [InlineData(5, 1, 2, 3, 4)]
    public void ReadWriteAsNonSymmetricArray(uint numberOfSymbols, uint val1, uint val2, uint val3, uint val4)
    {
        uint[] values = [val1, val2, val3, val4];
        byte[] buffer = new byte[values.Length * sizeof(uint)];
        Av1BitStreamWriter writer = new(buffer);
        for (int i = 0; i < values.Length; i++)
        {
            writer.WriteNonSymmetric(values[i], numberOfSymbols);
        }

        writer.Flush();

        // Read the written value back.
        Av1BitStreamReader reader = new(buffer);
        uint[] actuals = new uint[4];
        for (int i = 0; i < values.Length; i++)
        {
            ulong actual = reader.ReadNonSymmetric(numberOfSymbols);
            actuals[i] = (uint)actual;
        }

        Assert.Equal(values, actuals);
    }

    [Fact]
    public void SignedReferenceSubexponentialMatchesFiniteRecentering()
    {
        const int ValueMagnitude = 5;
        const int GroupBitCount = 3;
        byte[] buffer = new byte[2];
        Av1BitStreamWriter writer = new(buffer);
        writer.WriteSignedReferenceSubexponential(-4, ValueMagnitude, GroupBitCount, -4);
        writer.WriteSignedReferenceSubexponential(4, ValueMagnitude, GroupBitCount, -4);
        writer.WriteSignedReferenceSubexponential(0, ValueMagnitude, GroupBitCount, 0);
        writer.Flush();

        Assert.Equal([0x1e, 0x00], buffer);

        Av1BitStreamReader reader = new(buffer);
        Assert.Equal(-4, reader.ReadSignedReferenceSubexponential(ValueMagnitude, GroupBitCount, -4));
        Assert.Equal(4, reader.ReadSignedReferenceSubexponential(ValueMagnitude, GroupBitCount, -4));
        Assert.Equal(0, reader.ReadSignedReferenceSubexponential(ValueMagnitude, GroupBitCount, 0));
    }

    [Fact]
    public void ReadSignedFromUnsigned()
    {
        // arrange
        byte[] buffer = { 0xd2, 0xa4 };
        Av1BitStreamReader reader = new(buffer);
        int expected0 = -23;
        int expected1 = 41;

        // act
        int actual0 = reader.ReadSignedFromUnsigned(7);
        int actual1 = reader.ReadSignedFromUnsigned(7);

        Assert.Equal(expected0, actual0);
        Assert.Equal(expected1, actual1);
    }

    [Theory]
    [InlineData(new byte[] { 0xD9, 0x01, 0x00, 0x00 }, 473, 4, 32)] // Two byte value with leading bytes.
    public void ReadLittleEndian(byte[] buffer, uint expected, int n, int expectedBitPosition)
    {
        // arrange
        Av1BitStreamReader reader = new(buffer);

        // act
        uint actual = reader.ReadLittleEndian(n);

        Assert.Equal(expected, actual);
        Assert.Equal(expectedBitPosition, reader.BitPosition);
    }

    [Theory]
    [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x01, 0xFF, 0xFF, 0xFF, 0xFF, 0xFE }, uint.MaxValue - 1, 63)] // 31 bit value.
    public void ReadUnsignedVariableLength(byte[] buffer, uint expected, int expectedBitPosition)
    {
        // arrange
        Av1BitStreamReader reader = new(buffer);

        // act
        uint actual = reader.ReadUnsignedVariableLength();

        // assert
        Assert.Equal(expected, actual);
        Assert.Equal(expectedBitPosition, reader.BitPosition);
    }

    [Theory]
    [InlineData(new byte[] { 0xD9, 0x81, 0x80, 0x80, 0x00 }, 217, 40)] // Two byte value with trailing bytes.
    public void ReadLittleEndianBytes128(byte[] buffer, ulong expected, int expectedBitPosition)
    {
        // arrange
        Av1BitStreamReader reader = new(buffer);

        // act
        ulong actual = reader.ReadLittleEndianBytes128(out int length);

        // assert
        Assert.Equal(expected, actual);
        Assert.Equal(expectedBitPosition, reader.BitPosition);
        Assert.NotEqual(0UL, actual);
    }
}
