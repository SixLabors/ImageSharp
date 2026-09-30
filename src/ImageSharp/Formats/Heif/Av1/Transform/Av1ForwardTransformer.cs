// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Converts spatial residual samples into AV1 transform coefficients.
/// </summary>
/// <remarks>
/// The SIMD pipeline transposes rows into lanes before invoking the one-dimensional operators. One vector then holds
/// the same transform position from several independent axes, allowing the complete stage network to run lane-wise.
/// Eight-bit blocks use saturating 16-bit stages where their normative ranges permit it; high-bit-depth and scalar
/// fallback paths retain 32-bit stages. Both representations produce the same row-major coefficient contract.
/// </remarks>
internal static partial class Av1ForwardTransformer
{
    /// <summary>
    /// Computes the unnormalized eight-by-eight Hadamard magnitude used to screen intra prediction candidates.
    /// </summary>
    /// <param name="residual">The sixty-four packed prediction residuals.</param>
    /// <param name="workspace">The containing block's reusable transform scratch.</param>
    /// <returns>The sum of absolute transformed residuals at their original sample precision.</returns>
    public static int GetHadamard8x8Cost(ReadOnlySpan<short> residual, Span<int> workspace)
    {
        const int width = 8;
        const int sampleCount = width * width;
        Span<int> columns = workspace[..sampleCount];
        Span<int> rows = workspace.Slice(sampleCount, sampleCount);
        Span<int> temporary = workspace.Slice(2 * sampleCount, width);

        // Keep both passes in Int32: twelve-bit residuals can produce coefficients of magnitude 262080. Widening
        // once lets the existing vectorized tensor operations serve all three sample depths without saturation.
        TensorPrimitives.ConvertChecked(residual[..sampleCount], columns);
        Hadamard8Columns(columns, temporary);

        ref int columnBase = ref MemoryMarshal.GetReference(columns);
        ref int rowBase = ref MemoryMarshal.GetReference(rows);
        if (Vector256.IsHardwareAccelerated)
        {
            Av1Transform2dOperations.Transpose8x8Int32(ref columnBase, width, ref rowBase, width, 0, false);
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            // Four four-by-four tiles exchange their row and column origins, preserving the same eight-by-eight
            // layout on machines whose vectors cannot hold a complete row of eight Int32 values.
            for (int y = 0; y < width; y += Vector128<int>.Count)
            {
                for (int x = 0; x < width; x += Vector128<int>.Count)
                {
                    Av1Transform2dOperations.Transpose4x4Int32(
                        ref Unsafe.Add(ref columnBase, (y * width) + x), width, ref Unsafe.Add(ref rowBase, (x * width) + y), width, 0, false);
                }
            }
        }
        else
        {
            for (int y = 0; y < width; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    rows[(x * width) + y] = columns[(y * width) + x];
                }
            }
        }

        Hadamard8Columns(rows, temporary);

