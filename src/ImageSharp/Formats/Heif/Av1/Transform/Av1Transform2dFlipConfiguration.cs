// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Resolves an AV1 compound transform into its per-axis functions, flips, shifts, and stage ranges.
/// </summary>
internal class Av1Transform2dFlipConfiguration
{
    /// <summary>
    /// The maximum number of fixed-point stages in any supported one-dimensional transform.
    /// </summary>
    public const int MaxStageNumber = 12;

    /// <summary>
    /// The base-two logarithm of the smallest supported transform dimension.
    /// </summary>
    private const int SmallestTransformSizeLog2 = 2;

    /// <summary>
    /// Maps each compound transform type to the function applied down the transform columns.
    /// </summary>
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

    /// <summary>
    /// Maps each compound transform type to the function applied across the transform rows.
    /// </summary>
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

    /// <summary>
    /// Contains the three normative fixed-point shifts for every transform size.
    /// </summary>
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

    /// <summary>
    /// Selects column-transform cosine precision by width and height logarithm.
    /// </summary>
    private static readonly int[][] CosBitColumnMap =
        [[13, 13, 13, 0, 0], [13, 13, 13, 12, 0], [13, 13, 13, 12, 13], [0, 13, 13, 12, 13], [0, 0, 13, 12, 13]];

    /// <summary>
    /// Selects row-transform cosine precision by width and height logarithm.
    /// </summary>
    private static readonly int[][] CosBitRowMap =
        [[13, 13, 12, 0, 0], [13, 13, 13, 12, 0], [13, 13, 12, 13, 12], [0, 12, 13, 12, 11], [0, 0, 12, 11, 10]];

    /// <summary>
    /// Maps a transform dimension and one-dimensional type to its concrete staged function.
    /// </summary>
    private static readonly Av1TransformFunctionType[][] TransformFunctionTypeMap =
        [
            [Av1TransformFunctionType.Dct4, Av1TransformFunctionType.Adst4, Av1TransformFunctionType.Adst4, Av1TransformFunctionType.Identity4],
            [Av1TransformFunctionType.Dct8, Av1TransformFunctionType.Adst8, Av1TransformFunctionType.Adst8, Av1TransformFunctionType.Identity8],
            [Av1TransformFunctionType.Dct16, Av1TransformFunctionType.Adst16, Av1TransformFunctionType.Adst16, Av1TransformFunctionType.Identity16],
            [Av1TransformFunctionType.Dct32, Av1TransformFunctionType.Adst32, Av1TransformFunctionType.Adst32, Av1TransformFunctionType.Identity32],
            [Av1TransformFunctionType.Dct64, Av1TransformFunctionType.Invalid, Av1TransformFunctionType.Invalid, Av1TransformFunctionType.Identity64]
        ];

    /// <summary>
    /// Contains the number of fixed-point stages executed by each concrete transform function.
    /// </summary>
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

    /// <summary>
    /// Contains twice the non-scaled bit range required after every transform stage.
    /// </summary>
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

    /// <summary>
    /// The three fixed-point shifts applied before the column transform, between axes, and after the row transform.
    /// </summary>
    private int[] shift;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1Transform2dFlipConfiguration"/> class.
    /// </summary>
    /// <param name="transformType">The compound horizontal and vertical transform type.</param>
    /// <param name="transformSize">The transform-block dimensions.</param>
    public Av1Transform2dFlipConfiguration(Av1TransformType transformType, Av1TransformSize transformSize)
    {
        // SVT: svt_av1_get_inv_txfm_cfg
        // SVT: svt_aom_transform_config
        this.TransformSize = transformSize;
        this.TransformType = transformType;
        this.SetFlip(transformType);
        this.TransformTypeColumn = VerticalType[(int)transformType];
        this.TransformTypeRow = HorizontalType[(int)transformType];
        int transformWidthIndex = transformSize.GetBlockWidthLog2() - SmallestTransformSizeLog2;
        int transformHeightIndex = transformSize.GetBlockHeightLog2() - SmallestTransformSizeLog2;
        this.shift = ShiftMap[(int)transformSize];
        this.CosBitColumn = CosBitColumnMap[transformWidthIndex][transformHeightIndex];
        this.CosBitRow = CosBitRowMap[transformWidthIndex][transformHeightIndex];
        this.TransformFunctionTypeColumn = TransformFunctionTypeMap[transformHeightIndex][(int)this.TransformTypeColumn];
        this.TransformFunctionTypeRow = TransformFunctionTypeMap[transformWidthIndex][(int)this.TransformTypeRow];
        this.StageNumberColumn = this.TransformFunctionTypeColumn != Av1TransformFunctionType.Invalid ? StageNumberList[(int)this.TransformFunctionTypeColumn] : -1;
        this.StageNumberRow = this.TransformFunctionTypeRow != Av1TransformFunctionType.Invalid ? StageNumberList[(int)this.TransformFunctionTypeRow] : -1;
        this.StageRangeColumn = new byte[12];
        this.StageRangeRow = new byte[12];
        this.NonScaleRange();
    }

