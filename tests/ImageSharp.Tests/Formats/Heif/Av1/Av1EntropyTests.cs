// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Tests.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1EntropyTests
{
    private const int BaseQIndex = 23;

    [Theory]
    [InlineData(4, true)]
    [InlineData(1, true)]
    public void DeltaLoopFilterChannelsAdaptIndependently(int channelCount, bool updateCdf)
    {
        ReadOnlySpan<int> deltas = [0, 1, -2, 3, -1, 0, 17, -3, 2, -9, 0, 1];
        Av1Distribution[] channels = new Av1Distribution[channelCount];
        for (int channel = 0; channel < channelCount; channel++)
        {
            // libaom gives each multi-delta channel this independent initial CDF.
            channels[channel] = new(28160, 32120, 32677);
        }

        using Av1SymbolWriter writer = new(Configuration.Default, updateCdf);
        Span<byte> output = writer.GetTileBuffer();
        for (int index = 0; index < 96; index++)
        {
            int delta = deltas[index % deltas.Length];
            int magnitude = Math.Abs(delta);
            writer.WriteSymbol(ref output, Math.Min(magnitude, 3), channels[index % channelCount]);
            if (magnitude >= 3)
            {
                // The escape magnitude is 2^bits + 1 plus the transmitted remainder, followed by its sign.
                int bits = BitOperations.Log2((uint)(magnitude - 1));
                writer.WriteLiteral(ref output, (uint)(bits - 1), 3);
                writer.WriteLiteral(ref output, (uint)(magnitude - (1 << bits) - 1), bits);
            }

            if (magnitude != 0)
            {
                writer.WriteLiteral(ref output, delta < 0 ? 1U : 0U, 1);
            }
        }

        using IMemoryOwner<byte> payload = writer.Exit();
        Av1SymbolDecoder reader = new(Configuration.Default, payload.Memory.Span, BaseQIndex, updateCdf);
        for (int index = 0; index < 96; index++)
        {
            Assert.Equal(deltas[index % deltas.Length], reader.ReadDeltaLoopFilter(channelCount > 1, index % channelCount));
        }
    }

    [Fact]
    public void FrameEntropyLifecyclePreservesIndependentDeltaLoopFilterChannels()
    {
        Av1FrameEntropyContext source = new(BaseQIndex);
        Av1FrameEntropyContext copy = new(BaseQIndex);
        Av1FrameEntropyContext snapshot = new(BaseQIndex);
        Av1Distribution defaults = new(28160, 32120, 32677);
        for (int channel = 0; channel < 4; channel++)
        {
            for (int observation = 0; observation < 20; observation++)
            {
                source.DeltaLoopFilterMultiAbsolute[channel].Update((channel % 3) + 1);
            }
        }

        copy.CopyFrom(source);
        source.SnapshotTo(snapshot);
        for (int channel = 0; channel < 4; channel++)
        {
            Av1Distribution original = source.DeltaLoopFilterMultiAbsolute[channel];
            Av1Distribution copied = copy.DeltaLoopFilterMultiAbsolute[channel];
            Av1Distribution published = snapshot.DeltaLoopFilterMultiAbsolute[channel];
            Assert.NotSame(original, copied);
            Assert.NotSame(original, published);
            for (int symbol = 0; symbol < 4; symbol++)
            {
                Assert.Equal(original[symbol], copied[symbol]);
                Assert.Equal(original[symbol], published[symbol]);
                Assert.Equal(defaults[symbol], source.DeltaLoopFilterAbsolute[symbol]);
            }

            // A published CDF preserves probabilities but restarts its observation history. The next
            // identical symbol must therefore move its threshold further than in the twenty-count source.
            original.Update(0);
            published.Update(0);
            Assert.NotEqual(original[0], published[0]);
            Assert.NotEqual(original[0], copied[0]);
        }

        copy.ResetToDefaults(255);
        Av1FrameEntropyContext fresh = new(BaseQIndex);
        for (int channel = 0; channel < 4; channel++)
        {
            for (int symbol = 0; symbol < 4; symbol++)
            {
                Assert.Equal(defaults[symbol], copy.DeltaLoopFilterMultiAbsolute[channel][symbol]);
                Assert.Equal(defaults[symbol], fresh.DeltaLoopFilterMultiAbsolute[channel][symbol]);
            }
        }
    }

    [Fact]
    public void ProbabilityCostTableMatchesDefinition()
    {
        for (int normalizedProbability = 128; normalizedProbability < 256; normalizedProbability++)
        {
            int expected = (int)Math.Round(
                -Math.Log2(normalizedProbability / 256D) * (1 << Av1ProbabilityCost.CostShift),
                MidpointRounding.AwayFromZero);

            int actual = Av1ProbabilityCost.GetProbabilityCost(normalizedProbability << 7);

            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void SymbolWriterMatchesCurrentLibaomCarryRegression()
    {
        using Av1SymbolWriter writer = new(Configuration.Default, updateCdf: false);
        Span<byte> output = writer.GetTileBuffer();
        writer.WriteBoolean(ref output, false, 16_384);
        writer.WriteBoolean(ref output, false, 16_384);
        writer.WriteBoolean(ref output, true, 512);
        writer.WriteBoolean(ref output, false, 8_192);
        using IMemoryOwner<byte> encoded = writer.Exit();

        Assert.Equal(2, encoded.Memory.Length);
        Assert.Equal(63, encoded.Memory.Span[0]);
    }

    [Fact]
    public void SymbolWriterRentsOneTileBuffer()
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        TestMemoryAllocator.AllocationRequest allocation;

        using (Av1SymbolWriter writer = new(configuration, updateCdf: false))
        {
            Span<byte> output = writer.GetTileBuffer();
            writer.WriteLiteral(ref output, false);
            allocation = Assert.Single(allocator.AllocationLog);

            Assert.Equal(typeof(byte), allocation.ElementType);
            Assert.Equal(Av1SymbolWriter.InitialTileBufferLength, allocation.Length);
        }

        TestMemoryAllocator.ReturnRequest returned = Assert.Single(allocator.ReturnLog);
        Assert.Equal(allocation.HashCodeOfBuffer, returned.HashCodeOfBuffer);
    }

    [Fact]
    public void SymbolWriterGrowsTileBufferAndKeepsTheOtherTiles()
    {
        // The second tile writes more bytes than a new tile buffer holds, so its buffer grows during the writes.
        const int largeTileByteCount = Av1SymbolWriter.InitialTileBufferLength + 4096;
        using Av1SymbolWriter writer = new(Configuration.Default, updateCdf: false);

        writer.Reset(0);
        Span<byte> output = writer.GetTileBuffer();
        for (int index = 0; index < 64; index++)
        {
            writer.WriteLiteral(ref output, (uint)(index * 7) & 0xFF, 8);
        }

        int firstLength = writer.ExitTile();

        writer.Reset(1);
        output = writer.GetTileBuffer();
        for (int index = 0; index < largeTileByteCount; index++)
        {
            writer.WriteLiteral(ref output, (uint)(index * 13) & 0xFF, 8);
        }

        int secondLength = writer.ExitTile();
        Assert.True(secondLength > Av1SymbolWriter.InitialTileBufferLength);

        // Each tile decodes from its own buffer. The growth of the second buffer does not change the first tile.
        Av1SymbolReader first = new(writer.GetTileOutput(0, firstLength).ToArray(), updateCdf: false);
        for (int index = 0; index < 64; index++)
        {
            Assert.Equal((index * 7) & 0xFF, first.ReadLiteral(8));
        }

        Av1SymbolReader second = new(writer.GetTileOutput(1, secondLength).ToArray(), updateCdf: false);
        for (int index = 0; index < largeTileByteCount; index++)
        {
            Assert.Equal((index * 13) & 0xFF, second.ReadLiteral(8));
        }
    }

    [Theory]
    [InlineData(Av1SymbolWriter.InitialTileBufferLength - 16)]
    [InlineData(Av1SymbolWriter.InitialTileBufferLength - 9)]
    [InlineData(Av1SymbolWriter.InitialTileBufferLength - 8)]
    [InlineData(Av1SymbolWriter.InitialTileBufferLength - 5)]
    [InlineData(Av1SymbolWriter.InitialTileBufferLength - 2)]
    [InlineData(Av1SymbolWriter.InitialTileBufferLength - 1)]
    [InlineData(Av1SymbolWriter.InitialTileBufferLength)]
    [InlineData(Av1SymbolWriter.InitialTileBufferLength + 16)]
    public void SymbolWriterRoundTripsTilesAtTheTileBufferLimit(int byteCount)
    {
        // Near the limit, the buffer grows either in a word flush or only for the terminating bytes of the tile.
        using Av1SymbolWriter writer = new(Configuration.Default, updateCdf: false);
        Span<byte> output = writer.GetTileBuffer();
        for (int index = 0; index < byteCount; index++)
        {
            writer.WriteLiteral(ref output, (uint)((index * 29) + (index >> 8)) & 0xFF, 8);
        }

        int length = writer.ExitTile();
        Av1SymbolReader reader = new(writer.GetTileOutput(0, length).ToArray(), updateCdf: false);
        for (int index = 0; index < byteCount; index++)
        {
            Assert.Equal(((index * 29) + (index >> 8)) & 0xFF, reader.ReadLiteral(8));
        }
    }

    [Fact]
    public void RawBytesFromWriteLiteral4Bits()
    {
        RawBytesFromWriteLiteral4BitsCase(0U, 0, 0, 0, 0, 128);
        RawBytesFromWriteLiteral4BitsCase(1U, 17, 68, 34, 34, 128);
        RawBytesFromWriteLiteral4BitsCase(2U, 34, 86, 68, 68, 128);
        RawBytesFromWriteLiteral4BitsCase(3U, 51, 104, 102, 102, 128);
        RawBytesFromWriteLiteral4BitsCase(4U, 68, 118, 34, 34, 64);
        RawBytesFromWriteLiteral4BitsCase(5U, 85, 118, 170, 170, 192);
        RawBytesFromWriteLiteral4BitsCase(6U, 102, 119, 51, 51, 64);
        RawBytesFromWriteLiteral4BitsCase(7U, 119, 119, 187, 187, 192);
        RawBytesFromWriteLiteral4BitsCase(8U, 136, 129, 17, 17, 128);
        RawBytesFromWriteLiteral4BitsCase(9U, 153, 147, 51, 51, 128);
        RawBytesFromWriteLiteral4BitsCase(10U, 170, 165, 85, 85, 128);
        RawBytesFromWriteLiteral4BitsCase(11U, 187, 183, 119, 119, 128);
        RawBytesFromWriteLiteral4BitsCase(12U, 204, 201, 153, 153, 128);
        RawBytesFromWriteLiteral4BitsCase(13U, 221, 219, 187, 187, 128);
        RawBytesFromWriteLiteral4BitsCase(14U, 238, 237, 221, 221, 128);
        RawBytesFromWriteLiteral4BitsCase(15U, 255, 255, 255, 255, 128);
    }

    private static void RawBytesFromWriteLiteral4BitsCase(uint value, byte exp0, byte exp1, byte exp2, byte exp3, byte exp4)
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
        using Av1SymbolWriter writer = new(configuration, updateCdf: true);
        Span<byte> output = writer.GetTileBuffer();

        // Act
        for (int i = 0; i < writeCount; i++)
        {
            writer.WriteLiteral(ref output, value, bitCount);
        }

        using IMemoryOwner<byte> actual = writer.Exit();

        // Assert
        Assert.Equal(expected, actual.GetSpan().ToArray());
    }

    [Fact]
    public void RoundTripPaletteColorMaps()
    {
        const int Rows = 5;
        const int Columns = 7;
        const int Width = 9;
        const int Height = 6;
        Configuration configuration = Configuration.Default;
        using IMemoryOwner<byte> sourceOwner = configuration.MemoryAllocator.Allocate<byte>(Width * Height);
        using IMemoryOwner<byte> decodedOwner = configuration.MemoryAllocator.Allocate<byte>(Width * Height);
        Av1PlaneRegion<byte> source = new(sourceOwner.Memory, Width, new Rectangle(0, 0, Width, Height));
        Av1PlaneRegion<byte> decoded = new(decodedOwner.Memory, Width, new Rectangle(0, 0, Width, Height));
        Av1PlaneRegion<byte> sourceRegion = source;
        Av1PlaneRegion<byte> decodedRegion = decoded;
        using Av1SymbolEncoder encoder = new(configuration, BaseQIndex, updateCdf: true);
        Span<byte> output = encoder.GetTileBuffer();
        for (int paletteSize = 2; paletteSize <= Av1Constants.PaletteMaxSize; paletteSize++)
        {
            for (int plane = 0; plane < 2; plane++)
            {
                for (int row = 0; row < Rows; row++)
                {
                    Span<byte> sourceRow = source.GetRowSpan(row);
                    for (int column = 0; column < Columns; column++)
                    {
                        sourceRow[column] = (byte)(((row * 3) + (column * 5) + plane) % paletteSize);
                    }
                }

                encoder.WritePaletteColorMap<Av1SymbolEncoder.SymbolWriteOperation>(
                    ref output,
                    paletteSize,
                    (Av1PlaneType)plane,
                    Rows,
                    Columns,
                    sourceRegion);
            }
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();
        Av1SymbolDecoder decoder = new(configuration, encoded.GetSpan(), BaseQIndex, updateCdf: true);
        for (int paletteSize = 2; paletteSize <= Av1Constants.PaletteMaxSize; paletteSize++)
        {
            for (int plane = 0; plane < 2; plane++)
            {
                for (int row = 0; row < Height; row++)
                {
                    decoded.GetRowSpan(row).Fill(byte.MaxValue);
                }

                decoder.ReadPaletteColorMap(
                    paletteSize,
                    (Av1PlaneType)plane,
                    Rows,
                    Columns,
                    decodedRegion);

                for (int row = 0; row < Rows; row++)
                {
                    ReadOnlySpan<byte> decodedRow = decoded.GetRowSpan(row);
                    for (int column = 0; column < Columns; column++)
                    {
                        Assert.Equal(
                            (byte)(((row * 3) + (column * 5) + plane) % paletteSize),
                            decodedRow[column]);
                    }

                    for (int column = Columns; column < Width; column++)
                    {
                        Assert.Equal(byte.MaxValue, decodedRow[column]);
                    }
                }

                for (int row = Rows; row < Height; row++)
                {
                    ReadOnlySpan<byte> decodedRow = decoded.GetRowSpan(row);
                    for (int column = 0; column < Width; column++)
                    {
                        Assert.Equal(byte.MaxValue, decodedRow[column]);
                    }
                }
            }
        }

        decoder.ValidateTrailingBits();
    }

    [Fact]
    public void RoundTripPartitionType()
    {
        foreach (ITheoryDataRow row in GetRangeData(20))
        {
            object[] values = row.GetData();
            RoundTripPartitionTypeCase((int)values[0]);
        }

        foreach (ITheoryDataRow row in GetSplitPartitionTypeData())
        {
            object[] values = row.GetData();
            RoundTripSplitOrHorizontalPartitionTypeCase((int)values[0], (int)values[1]);
            RoundTripSplitOrVerticalPartitionTypeCase((int)values[0], (int)values[1]);
        }
    }

    private static void RoundTripPartitionTypeCase(int context)
    {
        // Assign
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, BaseQIndex, updateCdf: true);
        Span<byte> output = encoder.GetTileBuffer();
        Av1PartitionType[] values = [
            Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.None,
            Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.None, Av1PartitionType.None];

        Av1PartitionType[] actuals = new Av1PartitionType[values.Length];

        // Act
        foreach (Av1PartitionType value in values)
        {
            encoder.WritePartitionType<Av1SymbolEncoder.SymbolWriteOperation>(ref output, value, context);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex, updateCdf: true);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadPartitionType(context);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    private static void RoundTripSplitOrHorizontalPartitionTypeCase(int size, int context)
    {
        // Assign
        Av1BlockSize blockSize = (Av1BlockSize)size;
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, BaseQIndex, updateCdf: true);
        Span<byte> output = encoder.GetTileBuffer();
        Av1PartitionType[] values = [
            Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Horizontal,
            Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Horizontal, Av1PartitionType.Horizontal];

        Av1PartitionType[] actuals = new Av1PartitionType[values.Length];

        // Act
        foreach (Av1PartitionType value in values)
        {
            encoder.WriteSplitOrHorizontal<Av1SymbolEncoder.SymbolWriteOperation>(ref output, value, blockSize, context);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex, updateCdf: true);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadSplitOrHorizontal(blockSize, context);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    private static void RoundTripSplitOrVerticalPartitionTypeCase(int size, int context)
    {
        // Assign
        Av1BlockSize blockSize = (Av1BlockSize)size;
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, BaseQIndex, updateCdf: true);
        Span<byte> output = encoder.GetTileBuffer();
        Av1PartitionType[] values = [
            Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Vertical,
            Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Vertical, Av1PartitionType.Vertical];

        Av1PartitionType[] actuals = new Av1PartitionType[values.Length];

        // Act
        foreach (Av1PartitionType value in values)
        {
            encoder.WriteSplitOrVertical<Av1SymbolEncoder.SymbolWriteOperation>(ref output, value, blockSize, context);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex, updateCdf: true);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadSplitOrVertical(blockSize, context);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Fact]
    public void RoundTripSkip()
    {
        for (int context = 0; context < 3; context++)
        {
            RoundTripSkipCase(context);
        }

        foreach (ITheoryDataRow row in GetTransformBlockSkipData())
        {
            object[] values = row.GetData();
            RoundTripTransformBlockSkipCase((int)values[0], (int)values[1]);
        }
    }

    private static void RoundTripSkipCase(int context)
    {
        // Assign
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, BaseQIndex, updateCdf: true);
        Span<byte> output = encoder.GetTileBuffer();
        bool[] values = [true, true, false, false, false, false, false, false, true];
        bool[] actuals = new bool[values.Length];

        // Act
        foreach (bool value in values)
        {
            encoder.WriteSkip<Av1SymbolEncoder.SymbolWriteOperation>(ref output, value, context);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex, updateCdf: true);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadSkip(context);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    private static void RoundTripTransformBlockSkipCase(int transformContext, int skipContext)
    {
        // Assign
        Av1TransformSize transformSizeContext = (Av1TransformSize)transformContext;
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, BaseQIndex, updateCdf: true);
        Span<byte> output = encoder.GetTileBuffer();
        bool[] values = [true, true, false, false, false, false, false, false, true];
        bool[] actuals = new bool[values.Length];

        // Act
        foreach (bool value in values)
        {
            encoder.WriteTransformBlockSkip<Av1SymbolEncoder.SymbolWriteOperation>(ref output, value, transformSizeContext, skipContext);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex, updateCdf: true);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadTransformBlockSkip(transformSizeContext, skipContext);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Fact]
    public void RoundTripTransformType()
    {
        foreach (ITheoryDataRow row in GetTransformTypeData())
        {
            object[] values = row.GetData();
            RoundTripTransformTypeCase((int)values[0], (int)values[1], (int)values[2]);
        }

        foreach (ITheoryDataRow row in GetInterTransformTypeData())
        {
            object[] values = row.GetData();
            RoundTripInterTransformTypeCase((int)values[0], (bool)values[1]);
        }
    }

    private static void RoundTripTransformTypeCase(int txSizeContext, int intraMode, int intraDir)
    {
        // Assign
        Av1TransformSize transformSizeContext = (Av1TransformSize)txSizeContext;
        Av1FilterIntraMode filterIntraMode = (Av1FilterIntraMode)intraMode;
        Av1PredictionMode intraDirection = (Av1PredictionMode)intraDir;
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, BaseQIndex, updateCdf: true);
        Span<byte> output = encoder.GetTileBuffer();

        // TODO: Include AdstFlipAdst, which is currently mapped to Identity.
        Av1TransformType[] values = [
            Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.Identity, Av1TransformType.AdstDct,
            Av1TransformType.DctDct, Av1TransformType.AdstAdst, Av1TransformType.Identity, Av1TransformType.DctAdst
        ];

        Av1TransformType[] actuals = new Av1TransformType[values.Length];

        // Act
        foreach (Av1TransformType value in values)
        {
            encoder.WriteTransformType<Av1SymbolEncoder.SymbolWriteOperation>(
                ref output, value, transformSizeContext, true, BaseQIndex, filterIntraMode, intraDirection, usesInterTransformSet: false);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex, updateCdf: true);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadTransformType(transformSizeContext, true, false, false, true, filterIntraMode, intraDirection);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    private static void RoundTripInterTransformTypeCase(int txSize, bool useReducedTransformSet)
    {
        Av1TransformSize transformSize = (Av1TransformSize)txSize;
        Av1TransformSetType transformSetType = Av1SymbolContextHelper.GetExtendedTransformSetType(
            transformSize,
            isInter: true,
            useReducedTransformSet);

        int extendedSet = Av1SymbolContextHelper.GetExtendedTransformSet(transformSetType, isInter: true);
        Av1TransformSize squareTransformSize = transformSize.GetSquareSize();
        Av1Distribution expectedDistribution =
            Av1DefaultDistributions.InterExtendedTransform[extendedSet][(int)squareTransformSize];

        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder costEncoder = new(configuration, BaseQIndex, updateCdf: false);
        using Av1SymbolEncoder encoder = new(configuration, BaseQIndex, updateCdf: true);
        Span<byte> output = encoder.GetTileBuffer();
        int transformTypeCount = Av1SymbolContextHelper.GetExtendedTransformTypeCount(transformSetType);

        for (int symbol = 0; symbol < transformTypeCount; symbol++)
        {
            Av1TransformType transformType = Av1SymbolContextHelper.GetExtendedTransformType(transformSetType, symbol);
            int expectedCost = Av1ProbabilityCost.GetSymbolCost(expectedDistribution, symbol);
            int actualCost = costEncoder.GetTransformTypeCost(
                transformType,
                transformSize,
                useReducedTransformSet,
                lossless: false,
                Av1FilterIntraMode.AllFilterIntraModes,
                Av1PredictionMode.DC,
                usesInterTransformSet: true);

            Assert.Equal(expectedCost, actualCost);
            encoder.WriteTransformType<Av1SymbolEncoder.SymbolWriteOperation>(
                ref output,
                transformType,
                transformSize,
                useReducedTransformSet,
                BaseQIndex,
                Av1FilterIntraMode.AllFilterIntraModes,
                Av1PredictionMode.DC,
                usesInterTransformSet: true);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();
        Av1SymbolDecoder decoder = new(configuration, encoded.GetSpan(), BaseQIndex, updateCdf: true);
        for (int symbol = 0; symbol < transformTypeCount; symbol++)
        {
            Av1TransformType expected = Av1SymbolContextHelper.GetExtendedTransformType(transformSetType, symbol);
            Av1TransformType actual = decoder.ReadTransformType(
                transformSize,
                useReducedTransformSet,
                isInter: true,
                useFilterIntra: false,
                hasSegmentQuantizer: true,
                Av1FilterIntraMode.AllFilterIntraModes,
                Av1PredictionMode.DC);

            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void RoundTripGolomb()
    {
        // Assign
        Configuration configuration = Configuration.Default;
        int[] values = Enumerable.Range(0, 16384).ToArray();
        int[] actuals = new int[values.Length];

        using Av1SymbolEncoder encoder = new(configuration, BaseQIndex, updateCdf: true);
        Span<byte> output = encoder.GetTileBuffer();

        // Act
        foreach (int value in values)
        {
            encoder.WriteGolomb<Av1SymbolEncoder.SymbolWriteOperation>(ref output, value);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex, updateCdf: true);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadGolomb();
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Fact]
    public void RoundTripSegmentId()
    {
        for (int context = 0; context < 3; context++)
        {
            RoundTripSegmentIdCase(context);
        }
    }

    private static void RoundTripSegmentIdCase(int context)
    {
        // Assign
        int[] values = [3, 6, 7, 0, 2, 0, 2, 1, 1];
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, BaseQIndex, updateCdf: true);
        Span<byte> output = encoder.GetTileBuffer();
        int[] actuals = new int[values.Length];

        // Act
        foreach (int value in values)
        {
            encoder.WriteSegmentId<Av1SymbolEncoder.SymbolWriteOperation>(ref output, value, context);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex, updateCdf: true);
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
        using Av1SymbolEncoder encoder = new(configuration, BaseQIndex, updateCdf: true);
        Span<byte> output = encoder.GetTileBuffer();
        int[] actuals = new int[values.Length];

        // Act
        foreach (int value in values)
        {
            encoder.WriteDeltaQuantizerIndex<Av1SymbolEncoder.SymbolWriteOperation>(ref output, value);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex, updateCdf: true);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadDeltaQuantizerIndex();
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Fact]
    public void RoundTripFilterIntraMode()
    {
        foreach (ITheoryDataRow row in GetRangeData((int)Av1BlockSize.AllSizes))
        {
            object[] values = row.GetData();
            RoundTripFilterIntraModeCase((int)values[0]);
        }
    }

    private static void RoundTripFilterIntraModeCase(int bSize)
    {
        // Assign
        Av1BlockSize blockSize = (Av1BlockSize)bSize;
        Av1FilterIntraMode[] values = [
            Av1FilterIntraMode.DC, Av1FilterIntraMode.Vertical, Av1FilterIntraMode.DC, Av1FilterIntraMode.Paeth,
            Av1FilterIntraMode.AllFilterIntraModes, Av1FilterIntraMode.Directional157, Av1FilterIntraMode.DC, Av1FilterIntraMode.Directional157];

        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, BaseQIndex, updateCdf: true);
        Span<byte> output = encoder.GetTileBuffer();
        Av1FilterIntraMode[] actuals = new Av1FilterIntraMode[values.Length];

        // Act
        foreach (Av1FilterIntraMode value in values)
        {
            encoder.WriteFilterIntraMode<Av1SymbolEncoder.SymbolWriteOperation>(ref output, value, blockSize);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex, updateCdf: true);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadFilterUltraMode(blockSize);
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
            if (transformSize == Av1TransformSize.Size16x16)
            {
                for (Av1PredictionMode intraDirection = Av1PredictionMode.IntraModeStart; intraDirection < Av1PredictionMode.IntraModeEnd; intraDirection++)
                {
                    result.Add((int)transformSize, (int)Av1FilterIntraMode.AllFilterIntraModes, (int)intraDirection);
                }

                if (transformSize == Av1TransformSize.Size16x16)
                {
                    result.Add((int)transformSize, 0, 0);
                    result.Add((int)transformSize, 1, 1);
                    result.Add((int)transformSize, 2, 2);
                    result.Add((int)transformSize, 3, 6);
                    result.Add((int)transformSize, 4, 0);
                }

                continue;
            }

            if (transformSize.GetSquareSize() >= Av1TransformSize.Size16x16 || transformSize is Av1TransformSize.Size32x8 or Av1TransformSize.Size8x32)
            {
                // DctOnly, doesn't make sense to test.
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

    public static TheoryData<int, bool> GetInterTransformTypeData()
    {
        TheoryData<int, bool> result = [];
        for (Av1TransformSize transformSize = Av1TransformSize.Size4x4;
            transformSize <= Av1TransformSize.Size32x32;
            transformSize++)
        {
            result.Add((int)transformSize, false);
            result.Add((int)transformSize, true);
        }

        return result;
    }
}
