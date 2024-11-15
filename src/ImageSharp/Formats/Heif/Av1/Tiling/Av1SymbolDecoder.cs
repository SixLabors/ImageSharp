// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal ref struct Av1SymbolDecoder
{
    private static readonly int[] IntraModeContext = [0, 1, 2, 3, 4, 4, 4, 4, 3, 0, 1, 2, 0];
    private static readonly int[] AlphaVContexts = [-1, 0, 3, -1, 1, 4, -1, 2, 5];

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
    private readonly Av1Distribution[][][] coefficientsBase;
    private readonly Av1Distribution[][][] baseEndOfBlock;
    private readonly Av1Distribution[][] dcSign;
    private readonly Av1Distribution[][][] coefficientsBaseRange;
    private readonly Av1Distribution[][] transformBlockSkip;
    private readonly Av1Distribution[][][] endOfBlockExtra;
    private readonly Av1Distribution chromeForLumaSign = Av1DefaultDistributions.ChromeForLumaSign;
    private readonly Av1Distribution[] chromeForLumaAlpha = Av1DefaultDistributions.ChromeForLumaAlpha;
    private Av1SymbolReader reader;

    public Av1SymbolDecoder(Span<byte> tileData, int qIndex)
    {
        this.reader = new Av1SymbolReader(tileData);
        this.endOfBlockFlag = Av1DefaultDistributions.GetEndOfBlockFlag(qIndex);
        this.coefficientsBase = Av1DefaultDistributions.GetCoefficientsBase(qIndex);
        this.baseEndOfBlock = Av1DefaultDistributions.GetBaseEndOfBlock(qIndex);
        this.dcSign = Av1DefaultDistributions.GetDcSign(qIndex);
        this.coefficientsBaseRange = Av1DefaultDistributions.GetCoefficientsBaseRange(qIndex);
        this.transformBlockSkip = Av1DefaultDistributions.GetTransformBlockSkip(qIndex);
        this.endOfBlockExtra = Av1DefaultDistributions.GetEndOfBlockExtra(qIndex);
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

    /// <summary>
    /// SVT: partition_gather_vert_alike
    /// </summary>
    public Av1PartitionType ReadSplitOrHorizontal(Av1BlockSize blockSize, int context)
    {
        Av1Distribution distribution = GetSplitOrHorizontalDistribution(this.tilePartitionTypes, blockSize, context);
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(distribution) > 0 ? Av1PartitionType.Split : Av1PartitionType.Horizontal;
    }

    /// <summary>
    /// SVT: partition_gather_horz_alike
    /// </summary>
    public Av1PartitionType ReadSplitOrVertical(Av1BlockSize blockSize, int context)
    {
        Av1Distribution distribution = GetSplitOrVerticalDistribution(this.tilePartitionTypes, blockSize, context);
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(distribution) > 0 ? Av1PartitionType.Split : Av1PartitionType.Vertical;
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

    public bool ReadTransformBlockSkip(Av1TransformSize transformSizeContext, int skipContext)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.transformBlockSkip[(int)transformSizeContext][skipContext]) > 0;
    }

    public bool ReadEndOfBlockExtra(Av1TransformSize transformSizeContext, Av1PlaneType planeType, int endOfBlockContext)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.endOfBlockExtra[(int)transformSizeContext][(int)planeType][endOfBlockContext]) > 0;
    }

    public int ReadCoefficientsBaseRange(Av1TransformSize transformSizeContext, Av1PlaneType planeType, int baseRangeContext)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.coefficientsBaseRange[(int)transformSizeContext][(int)planeType][baseRangeContext]);
    }

    public int ReadDcSign(Av1PlaneType planeType, int dcSignContext)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.dcSign[(int)planeType][dcSignContext]);
    }

    public int ReadBaseEndOfBlock(Av1TransformSize transformSizeContext, Av1PlaneType planeType, int coefficientContext)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.baseEndOfBlock[(int)transformSizeContext][(int)planeType][coefficientContext]);
    }

    public int ReadCoefficientsBase(int coefficientContext, Av1TransformSize transformSizeContext, Av1PlaneType planeType)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.coefficientsBase[(int)transformSizeContext][(int)planeType][coefficientContext]);
    }

    public int ReadChromFromLumaSign()
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.chromeForLumaSign);
    }

    public int ReadChromaFromLumaAlphaU(int jointSignPlus1)
    {
        ref Av1SymbolReader r = ref this.reader;
        int context = jointSignPlus1 - 3;
        return r.ReadSymbol(this.chromeForLumaAlpha[context]);
    }

    public int ReadChromaFromLumaAlphaV(int jointSignPlus1)
    {
        ref Av1SymbolReader r = ref this.reader;
        int context = AlphaVContexts[jointSignPlus1];
        return r.ReadSymbol(this.chromeForLumaAlpha[context]);
    }

    internal static Av1Distribution GetSplitOrHorizontalDistribution(Av1Distribution[] inputs, Av1BlockSize blockSize, int context)
    {
        Av1Distribution input = inputs[context];
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

        return new(Av1Distribution.ProbabilityTop - p);
    }

    internal static Av1Distribution GetSplitOrVerticalDistribution(Av1Distribution[] inputs, Av1BlockSize blockSize, int context)
    {
        Av1Distribution input = inputs[context];
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

        return new(Av1Distribution.ProbabilityTop - p);
    }

    private static uint GetElementProbability(Av1Distribution probability, Av1PartitionType element)
        => probability[(int)element - 1] - probability[(int)element];
}
