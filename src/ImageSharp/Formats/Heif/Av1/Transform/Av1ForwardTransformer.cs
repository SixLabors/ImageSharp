// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Converts spatial residual samples into AV1 transform coefficients.
/// </summary>
internal static class Av1ForwardTransformer
{
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
        Span<short> input,
        Span<int> coefficients,
        uint stride,
        Av1TransformType transformType,
        Av1TransformSize transformSize,
        int bitDepth,
        Span<int> workspace)
    {
        Av1Transform2dFlipConfiguration config = Av1Transform2dFlipConfiguration.CreateForward(transformType, transformSize, bitDepth);
        Guard.MustBeSizedAtLeast(workspace, Av1TransformWorkspace.GetRequiredLength(transformSize), nameof(workspace));
        DispatchColumn(input, coefficients, stride, ref config, workspace);
    }

    /// <summary>
    /// Selects the concrete column operator for a transform block.
    /// </summary>
    private static void DispatchColumn(
        Span<short> input,
        Span<int> coefficients,
        uint stride,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace)
    {
        switch (config.TransformFunctionTypeColumn)
        {
            case Av1TransformFunctionType.Dct4:
                DispatchRow<Av1Dct4Forward1dOperator>(input, coefficients, stride, ref config, workspace);
                break;
            case Av1TransformFunctionType.Dct8:
                DispatchRow<Av1Dct8Forward1dOperator>(input, coefficients, stride, ref config, workspace);
                break;
            case Av1TransformFunctionType.Dct16:
                DispatchRow<Av1Dct16Forward1dOperator>(input, coefficients, stride, ref config, workspace);
                break;
            case Av1TransformFunctionType.Dct32:
                DispatchRow<Av1Dct32Forward1dOperator>(input, coefficients, stride, ref config, workspace);
                break;
            case Av1TransformFunctionType.Dct64:
                DispatchRow<Av1Dct64Forward1dOperator>(input, coefficients, stride, ref config, workspace);
                break;
            case Av1TransformFunctionType.Adst4:
                DispatchRow<Av1Adst4Forward1dOperator>(input, coefficients, stride, ref config, workspace);
                break;
            case Av1TransformFunctionType.Adst8:
                DispatchRow<Av1Adst8Forward1dOperator>(input, coefficients, stride, ref config, workspace);
                break;
            case Av1TransformFunctionType.Adst16:
                DispatchRow<Av1Adst16Forward1dOperator>(input, coefficients, stride, ref config, workspace);
                break;
            case Av1TransformFunctionType.Identity4:
                DispatchRow<Av1Identity4Forward1dOperator>(input, coefficients, stride, ref config, workspace);
                break;
            case Av1TransformFunctionType.Identity8:
                DispatchRow<Av1Identity8Forward1dOperator>(input, coefficients, stride, ref config, workspace);
                break;
            case Av1TransformFunctionType.Identity16:
                DispatchRow<Av1Identity16Forward1dOperator>(input, coefficients, stride, ref config, workspace);
                break;
            case Av1TransformFunctionType.Identity32:
                DispatchRow<Av1Identity32Forward1dOperator>(input, coefficients, stride, ref config, workspace);
                break;
            default:
                throw new InvalidImageContentException($"The {config.TransformFunctionTypeColumn} column transform is not valid for {config.TransformSize}.");
        }
    }

    /// <summary>
    /// Selects the concrete row operator after the column operator has been specialized.
    /// </summary>
    private static void DispatchRow<TColumnOperator>(
        Span<short> input,
        Span<int> coefficients,
        uint stride,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace)
        where TColumnOperator : struct, IAv1Transform1dOperator
    {
        switch (config.TransformFunctionTypeRow)
        {
            case Av1TransformFunctionType.Dct4:
                Transform2d<TColumnOperator, Av1Dct4Forward1dOperator>(input, coefficients, stride, ref config, workspace);
                break;
            case Av1TransformFunctionType.Dct8:
                Transform2d<TColumnOperator, Av1Dct8Forward1dOperator>(input, coefficients, stride, ref config, workspace);
                break;
            case Av1TransformFunctionType.Dct16:
                Transform2d<TColumnOperator, Av1Dct16Forward1dOperator>(input, coefficients, stride, ref config, workspace);
                break;
            case Av1TransformFunctionType.Dct32:
                Transform2d<TColumnOperator, Av1Dct32Forward1dOperator>(input, coefficients, stride, ref config, workspace);
                break;
            case Av1TransformFunctionType.Dct64:
                Transform2d<TColumnOperator, Av1Dct64Forward1dOperator>(input, coefficients, stride, ref config, workspace);
                break;
            case Av1TransformFunctionType.Adst4:
                Transform2d<TColumnOperator, Av1Adst4Forward1dOperator>(input, coefficients, stride, ref config, workspace);
                break;
            case Av1TransformFunctionType.Adst8:
                Transform2d<TColumnOperator, Av1Adst8Forward1dOperator>(input, coefficients, stride, ref config, workspace);
                break;
            case Av1TransformFunctionType.Adst16:
                Transform2d<TColumnOperator, Av1Adst16Forward1dOperator>(input, coefficients, stride, ref config, workspace);
                break;
            case Av1TransformFunctionType.Identity4:
                Transform2d<TColumnOperator, Av1Identity4Forward1dOperator>(input, coefficients, stride, ref config, workspace);
                break;
            case Av1TransformFunctionType.Identity8:
                Transform2d<TColumnOperator, Av1Identity8Forward1dOperator>(input, coefficients, stride, ref config, workspace);
                break;
            case Av1TransformFunctionType.Identity16:
                Transform2d<TColumnOperator, Av1Identity16Forward1dOperator>(input, coefficients, stride, ref config, workspace);
                break;
            case Av1TransformFunctionType.Identity32:
                Transform2d<TColumnOperator, Av1Identity32Forward1dOperator>(input, coefficients, stride, ref config, workspace);
                break;
            default:
                throw new InvalidImageContentException($"The {config.TransformFunctionTypeRow} row transform is not valid for {config.TransformSize}.");
        }
    }

    /// <summary>
    /// Applies the specialized operator pair using the widest lane width supported by the block and processor.
    /// </summary>
    private static void Transform2d<TColumnOperator, TRowOperator>(
        Span<short> input,
        Span<int> coefficients,
        uint stride,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace)
        where TColumnOperator : struct, IAv1Transform1dOperator
        where TRowOperator : struct, IAv1Transform1dOperator
    {
        int width = config.TransformSize.GetWidth();
        int height = config.TransformSize.GetHeight();

        if (Vector256.IsHardwareAccelerated && width >= Vector256<int>.Count && height >= Vector256<int>.Count)
        {
            Transform2dVector256<TColumnOperator, TRowOperator>(input, coefficients, stride, ref config, workspace);
            return;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Transform2dVector128<TColumnOperator, TRowOperator>(input, coefficients, stride, ref config, workspace);
            return;
        }

        Transform2dScalar<TColumnOperator, TRowOperator>(input, coefficients, stride, ref config, workspace);
    }

    /// <summary>
    /// Applies both transform axes with eight samples packed into each SIMD vector.
    /// </summary>
    /// <typeparam name="TColumnOperator">The one-dimensional operator applied down each column.</typeparam>
    /// <typeparam name="TRowOperator">The one-dimensional operator applied across each row.</typeparam>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="output">The destination transform coefficients.</param>
    /// <param name="inputStride">The number of input samples between rows.</param>
    /// <param name="config">The transform dimensions, operators, flips, and fixed-point settings.</param>
    /// <param name="workspace">The reusable storage for SIMD vectors and transposed coefficients.</param>
    public static void Transform2dVector256<TColumnOperator, TRowOperator>(
        Span<short> input,
        Span<int> output,
        uint inputStride,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace)
        where TColumnOperator : struct, IAv1Transform1dOperator
        where TRowOperator : struct, IAv1Transform1dOperator
    {
        const int laneCount = 8;
        const int vectorLength = Av1Constants.MaxTransformSize * laneCount;

        int width = config.TransformSize.GetWidth();
        int height = config.TransformSize.GetHeight();
        int shift0 = config.Shift0;
        int shift1 = config.Shift1;
        int shift2 = config.Shift2;
        bool normalizeRectangle = Math.Abs(config.TransformSize.GetRectangleLogRatio()) == 1;

        ref int workspaceBase = ref MemoryMarshal.GetReference(workspace);
        ref Av1TransformVector<Vector256<int>> tempIn = ref Unsafe.As<int, Av1TransformVector<Vector256<int>>>(ref workspaceBase);
        ref Av1TransformVector<Vector256<int>> tempOut = ref Unsafe.As<int, Av1TransformVector<Vector256<int>>>(ref Unsafe.Add(ref workspaceBase, vectorLength));
        ref Av1TransformVector<Vector256<int>> step = ref Unsafe.As<int, Av1TransformVector<Vector256<int>>>(ref Unsafe.Add(ref workspaceBase, 2 * vectorLength));
        Span<int> buffer = workspace.Slice(Av1TransformWorkspace.Vector256StorageLength, width * height);
        ref short inputBase = ref MemoryMarshal.GetReference(input);
        ref int bufferBase = ref MemoryMarshal.GetReference(buffer);

        // Each lane carries one complete column through every stage of the first transform axis.
        for (int column = 0; column < width; column += laneCount)
        {
            for (int row = 0; row < height; row++)
            {
                int sourceRow = config.FlipUpsideDown ? height - row - 1 : row;
                ref short source = ref Unsafe.Add(ref inputBase, (sourceRow * (int)inputStride) + column);
                tempIn[row] = Av1Transform2dOperations.RoundShift(Av1Transform2dOperations.Load8Int16(ref source), -shift0);
            }

            TColumnOperator.Transform(ref tempIn, ref tempOut, ref step, config.CosBitColumn, config.StageRangeColumn);
            int destinationColumn = config.FlipLeftToRight ? width - column - laneCount : column;

            for (int row = 0; row < height; row++)
            {
                Vector256<int> value = Av1Transform2dOperations.RoundShift(tempOut[row], -shift1);
                value = config.FlipLeftToRight ? Av1Transform2dOperations.Reverse(value) : value;
                value.StoreUnsafe(ref bufferBase, (nuint)((row * width) + destinationColumn));
            }
        }

        ref int outputBase = ref MemoryMarshal.GetReference(output);

        // Tile transposition changes the lane meaning from columns to rows without scalar gathers.
        for (int row = 0; row < height; row += laneCount)
        {
            for (int column = 0; column < width; column += laneCount)
            {
                Vector256<int> row0 = Vector256.LoadUnsafe(ref bufferBase, (nuint)(((row + 0) * width) + column));
                Vector256<int> row1 = Vector256.LoadUnsafe(ref bufferBase, (nuint)(((row + 1) * width) + column));
                Vector256<int> row2 = Vector256.LoadUnsafe(ref bufferBase, (nuint)(((row + 2) * width) + column));
                Vector256<int> row3 = Vector256.LoadUnsafe(ref bufferBase, (nuint)(((row + 3) * width) + column));
                Vector256<int> row4 = Vector256.LoadUnsafe(ref bufferBase, (nuint)(((row + 4) * width) + column));
                Vector256<int> row5 = Vector256.LoadUnsafe(ref bufferBase, (nuint)(((row + 5) * width) + column));
                Vector256<int> row6 = Vector256.LoadUnsafe(ref bufferBase, (nuint)(((row + 6) * width) + column));
                Vector256<int> row7 = Vector256.LoadUnsafe(ref bufferBase, (nuint)(((row + 7) * width) + column));
                Av1Transform2dOperations.Transpose(ref row0, ref row1, ref row2, ref row3, ref row4, ref row5, ref row6, ref row7);
                tempIn[column + 0] = row0;
                tempIn[column + 1] = row1;
                tempIn[column + 2] = row2;
                tempIn[column + 3] = row3;
                tempIn[column + 4] = row4;
                tempIn[column + 5] = row5;
                tempIn[column + 6] = row6;
                tempIn[column + 7] = row7;
            }

            TRowOperator.Transform(ref tempIn, ref tempOut, ref step, config.CosBitRow, config.StageRangeRow);

            for (int column = 0; column < width; column += laneCount)
            {
                Vector256<int> row0 = FinishForward(tempOut[column + 0], -shift2, normalizeRectangle);
                Vector256<int> row1 = FinishForward(tempOut[column + 1], -shift2, normalizeRectangle);
                Vector256<int> row2 = FinishForward(tempOut[column + 2], -shift2, normalizeRectangle);
                Vector256<int> row3 = FinishForward(tempOut[column + 3], -shift2, normalizeRectangle);
                Vector256<int> row4 = FinishForward(tempOut[column + 4], -shift2, normalizeRectangle);
                Vector256<int> row5 = FinishForward(tempOut[column + 5], -shift2, normalizeRectangle);
                Vector256<int> row6 = FinishForward(tempOut[column + 6], -shift2, normalizeRectangle);
                Vector256<int> row7 = FinishForward(tempOut[column + 7], -shift2, normalizeRectangle);
                Av1Transform2dOperations.Transpose(ref row0, ref row1, ref row2, ref row3, ref row4, ref row5, ref row6, ref row7);
                row0.StoreUnsafe(ref outputBase, (nuint)(((row + 0) * width) + column));
                row1.StoreUnsafe(ref outputBase, (nuint)(((row + 1) * width) + column));
                row2.StoreUnsafe(ref outputBase, (nuint)(((row + 2) * width) + column));
                row3.StoreUnsafe(ref outputBase, (nuint)(((row + 3) * width) + column));
                row4.StoreUnsafe(ref outputBase, (nuint)(((row + 4) * width) + column));
                row5.StoreUnsafe(ref outputBase, (nuint)(((row + 5) * width) + column));
                row6.StoreUnsafe(ref outputBase, (nuint)(((row + 6) * width) + column));
                row7.StoreUnsafe(ref outputBase, (nuint)(((row + 7) * width) + column));
            }
        }
    }

    /// <summary>
    /// Applies both transform axes with four samples packed into each SIMD vector.
    /// </summary>
    /// <typeparam name="TColumnOperator">The one-dimensional operator applied down each column.</typeparam>
    /// <typeparam name="TRowOperator">The one-dimensional operator applied across each row.</typeparam>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="output">The destination transform coefficients.</param>
    /// <param name="inputStride">The number of input samples between rows.</param>
    /// <param name="config">The transform dimensions, operators, flips, and fixed-point settings.</param>
    /// <param name="workspace">The reusable storage for SIMD vectors and transposed coefficients.</param>
    public static void Transform2dVector128<TColumnOperator, TRowOperator>(
        Span<short> input,
        Span<int> output,
        uint inputStride,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace)
        where TColumnOperator : struct, IAv1Transform1dOperator
        where TRowOperator : struct, IAv1Transform1dOperator
    {
        const int laneCount = 4;
        const int vectorLength = Av1Constants.MaxTransformSize * laneCount;

        int width = config.TransformSize.GetWidth();
        int height = config.TransformSize.GetHeight();
        int shift0 = config.Shift0;
        int shift1 = config.Shift1;
        int shift2 = config.Shift2;
        bool normalizeRectangle = Math.Abs(config.TransformSize.GetRectangleLogRatio()) == 1;

        ref int workspaceBase = ref MemoryMarshal.GetReference(workspace);
        ref Av1TransformVector<Vector128<int>> tempIn = ref Unsafe.As<int, Av1TransformVector<Vector128<int>>>(ref workspaceBase);
        ref Av1TransformVector<Vector128<int>> tempOut = ref Unsafe.As<int, Av1TransformVector<Vector128<int>>>(ref Unsafe.Add(ref workspaceBase, vectorLength));
        ref Av1TransformVector<Vector128<int>> step = ref Unsafe.As<int, Av1TransformVector<Vector128<int>>>(ref Unsafe.Add(ref workspaceBase, 2 * vectorLength));
        Span<int> buffer = workspace.Slice(Av1TransformWorkspace.Vector128StorageLength, width * height);
        ref short inputBase = ref MemoryMarshal.GetReference(input);
        ref int bufferBase = ref MemoryMarshal.GetReference(buffer);

        for (int column = 0; column < width; column += laneCount)
        {
            for (int row = 0; row < height; row++)
            {
                int sourceRow = config.FlipUpsideDown ? height - row - 1 : row;
                ref short source = ref Unsafe.Add(ref inputBase, (sourceRow * (int)inputStride) + column);
                tempIn[row] = Av1Transform2dOperations.RoundShift(Av1Transform2dOperations.Load4Int16(ref source), -shift0);
            }

            TColumnOperator.Transform(ref tempIn, ref tempOut, ref step, config.CosBitColumn, config.StageRangeColumn);
            int destinationColumn = config.FlipLeftToRight ? width - column - laneCount : column;

            for (int row = 0; row < height; row++)
            {
                Vector128<int> value = Av1Transform2dOperations.RoundShift(tempOut[row], -shift1);
                value = config.FlipLeftToRight ? Av1Transform2dOperations.Reverse(value) : value;
                value.StoreUnsafe(ref bufferBase, (nuint)((row * width) + destinationColumn));
            }
        }

        ref int outputBase = ref MemoryMarshal.GetReference(output);

        for (int row = 0; row < height; row += laneCount)
        {
            for (int column = 0; column < width; column += laneCount)
            {
                Vector128<int> row0 = Vector128.LoadUnsafe(ref bufferBase, (nuint)(((row + 0) * width) + column));
                Vector128<int> row1 = Vector128.LoadUnsafe(ref bufferBase, (nuint)(((row + 1) * width) + column));
                Vector128<int> row2 = Vector128.LoadUnsafe(ref bufferBase, (nuint)(((row + 2) * width) + column));
                Vector128<int> row3 = Vector128.LoadUnsafe(ref bufferBase, (nuint)(((row + 3) * width) + column));
                Av1Transform2dOperations.Transpose(ref row0, ref row1, ref row2, ref row3);
                tempIn[column + 0] = row0;
                tempIn[column + 1] = row1;
                tempIn[column + 2] = row2;
                tempIn[column + 3] = row3;
            }

            TRowOperator.Transform(ref tempIn, ref tempOut, ref step, config.CosBitRow, config.StageRangeRow);

            for (int column = 0; column < width; column += laneCount)
            {
                Vector128<int> row0 = FinishForward(tempOut[column + 0], -shift2, normalizeRectangle);
                Vector128<int> row1 = FinishForward(tempOut[column + 1], -shift2, normalizeRectangle);
                Vector128<int> row2 = FinishForward(tempOut[column + 2], -shift2, normalizeRectangle);
                Vector128<int> row3 = FinishForward(tempOut[column + 3], -shift2, normalizeRectangle);
                Av1Transform2dOperations.Transpose(ref row0, ref row1, ref row2, ref row3);
                row0.StoreUnsafe(ref outputBase, (nuint)(((row + 0) * width) + column));
                row1.StoreUnsafe(ref outputBase, (nuint)(((row + 1) * width) + column));
                row2.StoreUnsafe(ref outputBase, (nuint)(((row + 2) * width) + column));
                row3.StoreUnsafe(ref outputBase, (nuint)(((row + 3) * width) + column));
            }
        }
    }

    /// <summary>
    /// Applies both transform axes when hardware vectorization is unavailable.
    /// </summary>
    /// <typeparam name="TColumnOperator">The one-dimensional operator applied down each column.</typeparam>
    /// <typeparam name="TRowOperator">The one-dimensional operator applied across each row.</typeparam>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="output">The destination transform coefficients.</param>
    /// <param name="inputStride">The number of input samples between rows.</param>
    /// <param name="config">The transform dimensions, operators, flips, and fixed-point settings.</param>
    /// <param name="workspace">The reusable storage for transform stages and transposed coefficients.</param>
    public static void Transform2dScalar<TColumnOperator, TRowOperator>(
        Span<short> input,
        Span<int> output,
        uint inputStride,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace)
        where TColumnOperator : struct, IAv1Transform1dOperator
        where TRowOperator : struct, IAv1Transform1dOperator
    {
        int width = config.TransformSize.GetWidth();
        int height = config.TransformSize.GetHeight();
        int vectorLength = Math.Max(width, height);
        int shift0 = config.Shift0;
        int shift1 = config.Shift1;
        int shift2 = config.Shift2;
        bool normalizeRectangle = Math.Abs(config.TransformSize.GetRectangleLogRatio()) == 1;
        Span<int> tempIn = workspace[..vectorLength];
        Span<int> tempOut = workspace.Slice(vectorLength, vectorLength);
        Span<int> step = workspace.Slice(2 * vectorLength, vectorLength);
        Span<int> buffer = workspace.Slice(3 * vectorLength, width * height);

        for (int column = 0; column < width; column++)
        {
            int inputOffset = config.FlipUpsideDown ? column + ((height - 1) * (int)inputStride) : column;
            int inputStep = config.FlipUpsideDown ? -(int)inputStride : (int)inputStride;

            for (int row = 0; row < height; row++)
            {
                tempIn[row] = input[inputOffset];
                inputOffset += inputStep;
            }

            Av1InverseTransformMath.RoundShiftArray(tempIn, height, -shift0);
            TColumnOperator.Transform(tempIn, tempOut, step, config.CosBitColumn, config.StageRangeColumn);
            Av1InverseTransformMath.RoundShiftArray(tempOut, height, -shift1);
            int outputColumn = config.FlipLeftToRight ? width - column - 1 : column;

            for (int row = 0; row < height; row++)
            {
                buffer[(row * width) + outputColumn] = tempOut[row];
            }
        }

        for (int row = 0; row < height; row++)
        {
            int rowOffset = row * width;
            Span<int> outputRow = output.Slice(rowOffset, width);
            TRowOperator.Transform(buffer.Slice(rowOffset, width), outputRow, step, config.CosBitRow, config.StageRangeRow);
            Av1InverseTransformMath.RoundShiftArray(outputRow, width, -shift2);

            if (normalizeRectangle)
            {
                for (int column = 0; column < width; column++)
                {
                    outputRow[column] = Av1Math.RoundShift((long)outputRow[column] * Av1Transform1dMath.NewSqrt2, Av1Transform1dMath.NewSqrt2Bits);
                }
            }
        }
    }

    /// <summary>
    /// Applies the terminal shift and optional rectangular normalization to four coefficients.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> FinishForward(Vector128<int> value, int shift, bool normalizeRectangle)
    {
        value = Av1Transform2dOperations.RoundShift(value, shift);
        return normalizeRectangle
            ? Av1Transform1dMath.MultiplyRound(value, Av1Transform1dMath.NewSqrt2, Av1Transform1dMath.NewSqrt2Bits)
            : value;
    }

    /// <summary>
    /// Applies the terminal shift and optional rectangular normalization to eight coefficients.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> FinishForward(Vector256<int> value, int shift, bool normalizeRectangle)
    {
        value = Av1Transform2dOperations.RoundShift(value, shift);
        return normalizeRectangle
            ? Av1Transform1dMath.MultiplyRound(value, Av1Transform1dMath.NewSqrt2, Av1Transform1dMath.NewSqrt2Bits)
            : value;
    }
}
