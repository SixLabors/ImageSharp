// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

internal static partial class JxlQuantization
{
    /// <summary>
    /// Prepares the fast DCT8 quantizers and frame fields for a positive perceptual distance.
    /// </summary>
    /// <param name="distance">The positive distance selected by the encoder.</param>
    /// <returns>The serialized quantizers and the multipliers shared by every frame block.</returns>
    public static EncoderSettings CreateEncoderSettings(float distance)
    {
        // VarDCT uses a minimum perceptual distance of 0.05 before it prepares quantizers and frame fields.
        distance = MathF.Max(distance, .05F);

        // DC precision follows a power curve above distance 0.3. The half-distance floor limits its
        // reduction at large distances, while the cap of 50 bounds the precision at small distances.
        float dcDistance = MathF.Max(
            .5F * distance, MathF.Min(distance, .3F * MathF.Pow((1F / .3F) * distance, .83F)));

        float quantDc = MathF.Min(1.095924047623553F / dcDistance, 50F);

        // The constant AC field is measured in inverse XYB coefficient units. Map its target value
        // to integer five, then encode the scale as a fixed-point value with sixteen fraction bits.
        // Preserve the double division used to prepare the single-precision AC field.
        float quantAc = (float)(.79D / distance);
        int globalScale = (int)Math.Clamp(65536F * quantAc / 5F, 1F, 32768F);

        // Keep the integer DC quantizer at least ten where the scale range permits it.
        // The 4096 numerator and factor 1.6 set that limit before the quantizers are rounded.
        int dcScaleLimit = (int)(quantDc * 4096F * 1.6D);
        globalScale = Math.Max(1, Math.Min(globalScale, dcScaleLimit));
        float inverseScale = 65536F / globalScale;
        int dcQuantizer = (int)MathF.Min((quantDc * inverseScale) + .5F, 65536F);
        int acQuantizer = (int)Math.Clamp((quantAc * inverseScale) + .5F, 1F, 256F);

        // X precision increases at these distance boundaries. The fast path does not scan pixels
        // for additional chroma adjustments, so B keeps the neutral matrix scale code two.
        int xScale = 3;
        xScale += distance > 2.5F ? 1 : 0;
        xScale += distance > 5.5F ? 1 : 0;
        xScale += distance > 9.5F ? 1 : 0;

        // Each distance threshold enables one additional edge-preserving filter iteration.
        // The fast path does not apply inverse Gaborish before the forward DCT.
        int epfIterations = distance >= .7F ? 1 : 0;
        epfIterations += distance >= 1.5F ? 1 : 0;
        epfIterations += distance >= 4F ? 1 : 0;
        return new EncoderSettings(globalScale, dcQuantizer, acQuantizer, xScale, 2, epfIterations);
    }

