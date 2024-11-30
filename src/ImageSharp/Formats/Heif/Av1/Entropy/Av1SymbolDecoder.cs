// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

internal ref struct Av1SymbolDecoder
{
    private static readonly int[][] ExtendedTransformIndicesInverse = [
        [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        [9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        [9, 0, 3, 1, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        [9, 0, 10, 11, 3, 1, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        [9, 10, 11, 0, 1, 2, 4, 5, 3, 6, 7, 8, 0, 0, 0, 0],
        [9, 10, 11, 12, 13, 14, 15, 0, 1, 2, 4, 5, 3, 6, 7, 8],
    ];

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
    private readonly Av1Distribution[][][] intraExtendedTransform = Av1DefaultDistributions.IntraExtendedTransform;
    private readonly Configuration configuration;
    private Av1SymbolReader reader;
    private readonly int baseQIndex;

    public Av1SymbolDecoder(Configuration configuration, Span<byte> tileData, int qIndex)
    {
        this.configuration = configuration;
        this.reader = new Av1SymbolReader(tileData);
        this.baseQIndex = qIndex;
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

    public Av1TransformType ReadTransformType(
        Av1TransformSize transformSize,
        bool useReducedTransformSet,
        bool useFilterIntra,
        int baseQIndex,
        Av1FilterIntraMode filterIntraMode,
        Av1PredictionMode intraDirection)
    {
        Av1TransformType transformType = Av1TransformType.DctDct;

        /*
        // No need to read transform type if block is skipped.
        if (mbmi.Skip ||
            svt_aom_seg_feature_active(&parse_ctxt->frame_header->segmentation_params, mbmi->segment_id, SEG_LVL_SKIP))
            return;
        */

        // Ignoring INTER blocks here, as these should not end up here.
        // int inter_block = is_inter_block_dec(mbmi);
        Av1TransformSetType transformSetType = Av1SymbolContextHelper.GetExtendedTransformSetType(transformSize, useReducedTransformSet);
        if (Av1SymbolContextHelper.GetExtendedTransformTypeCount(transformSize, useReducedTransformSet) > 1 && baseQIndex > 0)
        {
            int extendedSet = Av1SymbolContextHelper.GetExtendedTransformSet(transformSize, useReducedTransformSet);

            // eset == 0 should correspond to a set with only DCT_DCT and
            // there is no need to read the tx_type
            Guard.IsFalse(extendedSet == 0, nameof(extendedSet), string.Empty);

            Av1TransformSize squareTransformSize = transformSize.GetSquareSize();
            Av1PredictionMode intraMode = useFilterIntra
                ? filterIntraMode.ToIntraDirection()
                : intraDirection;
            ref Av1SymbolReader r = ref this.reader;
            int symbol = r.ReadSymbol(this.intraExtendedTransform[extendedSet][(int)squareTransformSize][(int)intraMode]);
            transformType = (Av1TransformType)ExtendedTransformIndicesInverse[(int)transformSetType][symbol];
        }

        return transformType;
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
    /// SVT: parse_coeffs
    /// </summary>
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
        Av1TransformSize transformSizeContext = Av1SymbolContextHelper.GetTransformSizeContext(transformSize);
        Av1PlaneType planeType = (Av1PlaneType)Math.Min(plane, 1);
        int culLevel = 0;

        Av1LevelBuffer levels = new(this.configuration, new Size(width, height));

        bool allZero = this.ReadTransformBlockSkip(transformSizeContext, transformBlockContext.SkipContext);
        int endOfBlock;
        if (allZero)
        {
            if (plane == 0)
            {
                transformInfo.Type = Av1TransformType.DctDct;
                transformInfo.CodeBlockFlag = false;
            }

            UpdateCoefficientContext(modeInfo, aboveContexts, leftContexts, blocksWide, blocksHigh, transformSize, blockPosition, aboveOffset, leftOffset, culLevel, modeBlocksToRightEdge, modeBlocksToBottomEdge);
            return 0;
        }

        if (plane == (int)Av1Plane.Y)
        {
            this.ReadTransformType(transformSize, useReducedTransformSet, modeInfo.FilterIntraModeInfo.UseFilterIntra, this.baseQIndex, modeInfo.FilterIntraModeInfo.Mode, modeInfo.YMode);
        }

        transformInfo.Type = ComputeTransformType(planeType, modeInfo, isLossless, transformSize, transformInfo, useReducedTransformSet);
        Av1TransformClass transformClass = transformInfo.Type.ToClass();
        Av1ScanOrder scanOrder = Av1ScanOrderConstants.GetScanOrder(transformSize, transformInfo.Type);
        ReadOnlySpan<short> scan = scanOrder.Scan;

        endOfBlock = this.ReadEndOfBlockPosition(transformSize, transformClass, transformSizeContext, planeType);
        if (endOfBlock > 1)
        {
            levels.Clear();
        }

        this.ReadCoefficientsEndOfBlock(transformClass, endOfBlock, scan, levels, transformSizeContext, planeType);
        if (endOfBlock > 1)
        {
            if (transformClass == Av1TransformClass.Class2D)
            {
                this.ReadCoefficientsReverse2d(transformSize, 1, endOfBlock - 1 - 1, scan, levels, transformSizeContext, planeType);
                this.ReadCoefficientsReverse(transformSize, transformClass, 0, 0, scan, levels, transformSizeContext, planeType);
            }
            else
            {
                this.ReadCoefficientsReverse(transformSize, transformClass, 0, endOfBlock - 1 - 1, scan, levels, transformSizeContext, planeType);
            }
        }

        coefficientBuffer[0] = endOfBlock;

        DebugGuard.MustBeGreaterThan(scan.Length, 0, nameof(scan));
        culLevel = this.ReadCoefficientsSign(coefficientBuffer, endOfBlock, scan, levels, transformBlockContext.DcSignContext, planeType);
        UpdateCoefficientContext(modeInfo, aboveContexts, leftContexts, blocksWide, blocksHigh, transformSize, blockPosition, aboveOffset, leftOffset, culLevel, modeBlocksToRightEdge, modeBlocksToBottomEdge);

        transformInfo.CodeBlockFlag = true;
        return endOfBlock;
    }

    public int ReadEndOfBlockPosition(Av1TransformSize transformSize, Av1TransformClass transformClass, Av1TransformSize transformSizeContext, Av1PlaneType planeType)
    {
        int endOfBlockExtra = 0;
        int endOfBlockPoint = this.ReadEndOfBlockFlag(planeType, transformClass, transformSize);
        int endOfBlockShift = Av1SymbolContextHelper.EndOfBlockOffsetBits[endOfBlockPoint];
        if (endOfBlockShift > 0)
        {
            int endOfBlockContext = endOfBlockPoint;
            bool bit = this.ReadEndOfBlockExtra(transformSizeContext, planeType, endOfBlockContext);
            if (bit)
            {
                Av1Math.SetBit(ref endOfBlockExtra, endOfBlockShift - 1);
            }
            else
            {
                for (int j = 1; j < endOfBlockShift; j++)
                {
                    if (this.ReadLiteral(1) != 0)
                    {
                        Av1Math.SetBit(ref endOfBlockExtra, endOfBlockShift - 1 - j);
                    }
                }
            }
        }

        return Av1SymbolContextHelper.RecordEndOfBlockPosition(endOfBlockPoint, endOfBlockExtra);
    }

    public void ReadCoefficientsEndOfBlock(Av1TransformClass transformClass, int endOfBlock, ReadOnlySpan<short> scan, Av1LevelBuffer levels, Av1TransformSize transformSizeContext, Av1PlaneType planeType)
    {
        int i = endOfBlock - 1;
        Point position = levels.GetPosition(scan[i]);
        int coefficientContext = Av1SymbolContextHelper.GetLowerLevelContextEndOfBlock(levels, position);
        int level = this.ReadBaseEndOfBlock(transformSizeContext, planeType, coefficientContext);
        if (level > Av1Constants.BaseLevelsCount)
        {
            int baseRangeContext = Av1SymbolContextHelper.GetBaseRangeContextEndOfBlock(position, transformClass);
            for (int idx = 0; idx < Av1Constants.CoefficientBaseRange; idx += Av1Constants.BaseRangeSizeMinus1)
            {
                int coefficientBaseRange = this.ReadCoefficientsBaseRange(transformSizeContext, planeType, baseRangeContext);
                level += coefficientBaseRange;
                if (coefficientBaseRange < Av1Constants.BaseRangeSizeMinus1)
                {
                    break;
                }
            }
        }

        levels.GetRow(position)[0] = (byte)level;
    }

    public void ReadCoefficientsReverse2d(Av1TransformSize transformSize, int startScanIndex, int endScanIndex, ReadOnlySpan<short> scan, Av1LevelBuffer levels, Av1TransformSize transformSizeContext, Av1PlaneType planeType)
    {
        for (int c = endScanIndex; c >= startScanIndex; --c)
        {
            Point position = levels.GetPosition(scan[c]);
            int coefficientContext = Av1SymbolContextHelper.GetLowerLevelsContext2d(levels, position, transformSize);
            int level = this.ReadCoefficientsBase(coefficientContext, transformSizeContext, planeType);
            if (level > Av1Constants.BaseLevelsCount)
            {
                int baseRangeContext = Av1SymbolContextHelper.GetBaseRangeContext2d(levels, position);
                for (int idx = 0; idx < Av1Constants.CoefficientBaseRange; idx += Av1Constants.BaseRangeSizeMinus1)
                {
                    int coefficientBaseRange = this.ReadCoefficientsBaseRange(transformSizeContext, planeType, baseRangeContext);
                    level += coefficientBaseRange;
                    if (coefficientBaseRange < Av1Constants.BaseRangeSizeMinus1)
                    {
                        break;
                    }
                }
            }

            levels.GetRow(position)[0] = (byte)level;
        }
    }

    public void ReadCoefficientsReverse(Av1TransformSize transformSize, Av1TransformClass transformClass, int startScanIndex, int endScanIndex, ReadOnlySpan<short> scan, Av1LevelBuffer levels, Av1TransformSize transformSizeContext, Av1PlaneType planeType)
    {
        for (int c = endScanIndex; c >= startScanIndex; --c)
        {
            int pos = scan[c];
            Point position = levels.GetPosition(pos);
            int coefficientContext = Av1SymbolContextHelper.GetLowerLevelsContext(levels, position, transformSize, transformClass);
            int level = this.ReadCoefficientsBase(coefficientContext, transformSizeContext, planeType);
            if (level > Av1Constants.BaseLevelsCount)
            {
                int baseRangeContext = Av1SymbolContextHelper.GetBaseRangeContext(levels, position, transformClass);
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

            levels.GetRow(position)[0] = (byte)level;
        }
    }

    public int ReadCoefficientsSign(Span<int> coefficientBuffer, int endOfBlock, ReadOnlySpan<short> scan, Av1LevelBuffer levels, int dcSignContext, Av1PlaneType planeType)
    {
        int maxScanLine = 0;
        int culLevel = 0;
        int dcValue = 0;
        coefficientBuffer[0] = endOfBlock;
        for (int c = 0; c < endOfBlock; c++)
        {
            int sign = 0;
            Point position = levels.GetPosition(scan[c]);
            int level = levels.GetRow(position)[0];
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
        Av1SymbolContextHelper.SetDcSign(ref culLevel, dcValue);

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
        int transformContext = Math.Min((int)transformSizeContext, (int)Av1TransformSize.Size32x32);
        return r.ReadSymbol(this.coefficientsBaseRange[transformContext][(int)planeType][baseRangeContext]);
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

    internal int ReadGolomb()
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

    private static void UpdateCoefficientContext(
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
                transformType = Av1SymbolContextHelper.ConvertIntraModeToTransformType(modeInfo, Av1PlaneType.Uv);
            }
        }

        Av1TransformSetType transformSetType = Av1SymbolContextHelper.GetExtendedTransformSetType(transformSize, useReducedTransformSet);
        if (!transformType.IsExtendedSetUsed(transformSetType))
        {
            transformType = Av1TransformType.DctDct;
        }

        return transformType;
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
