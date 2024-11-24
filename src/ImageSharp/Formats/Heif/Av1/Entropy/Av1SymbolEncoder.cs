// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

internal class Av1SymbolEncoder : IDisposable
{
    private static readonly int[][] ExtendedTransformIndices = [
        [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        [1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        [1, 3, 4, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        [1, 5, 6, 4, 0, 0, 0, 0, 0, 0, 2, 3, 0, 0, 0, 0],
        [3, 4, 5, 8, 6, 7, 9, 10, 11, 0, 1, 2, 0, 0, 0, 0],
        [7, 8, 9, 12, 10, 11, 13, 14, 15, 0, 1, 2, 3, 4, 5, 6],
    ];

    private readonly Av1Distribution tileIntraBlockCopy = Av1DefaultDistributions.IntraBlockCopy;
    private readonly Av1Distribution[] tilePartitionTypes = Av1DefaultDistributions.PartitionTypes;
    private readonly Av1Distribution[] skip = Av1DefaultDistributions.Skip;
    private readonly Av1Distribution[][][] endOfBlockFlag;
    private readonly Av1Distribution[][][] coefficientsBaseRange;
    private readonly Av1Distribution[][][] coefficientsBase;
    private readonly Av1Distribution[][][] coefficientsBaseEndOfBlock;
    private readonly Av1Distribution[][] dcSign;
    private readonly Av1Distribution[][][] endOfBlockExtra;
    private readonly Av1Distribution[][][] intraExtendedTransform = Av1DefaultDistributions.IntraExtendedTransform;
    private bool isDisposed;
    private readonly Configuration configuration;
    private Av1SymbolWriter writer;

    public Av1SymbolEncoder(Configuration configuration, int initialSize, int qIndex)
    {
        this.endOfBlockFlag = Av1DefaultDistributions.GetEndOfBlockFlag(qIndex);
        this.coefficientsBaseRange = Av1DefaultDistributions.GetCoefficientsBaseRange(qIndex);
        this.coefficientsBase = Av1DefaultDistributions.GetCoefficientsBase(qIndex);
        this.coefficientsBaseEndOfBlock = Av1DefaultDistributions.GetBaseEndOfBlock(qIndex);
        this.dcSign = Av1DefaultDistributions.GetDcSign(qIndex);
        this.endOfBlockExtra = Av1DefaultDistributions.GetEndOfBlockExtra(qIndex);
        this.configuration = configuration;
        this.writer = new(configuration, initialSize);
    }

    public void WriteUseIntraBlockCopy(bool value)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(value ? 1 : 0, this.tileIntraBlockCopy);
    }

    public void WritePartitionType(Av1PartitionType partitionType, int context)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol((int)partitionType, this.tilePartitionTypes[context]);
    }

    public void WriteSplitOrHorizontal(Av1PartitionType partitionType, Av1BlockSize blockSize, int context)
    {
        Av1Distribution distribution = Av1SymbolDecoder.GetSplitOrHorizontalDistribution(this.tilePartitionTypes, blockSize, context);
        int value = partitionType == Av1PartitionType.Split ? 1 : 0;
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(value, distribution);
    }

    public void WriteSplitOrVertical(Av1PartitionType partitionType, Av1BlockSize blockSize, int context)
    {
        Av1Distribution distribution = Av1SymbolDecoder.GetSplitOrVerticalDistribution(this.tilePartitionTypes, blockSize, context);
        int value = partitionType == Av1PartitionType.Split ? 1 : 0;
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(value, distribution);
    }

    /// <summary>
    /// SVT: av1_write_coeffs_txb_1d
    /// </summary>
    public int WriteCoefficients(
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int txbIndex, // TODO: Doesn't seem to be used, remove.
        Av1PredictionMode intraDirection,
        Span<int> coeffBuffer,
        Av1ComponentType componentType,
        short transformBlockSkipContext,
        short dcSignContext,
        ushort eob,
        bool useReducedTransformSet,
        int baseQIndex,
        Av1FilterIntraMode filterIntraMode)
    {
        int c;
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        Av1TransformClass transformClass = transformType.ToClass();
        Av1ScanOrder scanOrder = Av1ScanOrderConstants.GetScanOrder(transformSize, transformType);
        ReadOnlySpan<short> scan = scanOrder.Scan;
        int bwl = transformSize.GetBlockWidthLog2();
        Av1TransformSize transformSizeContext = (Av1TransformSize)(((int)transformSize.GetSquareSize() + (int)transformSize.GetSquareUpSize() + 1) >> 1);

        ref Av1SymbolWriter w = ref this.writer;

        Av1LevelBuffer levels = new(this.configuration, new Size(width, height));
        Span<sbyte> coeff_contexts = new sbyte[Av1Constants.MaxTransformSize * Av1Constants.MaxTransformSize];

        Guard.MustBeLessThan((int)transformSizeContext, (int)Av1TransformSize.AllSizes, nameof(transformSizeContext));

        bool hasEndOfBlock = eob != 0;
        this.WriteSkip(!hasEndOfBlock, transformBlockSkipContext);

        if (eob == 0)
        {
            return 0;
        }

        levels.Initialize(coeffBuffer);
        if (componentType == Av1ComponentType.Luminance)
        {
            this.WriteTransformType(transformType, transformSize, useReducedTransformSet, baseQIndex, filterIntraMode, intraDirection);
        }

        short endOfBlockPosition = Av1SymbolContextHelper.GetEndOfBlockPosition(eob, out int eob_extra);
        this.WriteEndOfBlockFlag(componentType, transformClass, transformSize, endOfBlockPosition);

        int eob_offset_bits = Av1SymbolContextHelper.EndOfBlockOffsetBits[endOfBlockPosition];
        if (eob_offset_bits > 0)
        {
            int eob_shift = eob_offset_bits - 1;
            int bit = (eob_extra & (1 << eob_shift)) != 0 ? 1 : 0;
            w.WriteSymbol(bit, this.endOfBlockExtra[(int)transformSizeContext][(int)componentType][endOfBlockPosition]);
            for (int i = 1; i < eob_offset_bits; i++)
            {
                eob_shift = eob_offset_bits - 1 - i;
                bit = (eob_extra & (1 << eob_shift)) != 0 ? 1 : 0;
                w.WriteLiteral((uint)bit, 1);
            }
        }

        Av1SymbolContextHelper.GetNzMapContexts(levels, scan, eob, transformSize, transformClass, coeff_contexts);
        int limitedTransformSizeContext = Math.Min((int)transformSizeContext, (int)Av1TransformSize.Size32x32);
        for (c = eob - 1; c >= 0; --c)
        {
            short pos = scan[c];
            int v = coeffBuffer[pos];
            short coeff_ctx = coeff_contexts[pos];
            int level = Math.Abs(v);

            if (c == eob - 1)
            {
                w.WriteSymbol(Math.Min(level, 3) - 1, this.coefficientsBaseEndOfBlock[(int)transformSizeContext][(int)componentType][coeff_ctx]);
            }
            else
            {
                w.WriteSymbol(Math.Min(level, 3), this.coefficientsBase[(int)transformSizeContext][(int)componentType][coeff_ctx]);
            }

            if (level > Av1Constants.BaseLevelsCount)
            {
                // level is above 1.
                int baseRange = level - 1 - Av1Constants.BaseLevelsCount;
                int baseRangeContext = Av1SymbolContextHelper.GetBaseRangeContext(levels, pos, bwl, transformClass);
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

        // Loop to code all signs in the transform block,
        // starting with the sign of DC (if applicable)
        int cul_level = 0;
        for (c = 0; c < eob; ++c)
        {
            short pos = scan[c];
            int v = coeffBuffer[pos];
            int level = Math.Abs(v);
            cul_level += level;

            uint sign = v < 0 ? 1u : 0u;
            if (level > 0)
            {
                if (c == 0)
                {
                    w.WriteSymbol((int)sign, this.dcSign[(int)componentType][dcSignContext]);
                }
                else
                {
                    w.WriteLiteral(sign, 1);
                }

                if (level > Av1Constants.CoefficientBaseRange + Av1Constants.BaseLevelsCount)
                {
                    this.WriteGolomb(level - Av1Constants.CoefficientBaseRange - 1 - Av1Constants.BaseLevelsCount);
                }
            }
        }

        cul_level = Math.Min(Av1Constants.CoefficientContextMask, cul_level);

        // DC value
        Av1SymbolContextHelper.SetDcSign(ref cul_level, coeffBuffer[0]);
        return cul_level;
    }

    private void WriteSkip(bool hasEndOfBlock, int context)
    {
        // Has EOB, means we won't skip, negating the logic.
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(hasEndOfBlock ? 0 : 1, this.skip[context]);
    }

    public IMemoryOwner<byte> Exit()
    {
        ref Av1SymbolWriter w = ref this.writer;
        return w.Exit();
    }

    public void Dispose()
    {
        if (!this.isDisposed)
        {
            this.writer.Dispose();
            this.isDisposed = true;
        }
    }

    /// <summary>
    /// SVT: write_golomb
    /// </summary>
    private void WriteGolomb(int level)
    {
        int x = level + 1;
        int i = x;
        int length = (int)Av1Math.Log2_32((uint)x) + 1;

        Guard.MustBeGreaterThan(length, 0, nameof(length));

        ref Av1SymbolWriter w = ref this.writer;
        for (i = 0; i < length - 1; ++i)
        {
            w.WriteLiteral(0, 1);
        }

        for (int j = length - 1; j >= 0; --j)
        {
            w.WriteLiteral((uint)(x >> j & 0x01), 1);
        }
    }

    private void WriteEndOfBlockFlag(Av1ComponentType componentType, Av1TransformClass transformClass, Av1TransformSize transformSize, int endOfBlockPosition)
    {
        int endOfBlockMultiSize = transformSize.GetLog2Minus4();
        int endOfBlockContext = transformClass == Av1TransformClass.Class2D ? 0 : 1;
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(endOfBlockPosition - 1, this.endOfBlockFlag[endOfBlockMultiSize][(int)componentType][endOfBlockContext]);
    }

    /// <summary>
    /// SVT: av1_write_tx_type
    /// </summary>
    private void WriteTransformType(
        Av1TransformType transformType,
        Av1TransformSize transformSize,
        bool useReducedTransformSet,
        int baseQIndex,
        Av1FilterIntraMode filterIntraMode,
        Av1PredictionMode intraDirection)
    {
        // bool isInter = mbmi->block_mi.use_intrabc || is_inter_mode(mbmi->block_mi.mode);
        ref Av1SymbolWriter w = ref this.writer;
        if (Av1SymbolContextHelper.GetExtendedTransformTypeCount(transformSize, useReducedTransformSet) > 1 && baseQIndex > 0)
        {
            Av1TransformSize square_tx_size = transformSize.GetSquareSize();
            Guard.MustBeLessThanOrEqualTo((int)square_tx_size, Av1Constants.ExtendedTransformCount, nameof(square_tx_size));

            Av1TransformSetType tx_set_type = Av1SymbolContextHelper.GetExtendedTransformSetType(transformSize, useReducedTransformSet);
            int eset = Av1SymbolContextHelper.GetExtendedTransformSet(transformSize, useReducedTransformSet);

            // eset == 0 should correspond to a set with only DCT_DCT and there
            // is no need to send the tx_type
            Guard.MustBeGreaterThan(eset, 0, nameof(eset));

            // assert(av1_ext_tx_used[tx_set_type][transformType]);
            {
                Av1PredictionMode intra_dir;
                if (filterIntraMode != Av1FilterIntraMode.AllFilterIntraModes)
                {
                    intra_dir = filterIntraMode.ToIntraDirection();
                }
                else
                {
                    intra_dir = intraDirection;
                }

                Guard.MustBeLessThan((int)intra_dir, 13, nameof(intra_dir));
                Guard.MustBeLessThan((int)square_tx_size, 4, nameof(square_tx_size));
                w.WriteSymbol(
                    ExtendedTransformIndices[(int)tx_set_type][(int)transformType],
                    this.intraExtendedTransform[eset][(int)square_tx_size][(int)intra_dir]);
            }
        }
    }
}
