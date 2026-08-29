// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Applies HEVC inverse transforms and reconstructs predicted samples.
/// </summary>
internal static partial class HevcInverseTransformer
{
    /// <summary>
    /// The signed residual precision used after the second inverse-transform pass.
    /// </summary>
    private const int ResidualPrecision = 16;

    /// <summary>
    /// Gets the scratch length required for the specified rectangular transform block.
    /// </summary>
    /// <param name="log2Width">The base-two logarithm of the transform-block width.</param>
    /// <param name="log2Height">The base-two logarithm of the transform-block height.</param>
    /// <returns>The required number of signed thirty-two-bit elements.</returns>
    public static int GetScratchLength(int log2Width, int log2Height)
    {
        DebugGuard.MustBeBetweenOrEqualTo(log2Width, 2, 5, nameof(log2Width));
        DebugGuard.MustBeBetweenOrEqualTo(log2Height, 2, 5, nameof(log2Height));
        return 2 << (log2Width + log2Height);
    }

    /// <summary>
    /// Reconstructs one transform block by adding its inverse-transformed residual to the predicted samples.
    /// </summary>
    /// <param name="coefficients">The dequantized transform coefficients in raster order.</param>
    /// <param name="destination">The predicted samples beginning at the transform-block origin.</param>
    /// <param name="destinationStride">The destination row stride in samples.</param>
    /// <param name="log2Width">The base-two logarithm of the transform-block width.</param>
    /// <param name="log2Height">The base-two logarithm of the transform-block height.</param>
    /// <param name="bitDepth">The reconstructed component precision.</param>
    /// <param name="maxTransformDynamicRange">The transform dynamic range excluding its sign bit.</param>
    /// <param name="useDiscreteSineTransform">Whether the four-by-four luma intra block uses the discrete sine transform.</param>
    /// <param name="scratch">The caller-owned scratch returned by <see cref="GetScratchLength(int, int)"/>.</param>
    public static void TransformAdd(
        ReadOnlySpan<int> coefficients,
        Span<ushort> destination,
        int destinationStride,
        int log2Width,
        int log2Height,
        int bitDepth,
        int maxTransformDynamicRange,
        bool useDiscreteSineTransform,
        Span<int> scratch)
    {
        DebugGuard.MustBeBetweenOrEqualTo(log2Width, 2, 5, nameof(log2Width));
        DebugGuard.MustBeBetweenOrEqualTo(log2Height, 2, 5, nameof(log2Height));
        DebugGuard.MustBeBetweenOrEqualTo(bitDepth, 8, 16, nameof(bitDepth));
        DebugGuard.IsTrue(!useDiscreteSineTransform || (log2Width == 2 && log2Height == 2), "The HEVC inverse DST is defined only for four-by-four blocks.");

        int width = 1 << log2Width;
        int height = 1 << log2Height;
        int sampleCount = width * height;
        DebugGuard.IsTrue(coefficients.Length >= sampleCount, "The coefficient span is shorter than the transform block.");
        DebugGuard.IsTrue(scratch.Length >= sampleCount * 2, "The scratch span is shorter than the inverse-transform requirement.");

        Span<int> first = scratch[..sampleCount];
        Span<int> second = scratch.Slice(sampleCount, sampleCount);
        Span<int> residual = TransformCore(
            coefficients[..sampleCount],
            first,
            second,
            width,
            height,
            bitDepth,
            maxTransformDynamicRange,
            useDiscreteSineTransform);

        AddResidual(residual, destination, destinationStride, width, height, bitDepth);
    }

    /// <summary>
    /// Applies one two-dimensional inverse transform and writes signed residual samples in raster order.
    /// </summary>
    /// <param name="coefficients">The dequantized transform coefficients in raster order.</param>
    /// <param name="residual">The destination residual samples in raster order.</param>
    /// <param name="log2Width">The base-two logarithm of the transform-block width.</param>
    /// <param name="log2Height">The base-two logarithm of the transform-block height.</param>
    /// <param name="bitDepth">The reconstructed component precision.</param>
    /// <param name="maxTransformDynamicRange">The transform dynamic range excluding its sign bit.</param>
    /// <param name="useDiscreteSineTransform">Whether the four-by-four luma intra block uses the discrete sine transform.</param>
    /// <param name="scratch">The caller-owned scratch returned by <see cref="GetScratchLength(int, int)"/>.</param>
    public static void Transform(
        ReadOnlySpan<int> coefficients,
        Span<int> residual,
        int log2Width,
        int log2Height,
        int bitDepth,
        int maxTransformDynamicRange,
        bool useDiscreteSineTransform,
        Span<int> scratch)
    {
        int width = 1 << log2Width;
        int height = 1 << log2Height;
        int sampleCount = width * height;
        DebugGuard.IsTrue(residual.Length >= sampleCount, "The residual span is shorter than the transform block.");
        DebugGuard.IsTrue(scratch.Length >= sampleCount * 2, "The scratch span is shorter than the inverse-transform requirement.");

        Span<int> transformed = TransformCore(
            coefficients[..sampleCount],
            scratch[..sampleCount],
            scratch.Slice(sampleCount, sampleCount),
            width,
            height,
            bitDepth,
            maxTransformDynamicRange,
            useDiscreteSineTransform);

        transformed.CopyTo(residual);
    }

