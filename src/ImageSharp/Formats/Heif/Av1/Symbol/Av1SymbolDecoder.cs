// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Symbol;

internal ref struct Av1SymbolDecoder
{
    private readonly Av1Distribution tileIntraBlockCopy = Av1DefaultDistributions.IntraBlockCopy;
    private readonly Av1Distribution[] tilePartitionTypes = Av1DefaultDistributions.PartitionTypes;
    private readonly Av1Distribution[] frameYMode = Av1DefaultDistributions.FrameYMode;
    private readonly Av1Distribution[][] uvMode = Av1DefaultDistributions.UvMode;
    private readonly Av1Distribution[] skip = Av1DefaultDistributions.Skip;
    private readonly Av1Distribution deltaLoopFilterAbsolute = Av1DefaultDistributions.DeltaLoopFilterAbsolute;
    private readonly Av1Distribution deltaQuantizerAbsolute = Av1DefaultDistributions.DeltaQuantizerAbsolute;
    private readonly Av1Distribution[] segmentId = Av1DefaultDistributions.SegmentId;
    private readonly Av1Distribution[] angleDelta = Av1DefaultDistributions.AngleDelta;
    private Av1SymbolReader reader;

    public Av1SymbolDecoder(Span<byte> tileData) => this.reader = new Av1SymbolReader(tileData);

    public int ReadLiteral(int bitCount)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadLiteral(bitCount);
    }

    public bool ReadUseIntraBlockCopy()
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.tileIntraBlockCopy) > 0;
    }

    public Av1PartitionType ReadPartitionType(int context)
    {
        ref Av1SymbolReader r = ref this.reader;
        return (Av1PartitionType)r.ReadSymbol(this.tilePartitionTypes[context]);
    }

    public bool ReadSplitOrHorizontal(Av1BlockSize blockSize, int context)
    {
        Av1Distribution input = this.tilePartitionTypes[context];
        uint p = Av1Distribution.ProbabilityTop;
        p -= GetElementProbability(input, Av1PartitionType.Horizontal);
        p -= GetElementProbability(input, Av1PartitionType.Split);
        p -= GetElementProbability(input, Av1PartitionType.HorizontalA);
        p -= GetElementProbability(input, Av1PartitionType.HorizontalB);
        p -= GetElementProbability(input, Av1PartitionType.VerticalA);
        if (blockSize != Av1BlockSize.Block128x128)
        {
            p -= GetElementProbability(input, Av1PartitionType.Horizontal4);
        }

        Av1Distribution distribution = new(Av1Distribution.ProbabilityTop - p);
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(distribution) > 0;
    }

    public bool ReadSplitOrVertical(Av1BlockSize blockSize, int context)
    {
        Av1Distribution input = this.tilePartitionTypes[context];
        uint p = Av1Distribution.ProbabilityTop;
        p -= GetElementProbability(input, Av1PartitionType.Vertical);
        p -= GetElementProbability(input, Av1PartitionType.Split);
        p -= GetElementProbability(input, Av1PartitionType.HorizontalA);
        p -= GetElementProbability(input, Av1PartitionType.VerticalA);
        p -= GetElementProbability(input, Av1PartitionType.VerticalB);
        if (blockSize != Av1BlockSize.Block128x128)
        {
            p -= GetElementProbability(input, Av1PartitionType.Vertical4);
        }

        Av1Distribution distribution = new(Av1Distribution.ProbabilityTop - p);
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(distribution) > 0;
    }

    public Av1PredictionMode ReadIntraFrameYMode(Av1BlockSize blockSize)
    {
        ref Av1SymbolReader r = ref this.reader;
        return (Av1PredictionMode)r.ReadSymbol(this.frameYMode[(int)blockSize]);
    }

    public Av1PredictionMode ReadUvMode(Av1BlockSize blockSize, bool chromaFromLumaAllowed)
    {
        int chromaForLumaIndex = chromaFromLumaAllowed ? 1 : 0;
        ref Av1SymbolReader r = ref this.reader;
        return (Av1PredictionMode)r.ReadSymbol(this.uvMode[chromaForLumaIndex][(int)blockSize]);
    }

    public bool ReadSkip(int ctx)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.skip[ctx]) > 0;
    }

    public int ReadDeltaLoopFilterAbsolute()
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.deltaLoopFilterAbsolute);
    }

    public int ReadDeltaQuantizerAbsolute()
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.deltaQuantizerAbsolute);
    }

    public int ReadSegmentId(int ctx)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.segmentId[ctx]);
    }

    public int ReadAngleDelta(Av1PredictionMode mode)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.angleDelta[((int)mode) - 1]);
    }

    public bool ReadUseFilterUltra() => throw new NotImplementedException();

    public object ReadFilterUltraMode() => throw new NotImplementedException();

    private static uint GetElementProbability(Av1Distribution probability, Av1PartitionType element)
        => probability[(int)element - 1] - probability[(int)element];
}
