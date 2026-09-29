// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;

internal static partial class Av1ForwardQuantizer
{
    /// <summary>
    /// The largest rounded magnitude the eight-bit quantizers keep. Reference: the INT16_MAX clamp of
    /// quantize_fp_helper_c() and aom_quantize_b_helper_c().
    /// </summary>
    private const int ByteMagnitudeLimit = short.MaxValue;

    /// <summary>
    /// Selects the matrix form of one quantizer.
    /// </summary>
    private interface IMatrixQuantizationOperator
    {
        /// <summary>
        /// Quantizes one vector of coefficients with their matrix weights.
        /// </summary>
        /// <param name="coefficients">The transform coefficients.</param>
        /// <param name="weights">The forward matrix weight of each coefficient.</param>
        /// <param name="inverseWeights">The inverse matrix weight of each coefficient.</param>
        /// <param name="constants">The quantizer constants of the coefficient class.</param>
        /// <param name="dequantizedCoefficients">The reconstruction coefficients.</param>
        /// <returns>The quantized coefficients.</returns>
        public static abstract Vector128<int> Quantize(
            Vector128<int> coefficients,
            Vector128<int> weights,
            Vector128<int> inverseWeights,
            in MatrixQuantizerConstants constants,
            out Vector128<int> dequantizedCoefficients);

        /// <inheritdoc cref="Quantize(Vector128{int}, Vector128{int}, Vector128{int}, in MatrixQuantizerConstants, out Vector128{int})"/>
        public static abstract Vector256<int> Quantize(
            Vector256<int> coefficients,
            Vector256<int> weights,
            Vector256<int> inverseWeights,
            in MatrixQuantizerConstants constants,
            out Vector256<int> dequantizedCoefficients);

        /// <inheritdoc cref="Quantize(Vector128{int}, Vector128{int}, Vector128{int}, in MatrixQuantizerConstants, out Vector128{int})"/>
        public static abstract Vector512<int> Quantize(
            Vector512<int> coefficients,
            Vector512<int> weights,
            Vector512<int> inverseWeights,
            in MatrixQuantizerConstants constants,
            out Vector512<int> dequantizedCoefficients);

        /// <summary>
        /// Quantizes one coefficient with its matrix weights.
        /// </summary>
        /// <param name="coefficient">The transform coefficient.</param>
        /// <param name="weight">The forward matrix weight.</param>
        /// <param name="inverseWeight">The inverse matrix weight.</param>
        /// <param name="constants">The quantizer constants of the coefficient class.</param>
        /// <param name="dequantizedCoefficient">The reconstruction coefficient.</param>
        /// <returns>The quantized coefficient.</returns>
        public static abstract int Quantize(
            int coefficient,
            int weight,
            int inverseWeight,
            in MatrixQuantizerConstants constants,
            out int dequantizedCoefficient);
    }

