// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <summary>
/// Provides the separable row-and-column pipeline shared by AV1 forward two-dimensional transforms.
/// </summary>
internal abstract class Av1Forward2dTransformerBase
{
    /// <summary>
    /// The fixed-point representation of <c>sqrt(2)</c> at <see cref="NewSqrt2BitCount"/> fractional bits.
    /// </summary>
    internal const int NewSqrt2 = 5793;

    /// <summary>
    /// The number of fractional bits used by <see cref="NewSqrt2"/>.
    /// </summary>
    internal const int NewSqrt2BitCount = 12;

    /// <summary>
    /// Applies the separable column and row stages, including normative flips, shifts, and rectangular scaling.
    /// </summary>
    /// <typeparam name="TColumn">The column-transform implementation type.</typeparam>
    /// <typeparam name="TRow">The row-transform implementation type.</typeparam>
    /// <param name="transformFunctionColumn">The column-transform implementation.</param>
    /// <param name="transformFunctionRow">The row-transform implementation.</param>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="inputStride">The number of input samples between rows.</param>
    /// <param name="output">The destination transform coefficients and temporary axis buffers.</param>
    /// <param name="config">The per-axis transform, flip, shift, and range configuration.</param>
    /// <param name="buf">The transposed intermediate coefficient plane.</param>
    /// <param name="bitDepth">The source sample bit depth.</param>
    /// <remarks>Corresponds to <c>av1_tranform_two_d_core_c</c> in the original WIP reference.</remarks>
    protected static void Transform2dCore<TColumn, TRow>(TColumn transformFunctionColumn, TRow transformFunctionRow, Span<short> input, uint inputStride, Span<int> output, Av1Transform2dFlipConfiguration config, Span<int> buf, int bitDepth)
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
        ref short inputRef = ref input[0];
        ref int outputRef = ref output[0];
        ref int bufRef = ref buf[0];
        Span<int> tempInSpan = output.Slice(0, transformRowCount);
        Span<int> tempOutSpan = output.Slice(transformRowCount, transformRowCount);
        ref int tempIn = ref tempInSpan[0];
        ref int tempOut = ref tempOutSpan[0];

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
                    // flip upside down
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
        for (r = 0; r < transformRowCount; ++r)
        {
            transformFunctionRow.Transform(buf.Slice(r * transformColumnCount, transformColumnCount), output.Slice(r * transformColumnCount, transformColumnCount), cosBitRow, stageRangeRow);
            RoundShiftArray(ref Unsafe.Add(ref outputRef, r * transformColumnCount), transformColumnCount, -shift[2]);

            if (Math.Abs(rectangleType) == 1)
            {
                // Multiply everything by Sqrt2 if the transform is rectangular and the
                // size difference is a factor of 2.
                for (c = 0; c < transformColumnCount; ++c)
                {
                    ref int current = ref Unsafe.Add(ref outputRef, (r * transformColumnCount) + c);
                    current = Av1Math.RoundShift((long)current * NewSqrt2, NewSqrt2BitCount);
                }
            }
        }
    }

    /// <summary>
    /// Applies a signed fixed-point shift to a contiguous transform-stage vector.
    /// </summary>
    /// <param name="arr">A reference to the first transform-stage value.</param>
    /// <param name="size">The number of values to update.</param>
    /// <param name="bit">A positive rounded-right shift or a negative exact-left shift.</param>
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
    /// Gets the signed base-two ratio between transform columns and rows.
    /// </summary>
    /// <param name="col">The transform width.</param>
    /// <param name="row">The transform height.</param>
    /// <returns>Zero for square transforms, positive when wider, or negative when taller.</returns>
    /// <remarks>Corresponds to <c>get_rect_tx_log_ratio</c> in the original WIP reference.</remarks>
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
