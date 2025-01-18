// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal class Av1Transform2dFlipConfiguration
{
    public const int MaxStageNumber = 12;
    private const int SmallestTransformSizeLog2 = 2;

    private static readonly Av1TransformType1d[] VerticalType =
        [
            Av1TransformType1d.Dct,
            Av1TransformType1d.Adst,
            Av1TransformType1d.Dct,
            Av1TransformType1d.Adst,
            Av1TransformType1d.FlipAdst,
            Av1TransformType1d.Dct,
            Av1TransformType1d.FlipAdst,
            Av1TransformType1d.Adst,
            Av1TransformType1d.FlipAdst,
            Av1TransformType1d.Identity,
            Av1TransformType1d.Dct,
            Av1TransformType1d.Identity,
            Av1TransformType1d.Adst,
            Av1TransformType1d.Identity,
            Av1TransformType1d.FlipAdst,
            Av1TransformType1d.Identity,
        ];

    private static readonly Av1TransformType1d[] HorizontalType =
        [
            Av1TransformType1d.Dct,
            Av1TransformType1d.Dct,
            Av1TransformType1d.Adst,
            Av1TransformType1d.Adst,
            Av1TransformType1d.Dct,
            Av1TransformType1d.FlipAdst,
            Av1TransformType1d.FlipAdst,
            Av1TransformType1d.FlipAdst,
            Av1TransformType1d.Adst,
            Av1TransformType1d.Identity,
            Av1TransformType1d.Identity,
            Av1TransformType1d.Dct,
            Av1TransformType1d.Identity,
            Av1TransformType1d.Adst,
            Av1TransformType1d.Identity,
            Av1TransformType1d.FlipAdst,
        ];

    private static readonly int[][] ShiftMap =
        [
            [2, 0, 0], // 4x4
            [2, -1, 0], // 8x8
            [2, -2, 0], // 16x16
            [2, -4, 0], // 32x32
            [0, -2, -2], // 64x64
            [2, -1, 0], // 4x8
            [2, -1, 0], // 8x4
            [2, -2, 0], // 8x16
            [2, -2, 0], // 16x8
            [2, -4, 0], // 16x32
            [2, -4, 0], // 32x16
            [0, -2, -2], // 32x64
            [2, -4, -2], // 64x32
            [2, -1, 0], // 4x16
            [2, -1, 0], // 16x4
            [2, -2, 0], // 8x32
            [2, -2, 0], // 32x8
            [0, -2, 0], // 16x64
            [2, -4, 0], // 64x16
        ];

    private static readonly int[][] CosBitColumnMap =
        [[13, 13, 13, 0, 0], [13, 13, 13, 12, 0], [13, 13, 13, 12, 13], [0, 13, 13, 12, 13], [0, 0, 13, 12, 13]];

    private static readonly int[][] CosBitRowMap =
        [[13, 13, 12, 0, 0], [13, 13, 13, 12, 0], [13, 13, 12, 13, 12], [0, 12, 13, 12, 11], [0, 0, 12, 11, 10]];

    private static readonly Av1TransformFunctionType[][] TransformFunctionTypeMap =
        [
            [Av1TransformFunctionType.Dct4, Av1TransformFunctionType.Adst4, Av1TransformFunctionType.Adst4, Av1TransformFunctionType.Identity4],
            [Av1TransformFunctionType.Dct8, Av1TransformFunctionType.Adst8, Av1TransformFunctionType.Adst8, Av1TransformFunctionType.Identity8],
            [Av1TransformFunctionType.Dct16, Av1TransformFunctionType.Adst16, Av1TransformFunctionType.Adst16, Av1TransformFunctionType.Identity16],
            [Av1TransformFunctionType.Dct32, Av1TransformFunctionType.Adst32, Av1TransformFunctionType.Adst32, Av1TransformFunctionType.Identity32],
            [Av1TransformFunctionType.Dct64, Av1TransformFunctionType.Invalid, Av1TransformFunctionType.Invalid, Av1TransformFunctionType.Identity64]
        ];

    private static readonly int[] StageNumberList =
        [
            4, // TXFM_TYPE_DCT4
            6, // TXFM_TYPE_DCT8
            8, // TXFM_TYPE_DCT16
            10, // TXFM_TYPE_DCT32
            12, // TXFM_TYPE_DCT64
            7, // TXFM_TYPE_ADST4
            8, // TXFM_TYPE_ADST8
            10, // TXFM_TYPE_ADST16
            12, // TXFM_TYPE_ADST32
            1, // TXFM_TYPE_IDENTITY4
            1, // TXFM_TYPE_IDENTITY8
            1, // TXFM_TYPE_IDENTITY16
            1, // TXFM_TYPE_IDENTITY32
            1, // TXFM_TYPE_IDENTITY64
        ];

    private static readonly int[][] RangeMulti2List =
        [
            [0, 2, 3, 3], // fdct4_range_mult2
            [0, 2, 4, 5, 5, 5], // fdct8_range_mult2
            [0, 2, 4, 6, 7, 7, 7, 7], // fdct16_range_mult2
            [0, 2, 4, 6, 8, 9, 9, 9, 9, 9], // fdct32_range_mult2
            [0, 2, 4, 6, 8, 10, 11, 11, 11, 11, 11, 11], // fdct64_range_mult2
            [0, 2, 4, 3, 3, 3, 3], // fadst4_range_mult2
            [0, 0, 1, 3, 3, 5, 5, 5], // fadst8_range_mult2
            [0, 0, 1, 3, 3, 5, 5, 7, 7, 7], // fadst16_range_mult2
            [0, 0, 1, 3, 3, 5, 5, 7, 7, 9, 9, 9], // fadst32_range_mult2
            [1], // fidtx4_range_mult2
            [2], // fidtx8_range_mult2
            [3], // fidtx16_range_mult2
            [4], // fidtx32_range_mult2
            [5], // fidtx64_range_mult2
        ];

    private int[] shift;

    public Av1Transform2dFlipConfiguration(Av1TransformType transformType, Av1TransformSize transformSize)
    {
        // SVT: svt_av1_get_inv_txfm_cfg
        // SVT: svt_aom_transform_config
        this.TransformSize = transformSize;
        this.TransformType = transformType;
        this.SetFlip(transformType);
        this.TransformTypeColumn = VerticalType[(int)transformType];
        this.TransformTypeRow = HorizontalType[(int)transformType];
        int txw_idx = transformSize.GetBlockWidthLog2() - SmallestTransformSizeLog2;
        int txh_idx = transformSize.GetBlockHeightLog2() - SmallestTransformSizeLog2;
        this.shift = ShiftMap[(int)transformSize];
        this.CosBitColumn = CosBitColumnMap[txw_idx][txh_idx];
        this.CosBitRow = CosBitRowMap[txw_idx][txh_idx];
        this.TransformFunctionTypeColumn = TransformFunctionTypeMap[txh_idx][(int)this.TransformTypeColumn];
        this.TransformFunctionTypeRow = TransformFunctionTypeMap[txw_idx][(int)this.TransformTypeRow];
        this.StageNumberColumn = this.TransformFunctionTypeColumn != Av1TransformFunctionType.Invalid ? StageNumberList[(int)this.TransformFunctionTypeColumn] : -1;
        this.StageNumberRow = this.TransformFunctionTypeRow != Av1TransformFunctionType.Invalid ? StageNumberList[(int)this.TransformFunctionTypeRow] : -1;
        this.StageRangeColumn = new byte[12];
        this.StageRangeRow = new byte[12];
        this.NonScaleRange();
    }

    public int CosBitColumn { get; }

    public int CosBitRow { get; }

    public Av1TransformType1d TransformTypeColumn { get; }

    public Av1TransformType1d TransformTypeRow { get; }

    public Av1TransformFunctionType TransformFunctionTypeColumn { get; }

    public Av1TransformFunctionType TransformFunctionTypeRow { get; }

    public int StageNumberColumn { get; }

    public int StageNumberRow { get; }

    public Av1TransformSize TransformSize { get; }

    public Av1TransformType TransformType { get; }

    public bool FlipUpsideDown { get; private set; }

    public bool FlipLeftToRight { get; private set; }

    public Span<int> Shift => this.shift;

    public byte[] StageRangeColumn { get; }

    public byte[] StageRangeRow { get; }

    /// <summary>
    /// SVT: svt_av1_gen_fwd_stage_range
    /// SVT: svt_av1_gen_inv_stage_range
    /// </summary>
    public void GenerateStageRange(int bitDepth)
    {
        // Take the shift from the larger dimension in the rectangular case.
        Span<int> shift = this.Shift;

        // i < MAX_TXFM_STAGE_NUM will mute above array bounds warning
        for (int i = 0; i < this.StageNumberColumn && i < MaxStageNumber; ++i)
        {
            this.StageRangeColumn[i] = (byte)(this.StageRangeColumn[i] + shift[0] + bitDepth + 1);
        }

        // i < MAX_TXFM_STAGE_NUM will mute above array bounds warning
        for (int i = 0; i < this.StageNumberRow && i < MaxStageNumber; ++i)
        {
            this.StageRangeRow[i] = (byte)(this.StageRangeRow[i] + shift[0] + shift[1] + bitDepth + 1);
        }
    }

    /// <summary>
    /// SVT: is_txfm_allowed
    /// </summary>
    public bool IsAllowed()
    {
        Av1TransformType[] supportedTypes =
            [
                Av1TransformType.DctDct,
                Av1TransformType.AdstDct,
                Av1TransformType.DctAdst,
                Av1TransformType.AdstAdst,
                Av1TransformType.FlipAdstDct,
                Av1TransformType.DctFlipAdst,
                Av1TransformType.FlipAdstFlipAdst,
                Av1TransformType.AdstFlipAdst,
                Av1TransformType.FlipAdstAdst,
                Av1TransformType.Identity,
                Av1TransformType.VerticalDct,
                Av1TransformType.HorizontalDct,
                Av1TransformType.VerticalAdst,
                Av1TransformType.HorizontalAdst,
                Av1TransformType.VerticalFlipAdst,
                Av1TransformType.HorizontalFlipAdst,
            ];

        switch (this.TransformSize)
        {
            case Av1TransformSize.Size32x32:
                supportedTypes = [Av1TransformType.DctDct, Av1TransformType.Identity, Av1TransformType.VerticalDct, Av1TransformType.HorizontalDct];
                break;
            case Av1TransformSize.Size32x64:
            case Av1TransformSize.Size64x32:
            case Av1TransformSize.Size16x64:
            case Av1TransformSize.Size64x16:
                supportedTypes = [Av1TransformType.DctDct];
                break;
            case Av1TransformSize.Size16x32:
            case Av1TransformSize.Size32x16:
            case Av1TransformSize.Size64x64:
            case Av1TransformSize.Size8x32:
            case Av1TransformSize.Size32x8:
                supportedTypes = [Av1TransformType.DctDct, Av1TransformType.Identity];
                break;
            default:
                break;
        }

        return supportedTypes.Contains(this.TransformType);
    }

    internal void SetShift(int shift0, int shift1, int shift2) => this.shift = [shift0, shift1, shift2];

    internal void SetFlip(bool upsideDown, bool leftToRight)
    {
        this.FlipUpsideDown = upsideDown;
        this.FlipLeftToRight = leftToRight;
    }

    private void SetFlip(Av1TransformType transformType)
    {
        switch (transformType)
        {
            case Av1TransformType.DctDct:
            case Av1TransformType.AdstDct:
            case Av1TransformType.DctAdst:
            case Av1TransformType.AdstAdst:
                this.FlipUpsideDown = false;
                this.FlipLeftToRight = false;
                break;
            case Av1TransformType.Identity:
            case Av1TransformType.VerticalDct:
            case Av1TransformType.HorizontalDct:
            case Av1TransformType.VerticalAdst:
            case Av1TransformType.HorizontalAdst:
                this.FlipUpsideDown = false;
                this.FlipLeftToRight = false;
                break;
            case Av1TransformType.FlipAdstDct:
            case Av1TransformType.FlipAdstAdst:
            case Av1TransformType.VerticalFlipAdst:
                this.FlipUpsideDown = true;
                this.FlipLeftToRight = false;
                break;
            case Av1TransformType.DctFlipAdst:
            case Av1TransformType.AdstFlipAdst:
            case Av1TransformType.HorizontalFlipAdst:
                this.FlipUpsideDown = false;
                this.FlipLeftToRight = true;
                break;
            case Av1TransformType.FlipAdstFlipAdst:
                this.FlipUpsideDown = true;
                this.FlipLeftToRight = true;
                break;
            default:
                Guard.IsTrue(false, nameof(transformType), "Unknown transform type for determining flip.");
                break;
        }
    }

    /// <summary>
    /// SVT: set_fwd_txfm_non_scale_range
    /// </summary>
    private void NonScaleRange()
    {
        if (this.TransformFunctionTypeColumn != Av1TransformFunctionType.Invalid)
        {
            Span<int> range_mult2_col = RangeMulti2List[(int)this.TransformFunctionTypeColumn];
            int stage_num_col = this.StageNumberColumn;
            for (int i = 0; i < stage_num_col; ++i)
            {
                this.StageRangeColumn[i] = (byte)((range_mult2_col[i] + 1) >> 1);
            }

            if (this.TransformFunctionTypeRow != Av1TransformFunctionType.Invalid)
            {
                int stage_num_row = this.StageNumberRow;
                Span<int> range_mult2_row = RangeMulti2List[(int)this.TransformFunctionTypeRow];
                for (int i = 0; i < stage_num_row; ++i)
                {
                    this.StageRangeRow[i] = (byte)((range_mult2_col[this.StageNumberColumn - 1] + range_mult2_row[i] + 1) >> 1);
                }
            }
        }
    }
}
