// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats.Heif.Av1;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1BitsStreamTests
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

    [Theory]
    [InlineData(new bool[] { false, false, true, false, true, false, true, false })]
    [InlineData(new bool[] { false, true, false, true })]
    public void WriteAsBoolean(bool[] booleans)
    {
        using MemoryStream stream = new(8);
        Av1BitStreamWriter writer = new(stream);
        for (int i = 0; i < booleans.Length; i++)
        {
            writer.WriteBoolean(booleans[i]);
        }

        writer.Flush();

        // Read the written value back.
        Av1BitStreamReader reader = new(stream.GetBuffer());
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
        using MemoryStream stream = new(8);
        Av1BitStreamWriter writer = new(stream);
        writer.WriteLiteral(value, bitCount);
        writer.Flush();

        // Read the written value back.
        Av1BitStreamReader reader = new(stream.GetBuffer());
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
        using MemoryStream stream = new(280);
        Av1BitStreamWriter writer = new(stream);
        for (int i = 0; i < values.Length; i++)
        {
            writer.WriteLiteral(values[i], bitCount);
        }

        writer.Flush();

        // Read the written value back.
        Av1BitStreamReader reader = new(stream.GetBuffer());
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
        using MemoryStream stream = new(80);
        Av1BitStreamWriter writer = new(stream);
        for (int i = 0; i < values.Length; i++)
        {
            writer.WriteLiteral(values[i], bitCount);
        }

        writer.Flush();

        // Read the written value back.
        Av1BitStreamReader reader = new(stream.GetBuffer());
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
        using MemoryStream stream = new(80);
        Av1BitStreamWriter writer = new(stream);
        for (int i = 0; i < values.Length; i++)
        {
            writer.WriteNonSymmetric(values[i], numberOfSymbols);
        }

        writer.Flush();

        // Read the written value back.
        Av1BitStreamReader reader = new(stream.GetBuffer());
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
        using MemoryStream stream = new(280);
        Av1BitStreamWriter writer = new(stream);
        for (int i = 0; i < values.Length; i++)
        {
            writer.WriteSignedFromUnsigned(values[i], bitCount);
        }

        writer.Flush();

        // Read the written value back.
        Av1BitStreamReader reader = new(stream.GetBuffer());
        int[] actuals = new int[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            int actual = reader.ReadSignedFromUnsigned(bitCount);
            actuals[i] = actual;
        }

        Assert.Equal(values, actuals);
    }

    [Theory]
    [InlineData(5, 6, 4, -7, -2)]
    [InlineData(7, 26, -8, -19, -26)]
    [InlineData(8, 52, 127, -127, -21)]
    [InlineData(16, -4050, -16003, -503, 8414)]
    public void ReadWriteSignedArray(int bitCount, int val1, int val2, int val3, int val4)
    {
        int[] values = [val1, val2, val3, val4];
        using MemoryStream stream = new(80);
        Av1BitStreamWriter writer = new(stream);
        for (int i = 0; i < values.Length; i++)
        {
            writer.WriteSignedFromUnsigned(values[i], bitCount);
        }

        writer.Flush();

        // Read the written value back.
        Av1BitStreamReader reader = new(stream.GetBuffer());
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
    [InlineData(4, 6, 7, 9, 14)]
    [InlineData(8, 42, 8, 189, 63)]
    [InlineData(8, 52, 18, 255, 241)]
    [InlineData(16, 4050, 16003, 503, 8414)]
    public void ReadWriteLittleEndianBytes128Array(uint val0, uint val1, uint val2, uint val3, uint val4)
    {
        uint[] values = [val0, val1, val2, val3, val4];
        using MemoryStream stream = new(80);
        Av1BitStreamWriter writer = new(stream);
        for (int i = 0; i < values.Length; i++)
        {
            writer.WriteLittleEndianBytes128(values[i]);
        }

        writer.Flush();

        // Read the written value back.
        Av1BitStreamReader reader = new(stream.GetBuffer());
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
