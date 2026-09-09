// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Resolves an AV1 compound transform into its per-axis functions, flips, shifts, and stage ranges.
/// </summary>
internal ref struct Av1Transform2dFlipConfiguration
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
    /// The fixed-point cosine precision used by every inverse transform.
    /// </summary>
    private const int InverseCosBit = 12;

    /// <summary>
    /// The fixed-point shifts applied between successive stages of the configured transform pipeline.
    /// </summary>
    private InlineArray3<int> shift;

    /// <summary>
    /// The signed-bit ranges produced by the column transform stages.
    /// </summary>
    private InlineArray12<byte> stageRangeColumn;

    /// <summary>
    /// The signed-bit ranges produced by the row transform stages.
    /// </summary>
    private InlineArray12<byte> stageRangeRow;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1Transform2dFlipConfiguration"/> struct.
    /// </summary>
    /// <param name="transformType">The compound horizontal and vertical transform type.</param>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="isForward">Whether to configure the forward transform pipeline.</param>
    private Av1Transform2dFlipConfiguration(Av1TransformType transformType, Av1TransformSize transformSize, int bitDepth, bool isForward)
    {
        this = default;

        // Resolve the axis operators and fixed-point settings once so the hot traversal contains no per-row
        // transform-type lookup or flip decision.
        this.TransformSize = transformSize;
        this.TransformType = transformType;
        this.NonzeroWidth = Math.Min(transformSize.GetWidth(), 32);
        this.NonzeroHeight = Math.Min(transformSize.GetHeight(), 32);
        this.SetFlip(transformType);
        this.TransformTypeColumn = VerticalType[(int)transformType];
        this.TransformTypeRow = HorizontalType[(int)transformType];
        int transformWidthIndex = transformSize.GetBlockWidthLog2() - SmallestTransformSizeLog2;
        int transformHeightIndex = transformSize.GetBlockHeightLog2() - SmallestTransformSizeLog2;
        this.TransformFunctionTypeColumn = TransformFunctionTypeMap[(transformHeightIndex * 4) + (int)this.TransformTypeColumn];
        this.TransformFunctionTypeRow = TransformFunctionTypeMap[(transformWidthIndex * 4) + (int)this.TransformTypeRow];
        this.StageNumberColumn = this.TransformFunctionTypeColumn != Av1TransformFunctionType.Invalid ? StageNumberList[(int)this.TransformFunctionTypeColumn] : -1;
        this.StageNumberRow = this.TransformFunctionTypeRow != Av1TransformFunctionType.Invalid ? StageNumberList[(int)this.TransformFunctionTypeRow] : -1;

        if (isForward)
        {
            int shiftIndex = (int)transformSize * 3;
            this.shift[0] = ForwardShiftMap[shiftIndex];
            this.shift[1] = ForwardShiftMap[shiftIndex + 1];
            this.shift[2] = ForwardShiftMap[shiftIndex + 2];
            this.CosBitColumn = ForwardCosBitColumnMap[(transformWidthIndex * 5) + transformHeightIndex];
            this.CosBitRow = ForwardCosBitRowMap[(transformWidthIndex * 5) + transformHeightIndex];
            this.InitializeForwardStageRange();
            this.GenerateForwardStageRange(bitDepth);
        }
        else
        {
            int shiftIndex = (int)transformSize * 2;
            this.shift[0] = InverseShiftMap[shiftIndex];
            this.shift[1] = InverseShiftMap[shiftIndex + 1];
            this.CosBitColumn = InverseCosBit;
            this.CosBitRow = InverseCosBit;
            this.GenerateInverseStageRange(bitDepth);
        }
    }

    /// <summary>
    /// Gets the function applied down the transform columns for each compound transform type.
    /// </summary>
    private static ReadOnlySpan<Av1TransformType1d> VerticalType =>
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
    /// Gets the function applied across the transform rows for each compound transform type.
    /// </summary>
    private static ReadOnlySpan<Av1TransformType1d> HorizontalType =>
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
    /// Gets the three normative forward fixed-point shifts for every transform size.
    /// </summary>
    private static ReadOnlySpan<int> ForwardShiftMap =>
        [
            2, 0, 0, // 4x4
            2, -1, 0, // 8x8
            2, -2, 0, // 16x16
            2, -4, 0, // 32x32
            0, -2, -2, // 64x64
            2, -1, 0, // 4x8
            2, -1, 0, // 8x4
            2, -2, 0, // 8x16
            2, -2, 0, // 16x8
            2, -4, 0, // 16x32
            2, -4, 0, // 32x16
            0, -2, -2, // 32x64
            2, -4, -2, // 64x32
            2, -1, 0, // 4x16
            2, -1, 0, // 16x4
            2, -2, 0, // 8x32
            2, -2, 0, // 32x8
            0, -2, 0, // 16x64
            2, -4, 0, // 64x16
        ];

    /// <summary>
    /// Gets the two normative inverse fixed-point shifts for every transform size.
    /// </summary>
    private static ReadOnlySpan<int> InverseShiftMap =>
        [
            0, -4, // 4x4
            -1, -4, // 8x8
            -2, -4, // 16x16
            -2, -4, // 32x32
            -2, -4, // 64x64
            0, -4, // 4x8
            0, -4, // 8x4
            -1, -4, // 8x16
            -1, -4, // 16x8
            -1, -4, // 16x32
            -1, -4, // 32x16
            -1, -4, // 32x64
            -1, -4, // 64x32
            -1, -4, // 4x16
            -1, -4, // 16x4
            -2, -4, // 8x32
            -2, -4, // 32x8
            -2, -4, // 16x64
            -2, -4, // 64x16
        ];

    /// <summary>
    /// Gets column-transform cosine precision by width and height logarithm.
    /// </summary>
    private static ReadOnlySpan<int> ForwardCosBitColumnMap =>
    [
        13, 13, 13, 0, 0,
        13, 13, 13, 12, 0,
        13, 13, 13, 12, 13,
        0, 13, 13, 12, 13,
        0, 0, 13, 12, 13,
    ];

    /// <summary>
    /// Gets row-transform cosine precision by width and height logarithm.
    /// </summary>
    private static ReadOnlySpan<int> ForwardCosBitRowMap =>
    [
        13, 13, 12, 0, 0,
        13, 13, 13, 12, 0,
        13, 13, 12, 13, 12,
        0, 12, 13, 12, 11,
        0, 0, 12, 11, 10,
    ];

    /// <summary>
    /// Gets the concrete staged function for each transform dimension and one-dimensional type.
    /// </summary>
    private static ReadOnlySpan<Av1TransformFunctionType> TransformFunctionTypeMap =>
        [
            Av1TransformFunctionType.Dct4, Av1TransformFunctionType.Adst4, Av1TransformFunctionType.Adst4, Av1TransformFunctionType.Identity4,
            Av1TransformFunctionType.Dct8, Av1TransformFunctionType.Adst8, Av1TransformFunctionType.Adst8, Av1TransformFunctionType.Identity8,
            Av1TransformFunctionType.Dct16, Av1TransformFunctionType.Adst16, Av1TransformFunctionType.Adst16, Av1TransformFunctionType.Identity16,
            Av1TransformFunctionType.Dct32, Av1TransformFunctionType.Invalid, Av1TransformFunctionType.Invalid, Av1TransformFunctionType.Identity32,
            Av1TransformFunctionType.Dct64, Av1TransformFunctionType.Invalid, Av1TransformFunctionType.Invalid, Av1TransformFunctionType.Invalid,
        ];

    /// <summary>
    /// Gets the number of fixed-point stages executed by each concrete transform function.
    /// </summary>
    private static ReadOnlySpan<int> StageNumberList =>
        [
            4, // TXFM_TYPE_DCT4
            6, // TXFM_TYPE_DCT8
            8, // TXFM_TYPE_DCT16
            10, // TXFM_TYPE_DCT32
            12, // TXFM_TYPE_DCT64
            7, // TXFM_TYPE_ADST4
            8, // TXFM_TYPE_ADST8
            10, // TXFM_TYPE_ADST16
            1, // TXFM_TYPE_IDENTITY4
            1, // TXFM_TYPE_IDENTITY8
            1, // TXFM_TYPE_IDENTITY16
            1, // TXFM_TYPE_IDENTITY32
        ];

    /// <summary>
    /// Gets twice the non-scaled bit range required after every transform stage.
    /// </summary>
    private static ReadOnlySpan<int> RangeMulti2Map =>
        [
            0, 2, 3, 3, 0, 0, 0, 0, 0, 0, 0, 0, // fdct4_range_mult2
            0, 2, 4, 5, 5, 5, 0, 0, 0, 0, 0, 0, // fdct8_range_mult2
            0, 2, 4, 6, 7, 7, 7, 7, 0, 0, 0, 0, // fdct16_range_mult2
            0, 2, 4, 6, 8, 9, 9, 9, 9, 9, 0, 0, // fdct32_range_mult2
            0, 2, 4, 6, 8, 10, 11, 11, 11, 11, 11, 11, // fdct64_range_mult2
            0, 2, 4, 3, 3, 3, 3, 0, 0, 0, 0, 0, // fadst4_range_mult2
            0, 0, 1, 3, 3, 5, 5, 5, 0, 0, 0, 0, // fadst8_range_mult2
            0, 0, 1, 3, 3, 5, 5, 7, 7, 7, 0, 0, // fadst16_range_mult2
            1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // fidtx4_range_mult2
            2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // fidtx8_range_mult2
            3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // fidtx16_range_mult2
            4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // fidtx32_range_mult2
        ];

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
    /// Gets the exclusive horizontal bound of potentially nonzero coefficients.
    /// </summary>
    public int NonzeroWidth { get; private set; }

    /// <summary>
    /// Gets the exclusive vertical bound of potentially nonzero coefficients.
    /// </summary>
    public int NonzeroHeight { get; private set; }

    /// <summary>
    /// Gets a value indicating whether column input is traversed from bottom to top.
    /// </summary>
    public bool FlipUpsideDown { get; private set; }

    /// <summary>
    /// Gets a value indicating whether row output is written from right to left.
    /// </summary>
    public bool FlipLeftToRight { get; private set; }

    /// <summary>
    /// Gets the first fixed-point pipeline shift.
    /// </summary>
    public readonly int Shift0 => this.shift[0];

    /// <summary>
    /// Gets the second fixed-point pipeline shift.
    /// </summary>
    public readonly int Shift1 => this.shift[1];

    /// <summary>
    /// Gets the terminal forward-transform shift, or zero for an inverse transform.
    /// </summary>
    public readonly int Shift2 => this.shift[2];

    /// <summary>
    /// Gets the allowed signed-bit range after each column-transform stage.
    /// </summary>
    public readonly InlineArray12<byte> StageRangeColumn => this.stageRangeColumn;

    /// <summary>
    /// Gets the allowed signed-bit range after each row-transform stage.
    /// </summary>
    public readonly InlineArray12<byte> StageRangeRow => this.stageRangeRow;

    /// <summary>
    /// Creates the configuration used to transform spatial residuals into coefficients.
    /// </summary>
    /// <param name="transformType">The compound horizontal and vertical transform type.</param>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <returns>The forward transform configuration.</returns>
    public static Av1Transform2dFlipConfiguration CreateForward(Av1TransformType transformType, Av1TransformSize transformSize, int bitDepth)
        => new(transformType, transformSize, bitDepth, true);

    /// <summary>
    /// Creates the configuration used to reconstruct samples from transform coefficients.
    /// </summary>
    /// <param name="transformType">The compound horizontal and vertical transform type.</param>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <returns>The inverse transform configuration.</returns>
    public static Av1Transform2dFlipConfiguration CreateInverse(Av1TransformType transformType, Av1TransformSize transformSize, int bitDepth)
        => new(transformType, transformSize, bitDepth, false);

    /// <summary>
    /// Selects inverse-transform coefficient bounds from the last coded scan position.
    /// </summary>
    /// <param name="endOfBuffer">The number of scan positions through the last nonzero coefficient.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    public void ConfigureInverseSparsity(int endOfBuffer, int bitDepth)
    {
        int width = this.NonzeroWidth;
        int height = this.NonzeroHeight;

        // Four-point axes use complete kernels. A two-axis identity transform retains the complete coefficient
        // rectangle because each input maps directly to one output instead of spreading across the other axis.
        if (width == 4 || height == 4 || this.TransformType == Av1TransformType.Identity)
        {
            return;
        }

        // The sparse kernels accept one, eight, sixteen, or thirty-two inputs. Each entry rounds a last coefficient
        // index up to the corresponding exclusive bound, preserving every coded coefficient in the scan prefix.
        ReadOnlySpan<byte> extentByLastIndex =
        [
            1, 8, 8, 8, 8, 8, 8, 8, 16, 16, 16, 16, 16, 16, 16, 16,
            32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32,
        ];

        int last = endOfBuffer - 1;
        if (this.TransformTypeRow == Av1TransformType1d.Identity)
        {
            // The byte path follows the row scan in both dimensions. The wider path keeps the entire identity axis
            // and uses a conservative column bound, which must not also limit the identity axis.
            if (bitDepth == 8)
            {
                this.NonzeroWidth = last >= width - 1 ? width : extentByLastIndex[last];
                this.NonzeroHeight = extentByLastIndex[last / width];
            }
            else
            {
                this.NonzeroHeight = last >= height - 1 ? height : extentByLastIndex[last];
            }

            return;
        }

        if (this.TransformTypeColumn == Av1TransformType1d.Identity)
        {
            // The column scan advances down the block first. The wider pipeline retains the full identity axis
            // while only the horizontal transform selects a reduced input network.
            if (bitDepth == 8)
            {
                this.NonzeroWidth = extentByLastIndex[last / height];
                this.NonzeroHeight = last >= height - 1 ? height : extentByLastIndex[last];
            }
            else
            {
                this.NonzeroWidth = last >= width - 1 ? width : extentByLastIndex[last];
            }

            return;
        }

        if (endOfBuffer == 1)
        {
            this.NonzeroWidth = 1;
            this.NonzeroHeight = 1;
            return;
        }

        // Diagonal scan bounds use complete scan-prefix rows, not the raster row of the final coefficient. The low
        // byte stores the last column and the high byte stores the last row. Adjusted dimensions keep sixty-four-point
        // transforms within their coded thirty-two-frequency rectangle.
        ReadOnlySpan<ushort> bounds = this.TransformSize.GetAdjusted() switch
        {
            Av1TransformSize.Size8x8 =>
            [
                0x0707, 0x0707, 0x0707, 0x0707, 0x0707, 0x0707, 0x0707, 0x0707,
            ],
            Av1TransformSize.Size16x16 =>
            [
                0x0707, 0x0707, 0x0F0F, 0x0F0F, 0x0F0F, 0x0F0F, 0x0F0F, 0x0F0F,
                0x0F0F, 0x0F0F, 0x0F0F, 0x0F0F, 0x0F0F, 0x0F0F, 0x0F0F, 0x0F0F,
            ],
            Av1TransformSize.Size8x16 =>
            [
                0x0707, 0x0707, 0x0707, 0x0707, 0x0707, 0x0F07, 0x0F07, 0x0F07,
                0x0F07, 0x0F07, 0x0F07, 0x0F07, 0x0F07, 0x0F07, 0x0F07, 0x0F07,
            ],
            Av1TransformSize.Size16x8 =>
            [
                0x0707, 0x0707, 0x070F, 0x070F, 0x070F, 0x070F, 0x070F, 0x070F,
            ],
            Av1TransformSize.Size16x32 =>
            [
                0x0707, 0x0707, 0x0F0F, 0x0F0F, 0x0F0F, 0x0F0F, 0x0F0F, 0x0F0F,
                0x0F0F, 0x1F0F, 0x1F0F, 0x1F0F, 0x1F0F, 0x1F0F, 0x1F0F, 0x1F0F,
                0x1F0F, 0x1F0F, 0x1F0F, 0x1F0F, 0x1F0F, 0x1F0F, 0x1F0F, 0x1F0F,
                0x1F0F, 0x1F0F, 0x1F0F, 0x1F0F, 0x1F0F, 0x1F0F, 0x1F0F, 0x1F0F,
            ],
            Av1TransformSize.Size32x16 =>
            [
                0x0707, 0x0F0F, 0x0F0F, 0x0F0F, 0x0F1F, 0x0F1F, 0x0F1F, 0x0F1F,
                0x0F1F, 0x0F1F, 0x0F1F, 0x0F1F, 0x0F1F, 0x0F1F, 0x0F1F, 0x0F1F,
            ],
            Av1TransformSize.Size8x32 =>
            [
                0x0707, 0x0707, 0x0707, 0x0707, 0x0707, 0x0F07, 0x0F07, 0x0F07,
                0x0F07, 0x0F07, 0x0F07, 0x0F07, 0x0F07, 0x1F07, 0x1F07, 0x1F07,
                0x1F07, 0x1F07, 0x1F07, 0x1F07, 0x1F07, 0x1F07, 0x1F07, 0x1F07,
                0x1F07, 0x1F07, 0x1F07, 0x1F07, 0x1F07, 0x1F07, 0x1F07, 0x1F07,
            ],
            Av1TransformSize.Size32x8 =>
            [
                0x0707, 0x070F, 0x070F, 0x071F, 0x071F, 0x071F, 0x071F, 0x071F,
            ],
            _ =>
            [
                0x0707, 0x0F0F, 0x0F0F, 0x0F0F, 0x1F1F, 0x1F1F, 0x1F1F, 0x1F1F,
                0x1F1F, 0x1F1F, 0x1F1F, 0x1F1F, 0x1F1F, 0x1F1F, 0x1F1F, 0x1F1F,
                0x1F1F, 0x1F1F, 0x1F1F, 0x1F1F, 0x1F1F, 0x1F1F, 0x1F1F, 0x1F1F,
                0x1F1F, 0x1F1F, 0x1F1F, 0x1F1F, 0x1F1F, 0x1F1F, 0x1F1F, 0x1F1F,
            ],
        };

        int bound = bounds[last / width];
        this.NonzeroWidth = (bound & 255) + 1;
        this.NonzeroHeight = (bound >> 8) + 1;
    }

    /// <summary>
    /// Determines whether the transform type is permitted for the configured dimensions.
    /// </summary>
    /// <returns><see langword="true"/> when the transform combination is valid for the transform size.</returns>
    public bool IsAllowed()
    {
        // AV1 selects the legal transform set from the block's square-up size: blocks up to 16x16 allow all sixteen
        // types, a 32x32 square-up allows DCT and identity, and larger square-up sizes allow DCT only.
        return this.TransformSize switch
        {
            Av1TransformSize.Size32x32 or Av1TransformSize.Size16x32 or Av1TransformSize.Size32x16 or
                Av1TransformSize.Size8x32 or Av1TransformSize.Size32x8 =>
                this.TransformType is Av1TransformType.DctDct or Av1TransformType.Identity,
            Av1TransformSize.Size64x64 or Av1TransformSize.Size32x64 or Av1TransformSize.Size64x32 or
                Av1TransformSize.Size16x64 or Av1TransformSize.Size64x16 => this.TransformType == Av1TransformType.DctDct,
            _ => true,
        };
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
    private void InitializeForwardStageRange()
    {
        if (this.TransformFunctionTypeColumn != Av1TransformFunctionType.Invalid)
        {
            int columnRangeOffset = (int)this.TransformFunctionTypeColumn * MaxStageNumber;
            int columnStageCount = this.StageNumberColumn;

            for (int i = 0; i < columnStageCount; ++i)
            {
                this.stageRangeColumn[i] = (byte)((RangeMulti2Map[columnRangeOffset + i] + 1) >> 1);
            }

            if (this.TransformFunctionTypeRow != Av1TransformFunctionType.Invalid)
            {
                int rowStageCount = this.StageNumberRow;
                int rowRangeOffset = (int)this.TransformFunctionTypeRow * MaxStageNumber;
                int columnRange = RangeMulti2Map[columnRangeOffset + this.StageNumberColumn - 1];

                for (int i = 0; i < rowStageCount; ++i)
                {
                    this.stageRangeRow[i] = (byte)((columnRange + RangeMulti2Map[rowRangeOffset + i] + 1) >> 1);
                }
            }
        }
    }

    /// <summary>
    /// Adds input bit depth and inter-stage shifts to the non-scaled forward stage ranges.
    /// </summary>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    private void GenerateForwardStageRange(int bitDepth)
    {
        for (int i = 0; i < this.StageNumberColumn; ++i)
        {
            this.stageRangeColumn[i] = (byte)(this.stageRangeColumn[i] + this.Shift0 + bitDepth + 1);
        }

        for (int i = 0; i < this.StageNumberRow; ++i)
        {
            this.stageRangeRow[i] = (byte)(this.stageRangeRow[i] + this.Shift0 + this.Shift1 + bitDepth + 1);
        }
    }

    /// <summary>
    /// Sets the optimized inverse stage ranges used to clamp intermediate values at the coded bit depth.
    /// </summary>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    private void GenerateInverseStageRange(int bitDepth)
    {
        byte rowRange = bitDepth switch
        {
            8 => 16,
            10 => 18,
            _ => 20,
        };

        byte columnRange = bitDepth == 12 ? (byte)18 : (byte)16;

        for (int i = 0; i < this.StageNumberColumn; ++i)
        {
            this.stageRangeColumn[i] = columnRange;
        }

        for (int i = 0; i < this.StageNumberRow; ++i)
        {
            this.stageRangeRow[i] = rowRange;
        }
    }
}
