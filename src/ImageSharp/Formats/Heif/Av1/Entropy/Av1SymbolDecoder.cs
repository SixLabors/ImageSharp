// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

internal ref struct Av1SymbolDecoder
{
    private static readonly int[] IntraModeContext = [0, 1, 2, 3, 4, 4, 4, 4, 3, 0, 1, 2, 0];
    private static readonly int[] AlphaVContexts = [-1, 0, 3, -1, 1, 4, -1, 2, 5];
    public static readonly int[] EndOfBlockOffsetBits = [0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9];
    public static readonly int[] EndOfBlockGroupStart = [0, 1, 2, 3, 5, 9, 17, 33, 65, 129, 257, 513];

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
        return r.ReadSymbol(this.angleDelta[(int)mode - 1]);
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

    public bool ReadTransformBlockSkip(Av1TransformSize transformSizeContext, int skipContext)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.transformBlockSkip[(int)transformSizeContext][skipContext]) > 0;
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

    /// <summary>
    /// 5.11.39. Coefficients syntax.
    /// </summary>
    /// <remarks>
    /// The implementation is taken from SVT-AV1 library, which deviates from the code flow in the specification.
    /// </remarks>
    public int ReadCoefficients(
        Av1BlockModeInfo modeInfo,
        Point blockPosition,
        int[] aboveContexts,
        int[] leftContexts,
        int aboveOffset,
        int leftOffset,
        int plane,
        int blocksWide,
        int blocksHigh,
        Av1TransformBlockContext transformBlockContext,
        Av1TransformSize transformSize,
        bool isLossless,
        bool useReducedTransformSet,
        Av1TransformInfo transformInfo,
        int modeBlocksToRightEdge,
        int modeBlocksToBottomEdge,
        Span<int> coefficientBuffer)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        Av1TransformSize transformSizeContext = (Av1TransformSize)(((int)transformSize.GetSquareSize() + ((int)transformSize.GetSquareUpSize() + 1)) >> 1);
        Av1PlaneType planeType = (Av1PlaneType)Math.Min(plane, 1);
        int culLevel = 0;

        byte[] levelsBuffer = new byte[Av1Constants.TransformPad2d];
        Span<byte> levels = levelsBuffer.AsSpan()[(Av1Constants.TransformPadTop * (width + Av1Constants.TransformPadHorizontal))..];

        bool allZero = this.ReadTransformBlockSkip(transformSizeContext, transformBlockContext.SkipContext);
        int bwl = transformSize.GetBlockWidthLog2();
        int endOfBlock;
        if (allZero)
        {
            if (plane == 0)
            {
                transformInfo.Type = Av1TransformType.DctDct;
                transformInfo.CodeBlockFlag = false;
            }

            this.UpdateCoefficientContext(modeInfo, aboveContexts, leftContexts, blocksWide, blocksHigh, transformSize, blockPosition, aboveOffset, leftOffset, culLevel, modeBlocksToRightEdge, modeBlocksToBottomEdge);
            return 0;
        }

        transformInfo.Type = ComputeTransformType(planeType, modeInfo, isLossless, transformSize, transformInfo, useReducedTransformSet);
        Av1TransformClass transformClass = transformInfo.Type.ToClass();
        Av1ScanOrder scanOrder = Av1ScanOrderConstants.GetScanOrder(transformSize, transformInfo.Type);
        ReadOnlySpan<short> scan = scanOrder.Scan;

        endOfBlock = this.ReadEndOfBlockPosition(transformSize, transformClass, transformSizeContext, planeType);
        if (endOfBlock > 1)
        {
            Array.Fill(levelsBuffer, (byte)0, 0, ((width + Av1Constants.TransformPadHorizontal) * (height + Av1Constants.TransformPadVertical)) + Av1Constants.TransformPadEnd);
        }

        this.ReadCoefficientsEndOfBlock(transformClass, endOfBlock, height, scan, bwl, levels, transformSizeContext, planeType);
        if (endOfBlock > 1)
        {
            if (transformClass == Av1TransformClass.Class2D)
            {
                this.ReadCoefficientsReverse2d(transformSize, 1, endOfBlock - 1 - 1, scan, bwl, levels, transformSizeContext, planeType);
                this.ReadCoefficientsReverse(transformSize, transformClass, 0, 0, scan, bwl, levels, transformSizeContext, planeType);
            }
            else
            {
                this.ReadCoefficientsReverse(transformSize, transformClass, 0, endOfBlock - 1 - 1, scan, bwl, levels, transformSizeContext, planeType);
            }
        }

        DebugGuard.MustBeGreaterThan(scan.Length, 0, nameof(scan));
        culLevel = this.ReadCoefficientsDc(coefficientBuffer, endOfBlock, scan, bwl, levels, transformBlockContext.DcSignContext, planeType);
        this.UpdateCoefficientContext(modeInfo, aboveContexts, leftContexts, blocksWide, blocksHigh, transformSize, blockPosition, aboveOffset, leftOffset, culLevel, modeBlocksToRightEdge, modeBlocksToBottomEdge);

        transformInfo.CodeBlockFlag = true;
        return endOfBlock;
    }

    public int ReadEndOfBlockPosition(Av1TransformSize transformSize, Av1TransformClass transformClass, Av1TransformSize transformSizeContext, Av1PlaneType planeType)
    {
        int endOfBlockExtra = 0;
        int endOfBlockPoint = this.ReadEndOfBlockFlag(planeType, transformClass, transformSize);
        int endOfBlockShift = EndOfBlockOffsetBits[endOfBlockPoint];
        if (endOfBlockShift > 0)
        {
            int endOfBlockContext = endOfBlockPoint;
            bool bit = this.ReadEndOfBlockExtra(transformSizeContext, planeType, endOfBlockContext);
            if (bit)
            {
                endOfBlockExtra += 1 << endOfBlockShift - 1;
            }
            else
            {
                for (int j = 1; j < endOfBlockShift; j++)
                {
                    if (this.ReadLiteral(1) != 0)
                    {
                        endOfBlockExtra += 1 << endOfBlockShift - 1 - j;
                    }
                }
            }
        }

        return RecordEndOfBlockPosition(endOfBlockPoint, endOfBlockExtra);
    }

    public void ReadCoefficientsEndOfBlock(Av1TransformClass transformClass, int endOfBlock, int height, ReadOnlySpan<short> scan, int bwl, Span<byte> levels, Av1TransformSize transformSizeContext, Av1PlaneType planeType)
    {
        int i = endOfBlock - 1;
        int pos = scan[i];
        int coefficientContext = GetLowerLevelContextEndOfBlock(bwl, height, i);
        int level = this.ReadBaseEndOfBlock(transformSizeContext, planeType, coefficientContext);
        if (level > Av1Constants.BaseLevelsCount)
        {
            int baseRangeContext = GetBaseRangeContextEndOfBlock(pos, bwl, transformClass);
            for (int idx = 0; idx < Av1Constants.CoefficientBaseRange / Av1Constants.BaseRangeSizeMinus1; idx++)
            {
                int coefficinetBaseRange = this.ReadCoefficientsBaseRange(transformSizeContext, planeType, baseRangeContext);
                level += coefficinetBaseRange;
                if (coefficinetBaseRange < Av1Constants.BaseRangeSizeMinus1)
                {
                    break;
                }
            }
        }

        levels[GetPaddedIndex(pos, bwl)] = (byte)level;
    }

    public void ReadCoefficientsReverse2d(Av1TransformSize transformSize, int startScanIndex, int endScanIndex, ReadOnlySpan<short> scan, int bwl, Span<byte> levels, Av1TransformSize transformSizeContext, Av1PlaneType planeType)
    {
        for (int c = endScanIndex; c >= startScanIndex; --c)
        {
            int pos = scan[c];
            int coefficientContext = GetLowerLevelsContext2d(levels, pos, bwl, transformSize);
            int level = this.ReadCoefficientsBase(coefficientContext, transformSizeContext, planeType);
            if (level > Av1Constants.BaseLevelsCount)
            {
                int baseRangeContext = GetBaseRangeContext2d(levels, pos, bwl);
                for (int idx = 0; idx < Av1Constants.CoefficientBaseRange; idx += Av1Constants.BaseRangeSizeMinus1)
                {
                    int k = this.ReadCoefficientsBaseRange(transformSizeContext, planeType, baseRangeContext);
                    level += k;
                    if (k < Av1Constants.BaseRangeSizeMinus1)
                    {
                        break;
                    }
                }
            }

            levels[GetPaddedIndex(pos, bwl)] = (byte)level;
        }
    }

    public void ReadCoefficientsReverse(Av1TransformSize transformSize, Av1TransformClass transformClass, int startScanIndex, int endScanIndex, ReadOnlySpan<short> scan, int bwl, Span<byte> levels, Av1TransformSize transformSizeContext, Av1PlaneType planeType)
    {
        for (int c = endScanIndex; c >= startScanIndex; --c)
        {
            int pos = scan[c];
            int coefficientContext = GetLowerLevelsContext(levels, pos, bwl, transformSize, transformClass);
            int level = this.ReadCoefficientsBase(coefficientContext, transformSizeContext, planeType);
            if (level > Av1Constants.BaseLevelsCount)
            {
                int baseRangeContext = GetBaseRangeContext(levels, pos, bwl, transformClass);
                for (int idx = 0; idx < Av1Constants.CoefficientBaseRange; idx += Av1Constants.BaseRangeSizeMinus1)
                {
                    int k = this.ReadCoefficientsBaseRange(transformSizeContext, planeType, baseRangeContext);
                    level += k;
                    if (k < Av1Constants.BaseRangeSizeMinus1)
                    {
                        break;
                    }
                }
            }

            levels[GetPaddedIndex(pos, bwl)] = (byte)level;
        }
    }

    public int ReadCoefficientsDc(Span<int> coefficientBuffer, int endOfBlock, ReadOnlySpan<short> scan, int bwl, Span<byte> levels, int dcSignContext, Av1PlaneType planeType)
    {
        int maxScanLine = 0;
        int culLevel = 0;
        int dcValue = 0;
        coefficientBuffer[0] = endOfBlock;
        for (int c = 0; c < endOfBlock; c++)
        {
            int sign = 0;
            int level = levels[GetPaddedIndex(scan[c], bwl)];
            if (level != 0)
            {
                maxScanLine = Math.Max(maxScanLine, scan[c]);
                if (c == 0)
                {
                    sign = this.ReadDcSign(planeType, dcSignContext);
                }
                else
                {
                    sign = this.ReadLiteral(1);
                }

                if (level >= Av1Constants.CoefficientBaseRange + Av1Constants.BaseLevelsCount + 1)
                {
                    level += this.ReadGolomb();
                }

                if (c == 0)
                {
                    dcValue = sign != 0 ? -level : level;
                }

                level &= 0xfffff;
                culLevel += level;
            }

            coefficientBuffer[c + 1] = sign != 0 ? -level : level;
        }

        culLevel = Math.Min(Av1Constants.CoefficientContextMask, culLevel);
        SetDcSign(ref culLevel, dcValue);

        return culLevel;
    }

    private int ReadEndOfBlockFlag(Av1PlaneType planeType, Av1TransformClass transformClass, Av1TransformSize transformSize)
    {
        int endOfBlockContext = transformClass == Av1TransformClass.Class2D ? 0 : 1;
        int endOfBlockMultiSize = transformSize.GetLog2Minus4();
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.endOfBlockFlag[endOfBlockMultiSize][(int)planeType][endOfBlockContext]) + 1;
    }

    private bool ReadEndOfBlockExtra(Av1TransformSize transformSizeContext, Av1PlaneType planeType, int endOfBlockContext)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.endOfBlockExtra[(int)transformSizeContext][(int)planeType][endOfBlockContext]) > 0;
    }

    private int ReadCoefficientsBaseRange(Av1TransformSize transformSizeContext, Av1PlaneType planeType, int baseRangeContext)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.coefficientsBaseRange[(int)transformSizeContext][(int)planeType][baseRangeContext]);
    }

    private int ReadDcSign(Av1PlaneType planeType, int dcSignContext)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.dcSign[(int)planeType][dcSignContext]);
    }

    private int ReadBaseEndOfBlock(Av1TransformSize transformSizeContext, Av1PlaneType planeType, int coefficientContext)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.baseEndOfBlock[(int)transformSizeContext][(int)planeType][coefficientContext]);
    }

    private int ReadCoefficientsBase(int coefficientContext, Av1TransformSize transformSizeContext, Av1PlaneType planeType)
    {
        ref Av1SymbolReader r = ref this.reader;
        return r.ReadSymbol(this.coefficientsBase[(int)transformSizeContext][(int)planeType][coefficientContext]);
    }

    private static int RecordEndOfBlockPosition(int endOfBlockPoint, int endOfBlockExtra)
    {
        int endOfBlock = EndOfBlockGroupStart[endOfBlockPoint];
        if (endOfBlock > 2)
        {
            endOfBlock += endOfBlockExtra;
        }

        return endOfBlock;
    }

    private static int GetBaseRangeContextEndOfBlock(int pos, int bwl, Av1TransformClass transformClass)
    {
        int row = pos >> bwl;
        int col = pos - (row << bwl);
        if (pos == 0)
        {
            return 0;
        }

        if (transformClass == Av1TransformClass.Class2D && row < 2 && col < 2 ||
            transformClass == Av1TransformClass.ClassHorizontal && col == 0 ||
            transformClass == Av1TransformClass.ClassVertical && row == 0)
        {
            return 7;
        }

        return 14;
    }

    private static int GetLowerLevelContextEndOfBlock(int bwl, int height, int scanIndex)
    {
        if (scanIndex == 0)
        {
            return 0;
        }

        if (scanIndex <= height << bwl >> 3)
        {
            return 1;
        }

        if (scanIndex <= height << bwl >> 2)
        {
            return 2;
        }

        return 3;
    }

    private static int GetBaseRangeContext2d(Span<byte> levels, int c, int bwl)
    {
        DebugGuard.MustBeGreaterThan(c, 0, nameof(c));
        int row = c >> bwl;
        int col = c - (row << bwl);
        int stride = (1 << bwl) + Av1Constants.TransformPadHorizontal;
        int pos = row * stride + col;
        int mag =
            Math.Min((int)levels[pos + 1], Av1Constants.MaxBaseRange) +
            Math.Min((int)levels[pos + stride], Av1Constants.MaxBaseRange) +
            Math.Min((int)levels[pos + 1 + stride], Av1Constants.MaxBaseRange);
        mag = Math.Min(mag + 1 >> 1, 6);
        if ((row | col) < 2)
        {
            return mag + 7;
        }

        return mag + 14;
    }

    private static int GetLowerLevelsContext2d(Span<byte> levels, int pos, int bwl, Av1TransformSize transformSize)
    {
        DebugGuard.MustBeGreaterThan(pos, 0, nameof(pos));
        int mag;
        levels = levels[GetPaddedIndex(pos, bwl)..];
        mag = Math.Min((int)levels[1], 3); // { 0, 1 }
        mag += Math.Min((int)levels[(1 << bwl) + Av1Constants.TransformPadHorizontal], 3); // { 1, 0 }
        mag += Math.Min((int)levels[(1 << bwl) + Av1Constants.TransformPadHorizontal + 1], 3); // { 1, 1 }
        mag += Math.Min((int)levels[2], 3); // { 0, 2 }
        mag += Math.Min((int)levels[(2 << bwl) + (2 << Av1Constants.TransformPadHorizontalLog2)], 3); // { 2, 0 }

        int ctx = Math.Min(mag + 1 >> 1, 4);
        return ctx + Av1NzMap.GetNzMapContext(transformSize, pos);
    }

    private static int GetBaseRangeContext(Span<byte> levels, int c, int bwl, Av1TransformClass transformClass)
    {
        int row = c >> bwl;
        int col = c - (row << bwl);
        int stride = (1 << bwl) + Av1Constants.TransformPadHorizontal;
        int pos = row * stride + col;
        int mag = levels[pos + 1];
        mag += levels[pos + stride];
        switch (transformClass)
        {
            case Av1TransformClass.Class2D:
                mag += levels[pos + stride + 1];
                mag = Math.Min(mag + 1 >> 1, 6);
                if (c == 0)
                {
                    return mag;
                }

                if (row < 2 && col < 2)
                {
                    return mag + 7;
                }

                break;
            case Av1TransformClass.ClassHorizontal:
                mag += levels[pos + 2];
                mag = Math.Min(mag + 1 >> 1, 6);
                if (c == 0)
                {
                    return mag;
                }

                if (col == 0)
                {
                    return mag + 7;
                }

                break;
            case Av1TransformClass.ClassVertical:
                mag += levels[pos + (stride << 1)];
                mag = Math.Min(mag + 1 >> 1, 6);
                if (c == 0)
                {
                    return mag;
                }

                if (row == 0)
                {
                    return mag + 7;
                }

                break;
            default:
                break;
        }

        return mag + 14;
    }

    private static int GetLowerLevelsContext(ReadOnlySpan<byte> levels, int pos, int bwl, Av1TransformSize transformSize, Av1TransformClass transformClass)
    {
        int stats = Av1NzMap.GetNzMagnitude(levels[GetPaddedIndex(pos, bwl)..], bwl, transformClass);
        return Av1NzMap.GetNzMapContextFromStats(stats, pos, bwl, transformSize, transformClass);
    }

    public static int GetPaddedIndex(int scanIndex, int bwl)
        => scanIndex + (scanIndex >> bwl << Av1Constants.TransformPadHorizontalLog2);

    private int ReadGolomb()
    {
        int x = 1;
        int length = 0;
        int i = 0;

        while (i == 0)
        {
            i = this.ReadLiteral(1);
            ++length;
            if (length > 20)
            {
                // SVT_LOG("Invalid length in read_golomb");
                break;
            }
        }

        for (i = 0; i < length - 1; ++i)
        {
            x <<= 1;
            x += this.ReadLiteral(1);
        }

        return x - 1;
    }

    private static void SetDcSign(ref int culLevel, int dcValue)
    {
        if (dcValue < 0)
        {
            culLevel |= 1 << Av1Constants.CoefficientContextBitCount;
        }
        else if (dcValue > 0)
        {
            culLevel += 2 << Av1Constants.CoefficientContextBitCount;
        }
    }


    private void UpdateCoefficientContext(
        Av1BlockModeInfo modeInfo,
        int[] aboveContexts,
        int[] leftContexts,
        int blocksWide,
        int blocksHigh,
        Av1TransformSize transformSize,
        Point blockPosition,
        int aboveOffset,
        int leftOffset,
        int culLevel,
        int modeBlockToRightEdge,
        int modeBlockToBottomEdge)
    {
        int transformSizeWide = transformSize.Get4x4WideCount();
        int transformSizeHigh = transformSize.Get4x4HighCount();

        if (modeBlockToRightEdge < 0)
        {
            int aboveContextCount = Math.Min(transformSizeWide, blocksWide - aboveOffset);
            Array.Fill(aboveContexts, culLevel, 0, aboveContextCount);
            Array.Fill(aboveContexts, 0, aboveContextCount, transformSizeWide - aboveContextCount);
        }
        else
        {
            Array.Fill(aboveContexts, culLevel, 0, transformSizeWide);
        }

        if (modeBlockToBottomEdge < 0)
        {
            int leftContextCount = Math.Min(transformSizeHigh, blocksHigh - leftOffset);
            Array.Fill(leftContexts, culLevel, 0, leftContextCount);
            Array.Fill(leftContexts, 0, leftContextCount, transformSizeWide - leftContextCount);
        }
        else
        {
            Array.Fill(leftContexts, culLevel, 0, transformSizeHigh);
        }
    }

    private static Av1TransformType ComputeTransformType(Av1PlaneType planeType, Av1BlockModeInfo modeInfo, bool isLossless, Av1TransformSize transformSize, Av1TransformInfo transformInfo, bool useReducedTransformSet)
    {
        Av1TransformType transformType = Av1TransformType.DctDct;
        if (isLossless || transformSize.GetSquareUpSize() > Av1TransformSize.Size32x32)
        {
            transformType = Av1TransformType.DctDct;
        }
        else
        {
            if (planeType == Av1PlaneType.Y)
            {
                transformType = transformInfo.Type;
            }
            else
            {
                // In intra mode, uv planes don't share the same prediction mode as y
                // plane, so the tx_type should not be shared
                transformType = ConvertIntraModeToTransformType(modeInfo, Av1PlaneType.Uv);
            }
        }

        Av1TransformSetType transformSetType = GetExtendedTransformSetType(transformSize, useReducedTransformSet);
        if (!transformType.IsExtendedSetUsed(transformSetType))
        {
            transformType = Av1TransformType.DctDct;
        }

        return transformType;
    }

    private static Av1TransformSetType GetExtendedTransformSetType(Av1TransformSize transformSize, bool useReducedSet)
    {
        Av1TransformSize squareUpSize = transformSize.GetSquareUpSize();

        if (squareUpSize >= Av1TransformSize.Size32x32)
        {
            return Av1TransformSetType.DctOnly;
        }

        if (useReducedSet)
        {
            return Av1TransformSetType.Dtt4Identity;
        }

        Av1TransformSize squareSize = transformSize.GetSquareSize();
        return squareSize == Av1TransformSize.Size16x16 ? Av1TransformSetType.Dtt4Identity : Av1TransformSetType.Dtt4Identity1dDct;
    }

    private static Av1TransformType ConvertIntraModeToTransformType(Av1BlockModeInfo modeInfo, Av1PlaneType planeType)
    {
        Av1PredictionMode mode = (planeType == Av1PlaneType.Y) ? modeInfo.YMode : modeInfo.UvMode;
        if (mode == Av1PredictionMode.UvChromaFromLuma)
        {
            mode = Av1PredictionMode.DC;
        }

        return mode.ToTransformType();
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
