// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
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
    /// Defines one AV1 forward transform which can be specialized for the selected sample and SIMD lane type.
    /// </summary>
    /// <remarks>
    /// A concrete operator identifies the transform stage network. The two-dimensional driver selects the sample type
    /// and vector width once per block, allowing the JIT to specialize the complete network without interface dispatch
    /// inside the transform stages.
    /// </remarks>
    internal interface IAv1ForwardTransform1dOperator
    {
        /// <summary>
        /// Transforms the independent axes stored in each value lane.
        /// </summary>
        /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
        /// <param name="values">The first value in the strided transform block.</param>
        /// <param name="inputStride">The byte distance between consecutive input positions.</param>
        /// <param name="outputStride">The byte distance between consecutive output positions.</param>
        /// <param name="buffer0">The first fixed transform-stage buffer.</param>
        /// <param name="buffer1">The second fixed transform-stage buffer.</param>
        /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
        public static abstract void Transform<TValue>(
            ref byte values,
            nint inputStride,
            nint outputStride,
            ref Av1TransformVector<TValue> buffer0,
            ref Av1TransformVector<TValue> buffer1,
            int cosBit)
            where TValue : struct;
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

        DispatchColumn(input, coefficients, stride, bitDepth, ref config, workspace);
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
        Span<short> input,
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
        Span<short> input,
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
        Span<short> input,
        Span<int> coefficients,
        uint stride,
        int bitDepth,
        ref Av1Transform2dFlipConfiguration config,
        Span<int> workspace)
        where TColumnOperator : struct, IAv1ForwardTransform1dOperator
        where TRowOperator : struct, IAv1ForwardTransform1dOperator
    {
        // Highway keeps eight-bit transform stages in Int16 lanes and promotes only the large rectangular layouts.
        // Scalar libaom uses Int32, so hardware without packed Int16 support follows that exact fallback instead.
        if (bitDepth == 8 && Vector128.IsHardwareAccelerated)
        {
            TransformPacked<TColumnOperator, TRowOperator>(input, coefficients, stride, ref config, workspace);
            return;
        }

        TransformExpanded<TColumnOperator, TRowOperator>(input, coefficients, stride, ref config, workspace);
    }

    /// <summary>
    /// Applies the libaom Int16 stage pipeline used for eight-bit residuals.
    /// </summary>
    /// <typeparam name="TColumnOperator">The column transform operator.</typeparam>
    /// <typeparam name="TRowOperator">The row transform operator.</typeparam>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="config">The resolved transform functions, shifts, and axis orientation.</param>
    /// <param name="workspace">The reusable transform workspace.</param>
    private static void TransformPacked<TColumnOperator, TRowOperator>(
        Span<short> input,
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
        int blockLaneCount = Avx512BW.IsSupported
            ? Vector512<short>.Count
            : Avx2.IsSupported ? Vector256<short>.Count : Vector128<short>.Count;

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
        // once more adapts libaom's native layout to the row-major coefficient contract used by ImageSharp.
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
    /// Applies the libaom Int32 stage pipeline used for high-bit-depth residuals and scalar fallback.
    /// </summary>
    /// <typeparam name="TColumnOperator">The column transform operator.</typeparam>
    /// <typeparam name="TRowOperator">The row transform operator.</typeparam>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="config">The resolved transform functions, shifts, and axis orientation.</param>
    /// <param name="workspace">The reusable transform workspace.</param>
    private static void TransformExpanded<TColumnOperator, TRowOperator>(
        Span<short> input,
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
        Span<short> input,
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

            if (Avx512BW.IsSupported && width >= Vector512<short>.Count)
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

            if (Avx2.IsSupported && width >= Vector256<short>.Count)
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
        Span<short> input,
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
    /// <typeparam name="TOperator">The transform operator applied to each independent axis.</typeparam>
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
        if (Avx512BW.IsSupported && transformCount >= Vector512<short>.Count)
        {
            TransformAxis<TOperator, short, Vector512<short>>(buffer, transformCount, inputStride, outputStride, cosBit, workspace);
            return;
        }

        if (Avx2.IsSupported && transformCount >= Vector256<short>.Count)
        {
            TransformAxis<TOperator, short, Vector256<short>>(buffer, transformCount, inputStride, outputStride, cosBit, workspace);
            return;
        }

        TransformAxis<TOperator, short, Vector128<short>>(buffer, transformCount, inputStride, outputStride, cosBit, workspace);
    }

    /// <summary>
    /// Applies one signed thirty-two-bit transform axis using the widest efficient lane count available for the block.
    /// </summary>
    /// <typeparam name="TOperator">The transform operator applied to each independent axis.</typeparam>
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
            TransformAxis<TOperator, int, Vector512<int>>(buffer, transformCount, inputStride, outputStride, cosBit, workspace);
            return;
        }

        if (Vector256.IsHardwareAccelerated && transformCount >= Vector256<int>.Count)
        {
            TransformAxis<TOperator, int, Vector256<int>>(buffer, transformCount, inputStride, outputStride, cosBit, workspace);
            return;
        }

        if (Vector128.IsHardwareAccelerated && transformCount >= Vector128<int>.Count)
        {
            TransformAxis<TOperator, int, Vector128<int>>(buffer, transformCount, inputStride, outputStride, cosBit, workspace);
            return;
        }

        TransformAxis<TOperator, int, int>(buffer, transformCount, inputStride, outputStride, cosBit, workspace);
    }

    /// <summary>
    /// Applies one transform stage network to independent axes held in scalar or SIMD lanes.
    /// </summary>
    /// <typeparam name="TOperator">The transform operator applied to each independent axis.</typeparam>
    /// <typeparam name="TElement">The scalar storage element.</typeparam>
    /// <typeparam name="TValue">The scalar or SIMD value containing independent transform axes.</typeparam>
    /// <param name="buffer">The transform block.</param>
    /// <param name="transformCount">The number of independent axes.</param>
    /// <param name="inputStride">The number of storage elements between input positions.</param>
    /// <param name="outputStride">The number of storage elements between output positions.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    /// <param name="workspace">The reusable transform-stage workspace.</param>
    private static void TransformAxis<TOperator, TElement, TValue>(
        Span<TElement> buffer,
        int transformCount,
        int inputStride,
        int outputStride,
        int cosBit,
        Span<int> workspace)
        where TOperator : struct, IAv1ForwardTransform1dOperator
        where TElement : unmanaged
        where TValue : struct
    {
        int vectorByteLength = Unsafe.SizeOf<Av1TransformVector<TValue>>();
        int laneCount = Unsafe.SizeOf<TValue>() / Unsafe.SizeOf<TElement>();
        ref byte workspaceBase = ref Unsafe.As<int, byte>(ref MemoryMarshal.GetReference(workspace));
        ref Av1TransformVector<TValue> buffer0 = ref Unsafe.As<byte, Av1TransformVector<TValue>>(ref workspaceBase);
        ref Av1TransformVector<TValue> buffer1 =
            ref Unsafe.As<byte, Av1TransformVector<TValue>>(ref Unsafe.Add(ref workspaceBase, vectorByteLength));

        ref TElement sourceBase = ref MemoryMarshal.GetReference(buffer);
        nint inputByteStride = inputStride * Unsafe.SizeOf<TElement>();
        nint outputByteStride = outputStride * Unsafe.SizeOf<TElement>();

        // Each lane is an independent row or column. The operators load from and retire coefficients directly to
        // the strided block, matching Highway's two-buffer stage network without a separate input/output copy pass.
        for (int batch = 0; batch < transformCount; batch += laneCount)
        {
            ref byte values = ref Unsafe.As<TElement, byte>(ref Unsafe.Add(ref sourceBase, batch));

            TOperator.Transform<TValue>(ref values, inputByteStride, outputByteStride, ref buffer0, ref buffer1, cosBit);
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
        int tileSize = Avx2.IsSupported && Math.Min(sourceWidth, sourceHeight) >= 16 ? 16 : Math.Min(sourceWidth, sourceHeight) >= 8 ? 8 : 4;
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
    /// Promotes and transposes the large packed layouts at the same axis boundary as libaom.
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
        int tileSize = Avx512BW.IsSupported ? 16 : Vector256.IsHardwareAccelerated ? 8 : 4;

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

            if (Avx512BW.IsSupported)
            {
                for (; column <= width - Vector512<short>.Count; column += Vector512<short>.Count)
                {
                    (Vector512<int> lower, Vector512<int> upper) = Vector512.Widen(Vector512.LoadUnsafe(ref sourceRow, (nuint)column));

                    lower.StoreUnsafe(ref destinationRow, (nuint)column);
                    upper.StoreUnsafe(ref destinationRow, (nuint)(column + Vector512<int>.Count));
                }
            }

            if (Avx2.IsSupported)
            {
                for (; column <= width - Vector256<short>.Count; column += Vector256<short>.Count)
                {
                    (Vector256<int> lower, Vector256<int> upper) = Vector256.Widen(Vector256.LoadUnsafe(ref sourceRow, (nuint)column));

                    lower.StoreUnsafe(ref destinationRow, (nuint)column);
                    upper.StoreUnsafe(ref destinationRow, (nuint)(column + Vector256<int>.Count));
                }
            }

            for (; column <= width - Vector128<short>.Count; column += Vector128<short>.Count)
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
