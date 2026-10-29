// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AcStrategy;

internal static partial class JxlAcStrategyEncoder
{
    /// <summary>
    /// The native red-green loss weight pow(8.2, 8), narrowed once from double to float: bits 0x4B9BF4A0.
    /// </summary>
    private const float XChannelLossMultiplier = 20441408F;

    /// <summary>
    /// The native blue loss weight pow(1.03, 8), narrowed once from double to float: bits 0x3FA22586.
    /// </summary>
    private const float BChannelLossMultiplier = 1.2667701244354248F;

    /// <summary>
    /// Estimates coefficient coding cost and masked reconstruction loss for one candidate transform.
    /// </summary>
    /// <param name="acs">The candidate strategy. Its pixel extent fits the source and the prepared matrix storage.</param>
    /// <param name="entropyMultiplier">The candidate's fixed coding-cost multiplier.</param>
    /// <param name="x">The source pixel column of the candidate's top-left corner.</param>
    /// <param name="y">The source pixel row of the candidate's top-left corner.</param>
    /// <param name="config">The borrowed source, quantization, mask and matrix storage, with their physical strides.</param>
    /// <param name="matrixStorage">The prepared coefficient tables borrowed before the search loops.</param>
    /// <param name="cmapFactors">The X, Y and B color-correlation factors. The Y factor is zero.</param>
    /// <param name="block">Three packed coefficient regions, each large enough for this strategy's sample count.</param>
    /// <param name="fullScratchSpace">One maximum-area error region followed by the existing transform workspace.</param>
    /// <param name="entropy">The estimated coding cost plus information loss.</param>
    /// <returns>True after the estimate is complete. Storage and strategy validity are established by the search owner.</returns>
    public static bool EstimateEntropy(
        JxlAcStrategy acs,
        float entropyMultiplier,
        int x,
        int y,
        JxlAcStrategyConfiguration config,
        ReadOnlySpan<float> matrixStorage,
        Span<float> cmapFactors,
        Span<float> block,
        Span<float> fullScratchSpace,
        out float entropy)
    {
        int blocksX = acs.CoveredBlocksX;
        int blocksY = acs.CoveredBlocksY;
        int numBlocks = blocksX * blocksY;
        int size = numBlocks * JxlFrameDimensions.DctBlockSize;
        Span<float> error = fullScratchSpace[..size];
        Span<float> transformScratch = fullScratchSpace[JxlAcStrategy.MaximumCoefficientArea..];

        // Borrow once before candidate processing. Neither the frame-owned source nor the prepared matrices change during the estimate.
        // Independent physical strides belong to their own fields; reconstructed error below is a tightly packed candidate rectangle.
        ReadOnlySpan<float> sourceX = config.SourceRows[0].Span;
        ReadOnlySpan<float> sourceY = config.SourceRows[1].Span;
        ReadOnlySpan<float> sourceB = config.SourceRows[2].Span;
        ReadOnlySpan<float> quantField = config.QuantFieldRow.Span;
        ReadOnlySpan<float> mask = config.Masking1x1FieldRow.Span;
        int sourceOffset = (y * config.SourceStride) + x;
        JxlTransformsEncoder.TransformFromPixels(acs.Strategy, sourceX[sourceOffset..], config.SourceStride, block[..size], transformScratch);
        JxlTransformsEncoder.TransformFromPixels(acs.Strategy, sourceY[sourceOffset..], config.SourceStride, block.Slice(size, size), transformScratch);
        JxlTransformsEncoder.TransformFromPixels(acs.Strategy, sourceB[sourceOffset..], config.SourceStride, block.Slice(2 * size, size), transformScratch);

        int quantX = x / JxlFrameDimensions.BlockDimensions;
        int quantY = y / JxlFrameDimensions.BlockDimensions;
        int quantOffset = (quantY * config.QuantFieldStride) + quantX;
        float quantNorm;
        if (numBlocks == 1)
        {
            quantNorm = quantField[quantOffset];
        }
        else if (numBlocks == 2)
        {
            // Native selection uses the larger field value for the smallest rectangular transforms, rather than the general sixteenth norm.
            int next = blocksY == 2 ? quantOffset + config.QuantFieldStride : quantOffset + 1;
            quantNorm = MathF.Max(quantField[quantOffset], quantField[next]);
        }
        else
        {
            float powers = 0;
            for (int iy = 0; iy < blocksY; iy++)
            {
                ReadOnlySpan<float> row = quantField.Slice(quantOffset + (iy * config.QuantFieldStride), blocksX);
                for (int ix = 0; ix < blocksX; ix++)
                {
                    float value = row[ix];
                    value *= value;
                    value *= value;
                    value *= value;
                    powers += value * value;
                }
            }

            quantNorm = MathF.Pow(powers / numBlocks, 1F / 16F);
        }

        entropy = 0;
        Vector256<float> loss = Vector256<float>.Zero;
        ReadOnlySpan<float> luminance = block.Slice(size, size);
        for (int c = 0; c < 3; c++)
        {
            ReadOnlySpan<float> coefficients = block.Slice(c * size, size);
            ReadOnlySpan<float> inverseMatrix = config.Dequant.GetInverseMatrix(matrixStorage, acs.Strategy, c)[..size];
            ReadOnlySpan<float> matrix = config.Dequant.GetMatrix(matrixStorage, acs.Strategy, c)[..size];
            EntropyOperator operation = new(quantNorm, cmapFactors[c]);
            float coefficientCost = ComputeErrors(coefficients, luminance, inverseMatrix, matrix, error, operation, out int nonzero);

            // All three coefficient regions are complete before reconstruction starts. Output uses only the first region;
            // the Y coefficients remain available for both color-correlation subtractions until the last channel is processed.
            JxlTransformsDecoder.TransformToPixels(acs.Strategy, error, block[..size], blocksX * JxlFrameDimensions.BlockDimensions, transformScratch);
            LossOperator lossOperation = new(c == 0 ? 12F : c == 1 ? 0F : 4F);
            Vector256<float> channelLoss = ComputeLoss(
                block[..size],
                blocksX * JxlFrameDimensions.BlockDimensions,
                blocksY * JxlFrameDimensions.BlockDimensions,
                mask,
                config.Masking1x1FieldStride,
                config.Mask1x1Width,
                x,
                y,
                lossOperation);

            float channelMultiplier = c == 0 ? XChannelLossMultiplier : c == 1 ? 1F : BChannelLossMultiplier;
            loss += channelLoss * Vector256.Create(channelMultiplier);
            entropy += config.CostDelta * coefficientCost;
            int bits = JxlMath.CeilLog2Nonzero(nonzero + 1) + 1;
            entropy += config.ZerosMultiplier * (JxlMath.CeilLog2Nonzero(bits + 17) + bits);

            if (c == 0 && numBlocks >= 2)
            {
                // Bound the extra red-green penalty at four. A minimum of three would instead over-penalize every small rectangle.
                float weight = 1F + MathF.Min(3F, numBlocks / 8F);
                entropy *= weight;
                loss *= Vector256.Create(weight);
            }
        }

        // The eighth norm is a mean per pixel, not per block column. Every block contains 64 samples, including all channels' weighted errors.
        float informationLoss = MathF.Pow(Vector256.Sum(loss) / size, 1F / 8F) * size / quantNorm;
        entropy *= entropyMultiplier;
        entropy += config.InfoLossMultiplier * informationLoss;

        return true;
    }

