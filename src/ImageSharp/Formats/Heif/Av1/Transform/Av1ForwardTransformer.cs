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

    private static readonly IAv1ForwardTransformer?[] Transformers =
        [
            new Av1Dct4ForwardTransformer(),
            new Av1Dct8ForwardTransformer(),
            new Av1Dct16ForwardTransformer(),
            new Av1Dct32ForwardTransformer(),
            new Av1Dct64ForwardTransformer(),
            new Av1Adst4ForwardTransformer(),
            new Av1Adst8ForwardTransformer(),
            new Av1Adst16ForwardTransformer(),
            new Av1Adst32ForwardTransformer(),
            new Av1Identity4ForwardTransformer(),
            new Av1Identity8ForwardTransformer(),
            new Av1Identity16ForwardTransformer(),
            new Av1Identity32ForwardTransformer(),
            new Av1Identity64ForwardTransformer(),
            null
        ];

    private static readonly int[] TemporaryCoefficientsBuffer = new int[64 * 64];

    internal static void Transform2d(Span<short> input, Span<int> coefficients, uint stride, Av1TransformType transformType, Av1TransformSize transformSize, int bitDepth)
    {
        Av1Transform2dFlipConfiguration config = new(transformType, transformSize);
        ref int buffer = ref TemporaryCoefficientsBuffer[0];
        IAv1ForwardTransformer? columnTransformer = GetTransformer(config.TransformFunctionTypeColumn);
        IAv1ForwardTransformer? rowTransformer = GetTransformer(config.TransformFunctionTypeRow);
        if (columnTransformer != null && rowTransformer != null)
        {
            Transform2dCore(columnTransformer, rowTransformer, ref input[0], stride, ref coefficients[0], config, ref buffer, bitDepth);
        }
        else
        {
            throw new InvalidImageContentException($"Cannot find 1d transformer implementation for {config.TransformFunctionTypeColumn} or {config.TransformFunctionTypeRow}.");
        }
    }

    internal static void Transform2dAvx2(Span<short> input, Span<int> coefficients, uint stride, Av1TransformType transformType, Av1TransformSize transformSize, int bitDepth)
    {
        switch (transformSize)
        {
            case Av1TransformSize.Size4x4:
                // Too small for intrinsics, use the scalar codepath instead.
                Transform2d(input, coefficients, stride, transformType, transformSize, bitDepth);
                break;
            case Av1TransformSize.Size8x8:
                Transform8x8Avx2(input, coefficients, stride, transformType, bitDepth);
                break;
            default:
                Transform2d(input, coefficients, stride, transformType, transformSize, bitDepth);
                break;
        }
    }

    /// <summary>
    /// SVT: svt_av1_fwd_txfm2d_8x8_avx2
    /// </summary>
    private static void Transform8x8Avx2(Span<short> input, Span<int> coefficients, uint stride, Av1TransformType transformType, int bitDepth)
    {
        Av1Transform2dFlipConfiguration config = new(transformType, Av1TransformSize.Size8x8);
        Span<int> shift = config.Shift;
        Span<Vector256<int>> inVector = stackalloc Vector256<int>[8];
        Span<Vector256<int>> outVector = stackalloc Vector256<int>[8];
        ref Vector256<int> inRef = ref inVector[0];
        ref Vector256<int> outRef = ref outVector[0];
        switch (transformType)
        {
            case Av1TransformType.DctDct:
                /* Pseudo code
                Av1Dct8ForwardTransformer dct8 = new();
                LoadBuffer8x8(ref input[0], ref inRef, stride, 0, 0, shift[0]);
                dct8.TransformAvx2(ref inRef, ref outRef, config.CosBitColumn, 1);
                Column8x8Rounding(ref outRef, -shift[1]);
                Transpose8x8Avx2(ref outRef, ref inRef);
                dct8.TransformAvx2(ref inRef, ref outRef, config.CosBitRow, 1);
                Transpose8x8Avx2(ref outRef, ref inRef);
                WriteBuffer8x8(ref inRef, ref coefficients[0]);
                break;
                */
                throw new NotImplementedException();
            default:
                throw new NotImplementedException();
        }
    }

    private static IAv1ForwardTransformer? GetTransformer(Av1TransformFunctionType transformerType)
        => Transformers[(int)transformerType];

    /// <summary>
    /// SVT: av1_tranform_two_d_core_c
    /// </summary>
    private static void Transform2dCore<TColumn, TRow>(TColumn transformFunctionColumn, TRow transformFunctionRow, ref short input, uint inputStride, ref int output, Av1Transform2dFlipConfiguration config, ref int buf, int bitDepth)
            where TColumn : IAv1ForwardTransformer
            where TRow : IAv1ForwardTransformer
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
                    // flip upside down
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
            transformFunctionRow.Transform(ref Unsafe.Add(ref buf, r * transformColumnCount), ref Unsafe.Add(ref output, r * transformColumnCount), cosBitRow, stageRangeRow);
            RoundShiftArray(ref Unsafe.Add(ref output, r * transformColumnCount), transformColumnCount, -shift[2]);

            if (Math.Abs(rectangleType) == 1)
            {
                // Multiply everything by Sqrt2 if the transform is rectangular and the
                // size difference is a factor of 2.
                for (c = 0; c < transformColumnCount; ++c)
                {
                    ref int current = ref Unsafe.Add(ref output, (r * transformColumnCount) + c);
                    current = Av1Math.RoundShift((long)current * NewSqrt, NewSqrtBitCount);
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