        // SATD is invariant under coefficient permutation. Retain natural Hadamard order and omit the reference's
        // output permutation and final transpose, since only the magnitude sum escapes this scratch workspace.
        return TensorPrimitives.SumOfMagnitudes<int>(rows);
    }

    /// <summary>
    /// Computes the quick-transform SATD used by libaom's intra mode model.
    /// </summary>
    public static long GetHadamardCost(
        ReadOnlySpan<short> residual,
        int stride,
        int size,
        bool highBitDepth,
        Span<int> coefficients,
        Span<int> workspace)
    {
        if (size == 4)
        {
            Hadamard4x4(residual, stride, coefficients);
            return TensorPrimitives.SumOfMagnitudes<int>(coefficients[..16]);
        }

        if (size == 8)
        {
            if (highBitDepth)
            {
                Span<short> packed = MemoryMarshal.Cast<int, short>(coefficients[..32]);
                for (int row = 0; row < 8; row++)
                {
                    residual.Slice(row * stride, 8).CopyTo(packed.Slice(row * 8, 8));
                }

                _ = GetHadamard8x8Cost(packed, workspace);
                workspace.Slice(64, 64).CopyTo(coefficients);
                return TensorPrimitives.SumOfMagnitudes<int>(coefficients[..64]);
            }

            TransformForModeEstimation(
                residual,
                stride,
                size,
                coefficients[..64],
                workspace,
                highBitDepth);

            return TensorPrimitives.SumOfMagnitudes<int>(coefficients[..64]);
        }

        int half = size >> 1;
        int quadrantLength = half * half;
        for (int quadrant = 0; quadrant < 4; quadrant++)
        {
            int rowOffset = (quadrant >> 1) * half;
            int columnOffset = (quadrant & 1) * half;
            GetHadamardCost(
                residual[((rowOffset * stride) + columnOffset)..],
                stride,
                half,
                highBitDepth,
                coefficients.Slice(quadrant * quadrantLength, quadrantLength),
                workspace);
        }

        CombineHadamardQuadrants(coefficients, quadrantLength, size == 32 ? 2 : 1);
        return TensorPrimitives.SumOfMagnitudes<int>(coefficients[..(size * size)]);
    }

    /// <summary>
    /// Computes the 4x4 Hadamard transform with the halving butterflies of aom_hadamard_4x4(). Each vector lane
    /// is one column, so both passes are lane-wise butterflies over rows; the two transposes keep the coefficient
    /// order that the column-by-column definition produces.
    /// </summary>
    /// <param name="residual">The residual samples.</param>
    /// <param name="stride">The residual row stride.</param>
    /// <param name="coefficients">Receives the sixteen coefficients.</param>
    private static void Hadamard4x4(ReadOnlySpan<short> residual, int stride, Span<int> coefficients)
    {
        ref short residualBase = ref MemoryMarshal.GetReference(residual);
        Vector128<int> row0 = LoadFourSamples(ref residualBase, 0);
        Vector128<int> row1 = LoadFourSamples(ref residualBase, stride);
        Vector128<int> row2 = LoadFourSamples(ref residualBase, 2 * stride);
        Vector128<int> row3 = LoadFourSamples(ref residualBase, 3 * stride);

        HadamardRows4(ref row0, ref row1, ref row2, ref row3);
        Av1Transform2dOperations.Transpose(ref row0, ref row1, ref row2, ref row3);
        HadamardRows4(ref row0, ref row1, ref row2, ref row3);
        Av1Transform2dOperations.Transpose(ref row0, ref row1, ref row2, ref row3);

        ref int coefficientBase = ref MemoryMarshal.GetReference(coefficients);
        row0.StoreUnsafe(ref coefficientBase);
        row1.StoreUnsafe(ref coefficientBase, 4);
        row2.StoreUnsafe(ref coefficientBase, 8);
        row3.StoreUnsafe(ref coefficientBase, 12);
    }

    /// <summary>
    /// Loads four residual samples as 32-bit lanes without reading past them.
    /// </summary>
    /// <param name="residual">The first residual sample.</param>
    /// <param name="offset">The offset of the four samples.</param>
    /// <returns>The widened samples.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> LoadFourSamples(ref short residual, int offset)
        => Vector128.WidenLower(Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref residual, offset)))).AsInt16());

    /// <summary>
    /// Applies the halving butterflies of one aom_hadamard_4x4() pass to four columns at once.
    /// </summary>
    /// <param name="row0">The first row, replaced by the first output.</param>
    /// <param name="row1">The second row, replaced by the second output.</param>
    /// <param name="row2">The third row, replaced by the third output.</param>
    /// <param name="row3">The fourth row, replaced by the fourth output.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HadamardRows4(ref Vector128<int> row0, ref Vector128<int> row1, ref Vector128<int> row2, ref Vector128<int> row3)
    {
        Vector128<int> b0 = (row0 + row1) >> 1;
        Vector128<int> b1 = (row0 - row1) >> 1;
        Vector128<int> b2 = (row2 + row3) >> 1;
        Vector128<int> b3 = (row2 - row3) >> 1;
        row0 = b0 + b2;
        row1 = b1 + b3;
        row2 = b0 - b2;
        row3 = b1 - b3;
    }

    /// <summary>
    /// Combines four equal Hadamard quadrants in place with the halving butterflies of the 16x16 and 32x32
    /// transforms. Reference: the combine loops of aom_hadamard_16x16_c() and aom_hadamard_32x32_c().
    /// </summary>
    /// <param name="coefficients">The four quadrants in quadrant order.</param>
    /// <param name="quadrantLength">The number of coefficients in one quadrant.</param>
    /// <param name="shift">The halving shift, one for 16x16 and two for 32x32.</param>
    private static void CombineHadamardQuadrants(Span<int> coefficients, int quadrantLength, int shift)
    {
        ref int quadrant0 = ref MemoryMarshal.GetReference(coefficients);
        ref int quadrant1 = ref Unsafe.Add(ref quadrant0, quadrantLength);
        ref int quadrant2 = ref Unsafe.Add(ref quadrant1, quadrantLength);
        ref int quadrant3 = ref Unsafe.Add(ref quadrant2, quadrantLength);
        nuint length = (nuint)quadrantLength;
        nuint index = 0;

        // Quadrants hold 64 or 256 coefficients, so every width divides them exactly.
        if (Vector512.IsHardwareAccelerated)
        {
            for (; index < length; index += (nuint)Vector512<int>.Count)
            {
                Vector512<int> a0 = Vector512.LoadUnsafe(ref quadrant0, index);
                Vector512<int> a1 = Vector512.LoadUnsafe(ref quadrant1, index);
                Vector512<int> a2 = Vector512.LoadUnsafe(ref quadrant2, index);
                Vector512<int> a3 = Vector512.LoadUnsafe(ref quadrant3, index);
                Vector512<int> b0 = (a0 + a1) >> shift;
                Vector512<int> b1 = (a0 - a1) >> shift;
                Vector512<int> b2 = (a2 + a3) >> shift;
                Vector512<int> b3 = (a2 - a3) >> shift;
                (b0 + b2).StoreUnsafe(ref quadrant0, index);
                (b1 + b3).StoreUnsafe(ref quadrant1, index);
                (b0 - b2).StoreUnsafe(ref quadrant2, index);
                (b1 - b3).StoreUnsafe(ref quadrant3, index);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; index < length; index += (nuint)Vector256<int>.Count)
            {
                Vector256<int> a0 = Vector256.LoadUnsafe(ref quadrant0, index);
                Vector256<int> a1 = Vector256.LoadUnsafe(ref quadrant1, index);
                Vector256<int> a2 = Vector256.LoadUnsafe(ref quadrant2, index);
                Vector256<int> a3 = Vector256.LoadUnsafe(ref quadrant3, index);
                Vector256<int> b0 = (a0 + a1) >> shift;
                Vector256<int> b1 = (a0 - a1) >> shift;
                Vector256<int> b2 = (a2 + a3) >> shift;
                Vector256<int> b3 = (a2 - a3) >> shift;
                (b0 + b2).StoreUnsafe(ref quadrant0, index);
                (b1 + b3).StoreUnsafe(ref quadrant1, index);
                (b0 - b2).StoreUnsafe(ref quadrant2, index);
                (b1 - b3).StoreUnsafe(ref quadrant3, index);
            }
        }

        for (; index < length; index += (nuint)Vector128<int>.Count)
        {
            Vector128<int> a0 = Vector128.LoadUnsafe(ref quadrant0, index);
            Vector128<int> a1 = Vector128.LoadUnsafe(ref quadrant1, index);
            Vector128<int> a2 = Vector128.LoadUnsafe(ref quadrant2, index);
            Vector128<int> a3 = Vector128.LoadUnsafe(ref quadrant3, index);
            Vector128<int> b0 = (a0 + a1) >> shift;
            Vector128<int> b1 = (a0 - a1) >> shift;
            Vector128<int> b2 = (a2 + a3) >> shift;
            Vector128<int> b3 = (a2 - a3) >> shift;
            (b0 + b2).StoreUnsafe(ref quadrant0, index);
            (b1 + b3).StoreUnsafe(ref quadrant1, index);
            (b0 - b2).StoreUnsafe(ref quadrant2, index);
            (b1 - b3).StoreUnsafe(ref quadrant3, index);
        }
    }

    /// <summary>
    /// Applies three stages of unnormalized Hadamard butterflies to eight independent columns.
    /// </summary>
    /// <param name="block">The eight-by-eight Int32 block transformed in place.</param>
    /// <param name="temporary">One reusable row that preserves a butterfly sum while its difference is written.</param>
    private static void Hadamard8Columns(Span<int> block, Span<int> temporary)
    {
        const int width = 8;
        for (int half = 1; half < width; half *= 2)
        {
            for (int start = 0; start < width; start += 2 * half)
            {
                for (int row = start; row < start + half; row++)
                {
                    Span<int> first = block.Slice(row * width, width);
                    Span<int> second = block.Slice((row + half) * width, width);

                    // Whole-row addition and subtraction use the existing SIMD APIs, including narrower hardware
                    // and scalar fallback. Preserve the sum until subtraction has consumed the original first row.
                    TensorPrimitives.Add<int>(first, second, temporary);
                    TensorPrimitives.Subtract<int>(first, second, second);
                    temporary.CopyTo(first);
                }
            }
        }
    }

    /// <summary>
    /// Resolves and applies the configured two-dimensional AV1 forward transform.
    /// </summary>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <param name="bitDepth">The source sample bit depth.</param>
    /// <param name="workspace">The reusable workspace owned by the containing encode operation.</param>
    public static void Transform2d(
        ReadOnlySpan<short> input,
        Span<int> coefficients,
        uint stride,
        Av1TransformType transformType,
        Av1TransformSize transformSize,
        int bitDepth,
        Span<int> workspace)
    {
        if (KernelsSupported && bitDepth == 8)
        {
            // The most frequent blocks of a still image have kernels that need no configuration or workspace.
            if (transformSize == Av1TransformSize.Size8x8)
            {
                Transform8x8(input, stride, transformType, coefficients);
                return;
            }

            if (transformSize == Av1TransformSize.Size4x4)
            {
                Transform4x4(input, stride, transformType, coefficients);
                return;
            }

            if (transformSize == Av1TransformSize.Size16x16 && WideKernelsSupported)
            {
                Transform16x16(input, stride, transformType, coefficients);
                return;
            }
        }

        Av1Transform2dFlipConfiguration config = Av1Transform2dFlipConfiguration.CreateForward(transformType, transformSize, bitDepth);
        Guard.MustBeSizedAtLeast(workspace, Av1TransformWorkspace.GetRequiredLength(transformSize), nameof(workspace));

        DispatchColumn(input, coefficients, stride, bitDepth, ref config, workspace);
    }

    /// <summary>
    /// Applies the reversible four-by-four transform required by coded-lossless AV1 blocks.
    /// </summary>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    public static void TransformLossless4x4(ReadOnlySpan<short> input, Span<int> coefficients, uint stride)
    {
        int inputStride = (int)stride;

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<int> row0 = Vector128.Create((int)input[0], input[1], input[2], input[3]);
            Vector128<int> row1 = Vector128.Create(
                input[inputStride],
                input[inputStride + 1],
                input[inputStride + 2],
                input[inputStride + 3]);

            Vector128<int> row2 = Vector128.Create(
                input[2 * inputStride],
                input[(2 * inputStride) + 1],
                input[(2 * inputStride) + 2],
                input[(2 * inputStride) + 3]);

            Vector128<int> row3 = Vector128.Create(
                input[3 * inputStride],
                input[(3 * inputStride) + 1],
                input[(3 * inputStride) + 2],
                input[(3 * inputStride) + 3]);

            // Each lane initially holds one column. The first stage transforms those columns in parallel, then the
            // transpose makes each transformed column a row so the same lane-wise network can process the other axis.
            TransformLosslessStage(ref row0, ref row1, ref row2, ref row3);
            Av1Transform2dOperations.Transpose(ref row0, ref row1, ref row2, ref row3);
            TransformLosslessStage(ref row0, ref row1, ref row2, ref row3);

            // The entropy pipeline stores transform positions in row-major order, while the reversible reference
            // walk leaves the two frequency axes exchanged. Normalize that boundary before scan-order traversal.
            Av1Transform2dOperations.Transpose(ref row0, ref row1, ref row2, ref row3);

            ref int coefficientBase = ref MemoryMarshal.GetReference(coefficients);
            (row0 * 4).StoreUnsafe(ref coefficientBase);
            (row1 * 4).StoreUnsafe(ref coefficientBase, 4);
            (row2 * 4).StoreUnsafe(ref coefficientBase, 8);
            (row3 * 4).StoreUnsafe(ref coefficientBase, 12);
            return;
        }

        // The first pass writes transposed columns into the destination, matching the layout consumed in-place by
        // the second pass. This keeps the scalar fallback allocation-free without a temporary matrix.
        for (int column = 0; column < 4; column++)
        {
            int a = input[column];
            int b = input[inputStride + column];
            int c = input[(2 * inputStride) + column];
            int d = input[(3 * inputStride) + column];

            a += b;
            d -= c;
            int e = (a - d) >> 1;
            b = e - b;
            c = e - c;
            a -= c;
            d += b;

            int offset = column * 4;
            coefficients[offset] = a;
            coefficients[offset + 1] = c;
            coefficients[offset + 2] = d;
            coefficients[offset + 3] = b;
        }

        for (int column = 0; column < 4; column++)
        {
            int a = coefficients[column];
            int b = coefficients[4 + column];
            int c = coefficients[8 + column];
            int d = coefficients[12 + column];

            a += b;
            d -= c;
            int e = (a - d) >> 1;
            b = e - b;
            c = e - c;
            a -= c;
            d += b;

            coefficients[column] = a * 4;
            coefficients[4 + column] = c * 4;
            coefficients[8 + column] = d * 4;
            coefficients[12 + column] = b * 4;
        }

        // Normalize the scalar reference walk to the row-major coefficient contract used by entropy coding.
        (coefficients[1], coefficients[4]) = (coefficients[4], coefficients[1]);
        (coefficients[2], coefficients[8]) = (coefficients[8], coefficients[2]);
        (coefficients[3], coefficients[12]) = (coefficients[12], coefficients[3]);
        (coefficients[6], coefficients[9]) = (coefficients[9], coefficients[6]);
        (coefficients[7], coefficients[13]) = (coefficients[13], coefficients[7]);
        (coefficients[11], coefficients[14]) = (coefficients[14], coefficients[11]);
    }

    /// <summary>
    /// Applies one axis of the reversible four-point transform to four independent SIMD lanes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TransformLosslessStage(
        ref Vector128<int> row0,
        ref Vector128<int> row1,
        ref Vector128<int> row2,
        ref Vector128<int> row3)
    {
        Vector128<int> a = row0 + row1;
        Vector128<int> d = row3 - row2;
        Vector128<int> e = (a - d) >> 1;
        Vector128<int> b = e - row1;
        Vector128<int> c = e - row2;
        a -= c;
        d += b;

        row0 = a;
        row1 = c;
        row2 = d;
        row3 = b;
    }

    /// <summary>
    /// Selects the concrete column operator for a transform block.
    /// </summary>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="bitDepth">The source sample bit depth.</param>
    /// <param name="config">The resolved transform functions, shifts, and axis orientation.</param>
    /// <param name="workspace">The reusable transform workspace.</param>
    private static void DispatchColumn(
        ReadOnlySpan<short> input,
        Span<int> coefficients,
        uint stride,
        int bitDepth,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace)
    {
        switch (config.TransformFunctionTypeColumn)
        {
            case Av1TransformFunctionType.Dct4:
                DispatchRow<Dct4Operator>(input, coefficients, stride, bitDepth, ref config, workspace);
                break;
            case Av1TransformFunctionType.Dct8:
                DispatchRow<Dct8Operator>(input, coefficients, stride, bitDepth, ref config, workspace);
                break;
            case Av1TransformFunctionType.Dct16:
                DispatchRow<Dct16Operator>(input, coefficients, stride, bitDepth, ref config, workspace);
                break;
            case Av1TransformFunctionType.Dct32:
                DispatchRow<Dct32Operator>(input, coefficients, stride, bitDepth, ref config, workspace);
                break;
            case Av1TransformFunctionType.Dct64:
                DispatchRow<Dct64Operator>(input, coefficients, stride, bitDepth, ref config, workspace);
                break;
            case Av1TransformFunctionType.Adst4:
                DispatchRow<Adst4Operator>(input, coefficients, stride, bitDepth, ref config, workspace);
                break;
            case Av1TransformFunctionType.Adst8:
                DispatchRow<Adst8Operator>(input, coefficients, stride, bitDepth, ref config, workspace);
                break;
            case Av1TransformFunctionType.Adst16:
                DispatchRow<Adst16Operator>(input, coefficients, stride, bitDepth, ref config, workspace);
                break;
            case Av1TransformFunctionType.Identity4:
                DispatchRow<Identity4Operator>(input, coefficients, stride, bitDepth, ref config, workspace);
                break;
            case Av1TransformFunctionType.Identity8:
                DispatchRow<Identity8Operator>(input, coefficients, stride, bitDepth, ref config, workspace);
                break;
            case Av1TransformFunctionType.Identity16:
                DispatchRow<Identity16Operator>(input, coefficients, stride, bitDepth, ref config, workspace);
                break;
            case Av1TransformFunctionType.Identity32:
                DispatchRow<Identity32Operator>(input, coefficients, stride, bitDepth, ref config, workspace);
                break;
            default:
                throw new InvalidImageContentException($"The {config.TransformFunctionTypeColumn} column transform is not valid for {config.TransformSize}.");
        }
    }

    /// <summary>
    /// Selects the concrete row operator after the column operator has been specialized.
    /// </summary>
    /// <typeparam name="TColumnOperator">The column transform operator selected for the block.</typeparam>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="bitDepth">The source sample bit depth.</param>
    /// <param name="config">The resolved transform functions, shifts, and axis orientation.</param>
    /// <param name="workspace">The reusable transform workspace.</param>
    private static void DispatchRow<TColumnOperator>(
        ReadOnlySpan<short> input,
        Span<int> coefficients,
        uint stride,
        int bitDepth,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace)
        where TColumnOperator : struct, IAv1ForwardTransform1dOperator
    {
        switch (config.TransformFunctionTypeRow)
        {
            case Av1TransformFunctionType.Dct4:
                Transform2d<TColumnOperator, Dct4Operator>(input, coefficients, stride, bitDepth, ref config, workspace);
                break;
            case Av1TransformFunctionType.Dct8:
                Transform2d<TColumnOperator, Dct8Operator>(input, coefficients, stride, bitDepth, ref config, workspace);
                break;
            case Av1TransformFunctionType.Dct16:
                Transform2d<TColumnOperator, Dct16Operator>(input, coefficients, stride, bitDepth, ref config, workspace);
                break;
            case Av1TransformFunctionType.Dct32:
                Transform2d<TColumnOperator, Dct32Operator>(input, coefficients, stride, bitDepth, ref config, workspace);
                break;
            case Av1TransformFunctionType.Dct64:
                Transform2d<TColumnOperator, Dct64Operator>(input, coefficients, stride, bitDepth, ref config, workspace);
                break;
            case Av1TransformFunctionType.Adst4:
                Transform2d<TColumnOperator, Adst4Operator>(input, coefficients, stride, bitDepth, ref config, workspace);
                break;
            case Av1TransformFunctionType.Adst8:
                Transform2d<TColumnOperator, Adst8Operator>(input, coefficients, stride, bitDepth, ref config, workspace);
                break;
            case Av1TransformFunctionType.Adst16:
                Transform2d<TColumnOperator, Adst16Operator>(input, coefficients, stride, bitDepth, ref config, workspace);
                break;
            case Av1TransformFunctionType.Identity4:
                Transform2d<TColumnOperator, Identity4Operator>(input, coefficients, stride, bitDepth, ref config, workspace);
                break;
            case Av1TransformFunctionType.Identity8:
                Transform2d<TColumnOperator, Identity8Operator>(input, coefficients, stride, bitDepth, ref config, workspace);
                break;
            case Av1TransformFunctionType.Identity16:
                Transform2d<TColumnOperator, Identity16Operator>(input, coefficients, stride, bitDepth, ref config, workspace);
                break;
            case Av1TransformFunctionType.Identity32:
                Transform2d<TColumnOperator, Identity32Operator>(input, coefficients, stride, bitDepth, ref config, workspace);
                break;
            default:
                throw new InvalidImageContentException($"The {config.TransformFunctionTypeRow} row transform is not valid for {config.TransformSize}.");
        }
    }

    /// <summary>
    /// Applies the specialized operator pair using the sample representation selected for the coded bit depth.
    /// </summary>
    /// <typeparam name="TColumnOperator">The column transform operator.</typeparam>
    /// <typeparam name="TRowOperator">The row transform operator.</typeparam>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="bitDepth">The source sample bit depth.</param>
    /// <param name="config">The resolved transform functions, shifts, and axis orientation.</param>
    /// <param name="workspace">The reusable transform workspace.</param>
    private static void Transform2d<TColumnOperator, TRowOperator>(
        ReadOnlySpan<short> input,
        Span<int> coefficients,
        uint stride,
        int bitDepth,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace)
        where TColumnOperator : struct, IAv1ForwardTransform1dOperator
        where TRowOperator : struct, IAv1ForwardTransform1dOperator
    {
        // Highway keeps eight-bit transform stages in Int16 lanes and promotes only the large rectangular layouts.
        // The independent scalar reference uses Int32, so hardware without packed Int16 support follows that exact fallback instead.
        if (bitDepth == 8 && Vector128.IsHardwareAccelerated)
        {
            TransformPacked<TColumnOperator, TRowOperator>(input, coefficients, stride, ref config, workspace);
            return;
        }

        TransformExpanded<TColumnOperator, TRowOperator>(input, coefficients, stride, ref config, workspace);
    }

    /// <summary>
    /// Applies the signed Int16 stage pipeline used for eight-bit residuals.
    /// </summary>
    /// <typeparam name="TColumnOperator">The column transform operator.</typeparam>
    /// <typeparam name="TRowOperator">The row transform operator.</typeparam>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="config">The resolved transform functions, shifts, and axis orientation.</param>
    /// <param name="workspace">The reusable transform workspace.</param>
    private static void TransformPacked<TColumnOperator, TRowOperator>(
        ReadOnlySpan<short> input,
        Span<int> coefficients,
        uint stride,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace)
        where TColumnOperator : struct, IAv1ForwardTransform1dOperator
        where TRowOperator : struct, IAv1ForwardTransform1dOperator
    {
        int width = config.TransformSize.GetWidth();
        int height = config.TransformSize.GetHeight();

        // Packed short stages halve the arithmetic width and AVX-512BW doubles their lane count. Highway selects
        // this representation by ISA capability, independently of the runtime preference used for generic vectors.
        int blockLaneCount = Vector512.IsHardwareAccelerated
            ? Vector512<short>.Count
            : Vector256.IsHardwareAccelerated ? Vector256<short>.Count : Vector128<short>.Count;

        int blockWidth = Math.Max(width, blockLaneCount);
        int blockHeight = Math.Max(height, blockLaneCount);
        int blockArea = blockWidth * blockHeight;
        int packedBlockLength = (blockArea + 1) / 2;
        bool promote = (blockWidth == 64 && blockHeight >= 32) || (blockWidth >= 32 && blockHeight == 64);
        int dataOffset = Av1TransformWorkspace.Vector512StorageLength;
        Span<short> buffer0 = MemoryMarshal.Cast<int, short>(workspace.Slice(dataOffset, packedBlockLength));
        ref short buffer0Base = ref MemoryMarshal.GetReference(buffer0);

        LoadPacked(input, stride, ref buffer0Base, blockWidth, width, height, config.Shift0, config.FlipUpsideDown, config.FlipLeftToRight);
        TransformPackedAxis<TColumnOperator>(buffer0, width, blockWidth, blockWidth, config.CosBitColumn, workspace);

        int retainedHeight = Math.Min(height, 32);
        int retainedWidth = Math.Min(width, 32);
        bool normalizeRectangle = Math.Abs(config.TransformSize.GetRectangleLogRatio()) == 1;

        if (promote)
        {
            Span<int> buffer1 = workspace.Slice(dataOffset + packedBlockLength, blockArea);
            int scratchOffset = dataOffset + packedBlockLength + blockArea;
            Span<int> scratch = workspace.Slice(scratchOffset);
            ref int buffer1Base = ref MemoryMarshal.GetReference(buffer1);

            TransposeAndPromote(
                ref buffer0Base,
                blockWidth,
                ref buffer1Base,
                blockHeight,
                width,
                height,
                -config.Shift1,
                scratch);

            int rowOutputStride = width == 64 && height == 64 ? 32 : blockHeight;

            TransformExpandedAxis<TRowOperator>(buffer1, retainedHeight, blockHeight, rowOutputStride, config.CosBitRow, workspace);
            ref int coefficientBase = ref MemoryMarshal.GetReference(coefficients);

            TransposeExpanded(
                ref buffer1Base,
                rowOutputStride,
                ref coefficientBase,
                retainedWidth,
                retainedHeight,
                retainedWidth,
                -config.Shift2,
                normalizeRectangle,
                scratch);

            return;
        }

        Span<short> buffer1Packed = MemoryMarshal.Cast<int, short>(workspace.Slice(dataOffset + packedBlockLength, packedBlockLength));
        int packedScratchOffset = dataOffset + (2 * packedBlockLength);
        Span<int> packedScratch = workspace.Slice(packedScratchOffset);
        ref short buffer1PackedBase = ref MemoryMarshal.GetReference(buffer1Packed);

        TransposePacked(
            ref buffer0Base,
            blockWidth,
            ref buffer1PackedBase,
            blockHeight,
            width,
            height,
            -config.Shift1,
            false,
            packedScratch);

        TransformPackedAxis<TRowOperator>(buffer1Packed, height, blockHeight, blockHeight, config.CosBitRow, workspace);

        // The second transform produces horizontal frequency in rows and vertical frequency in lanes. Transposing
        // once more adapts the reference decoder's native layout to the row-major coefficient contract used by ImageSharp.
        TransposePacked(
            ref buffer1PackedBase,
            blockHeight,
            ref buffer0Base,
            retainedWidth,
            retainedHeight,
            retainedWidth,
            -config.Shift2,
            normalizeRectangle,
            packedScratch);

        StorePacked(ref buffer0Base, retainedWidth, retainedHeight, coefficients);
    }

    /// <summary>
    /// Applies the signed Int32 stage pipeline used for high-bit-depth residuals and scalar fallback.
    /// </summary>
    /// <typeparam name="TColumnOperator">The column transform operator.</typeparam>
    /// <typeparam name="TRowOperator">The row transform operator.</typeparam>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="config">The resolved transform functions, shifts, and axis orientation.</param>
    /// <param name="workspace">The reusable transform workspace.</param>
    private static void TransformExpanded<TColumnOperator, TRowOperator>(
        ReadOnlySpan<short> input,
        Span<int> coefficients,
        uint stride,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace)
        where TColumnOperator : struct, IAv1ForwardTransform1dOperator
        where TRowOperator : struct, IAv1ForwardTransform1dOperator
    {
        int width = config.TransformSize.GetWidth();
        int height = config.TransformSize.GetHeight();
        int blockLaneCount = Vector512.IsHardwareAccelerated
            ? Vector512<int>.Count
            : Vector256.IsHardwareAccelerated ? Vector256<int>.Count : Vector128.IsHardwareAccelerated ? Vector128<int>.Count : 1;

        int blockWidth = Math.Max(width, blockLaneCount);
        int blockHeight = Math.Max(height, blockLaneCount);
        int blockArea = blockWidth * blockHeight;
        int dataOffset = Av1TransformWorkspace.Vector512StorageLength;
        Span<int> buffer0 = workspace.Slice(dataOffset, blockArea);
        Span<int> buffer1 = workspace.Slice(dataOffset + blockArea, blockArea);
        Span<int> scratch = workspace.Slice(dataOffset + (2 * blockArea));
        ref int buffer0Base = ref MemoryMarshal.GetReference(buffer0);
        ref int buffer1Base = ref MemoryMarshal.GetReference(buffer1);

        LoadExpanded(input, stride, ref buffer0Base, blockWidth, width, height, config.Shift0, config.FlipUpsideDown, config.FlipLeftToRight);
        TransformExpandedAxis<TColumnOperator>(buffer0, width, blockWidth, blockWidth, config.CosBitColumn, workspace);

        TransposeExpanded(
            ref buffer0Base,
            blockWidth,
            ref buffer1Base,
            blockHeight,
            width,
            height,
            -config.Shift1,
            false,
            scratch);

        int retainedHeight = Math.Min(height, 32);
        int retainedWidth = Math.Min(width, 32);
        int rowOutputStride = width == 64 && height == 64 ? 32 : blockHeight;

        TransformExpandedAxis<TRowOperator>(buffer1, retainedHeight, blockHeight, rowOutputStride, config.CosBitRow, workspace);
        bool normalizeRectangle = Math.Abs(config.TransformSize.GetRectangleLogRatio()) == 1;
        ref int coefficientBase = ref MemoryMarshal.GetReference(coefficients);

        TransposeExpanded(
            ref buffer1Base,
            rowOutputStride,
            ref coefficientBase,
            retainedWidth,
            retainedHeight,
            retainedWidth,
            -config.Shift2,
            normalizeRectangle,
            scratch);
    }

    /// <summary>
    /// Loads, flips, and scales one eight-bit residual block into packed transform storage.
    /// </summary>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="inputStride">The number of input samples between rows.</param>
    /// <param name="destination">The first value in the packed transform block.</param>
    /// <param name="destinationStride">The number of packed values between destination rows.</param>
    /// <param name="width">The transform-block width.</param>
    /// <param name="height">The transform-block height.</param>
    /// <param name="shift">The initial transform scaling shift.</param>
    /// <param name="flipUpsideDown">Whether to reverse the input row order.</param>
    /// <param name="flipLeftToRight">Whether to reverse the samples within each row.</param>
    private static void LoadPacked(
        ReadOnlySpan<short> input,
        uint inputStride,
        ref short destination,
        int destinationStride,
        int width,
        int height,
        int shift,
        bool flipUpsideDown,
        bool flipLeftToRight)
    {
        ref short inputBase = ref MemoryMarshal.GetReference(input);

        for (int row = 0; row < height; row++)
        {
            int sourceRow = flipUpsideDown ? height - row - 1 : row;
            ref short source = ref Unsafe.Add(ref inputBase, sourceRow * (int)inputStride);
            ref short target = ref Unsafe.Add(ref destination, row * destinationStride);

            if (Vector512.IsHardwareAccelerated && width >= Vector512<short>.Count)
            {
                for (int column = 0; column < width; column += Vector512<short>.Count)
                {
                    int sourceColumn = flipLeftToRight ? width - column - Vector512<short>.Count : column;
                    Vector512<short> value = Vector512.LoadUnsafe(ref source, (nuint)sourceColumn);
                    value = flipLeftToRight ? Av1Transform2dOperations.Reverse(value) : value;
                    Av1Transform2dOperations.RoundShift(value, -shift).StoreUnsafe(ref target, (nuint)column);
                }

                continue;
            }

            if (Vector256.IsHardwareAccelerated && width >= Vector256<short>.Count)
            {
                for (int column = 0; column < width; column += Vector256<short>.Count)
                {
                    int sourceColumn = flipLeftToRight ? width - column - Vector256<short>.Count : column;
                    Vector256<short> value = Vector256.LoadUnsafe(ref source, (nuint)sourceColumn);
                    value = flipLeftToRight ? Av1Transform2dOperations.Reverse(value) : value;
                    Av1Transform2dOperations.RoundShift(value, -shift).StoreUnsafe(ref target, (nuint)column);
                }

                continue;
            }

            if (width >= Vector128<short>.Count)
            {
                for (int column = 0; column < width; column += Vector128<short>.Count)
                {
                    int sourceColumn = flipLeftToRight ? width - column - Vector128<short>.Count : column;
                    Vector128<short> value = Vector128.LoadUnsafe(ref source, (nuint)sourceColumn);
                    value = flipLeftToRight ? Av1Transform2dOperations.Reverse(value) : value;
                    Av1Transform2dOperations.RoundShift(value, -shift).StoreUnsafe(ref target, (nuint)column);
                }

                continue;
            }

            // Four-point transforms occupy the lower half of the padded Vector128 row. Loading each source value
            // explicitly avoids reading beyond a caller row whose stride is exactly four samples.
            for (int column = 0; column < width; column++)
            {
                int sourceColumn = flipLeftToRight ? width - column - 1 : column;
                target = (short)(Unsafe.Add(ref source, sourceColumn) << shift);
                target = ref Unsafe.Add(ref target, 1);
            }
        }
    }

    /// <summary>
    /// Loads, flips, widens, and scales one residual block into signed thirty-two-bit transform storage.
    /// </summary>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="inputStride">The number of input samples between rows.</param>
    /// <param name="destination">The first value in the expanded transform block.</param>
    /// <param name="destinationStride">The number of expanded values between destination rows.</param>
    /// <param name="width">The transform-block width.</param>
    /// <param name="height">The transform-block height.</param>
    /// <param name="shift">The initial transform scaling shift.</param>
    /// <param name="flipUpsideDown">Whether to reverse the input row order.</param>
    /// <param name="flipLeftToRight">Whether to reverse the samples within each row.</param>
    private static void LoadExpanded(
        ReadOnlySpan<short> input,
        uint inputStride,
        ref int destination,
        int destinationStride,
        int width,
        int height,
        int shift,
        bool flipUpsideDown,
        bool flipLeftToRight)
    {
        ref short inputBase = ref MemoryMarshal.GetReference(input);

        for (int row = 0; row < height; row++)
        {
            int sourceRow = flipUpsideDown ? height - row - 1 : row;
            ref short source = ref Unsafe.Add(ref inputBase, sourceRow * (int)inputStride);
            ref int target = ref Unsafe.Add(ref destination, row * destinationStride);

            if (Vector512.IsHardwareAccelerated && width >= Vector512<int>.Count)
            {
                for (int column = 0; column < width; column += Vector512<int>.Count)
                {
                    int sourceColumn = flipLeftToRight ? width - column - Vector512<int>.Count : column;
                    Vector512<int> value = Av1Transform2dOperations.Load16Int16(ref Unsafe.Add(ref source, sourceColumn));
                    value = flipLeftToRight ? Av1Transform2dOperations.Reverse(value) : value;
                    Av1Transform2dOperations.RoundShift(value, -shift).StoreUnsafe(ref target, (nuint)column);
                }

                continue;
            }

            if (Vector256.IsHardwareAccelerated && width >= Vector256<int>.Count)
            {
                for (int column = 0; column < width; column += Vector256<int>.Count)
                {
                    int sourceColumn = flipLeftToRight ? width - column - Vector256<int>.Count : column;
                    Vector256<int> value = Av1Transform2dOperations.Load8Int16(ref Unsafe.Add(ref source, sourceColumn));
                    value = flipLeftToRight ? Av1Transform2dOperations.Reverse(value) : value;
                    Av1Transform2dOperations.RoundShift(value, -shift).StoreUnsafe(ref target, (nuint)column);
                }

                continue;
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (int column = 0; column < width; column += Vector128<int>.Count)
                {
                    int sourceColumn = flipLeftToRight ? width - column - Vector128<int>.Count : column;
                    Vector128<int> value = Av1Transform2dOperations.Load4Int16(ref Unsafe.Add(ref source, sourceColumn));
                    value = flipLeftToRight ? Av1Transform2dOperations.Reverse(value) : value;
                    Av1Transform2dOperations.RoundShift(value, -shift).StoreUnsafe(ref target, (nuint)column);
                }

                continue;
            }

            for (int column = 0; column < width; column++)
            {
                int sourceColumn = flipLeftToRight ? width - column - 1 : column;
                target = Unsafe.Add(ref source, sourceColumn) << shift;
                target = ref Unsafe.Add(ref target, 1);
            }
        }
    }

    /// <summary>
    /// Applies one packed transform axis using the widest efficient lane count available for the block.
    /// </summary>
    /// <typeparam name="TOperator">The semantic transform operator.</typeparam>
    /// <param name="buffer">The packed transform block.</param>
    /// <param name="transformCount">The number of independent axes.</param>
    /// <param name="inputStride">The number of packed values between input positions.</param>
    /// <param name="outputStride">The number of packed values between output positions.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    /// <param name="workspace">The reusable transform-stage workspace.</param>
    private static void TransformPackedAxis<TOperator>(
        Span<short> buffer,
        int transformCount,
        int inputStride,
        int outputStride,
        int cosBit,
        Span<int> workspace)
        where TOperator : struct, IAv1ForwardTransform1dOperator
    {
        if (Vector512.IsHardwareAccelerated && transformCount >= Vector512<short>.Count)
        {
            TransformPackedVector512<TOperator>(buffer, transformCount, inputStride, outputStride, cosBit, workspace);
            return;
        }

        if (Vector256.IsHardwareAccelerated && transformCount >= Vector256<short>.Count)
        {
            TransformPackedVector256<TOperator>(buffer, transformCount, inputStride, outputStride, cosBit, workspace);
            return;
        }

        TransformPackedVector128<TOperator>(buffer, transformCount, inputStride, outputStride, cosBit, workspace);
    }

    /// <summary>
    /// Applies one expanded transform axis using the widest efficient lane count available for the block.
    /// </summary>
    /// <typeparam name="TOperator">The semantic transform operator.</typeparam>
    /// <param name="buffer">The expanded transform block.</param>
    /// <param name="transformCount">The number of independent axes.</param>
    /// <param name="inputStride">The number of expanded values between input positions.</param>
    /// <param name="outputStride">The number of expanded values between output positions.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    /// <param name="workspace">The reusable transform-stage workspace.</param>
    private static void TransformExpandedAxis<TOperator>(
        Span<int> buffer,
        int transformCount,
        int inputStride,
        int outputStride,
        int cosBit,
        Span<int> workspace)
        where TOperator : struct, IAv1ForwardTransform1dOperator
    {
        if (Vector512.IsHardwareAccelerated && transformCount >= Vector512<int>.Count)
        {
            TransformExpandedVector512<TOperator>(buffer, transformCount, inputStride, outputStride, cosBit, workspace);
            return;
        }

        if (Vector256.IsHardwareAccelerated && transformCount >= Vector256<int>.Count)
        {
            TransformExpandedVector256<TOperator>(buffer, transformCount, inputStride, outputStride, cosBit, workspace);
            return;
        }

        if (Vector128.IsHardwareAccelerated && transformCount >= Vector128<int>.Count)
        {
            TransformExpandedVector128<TOperator>(buffer, transformCount, inputStride, outputStride, cosBit, workspace);
            return;
        }

        TransformExpandedScalar<TOperator>(buffer, transformCount, inputStride, outputStride, cosBit, workspace);
    }

    /// <summary>
    /// Applies a packed transform to thirty-two independent axes.
    /// </summary>
    private static void TransformPackedVector512<TOperator>(
        Span<short> buffer,
        int transformCount,
        int inputStride,
        int outputStride,
        int cosBit,
        Span<int> workspace)
        where TOperator : struct, IAv1ForwardTransform1dOperator
    {
        ref Av1TransformVector<Vector512<short>> buffer0 =
            ref Unsafe.As<int, Av1TransformVector<Vector512<short>>>(ref MemoryMarshal.GetReference(workspace));

        ref Av1TransformVector<Vector512<short>> buffer1 =
            ref Unsafe.Add(ref buffer0, 1);

        ref short source = ref MemoryMarshal.GetReference(buffer);
        nint inputByteStride = inputStride * sizeof(short);
        nint outputByteStride = outputStride * sizeof(short);

        for (int batch = 0; batch < transformCount; batch += Vector512<short>.Count)
        {
            ref byte values = ref Unsafe.As<short, byte>(ref Unsafe.Add(ref source, batch));

            TOperator.Transform(ref values, inputByteStride, outputByteStride, ref buffer0, ref buffer1, cosBit);
        }
    }

    /// <summary>
    /// Applies a packed transform to sixteen independent axes.
    /// </summary>
    private static void TransformPackedVector256<TOperator>(
        Span<short> buffer,
        int transformCount,
        int inputStride,
        int outputStride,
        int cosBit,
        Span<int> workspace)
        where TOperator : struct, IAv1ForwardTransform1dOperator
    {
        ref Av1TransformVector<Vector256<short>> buffer0 =
            ref Unsafe.As<int, Av1TransformVector<Vector256<short>>>(ref MemoryMarshal.GetReference(workspace));

        ref Av1TransformVector<Vector256<short>> buffer1 =
            ref Unsafe.Add(ref buffer0, 1);

        ref short source = ref MemoryMarshal.GetReference(buffer);
        nint inputByteStride = inputStride * sizeof(short);
        nint outputByteStride = outputStride * sizeof(short);

        for (int batch = 0; batch < transformCount; batch += Vector256<short>.Count)
        {
            ref byte values = ref Unsafe.As<short, byte>(ref Unsafe.Add(ref source, batch));

            TOperator.Transform(ref values, inputByteStride, outputByteStride, ref buffer0, ref buffer1, cosBit);
        }
    }

    /// <summary>
    /// Applies a packed transform to eight independent axes.
    /// </summary>
    private static void TransformPackedVector128<TOperator>(
        Span<short> buffer,
        int transformCount,
        int inputStride,
        int outputStride,
        int cosBit,
        Span<int> workspace)
        where TOperator : struct, IAv1ForwardTransform1dOperator
    {
        ref Av1TransformVector<Vector128<short>> buffer0 =
            ref Unsafe.As<int, Av1TransformVector<Vector128<short>>>(ref MemoryMarshal.GetReference(workspace));

        ref Av1TransformVector<Vector128<short>> buffer1 =
            ref Unsafe.Add(ref buffer0, 1);

        ref short source = ref MemoryMarshal.GetReference(buffer);
        nint inputByteStride = inputStride * sizeof(short);
        nint outputByteStride = outputStride * sizeof(short);

        for (int batch = 0; batch < transformCount; batch += Vector128<short>.Count)
        {
            ref byte values = ref Unsafe.As<short, byte>(ref Unsafe.Add(ref source, batch));

            TOperator.Transform(ref values, inputByteStride, outputByteStride, ref buffer0, ref buffer1, cosBit);
        }
    }

    /// <summary>
    /// Applies an expanded transform to sixteen independent axes.
    /// </summary>
    private static void TransformExpandedVector512<TOperator>(
        Span<int> buffer,
        int transformCount,
        int inputStride,
        int outputStride,
        int cosBit,
        Span<int> workspace)
        where TOperator : struct, IAv1ForwardTransform1dOperator
    {
        ref Av1TransformVector<Vector512<int>> buffer0 =
            ref Unsafe.As<int, Av1TransformVector<Vector512<int>>>(ref MemoryMarshal.GetReference(workspace));

        ref Av1TransformVector<Vector512<int>> buffer1 =
            ref Unsafe.Add(ref buffer0, 1);

        ref int source = ref MemoryMarshal.GetReference(buffer);
        nint inputByteStride = inputStride * sizeof(int);
        nint outputByteStride = outputStride * sizeof(int);

        for (int batch = 0; batch < transformCount; batch += Vector512<int>.Count)
        {
            ref byte values = ref Unsafe.As<int, byte>(ref Unsafe.Add(ref source, batch));

            TOperator.Transform(ref values, inputByteStride, outputByteStride, ref buffer0, ref buffer1, cosBit);
        }
    }

    /// <summary>
    /// Applies an expanded transform to eight independent axes.
    /// </summary>
    private static void TransformExpandedVector256<TOperator>(
        Span<int> buffer,
        int transformCount,
        int inputStride,
        int outputStride,
        int cosBit,
        Span<int> workspace)
        where TOperator : struct, IAv1ForwardTransform1dOperator
    {
        ref Av1TransformVector<Vector256<int>> buffer0 =
            ref Unsafe.As<int, Av1TransformVector<Vector256<int>>>(ref MemoryMarshal.GetReference(workspace));

        ref Av1TransformVector<Vector256<int>> buffer1 =
            ref Unsafe.Add(ref buffer0, 1);

        ref int source = ref MemoryMarshal.GetReference(buffer);
        nint inputByteStride = inputStride * sizeof(int);
        nint outputByteStride = outputStride * sizeof(int);

        for (int batch = 0; batch < transformCount; batch += Vector256<int>.Count)
        {
            ref byte values = ref Unsafe.As<int, byte>(ref Unsafe.Add(ref source, batch));

            TOperator.Transform(ref values, inputByteStride, outputByteStride, ref buffer0, ref buffer1, cosBit);
        }
    }

    /// <summary>
    /// Applies an expanded transform to four independent axes.
    /// </summary>
    private static void TransformExpandedVector128<TOperator>(
        Span<int> buffer,
        int transformCount,
        int inputStride,
        int outputStride,
        int cosBit,
        Span<int> workspace)
        where TOperator : struct, IAv1ForwardTransform1dOperator
    {
        ref Av1TransformVector<Vector128<int>> buffer0 =
            ref Unsafe.As<int, Av1TransformVector<Vector128<int>>>(ref MemoryMarshal.GetReference(workspace));

        ref Av1TransformVector<Vector128<int>> buffer1 =
            ref Unsafe.Add(ref buffer0, 1);

        ref int source = ref MemoryMarshal.GetReference(buffer);
        nint inputByteStride = inputStride * sizeof(int);
        nint outputByteStride = outputStride * sizeof(int);

        for (int batch = 0; batch < transformCount; batch += Vector128<int>.Count)
        {
            ref byte values = ref Unsafe.As<int, byte>(ref Unsafe.Add(ref source, batch));

            TOperator.Transform(ref values, inputByteStride, outputByteStride, ref buffer0, ref buffer1, cosBit);
        }
    }

    /// <summary>
    /// Applies an expanded transform to one axis.
    /// </summary>
    private static void TransformExpandedScalar<TOperator>(
        Span<int> buffer,
        int transformCount,
        int inputStride,
        int outputStride,
        int cosBit,
        Span<int> workspace)
        where TOperator : struct, IAv1ForwardTransform1dOperator
    {
        ref Av1TransformVector<int> buffer0 =
            ref Unsafe.As<int, Av1TransformVector<int>>(ref MemoryMarshal.GetReference(workspace));

        ref Av1TransformVector<int> buffer1 =
            ref Unsafe.Add(ref buffer0, 1);

        ref int source = ref MemoryMarshal.GetReference(buffer);
        nint inputByteStride = inputStride * sizeof(int);
        nint outputByteStride = outputStride * sizeof(int);

        for (int batch = 0; batch < transformCount; batch++)
        {
            ref byte values = ref Unsafe.As<int, byte>(ref Unsafe.Add(ref source, batch));

            TOperator.Transform(ref values, inputByteStride, outputByteStride, ref buffer0, ref buffer1, cosBit);
        }
    }

    /// <summary>
    /// Transposes packed transform storage while applying an AV1 pipeline shift and optional rectangle scaling.
    /// </summary>
    /// <param name="source">The first value in the source block.</param>
    /// <param name="sourceStride">The number of packed values between source rows.</param>
    /// <param name="destination">The first value in the destination block.</param>
    /// <param name="destinationStride">The number of packed values between destination rows.</param>
    /// <param name="sourceWidth">The number of source columns.</param>
    /// <param name="sourceHeight">The number of source rows.</param>
    /// <param name="roundShift">The signed AV1 scaling shift.</param>
    /// <param name="normalizeRectangle">Whether to apply the square-root-of-two rectangle normalization.</param>
    /// <param name="scratch">The reusable transpose workspace.</param>
    private static void TransposePacked(
        ref short source,
        int sourceStride,
        ref short destination,
        int destinationStride,
        int sourceWidth,
        int sourceHeight,
        int roundShift,
        bool normalizeRectangle,
        Span<int> scratch)
    {
        int tileSize = Vector256.IsHardwareAccelerated && Math.Min(sourceWidth, sourceHeight) >= 16 ? 16 : Math.Min(sourceWidth, sourceHeight) >= 8 ? 8 : 4;
        Span<long> transposeScratch = MemoryMarshal.Cast<int, long>(scratch);

        for (int row = 0; row < sourceHeight; row += tileSize)
        {
            for (int column = 0; column < sourceWidth; column += tileSize)
            {
                ref short tileSource = ref Unsafe.Add(ref source, (row * sourceStride) + column);
                ref short tileDestination = ref Unsafe.Add(ref destination, (column * destinationStride) + row);

                if (tileSize == 16)
                {
                    Av1Transform2dOperations.Transpose16x16Int16(
                        ref tileSource,
                        sourceStride,
                        ref tileDestination,
                        destinationStride,
                        transposeScratch,
                        roundShift,
                        normalizeRectangle);
                }
                else if (tileSize == 8)
                {
                    Av1Transform2dOperations.Transpose8x8Int16(
                        ref tileSource,
                        sourceStride,
                        ref tileDestination,
                        destinationStride,
                        roundShift,
                        normalizeRectangle);
                }
                else
                {
                    Av1Transform2dOperations.Transpose4x4Int16(
                        ref tileSource,
                        sourceStride,
                        ref tileDestination,
                        destinationStride,
                        roundShift,
                        normalizeRectangle);
                }
            }
        }
    }

    /// <summary>
    /// Promotes and transposes the large packed layouts at the same axis boundary as the reference decoder.
    /// </summary>
    /// <param name="source">The first packed value in the source block.</param>
    /// <param name="sourceStride">The number of packed values between source rows.</param>
    /// <param name="destination">The first expanded value in the destination block.</param>
    /// <param name="destinationStride">The number of expanded values between destination rows.</param>
    /// <param name="sourceWidth">The number of source columns.</param>
    /// <param name="sourceHeight">The number of source rows.</param>
    /// <param name="roundShift">The signed AV1 scaling shift.</param>
    /// <param name="scratch">The reusable conversion and transpose workspace.</param>
    private static void TransposeAndPromote(
        ref short source,
        int sourceStride,
        ref int destination,
        int destinationStride,
        int sourceWidth,
        int sourceHeight,
        int roundShift,
        Span<int> scratch)
    {
        int tileSize = Vector512.IsHardwareAccelerated ? 16 : Vector256.IsHardwareAccelerated ? 8 : 4;

        for (int row = 0; row < sourceHeight; row += tileSize)
        {
            for (int column = 0; column < sourceWidth; column += tileSize)
            {
                ref short tileSource = ref Unsafe.Add(ref source, (row * sourceStride) + column);
                ref int tileDestination = ref Unsafe.Add(ref destination, (column * destinationStride) + row);

                if (tileSize == 16)
                {
                    Av1Transform2dOperations.Transpose16x16Int16ToInt32(
                        ref tileSource,
                        sourceStride,
                        ref tileDestination,
                        destinationStride,
                        scratch[..256],
                        MemoryMarshal.Cast<int, long>(scratch[256..]),
                        roundShift,
                        false);
                }
                else if (tileSize == 8)
                {
                    Av1Transform2dOperations.Transpose8x8Int16ToInt32(
                        ref tileSource,
                        sourceStride,
                        ref tileDestination,
                        destinationStride,
                        scratch[..64],
                        roundShift,
                        false);
                }
                else
                {
                    Av1Transform2dOperations.Transpose4x4Int16ToInt32(
                        ref tileSource,
                        sourceStride,
                        ref tileDestination,
                        destinationStride,
                        scratch[..16],
                        roundShift,
                        false);
                }
            }
        }
    }

    /// <summary>
    /// Transposes signed thirty-two-bit transform storage while applying the configured terminal operations.
    /// </summary>
    /// <param name="source">The first value in the source block.</param>
    /// <param name="sourceStride">The number of expanded values between source rows.</param>
    /// <param name="destination">The first value in the destination block.</param>
    /// <param name="destinationStride">The number of expanded values between destination rows.</param>
    /// <param name="sourceWidth">The number of source columns.</param>
    /// <param name="sourceHeight">The number of source rows.</param>
    /// <param name="roundShift">The signed AV1 scaling shift.</param>
    /// <param name="normalizeRectangle">Whether to apply the square-root-of-two rectangle normalization.</param>
    /// <param name="scratch">The reusable transpose workspace.</param>
    private static void TransposeExpanded(
        ref int source,
        int sourceStride,
        ref int destination,
        int destinationStride,
        int sourceWidth,
        int sourceHeight,
        int roundShift,
        bool normalizeRectangle,
        Span<int> scratch)
    {
        bool useVector512 = Vector512.IsHardwareAccelerated && Math.Min(sourceWidth, sourceHeight) >= 16;
        int tileSize = useVector512
            ? 16
            : Vector256.IsHardwareAccelerated && Math.Min(sourceWidth, sourceHeight) >= 8 ? 8 : Vector128.IsHardwareAccelerated ? 4 : 1;

        Span<long> transposeScratch = MemoryMarshal.Cast<int, long>(scratch);

        for (int row = 0; row < sourceHeight; row += tileSize)
        {
            for (int column = 0; column < sourceWidth; column += tileSize)
            {
                ref int tileSource = ref Unsafe.Add(ref source, (row * sourceStride) + column);
                ref int tileDestination = ref Unsafe.Add(ref destination, (column * destinationStride) + row);

                if (tileSize == 16)
                {
                    Av1Transform2dOperations.Transpose16x16Avx512(
                        ref tileSource,
                        sourceStride,
                        ref tileDestination,
                        destinationStride,
                        transposeScratch,
                        roundShift,
                        normalizeRectangle);
                }
                else if (tileSize == 8)
                {
                    Av1Transform2dOperations.Transpose8x8Int32(
                        ref tileSource,
                        sourceStride,
                        ref tileDestination,
                        destinationStride,
                        roundShift,
                        normalizeRectangle);
                }
                else if (tileSize == 4)
                {
                    Av1Transform2dOperations.Transpose4x4Int32(
                        ref tileSource,
                        sourceStride,
                        ref tileDestination,
                        destinationStride,
                        roundShift,
                        normalizeRectangle);
                }
                else
                {
                    int value = Av1Math.RoundShift(tileSource, roundShift);
                    tileDestination = normalizeRectangle
                        ? Av1Transform1dMath.HalfButterfly(Av1Transform1dMath.NewSqrt2, value, 0, 0, Av1Transform1dMath.NewSqrt2Bits)
                        : value;
                }
            }
        }
    }

    /// <summary>
    /// Widens the completed packed coefficient matrix into its external signed thirty-two-bit representation.
    /// </summary>
    /// <param name="source">The first packed transform coefficient.</param>
    /// <param name="width">The coefficient matrix width.</param>
    /// <param name="height">The coefficient matrix height.</param>
    /// <param name="destination">The destination signed thirty-two-bit coefficients.</param>
    private static void StorePacked(ref short source, int width, int height, Span<int> destination)
    {
        ref int destinationBase = ref MemoryMarshal.GetReference(destination);

        for (int row = 0; row < height; row++)
        {
            ref short sourceRow = ref Unsafe.Add(ref source, row * width);
            ref int destinationRow = ref Unsafe.Add(ref destinationBase, row * width);
            int column = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                nuint vector512Count = Numerics.Vector512Count<short>(width - column);
                for (; vector512Count > 0; vector512Count--, column += Vector512<short>.Count)
                {
                    (Vector512<int> lower, Vector512<int> upper) = Vector512.Widen(Vector512.LoadUnsafe(ref sourceRow, (nuint)column));

                    lower.StoreUnsafe(ref destinationRow, (nuint)column);
                    upper.StoreUnsafe(ref destinationRow, (nuint)(column + Vector512<int>.Count));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint vector256Count = Numerics.Vector256Count<short>(width - column);
                for (; vector256Count > 0; vector256Count--, column += Vector256<short>.Count)
                {
                    (Vector256<int> lower, Vector256<int> upper) = Vector256.Widen(Vector256.LoadUnsafe(ref sourceRow, (nuint)column));

                    lower.StoreUnsafe(ref destinationRow, (nuint)column);
                    upper.StoreUnsafe(ref destinationRow, (nuint)(column + Vector256<int>.Count));
                }
            }

            nuint vector128Count = Numerics.Vector128Count<short>(width - column);
            for (; vector128Count > 0; vector128Count--, column += Vector128<short>.Count)
            {
                (Vector128<int> lower, Vector128<int> upper) = Vector128.Widen(Vector128.LoadUnsafe(ref sourceRow, (nuint)column));

                lower.StoreUnsafe(ref destinationRow, (nuint)column);
                upper.StoreUnsafe(ref destinationRow, (nuint)(column + Vector128<int>.Count));
            }

            if (column < width)
            {
                Vector128<int> value = Vector128.WidenLower(
                    Vector128.Create(Unsafe.As<short, ulong>(ref Unsafe.Add(ref sourceRow, column)), 0UL).AsInt16());

                value.StoreUnsafe(ref destinationRow, (nuint)column);
            }
        }
    }
}
