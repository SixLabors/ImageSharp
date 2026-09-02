// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <summary>
/// Encodes AV1 tile syntax elements and transform coefficients with tile-local adaptive distributions.
/// </summary>
internal class Av1SymbolEncoder : IDisposable
{
    /// <summary>
    /// The largest coefficient-context plane required after AV1 removes the uncoded half of 64-point transforms.
    /// </summary>
    private const int MaximumCoefficientContextCount = (Av1Constants.MaxTransformSize / 2) * (Av1Constants.MaxTransformSize / 2);

    /// <summary>
    /// The tile-adaptive intra-block-copy distribution.
    /// </summary>
    private readonly Av1Distribution tileIntraBlockCopy;

    /// <summary>
    /// The tile-adaptive integer displacement-vector context.
    /// </summary>
    private readonly Av1MotionVectorContext displacementVector = new();

    /// <summary>
    /// The tile-adaptive partition-type distributions.
    /// </summary>
    private readonly Av1Distribution[] tilePartitionTypes;

    /// <summary>
    /// The tile-adaptive key-frame luma-mode distributions.
    /// </summary>
    private readonly Av1Distribution[][] keyFrameYMode;

    /// <summary>
    /// The tile-adaptive chroma intra-mode distributions.
    /// </summary>
    private readonly Av1Distribution[][] uvMode;

    /// <summary>
    /// The tile-adaptive transform-block skip distributions selected for the frame base quantizer.
    /// </summary>
    private readonly Av1Distribution[][] transformBlockSkip;

    /// <summary>
    /// The tile-adaptive end-of-block token distributions selected for the frame base quantizer.
    /// </summary>
    private readonly Av1Distribution[][][] endOfBlockFlag;

    /// <summary>
    /// The tile-adaptive coefficient base-range distributions selected for the frame base quantizer.
    /// </summary>
    private readonly Av1Distribution[][][] coefficientsBaseRange;

    /// <summary>
    /// The tile-adaptive coefficient base-level distributions selected for the frame base quantizer.
    /// </summary>
    private readonly Av1Distribution[][][] coefficientsBase;

    /// <summary>
    /// The tile-adaptive final-nonzero coefficient distributions selected for the frame base quantizer.
    /// </summary>
    private readonly Av1Distribution[][][] coefficientsBaseEndOfBlock;

    /// <summary>
    /// The tile-adaptive filter-intra enable distributions.
    /// </summary>
    private readonly Av1Distribution[] filterIntra;

    /// <summary>
    /// The tile-adaptive filter-intra mode distribution.
    /// </summary>
    private readonly Av1Distribution filterIntraMode;

    /// <summary>
    /// The tile-adaptive absolute quantizer delta distribution.
    /// </summary>
    private readonly Av1Distribution deltaQuantizerAbsolute;

    /// <summary>
    /// The tile-adaptive DC sign distributions selected for the frame base quantizer.
    /// </summary>
    private readonly Av1Distribution[][] dcSign;

    /// <summary>
    /// The tile-adaptive end-of-block extra-bit distributions selected for the frame base quantizer.
    /// </summary>
    private readonly Av1Distribution[][][] endOfBlockExtra;

    /// <summary>
    /// The tile-adaptive intra transform-type distributions.
    /// </summary>
    private readonly Av1Distribution[][][] intraExtendedTransform;

    /// <summary>
    /// The tile-adaptive spatial segment-identifier distributions.
    /// </summary>
    private readonly Av1Distribution[] segmentId;

    /// <summary>
    /// The tile-adaptive directional angle-delta distributions.
    /// </summary>
    private readonly Av1Distribution[] angleDelta;

    /// <summary>
    /// The tile-adaptive transform-skip distributions.
    /// </summary>
    private readonly Av1Distribution[] skip;

    /// <summary>
    /// The tile-adaptive skip-mode distributions.
    /// </summary>
    private readonly Av1Distribution[] skipMode;

    /// <summary>
    /// The tile-adaptive joint chroma-from-luma sign distribution.
    /// </summary>
    private readonly Av1Distribution chromaFromLumaSign;

    /// <summary>
    /// The tile-adaptive chroma-from-luma alpha-magnitude distributions.
    /// </summary>
    private readonly Av1Distribution[] chromaFromLumaAlpha;

