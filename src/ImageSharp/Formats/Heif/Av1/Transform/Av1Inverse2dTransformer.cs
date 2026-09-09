// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Applies separable two-dimensional AV1 inverse transforms and reconstructs decoded samples.
/// </summary>
/// <remarks>
/// Coefficients are transposed so that each SIMD lane represents an independent transform axis and each vector field
/// represents one coefficient position. The column and row operators can then use the scalar stage graph without
/// cross-lane permutations. Reconstruction adds the final residuals to their matching prediction lanes before
/// narrowing to the decoded sample depth.
/// </remarks>
internal static partial class Av1Inverse2dTransformer
{
    /// <summary>
    /// Reconstructs a DCT block whose only coded coefficient is DC, without traversing either full transform axis.
    /// </summary>
    /// <typeparam name="TSample">The reconstructed sample storage type.</typeparam>
    /// <typeparam name="TOutputOperator">The existing sample-depth reconstruction operator.</typeparam>
    /// <param name="dc">The dequantized DC coefficient.</param>
    /// <param name="prediction">The predicted samples.</param>
    /// <param name="predictionStride">The number of prediction samples between rows.</param>
    /// <param name="destination">The reconstructed samples.</param>
    /// <param name="destinationStride">The number of destination samples between rows.</param>
    /// <param name="config">The inverse DCT dimensions and fixed-point settings.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    public static void TransformDcAdd<TSample, TOutputOperator>(
        int dc,
        ReadOnlySpan<TSample> prediction,
        int predictionStride,
        Span<TSample> destination,
        int destinationStride,
        ref Av1Transform2dFlipConfiguration config,
        int bitDepth)
        where TSample : unmanaged
        where TOutputOperator : struct, Av1InverseTransformer.IAv1InverseTransformOutputOperator<TSample>
    {
        // A DC-only DCT produces one constant residual across the block. Each axis still requires its own cosine
        // multiply and rounding; combining the two multiplies would change samples at the fixed-point boundaries.
        // Cosine-table index 32 is cos(pi/4), the DC basis factor for every supported DCT length.
        if (Math.Abs(config.TransformSize.GetRectangleLogRatio()) == 1)
        {
            dc = Av1Math.RoundShift((long)dc * Av1InverseTransformMath.NewInverseSqrt2, Av1InverseTransformMath.NewSqrt2BitCount);
        }

        dc = Av1Transform1dMath.Clamp(dc, (byte)(bitDepth + 8));
        dc = Av1Math.RoundShift((long)dc * Av1SinusConstants.CosinusPi(config.CosBitRow)[32], config.CosBitRow);
        dc = Av1Math.RoundPowerOf2(dc, -config.Shift0);
        dc = Av1Transform1dMath.Clamp(dc, (byte)Math.Max(bitDepth + 6, 16));
        dc = Av1Math.RoundShift((long)dc * Av1SinusConstants.CosinusPi(config.CosBitColumn)[32], config.CosBitColumn);
        dc = Av1Math.RoundPowerOf2(dc, -config.Shift1);

        int width = config.TransformSize.GetWidth();
        int height = config.TransformSize.GetHeight();
        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<TSample> predictionRow = prediction.Slice(y * predictionStride, width);
            Span<TSample> destinationRow = destination.Slice(y * destinationStride, width);
            ref TSample predictionBase = ref MemoryMarshal.GetReference(predictionRow);
            ref TSample destinationBase = ref MemoryMarshal.GetReference(destinationRow);
            int x = 0;

            // Every Int32 lane carries the same residual, while the output operator widens the corresponding packed
            // prediction samples and clips before narrowing. Independent strides preserve both in-place and separate
            // buffers. The vector counts cover active samples only, never row padding.
            if (Vector512.IsHardwareAccelerated)
            {
                Vector512<int> residual = Vector512.Create(dc);
                for (nuint count = Numerics.Vector512Count<int>(width); count > 0; count--, x += Vector512<int>.Count)
                {
                    TOutputOperator.Add(ref Unsafe.Add(ref predictionBase, x), ref Unsafe.Add(ref destinationBase, x), residual, bitDepth);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                Vector256<int> residual = Vector256.Create(dc);
                for (nuint count = Numerics.Vector256Count<int>(width - x); count > 0; count--, x += Vector256<int>.Count)
                {
                    TOutputOperator.Add(ref Unsafe.Add(ref predictionBase, x), ref Unsafe.Add(ref destinationBase, x), residual, bitDepth);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<int> residual = Vector128.Create(dc);
                for (nuint count = Numerics.Vector128Count<int>(width - x); count > 0; count--, x += Vector128<int>.Count)
                {
                    TOutputOperator.Add(ref Unsafe.Add(ref predictionBase, x), ref Unsafe.Add(ref destinationBase, x), residual, bitDepth);
                }
            }

            for (; x < width; x++)
            {
                destinationRow[x] = TOutputOperator.Add(predictionRow[x], dc, bitDepth);
            }
        }
    }

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
        ReadOnlySpan<int> input,
        ReadOnlySpan<short> outputForRead,
        int strideForRead,
        Span<short> outputForWrite,
        int strideForWrite,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace,
        int bitDepth)
        => Transform2dAdd<short, Av1InverseTransformer.HighBitDepthOutputOperator>(
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
        ReadOnlySpan<int> input,
        ReadOnlySpan<byte> outputForRead,
        int strideForRead,
        Span<byte> outputForWrite,
        int strideForWrite,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace)
        => Transform2dAdd<byte, Av1InverseTransformer.ByteOutputOperator>(
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
        ReadOnlySpan<int> input,
        ReadOnlySpan<TSample> outputForRead,
        int strideForRead,
        Span<TSample> outputForWrite,
        int strideForWrite,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace,
        int bitDepth)
        where TSample : unmanaged
        where TOutputOperator : struct, Av1InverseTransformer.IAv1InverseTransformOutputOperator<TSample>
    {
        Guard.MustBeSizedAtLeast(workspace, Av1TransformWorkspace.GetInverseRequiredLength(config.TransformSize), nameof(workspace));
        switch (config.TransformFunctionTypeColumn)
        {
            case Av1TransformFunctionType.Dct4:
                DispatchRow<TSample, TOutputOperator, Dct4Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct8 when config.NonzeroHeight == 1:
                DispatchRow<TSample, TOutputOperator, Dct8Low1Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct8:
                DispatchRow<TSample, TOutputOperator, Dct8Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct16 when config.NonzeroHeight == 1:
                DispatchRow<TSample, TOutputOperator, Dct16Low1Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct16 when config.NonzeroHeight <= 8:
                DispatchRow<TSample, TOutputOperator, Dct16Low8Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct16:
                DispatchRow<TSample, TOutputOperator, Dct16Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct32 when config.NonzeroHeight == 1:
                DispatchRow<TSample, TOutputOperator, Dct32Low1Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct32 when config.NonzeroHeight <= 8:
                DispatchRow<TSample, TOutputOperator, Dct32Low8Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct32 when config.NonzeroHeight <= 16:
                DispatchRow<TSample, TOutputOperator, Dct32Low16Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct32:
                DispatchRow<TSample, TOutputOperator, Dct32Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct64 when config.NonzeroHeight == 1:
                DispatchRow<TSample, TOutputOperator, Dct64Low1Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct64 when config.NonzeroHeight <= 8:
                DispatchRow<TSample, TOutputOperator, Dct64Low8Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct64 when config.NonzeroHeight <= 16:
                DispatchRow<TSample, TOutputOperator, Dct64Low16Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct64 when config.NonzeroHeight <= 32:
                DispatchRow<TSample, TOutputOperator, Dct64Low32Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct64:
                DispatchRow<TSample, TOutputOperator, Dct64Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Adst4:
                DispatchRow<TSample, TOutputOperator, Adst4Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Adst8 when config.NonzeroHeight == 1:
                DispatchRow<TSample, TOutputOperator, Adst8Low1Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Adst8:
                DispatchRow<TSample, TOutputOperator, Adst8Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Adst16 when config.NonzeroHeight == 1:
                DispatchRow<TSample, TOutputOperator, Adst16Low1Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Adst16 when config.NonzeroHeight <= 8:
                DispatchRow<TSample, TOutputOperator, Adst16Low8Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Adst16:
                DispatchRow<TSample, TOutputOperator, Adst16Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Identity4:
                DispatchRow<TSample, TOutputOperator, Identity4Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Identity8:
                DispatchRow<TSample, TOutputOperator, Identity8Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Identity16:
                DispatchRow<TSample, TOutputOperator, Identity16Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Identity32:
                DispatchRow<TSample, TOutputOperator, Identity32Operator>(
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
        ReadOnlySpan<int> input,
        ReadOnlySpan<TSample> outputForRead,
        int strideForRead,
        Span<TSample> outputForWrite,
        int strideForWrite,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace,
        int bitDepth)
        where TSample : unmanaged
        where TOutputOperator : struct, Av1InverseTransformer.IAv1InverseTransformOutputOperator<TSample>
        where TColumnOperator : struct, IAv1Transform1dOperator
    {
        switch (config.TransformFunctionTypeRow)
        {
            case Av1TransformFunctionType.Dct4:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Dct4Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct8 when config.NonzeroWidth == 1:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Dct8Low1Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct8:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Dct8Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct16 when config.NonzeroWidth == 1:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Dct16Low1Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct16 when config.NonzeroWidth <= 8:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Dct16Low8Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct16:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Dct16Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct32 when config.NonzeroWidth == 1:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Dct32Low1Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct32 when config.NonzeroWidth <= 8:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Dct32Low8Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct32 when config.NonzeroWidth <= 16:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Dct32Low16Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct32:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Dct32Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct64 when config.NonzeroWidth == 1:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Dct64Low1Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct64 when config.NonzeroWidth <= 8:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Dct64Low8Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct64 when config.NonzeroWidth <= 16:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Dct64Low16Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct64 when config.NonzeroWidth <= 32:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Dct64Low32Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Dct64:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Dct64Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Adst4:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Adst4Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Adst8 when config.NonzeroWidth == 1:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Adst8Low1Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Adst8:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Adst8Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Adst16 when config.NonzeroWidth == 1:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Adst16Low1Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Adst16 when config.NonzeroWidth <= 8:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Adst16Low8Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Adst16:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Adst16Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Identity4:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Identity4Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Identity8:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Identity8Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Identity16:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Identity16Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            case Av1TransformFunctionType.Identity32:
                Transform2d<TSample, TOutputOperator, TColumnOperator, Identity32Operator>(
                    input, outputForRead, strideForRead, outputForWrite, strideForWrite, ref config, workspace, bitDepth);

                break;
            default:
                throw new InvalidImageContentException($"The {config.TransformFunctionTypeRow} row transform is not valid for {config.TransformSize}.");
        }
    }

    /// <summary>
    /// Applies the specialized operator pair using the production lane width selected for the block and processor.
    /// </summary>
    private static void Transform2d<TSample, TOutputOperator, TColumnOperator, TRowOperator>(
        ReadOnlySpan<int> input,
        ReadOnlySpan<TSample> outputForRead,
        int strideForRead,
        Span<TSample> outputForWrite,
        int strideForWrite,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace,
        int bitDepth)
        where TSample : unmanaged
        where TOutputOperator : struct, Av1InverseTransformer.IAv1InverseTransformOutputOperator<TSample>
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
        ReadOnlySpan<int> input,
        ReadOnlySpan<TSample> outputForRead,
        int strideForRead,
        Span<TSample> outputForWrite,
        int strideForWrite,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace,
        int bitDepth)
        where TSample : unmanaged
        where TOutputOperator : struct, Av1InverseTransformer.IAv1InverseTransformOutputOperator<TSample>
        where TColumnOperator : struct, IAv1Transform1dOperator
        where TRowOperator : struct, IAv1Transform1dOperator
    {
        const int laneCount = 8;

        int width = config.TransformSize.GetWidth();
        int height = config.TransformSize.GetHeight();
        int vectorLength = Math.Max(width, height) * laneCount;
        Av1TransformSize adjustedTransformSize = config.TransformSize.GetAdjusted();
        int inputWidth = adjustedTransformSize.GetWidth();
        int inputHeight = adjustedTransformSize.GetHeight();
        int rowCount = (config.NonzeroHeight + laneCount - 1) & -laneCount;
        int rowInputCount = (TRowOperator.InputLength + laneCount - 1) & -laneCount;
        int shift0 = config.Shift0;
        int shift1 = config.Shift1;
        bool normalizeRectangle = Math.Abs(config.TransformSize.GetRectangleLogRatio()) == 1;
        byte rowClampBits = (byte)(bitDepth + 8);
        byte columnClampBits = (byte)Math.Max(bitDepth + 6, 16);

        // Input and stage storage share one vector: each operator finishes consuming coefficients before its first
        // stage write. The second vector holds outputs, and the remaining raster stores completed first-axis rows.
        // Each vector spans only the longer active axis; no fields beyond that transform length are accessed.
        ref int workspaceBase = ref MemoryMarshal.GetReference(workspace);
        ref Av1TransformVector<Vector256<int>> tempIn = ref Unsafe.As<int, Av1TransformVector<Vector256<int>>>(ref workspaceBase);
        ref Av1TransformVector<Vector256<int>> tempOut =
            ref Unsafe.As<int, Av1TransformVector<Vector256<int>>>(ref Unsafe.Add(ref workspaceBase, vectorLength));

        ref Av1TransformVector<Vector256<int>> step = ref tempIn;
        Span<int> buffer = workspace.Slice(2 * vectorLength, width * height);
        ref int inputBase = ref MemoryMarshal.GetReference(input);
        ref int bufferBase = ref MemoryMarshal.GetReference(buffer);

        // Rows are transposed into lanes so the complete 1-D operator runs once for eight rows.
        for (int row = 0; row < rowCount; row += laneCount)
        {
            for (int column = 0; column < rowInputCount; column += laneCount)
            {
                bool hasCodedCoefficients = row < inputHeight && column < inputWidth;
                Vector256<int> row0;
                Vector256<int> row1;
                Vector256<int> row2;
                Vector256<int> row3;
                Vector256<int> row4;
                Vector256<int> row5;
                Vector256<int> row6;
                Vector256<int> row7;

                if (hasCodedCoefficients)
                {
                    row0 = Vector256.LoadUnsafe(ref inputBase, (nuint)(((row + 0) * inputWidth) + column));
                    row1 = Vector256.LoadUnsafe(ref inputBase, (nuint)(((row + 1) * inputWidth) + column));
                    row2 = Vector256.LoadUnsafe(ref inputBase, (nuint)(((row + 2) * inputWidth) + column));
                    row3 = Vector256.LoadUnsafe(ref inputBase, (nuint)(((row + 3) * inputWidth) + column));
                    row4 = Vector256.LoadUnsafe(ref inputBase, (nuint)(((row + 4) * inputWidth) + column));
                    row5 = Vector256.LoadUnsafe(ref inputBase, (nuint)(((row + 5) * inputWidth) + column));
                    row6 = Vector256.LoadUnsafe(ref inputBase, (nuint)(((row + 6) * inputWidth) + column));
                    row7 = Vector256.LoadUnsafe(ref inputBase, (nuint)(((row + 7) * inputWidth) + column));
                }
                else
                {
                    row0 = Vector256<int>.Zero;
                    row1 = Vector256<int>.Zero;
                    row2 = Vector256<int>.Zero;
                    row3 = Vector256<int>.Zero;
                    row4 = Vector256<int>.Zero;
                    row5 = Vector256<int>.Zero;
                    row6 = Vector256<int>.Zero;
                    row7 = Vector256<int>.Zero;
                }

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

        // The intermediate rows already contain contiguous column groups, avoiding a second transpose. Horizontal and
        // vertical flips are folded into these loads and row selections so flipped transforms need no reversal pass.
        for (int column = 0; column < width; column += laneCount)
        {
            int sourceColumn = config.FlipLeftToRight ? width - column - laneCount : column;

            // Sparse operators read only their declared input prefix. Complete operators still receive zeros for
            // rows omitted by the first axis, including the uncoded half of a sixty-four-point transform.
            for (int row = 0; row < TColumnOperator.InputLength; row++)
            {
                Vector256<int> value = row < rowCount
                    ? Vector256.LoadUnsafe(ref bufferBase, (nuint)((row * width) + sourceColumn))
                    : Vector256<int>.Zero;

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
        ReadOnlySpan<int> input,
        ReadOnlySpan<TSample> outputForRead,
        int strideForRead,
        Span<TSample> outputForWrite,
        int strideForWrite,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace,
        int bitDepth)
        where TSample : unmanaged
        where TOutputOperator : struct, Av1InverseTransformer.IAv1InverseTransformOutputOperator<TSample>
        where TColumnOperator : struct, IAv1Transform1dOperator
        where TRowOperator : struct, IAv1Transform1dOperator
    {
        const int laneCount = 4;

        int width = config.TransformSize.GetWidth();
        int height = config.TransformSize.GetHeight();
        int vectorLength = Math.Max(width, height) * laneCount;
        Av1TransformSize adjustedTransformSize = config.TransformSize.GetAdjusted();
        int inputWidth = adjustedTransformSize.GetWidth();
        int inputHeight = adjustedTransformSize.GetHeight();
        int rowCount = (config.NonzeroHeight + laneCount - 1) & -laneCount;
        int rowInputCount = (TRowOperator.InputLength + laneCount - 1) & -laneCount;
        int shift0 = config.Shift0;
        int shift1 = config.Shift1;
        bool normalizeRectangle = Math.Abs(config.TransformSize.GetRectangleLogRatio()) == 1;
        byte rowClampBits = (byte)(bitDepth + 8);
        byte columnClampBits = (byte)Math.Max(bitDepth + 6, 16);

        // Input and stage storage share one vector: each operator finishes consuming coefficients before its first
        // stage write. The second vector holds outputs, and the remaining raster stores completed first-axis rows.
        // Each vector spans only the longer active axis; no fields beyond that transform length are accessed.
        ref int workspaceBase = ref MemoryMarshal.GetReference(workspace);
        ref Av1TransformVector<Vector128<int>> tempIn = ref Unsafe.As<int, Av1TransformVector<Vector128<int>>>(ref workspaceBase);
        ref Av1TransformVector<Vector128<int>> tempOut =
            ref Unsafe.As<int, Av1TransformVector<Vector128<int>>>(ref Unsafe.Add(ref workspaceBase, vectorLength));

        ref Av1TransformVector<Vector128<int>> step = ref tempIn;
        Span<int> buffer = workspace.Slice(2 * vectorLength, width * height);
        ref int inputBase = ref MemoryMarshal.GetReference(input);
        ref int bufferBase = ref MemoryMarshal.GetReference(buffer);

        // A 4-by-4 transpose changes four raster rows into four coefficient-position vectors. Each lane then remains
        // one independent row throughout the complete first-axis stage network.
        for (int row = 0; row < rowCount; row += laneCount)
        {
            for (int column = 0; column < rowInputCount; column += laneCount)
            {
                bool hasCodedCoefficients = row < inputHeight && column < inputWidth;
                Vector128<int> row0;
                Vector128<int> row1;
                Vector128<int> row2;
                Vector128<int> row3;

                if (hasCodedCoefficients)
                {
                    row0 = Vector128.LoadUnsafe(ref inputBase, (nuint)(((row + 0) * inputWidth) + column));
                    row1 = Vector128.LoadUnsafe(ref inputBase, (nuint)(((row + 1) * inputWidth) + column));
                    row2 = Vector128.LoadUnsafe(ref inputBase, (nuint)(((row + 2) * inputWidth) + column));
                    row3 = Vector128.LoadUnsafe(ref inputBase, (nuint)(((row + 3) * inputWidth) + column));
                }
                else
                {
                    row0 = Vector128<int>.Zero;
                    row1 = Vector128<int>.Zero;
                    row2 = Vector128<int>.Zero;
                    row3 = Vector128<int>.Zero;
                }

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

        // Contiguous four-column groups become the independent lanes for the second axis. Flip selection is applied
        // while reading the intermediate block and selecting completed rows, avoiding any extra copy or reversal.
        for (int column = 0; column < width; column += laneCount)
        {
            int sourceColumn = config.FlipLeftToRight ? width - column - laneCount : column;

            // Sparse operators read only their declared input prefix. Complete operators still receive zeros for
            // rows omitted by the first axis, including the uncoded half of a sixty-four-point transform.
            for (int row = 0; row < TColumnOperator.InputLength; row++)
            {
                Vector128<int> value = row < rowCount
                    ? Vector128.LoadUnsafe(ref bufferBase, (nuint)((row * width) + sourceColumn))
                    : Vector128<int>.Zero;

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
        ReadOnlySpan<int> input,
        ReadOnlySpan<TSample> outputForRead,
        int strideForRead,
        Span<TSample> outputForWrite,
        int strideForWrite,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace,
        int bitDepth)
        where TSample : unmanaged
        where TOutputOperator : struct, Av1InverseTransformer.IAv1InverseTransformOutputOperator<TSample>
        where TColumnOperator : struct, IAv1Transform1dOperator
        where TRowOperator : struct, IAv1Transform1dOperator
    {
        int width = config.TransformSize.GetWidth();
        int height = config.TransformSize.GetHeight();
        Av1TransformSize adjustedTransformSize = config.TransformSize.GetAdjusted();
        int inputWidth = adjustedTransformSize.GetWidth();
        int inputHeight = adjustedTransformSize.GetHeight();
        int rowInputCount = Math.Min(inputWidth, TRowOperator.InputLength);
        int vectorLength = Math.Max(width, height);
        int shift0 = config.Shift0;
        int shift1 = config.Shift1;
        bool normalizeRectangle = Math.Abs(config.TransformSize.GetRectangleLogRatio()) == 1;
        byte rowClampBits = (byte)(bitDepth + 8);
        byte columnClampBits = (byte)Math.Max(bitDepth + 6, 16);

        // Stage exchange starts after the operator consumes its input. Sharing those spans leaves only two active
        // vectors before the raster intermediate, with no copy required between transform stages.
        Span<int> tempIn = workspace[..vectorLength];
        Span<int> tempOut = workspace.Slice(vectorLength, vectorLength);
        Span<int> step = tempIn;
        Span<int> buffer = workspace.Slice(2 * vectorLength, width * height);

        for (int row = 0; row < config.NonzeroHeight; row++)
        {
            int rowOffset = row * width;
            tempIn[..TRowOperator.InputLength].Clear();

            if (row < inputHeight)
            {
                int inputOffset = row * inputWidth;

                for (int column = 0; column < rowInputCount; column++)
                {
                    int value = input[inputOffset + column];
                    value = normalizeRectangle
                        ? Av1Math.RoundShift((long)value * Av1InverseTransformMath.NewInverseSqrt2, Av1InverseTransformMath.NewSqrt2BitCount)
                        : value;
                    tempIn[column] = Av1Transform1dMath.Clamp(value, rowClampBits);
                }
            }

            TRowOperator.Transform(tempIn, tempOut, step, config.CosBitRow, config.StageRangeRow);
            Av1InverseTransformMath.RoundShiftArray(tempOut, width, -shift0);
            tempOut[..width].CopyTo(buffer.Slice(rowOffset, width));
        }

        for (int column = 0; column < width; column++)
        {
            int sourceColumn = config.FlipLeftToRight ? width - column - 1 : column;

            // The first axis writes only potentially nonzero rows. Initialize every input consumed by the selected
            // second-axis operator so a complete identity or DCT kernel cannot read prior-block workspace contents.
            for (int row = 0; row < TColumnOperator.InputLength; row++)
            {
                tempIn[row] = row < config.NonzeroHeight
                    ? Av1Transform1dMath.Clamp(buffer[(row * width) + sourceColumn], columnClampBits)
                    : 0;
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