    /// <summary>
    /// Writes every dequantized coefficient error while accumulating its coding cost and nonzero count in the same traversal.
    /// </summary>
    /// <param name="coefficients">One channel's logical coefficient region.</param>
    /// <param name="luminance">The corresponding Y coefficients, retained until all channels have been read.</param>
    /// <param name="inverseMatrix">The forward quantization weights for this channel.</param>
    /// <param name="matrix">The inverse quantization weights for restoring coefficient errors.</param>
    /// <param name="error">The existing error region, with one output per coefficient.</param>
    /// <param name="operation">The candidate's quantization scale and channel-correlation factor.</param>
    /// <param name="nonzero">The exact number of rounded nonzero coefficients.</param>
    /// <returns>The sum of square roots of absolute rounded coefficients.</returns>
    private static float ComputeErrors(
        ReadOnlySpan<float> coefficients,
        ReadOnlySpan<float> luminance,
        ReadOnlySpan<float> inverseMatrix,
        ReadOnlySpan<float> matrix,
        Span<float> error,
        EntropyOperator operation,
        out int nonzero)
    {
        ref float input = ref MemoryMarshal.GetReference(coefficients);
        ref float inputY = ref MemoryMarshal.GetReference(luminance);
        ref float inverseWeights = ref MemoryMarshal.GetReference(inverseMatrix);
        ref float weights = ref MemoryMarshal.GetReference(matrix);
        ref float output = ref MemoryMarshal.GetReference(error);
        float entropy = 0;
        float count = 0;
        int i = 0;

        // Each lane describes one coefficient. Four inputs, an error output, and two reductions share these loads;
        // separate tensor calls would require intermediate storage and extra full passes over the candidate.
        // Valid coefficient areas are multiples of 64, so the widest enabled register consumes the complete region without narrower follow-up passes.
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<float> costs = Vector512<float>.Zero;
            Vector512<float> counts = Vector512<float>.Zero;
            for (; i <= coefficients.Length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                Vector512<float> value = operation.Invoke(
                    Vector512.LoadUnsafe(ref input, (uint)i),
                    Vector512.LoadUnsafe(ref inputY, (uint)i),
                    Vector512.LoadUnsafe(ref inverseWeights, (uint)i),
                    Vector512.LoadUnsafe(ref weights, (uint)i),
                    out Vector512<float> cost,
                    out Vector512<float> used);

                value.StoreUnsafe(ref output, (uint)i);
                costs += cost;
                counts += used;
            }

            entropy += Vector512.Sum(costs);
            count += Vector512.Sum(counts);
        }

