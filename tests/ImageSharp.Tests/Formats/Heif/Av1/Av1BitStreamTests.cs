// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1BitStreamTests
{
    [Theory]
    [InlineData(42, new bool[] { false, false, true, false, true, false, true, false })]
    [InlineData(52, new bool[] { false, false, true, true, false, true, false, false })]
    public void ReadAsBoolean(byte value, bool[] bits)
    {
        int bitCount = bits.Length;
        byte[] buffer = new byte[8];
        buffer[0] = value;
        Av1BitStreamReader reader = new(buffer);
        bool[] actual = new bool[bitCount];
        for (int i = 0; i < bitCount; i++)
        {
            actual[i] = reader.ReadBoolean();
        }

        Assert.Equal(bits, actual);
    }

    [Theory]
    [InlineData(6, 4)]
    [InlineData(42, 8)]
    [InlineData(52, 8)]
    [InlineData(4050, 16)]
    public void ReadAsLiteral(uint expected, int bitCount)
    {
        byte[] buffer = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, expected << (32 - bitCount));
        Av1BitStreamReader reader = new(buffer);
        uint actual = reader.ReadLiteral(bitCount);
        Assert.Equal(expected, actual);
    }

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
    [InlineData(new bool[] { false, false, true, false, true, false, true, false })]
    [InlineData(new bool[] { false, true, false, true })]
    public void WriteAsBoolean(bool[] booleans)
    {
        using AutoExpandingMemory<byte> stream = new(Configuration.Default, 8);
        Av1BitStreamWriter writer = new(stream);
        for (int i = 0; i < booleans.Length; i++)
        {
            writer.WriteBoolean(booleans[i]);
        }

        writer.Flush();

        // Read the written value back.
        Av1BitStreamReader reader = new(stream.GetEntireSpan());
        bool[] actual = new bool[booleans.Length];
        for (int i = 0; i < booleans.Length; i++)
        {
            actual[i] = reader.ReadBoolean();
        }

        Assert.Equal(booleans, actual);
    }

    [Theory]
    [InlineData(6, 4)]
    [InlineData(42, 8)]
    [InlineData(52, 8)]
    [InlineData(4050, 16)]
    public void WriteAsLiteral(uint value, int bitCount)
    {
        using AutoExpandingMemory<byte> stream = new(Configuration.Default, 8);
        Av1BitStreamWriter writer = new(stream);
        writer.WriteLiteral(value, bitCount);
        writer.Flush();

        // Read the written value back.
        Av1BitStreamReader reader = new(stream.GetEntireSpan());
        uint actual = reader.ReadLiteral(bitCount);
        Assert.Equal(value, actual);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(16)]
    public void ReadLiteralRainbowArray(int bitCount)
    {
        uint[] values = Enumerable.Range(0, (1 << bitCount) - 1).Select(i => (uint)i).ToArray();
        using AutoExpandingMemory<byte> stream = new(Configuration.Default, 280);
        Av1BitStreamWriter writer = new(stream);
        for (int i = 0; i < values.Length; i++)
        {
            writer.WriteLiteral(values[i], bitCount);
        }

        writer.Flush();

        // Read the written value back.
        Av1BitStreamReader reader = new(stream.GetEntireSpan());
        uint[] actuals = new uint[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            uint actual = reader.ReadLiteral(bitCount);
            actuals[i] = actual;
        }

        Assert.Equal(values, actuals);
    }

    [Theory]
    [InlineData(4, 6, 4, 9, 14)]
    [InlineData(8, 42, 8, 189, 63)]
    [InlineData(8, 52, 18, 255, 241)]
    [InlineData(16, 4050, 16003, 503, 814)]
    public void ReadWriteAsLiteralArray(int bitCount, uint val1, uint val2, uint val3, uint val4)
    {
        uint[] values = [val1, val2, val3, val4];
        using AutoExpandingMemory<byte> stream = new(Configuration.Default, 80);
        Av1BitStreamWriter writer = new(stream);
        for (int i = 0; i < values.Length; i++)
        {
            writer.WriteLiteral(values[i], bitCount);
        }

        writer.Flush();

        // Read the written value back.
        Av1BitStreamReader reader = new(stream.GetEntireSpan());
        for (int i = 0; i < values.Length; i++)
        {
            uint actual = reader.ReadLiteral(bitCount);
            Assert.NotEqual(0U, actual);
            Assert.Equal(values[i], actual);
        }
    }

    [Theory]

    [InlineData(4, 0, 1, 2, 3)]
    [InlineData(5, 0, 1, 2, 3)]
    [InlineData(8, 0, 1, 2, 3)]
    [InlineData(8, 4, 5, 6, 7)]
    [InlineData(16, 15, 0, 5, 8)]
    public void ReadWriteAsNonSymmetricArray(uint numberOfSymbols, uint val1, uint val2, uint val3, uint val4)
    {
        uint[] values = [val1, val2, val3, val4];
        using AutoExpandingMemory<byte> stream = new(Configuration.Default, 80);
        Av1BitStreamWriter writer = new(stream);
        for (int i = 0; i < values.Length; i++)
        {
            writer.WriteNonSymmetric(values[i], numberOfSymbols);
        }

        writer.Flush();

        // Read the written value back.
        Av1BitStreamReader reader = new(stream.GetEntireSpan());
        uint[] actuals = new uint[4];
        for (int i = 0; i < values.Length; i++)
        {
            ulong actual = reader.ReadNonSymmetric(numberOfSymbols);
            actuals[i] = (uint)actual;

            // Assert.NotEqual(0UL, actual);
        }

        Assert.Equal(values, actuals);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(8)]
    public void ReadSignedRainbowArray(int bitCount)
    {
        int maxValue = (1 << (bitCount - 1)) - 1;
        int[] values = Enumerable.Range(-maxValue, maxValue).ToArray();
        using AutoExpandingMemory<byte> stream = new(Configuration.Default, 280);
        Av1BitStreamWriter writer = new(stream);
        for (int i = 0; i < values.Length; i++)
        {
            writer.WriteSignedFromUnsigned(values[i], bitCount);
        }

        writer.Flush();

        // Read the written value back.
        Av1BitStreamReader reader = new(stream.GetEntireSpan());
        int[] actuals = new int[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            int actual = reader.ReadSignedFromUnsigned(bitCount);
            actuals[i] = actual;
        }

        Assert.Equal(values, actuals);
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
    [InlineData(new byte[] { 0x01 }, 1, 1, 8)]
    [InlineData(new byte[] { 0x01, 0x00, 0x00, 0x00 }, 1, 4, 32)] // One byte value with leading bytes.
    [InlineData(new byte[] { 0xD9, 0x01 }, 473, 2, 16)] // Two bytes.
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
    [InlineData(new byte[] { 0x80 }, 0, 1)] // Zero bit value.
    [InlineData(new byte[] { 0x60 }, 2, 3)] // One bit value, 011.
    [InlineData(new byte[] { 0x38 }, 6, 5)] // Two bit value, 00111.
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
    [InlineData(5, 6, 4, -7, -2)]
    [InlineData(7, 26, -8, -19, -26)]
    [InlineData(8, 52, 127, -127, -21)]
    [InlineData(16, -4050, -16003, -503, 8414)]
    public void ReadWriteSignedArray(int bitCount, int val1, int val2, int val3, int val4)
    {
        int[] values = [val1, val2, val3, val4];
        using AutoExpandingMemory<byte> stream = new(Configuration.Default, 80);
        Av1BitStreamWriter writer = new(stream);
        for (int i = 0; i < values.Length; i++)
        {
            writer.WriteSignedFromUnsigned(values[i], bitCount);
        }

        writer.Flush();

        // Read the written value back.
        Av1BitStreamReader reader = new(stream.GetEntireSpan());
        int[] actuals = new int[4];
        for (int i = 0; i < values.Length; i++)
        {
            int actual = reader.ReadSignedFromUnsigned(bitCount);
            actuals[i] = actual;

            // Assert.NotEqual(0, actual);
        }

        Assert.Equal(values, actuals);
    }

    [Theory]
    [InlineData(new byte[] { 0x01 }, 1, 8)] // One byte value.
    [InlineData(new byte[] { 0x81, 0x80, 0x80, 0x00 }, 1, 32)] // One byte value with trailing bytes.
    [InlineData(new byte[] { 0xD9, 0x01 }, 217, 16)] // Two byte value.
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

    [Theory]
    [InlineData(4, 6, 7, 9, 14)]
    [InlineData(8, 42, 8, 189, 63)]
    [InlineData(8, 52, 18, 255, 241)]
    [InlineData(16, 4050, 16003, 503, 8414)]
    public void ReadWriteLittleEndianBytes128Array(uint val0, uint val1, uint val2, uint val3, uint val4)
    {
        uint[] values = [val0, val1, val2, val3, val4];
        int bufferSize = 80;
        using AutoExpandingMemory<byte> stream = new(Configuration.Default, bufferSize);
        Av1BitStreamWriter writer = new(stream);
        for (int i = 0; i < values.Length; i++)
        {
            writer.WriteLittleEndianBytes128(values[i]);
        }

        writer.Flush();

        // Read the written value back.
        Av1BitStreamReader reader = new(stream.GetSpan(bufferSize));
        uint[] actuals = new uint[5];
        for (int i = 0; i < values.Length; i++)
        {
            ulong actual = reader.ReadLittleEndianBytes128(out int length);
            actuals[i] = (uint)actual;
            Assert.NotEqual(0UL, actual);
        }

        Assert.Equal(values, actuals);
    }
}