    /// <summary>
    /// Dispatches both separable transform passes through closed operators selected from the block dimensions.
    /// </summary>
    /// <param name="coefficients">The complete dequantized coefficient block.</param>
    /// <param name="first">The first full-block scratch buffer.</param>
    /// <param name="second">The second full-block scratch buffer.</param>
    /// <param name="width">The transform-block width.</param>
    /// <param name="height">The transform-block height.</param>
    /// <param name="bitDepth">The reconstructed component precision.</param>
    /// <param name="maxTransformDynamicRange">The transform dynamic range excluding its sign bit.</param>
    /// <param name="useDiscreteSineTransform">Whether both four-point passes use the discrete sine transform.</param>
    /// <returns>The scratch buffer containing the raster-ordered residual.</returns>
    private static Span<int> TransformCore(
        ReadOnlySpan<int> coefficients,
        Span<int> first,
        Span<int> second,
        int width,
        int height,
        int bitDepth,
        int maxTransformDynamicRange,
        bool useDiscreteSineTransform)
    {
        int dynamicMinimum = -(1 << maxTransformDynamicRange);
        int dynamicMaximum = (1 << maxTransformDynamicRange) - 1;

        // HEVC moves one normalization bit from the second pass to the first. The intermediate clip therefore
        // belongs after the vertical pass and must not be combined with the final residual clipping operation.
        Span<int> vertical = TransformDimension(
            coefficients,
            first,
            second,
            height,
            width,
            7,
            dynamicMinimum,
            dynamicMaximum,
            useDiscreteSineTransform);

        Span<int> horizontalInput = vertical.Overlaps(first) ? second : first;
        Transpose(vertical, horizontalInput, height, width);

        Span<int> horizontalWorkspace = horizontalInput.Overlaps(first) ? second : first;
        int secondShift = maxTransformDynamicRange + 5 - bitDepth;
        Span<int> horizontal = TransformDimension(
            horizontalInput,
            horizontalWorkspace,
            horizontalInput,
            width,
            height,
            secondShift,
            -(1 << (ResidualPrecision - 1)),
            (1 << (ResidualPrecision - 1)) - 1,
            useDiscreteSineTransform);

        Span<int> residual = horizontal.Overlaps(first) ? second : first;
        Transpose(horizontal, residual, width, height);
        return residual;
    }

    /// <summary>
    /// Selects the statically specialized operator for one transform dimension.
    /// </summary>
    /// <param name="source">The frequency rows followed by contiguous independent lines.</param>
    /// <param name="initial">The initial operator output buffer.</param>
    /// <param name="alternate">The alternate combination buffer.</param>
    /// <param name="size">The transform dimension.</param>
    /// <param name="lineCount">The number of independent lines transformed together.</param>
    /// <param name="shift">The rounded right shift applied to the spatial results.</param>
    /// <param name="minimum">The inclusive output minimum.</param>
    /// <param name="maximum">The inclusive output maximum.</param>
    /// <param name="useDiscreteSineTransform">Whether the four-point pass uses the discrete sine transform.</param>
    /// <returns>The buffer containing spatial rows followed by contiguous independent lines.</returns>
    private static Span<int> TransformDimension(
        ReadOnlySpan<int> source,
        Span<int> initial,
        Span<int> alternate,
        int size,
        int lineCount,
        int shift,
        int minimum,
        int maximum,
        bool useDiscreteSineTransform)
        => (size, useDiscreteSineTransform) switch
        {
            (4, true) => TransformDimension<DiscreteSine4Operator>(source, initial, alternate, lineCount, shift, minimum, maximum),
            (4, false) => TransformDimension<DiscreteCosine4Operator>(source, initial, alternate, lineCount, shift, minimum, maximum),
            (8, _) => TransformDimension<DiscreteCosine8Operator>(source, initial, alternate, lineCount, shift, minimum, maximum),
            (16, _) => TransformDimension<DiscreteCosine16Operator>(source, initial, alternate, lineCount, shift, minimum, maximum),
            _ => TransformDimension<DiscreteCosine32Operator>(source, initial, alternate, lineCount, shift, minimum, maximum)
        };

    /// <summary>
    /// Invokes one statically selected inverse-transform operator.
    /// </summary>
    /// <typeparam name="TOperator">The selected inverse-transform operator.</typeparam>
    /// <param name="source">The frequency rows followed by contiguous independent lines.</param>
    /// <param name="initial">The initial operator output buffer.</param>
    /// <param name="alternate">The alternate combination buffer.</param>
    /// <param name="lineCount">The number of independent lines transformed together.</param>
    /// <param name="shift">The rounded right shift applied to the spatial results.</param>
    /// <param name="minimum">The inclusive output minimum.</param>
    /// <param name="maximum">The inclusive output maximum.</param>
    /// <returns>The buffer containing spatial rows followed by contiguous independent lines.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Span<int> TransformDimension<TOperator>(
        ReadOnlySpan<int> source,
        Span<int> initial,
        Span<int> alternate,
        int lineCount,
        int shift,
        int minimum,
        int maximum)
        where TOperator : struct, IHevcInverseTransformOperator
    {
        if (!TOperator.UsesButterfly)
        {
            TransformDense<TOperator>(source, initial, lineCount, shift, minimum, maximum);
            return initial;
        }

        PopulateButterflyGroups<TOperator>(source, initial, lineCount);
        return CombineButterflyGroups<TOperator>(initial, alternate, lineCount, shift, minimum, maximum);
    }
}