        else if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> costs = Vector256<float>.Zero;
            Vector256<float> counts = Vector256<float>.Zero;
            for (; i <= coefficients.Length - Vector256<float>.Count; i += Vector256<float>.Count)
            {
                Vector256<float> value = operation.Invoke(
                    Vector256.LoadUnsafe(ref input, (uint)i),
                    Vector256.LoadUnsafe(ref inputY, (uint)i),
                    Vector256.LoadUnsafe(ref inverseWeights, (uint)i),
                    Vector256.LoadUnsafe(ref weights, (uint)i),
                    out Vector256<float> cost,
                    out Vector256<float> used);

                value.StoreUnsafe(ref output, (uint)i);
                costs += cost;
                counts += used;
            }

            entropy += Vector256.Sum(costs);
            count += Vector256.Sum(counts);
        }

        else if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> costs = Vector128<float>.Zero;
            Vector128<float> counts = Vector128<float>.Zero;
            for (; i <= coefficients.Length - Vector128<float>.Count; i += Vector128<float>.Count)
            {
                Vector128<float> value = operation.Invoke(
                    Vector128.LoadUnsafe(ref input, (uint)i),
                    Vector128.LoadUnsafe(ref inputY, (uint)i),
                    Vector128.LoadUnsafe(ref inverseWeights, (uint)i),
                    Vector128.LoadUnsafe(ref weights, (uint)i),
                    out Vector128<float> cost,
                    out Vector128<float> used);

                value.StoreUnsafe(ref output, (uint)i);
                costs += cost;
                counts += used;
            }

            entropy += Vector128.Sum(costs);
            count += Vector128.Sum(counts);
        }

        for (; i < coefficients.Length; i++)
        {
            Unsafe.Add(ref output, i) = operation.Invoke(
                Unsafe.Add(ref input, i),
                Unsafe.Add(ref inputY, i),
                Unsafe.Add(ref inverseWeights, i),
                Unsafe.Add(ref weights, i),
                out float cost,
                out float used);

            entropy += cost;
            count += used;
        }

        // At most 65,536 coefficients contribute ones. Float represents every possible count exactly, so this conversion loses no bits.
        nonzero = (int)count;

        return entropy;
    }

    /// <summary>
    /// Measures masked pixel error with the existing whole-eight-sample edge policy and fixed eight-lane accumulation order.
    /// </summary>
    /// <param name="pixels">The tightly packed reconstructed candidate error.</param>
    /// <param name="width">The candidate width in pixels, a multiple of eight.</param>
    /// <param name="height">The candidate height in pixels, a multiple of eight.</param>
    /// <param name="mask">The borrowed pixel mask from its logical origin.</param>
    /// <param name="maskStride">The mask row stride, in floats.</param>
    /// <param name="maskWidth">The logical mask width. Incomplete eight-sample groups do not contribute.</param>
    /// <param name="x">The candidate's first mask column.</param>
    /// <param name="y">The candidate's first mask row.</param>
    /// <param name="operation">The channel-specific mask offset and eighth-power calculation.</param>
    /// <returns>Eight loss accumulators, before the channel multiplier is applied.</returns>
    private static Vector256<float> ComputeLoss(
        ReadOnlySpan<float> pixels,
        int width,
        int height,
        ReadOnlySpan<float> mask,
        int maskStride,
        int maskWidth,
        int x,
        int y,
        LossOperator operation)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            return ComputeLoss512(pixels, width, height, mask, maskStride, maskWidth, x, y, operation);
        }

        if (Vector256.IsHardwareAccelerated)
        {
            return ComputeLoss256(pixels, width, height, mask, maskStride, maskWidth, x, y, operation);
        }

        if (Vector128.IsHardwareAccelerated)
        {
            return ComputeLoss128(pixels, width, height, mask, maskStride, maskWidth, x, y, operation);
        }

        return ComputeLossScalar(pixels, width, height, mask, maskStride, maskWidth, x, y, operation);
    }

    /// <inheritdoc cref="ComputeLoss"/>
    private static Vector256<float> ComputeLoss512(
        ReadOnlySpan<float> pixels,
        int width,
        int height,
        ReadOnlySpan<float> mask,
        int maskStride,
        int maskWidth,
        int x,
        int y,
        LossOperator operation)
    {
        ref float input = ref MemoryMarshal.GetReference(pixels);
        ref float masks = ref MemoryMarshal.GetReference(mask);
        Vector256<float> loss = Vector256<float>.Zero;

        // Keep block-column order outside the eight image rows, as in the native capped-eight loss calculation.
        for (int blockY = 0; blockY < height; blockY += 8)
        {
            for (int blockX = 0; blockX < width; blockX += 8)
            {
                int maskX = x + blockX;
                if (maskX + 8 > maskWidth)
                {
                    // This is the existing mask boundary, not permission to read physical row padding. It also handles an absent mask without a load.
                    continue;
                }

                // Two independent eight-pixel rows fill one 512-bit register. Fold the two results into the same eight lanes in row order.
                for (int dy = 0; dy < 8; dy += 2)
                {
                    int row = blockY + dy;
                    int pixelOffset = (row * width) + blockX;
                    int maskOffset = ((y + row) * maskStride) + maskX;
                    Vector512<float> values = Vector512.Create(
                        Vector256.LoadUnsafe(ref input, (uint)pixelOffset),
                        Vector256.LoadUnsafe(ref input, (uint)(pixelOffset + width)));

                    Vector512<float> weights = Vector512.Create(
                        Vector256.LoadUnsafe(ref masks, (uint)maskOffset),
                        Vector256.LoadUnsafe(ref masks, (uint)(maskOffset + maskStride)));

                    Vector512<float> result = operation.Invoke(values, weights);
                    loss += result.GetLower();
                    loss += result.GetUpper();
                }
            }
        }

        return loss;
    }

    /// <inheritdoc cref="ComputeLoss"/>
    private static Vector256<float> ComputeLoss256(
        ReadOnlySpan<float> pixels,
        int width,
        int height,
        ReadOnlySpan<float> mask,
        int maskStride,
        int maskWidth,
        int x,
        int y,
        LossOperator operation)
    {
        ref float input = ref MemoryMarshal.GetReference(pixels);
        ref float masks = ref MemoryMarshal.GetReference(mask);
        Vector256<float> loss = Vector256<float>.Zero;

        // Keep block-column order outside the eight image rows, as in the native capped-eight loss calculation.
        for (int blockY = 0; blockY < height; blockY += 8)
        {
            for (int blockX = 0; blockX < width; blockX += 8)
            {
                int maskX = x + blockX;
                if (maskX + 8 > maskWidth)
                {
                    // This is the existing mask boundary, not permission to read physical row padding. It also handles an absent mask without a load.
                    continue;
                }

                for (int dy = 0; dy < 8; dy++)
                {
                    int row = blockY + dy;
                    Vector256<float> values = Vector256.LoadUnsafe(ref input, (uint)((row * width) + blockX));
                    Vector256<float> weights = Vector256.LoadUnsafe(ref masks, (uint)(((y + row) * maskStride) + maskX));
                    loss += operation.Invoke(values, weights);
                }
            }
        }

        return loss;
    }

    /// <inheritdoc cref="ComputeLoss"/>
    private static Vector256<float> ComputeLoss128(
        ReadOnlySpan<float> pixels,
        int width,
        int height,
        ReadOnlySpan<float> mask,
        int maskStride,
        int maskWidth,
        int x,
        int y,
        LossOperator operation)
    {
        ref float input = ref MemoryMarshal.GetReference(pixels);
        ref float masks = ref MemoryMarshal.GetReference(mask);
        Vector128<float> low = Vector128<float>.Zero;
        Vector128<float> high = Vector128<float>.Zero;

        // Keep block-column order outside the eight image rows, as in the native capped-eight loss calculation.
        for (int blockY = 0; blockY < height; blockY += 8)
        {
            for (int blockX = 0; blockX < width; blockX += 8)
            {
                int maskX = x + blockX;
                if (maskX + 8 > maskWidth)
                {
                    // This is the existing mask boundary, not permission to read physical row padding. It also handles an absent mask without a load.
                    continue;
                }

                for (int dy = 0; dy < 8; dy++)
                {
                    int row = blockY + dy;
                    int pixelOffset = (row * width) + blockX;
                    int maskOffset = ((y + row) * maskStride) + maskX;
                    low += operation.Invoke(Vector128.LoadUnsafe(ref input, (uint)pixelOffset), Vector128.LoadUnsafe(ref masks, (uint)maskOffset));
                    high += operation.Invoke(
                        Vector128.LoadUnsafe(ref input, (uint)(pixelOffset + 4)),
                        Vector128.LoadUnsafe(ref masks, (uint)(maskOffset + 4)));
                }
            }
        }

        return Vector256.Create(low, high);
    }

    /// <inheritdoc cref="ComputeLoss"/>
    private static Vector256<float> ComputeLossScalar(
        ReadOnlySpan<float> pixels,
        int width,
        int height,
        ReadOnlySpan<float> mask,
        int maskStride,
        int maskWidth,
        int x,
        int y,
        LossOperator operation)
    {
        ref float input = ref MemoryMarshal.GetReference(pixels);
        ref float masks = ref MemoryMarshal.GetReference(mask);
        InlineArray8<float> storage = default;
        Span<float> loss = storage;

        // Keep block-column order outside the eight image rows, as in the native capped-eight loss calculation.
        for (int blockY = 0; blockY < height; blockY += 8)
        {
            for (int blockX = 0; blockX < width; blockX += 8)
            {
                int maskX = x + blockX;
                if (maskX + 8 > maskWidth)
                {
                    // This is the existing mask boundary, not permission to read physical row padding. It also handles an absent mask without a load.
                    continue;
                }

                for (int dy = 0; dy < 8; dy++)
                {
                    int row = blockY + dy;
                    int pixelOffset = (row * width) + blockX;
                    int maskOffset = ((y + row) * maskStride) + maskX;
                    for (int dx = 0; dx < 8; dx++)
                    {
                        loss[dx] += operation.Invoke(Unsafe.Add(ref input, pixelOffset + dx), Unsafe.Add(ref masks, maskOffset + dx));
                    }
                }
            }
        }

        return Vector256.Create(loss);
    }

    /// <summary>
    /// Computes one coefficient's error and coding statistics without changing the estimator's plain nearest-even rounding rule.
    /// </summary>
    private readonly struct EntropyOperator
    {
        /// <summary>
        /// The candidate's quantization-field norm.
        /// </summary>
        private readonly float quantization;

        /// <summary>
        /// The channel's factor for subtracting the Y coefficient.
        /// </summary>
        private readonly float correlation;

        /// <summary>
        /// Initializes the invariant values shared by every coefficient in one channel.
        /// </summary>
        /// <param name="quantization">The positive candidate quantization norm.</param>
        /// <param name="correlation">The channel's color-correlation factor.</param>
        public EntropyOperator(float quantization, float correlation)
        {
            this.quantization = quantization;
            this.correlation = correlation;
        }

        /// <summary>
        /// Returns the dequantized rounding error and the two statistics for the same coefficient.
        /// </summary>
        /// <param name="input">The channel coefficient.</param>
        /// <param name="luminance">The corresponding Y coefficient.</param>
        /// <param name="inverseMatrix">The forward quantization weight.</param>
        /// <param name="matrix">The inverse quantization weight.</param>
        /// <param name="cost">The square root of the absolute rounded coefficient.</param>
        /// <param name="nonzero">One for a nonzero rounded coefficient; otherwise zero.</param>
        /// <returns>The restored quantization error.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float input, float luminance, float inverseMatrix, float matrix, out float cost, out float nonzero)
        {
            // Preserve the native multiply/subtract grouping. Fusing the subtraction could move a coefficient across its rounding midpoint.
            float value = (input - (luminance * this.correlation)) * (inverseMatrix * this.quantization);
            float rounded = MathF.Round(value, MidpointRounding.ToEven);
            cost = MathF.Sqrt(MathF.Abs(rounded));
            nonzero = rounded == 0 ? 0F : 1F;

            return matrix * (value - rounded);
        }

        /// <inheritdoc cref="Invoke(float, float, float, float, out float, out float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(
            Vector128<float> input,
            Vector128<float> luminance,
            Vector128<float> inverseMatrix,
            Vector128<float> matrix,
            out Vector128<float> cost,
            out Vector128<float> nonzero)
        {
            // Preserve the native multiply/subtract grouping. Fusing the subtraction could move a coefficient across its rounding midpoint.
            Vector128<float> value = (input - (luminance * Vector128.Create(this.correlation))) * (inverseMatrix * Vector128.Create(this.quantization));
            Vector128<float> rounded = Vector128.Round(value);
            cost = Vector128.Sqrt(Vector128.Abs(rounded));
            nonzero = Vector128.ConditionalSelect(Vector128.Equals(rounded, Vector128<float>.Zero), Vector128<float>.Zero, Vector128<float>.One);

            return matrix * (value - rounded);
        }

        /// <inheritdoc cref="Invoke(float, float, float, float, out float, out float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(
            Vector256<float> input,
            Vector256<float> luminance,
            Vector256<float> inverseMatrix,
            Vector256<float> matrix,
            out Vector256<float> cost,
            out Vector256<float> nonzero)
        {
            // Preserve the native multiply/subtract grouping. Fusing the subtraction could move a coefficient across its rounding midpoint.
            Vector256<float> value = (input - (luminance * Vector256.Create(this.correlation))) * (inverseMatrix * Vector256.Create(this.quantization));
            Vector256<float> rounded = Vector256.Round(value);
            cost = Vector256.Sqrt(Vector256.Abs(rounded));
            nonzero = Vector256.ConditionalSelect(Vector256.Equals(rounded, Vector256<float>.Zero), Vector256<float>.Zero, Vector256<float>.One);

            return matrix * (value - rounded);
        }

        /// <inheritdoc cref="Invoke(float, float, float, float, out float, out float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(
            Vector512<float> input,
            Vector512<float> luminance,
            Vector512<float> inverseMatrix,
            Vector512<float> matrix,
            out Vector512<float> cost,
            out Vector512<float> nonzero)
        {
            // Preserve the native multiply/subtract grouping. Fusing the subtraction could move a coefficient across its rounding midpoint.
            Vector512<float> value = (input - (luminance * Vector512.Create(this.correlation))) * (inverseMatrix * Vector512.Create(this.quantization));
            Vector512<float> rounded = Vector512.Round(value);
            cost = Vector512.Sqrt(Vector512.Abs(rounded));
            nonzero = Vector512.ConditionalSelect(Vector512.Equals(rounded, Vector512<float>.Zero), Vector512<float>.Zero, Vector512<float>.One);

            return matrix * (value - rounded);
        }
    }

    /// <summary>
    /// Computes the eighth power of a pixel's error after adding the channel offset to its perceptual mask.
    /// </summary>
    private readonly struct LossOperator : TensorPrimitives_.IBinaryOperator
    {
        /// <summary>
        /// The native channel offset, in mask units: twelve for X, zero for Y and four for B.
        /// </summary>
        private readonly float offset;

        /// <summary>
        /// Initializes the mask offset shared by one channel's pixels.
        /// </summary>
        /// <param name="offset">The additive channel offset in mask units.</param>
        public LossOperator(float offset) => this.offset = offset;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float first, float second)
        {
            float value = first * (second + this.offset);
            value *= value;
            value *= value;
            return value * value;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> first, Vector128<float> second)
        {
            Vector128<float> value = first * (second + Vector128.Create(this.offset));
            value *= value;
            value *= value;
            return value * value;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> first, Vector256<float> second)
        {
            Vector256<float> value = first * (second + Vector256.Create(this.offset));
            value *= value;
            value *= value;
            return value * value;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> first, Vector512<float> second)
        {
            Vector512<float> value = first * (second + Vector512.Create(this.offset));
            value *= value;
            value *= value;
            return value * value;
        }
    }
}
