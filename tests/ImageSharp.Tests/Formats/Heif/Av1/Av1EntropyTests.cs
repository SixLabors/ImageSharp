// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1EntropyTests
{
    private const int BaseQIndex = 23;

    [Fact]
    public void ReadRandomLiteral()
    {
        // Assign
        const int bitCount = 4;
        Random rand = new(bitCount);
        byte[] values = Enumerable.Range(0, 100).Select(x => (byte)rand.Next(1 << bitCount)).ToArray();
        Av1SymbolReader reader = new(values);
        List<int> actuals = [];

        // Act
        for (int i = 0; i < values.Length; i++)
        {
            actuals.Add(reader.ReadLiteral(bitCount));
        }

        // Assert
        Assert.True(values.Length > bitCount);
    }

    [Theory]
    [InlineData(0, 0, 128)]
    [InlineData(1, 255, 128)]
    public void RawBytesFromWriteLiteral1Bit(uint value, byte exp0, byte exp1)
    {
        byte[] expected = [exp0, exp1];
        AssertRawBytesWritten(1, value, expected);
    }

    [Theory]
    [InlineData(0, 0, 0, 128)]
    [InlineData(1, 85, 118, 192)]
    [InlineData(2, 170, 165, 128)]
    [InlineData(3, 255, 255, 128)]
    public void RawBytesFromWriteLiteral2Bits(uint value, byte exp0, byte exp1, byte exp2)
    {
        byte[] expected = [exp0, exp1, exp2];
        AssertRawBytesWritten(2, value, expected);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 128)]
    [InlineData(1, 36, 198, 146, 128)]
    [InlineData(2, 73, 81, 182, 192)]
    [InlineData(3, 109, 192, 146, 64)]
    [InlineData(4, 146, 66, 73, 128)]
    [InlineData(5, 182, 214, 219, 128)]
    [InlineData(6, 219, 107, 109, 128)]
    [InlineData(7, 255, 255, 255, 128)]
    public void RawBytesFromWriteLiteral3Bits(uint value, byte exp0, byte exp1, byte exp2, byte exp3)
    {
        byte[] expected = [exp0, exp1, exp2, exp3];
        AssertRawBytesWritten(3, value, expected);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 128)]
    [InlineData(1, 17, 68, 34, 34, 128)]
    [InlineData(2, 34, 86, 68, 68, 128)]
    [InlineData(3, 51, 104, 102, 102, 128)]
    [InlineData(4, 68, 118, 34, 34, 64)]
    [InlineData(5, 85, 118, 170, 170, 192)]
    [InlineData(6, 102, 119, 51, 51, 64)]
    [InlineData(7, 119, 119, 187, 187, 192)]
    [InlineData(8, 136, 129, 17, 17, 128)]
    [InlineData(9, 153, 147, 51, 51, 128)]
    [InlineData(10, 170, 165, 85, 85, 128)]
    [InlineData(11, 187, 183, 119, 119, 128)]
    [InlineData(12, 204, 201, 153, 153, 128)]
    [InlineData(13, 221, 219, 187, 187, 128)]
    [InlineData(14, 238, 237, 221, 221, 128)]
    [InlineData(15, 255, 255, 255, 255, 128)]
    public void RawBytesFromWriteLiteral4Bits(uint value, byte exp0, byte exp1, byte exp2, byte exp3, byte exp4)
    {
        byte[] expected = [exp0, exp1, exp2, exp3, exp4];
        AssertRawBytesWritten(4, value, expected);
    }

    private static void AssertRawBytesWritten(int bitCount, uint value, byte[] expected)
    {
        // Assign
        const int writeCount = 8;
        uint[] values = new uint[writeCount];
        Array.Fill(values, value);
        Configuration configuration = Configuration.Default;
        using Av1SymbolWriter writer = new(configuration, (writeCount * bitCount) >> 3);

        // Act
        for (int i = 0; i < writeCount; i++)
        {
            writer.WriteLiteral(value, bitCount);
        }

        using IMemoryOwner<byte> actual = writer.Exit();

        // Assert
        Assert.Equal(expected, actual.GetSpan().ToArray());
    }

    [Theory]
    [InlineData(0, 0, 128)]
    [InlineData(1, 255, 128)]
    public void RawBytesReadLiteral1Bit(int value, byte exp0, byte exp1)
    {
        byte[] buffer = [exp0, exp1];
        AssertRawBytesRead(1, buffer, value);
    }

    [Theory]
    [InlineData(0, 0, 0, 128)]
    [InlineData(1, 85, 118, 192)]
    [InlineData(2, 170, 165, 128)]
    [InlineData(3, 255, 255, 128)]
    public void RawBytesReadLiteral2Bits(int value, byte exp0, byte exp1, byte exp2)
    {
        byte[] buffer = [exp0, exp1, exp2];
        AssertRawBytesRead(2, buffer, value);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 128)]
    [InlineData(1, 36, 198, 146, 128)]
    [InlineData(2, 73, 81, 182, 192)]
    [InlineData(3, 109, 192, 146, 64)]
    [InlineData(4, 146, 66, 73, 128)]
    [InlineData(5, 182, 214, 219, 128)]
    [InlineData(6, 219, 107, 109, 128)]
    [InlineData(7, 255, 255, 255, 128)]
    public void RawBytesReadLiteral3Bits(int value, byte exp0, byte exp1, byte exp2, byte exp3)
    {
        byte[] buffer = [exp0, exp1, exp2, exp3];
        AssertRawBytesRead(3, buffer, value);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 128)]
    [InlineData(1, 17, 68, 34, 34, 128)]
    [InlineData(2, 34, 86, 68, 68, 128)]
    [InlineData(3, 51, 104, 102, 102, 128)]
    [InlineData(4, 68, 118, 34, 34, 64)]
    [InlineData(5, 85, 118, 170, 170, 192)]
    [InlineData(6, 102, 119, 51, 51, 64)]
    [InlineData(7, 119, 119, 187, 187, 192)]
    [InlineData(8, 136, 129, 17, 17, 128)]
    [InlineData(9, 153, 147, 51, 51, 128)]
    [InlineData(10, 170, 165, 85, 85, 128)]
    [InlineData(11, 187, 183, 119, 119, 128)]
    [InlineData(12, 204, 201, 153, 153, 128)]
    [InlineData(13, 221, 219, 187, 187, 128)]
    [InlineData(14, 238, 237, 221, 221, 128)]
    [InlineData(15, 255, 255, 255, 255, 128)]
    public void RawBytesReadLiteral4Bits(int value, byte exp0, byte exp1, byte exp2, byte exp3, byte exp4)
    {
        byte[] buffer = [exp0, exp1, exp2, exp3, exp4];
        AssertRawBytesRead(4, buffer, value);
    }

    private static void AssertRawBytesRead(int bitCount, byte[] buffer, int expected)
    {
        // Assign
        int[] values = new int[8];
        int[] expectedValues = new int[8];
        Array.Fill(expectedValues, expected);
        Av1SymbolReader reader = new(buffer);

        // Act
        for (int i = 0; i < 8; i++)
        {
            values[i] = reader.ReadLiteral(bitCount);
        }

        // Assert
        Assert.Equal(expectedValues, values);
    }

    [Fact]
    public void RoundTripPartitionType()
    {
        // Assign
        int ctx = 7;
        Configuration configuration = Configuration.Default;
        Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        Av1PartitionType[] values = [
            Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.None,
            Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.None, Av1PartitionType.None];
        Av1PartitionType[] actuals = new Av1PartitionType[values.Length];

        // Act
        foreach (Av1PartitionType value in values)
        {
            encoder.WritePartitionType(value, 7);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), 0);
        Av1SymbolReader reader = new(encoded.GetSpan());
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadPartitionType(ctx);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Theory]
    [InlineData((int)Av1BlockSize.Block4x4, 7)]
    [InlineData((int)Av1BlockSize.Block4x4, 5)]
    [InlineData((int)Av1BlockSize.Block8x4, 7)]
    [InlineData((int)Av1BlockSize.Block4x8, 7)]
    [InlineData((int)Av1BlockSize.Block32x64, 7)]
    [InlineData((int)Av1BlockSize.Block64x32, 7)]
    [InlineData((int)Av1BlockSize.Block64x64, 7)]
    public void RoundTripSplitOrHorizontalPartitionType(int blockSize, int context)
    {
        // Assign
        Configuration configuration = Configuration.Default;
        Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        Av1PartitionType[] values = [
            Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Horizontal,
            Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Horizontal, Av1PartitionType.Horizontal];
        Av1PartitionType[] actuals = new Av1PartitionType[values.Length];

        // Act
        foreach (Av1PartitionType value in values)
        {
            encoder.WriteSplitOrHorizontal(value, (Av1BlockSize)blockSize, context);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), 0);
        Av1SymbolReader reader = new(encoded.GetSpan());
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadSplitOrHorizontal((Av1BlockSize)blockSize, context);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Theory]
    [InlineData((int)Av1BlockSize.Block4x4, 7)]
    [InlineData((int)Av1BlockSize.Block4x4, 5)]
    [InlineData((int)Av1BlockSize.Block8x4, 7)]
    [InlineData((int)Av1BlockSize.Block4x8, 7)]
    [InlineData((int)Av1BlockSize.Block32x64, 7)]
    [InlineData((int)Av1BlockSize.Block64x32, 7)]
    [InlineData((int)Av1BlockSize.Block64x64, 7)]
    public void RoundTripSplitOrVerticalPartitionType(int blockSize, int context)
    {
        // Assign
        Configuration configuration = Configuration.Default;
        Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        Av1PartitionType[] values = [
            Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Vertical,
            Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Vertical, Av1PartitionType.Vertical];
        Av1PartitionType[] actuals = new Av1PartitionType[values.Length];

        // Act
        foreach (Av1PartitionType value in values)
        {
            encoder.WriteSplitOrVertical(value, (Av1BlockSize)blockSize, context);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), 0);
        Av1SymbolReader reader = new(encoded.GetSpan());
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadSplitOrVertical((Av1BlockSize)blockSize, context);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Fact]
    public void RoundTripUseIntraBlockCopy()
    {
        // Assign
        bool[] values = [true, true, false, true, false, false, false];
        Configuration configuration = Configuration.Default;
        Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        bool[] actuals = new bool[values.Length];

        // Act
        foreach (bool value in values)
        {
            encoder.WriteUseIntraBlockCopy(value);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), 0);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadUseIntraBlockCopy();
        }

        // Assert
        Assert.Equal(values, actuals);
    }
}