    /// <summary>
    /// Quantizes one lossy transform block with quantization matrices.
    /// </summary>
    /// <remarks>
    /// Reference: quantize_fp_helper_c() and highbd_quantize_fp_helper_c() for the fast form, and
    /// aom_quantize_b_helper_c() and aom_highbd_quantize_b_helper_c() for the regular form, each with a matrix. The
    /// pre-scan of the regular form keeps exactly the coefficients that pass the weighted zero bin, so one raster
    /// traversal gives the same output.
    /// </remarks>
    /// <param name="coefficients">The raster-order transform coefficients.</param>
    /// <param name="quantizedCoefficients">The raster-order coding coefficients.</param>
    /// <param name="dequantizedCoefficients">The raster-order reconstruction coefficients.</param>
    /// <param name="transformSize">The transform dimensions.</param>
    /// <param name="transformType">The transform type selecting the scan order.</param>
    /// <param name="qIndex">The block quantizer index.</param>
    /// <param name="dcDeltaQ">The DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The AC quantizer adjustment.</param>
    /// <param name="bitDepth">The coded sample precision.</param>
    /// <param name="sharpness">The encoder sharpness from zero through seven.</param>
    /// <param name="weights">The forward quantization matrix.</param>
    /// <param name="inverseWeights">The inverse quantization matrix.</param>
    /// <param name="regular">Whether to use the regular form instead of the fast form.</param>
    /// <returns>The one-based end position in coefficient scan order.</returns>
    public static ushort QuantizeWithMatrix(
        ReadOnlySpan<int> coefficients,
        Span<int> quantizedCoefficients,
        Span<int> dequantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1BitDepth bitDepth,
        int sharpness,
        ReadOnlySpan<byte> weights,
        ReadOnlySpan<byte> inverseWeights,
        bool regular)
    {
        int logScale = transformSize.GetScale();
        int magnitudeLimit = bitDepth == Av1BitDepth.EightBit ? ByteMagnitudeLimit : int.MaxValue;
        int dcDequantizer = Av1QuantizationLookup.GetDcQuant(qIndex, dcDeltaQ, bitDepth);
        int acDequantizer = Av1QuantizationLookup.GetAcQuant(qIndex, acDeltaQ, bitDepth);
        MatrixQuantizerConstants dc;
        MatrixQuantizerConstants ac;
        int sharpnessAdjustment = 16 * (7 - sharpness) / 7;
        if (regular)
        {
            // Reference: the zbin, round, quant and quant_shift tables of av1_build_quantizer().
            int zeroBinFactor = Av1InverseTransformMath.GetQzbinFactor(qIndex, bitDepth);
            int roundingFactor = qIndex == 0 ? 64 : sharpness == 0 ? 48 : 64 - sharpnessAdjustment;
            int dcQuantizer = Av1QuantizationLookup.GetDcRegularQuantizer(qIndex, dcDeltaQ, bitDepth, out int dcShift);
            int acQuantizer = Av1QuantizationLookup.GetAcRegularQuantizer(qIndex, acDeltaQ, bitDepth, out int acShift);
            dc = new MatrixQuantizerConstants(
                RoundPowerOfTwo(RoundPowerOfTwo(zeroBinFactor * dcDequantizer, 7), logScale) << Av1Constants.QuantizationMatrixElementBitCount,
                RoundPowerOfTwo((roundingFactor * dcDequantizer) >> 7, logScale),
                dcQuantizer,
                dcShift,
                dcDequantizer,
                logScale,
                magnitudeLimit);

            ac = new MatrixQuantizerConstants(
                RoundPowerOfTwo(RoundPowerOfTwo(zeroBinFactor * acDequantizer, 7), logScale) << Av1Constants.QuantizationMatrixElementBitCount,
                RoundPowerOfTwo((roundingFactor * acDequantizer) >> 7, logScale),
                acQuantizer,
                acShift,
                acDequantizer,
                logScale,
                magnitudeLimit);

            return QuantizeWithMatrix<RegularMatrixQuantizationOperator>(
                coefficients, quantizedCoefficients, dequantizedCoefficients, transformSize, transformType, weights, inverseWeights, in dc, in ac);
        }

        // Reference: the quant_fp and round_fp tables of av1_build_quantizer().
        int fastRoundingFactor = sharpness != 0 && qIndex != 0 ? 64 - sharpnessAdjustment : 64;
        int thresholdShift = Av1Constants.QuantizationMatrixElementBitCount - (1 + logScale);
        dc = new MatrixQuantizerConstants(
            dcDequantizer << thresholdShift,
            RoundPowerOfTwo((fastRoundingFactor * dcDequantizer) >> 7, logScale),
            Av1QuantizationLookup.GetDcQuantizer(qIndex, dcDeltaQ, bitDepth),
            0,
            dcDequantizer,
            logScale,
            magnitudeLimit);

        ac = new MatrixQuantizerConstants(
            acDequantizer << thresholdShift,
            RoundPowerOfTwo((fastRoundingFactor * acDequantizer) >> 7, logScale),
            Av1QuantizationLookup.GetAcQuantizer(qIndex, acDeltaQ, bitDepth),
            0,
            acDequantizer,
            logScale,
            magnitudeLimit);

        return QuantizeWithMatrix<FastMatrixQuantizationOperator>(
            coefficients, quantizedCoefficients, dequantizedCoefficients, transformSize, transformType, weights, inverseWeights, in dc, in ac);
    }

