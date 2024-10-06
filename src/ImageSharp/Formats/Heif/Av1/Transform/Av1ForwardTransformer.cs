// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal class Av1ForwardTransformer
{
    private const int NewSqrt = 5793;
    private const int NewSqrtBitCount = 12;

    private static readonly IAv1Forward1dTransformer?[] Transformers =
        [
            new Av1Dct4Forward1dTransformer(),
            new Av1Dct8Forward1dTransformer(),
            new Av1Dct16Forward1dTransformer(),
            new Av1Dct32Forward1dTransformer(),
            new Av1Dct64Forward1dTransformer(),
            new Av1Adst4Forward1dTransformer(),
            new Av1Adst8Forward1dTransformer(),
            new Av1Adst16Forward1dTransformer(),
            new Av1Adst32Forward1dTransformer(),
            new Av1Identity4Forward1dTransformer(),
            new Av1Identity8Forward1dTransformer(),
            new Av1Identity16Forward1dTransformer(),
            new Av1Identity32Forward1dTransformer(),
            new Av1Identity64Forward1dTransformer(),
            null
        ];

    private static readonly int[] TemporaryCoefficientsBuffer = new int[Av1Constants.MaxTransformSize * Av1Constants.MaxTransformSize];

    internal static void Transform2d(Span<short> input, Span<int> coefficients, uint stride, Av1TransformType transformType, Av1TransformSize transformSize, int bitDepth)
    {
        Av1Transform2dFlipConfiguration config = new(transformType, transformSize);
        ref int buffer = ref TemporaryCoefficientsBuffer[0];
        IAv1Forward1dTransformer? columnTransformer = GetTransformer(config.TransformFunctionTypeColumn);
        IAv1Forward1dTransformer? rowTransformer = GetTransformer(config.TransformFunctionTypeRow);
        if (columnTransformer != null && rowTransformer != null)
        {
            Transform2dCore(columnTransformer, rowTransformer, ref input[0], stride, ref coefficients[0], config, ref buffer, bitDepth);
        }
        else
        {
            throw new InvalidImageContentException($"Cannot find 1d transformer implementation for {config.TransformFunctionTypeColumn} or {config.TransformFunctionTypeRow}.");
        }
    }

    private static IAv1Forward1dTransformer? GetTransformer(Av1TransformFunctionType transformerType)
        => Transformers[(int)transformerType];

    /// <summary>
    /// SVT: av1_tranform_two_d_core_c
    /// </summary>
    private static void Transform2dCore<TColumn, TRow>(TColumn transformFunctionColumn, TRow transformFunctionRow, ref short input, uint inputStride, ref int output, Av1Transform2dFlipConfiguration config, ref int buf, int bitDepth)
            where TColumn : IAv1Forward1dTransformer
            where TRow : IAv1Forward1dTransformer
    {
        int c, r;

        // Note when assigning txfm_size_col, we use the txfm_size from the
        // row configuration and vice versa. This is intentionally done to
        // accurately perform rectangular transforms. When the transform is
        // rectangular, the number of columns will be the same as the
        // txfm_size stored in the row cfg struct. It will make no difference
        // for square transforms.
        int transformColumnCount = config.TransformSize.GetWidth();
        int transformRowCount = config.TransformSize.GetHeight();
        int transformCount = transformColumnCount * transformRowCount;

        // Take the shift from the larger dimension in the rectangular case.
        Span<int> shift = config.Shift;
        int rectangleType = GetRectangularRatio(transformColumnCount, transformRowCount);
        Span<byte> stageRangeColumn = stackalloc byte[Av1Transform2dFlipConfiguration.MaxStageNumber];
        Span<byte> stageRangeRow = stackalloc byte[Av1Transform2dFlipConfiguration.MaxStageNumber];

        // assert(cfg->stage_num_col <= MAX_TXFM_STAGE_NUM);
        // assert(cfg->stage_num_row <= MAX_TXFM_STAGE_NUM);
        config.GenerateStageRange(bitDepth);

        int cosBitColumn = config.CosBitColumn;
        int cosBitRow = config.CosBitRow;

        // ASSERT(txfm_func_col != NULL);
        // ASSERT(txfm_func_row != NULL);
        // use output buffer as temp buffer
        ref int tempIn = ref output;
        ref int tempOut = ref Unsafe.Add(ref output, transformRowCount);

        // Columns
        for (c = 0; c < transformColumnCount; ++c)
        {
            if (!config.FlipUpsideDown)
            {
                uint t = (uint)c;
                for (r = 0; r < transformRowCount; ++r)
                {
                    Unsafe.Add(ref tempIn, r) = Unsafe.Add(ref input, t);
                    t += inputStride;
                }
            }
            else
            {
                uint t = (uint)(c + ((transformRowCount - 1) * (int)inputStride));
                for (r = 0; r < transformRowCount; ++r)
                {
                    // Flip upside down
                    Unsafe.Add(ref tempIn, r) = Unsafe.Add(ref input, t);
                    t -= inputStride;
                }
            }

            RoundShiftArray(ref tempIn, transformRowCount, -shift[0]); // NM svt_av1_round_shift_array_c
            transformFunctionColumn.Transform(ref tempIn, ref tempOut, cosBitColumn, stageRangeColumn);
            RoundShiftArray(ref tempOut, transformRowCount, -shift[1]); // NM svt_av1_round_shift_array_c
            if (!config.FlipLeftToRight)
            {
                int t = c;
                for (r = 0; r < transformRowCount; ++r)
                {
                    Unsafe.Add(ref buf, t) = Unsafe.Add(ref tempOut, r);
                    t += transformColumnCount;
                }
            }
            else
            {
                int t = transformColumnCount - c - 1;
                for (r = 0; r < transformRowCount; ++r)
                {
                    // flip from left to right
                    Unsafe.Add(ref buf, t) = Unsafe.Add(ref tempOut, r);
                    t += transformColumnCount;
                }
            }
        }

        // Rows
        for (r = 0; r < transformRowCount; ++r)
        {
            transformFunctionRow.Transform(
                ref Unsafe.Add(ref buf, r * transformColumnCount),
                ref Unsafe.Add(ref output, r * transformColumnCount),
                cosBitRow,
                stageRangeRow);
            RoundShiftArray(ref Unsafe.Add(ref output, r * transformColumnCount), transformColumnCount, -shift[2]);

            if (Math.Abs(rectangleType) == 1)
            {
                // Multiply everything by Sqrt2 if the transform is rectangular and the
                // size difference is a factor of 2.
                int t = r * transformColumnCount;
                for (c = 0; c < transformColumnCount; ++c)
                {
                    ref int current = ref Unsafe.Add(ref output, t);
                    current = Av1Math.RoundShift((long)current * NewSqrt, NewSqrtBitCount);
                    t++;
                }
            }
        }
    }

    private static void RoundShiftArray(ref int arr, int size, int bit)
    {
        if (bit == 0)
        {
            return;
        }
        else
        {
            nuint sz = (nuint)size;
            if (bit > 0)
            {
                for (nuint i = 0; i < sz; i++)
                {
                    ref int a = ref Unsafe.Add(ref arr, i);
                    a = Av1Math.RoundShift(a, bit);
                }
            }
            else
            {
                for (nuint i = 0; i < sz; i++)
                {
                    ref int a = ref Unsafe.Add(ref arr, i);
                    a *= 1 << (-bit);
                }
            }
        }
    }

    /// <summary>
    /// SVT: get_rect_tx_log_ratio
    /// </summary>
    public static int GetRectangularRatio(int col, int row)
    {
        if (col == row)
        {
            return 0;
        }

        if (col > row)
        {
            if (col == row * 2)
            {
                return 1;
            }

            if (col == row * 4)
            {
                return 2;
            }

            Guard.IsTrue(false, nameof(row), "Unsupported transform size");
        }
        else
        {
            if (row == col * 2)
            {
                return -1;
            }

            if (row == col * 4)
            {
                return -2;
            }

            Guard.IsTrue(false, nameof(row), "Unsupported transform size");
        }

        return 0; // Invalid
    }
}
