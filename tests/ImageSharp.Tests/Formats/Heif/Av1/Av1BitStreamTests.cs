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
    public void ReadAsBoolean(int value, bool[] bits)
    {
        int bitCount = bits.Length;
        byte[] buffer = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value << (32 - bitCount));
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
        byte[] buffer = new byte[4];
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
        using MemoryStream stream = new(4);
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
        using MemoryStream stream = new(4);
        Av1BitStreamWriter writer = new(stream);
        writer.WriteLiteral(value, bitCount);
        writer.Flush();

        // Read the written value back.
        Av1BitStreamReader reader = new(stream.GetBuffer());
        uint actual = reader.ReadLiteral(bitCount);
        Assert.Equal(value, actual);
    }
}
