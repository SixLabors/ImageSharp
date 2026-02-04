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

    private static readonly IAv1Transformer1d?[] Transformers =
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
        IAv1Transformer1d? columnTransformer = GetTransformer(config.TransformFunctionTypeColumn);
        IAv1Transformer1d? rowTransformer = GetTransformer(config.TransformFunctionTypeRow);
        Transform2d(columnTransformer, rowTransformer, input, coefficients, stride, config, bitDepth);
    }

    internal static void Transform2d<TColumn, TRow>(TColumn? transformFunctionColumn, TRow? transformFunctionRow, Span<short> input, Span<int> coefficients, uint stride, Av1Transform2dFlipConfiguration config, int bitDepth)
            where TColumn : IAv1Transformer1d
            where TRow : IAv1Transformer1d
    {
        if (transformFunctionColumn != null && transformFunctionRow != null)
        {
            Transform2dCore(transformFunctionColumn, transformFunctionRow, input, stride, coefficients, config, TemporaryCoefficientsBuffer, bitDepth);
        }
        else
        {
            throw new InvalidImageContentException($"Cannot find 1d transformer implementation for {config.TransformFunctionTypeColumn} or {config.TransformFunctionTypeRow}.");
        }
    }

    private static IAv1Transformer1d? GetTransformer(Av1TransformFunctionType transformerType)
        => Transformers[(int)transformerType];

    /// <summary>
    /// SVT: av1_tranform_two_d_core_c
    /// </summary>
    private static void Transform2dCore<TColumn, TRow>(TColumn transformFunctionColumn, TRow transformFunctionRow, Span<short> input, uint inputStride, Span<int> output, Av1Transform2dFlipConfiguration config, Span<int> buf, int bitDepth)
            where TColumn : IAv1Transformer1d
            where TRow : IAv1Transformer1d
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
        Span<int> tempInSpan = output[..transformRowCount];
        Span<int> tempOutSpan = output.Slice(transformRowCount, transformRowCount);
        ref int tempIn = ref tempInSpan[0];
        ref int tempOut = ref tempOutSpan[0];
        ref short inputRef = ref input[0];
        ref int outputRef = ref output[0];
        ref int bufRef = ref buf[0];

        // Columns
        for (c = 0; c < transformColumnCount; ++c)
        {
            if (!config.FlipUpsideDown)
            {
                uint t = (uint)c;
                for (r = 0; r < transformRowCount; ++r)
                {
                    Unsafe.Add(ref tempIn, r) = Unsafe.Add(ref inputRef, t);
                    t += inputStride;
                }
            }
            else
            {
                uint t = (uint)(c + ((transformRowCount - 1) * (int)inputStride));
                for (r = 0; r < transformRowCount; ++r)
                {
                    // Flip upside down
                    Unsafe.Add(ref tempIn, r) = Unsafe.Add(ref inputRef, t);
                    t -= inputStride;
                }
            }

            RoundShiftArray(ref tempIn, transformRowCount, -shift[0]); // NM svt_av1_round_shift_array_c
            transformFunctionColumn.Transform(tempInSpan, tempOutSpan, cosBitColumn, stageRangeColumn);
            RoundShiftArray(ref tempOut, transformRowCount, -shift[1]); // NM svt_av1_round_shift_array_c
            if (!config.FlipLeftToRight)
            {
                int t = c;
                for (r = 0; r < transformRowCount; ++r)
                {
                    Unsafe.Add(ref bufRef, t) = Unsafe.Add(ref tempOut, r);
                    t += transformColumnCount;
                }
            }
            else
            {
                int t = transformColumnCount - c - 1;
                for (r = 0; r < transformRowCount; ++r)
                {
                    // flip from left to right
                    Unsafe.Add(ref bufRef, t) = Unsafe.Add(ref tempOut, r);
                    t += transformColumnCount;
                }
            }
        }

        // Rows
        for (r = 0; r < transformCount; r += transformColumnCount)
        {
            transformFunctionRow.Transform(
                buf.Slice(r, transformColumnCount),
                output.Slice(r, transformColumnCount),
                cosBitRow,
                stageRangeRow);
            RoundShiftArray(ref Unsafe.Add(ref outputRef, r), transformColumnCount, -shift[2]);

            if (Math.Abs(rectangleType) == 1)
            {
                // Multiply everything by Sqrt2 if the transform is rectangular and the
                // size difference is a factor of 2.
                int t = r;
                for (c = 0; c < transformColumnCount; ++c)
                {
                    ref int current = ref Unsafe.Add(ref outputRef, t);
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