    /// <summary>
    /// Quantizes one DCT8 block in Y, X, B order and replaces its Y plane with reconstructed AC values.
    /// </summary>
    /// <param name="coefficients">Three contiguous 64-value X, Y and B planes. DC values have already been retained.</param>
    /// <param name="weights">Three disjoint 64-value forward quantization weight planes.</param>
    /// <param name="dequantization">The corresponding three disjoint reciprocal weight planes.</param>
    /// <param name="x">The caller-owned 64-value X coefficient output.</param>
    /// <param name="y">The caller-owned 64-value Y coefficient output.</param>
    /// <param name="b">The caller-owned 64-value B coefficient output.</param>
    /// <param name="settings">The frame quantizers prepared before block traversal.</param>
    /// <param name="correlation">The X-from-Y and B-from-Y AC correlation ratios.</param>
    public static void QuantizeDct8Block(
        Span<float> coefficients,
        ReadOnlySpan<float> weights,
        ReadOnlySpan<float> dequantization,
        Span<int> x,
        Span<int> y,
        Span<int> b,
        in EncoderSettings settings,
        Vector2 correlation)
    {
        // DC belongs to the separate modular planes. Its forward matrix weight is implicitly zero,
        // so reconstructed AC Y at DC is zero and cannot change X or B DC through correlation.
        x[0] = y[0] = b[0] = 0;
        coefficients[64] = 0F;
        Vector4 bias = DefaultBias;
        ref float input = ref MemoryMarshal.GetReference(coefficients);
        ref float forward = ref MemoryMarshal.GetReference(weights);
        ref float inverse = ref MemoryMarshal.GetReference(dequantization);
        ref int outputX = ref MemoryMarshal.GetReference(x);
        ref int outputY = ref MemoryMarshal.GetReference(y);
        ref int outputB = ref MemoryMarshal.GetReference(b);
        int i = 1;

        // Each lane holds one natural-order coefficient. Complete unaligned loads stay inside
        // indices one through sixty-three. Descending widths leave only a scalar remainder.
        // Quantize Y first, apply the shared bias and reciprocal weights, then remove that
        // reconstructed Y contribution from X and B before their own quantization.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (64 - i).Vector512Count<float>();
            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                // Natural coefficient positions select the upper-left four-by-four dead zone.
                // Starting at one leaves DC untouched; unaligned batches can cross quadrant boundaries.
                Vector512<int> indices = Vector512<int>.Indices + Vector512.Create(i);
                Vector512<int> low = Vector512.LessThan(indices, Vector512.Create(32))
                    & Vector512.LessThan(indices & Vector512.Create(7), Vector512.Create(4));

                Vector512<float> thresholdY = Vector512.ConditionalSelect(
                    low.AsSingle(), Vector512.Create(.56F), Vector512.Create(.62F));

                Vector512<float> thresholdChroma = Vector512.ConditionalSelect(
                    low.AsSingle(), Vector512.Create(.58F), Vector512.Create(.62F));

                Vector512<int> quantizedY = QuantizeAc(
                    Vector512.LoadUnsafe(ref input, (nuint)(64 + i)),
                    Vector512.LoadUnsafe(ref forward, (nuint)(64 + i)),
                    settings.AcMultipliers.Y,
                    thresholdY);

                quantizedY.StoreUnsafe(ref outputY, (nuint)i);
                Vector512<float> reconstructedY = (AdjustBias(Vector512.ConvertToSingle(quantizedY), bias.Y, bias.W)
                    * Vector512.LoadUnsafe(ref inverse, (nuint)(64 + i))) * Vector512.Create(settings.InverseAcScale);

                reconstructedY.StoreUnsafe(ref input, (nuint)(64 + i));
                Vector512<float> residualX = Vector512.MultiplyAddEstimate(
                    Vector512.Create(-correlation.X), reconstructedY, Vector512.LoadUnsafe(ref input, (nuint)i));

                Vector512<float> residualB = Vector512.MultiplyAddEstimate(
                    Vector512.Create(-correlation.Y), reconstructedY, Vector512.LoadUnsafe(ref input, (nuint)(128 + i)));

                QuantizeAc(residualX, Vector512.LoadUnsafe(ref forward, (nuint)i), settings.AcMultipliers.X, thresholdChroma)
                    .StoreUnsafe(ref outputX, (nuint)i);

                QuantizeAc(residualB, Vector512.LoadUnsafe(ref forward, (nuint)(128 + i)), settings.AcMultipliers.Z, thresholdChroma)
                    .StoreUnsafe(ref outputB, (nuint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (64 - i).Vector256Count<float>();
            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                // Natural coefficient positions select the upper-left four-by-four dead zone.
                // Starting at one leaves DC untouched; unaligned batches can cross quadrant boundaries.
                Vector256<int> indices = Vector256<int>.Indices + Vector256.Create(i);
                Vector256<int> low = Vector256.LessThan(indices, Vector256.Create(32))
                    & Vector256.LessThan(indices & Vector256.Create(7), Vector256.Create(4));

                Vector256<float> thresholdY = Vector256.ConditionalSelect(
                    low.AsSingle(), Vector256.Create(.56F), Vector256.Create(.62F));

                Vector256<float> thresholdChroma = Vector256.ConditionalSelect(
                    low.AsSingle(), Vector256.Create(.58F), Vector256.Create(.62F));

                Vector256<int> quantizedY = QuantizeAc(
                    Vector256.LoadUnsafe(ref input, (nuint)(64 + i)),
                    Vector256.LoadUnsafe(ref forward, (nuint)(64 + i)),
                    settings.AcMultipliers.Y,
                    thresholdY);

                quantizedY.StoreUnsafe(ref outputY, (nuint)i);
                Vector256<float> reconstructedY = (AdjustBias(Vector256.ConvertToSingle(quantizedY), bias.Y, bias.W)
                    * Vector256.LoadUnsafe(ref inverse, (nuint)(64 + i))) * Vector256.Create(settings.InverseAcScale);

                reconstructedY.StoreUnsafe(ref input, (nuint)(64 + i));
                Vector256<float> residualX = Vector256.MultiplyAddEstimate(
                    Vector256.Create(-correlation.X), reconstructedY, Vector256.LoadUnsafe(ref input, (nuint)i));

                Vector256<float> residualB = Vector256.MultiplyAddEstimate(
                    Vector256.Create(-correlation.Y), reconstructedY, Vector256.LoadUnsafe(ref input, (nuint)(128 + i)));

                QuantizeAc(residualX, Vector256.LoadUnsafe(ref forward, (nuint)i), settings.AcMultipliers.X, thresholdChroma)
                    .StoreUnsafe(ref outputX, (nuint)i);

                QuantizeAc(residualB, Vector256.LoadUnsafe(ref forward, (nuint)(128 + i)), settings.AcMultipliers.Z, thresholdChroma)
                    .StoreUnsafe(ref outputB, (nuint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (64 - i).Vector128Count<float>();
            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                // Natural coefficient positions select the upper-left four-by-four dead zone.
                // Starting at one leaves DC untouched; unaligned batches can cross quadrant boundaries.
                Vector128<int> indices = Vector128<int>.Indices + Vector128.Create(i);
                Vector128<int> low = Vector128.LessThan(indices, Vector128.Create(32))
                    & Vector128.LessThan(indices & Vector128.Create(7), Vector128.Create(4));

                Vector128<float> thresholdY = Vector128.ConditionalSelect(
                    low.AsSingle(), Vector128.Create(.56F), Vector128.Create(.62F));

                Vector128<float> thresholdChroma = Vector128.ConditionalSelect(
                    low.AsSingle(), Vector128.Create(.58F), Vector128.Create(.62F));

                Vector128<int> quantizedY = QuantizeAc(
                    Vector128.LoadUnsafe(ref input, (nuint)(64 + i)),
                    Vector128.LoadUnsafe(ref forward, (nuint)(64 + i)),
                    settings.AcMultipliers.Y,
                    thresholdY);

                quantizedY.StoreUnsafe(ref outputY, (nuint)i);
                Vector128<float> reconstructedY = (AdjustBias(Vector128.ConvertToSingle(quantizedY), bias.Y, bias.W)
                    * Vector128.LoadUnsafe(ref inverse, (nuint)(64 + i))) * Vector128.Create(settings.InverseAcScale);

                reconstructedY.StoreUnsafe(ref input, (nuint)(64 + i));
                Vector128<float> residualX = Vector128.MultiplyAddEstimate(
                    Vector128.Create(-correlation.X), reconstructedY, Vector128.LoadUnsafe(ref input, (nuint)i));

                Vector128<float> residualB = Vector128.MultiplyAddEstimate(
                    Vector128.Create(-correlation.Y), reconstructedY, Vector128.LoadUnsafe(ref input, (nuint)(128 + i)));

                QuantizeAc(residualX, Vector128.LoadUnsafe(ref forward, (nuint)i), settings.AcMultipliers.X, thresholdChroma)
                    .StoreUnsafe(ref outputX, (nuint)i);

                QuantizeAc(residualB, Vector128.LoadUnsafe(ref forward, (nuint)(128 + i)), settings.AcMultipliers.Z, thresholdChroma)
                    .StoreUnsafe(ref outputB, (nuint)i);
            }
        }

        for (; i < 64; i++)
        {
            bool low = i < 32 && (i & 7) < 4;
            float thresholdY = low ? .56F : .62F;
            float thresholdChroma = low ? .58F : .62F;
            int quantizedY = QuantizeAc(Unsafe.Add(ref input, 64 + i), Unsafe.Add(ref forward, 64 + i), settings.AcMultipliers.Y, thresholdY);
            Unsafe.Add(ref outputY, i) = quantizedY;
            float reconstructedY = (AdjustBias(quantizedY, bias.Y, bias.W) * Unsafe.Add(ref inverse, 64 + i)) * settings.InverseAcScale;
            Unsafe.Add(ref input, 64 + i) = reconstructedY;
            float residualX = float.MultiplyAddEstimate(-correlation.X, reconstructedY, Unsafe.Add(ref input, i));
            float residualB = float.MultiplyAddEstimate(-correlation.Y, reconstructedY, Unsafe.Add(ref input, 128 + i));
            Unsafe.Add(ref outputX, i) = QuantizeAc(residualX, Unsafe.Add(ref forward, i), settings.AcMultipliers.X, thresholdChroma);
            Unsafe.Add(ref outputB, i) = QuantizeAc(residualB, Unsafe.Add(ref forward, 128 + i), settings.AcMultipliers.Z, thresholdChroma);
        }
    }

    /// <summary>
    /// Applies the component matrix multiplier, dead zone and nearest-even integer rounding.
    /// </summary>
    /// <param name="value">The signed coefficient or color-correlation residual.</param>
    /// <param name="weight">The positive forward matrix weight.</param>
    /// <param name="multiplier">The frame scale times AC quantizer and component multiplier.</param>
    /// <param name="threshold">The minimum retained magnitude in quantized coefficient units.</param>
    /// <returns>The quantized signed coefficient.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int QuantizeAc(float value, float weight, float multiplier, float threshold)
    {
        float scaled = (weight * multiplier) * value;
        return MathF.Abs(scaled) >= threshold ? (int)MathF.Round(scaled) : 0;
    }

    /// <inheritdoc cref="QuantizeAc(float, float, float, float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> QuantizeAc(
        Vector128<float> value, Vector128<float> weight, float multiplier, Vector128<float> threshold)
    {
        Vector128<float> scaled = (weight * Vector128.Create(multiplier)) * value;
        scaled = Vector128.ConditionalSelect(Vector128.GreaterThanOrEqual(Vector128.Abs(scaled), threshold), scaled, Vector128<float>.Zero);
        return Vector128_.ConvertToInt32RoundToEven(scaled);
    }

    /// <inheritdoc cref="QuantizeAc(float, float, float, float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> QuantizeAc(
        Vector256<float> value, Vector256<float> weight, float multiplier, Vector256<float> threshold)
    {
        Vector256<float> scaled = (weight * Vector256.Create(multiplier)) * value;
        scaled = Vector256.ConditionalSelect(Vector256.GreaterThanOrEqual(Vector256.Abs(scaled), threshold), scaled, Vector256<float>.Zero);
        return Vector256_.ConvertToInt32RoundToEven(scaled);
    }