    /// <summary>
    /// Traverses one matrix quantization with the DC coefficient first and then contiguous AC vectors.
    /// </summary>
    private static ushort QuantizeWithMatrix<TOperator>(
        ReadOnlySpan<int> coefficients,
        Span<int> quantizedCoefficients,
        Span<int> dequantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        ReadOnlySpan<byte> weights,
        ReadOnlySpan<byte> inverseWeights,
        in MatrixQuantizerConstants dc,
        in MatrixQuantizerConstants ac)
        where TOperator : struct, IMatrixQuantizationOperator
    {
        int count = transformSize.GetAdjusted().GetSize2d();
        ref int sourceBase = ref MemoryMarshal.GetReference(coefficients);
        ref int quantizedBase = ref MemoryMarshal.GetReference(quantizedCoefficients);
        ref int dequantizedBase = ref MemoryMarshal.GetReference(dequantizedCoefficients);
        ref byte weightBase = ref MemoryMarshal.GetReference(weights);
        ref byte inverseWeightBase = ref MemoryMarshal.GetReference(inverseWeights);

        // The DC coefficient has its own constants, so the AC traversal needs no lane masks.
        quantizedBase = TOperator.Quantize(sourceBase, weightBase, inverseWeightBase, in dc, out dequantizedBase);

        int index = 1;
        if (Vector512.IsHardwareAccelerated)
        {
            for (; index <= count - Vector512<int>.Count; index += Vector512<int>.Count)
            {
                Vector512<int> quantized = TOperator.Quantize(
                    Vector512.LoadUnsafe(ref sourceBase, (nuint)index),
                    LoadWeights(ref Unsafe.Add(ref weightBase, index), Vector512<int>.Zero),
                    LoadWeights(ref Unsafe.Add(ref inverseWeightBase, index), Vector512<int>.Zero),
                    in ac,
                    out Vector512<int> dequantized);

                quantized.StoreUnsafe(ref quantizedBase, (nuint)index);
                dequantized.StoreUnsafe(ref dequantizedBase, (nuint)index);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; index <= count - Vector256<int>.Count; index += Vector256<int>.Count)
            {
                Vector256<int> quantized = TOperator.Quantize(
                    Vector256.LoadUnsafe(ref sourceBase, (nuint)index),
                    LoadWeights(ref Unsafe.Add(ref weightBase, index), Vector256<int>.Zero),
                    LoadWeights(ref Unsafe.Add(ref inverseWeightBase, index), Vector256<int>.Zero),
                    in ac,
                    out Vector256<int> dequantized);

                quantized.StoreUnsafe(ref quantizedBase, (nuint)index);
                dequantized.StoreUnsafe(ref dequantizedBase, (nuint)index);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; index <= count - Vector128<int>.Count; index += Vector128<int>.Count)
            {
                Vector128<int> quantized = TOperator.Quantize(
                    Vector128.LoadUnsafe(ref sourceBase, (nuint)index),
                    LoadWeights(ref Unsafe.Add(ref weightBase, index), Vector128<int>.Zero),
                    LoadWeights(ref Unsafe.Add(ref inverseWeightBase, index), Vector128<int>.Zero),
                    in ac,
                    out Vector128<int> dequantized);

                quantized.StoreUnsafe(ref quantizedBase, (nuint)index);
                dequantized.StoreUnsafe(ref dequantizedBase, (nuint)index);
            }
        }

        for (; index < count; index++)
        {
            Unsafe.Add(ref quantizedBase, index) = TOperator.Quantize(
                Unsafe.Add(ref sourceBase, index),
                Unsafe.Add(ref weightBase, index),
                Unsafe.Add(ref inverseWeightBase, index),
                in ac,
                out Unsafe.Add(ref dequantizedBase, index));
        }

        ReadOnlySpan<short> inverseScan = Av1ScanOrderConstants.GetScanOrder(transformSize, transformType).InverseScan;
        return GetEndOfBlock(quantizedCoefficients[..count], inverseScan);
    }

