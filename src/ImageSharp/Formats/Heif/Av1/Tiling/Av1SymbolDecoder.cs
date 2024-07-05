// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Symbol;

internal ref struct Av1SymbolDecoder
{
    private static readonly int[] IntraModeContext = [0, 1, 2, 3, 4, 4, 4, 4, 3, 0, 1, 2, 0];

    private readonly Av1Distribution tileIntraBlockCopy = Av1DefaultDistributions.IntraBlockCopy;
    private readonly Av1Distribution[] tilePartitionTypes = Av1DefaultDistributions.PartitionTypes;
    private readonly Av1Distribution[][] keyFrameYMode = Av1DefaultDistributions.KeyFrameYMode;
    private readonly Av1Distribution[][] uvMode = Av1DefaultDistributions.UvMode;
    private readonly Av1Distribution[] skip = Av1DefaultDistributions.Skip;
    private readonly Av1Distribution deltaLoopFilterAbsolute = Av1DefaultDistributions.DeltaLoopFilterAbsolute;
    private readonly Av1Distribution deltaQuantizerAbsolute = Av1DefaultDistributions.DeltaQuantizerAbsolute;
    private readonly Av1Distribution[] segmentId = Av1DefaultDistributions.SegmentId;
    private readonly Av1Distribution[] angleDelta = Av1DefaultDistributions.AngleDelta;
    private readonly Av1Distribution filterIntraMode = Av1DefaultDistributions.FilterIntraMode;
    private readonly Av1Distribution[] filterIntra = Av1DefaultDistributions.FilterIntra;
    private readonly Av1Distribution[][] transformSize = Av1DefaultDistributions.TransformSize;
    private readonly Av1Distribution[][][] endOfBlockFlag;
    private Av1SymbolReader reader;

    public Av1SymbolDecoder(Span<byte> tileData, int qIndex)
    {
        this.reader = new Av1SymbolReader(tileData);
        this.endOfBlockFlag = Av1DefaultDistributions.GetEndOfBlockFlag(qIndex);
    }

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

    public Av1PredictionMode ReadYMode(Av1BlockModeInfo? aboveModeInfo, Av1BlockModeInfo? leftModeInfo)
    {
        ref Av1SymbolReader r = ref this.reader;
        Av1PredictionMode aboveMode = Av1PredictionMode.DC;
        if (aboveModeInfo != null)
        {
            aboveMode = aboveModeInfo.YMode;
        }

        Av1PredictionMode leftMode = Av1PredictionMode.DC;
        if (leftModeInfo != null)
        {
            leftMode = leftModeInfo.YMode;
        }

        int aboveContext = IntraModeContext[(int)aboveMode];
        int leftContext = IntraModeContext[(int)leftMode];
        return (Av1PredictionMode)r.ReadSymbol(this.keyFrameYMode[aboveContext][leftContext]);
    }

    public Av1PredictionMode ReadIntraModeUv(Av1PredictionMode mode, bool chromaFromLumaAllowed)
    {
        int chromaForLumaIndex = chromaFromLumaAllowed ? 1 : 0;
        ref Av1SymbolReader r = ref this.reader;
        return (Av1PredictionMode)r.ReadSymbol(this.uvMode[chromaForLumaIndex][(int)mode]);
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

    public bool ReadUseFilterUltra(Av1BlockSize blockSize)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.filterIntra[(int)blockSize]) > 0;
    }

    public Av1FilterIntraMode ReadFilterUltraMode()
    {
        ref Av1SymbolReader r = ref this.reader;
        return (Av1FilterIntraMode)r.ReadSymbol(this.filterIntraMode);
    }

    public Av1TransformSize ReadTransformSize(Av1BlockSize blockSize, int context)
    {
        ref Av1SymbolReader r = ref this.reader;
        Av1TransformSize maxTransformSize = blockSize.GetMaximumTransformSize();
        int depth = 0;
        while (maxTransformSize != Av1TransformSize.Size4x4)
        {
            depth++;
            maxTransformSize = maxTransformSize.GetSubSize();
            DebugGuard.MustBeLessThan(depth, 10, nameof(depth));
        }

        DebugGuard.MustBeLessThanOrEqualTo(depth, Av1Constants.MaxTransformCategories, nameof(depth));
        int category = depth - 1;
        int value = r.ReadSymbol(this.transformSize[category][context]);
        Av1TransformSize transformSize = blockSize.GetMaximumTransformSize();
        for (int d = 0; d < value; ++d)
        {
            transformSize = transformSize.GetSubSize();
        }

        return transformSize;
    }

    public int ReadEndOfBlockFlag(Av1PlaneType planeType, Av1TransformClass transformClass, Av1TransformSize transformSize)
    {
        int endOfBlockContext = transformClass == Av1TransformClass.Class2D ? 0 : 1;
        int endOfBlockMultiSize = transformSize.GetLog2Minus4();
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.endOfBlockFlag[endOfBlockMultiSize][(int)planeType][endOfBlockContext]) + 1;
    }

    public bool ReadTransformBlockSkip(Av1TransformSize transformSizeContext, int skipContext) => throw new NotImplementedException();

    public bool ReadEndOfBlockExtra(Av1TransformSize transformSizeContext, Av1PlaneType planeType, int endOfBlockContext) => throw new NotImplementedException();

    public int ReadBaseRange(Av1TransformSize transformSizeContext, Av1PlaneType planeType, int baseRangeContext) => throw new NotImplementedException();

    public int ReadDcSign(Av1PlaneType planeType, int dcSignContext) => throw new NotImplementedException();

    public int ReadBaseEndOfBlock(Av1TransformSize transformSizeContext, Av1PlaneType planeType, int coefficientContext) => throw new NotImplementedException();

    public int ReadBase(int coeff_ctx, Av1TransformSize transformSizeContext, Av1PlaneType planeType) => throw new NotImplementedException();

    private static uint GetElementProbability(Av1Distribution probability, Av1PartitionType element)
        => probability[(int)element - 1] - probability[(int)element];
}