    /// <summary>
    /// Indicates whether the range writer has been disposed.
    /// </summary>
    private bool isDisposed;

    /// <summary>
    /// The reusable padded coefficient levels used to derive entropy contexts.
    /// </summary>
    private readonly Av1LevelBuffer levels;

    /// <summary>
    /// The reusable raster-order coefficient contexts for one transform.
    /// </summary>
    private readonly IMemoryOwner<sbyte> coefficientContexts;

    /// <summary>
    /// The range writer producing the current tile payload.
    /// </summary>
    private Av1SymbolWriter writer;

    /// <summary>
    /// The frame base quantizer used to select coefficient probability models.
    /// </summary>
    private readonly int baseQIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1SymbolEncoder"/> class for one AV1 tile.
    /// </summary>
    /// <param name="configuration">The configuration providing output and temporary memory.</param>
    /// <param name="initialSize">The initial output buffer size in bytes.</param>
    /// <param name="qIndex">The frame base quantizer index.</param>
    /// <param name="updateCdf">A value indicating whether encoded symbols adapt their tile distributions.</param>
    public Av1SymbolEncoder(Configuration configuration, int initialSize, int qIndex, bool updateCdf = true)
    {
        // Every default accessor creates independently mutable state. Encoding and decoding therefore begin from
        // equivalent tile-local models without constructing and immediately deep-copying a second object graph.
        this.tileIntraBlockCopy = Av1DefaultDistributions.IntraBlockCopy;
        this.tilePartitionTypes = Av1DefaultDistributions.PartitionTypes;
        this.keyFrameYMode = Av1DefaultDistributions.KeyFrameYMode;
        this.uvMode = Av1DefaultDistributions.UvMode;
        this.filterIntra = Av1DefaultDistributions.FilterIntra;
        this.filterIntraMode = Av1DefaultDistributions.FilterIntraMode;
        this.deltaQuantizerAbsolute = Av1DefaultDistributions.DeltaQuantizerAbsolute;
        this.intraExtendedTransform = Av1DefaultDistributions.IntraExtendedTransform;
        this.segmentId = Av1DefaultDistributions.SegmentId;
        this.angleDelta = Av1DefaultDistributions.AngleDelta;
        this.skip = Av1DefaultDistributions.Skip;
        this.skipMode = Av1DefaultDistributions.SkipMode;
        this.chromaFromLumaSign = Av1DefaultDistributions.ChromaFromLumaSign;
        this.chromaFromLumaAlpha = Av1DefaultDistributions.ChromaFromLumaAlpha;
        this.transformBlockSkip = Av1DefaultDistributions.GetTransformBlockSkip(qIndex);
        this.endOfBlockFlag = Av1DefaultDistributions.GetEndOfBlockFlag(qIndex);
        this.coefficientsBaseRange = Av1DefaultDistributions.GetCoefficientsBaseRange(qIndex);
        this.coefficientsBase = Av1DefaultDistributions.GetCoefficientsBase(qIndex);
        this.coefficientsBaseEndOfBlock = Av1DefaultDistributions.GetBaseEndOfBlock(qIndex);
        this.dcSign = Av1DefaultDistributions.GetDcSign(qIndex);
        this.endOfBlockExtra = Av1DefaultDistributions.GetEndOfBlockExtra(qIndex);
        this.levels = new(configuration);
        this.coefficientContexts = configuration.MemoryAllocator.Allocate<sbyte>(MaximumCoefficientContextCount);
        this.writer = new(configuration, initialSize, updateCdf);
        this.baseQIndex = qIndex;
    }