    /// <inheritdoc cref="QuantizeAc(float, float, float, float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<int> QuantizeAc(
        Vector512<float> value, Vector512<float> weight, float multiplier, Vector512<float> threshold)
    {
        Vector512<float> scaled = (weight * Vector512.Create(multiplier)) * value;
        scaled = Vector512.ConditionalSelect(Vector512.GreaterThanOrEqual(Vector512.Abs(scaled), threshold), scaled, Vector512<float>.Zero);
        return Vector512_.ConvertToInt32RoundToEven(scaled);
    }

    /// <summary>
    /// Retains serialized frame quantizers and their derived single-precision multipliers.
    /// </summary>
    public readonly struct EncoderSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EncoderSettings"/> struct.
        /// </summary>
        /// <param name="globalScale">The positive scale with sixteen fraction bits.</param>
        /// <param name="dcQuantizer">The positive integer DC quantizer.</param>
        /// <param name="acQuantizer">The integer AC quantizer from one through 256.</param>
        /// <param name="xScale">The encoded X matrix scale.</param>
        /// <param name="bScale">The encoded B matrix scale.</param>
        /// <param name="epfIterations">The edge-preserving filter iteration count.</param>
        public EncoderSettings(int globalScale, int dcQuantizer, int acQuantizer, int xScale, int bScale, int epfIterations)
        {
            this.GlobalScale = globalScale;
            this.DcQuantizer = dcQuantizer;
            this.AcQuantizer = acQuantizer;
            this.XQuantizationScale = xScale;
            this.BQuantizationScale = bScale;
            this.EpfIterations = epfIterations;

            // Matrix scale code two is neutral. Each larger code multiplies the forward matrix
            // by 1.25; the decoder uses the reciprocal factor on reconstructed coefficients.
            this.Scale = globalScale * (1F / 65536F);
            float acScale = this.Scale * acQuantizer;
            this.AcMultipliers = new Vector3(acScale * MathF.Pow(1.25F, xScale - 2), acScale, acScale * MathF.Pow(1.25F, bScale - 2));
            this.InverseAcScale = (65536F / globalScale) / acQuantizer;
        }

        /// <summary>
        /// Gets the positive encoded global scale with sixteen fraction bits.
        /// </summary>
        public int GlobalScale { get; }

        /// <summary>
        /// Gets the integer DC quantizer.
        /// </summary>
        public int DcQuantizer { get; }

        /// <summary>
        /// Gets the integer AC quantizer from one through 256.
        /// </summary>
        public int AcQuantizer { get; }

        /// <summary>
        /// Gets the encoded X matrix scale.
        /// </summary>
        public int XQuantizationScale { get; }

        /// <summary>
        /// Gets the encoded B matrix scale.
        /// </summary>
        public int BQuantizationScale { get; }

        /// <summary>
        /// Gets the edge-preserving filter iteration count.
        /// </summary>
        public int EpfIterations { get; }

        /// <summary>
        /// Gets the encoded global scale converted to coefficient units.
        /// </summary>
        public float Scale { get; }

        /// <summary>
        /// Gets the reciprocal AC scale used before component matrix scaling.
        /// </summary>
        public float InverseAcScale { get; }

        /// <summary>
        /// Gets the X, Y and B forward AC multipliers.
        /// </summary>
        public Vector3 AcMultipliers { get; }
    }
}
