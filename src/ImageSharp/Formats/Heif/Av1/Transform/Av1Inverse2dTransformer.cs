// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Applies separable two-dimensional AV1 inverse transforms and reconstructs decoded samples.
/// </summary>
internal static class Av1Inverse2dTransformer
{
    /// <summary>
    /// Applies an inverse transform and adds its residual to high-bit-depth predicted samples.
    /// </summary>
    /// <param name="input">The dequantized coefficients in raster order.</param>
    /// <param name="outputForRead">The predicted samples read by reconstruction.</param>
    /// <param name="strideForRead">The number of read samples between rows.</param>
    /// <param name="outputForWrite">The destination reconstructed samples.</param>
    /// <param name="strideForWrite">The number of destination samples between rows.</param>
    /// <param name="config">The per-axis transform, flip, shift, and range configuration.</param>
    /// <param name="workspace">The reusable transform workspace.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    public static void Transform2dAdd(
        Span<int> input,
        Span<short> outputForRead,
        int strideForRead,
        Span<short> outputForWrite,
        int strideForWrite,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace,
        int bitDepth)
        => Transform2dAdd<short, Av1HighBitDepthInverseTransformOutputOperator>(
            input,
            outputForRead,
            strideForRead,
            outputForWrite,
            strideForWrite,
            ref config,
            workspace,
            bitDepth);

    /// <summary>
    /// Applies an inverse transform and adds its residual to eight-bit predicted samples.
    /// </summary>
    /// <param name="input">The dequantized coefficients in raster order.</param>
    /// <param name="outputForRead">The predicted samples read by reconstruction.</param>
    /// <param name="strideForRead">The number of read samples between rows.</param>
    /// <param name="outputForWrite">The destination reconstructed samples.</param>
    /// <param name="strideForWrite">The number of destination samples between rows.</param>
    /// <param name="config">The per-axis transform, flip, shift, and range configuration.</param>
    /// <param name="workspace">The reusable transform workspace.</param>
    public static void Transform2dAdd(
        Span<int> input,
        Span<byte> outputForRead,
        int strideForRead,
        Span<byte> outputForWrite,
        int strideForWrite,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace)
        => Transform2dAdd<byte, Av1ByteInverseTransformOutputOperator>(
            input,
            outputForRead,
            strideForRead,
            outputForWrite,
            strideForWrite,
            ref config,
            workspace,
            8);

    /// <summary>
    /// Initializes the transform ranges and selects the concrete column operator.
    /// </summary>
    private static void Transform2dAdd<TSample, TOutputOperator>(
        Span<int> input,
        Span<TSample> outputForRead,
        int strideForRead,
        Span<TSample> outputForWrite,
        int strideForWrite,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace,
        int bitDepth)
        where TSample : unmanaged
        where TOutputOperator : struct, IAv1InverseTransformOutputOperator<TSample>
    {
        Guard.MustBeSizedAtLeast(workspace, Av1TransformWorkspace.GetRequiredLength(config.TransformSize), nameof(workspace));
        switch (config.TransformFunctionTypeColumn)
        {
            case Av1TransformFunctionType.Dct4:
                DispatchRow<TSample, TOutputOperator, Av1Dct4Inverse1dOperator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct8:
                DispatchRow<TSample, TOutputOperator, Av1Dct8Inverse1dOperator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct16:
                DispatchRow<TSample, TOutputOperator, Av1Dct16Inverse1dOperator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct32:
                DispatchRow<TSample, TOutputOperator, Av1Dct32Inverse1dOperator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct64:
                DispatchRow<TSample, TOutputOperator, Av1Dct64Inverse1dOperator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Adst4:
                DispatchRow<TSample, TOutputOperator, Av1Adst4Inverse1dOperator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Adst8:
                DispatchRow<TSample, TOutputOperator, Av1Adst8Inverse1dOperator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Adst16:
                DispatchRow<TSample, TOutputOperator, Av1Adst16Inverse1dOperator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Identity4:
                DispatchRow<TSample, TOutputOperator, Av1Identity4Inverse1dOperator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Identity8:
                DispatchRow<TSample, TOutputOperator, Av1Identity8Inverse1dOperator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Identity16:
                DispatchRow<TSample, TOutputOperator, Av1Identity16Inverse1dOperator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Identity32:
                DispatchRow<TSample, TOutputOperator, Av1Identity32Inverse1dOperator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            default:
                throw new InvalidImageContentException($"The {config.TransformFunctionTypeColumn} column transform is not valid for {config.TransformSize}.");
        }
    }

    /// <summary>
    /// Selects the concrete row operator after the column operator has been specialized.
    /// </summary>
    private static void DispatchRow<TSample, TOutputOperator, TColumnOperator>(
        Span<int> input,
        Span<TSample> outputForRead,
        int strideForRead,
        Span<TSample> outputForWrite,
        int strideForWrite,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace,
        int bitDepth)
        where TSample : unmanaged
        where TOutputOperator : struct, IAv1InverseTransformOutputOperator<TSample>
        where TColumnOperator : struct, IAv1Transform1dOperator
    {
        switch (config.TransformFunctionTypeRow)
        {
            case Av1TransformFunctionType.Dct4:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Av1Dct4Inverse1dOperator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct8:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Av1Dct8Inverse1dOperator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct16:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Av1Dct16Inverse1dOperator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct32:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Av1Dct32Inverse1dOperator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct64:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Av1Dct64Inverse1dOperator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Adst4:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Av1Adst4Inverse1dOperator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Adst8:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Av1Adst8Inverse1dOperator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Adst16:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Av1Adst16Inverse1dOperator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Identity4:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Av1Identity4Inverse1dOperator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Identity8:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Av1Identity8Inverse1dOperator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Identity16:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Av1Identity16Inverse1dOperator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Identity32:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Av1Identity32Inverse1dOperator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            default:
                throw new InvalidImageContentException($"The {config.TransformFunctionTypeRow} row transform is not valid for {config.TransformSize}.");
        }
    }

    /// <summary>
    /// Applies the specialized operator pair using the widest lane width supported by the block and processor.
    /// </summary>
    private static void Transform2d<TSample, TOutputOperator, TColumnOperator, TRowOperator>(
        Span<int> input,
        Span<TSample> outputForRead,
        int strideForRead,
        Span<TSample> outputForWrite,
        int strideForWrite,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace,
        int bitDepth)
        where TSample : unmanaged
        where TOutputOperator : struct, IAv1InverseTransformOutputOperator<TSample>
        where TColumnOperator : struct, IAv1Transform1dOperator
        where TRowOperator : struct, IAv1Transform1dOperator
    {
        int width = config.TransformSize.GetWidth();
        int height = config.TransformSize.GetHeight();

        if (Vector256.IsHardwareAccelerated && width >= Vector256<int>.Count && height >= Vector256<int>.Count)
        {
            Transform2dVector256<TSample, TOutputOperator, TColumnOperator, TRowOperator>(
                input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

            return;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Transform2dVector128<TSample, TOutputOperator, TColumnOperator, TRowOperator>(
                input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

            return;
        }

        Transform2dScalar<TSample, TOutputOperator, TColumnOperator, TRowOperator>(
            input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);
    }

    /// <summary>
    /// Applies both inverse-transform axes with eight samples packed into each SIMD vector.
    /// </summary>
    /// <typeparam name="TSample">The reconstructed sample storage type.</typeparam>
    /// <typeparam name="TOutputOperator">The operator that adds and clips inverse residuals.</typeparam>
    /// <typeparam name="TColumnOperator">The one-dimensional operator applied down each column.</typeparam>
    /// <typeparam name="TRowOperator">The one-dimensional operator applied across each row.</typeparam>
    /// <param name="input">The dequantized transform coefficients.</param>
    /// <param name="outputForRead">The prediction samples.</param>
    /// <param name="strideForRead">The number of prediction samples between rows.</param>
    /// <param name="outputForWrite">The destination reconstruction samples.</param>
    /// <param name="strideForWrite">The number of destination samples between rows.</param>
    /// <param name="config">The transform dimensions, operators, flips, and fixed-point settings.</param>
    /// <param name="workspace">The reusable storage for SIMD vectors and transposed coefficients.</param>
    /// <param name="bitDepth">The coded sample bit depth used to clamp reconstructed values.</param>
    public static void Transform2dVector256<TSample, TOutputOperator, TColumnOperator, TRowOperator>(
        Span<int> input,
        Span<TSample> outputForRead,
        int strideForRead,
        Span<TSample> outputForWrite,
        int strideForWrite,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace,
        int bitDepth)
        where TSample : unmanaged
        where TOutputOperator : struct, IAv1InverseTransformOutputOperator<TSample>
        where TColumnOperator : struct, IAv1Transform1dOperator
        where TRowOperator : struct, IAv1Transform1dOperator
    {
        const int laneCount = 8;
        const int vectorLength = Av1Constants.MaxTransformSize * laneCount;

        int width = config.TransformSize.GetWidth();
        int height = config.TransformSize.GetHeight();
        int shift0 = config.Shift0;
        int shift1 = config.Shift1;
        bool normalizeRectangle = Math.Abs(config.TransformSize.GetRectangleLogRatio()) == 1;
        byte rowClampBits = (byte)(bitDepth + 8);
        byte columnClampBits = (byte)Math.Max(bitDepth + 6, 16);

        ref int workspaceBase = ref MemoryMarshal.GetReference(workspace);
        ref Av1TransformVector<Vector256<int>> tempIn = ref Unsafe.As<int, Av1TransformVector<Vector256<int>>>(ref workspaceBase);
        ref Av1TransformVector<Vector256<int>> tempOut = ref Unsafe.As<int, Av1TransformVector<Vector256<int>>>(ref Unsafe.Add(ref workspaceBase, vectorLength));
        ref Av1TransformVector<Vector256<int>> step = ref Unsafe.As<int, Av1TransformVector<Vector256<int>>>(ref Unsafe.Add(ref workspaceBase, 2 * vectorLength));
        Span<int> buffer = workspace.Slice(Av1TransformWorkspace.Vector256StorageLength, width * height);
        ref int inputBase = ref MemoryMarshal.GetReference(input);
        ref int bufferBase = ref MemoryMarshal.GetReference(buffer);

        // Rows are transposed into lanes so the complete 1-D operator runs once for eight rows.
        for (int row = 0; row < height; row += laneCount)
        {
            for (int column = 0; column < width; column += laneCount)
            {
                Vector256<int> row0 = Vector256.LoadUnsafe(ref inputBase, (nuint)(((row + 0) * width) + column));
                Vector256<int> row1 = Vector256.LoadUnsafe(ref inputBase, (nuint)(((row + 1) * width) + column));
                Vector256<int> row2 = Vector256.LoadUnsafe(ref inputBase, (nuint)(((row + 2) * width) + column));
                Vector256<int> row3 = Vector256.LoadUnsafe(ref inputBase, (nuint)(((row + 3) * width) + column));
                Vector256<int> row4 = Vector256.LoadUnsafe(ref inputBase, (nuint)(((row + 4) * width) + column));
                Vector256<int> row5 = Vector256.LoadUnsafe(ref inputBase, (nuint)(((row + 5) * width) + column));
                Vector256<int> row6 = Vector256.LoadUnsafe(ref inputBase, (nuint)(((row + 6) * width) + column));
                Vector256<int> row7 = Vector256.LoadUnsafe(ref inputBase, (nuint)(((row + 7) * width) + column));
                Av1Transform2dOperations.Transpose(ref row0, ref row1, ref row2, ref row3, ref row4, ref row5, ref row6, ref row7);
                tempIn[column + 0] = PrepareInverseRow(row0, normalizeRectangle, rowClampBits);
                tempIn[column + 1] = PrepareInverseRow(row1, normalizeRectangle, rowClampBits);
                tempIn[column + 2] = PrepareInverseRow(row2, normalizeRectangle, rowClampBits);
                tempIn[column + 3] = PrepareInverseRow(row3, normalizeRectangle, rowClampBits);
                tempIn[column + 4] = PrepareInverseRow(row4, normalizeRectangle, rowClampBits);
                tempIn[column + 5] = PrepareInverseRow(row5, normalizeRectangle, rowClampBits);
                tempIn[column + 6] = PrepareInverseRow(row6, normalizeRectangle, rowClampBits);
                tempIn[column + 7] = PrepareInverseRow(row7, normalizeRectangle, rowClampBits);
            }

            TRowOperator.Transform(ref tempIn, ref tempOut, ref step, config.CosBitRow, config.StageRangeRow);

            for (int column = 0; column < width; column += laneCount)
            {
                Vector256<int> row0 = Av1Transform2dOperations.RoundShift(tempOut[column + 0], -shift0);
                Vector256<int> row1 = Av1Transform2dOperations.RoundShift(tempOut[column + 1], -shift0);
                Vector256<int> row2 = Av1Transform2dOperations.RoundShift(tempOut[column + 2], -shift0);
                Vector256<int> row3 = Av1Transform2dOperations.RoundShift(tempOut[column + 3], -shift0);
                Vector256<int> row4 = Av1Transform2dOperations.RoundShift(tempOut[column + 4], -shift0);
                Vector256<int> row5 = Av1Transform2dOperations.RoundShift(tempOut[column + 5], -shift0);
                Vector256<int> row6 = Av1Transform2dOperations.RoundShift(tempOut[column + 6], -shift0);
                Vector256<int> row7 = Av1Transform2dOperations.RoundShift(tempOut[column + 7], -shift0);
                Av1Transform2dOperations.Transpose(ref row0, ref row1, ref row2, ref row3, ref row4, ref row5, ref row6, ref row7);
                row0.StoreUnsafe(ref bufferBase, (nuint)(((row + 0) * width) + column));
                row1.StoreUnsafe(ref bufferBase, (nuint)(((row + 1) * width) + column));
                row2.StoreUnsafe(ref bufferBase, (nuint)(((row + 2) * width) + column));
                row3.StoreUnsafe(ref bufferBase, (nuint)(((row + 3) * width) + column));
                row4.StoreUnsafe(ref bufferBase, (nuint)(((row + 4) * width) + column));
                row5.StoreUnsafe(ref bufferBase, (nuint)(((row + 5) * width) + column));
                row6.StoreUnsafe(ref bufferBase, (nuint)(((row + 6) * width) + column));
                row7.StoreUnsafe(ref bufferBase, (nuint)(((row + 7) * width) + column));
            }
        }

        ref TSample readBase = ref MemoryMarshal.GetReference(outputForRead);
        ref TSample writeBase = ref MemoryMarshal.GetReference(outputForWrite);

        // The intermediate rows already contain contiguous column groups, avoiding a second transpose.
        for (int column = 0; column < width; column += laneCount)
        {
            int sourceColumn = config.FlipLeftToRight ? width - column - laneCount : column;

            for (int row = 0; row < height; row++)
            {
                Vector256<int> value = Vector256.LoadUnsafe(ref bufferBase, (nuint)((row * width) + sourceColumn));
                value = config.FlipLeftToRight ? Av1Transform2dOperations.Reverse(value) : value;
                tempIn[row] = Av1Transform1dMath.Clamp(value, columnClampBits);
            }

            TColumnOperator.Transform(ref tempIn, ref tempOut, ref step, config.CosBitColumn, config.StageRangeColumn);

            for (int row = 0; row < height; row++)
            {
                int sourceRow = config.FlipUpsideDown ? height - row - 1 : row;
                Vector256<int> residual = Av1Transform2dOperations.RoundShift(tempOut[sourceRow], -shift1);
                ref TSample prediction = ref Unsafe.Add(ref readBase, (row * strideForRead) + column);
                ref TSample destination = ref Unsafe.Add(ref writeBase, (row * strideForWrite) + column);
                TOutputOperator.Add(ref prediction, ref destination, residual, bitDepth);
            }
        }
    }

    /// <summary>
    /// Applies both inverse-transform axes with four samples packed into each SIMD vector.
    /// </summary>
    /// <typeparam name="TSample">The reconstructed sample storage type.</typeparam>
    /// <typeparam name="TOutputOperator">The operator that adds and clips inverse residuals.</typeparam>
    /// <typeparam name="TColumnOperator">The one-dimensional operator applied down each column.</typeparam>
    /// <typeparam name="TRowOperator">The one-dimensional operator applied across each row.</typeparam>
    /// <param name="input">The dequantized transform coefficients.</param>
    /// <param name="outputForRead">The prediction samples.</param>
    /// <param name="strideForRead">The number of prediction samples between rows.</param>
    /// <param name="outputForWrite">The destination reconstruction samples.</param>
    /// <param name="strideForWrite">The number of destination samples between rows.</param>
    /// <param name="config">The transform dimensions, operators, flips, and fixed-point settings.</param>
    /// <param name="workspace">The reusable storage for SIMD vectors and transposed coefficients.</param>
    /// <param name="bitDepth">The coded sample bit depth used to clamp reconstructed values.</param>
    public static void Transform2dVector128<TSample, TOutputOperator, TColumnOperator, TRowOperator>(
        Span<int> input,
        Span<TSample> outputForRead,
        int strideForRead,
        Span<TSample> outputForWrite,
        int strideForWrite,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace,
        int bitDepth)
        where TSample : unmanaged
        where TOutputOperator : struct, IAv1InverseTransformOutputOperator<TSample>
        where TColumnOperator : struct, IAv1Transform1dOperator
        where TRowOperator : struct, IAv1Transform1dOperator
    {
        const int laneCount = 4;
        const int vectorLength = Av1Constants.MaxTransformSize * laneCount;

        int width = config.TransformSize.GetWidth();
        int height = config.TransformSize.GetHeight();
        int shift0 = config.Shift0;
        int shift1 = config.Shift1;
        bool normalizeRectangle = Math.Abs(config.TransformSize.GetRectangleLogRatio()) == 1;
        byte rowClampBits = (byte)(bitDepth + 8);
        byte columnClampBits = (byte)Math.Max(bitDepth + 6, 16);

        ref int workspaceBase = ref MemoryMarshal.GetReference(workspace);
        ref Av1TransformVector<Vector128<int>> tempIn = ref Unsafe.As<int, Av1TransformVector<Vector128<int>>>(ref workspaceBase);
        ref Av1TransformVector<Vector128<int>> tempOut = ref Unsafe.As<int, Av1TransformVector<Vector128<int>>>(ref Unsafe.Add(ref workspaceBase, vectorLength));
        ref Av1TransformVector<Vector128<int>> step = ref Unsafe.As<int, Av1TransformVector<Vector128<int>>>(ref Unsafe.Add(ref workspaceBase, 2 * vectorLength));
        Span<int> buffer = workspace.Slice(Av1TransformWorkspace.Vector128StorageLength, width * height);
        ref int inputBase = ref MemoryMarshal.GetReference(input);
        ref int bufferBase = ref MemoryMarshal.GetReference(buffer);

        for (int row = 0; row < height; row += laneCount)
        {
            for (int column = 0; column < width; column += laneCount)
            {
                Vector128<int> row0 = Vector128.LoadUnsafe(ref inputBase, (nuint)(((row + 0) * width) + column));
                Vector128<int> row1 = Vector128.LoadUnsafe(ref inputBase, (nuint)(((row + 1) * width) + column));
                Vector128<int> row2 = Vector128.LoadUnsafe(ref inputBase, (nuint)(((row + 2) * width) + column));
                Vector128<int> row3 = Vector128.LoadUnsafe(ref inputBase, (nuint)(((row + 3) * width) + column));
                Av1Transform2dOperations.Transpose(ref row0, ref row1, ref row2, ref row3);
                tempIn[column + 0] = PrepareInverseRow(row0, normalizeRectangle, rowClampBits);
                tempIn[column + 1] = PrepareInverseRow(row1, normalizeRectangle, rowClampBits);
                tempIn[column + 2] = PrepareInverseRow(row2, normalizeRectangle, rowClampBits);
                tempIn[column + 3] = PrepareInverseRow(row3, normalizeRectangle, rowClampBits);
            }

            TRowOperator.Transform(ref tempIn, ref tempOut, ref step, config.CosBitRow, config.StageRangeRow);

            for (int column = 0; column < width; column += laneCount)
            {
                Vector128<int> row0 = Av1Transform2dOperations.RoundShift(tempOut[column + 0], -shift0);
                Vector128<int> row1 = Av1Transform2dOperations.RoundShift(tempOut[column + 1], -shift0);
                Vector128<int> row2 = Av1Transform2dOperations.RoundShift(tempOut[column + 2], -shift0);
                Vector128<int> row3 = Av1Transform2dOperations.RoundShift(tempOut[column + 3], -shift0);
                Av1Transform2dOperations.Transpose(ref row0, ref row1, ref row2, ref row3);
                row0.StoreUnsafe(ref bufferBase, (nuint)(((row + 0) * width) + column));
                row1.StoreUnsafe(ref bufferBase, (nuint)(((row + 1) * width) + column));
                row2.StoreUnsafe(ref bufferBase, (nuint)(((row + 2) * width) + column));
                row3.StoreUnsafe(ref bufferBase, (nuint)(((row + 3) * width) + column));
            }
        }

        ref TSample readBase = ref MemoryMarshal.GetReference(outputForRead);
        ref TSample writeBase = ref MemoryMarshal.GetReference(outputForWrite);

        for (int column = 0; column < width; column += laneCount)
        {
            int sourceColumn = config.FlipLeftToRight ? width - column - laneCount : column;

            for (int row = 0; row < height; row++)
            {
                Vector128<int> value = Vector128.LoadUnsafe(ref bufferBase, (nuint)((row * width) + sourceColumn));
                value = config.FlipLeftToRight ? Av1Transform2dOperations.Reverse(value) : value;
                tempIn[row] = Av1Transform1dMath.Clamp(value, columnClampBits);
            }

            TColumnOperator.Transform(ref tempIn, ref tempOut, ref step, config.CosBitColumn, config.StageRangeColumn);

            for (int row = 0; row < height; row++)
            {
                int sourceRow = config.FlipUpsideDown ? height - row - 1 : row;
                Vector128<int> residual = Av1Transform2dOperations.RoundShift(tempOut[sourceRow], -shift1);
                ref TSample prediction = ref Unsafe.Add(ref readBase, (row * strideForRead) + column);
                ref TSample destination = ref Unsafe.Add(ref writeBase, (row * strideForWrite) + column);
                TOutputOperator.Add(ref prediction, ref destination, residual, bitDepth);
            }
        }
    }

    /// <summary>
    /// Applies both inverse-transform axes when hardware vectorization is unavailable.
    /// </summary>
    /// <typeparam name="TSample">The reconstructed sample storage type.</typeparam>
    /// <typeparam name="TOutputOperator">The operator that adds and clips inverse residuals.</typeparam>
    /// <typeparam name="TColumnOperator">The one-dimensional operator applied down each column.</typeparam>
    /// <typeparam name="TRowOperator">The one-dimensional operator applied across each row.</typeparam>
    /// <param name="input">The dequantized transform coefficients.</param>
    /// <param name="outputForRead">The prediction samples.</param>
    /// <param name="strideForRead">The number of prediction samples between rows.</param>
    /// <param name="outputForWrite">The destination reconstruction samples.</param>
    /// <param name="strideForWrite">The number of destination samples between rows.</param>
    /// <param name="config">The transform dimensions, operators, flips, and fixed-point settings.</param>
    /// <param name="workspace">The reusable storage for transform stages and transposed coefficients.</param>
    /// <param name="bitDepth">The coded sample bit depth used to clamp reconstructed values.</param>
    public static void Transform2dScalar<TSample, TOutputOperator, TColumnOperator, TRowOperator>(
        Span<int> input,
        Span<TSample> outputForRead,
        int strideForRead,
        Span<TSample> outputForWrite,
        int strideForWrite,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace,
        int bitDepth)
        where TSample : unmanaged
        where TOutputOperator : struct, IAv1InverseTransformOutputOperator<TSample>
        where TColumnOperator : struct, IAv1Transform1dOperator
        where TRowOperator : struct, IAv1Transform1dOperator
    {
        int width = config.TransformSize.GetWidth();
        int height = config.TransformSize.GetHeight();
        int vectorLength = Math.Max(width, height);
        int shift0 = config.Shift0;
        int shift1 = config.Shift1;
        bool normalizeRectangle = Math.Abs(config.TransformSize.GetRectangleLogRatio()) == 1;
        byte rowClampBits = (byte)(bitDepth + 8);
        byte columnClampBits = (byte)Math.Max(bitDepth + 6, 16);
        Span<int> tempIn = workspace[..vectorLength];
        Span<int> tempOut = workspace.Slice(vectorLength, vectorLength);
        Span<int> step = workspace.Slice(2 * vectorLength, vectorLength);
        Span<int> buffer = workspace.Slice(3 * vectorLength, width * height);

        for (int row = 0; row < height; row++)
        {
            int rowOffset = row * width;

            for (int column = 0; column < width; column++)
            {
                int value = input[rowOffset + column];
                value = normalizeRectangle
                    ? Av1Math.RoundShift((long)value * Av1InverseTransformMath.NewInverseSqrt2, Av1InverseTransformMath.NewSqrt2BitCount)
                    : value;
                tempIn[column] = Av1Transform1dMath.Clamp(value, rowClampBits);
            }

            TRowOperator.Transform(tempIn, tempOut, step, config.CosBitRow, config.StageRangeRow);
            Av1InverseTransformMath.RoundShiftArray(tempOut, width, -shift0);
            tempOut[..width].CopyTo(buffer.Slice(rowOffset, width));
        }

        for (int column = 0; column < width; column++)
        {
            int sourceColumn = config.FlipLeftToRight ? width - column - 1 : column;

            for (int row = 0; row < height; row++)
            {
                tempIn[row] = Av1Transform1dMath.Clamp(buffer[(row * width) + sourceColumn], columnClampBits);
            }

            TColumnOperator.Transform(tempIn, tempOut, step, config.CosBitColumn, config.StageRangeColumn);
            Av1InverseTransformMath.RoundShiftArray(tempOut, height, -shift1);

            for (int row = 0; row < height; row++)
            {
                int sourceRow = config.FlipUpsideDown ? height - row - 1 : row;
                int readIndex = (row * strideForRead) + column;
                int writeIndex = (row * strideForWrite) + column;
                outputForWrite[writeIndex] = TOutputOperator.Add(outputForRead[readIndex], tempOut[sourceRow], bitDepth);
            }
        }
    }

    /// <summary>
    /// Applies rectangular normalization and the row-input clamp to four coefficient lanes.
    /// </summary>
    private static Vector128<int> PrepareInverseRow(Vector128<int> value, bool normalizeRectangle, byte clampBits)
    {
        if (normalizeRectangle)
        {
            value = Av1Transform1dMath.MultiplyRound(value, Av1InverseTransformMath.NewInverseSqrt2, Av1InverseTransformMath.NewSqrt2BitCount);
        }

        return Av1Transform1dMath.Clamp(value, clampBits);
    }

    /// <summary>
    /// Applies rectangular normalization and the row-input clamp to eight coefficient lanes.
    /// </summary>
    private static Vector256<int> PrepareInverseRow(Vector256<int> value, bool normalizeRectangle, byte clampBits)
    {
        if (normalizeRectangle)
        {
            value = Av1Transform1dMath.MultiplyRound(value, Av1InverseTransformMath.NewInverseSqrt2, Av1InverseTransformMath.NewSqrt2BitCount);
        }

        return Av1Transform1dMath.Clamp(value, clampBits);
    }
}