    /// <summary>
    /// Writes the frame-local intra-block-copy flag.
    /// </summary>
    /// <param name="value">Indicates whether intra-block copy is selected.</param>
    public void WriteUseIntraBlockCopy(bool value)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(value, this.tileIntraBlockCopy);
    }

    /// <summary>
    /// Writes an integer intra-block-copy displacement vector relative to a spatial reference.
    /// </summary>
    /// <param name="value">The displacement vector to encode.</param>
    /// <param name="reference">The spatially derived reference vector.</param>
    public void WriteDisplacementVector(Av1MotionVector value, Av1MotionVector reference)
        => this.displacementVector.Write(this.writer, value, reference);

    /// <summary>
    /// Writes a complete block partition type using the selected partition context.
    /// </summary>
    /// <param name="partitionType">The partition type to encode.</param>
    /// <param name="context">The partition probability context.</param>
    public void WritePartitionType(Av1PartitionType partitionType, int context)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol((int)partitionType, this.tilePartitionTypes[context]);
    }

    /// <summary>
    /// Writes the split-versus-horizontal boundary decision for a block clipped at the bottom tile edge.
    /// </summary>
    /// <param name="partitionType">The split or horizontal partition outcome.</param>
    /// <param name="blockSize">The current block size.</param>
    /// <param name="context">The partition probability context.</param>
    public void WriteSplitOrHorizontal(Av1PartitionType partitionType, Av1BlockSize blockSize, int context)
    {
        uint frequency = Av1SymbolDecoder.GetSplitOrHorizontalFrequency(this.tilePartitionTypes, blockSize, context);
        bool value = partitionType == Av1PartitionType.Split;
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteBoolean(value, frequency);
    }

    /// <summary>
    /// Writes the split-versus-vertical boundary decision for a block clipped at the right tile edge.
    /// </summary>
    /// <param name="partitionType">The split or vertical partition outcome.</param>
    /// <param name="blockSize">The current block size.</param>
    /// <param name="context">The partition probability context.</param>
    public void WriteSplitOrVertical(Av1PartitionType partitionType, Av1BlockSize blockSize, int context)
    {
        uint frequency = Av1SymbolDecoder.GetSplitOrVerticalFrequency(this.tilePartitionTypes, blockSize, context);
        bool value = partitionType == Av1PartitionType.Split;
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteBoolean(value, frequency);
    }

    /// <summary>
    /// Encodes one transform block's coefficient syntax using scan-order probability contexts.
    /// </summary>
    /// <param name="transformSize">The signaled transform size.</param>
    /// <param name="transformType">The transform type selecting the scan and context class.</param>
    /// <param name="intraDirection">The block's intra prediction mode.</param>
    /// <param name="coefficientBuffer">The raster-ordered signed coefficient levels.</param>
    /// <param name="componentType">The luma or chroma component category.</param>
    /// <param name="transformBlockContext">The neighboring skip and DC sign contexts.</param>
    /// <param name="endOfBlock">The one-based final nonzero scan position, or zero for an empty block.</param>
    /// <param name="useReducedTransformSet">Indicates whether the frame restricts transform choices.</param>
    /// <param name="filterIntraMode">The selected filter-intra mode, or the disabled sentinel.</param>
    /// <returns>The packed coefficient context used by adjacent transform blocks.</returns>
    public int WriteCoefficients(
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        Av1PredictionMode intraDirection,
        Span<int> coefficientBuffer,
        Av1ComponentType componentType,
        Av1TransformBlockContext transformBlockContext,
        ushort endOfBlock,
        bool useReducedTransformSet,
        Av1FilterIntraMode filterIntraMode)
    {
        int c;
        Av1TransformSize adjustedTransformSize = transformSize.GetAdjusted();
        int width = adjustedTransformSize.GetWidth();
        int height = adjustedTransformSize.GetHeight();
        Av1TransformClass transformClass = transformType.ToClass();
        Av1ScanOrder scanOrder = Av1ScanOrderConstants.GetScanOrder(transformSize, transformType);
        ReadOnlySpan<short> scan = scanOrder.Scan;
        Av1TransformSize transformSizeContext = Av1SymbolContextHelper.GetTransformSizeContext(transformSize);

        ref Av1SymbolWriter w = ref this.writer;

        // AV1 omits high-frequency coefficients beyond 32 samples on every 64-point transform dimension. The tile
        // owns maximum-sized workspaces so repeated transform coding changes only their active views.
        this.levels.Reset(new Size(width, height));
        Span<sbyte> coefficientContexts = this.coefficientContexts.Memory.Span[..(width * height)];
        coefficientContexts.Clear();

        Guard.MustBeLessThan((int)transformSizeContext, (int)Av1TransformSize.AllSizes, nameof(transformSizeContext));

        this.WriteTransformBlockSkip(endOfBlock == 0, transformSizeContext, transformBlockContext.SkipContext);

        if (endOfBlock == 0)
        {
            return 0;
        }

        this.levels.Initialize(coefficientBuffer);
        if (componentType == Av1ComponentType.Luminance)
        {
            this.WriteTransformType(transformType, transformSize, useReducedTransformSet, this.baseQIndex, filterIntraMode, intraDirection);
        }

        this.WriteEndOfBlockPosition(endOfBlock, componentType, transformClass, transformSize, transformSizeContext);

        Av1SymbolContextHelper.GetNzMapContexts(this.levels, scan, endOfBlock, transformSize, transformClass, coefficientContexts);
        int limitedTransformSizeContext = Math.Min((int)transformSizeContext, (int)Av1TransformSize.Size32x32);
        for (c = endOfBlock - 1; c >= 0; --c)
        {
            short pos = scan[c];
            int v = coefficientBuffer[pos];
            short coeffContext = coefficientContexts[pos];
            Point position = this.levels.GetPosition(pos);
            int level = Math.Abs(v);

            if (c == endOfBlock - 1)
            {
                w.WriteSymbol(Math.Min(level, 3) - 1, this.coefficientsBaseEndOfBlock[(int)transformSizeContext][(int)componentType][coeffContext]);
            }
            else
            {
                w.WriteSymbol(Math.Min(level, 3), this.coefficientsBase[(int)transformSizeContext][(int)componentType][coeffContext]);
            }

            if (level > Av1Constants.BaseLevelsCount)
            {
                // Base-range symbols extend levels above the two base levels in fixed-size chunks.
                int baseRange = level - 1 - Av1Constants.BaseLevelsCount;
                int baseRangeContext = Av1SymbolContextHelper.GetBaseRangeContext(this.levels, position, transformClass);
                for (int idx = 0; idx < Av1Constants.CoefficientBaseRange; idx += Av1Constants.BaseRangeSizeMinus1)
                {
                    int k = Math.Min(baseRange - idx, Av1Constants.BaseRangeSizeMinus1);
                    w.WriteSymbol(k, this.coefficientsBaseRange[limitedTransformSizeContext][(int)componentType][baseRangeContext]);
                    if (k < Av1Constants.BaseRangeSizeMinus1)
                    {
                        break;
                    }
                }
            }
        }

        // Signs follow every magnitude so the DC sign can use its neighboring context and AC signs remain literals.
        int culLevel = 0;
        for (c = 0; c < endOfBlock; ++c)
        {
            short pos = scan[c];
            int v = coefficientBuffer[pos];
            int level = Math.Abs(v);
            culLevel += level;

            uint sign = v < 0 ? 1u : 0u;
            if (level > 0)
            {
                if (c == 0)
                {
                    w.WriteSymbol((int)sign, this.dcSign[(int)componentType][transformBlockContext.DcSignContext]);
                }
                else
                {
                    w.WriteLiteral(sign, 1);
                }

                if (level > (Av1Constants.CoefficientBaseRange + Av1Constants.BaseLevelsCount))
                {
                    this.WriteGolomb(level - Av1Constants.CoefficientBaseRange - 1 - Av1Constants.BaseLevelsCount);
                }
            }
        }

        culLevel = Math.Min(Av1Constants.CoefficientContextMask, culLevel);

        // The DC sign is packed above the magnitude bits so adjacent blocks can derive both contexts from one value.
        Av1SymbolContextHelper.SetDcSign(ref culLevel, coefficientBuffer[0]);
        return culLevel;
    }

    /// <summary>
    /// Writes an end-of-block token and its context-coded and literal suffix bits.
    /// </summary>
    /// <param name="endOfBlock">The one-based final nonzero scan position.</param>
    /// <param name="componentType">The luma or chroma component category.</param>
    /// <param name="transformClass">The transform direction class.</param>
    /// <param name="transformSize">The signaled transform size selecting the token alphabet.</param>
    /// <param name="transformSizeContext">The square transform-size probability context.</param>
    public void WriteEndOfBlockPosition(ushort endOfBlock, Av1ComponentType componentType, Av1TransformClass transformClass, Av1TransformSize transformSize, Av1TransformSize transformSizeContext)
    {
        short endOfBlockPosition = Av1SymbolContextHelper.GetEndOfBlockPosition(endOfBlock, out int eobExtra);
        this.WriteEndOfBlockFlag(componentType, transformClass, transformSize, endOfBlockPosition);

        int eobOffsetBitCount = Av1SymbolContextHelper.EndOfBlockOffsetBits[endOfBlockPosition];
        if (eobOffsetBitCount > 0)
        {
            ref Av1SymbolWriter w = ref this.writer;
            int eobShift = eobOffsetBitCount - 1;
            int bit = Av1Math.GetBit(eobExtra, eobShift);

            // The local table retains placeholders for the first three tokens, unlike the reference decoder's compact table,
            // so the encoded token is also the distribution index.
            int endOfBlockContext = endOfBlockPosition;
            w.WriteSymbol(bit, this.endOfBlockExtra[(int)transformSizeContext][(int)componentType][endOfBlockContext]);
            for (int i = 1; i < eobOffsetBitCount; i++)
            {
                eobShift = eobOffsetBitCount - 1 - i;
                bit = Av1Math.GetBit(eobExtra, eobShift);
                w.WriteLiteral((uint)bit, 1);
            }
        }
    }

    /// <summary>
    /// Writes whether a transform block has no coded coefficients.
    /// </summary>
    /// <param name="skip">Indicates whether the transform block is empty.</param>
    /// <param name="transformSizeContext">The square transform-size probability context.</param>
    /// <param name="skipContext">The context derived from neighboring coefficient blocks.</param>
    public void WriteTransformBlockSkip(bool skip, Av1TransformSize transformSizeContext, int skipContext)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(skip, this.transformBlockSkip[(int)transformSizeContext][skipContext]);
    }

    /// <summary>
    /// Finalizes the range-coded tile payload and transfers ownership of its memory.
    /// </summary>
    /// <returns>The memory owner containing the encoded tile bytes.</returns>
    public IMemoryOwner<byte> Exit()
    {
        ref Av1SymbolWriter w = ref this.writer;
        return w.Exit();
    }

    /// <summary>
    /// Releases output memory that has not been transferred by <see cref="Exit"/>.
    /// </summary>
    public void Dispose()
    {
        if (!this.isDisposed)
        {
            this.coefficientContexts.Dispose();
            this.levels.Dispose();
            this.writer.Dispose();
            this.isDisposed = true;
        }
    }

    /// <summary>
    /// Writes the unsigned exponential-Golomb suffix used for coefficient levels beyond the base range.
    /// </summary>
    /// <param name="level">The nonnegative suffix value.</param>
    public void WriteGolomb(int level)
    {
        uint x = (uint)level + 1u;
        int length = (int)Av1Math.Log2_32(x) + 1;

        Guard.MustBeGreaterThan(length, 0, nameof(length));

        ref Av1SymbolWriter w = ref this.writer;
        for (int i = 0; i < length - 1; ++i)
        {
            w.WriteLiteral(0u, 1);
        }

        for (int j = length - 1; j >= 0; --j)
        {
            w.WriteLiteral((x >> j) & 0x01, 1);
        }
    }

    /// <summary>
    /// Writes the end-of-block token for a transform coefficient-count category.
    /// </summary>
    /// <param name="componentType">The luma or chroma component category.</param>
    /// <param name="transformClass">The transform direction class.</param>
    /// <param name="transformSize">The signaled transform size.</param>
    /// <param name="endOfBlockPosition">The one-based end-of-block token.</param>
    private void WriteEndOfBlockFlag(Av1ComponentType componentType, Av1TransformClass transformClass, Av1TransformSize transformSize, int endOfBlockPosition)
    {
        int endOfBlockMultiSize = transformSize.GetLog2Minus4();
        int endOfBlockContext = transformClass == Av1TransformClass.Class2D ? 0 : 1;
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(endOfBlockPosition - 1, this.endOfBlockFlag[endOfBlockMultiSize][(int)componentType][endOfBlockContext]);
    }

    /// <summary>
    /// Writes an intra transform type when the permitted transform set contains multiple choices.
    /// </summary>
    /// <param name="transformType">The transform type to encode.</param>
    /// <param name="transformSize">The signaled transform size.</param>
    /// <param name="useReducedTransformSet">Indicates whether the frame restricts transform choices.</param>
    /// <param name="baseQIndex">The active base quantizer index.</param>
    /// <param name="filterIntraMode">The filter-intra mode when enabled.</param>
    /// <param name="intraDirection">The ordinary intra prediction mode.</param>
    public void WriteTransformType(
        Av1TransformType transformType,
        Av1TransformSize transformSize,
        bool useReducedTransformSet,
        int baseQIndex,
        Av1FilterIntraMode filterIntraMode,
        Av1PredictionMode intraDirection)
    {
        // Still-image encoding reaches this path only for intra blocks, so the intra transform set is authoritative.
        Av1TransformSetType transformSetType = Av1SymbolContextHelper.GetExtendedTransformSetType(transformSize, useReducedTransformSet);
        if (Av1SymbolContextHelper.GetExtendedTransformTypeCount(transformSetType) > 1 && baseQIndex > 0)
        {
            Av1TransformSize squareTransformSize = transformSize.GetSquareSize();
            Guard.MustBeLessThanOrEqualTo((int)squareTransformSize, Av1Constants.ExtendedTransformCount, nameof(squareTransformSize));

            int extendedSet = Av1SymbolContextHelper.GetExtendedTransformSet(transformSetType);

            // Set zero contains only DCT-DCT, which was excluded by the multiple-choice condition above.
            Guard.MustBeGreaterThan(extendedSet, 0, nameof(extendedSet));

            Av1PredictionMode intraDirectionContext;
            if (filterIntraMode != Av1FilterIntraMode.AllFilterIntraModes)
            {
                intraDirectionContext = filterIntraMode.ToIntraDirection();
            }
            else
            {
                intraDirectionContext = intraDirection;
            }

            Guard.MustBeLessThan((int)intraDirectionContext, 13, nameof(intraDirectionContext));
            Guard.MustBeLessThan((int)squareTransformSize, 4, nameof(squareTransformSize));
            ref Av1SymbolWriter w = ref this.writer;
            w.WriteSymbol(
                Av1SymbolContextHelper.GetExtendedTransformIndex(transformSetType, transformType),
                this.intraExtendedTransform[extendedSet][(int)squareTransformSize][(int)intraDirectionContext]);
        }
    }

    /// <summary>
    /// Writes a spatially predicted segment identifier.
    /// </summary>
    /// <param name="segmentId">The segment identifier.</param>
    /// <param name="context">The context derived from neighboring segment identifiers.</param>
    public void WriteSegmentId(int segmentId, int context)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(segmentId, this.segmentId[context]);
    }

    /// <summary>
    /// Writes the transform-skip flag from a neighboring skip context.
    /// </summary>
    /// <param name="skip">Indicates whether the block contains no coded transform coefficients.</param>
    /// <param name="context">The neighboring skip context.</param>
    public void WriteSkip(bool skip, int context)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(skip, this.skip[context]);
    }

    /// <summary>
    /// Writes the compound-reference skip-mode flag.
    /// </summary>
    /// <param name="skip">Indicates whether skip mode is selected.</param>
    /// <param name="context">The neighboring skip-mode context.</param>
    public void WriteSkipMode(bool skip, int context)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(skip, this.skipMode[context]);
    }

    /// <summary>
    /// Writes the filter-intra enable flag and, when enabled, its prediction mode.
    /// </summary>
    /// <param name="filterIntraMode">The selected filter-intra mode, or the disabled sentinel.</param>
    /// <param name="blockSize">The block size selecting the enable distribution.</param>
    public void WriteFilterIntraMode(Av1FilterIntraMode filterIntraMode, Av1BlockSize blockSize)
    {
        ref Av1SymbolWriter w = ref this.writer;
        bool useFilter = filterIntraMode != Av1FilterIntraMode.AllFilterIntraModes;
        w.WriteSymbol(useFilter, this.filterIntra[(int)blockSize]);
        if (useFilter)
        {
            w.WriteSymbol((int)filterIntraMode, this.filterIntraMode);
        }
    }

    /// <summary>
    /// Writes a signed quantizer-index delta value.
    /// </summary>
    /// <param name="deltaQindex">The signed quantizer-index delta.</param>
    public void WriteDeltaQuantizerIndex(int deltaQindex)
    {
        ref Av1SymbolWriter w = ref this.writer;
        bool sign = deltaQindex < 0;
        int abs = Math.Abs(deltaQindex);
        bool isSmallValue = abs < Av1Constants.DeltaQuantizerSmall;

        w.WriteSymbol(Math.Min(abs, Av1Constants.DeltaQuantizerSmall), this.deltaQuantizerAbsolute);

        if (!isSmallValue)
        {
            // Escape magnitudes encode their bit width first, followed by the offset within that width's range.
            int remainingBitCount = Av1Math.MostSignificantBit((uint)(abs - 1));
            int threshold = (1 << remainingBitCount) + 1;
            w.WriteLiteral((uint)(remainingBitCount - 1), 3);
            w.WriteLiteral((uint)(abs - threshold), remainingBitCount);
        }

        if (abs > 0)
        {
            w.WriteLiteral(sign);
        }
    }

    /// <summary>
    /// Writes a key-frame luma prediction mode using the above and left mode contexts.
    /// </summary>
    /// <param name="lumaMode">The luma prediction mode.</param>
    /// <param name="topContext">The reduced above-mode context.</param>
    /// <param name="leftContext">The reduced left-mode context.</param>
    public void WriteLumaMode(Av1PredictionMode lumaMode, byte topContext, byte leftContext)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol((int)lumaMode, this.keyFrameYMode[topContext][leftContext]);
    }

    /// <summary>
    /// Writes an unsigned directional angle-delta symbol.
    /// </summary>
    /// <param name="angleDelta">The signed angle delta offset by <see cref="Av1Constants.MaxAngleDelta"/>.</param>
    /// <param name="context">The directional prediction mode selecting the distribution.</param>
    public void WriteAngleDelta(int angleDelta, Av1PredictionMode context)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(angleDelta, this.angleDelta[context - Av1PredictionMode.Vertical]);
    }

    /// <summary>
    /// Writes a fixed-width CDEF strength index.
    /// </summary>
    /// <param name="cdefStrength">The CDEF strength index.</param>
    /// <param name="bitCount">The number of signaled bits.</param>
    public void WriteCdefStrength(int cdefStrength, int bitCount)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteLiteral((uint)cdefStrength, bitCount);
    }

    /// <summary>
    /// Writes a chroma intra prediction mode conditioned on the luma mode and chroma-from-luma availability.
    /// </summary>
    /// <param name="chromaMode">The chroma prediction mode.</param>
    /// <param name="isChromaFromLumaAllowed">Indicates whether chroma-from-luma is valid for the block.</param>
    /// <param name="lumaMode">The block's luma prediction mode.</param>
    public void WriteChromaMode(Av1ChromaPredictionMode chromaMode, bool isChromaFromLumaAllowed, Av1PredictionMode lumaMode)
    {
        ref Av1SymbolWriter w = ref this.writer;
        int cflAllowed = isChromaFromLumaAllowed ? 1 : 0;
        w.WriteSymbol((int)chromaMode, this.uvMode[cflAllowed][(int)lumaMode]);
    }

    /// <summary>
    /// Writes the joint chroma-from-luma signs and the magnitude index for each nonzero plane.
    /// </summary>
    /// <param name="chromaFromLumaIndex">The packed U/V alpha-magnitude indices.</param>
    /// <param name="joinedSign">The joint U/V sign symbol.</param>
    public void WriteChromaFromLumaAlphas(int chromaFromLumaIndex, int joinedSign)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(joinedSign, this.chromaFromLumaSign);

        // Magnitudes are only signaled for nonzero signs; the shared helper keeps encoder and decoder mappings exact.
        int signU = Av1ChromaFromLumaMath.SignU(joinedSign);
        if (signU != Av1ChromaFromLumaMath.SignZero)
        {
            int contextU = Av1ChromaFromLumaMath.ContextU(joinedSign);
            int indexU = Av1ChromaFromLumaMath.IndexU(chromaFromLumaIndex);
            w.WriteSymbol(indexU, this.chromaFromLumaAlpha[contextU]);
        }

        int signV = Av1ChromaFromLumaMath.SignV(joinedSign);
        if (signV != Av1ChromaFromLumaMath.SignZero)
        {
            int contextV = Av1ChromaFromLumaMath.ContextV(joinedSign);
            int indexV = Av1ChromaFromLumaMath.IndexV(chromaFromLumaIndex);
            w.WriteSymbol(indexV, this.chromaFromLumaAlpha[contextV]);
        }
    }
}