    /// <summary>
    /// Loads four matrix weights and widens them to thirty-two-bit lanes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<int> LoadWeights(ref byte source, Vector128<int> lanes)
    {
        Vector128<byte> bytes = Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<uint>(ref source)).AsByte();
        return Vector128.WidenLower(Vector128.WidenLower(bytes)).AsInt32();
    }

    /// <summary>
    /// Loads eight matrix weights and widens them to thirty-two-bit lanes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector256<int> LoadWeights(ref byte source, Vector256<int> lanes)
    {
        Vector128<ushort> words = Vector128.WidenLower(Vector64.LoadUnsafe(ref source).ToVector128());
        return Vector256.WidenLower(words.ToVector256Unsafe()).AsInt32();
    }

    /// <summary>
    /// Loads sixteen matrix weights and widens them to thirty-two-bit lanes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector512<int> LoadWeights(ref byte source, Vector512<int> lanes)
    {
        Vector256<ushort> words = Vector256.WidenLower(Vector128.LoadUnsafe(ref source).ToVector256Unsafe());
        return Vector512.WidenLower(words.ToVector512Unsafe()).AsInt32();
    }

    /// <summary>
    /// The quantizer constants of one coefficient class, DC or AC, for the matrix quantizers.
    /// </summary>
    private readonly struct MatrixQuantizerConstants
    {
        public MatrixQuantizerConstants(
            int threshold,
            int rounding,
            int quantizer,
            int quantizerShift,
            int dequantizer,
            int logScale,
            int magnitudeLimit)
        {
            this.Threshold = threshold;
            this.Rounding = rounding;
            this.Quantizer = quantizer;
            this.QuantizerShift = quantizerShift;
            this.Dequantizer = dequantizer;
            this.LogScale = logScale;
            this.MagnitudeLimit = magnitudeLimit;
        }

        /// <summary>
        /// Gets the bound that the weighted magnitude must reach, in matrix precision.
        /// </summary>
        public int Threshold { get; }

        /// <summary>
        /// Gets the rounding added to the magnitude at transform scale.
        /// </summary>
        public int Rounding { get; }

        /// <summary>
        /// Gets the reciprocal of the step; the fast form uses quant_fp and the regular form quant.
        /// </summary>
        public int Quantizer { get; }

        /// <summary>
        /// Gets the second multiplier of the regular form. Reference: quant_shift.
        /// </summary>
        public int QuantizerShift { get; }

        /// <summary>
        /// Gets the reconstruction step before the inverse weight.
        /// </summary>
        public int Dequantizer { get; }

        /// <summary>
        /// Gets the transform scale.
        /// </summary>
        public int LogScale { get; }

        /// <summary>
        /// Gets the largest rounded magnitude kept; eight-bit coding clamps to sixteen bits.
        /// </summary>
        public int MagnitudeLimit { get; }
    }

    /// <summary>
    /// Quantizes with the fast form and a matrix. Reference: quantize_fp_helper_c() and highbd_quantize_fp_helper_c().
    /// </summary>
    private readonly struct FastMatrixQuantizationOperator : IMatrixQuantizationOperator
    {
        /// <inheritdoc/>
        public static Vector128<int> Quantize(
            Vector128<int> coefficients,
            Vector128<int> weights,
            Vector128<int> inverseWeights,
            in MatrixQuantizerConstants constants,
            out Vector128<int> dequantizedCoefficients)
        {
            const int bits = Av1Constants.QuantizationMatrixElementBitCount;
            Vector128<int> sign = coefficients >> 31;
            Vector128<int> magnitude = Vector128.Abs(coefficients);
            Vector128<int> mask = ~Vector128.GreaterThan(Vector128.Create(constants.Threshold), magnitude * weights);
            Vector128<int> rounded = Vector128.Min(magnitude + Vector128.Create(constants.Rounding), Vector128.Create(constants.MagnitudeLimit));
            Vector128<int> weighted = rounded * weights;
            Vector128<long> quantizer = Vector128.Create((long)constants.Quantizer);
            int shift = 16 - constants.LogScale + bits;
            Vector128<long> lower = (Vector128.WidenLower(weighted) * quantizer) >> shift;
            Vector128<long> upper = (Vector128.WidenUpper(weighted) * quantizer) >> shift;
            Vector128<int> quantizedMagnitude = Vector128.Narrow(lower, upper) & mask;
            Vector128<int> dequantizer = ((inverseWeights * Vector128.Create(constants.Dequantizer)) + Vector128.Create(1 << (bits - 1))) >> bits;
            Vector128<int> dequantizedMagnitude = (quantizedMagnitude * dequantizer) >> constants.LogScale;
            dequantizedCoefficients = (dequantizedMagnitude ^ sign) - sign;
            return (quantizedMagnitude ^ sign) - sign;
        }

        /// <inheritdoc/>
        public static Vector256<int> Quantize(
            Vector256<int> coefficients,
            Vector256<int> weights,
            Vector256<int> inverseWeights,
            in MatrixQuantizerConstants constants,
            out Vector256<int> dequantizedCoefficients)
        {
            const int bits = Av1Constants.QuantizationMatrixElementBitCount;
            Vector256<int> sign = coefficients >> 31;
            Vector256<int> magnitude = Vector256.Abs(coefficients);
            Vector256<int> mask = ~Vector256.GreaterThan(Vector256.Create(constants.Threshold), magnitude * weights);
            Vector256<int> rounded = Vector256.Min(magnitude + Vector256.Create(constants.Rounding), Vector256.Create(constants.MagnitudeLimit));
            Vector256<int> weighted = rounded * weights;
            Vector256<long> quantizer = Vector256.Create((long)constants.Quantizer);
            int shift = 16 - constants.LogScale + bits;
            Vector256<long> lower = (Vector256.WidenLower(weighted) * quantizer) >> shift;
            Vector256<long> upper = (Vector256.WidenUpper(weighted) * quantizer) >> shift;
            Vector256<int> quantizedMagnitude = Vector256.Narrow(lower, upper) & mask;
            Vector256<int> dequantizer = ((inverseWeights * Vector256.Create(constants.Dequantizer)) + Vector256.Create(1 << (bits - 1))) >> bits;
            Vector256<int> dequantizedMagnitude = (quantizedMagnitude * dequantizer) >> constants.LogScale;
            dequantizedCoefficients = (dequantizedMagnitude ^ sign) - sign;
            return (quantizedMagnitude ^ sign) - sign;
        }

        /// <inheritdoc/>
        public static Vector512<int> Quantize(
            Vector512<int> coefficients,
            Vector512<int> weights,
            Vector512<int> inverseWeights,
            in MatrixQuantizerConstants constants,
            out Vector512<int> dequantizedCoefficients)
        {
            const int bits = Av1Constants.QuantizationMatrixElementBitCount;
            Vector512<int> sign = coefficients >> 31;
            Vector512<int> magnitude = Vector512.Abs(coefficients);
            Vector512<int> mask = ~Vector512.GreaterThan(Vector512.Create(constants.Threshold), magnitude * weights);
            Vector512<int> rounded = Vector512.Min(magnitude + Vector512.Create(constants.Rounding), Vector512.Create(constants.MagnitudeLimit));
            Vector512<int> weighted = rounded * weights;
            Vector512<long> quantizer = Vector512.Create((long)constants.Quantizer);
            int shift = 16 - constants.LogScale + bits;
            Vector512<long> lower = (Vector512.WidenLower(weighted) * quantizer) >> shift;
            Vector512<long> upper = (Vector512.WidenUpper(weighted) * quantizer) >> shift;
            Vector512<int> quantizedMagnitude = Vector512.Narrow(lower, upper) & mask;
            Vector512<int> dequantizer = ((inverseWeights * Vector512.Create(constants.Dequantizer)) + Vector512.Create(1 << (bits - 1))) >> bits;
            Vector512<int> dequantizedMagnitude = (quantizedMagnitude * dequantizer) >> constants.LogScale;
            dequantizedCoefficients = (dequantizedMagnitude ^ sign) - sign;
            return (quantizedMagnitude ^ sign) - sign;
        }

        /// <inheritdoc/>
        public static int Quantize(
            int coefficient,
            int weight,
            int inverseWeight,
            in MatrixQuantizerConstants constants,
            out int dequantizedCoefficient)
        {
            const int bits = Av1Constants.QuantizationMatrixElementBitCount;
            int sign = coefficient >> 31;
            int magnitude = (coefficient ^ sign) - sign;
            int quantizedMagnitude = 0;
            if ((long)magnitude * weight >= constants.Threshold)
            {
                long rounded = Math.Min((long)magnitude + constants.Rounding, constants.MagnitudeLimit);
                quantizedMagnitude = (int)((rounded * weight * constants.Quantizer) >> (16 - constants.LogScale + bits));
            }

            int dequantizer = ((constants.Dequantizer * inverseWeight) + (1 << (bits - 1))) >> bits;
            int dequantizedMagnitude = (quantizedMagnitude * dequantizer) >> constants.LogScale;
            dequantizedCoefficient = (dequantizedMagnitude ^ sign) - sign;
            return (quantizedMagnitude ^ sign) - sign;
        }
    }

    /// <summary>
    /// Quantizes with the regular form and a matrix. Reference: aom_quantize_b_helper_c() and
    /// aom_highbd_quantize_b_helper_c().
    /// </summary>
    private readonly struct RegularMatrixQuantizationOperator : IMatrixQuantizationOperator
    {
        /// <inheritdoc/>
        public static Vector128<int> Quantize(
            Vector128<int> coefficients,
            Vector128<int> weights,
            Vector128<int> inverseWeights,
            in MatrixQuantizerConstants constants,
            out Vector128<int> dequantizedCoefficients)
        {
            const int bits = Av1Constants.QuantizationMatrixElementBitCount;
            Vector128<int> sign = coefficients >> 31;
            Vector128<int> magnitude = Vector128.Abs(coefficients);
            Vector128<int> mask = ~Vector128.GreaterThan(Vector128.Create(constants.Threshold), magnitude * weights);
            Vector128<int> rounded = Vector128.Min(magnitude + Vector128.Create(constants.Rounding), Vector128.Create(constants.MagnitudeLimit));

            // The weighted magnitude and both products keep sixty-four bits, as the reference does.
            Vector128<long> lower = Vector128.WidenLower(rounded) * Vector128.WidenLower(weights);
            Vector128<long> upper = Vector128.WidenUpper(rounded) * Vector128.WidenUpper(weights);
            Vector128<long> quantizer = Vector128.Create((long)constants.Quantizer);
            Vector128<long> quantizerShift = Vector128.Create((long)constants.QuantizerShift);
            int shift = 16 - constants.LogScale + bits;
            lower = ((((lower * quantizer) >> 16) + lower) * quantizerShift) >> shift;
            upper = ((((upper * quantizer) >> 16) + upper) * quantizerShift) >> shift;
            Vector128<int> quantizedMagnitude = Vector128.Narrow(lower, upper) & mask;
            Vector128<int> dequantizer = ((inverseWeights * Vector128.Create(constants.Dequantizer)) + Vector128.Create(1 << (bits - 1))) >> bits;
            Vector128<int> dequantizedMagnitude = (quantizedMagnitude * dequantizer) >> constants.LogScale;
            dequantizedCoefficients = (dequantizedMagnitude ^ sign) - sign;
            return (quantizedMagnitude ^ sign) - sign;
        }

        /// <inheritdoc/>
        public static Vector256<int> Quantize(
            Vector256<int> coefficients,
            Vector256<int> weights,
            Vector256<int> inverseWeights,
            in MatrixQuantizerConstants constants,
            out Vector256<int> dequantizedCoefficients)
        {
            const int bits = Av1Constants.QuantizationMatrixElementBitCount;
            Vector256<int> sign = coefficients >> 31;
            Vector256<int> magnitude = Vector256.Abs(coefficients);
            Vector256<int> mask = ~Vector256.GreaterThan(Vector256.Create(constants.Threshold), magnitude * weights);
            Vector256<int> rounded = Vector256.Min(magnitude + Vector256.Create(constants.Rounding), Vector256.Create(constants.MagnitudeLimit));
            Vector256<long> lower = Vector256.WidenLower(rounded) * Vector256.WidenLower(weights);
            Vector256<long> upper = Vector256.WidenUpper(rounded) * Vector256.WidenUpper(weights);
            Vector256<long> quantizer = Vector256.Create((long)constants.Quantizer);
            Vector256<long> quantizerShift = Vector256.Create((long)constants.QuantizerShift);
            int shift = 16 - constants.LogScale + bits;
            lower = ((((lower * quantizer) >> 16) + lower) * quantizerShift) >> shift;
            upper = ((((upper * quantizer) >> 16) + upper) * quantizerShift) >> shift;
            Vector256<int> quantizedMagnitude = Vector256.Narrow(lower, upper) & mask;
            Vector256<int> dequantizer = ((inverseWeights * Vector256.Create(constants.Dequantizer)) + Vector256.Create(1 << (bits - 1))) >> bits;
            Vector256<int> dequantizedMagnitude = (quantizedMagnitude * dequantizer) >> constants.LogScale;
            dequantizedCoefficients = (dequantizedMagnitude ^ sign) - sign;
            return (quantizedMagnitude ^ sign) - sign;
        }

        /// <inheritdoc/>
        public static Vector512<int> Quantize(
            Vector512<int> coefficients,
            Vector512<int> weights,
            Vector512<int> inverseWeights,
            in MatrixQuantizerConstants constants,
            out Vector512<int> dequantizedCoefficients)
        {
            const int bits = Av1Constants.QuantizationMatrixElementBitCount;
            Vector512<int> sign = coefficients >> 31;
            Vector512<int> magnitude = Vector512.Abs(coefficients);
            Vector512<int> mask = ~Vector512.GreaterThan(Vector512.Create(constants.Threshold), magnitude * weights);
            Vector512<int> rounded = Vector512.Min(magnitude + Vector512.Create(constants.Rounding), Vector512.Create(constants.MagnitudeLimit));
            Vector512<long> lower = Vector512.WidenLower(rounded) * Vector512.WidenLower(weights);
            Vector512<long> upper = Vector512.WidenUpper(rounded) * Vector512.WidenUpper(weights);
            Vector512<long> quantizer = Vector512.Create((long)constants.Quantizer);
            Vector512<long> quantizerShift = Vector512.Create((long)constants.QuantizerShift);
            int shift = 16 - constants.LogScale + bits;
            lower = ((((lower * quantizer) >> 16) + lower) * quantizerShift) >> shift;
            upper = ((((upper * quantizer) >> 16) + upper) * quantizerShift) >> shift;
            Vector512<int> quantizedMagnitude = Vector512.Narrow(lower, upper) & mask;
            Vector512<int> dequantizer = ((inverseWeights * Vector512.Create(constants.Dequantizer)) + Vector512.Create(1 << (bits - 1))) >> bits;
            Vector512<int> dequantizedMagnitude = (quantizedMagnitude * dequantizer) >> constants.LogScale;
            dequantizedCoefficients = (dequantizedMagnitude ^ sign) - sign;
            return (quantizedMagnitude ^ sign) - sign;
        }

        /// <inheritdoc/>
        public static int Quantize(
            int coefficient,
            int weight,
            int inverseWeight,
            in MatrixQuantizerConstants constants,
            out int dequantizedCoefficient)
        {
            const int bits = Av1Constants.QuantizationMatrixElementBitCount;
            int sign = coefficient >> 31;
            int magnitude = (coefficient ^ sign) - sign;
            int quantizedMagnitude = 0;
            if ((long)magnitude * weight >= constants.Threshold)
            {
                long weighted = Math.Min((long)magnitude + constants.Rounding, constants.MagnitudeLimit) * weight;
                long corrected = ((weighted * constants.Quantizer) >> 16) + weighted;
                quantizedMagnitude = (int)((corrected * constants.QuantizerShift) >> (16 - constants.LogScale + bits));
            }

            int dequantizer = ((constants.Dequantizer * inverseWeight) + (1 << (bits - 1))) >> bits;
            int dequantizedMagnitude = (quantizedMagnitude * dequantizer) >> constants.LogScale;
            dequantizedCoefficient = (dequantizedMagnitude ^ sign) - sign;
            return (quantizedMagnitude ^ sign) - sign;
        }
    }
}