    /// <summary>
    /// Gets the fixed-point cosine precision used by the column transform.
    /// </summary>
    public int CosBitColumn { get; }

    /// <summary>
    /// Gets the fixed-point cosine precision used by the row transform.
    /// </summary>
    public int CosBitRow { get; }

    /// <summary>
    /// Gets the one-dimensional transform type applied down columns.
    /// </summary>
    public Av1TransformType1d TransformTypeColumn { get; }

    /// <summary>
    /// Gets the one-dimensional transform type applied across rows.
    /// </summary>
    public Av1TransformType1d TransformTypeRow { get; }

    /// <summary>
    /// Gets the concrete staged transform function applied down columns.
    /// </summary>
    public Av1TransformFunctionType TransformFunctionTypeColumn { get; }

    /// <summary>
    /// Gets the concrete staged transform function applied across rows.
    /// </summary>
    public Av1TransformFunctionType TransformFunctionTypeRow { get; }

    /// <summary>
    /// Gets the number of fixed-point stages in the column transform.
    /// </summary>
    public int StageNumberColumn { get; }

    /// <summary>
    /// Gets the number of fixed-point stages in the row transform.
    /// </summary>
    public int StageNumberRow { get; }

    /// <summary>
    /// Gets the transform-block dimensions.
    /// </summary>
    public Av1TransformSize TransformSize { get; }

    /// <summary>
    /// Gets the compound horizontal and vertical transform type.
    /// </summary>
    public Av1TransformType TransformType { get; }

    /// <summary>
    /// Gets a value indicating whether column input is traversed from bottom to top.
    /// </summary>
    public bool FlipUpsideDown { get; private set; }

    /// <summary>
    /// Gets a value indicating whether row output is written from right to left.
    /// </summary>
    public bool FlipLeftToRight { get; private set; }

    /// <summary>
    /// Gets the three fixed-point shifts applied by the two-dimensional transform pipeline.
    /// </summary>
    public Span<int> Shift => this.shift;

    /// <summary>
    /// Gets the allowed signed-bit range after each column-transform stage.
    /// </summary>
    public byte[] StageRangeColumn { get; }

    /// <summary>
    /// Gets the allowed signed-bit range after each row-transform stage.
    /// </summary>
    public byte[] StageRangeRow { get; }

    /// <summary>
    /// Adds input bit depth and inter-stage shifts to the non-scaled stage ranges.
    /// </summary>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <remarks>
    /// Corresponds to <c>svt_av1_gen_fwd_stage_range</c> and <c>svt_av1_gen_inv_stage_range</c>
    /// in the original WIP reference.
    /// </remarks>
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
    /// Determines whether the transform type is permitted for the configured dimensions.
    /// </summary>
    /// <returns><see langword="true"/> when the transform combination is valid for the transform size.</returns>
    /// <remarks>Corresponds to <c>is_txfm_allowed</c> in the original WIP reference.</remarks>
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

    /// <summary>
    /// Replaces the three transform-pipeline shifts.
    /// </summary>
    /// <param name="shift0">The pre-column-transform shift.</param>
    /// <param name="shift1">The shift between the column and row transforms.</param>
    /// <param name="shift2">The post-row-transform shift.</param>
    internal void SetShift(int shift0, int shift1, int shift2) => this.shift = [shift0, shift1, shift2];

    /// <summary>
    /// Overrides the axis traversal directions.
    /// </summary>
    /// <param name="upsideDown">Whether column input is traversed from bottom to top.</param>
    /// <param name="leftToRight">Whether row output is written from right to left.</param>
    internal void SetFlip(bool upsideDown, bool leftToRight)
    {
        this.FlipUpsideDown = upsideDown;
        this.FlipLeftToRight = leftToRight;
    }

    /// <summary>
    /// Derives the axis traversal directions encoded by a compound transform type.
    /// </summary>
    /// <param name="transformType">The compound transform type.</param>
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
    /// Initializes the per-stage signed-bit ranges before input depth and pipeline shifts are applied.
    /// </summary>
    /// <remarks>Corresponds to <c>set_fwd_txfm_non_scale_range</c> in the original WIP reference.</remarks>
    private void NonScaleRange()
    {
        if (this.TransformFunctionTypeColumn != Av1TransformFunctionType.Invalid)
        {
            Span<int> columnRangeTimesTwo = RangeMulti2List[(int)this.TransformFunctionTypeColumn];
            int columnStageCount = this.StageNumberColumn;
            for (int i = 0; i < columnStageCount; ++i)
            {
                this.StageRangeColumn[i] = (byte)((columnRangeTimesTwo[i] + 1) >> 1);
            }

            if (this.TransformFunctionTypeRow != Av1TransformFunctionType.Invalid)
            {
                int rowStageCount = this.StageNumberRow;
                Span<int> rowRangeTimesTwo = RangeMulti2List[(int)this.TransformFunctionTypeRow];
                for (int i = 0; i < rowStageCount; ++i)
                {
                    this.StageRangeRow[i] = (byte)((columnRangeTimesTwo[this.StageNumberColumn - 1] + rowRangeTimesTwo[i] + 1) >> 1);
                }
            }
        }
    }
}
