// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
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

    [Theory]
    [MemberData(nameof(GetRangeData), 20)]
    public void RoundTripPartitionType(int context)
    {
        // Assign
        Configuration configuration = Configuration.Default;
        Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        Av1PartitionType[] values = [
            Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.None,
            Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.None, Av1PartitionType.None];
        Av1PartitionType[] actuals = new Av1PartitionType[values.Length];

        // Act
        foreach (Av1PartitionType value in values)
        {
            encoder.WritePartitionType(value, context);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadPartitionType(context);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Theory]
    [MemberData(nameof(GetSplitPartitionTypeData))]
    public void RoundTripSplitOrHorizontalPartitionType(int size, int context)
    {
        // Assign
        Av1BlockSize blockSize = (Av1BlockSize)size;
        Configuration configuration = Configuration.Default;
        Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        Av1PartitionType[] values = [
            Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Horizontal,
            Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Horizontal, Av1PartitionType.Horizontal];
        Av1PartitionType[] actuals = new Av1PartitionType[values.Length];

        // Act
        foreach (Av1PartitionType value in values)
        {
            encoder.WriteSplitOrHorizontal(value, blockSize, context);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadSplitOrHorizontal(blockSize, context);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Theory]
    [MemberData(nameof(GetSplitPartitionTypeData))]
    public void RoundTripSplitOrVerticalPartitionType(int size, int context)
    {
        // Assign
        Av1BlockSize blockSize = (Av1BlockSize)size;
        Configuration configuration = Configuration.Default;
        Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        Av1PartitionType[] values = [
            Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Vertical,
            Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Vertical, Av1PartitionType.Vertical];
        Av1PartitionType[] actuals = new Av1PartitionType[values.Length];

        // Act
        foreach (Av1PartitionType value in values)
        {
            encoder.WriteSplitOrVertical(value, blockSize, context);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadSplitOrVertical(blockSize, context);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void RoundTripSkip(int context)
    {
        // Assign
        Configuration configuration = Configuration.Default;
        Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        bool[] values = [true, true, false, false, false, false, false, false, true];
        bool[] actuals = new bool[values.Length];

        // Act
        foreach (bool value in values)
        {
            encoder.WriteSkip(value, context);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadSkip(context);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Theory]
    [MemberData(nameof(GetTransformBlockSkipData))]
    internal void RoundTripTransformBlockSkip(int transformContext, int skipContext)
    {
        // Assign
        Av1TransformSize transformSizeContext = (Av1TransformSize)transformContext;
        Configuration configuration = Configuration.Default;
        Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        bool[] values = [true, true, false, false, false, false, false, false, true];
        bool[] actuals = new bool[values.Length];

        // Act
        foreach (bool value in values)
        {
            encoder.WriteTransformBlockSkip(value, transformSizeContext, skipContext);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadTransformBlockSkip(transformSizeContext, skipContext);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Theory]
    [MemberData(nameof(GetTransformTypeData))]

    // [InlineData(2, 0, 1)]
    public void RoundTripTransformType(int txSizeContext, int intraMode, int intraDir)
    {
        // Assign
        Av1TransformSize transformSizeContext = (Av1TransformSize)txSizeContext;
        Av1FilterIntraMode filterIntraMode = (Av1FilterIntraMode)intraMode;
        Av1PredictionMode intraDirection = (Av1PredictionMode)intraDir;
        Configuration configuration = Configuration.Default;
        Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);

        // TODO: Include AdstFlipAdst, which is currently mapped to Identity.
        Av1TransformType[] values = [
            Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.Identity, Av1TransformType.AdstDct,
            Av1TransformType.DctDct, Av1TransformType.AdstAdst, Av1TransformType.Identity, Av1TransformType.DctAdst
        ];
        Av1TransformType[] actuals = new Av1TransformType[values.Length];

        // Act
        foreach (Av1TransformType value in values)
        {
            encoder.WriteTransformType(value, transformSizeContext, true, BaseQIndex, filterIntraMode, intraDirection);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadTransformType(transformSizeContext, true, false, BaseQIndex, filterIntraMode, intraDirection);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Theory]
    [MemberData(nameof(GetEndOfBlockPositionData))]
    public void RoundTripEndOfBlockPosition(int txSize, int txSizeContext, int plane, int txClass)
    {
        // Assign
        Av1TransformSize transformSize = (Av1TransformSize)txSize;
        Av1TransformSize transformSizeContext = (Av1TransformSize)txSizeContext;
        Av1ComponentType componentType = (Av1ComponentType)plane;
        Av1PlaneType planeType = (Av1PlaneType)plane;
        Av1TransformClass transformClass = (Av1TransformClass)txClass;
        Configuration configuration = Configuration.Default;
        Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);

        int[] values = [1, 2, 3, 4, 5];
        int[] actuals = new int[values.Length];

        // Act
        foreach (int value in values)
        {
            encoder.WriteEndOfBlockPosition((ushort)value, componentType, transformClass, transformSize, transformSizeContext);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadEndOfBlockPosition(transformSize, transformClass, transformSizeContext, planeType);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Fact]
    public void RoundTripGolomb()
    {
        // Assign
        Configuration configuration = Configuration.Default;
        Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);

        int[] values = Enumerable.Range(0, 16384).ToArray();
        int[] actuals = new int[values.Length];

        // Act
        foreach (int value in values)
        {
            encoder.WriteGolomb(value);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadGolomb();
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void RoundTripSegmentId(int context)
    {
        // Assign
        int[] values = [3, 6, 7, 0, 2, 0, 2, 1, 1];
        Configuration configuration = Configuration.Default;
        Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        int[] actuals = new int[values.Length];

        // Act
        foreach (int value in values)
        {
            encoder.WriteSegmentId(value, context);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadSegmentId(context);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Fact]
    public void RoundTripDeltaQuantizerIndex()
    {
        // Assign
        int[] values = [3, 6, -7, -8, -2, 0, 2, 1, -1];
        Configuration configuration = Configuration.Default;
        Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        int[] actuals = new int[values.Length];

        // Act
        foreach (int value in values)
        {
            encoder.WriteDeltaQuantizerIndex(value);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadDeltaQuantizerIndex();
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Theory]
    [MemberData(nameof(GetRangeData), (int)Av1BlockSize.AllSizes)]
    public void RoundTripFilterIntraMode(int bSize)
    {
        // Assign
        Av1BlockSize blockSize = (Av1BlockSize)bSize;
        Av1FilterIntraMode[] values = [
            Av1FilterIntraMode.DC, Av1FilterIntraMode.Vertical, Av1FilterIntraMode.DC, Av1FilterIntraMode.Paeth,
            Av1FilterIntraMode.AllFilterIntraModes, Av1FilterIntraMode.Directional157, Av1FilterIntraMode.DC, Av1FilterIntraMode.Directional157];
        Configuration configuration = Configuration.Default;
        Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        Av1FilterIntraMode[] actuals = new Av1FilterIntraMode[values.Length];

        // Act
        foreach (Av1FilterIntraMode value in values)
        {
            encoder.WriteFilterIntraMode(value, blockSize);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadFilterUltraMode(blockSize);
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

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadUseIntraBlockCopy();
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    public static TheoryData<int> GetRangeData(int count)
    {
        TheoryData<int> result = [];
        for (int i = 0; i < count; i++)
        {
            result.Add(i);
        }

        return result;
    }

    public static TheoryData<int, int> GetTransformBlockSkipData()
    {
        TheoryData<int, int> result = [];
        for (Av1TransformSize transformSizeContext = Av1TransformSize.Size4x4; transformSizeContext <= Av1TransformSize.Size64x64; transformSizeContext++)
        {
            for (int skipContext = 0; skipContext < 5; skipContext++)
            {
                result.Add((int)transformSizeContext, skipContext);
            }
        }

        return result;
    }

    public static TheoryData<int, int> GetSplitPartitionTypeData()
    {
        TheoryData<int, int> result = [];
        for (Av1BlockSize blockSize = Av1BlockSize.Block4x4; blockSize < Av1BlockSize.AllSizes; blockSize++)
        {
            for (int context = 4; context < 16; context++)
            {
                result.Add((int)blockSize, context);
            }
        }

        return result;
    }

    public static TheoryData<int, int, int> GetTransformTypeData()
    {
        TheoryData<int, int, int> result = [];
        for (Av1TransformSize transformSize = Av1TransformSize.Size4x4; transformSize < Av1TransformSize.AllSizes; transformSize++)
        {
            // TODO: Figure out why larger sizes don't round trip correctly.
            if (transformSize.GetSquareSize() >= Av1TransformSize.Size16x16 || transformSize is Av1TransformSize.Size32x8 or Av1TransformSize.Size8x32)
            {
                continue;
            }

            for (Av1FilterIntraMode filterIntraMode = Av1FilterIntraMode.DC; filterIntraMode <= Av1FilterIntraMode.AllFilterIntraModes; filterIntraMode++)
            {
                for (Av1PredictionMode intraDirection = Av1PredictionMode.IntraModeStart; intraDirection < Av1PredictionMode.IntraModeEnd; intraDirection++)
                {
                    result.Add((int)transformSize, (int)filterIntraMode, (int)intraDirection);
                }
            }
        }

        return result;
    }

    public static TheoryData<int, int, int, int> GetEndOfBlockPositionData()
    {
        TheoryData<int, int, int, int> result = [];
        for (Av1TransformSize transformSize = Av1TransformSize.Size4x4; transformSize < Av1TransformSize.AllSizes; transformSize++)
        {
            for (Av1TransformSize transformSizeContext = Av1TransformSize.Size4x4; transformSizeContext <= Av1TransformSize.Size64x64; transformSizeContext++)
            {
                for (int componentType = 0; componentType < 2; componentType++)
                {
                    for (Av1TransformClass transformClass = Av1TransformClass.Class2D; transformClass <= Av1TransformClass.ClassVertical; transformClass++)
                    {
                        result.Add((int)transformSize, (int)transformSizeContext, componentType, (int)transformClass);
                    }
                }
            }
        }

        return result;
    }
}
