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

    // Short syntax round trips encode only their small in-method symbol vectors.
    private const int ShortSyntaxBufferLength = 64;

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

        using Av1SymbolWriter writer = new(Configuration.Default, 512, updateCdf);
        for (int index = 0; index < 96; index++)
        {
            int delta = deltas[index % deltas.Length];
            int magnitude = Math.Abs(delta);
            writer.WriteSymbol(Math.Min(magnitude, 3), channels[index % channelCount]);
            if (magnitude >= 3)
            {
                // The escape magnitude is 2^bits + 1 plus the transmitted remainder, followed by its sign.
                int bits = BitOperations.Log2((uint)(magnitude - 1));
                writer.WriteLiteral((uint)(bits - 1), 3);
                writer.WriteLiteral((uint)(magnitude - (1 << bits) - 1), bits);
            }

            if (magnitude != 0)
            {
                writer.WriteLiteral(delta < 0 ? 1U : 0U, 1);
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
        using Av1SymbolWriter writer = new(Configuration.Default, ShortSyntaxBufferLength, updateCdf: false);
        writer.WriteBoolean(false, 16_384);
        writer.WriteBoolean(false, 16_384);
        writer.WriteBoolean(true, 512);
        writer.WriteBoolean(false, 8_192);
        using IMemoryOwner<byte> encoded = writer.Exit();

        Assert.Equal(2, encoded.Memory.Length);
        Assert.Equal(63, encoded.Memory.Span[0]);
    }

    [Fact]
    public void SymbolWriterRentsFixedOutputBuffer()
    {
        const int bufferLength = 257;
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        TestMemoryAllocator.AllocationRequest allocation;

        using (Av1SymbolWriter writer = new(configuration, bufferLength, updateCdf: false))
        {
            writer.WriteLiteral(false);
            allocation = Assert.Single(allocator.AllocationLog);

            Assert.Equal(typeof(byte), allocation.ElementType);
            Assert.Equal(bufferLength, allocation.Length);
        }

        TestMemoryAllocator.ReturnRequest returned = Assert.Single(allocator.ReturnLog);
        Assert.Equal(allocation.HashCodeOfBuffer, returned.HashCodeOfBuffer);
    }

    [Fact]
    public void RawBytesFromWriteLiteral4Bits()
    {
        this.RawBytesFromWriteLiteral4BitsCase((uint)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)128);
        this.RawBytesFromWriteLiteral4BitsCase((uint)1, (byte)17, (byte)68, (byte)34, (byte)34, (byte)128);
        this.RawBytesFromWriteLiteral4BitsCase((uint)2, (byte)34, (byte)86, (byte)68, (byte)68, (byte)128);
        this.RawBytesFromWriteLiteral4BitsCase((uint)3, (byte)51, (byte)104, (byte)102, (byte)102, (byte)128);
        this.RawBytesFromWriteLiteral4BitsCase((uint)4, (byte)68, (byte)118, (byte)34, (byte)34, (byte)64);
        this.RawBytesFromWriteLiteral4BitsCase((uint)5, (byte)85, (byte)118, (byte)170, (byte)170, (byte)192);
        this.RawBytesFromWriteLiteral4BitsCase((uint)6, (byte)102, (byte)119, (byte)51, (byte)51, (byte)64);
        this.RawBytesFromWriteLiteral4BitsCase((uint)7, (byte)119, (byte)119, (byte)187, (byte)187, (byte)192);
        this.RawBytesFromWriteLiteral4BitsCase((uint)8, (byte)136, (byte)129, (byte)17, (byte)17, (byte)128);
        this.RawBytesFromWriteLiteral4BitsCase((uint)9, (byte)153, (byte)147, (byte)51, (byte)51, (byte)128);
        this.RawBytesFromWriteLiteral4BitsCase((uint)10, (byte)170, (byte)165, (byte)85, (byte)85, (byte)128);
        this.RawBytesFromWriteLiteral4BitsCase((uint)11, (byte)187, (byte)183, (byte)119, (byte)119, (byte)128);
        this.RawBytesFromWriteLiteral4BitsCase((uint)12, (byte)204, (byte)201, (byte)153, (byte)153, (byte)128);
        this.RawBytesFromWriteLiteral4BitsCase((uint)13, (byte)221, (byte)219, (byte)187, (byte)187, (byte)128);
        this.RawBytesFromWriteLiteral4BitsCase((uint)14, (byte)238, (byte)237, (byte)221, (byte)221, (byte)128);
        this.RawBytesFromWriteLiteral4BitsCase((uint)15, (byte)255, (byte)255, (byte)255, (byte)255, (byte)128);
    }

    private void RawBytesFromWriteLiteral4BitsCase(uint value, byte exp0, byte exp1, byte exp2, byte exp3, byte exp4)
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
        using Av1SymbolWriter writer = new(configuration, ShortSyntaxBufferLength, updateCdf: true);

        // Act
        for (int i = 0; i < writeCount; i++)
        {
            writer.WriteLiteral(value, bitCount);
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
        using Av1SymbolEncoder encoder = new(configuration, 512, BaseQIndex, updateCdf: true);
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

                encoder.WritePaletteColorMap(
                    paletteSize,
                    (Av1PlaneType)plane,
                    Rows,
                    Columns,
                    sourceRegion);
            }
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();
        Av1SymbolDecoder decoder = new(configuration, encoded.GetSpan(), BaseQIndex);
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
            object?[] values = row.GetData();
            this.RoundTripPartitionTypeCase((int)values[0]!);
        }

        foreach (ITheoryDataRow row in GetSplitPartitionTypeData())
        {
            object?[] values = row.GetData();
            this.RoundTripSplitOrHorizontalPartitionTypeCase((int)values[0]!, (int)values[1]!);
            this.RoundTripSplitOrVerticalPartitionTypeCase((int)values[0]!, (int)values[1]!);
        }
    }

    private void RoundTripPartitionTypeCase(int context)
    {
        // Assign
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, ShortSyntaxBufferLength, BaseQIndex, updateCdf: true);
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

    private void RoundTripSplitOrHorizontalPartitionTypeCase(int size, int context)
    {
        // Assign
        Av1BlockSize blockSize = (Av1BlockSize)size;
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, ShortSyntaxBufferLength, BaseQIndex, updateCdf: true);
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

    private void RoundTripSplitOrVerticalPartitionTypeCase(int size, int context)
    {
        // Assign
        Av1BlockSize blockSize = (Av1BlockSize)size;
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, ShortSyntaxBufferLength, BaseQIndex, updateCdf: true);
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

    [Fact]
    public void RoundTripSkip()
    {
        for (int context = 0; context < 3; context++)
        {
            this.RoundTripSkipCase(context);
        }

        foreach (ITheoryDataRow row in GetTransformBlockSkipData())
        {
            object?[] values = row.GetData();
            this.RoundTripTransformBlockSkipCase((int)values[0]!, (int)values[1]!);
        }
    }

    private void RoundTripSkipCase(int context)
    {
        // Assign
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, ShortSyntaxBufferLength, BaseQIndex, updateCdf: true);
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

    private void RoundTripTransformBlockSkipCase(int transformContext, int skipContext)
    {
        // Assign
        Av1TransformSize transformSizeContext = (Av1TransformSize)transformContext;
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, ShortSyntaxBufferLength, BaseQIndex, updateCdf: true);
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

    [Fact]
    public void RoundTripTransformType()
    {
        foreach (ITheoryDataRow row in GetTransformTypeData())
        {
            object?[] values = row.GetData();
            this.RoundTripTransformTypeCase((int)values[0]!, (int)values[1]!, (int)values[2]!);
        }

        foreach (ITheoryDataRow row in GetInterTransformTypeData())
        {
            object?[] values = row.GetData();
            this.RoundTripInterTransformTypeCase((int)values[0]!, (bool)values[1]!);
        }
    }

    private void RoundTripTransformTypeCase(int txSizeContext, int intraMode, int intraDir)
    {
        // Assign
        Av1TransformSize transformSizeContext = (Av1TransformSize)txSizeContext;
        Av1FilterIntraMode filterIntraMode = (Av1FilterIntraMode)intraMode;
        Av1PredictionMode intraDirection = (Av1PredictionMode)intraDir;
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, ShortSyntaxBufferLength, BaseQIndex, updateCdf: true);

        // TODO: Include AdstFlipAdst, which is currently mapped to Identity.
        Av1TransformType[] values = [
            Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.Identity, Av1TransformType.AdstDct,
            Av1TransformType.DctDct, Av1TransformType.AdstAdst, Av1TransformType.Identity, Av1TransformType.DctAdst
        ];

        Av1TransformType[] actuals = new Av1TransformType[values.Length];

        // Act
        foreach (Av1TransformType value in values)
        {
            encoder.WriteTransformType(value, transformSizeContext, true, BaseQIndex, filterIntraMode, intraDirection, usesInterTransformSet: false);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadTransformType(transformSizeContext, true, false, false, false, filterIntraMode, intraDirection);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    private void RoundTripInterTransformTypeCase(int txSize, bool useReducedTransformSet)
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
        using Av1SymbolEncoder costEncoder = new(configuration, ShortSyntaxBufferLength, BaseQIndex, updateCdf: false);
        using Av1SymbolEncoder encoder = new(configuration, ShortSyntaxBufferLength, BaseQIndex, updateCdf: true);
        int transformTypeCount = Av1SymbolContextHelper.GetExtendedTransformTypeCount(transformSetType);

        for (int symbol = 0; symbol < transformTypeCount; symbol++)
        {
            Av1TransformType transformType = Av1SymbolContextHelper.GetExtendedTransformType(transformSetType, symbol);
            int expectedCost = Av1ProbabilityCost.GetSymbolCost(expectedDistribution, symbol);
            int actualCost = costEncoder.GetTransformTypeCost(
                transformType,
                transformSize,
                useReducedTransformSet,
                BaseQIndex,
                Av1FilterIntraMode.AllFilterIntraModes,
                Av1PredictionMode.DC,
                usesInterTransformSet: true);

            Assert.Equal(expectedCost, actualCost);
            encoder.WriteTransformType(
                transformType,
                transformSize,
                useReducedTransformSet,
                BaseQIndex,
                Av1FilterIntraMode.AllFilterIntraModes,
                Av1PredictionMode.DC,
                usesInterTransformSet: true);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();
        Av1SymbolDecoder decoder = new(configuration, encoded.GetSpan(), BaseQIndex);
        for (int symbol = 0; symbol < transformTypeCount; symbol++)
        {
            Av1TransformType expected = Av1SymbolContextHelper.GetExtendedTransformType(transformSetType, symbol);
            Av1TransformType actual = decoder.ReadTransformType(
                transformSize,
                useReducedTransformSet,
                isInter: true,
                useFilterIntra: false,
                isLossless: false,
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

        // Reserve the longest code for every value so this broad corpus cannot exhaust the fixed entropy output.
        int maximumCodeBitCount = (BitOperations.Log2((uint)values.Length) * 2) + 1;
        int bufferLength = (int)Numerics.DivideCeil((uint)(values.Length * maximumCodeBitCount), 8);
        using Av1SymbolEncoder encoder = new(configuration, bufferLength, BaseQIndex, updateCdf: true);

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

    [Fact]
    public void RoundTripSegmentId()
    {
        for (int context = 0; context < 3; context++)
        {
            this.RoundTripSegmentIdCase(context);
        }
    }

    private void RoundTripSegmentIdCase(int context)
    {
        // Assign
        int[] values = [3, 6, 7, 0, 2, 0, 2, 1, 1];
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, ShortSyntaxBufferLength, BaseQIndex, updateCdf: true);
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
        using Av1SymbolEncoder encoder = new(configuration, ShortSyntaxBufferLength, BaseQIndex, updateCdf: true);
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

    [Fact]
    public void RoundTripFilterIntraMode()
    {
        foreach (ITheoryDataRow row in GetRangeData((int)Av1BlockSize.AllSizes))
        {
            object?[] values = row.GetData();
            this.RoundTripFilterIntraModeCase((int)values[0]!);
        }
    }

    private void RoundTripFilterIntraModeCase(int bSize)
    {
        // Assign
        Av1BlockSize blockSize = (Av1BlockSize)bSize;
        Av1FilterIntraMode[] values = [
            Av1FilterIntraMode.DC, Av1FilterIntraMode.Vertical, Av1FilterIntraMode.DC, Av1FilterIntraMode.Paeth,
            Av1FilterIntraMode.AllFilterIntraModes, Av1FilterIntraMode.Directional157, Av1FilterIntraMode.DC, Av1FilterIntraMode.Directional157];

        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, ShortSyntaxBufferLength, BaseQIndex, updateCdf: true);
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
