// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

internal sealed partial class JxlVarDctDecoder
{
    // The bounded base-two interpolation uses the native 3/3 rational approximation on [0,1).
    // Keep one coefficient set so scalar and SIMD evaluation cannot diverge during maintenance.
    private const int MatrixExponentBias = 127;
    private const int MatrixMantissaBits = 23;
    private const float MatrixExpNumerator0 = 98.5506591F;
    private const float MatrixExpNumerator1 = 48.8687798F;
    private const float MatrixExpNumerator2 = 10.1749063F;
    private const float MatrixExpDenominator0 = 98.5506633F;
    private const float MatrixExpDenominator1 = -19.4414990F;
    private const float MatrixExpDenominator2 = -0.0222328856F;
    private const float MatrixExpDenominator3 = 0.210242958F;

    /// <summary>
    /// Converts positive RAW table integers to relative weights without changing the format's reciprocal arithmetic.
    /// </summary>
    /// <param name="source">One decoded RAW row, with strictly positive integers.</param>
    /// <param name="destination">One output weight per source integer.</param>
    /// <param name="denominator">The finite positive denominator established by matrix parsing.</param>
    /// <remarks>
    /// This computes one divided by denominator times sample. The parser later validates and inverts retained weights.
    /// The two divisions have separate single-precision rounding and cannot be cancelled.
    /// </remarks>
    public static void ConvertRawQuantizationRow(ReadOnlySpan<int> source, Span<float> destination, float denominator)
    {
        ref int input = ref MemoryMarshal.GetReference(source);
        ref float output = ref MemoryMarshal.GetReference(destination);
        RawQuantizationOperator operation = new(denominator);
        int i = 0;

        // Every lane supplies one coefficient. Unaligned loads and stores preserve row order; later widths consume only the remainder.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector512Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector512<int>.Count)
            {
                operation.Invoke(Vector512.LoadUnsafe(ref input, (nuint)i)).StoreUnsafe(ref output, (nuint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector256Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector256<int>.Count)
            {
                operation.Invoke(Vector256.LoadUnsafe(ref input, (nuint)i)).StoreUnsafe(ref output, (nuint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector128Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector128<int>.Count)
            {
                operation.Invoke(Vector128.LoadUnsafe(ref input, (nuint)i)).StoreUnsafe(ref output, (nuint)i);
            }
        }

        for (; i < source.Length; i++)
        {
            Unsafe.Add(ref output, i) = operation.Invoke(Unsafe.Add(ref input, i));
        }
    }

    /// <summary>
    /// Establishes the format's weight limits and replaces retained relative weights with decoder factors in place.
    /// </summary>
    /// <param name="weights">The complete generated component planes.</param>
    private static void InvertQuantizationWeights(Span<float> weights)
    {
        ref float first = ref MemoryMarshal.GetReference(weights);
        int i = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (weights.Length - i).Vector512Count<float>();
            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                InverseQuantizationOperator.Invoke(Vector512.LoadUnsafe(ref first, (nuint)i)).StoreUnsafe(ref first, (nuint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (weights.Length - i).Vector256Count<float>();
            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                InverseQuantizationOperator.Invoke(Vector256.LoadUnsafe(ref first, (nuint)i)).StoreUnsafe(ref first, (nuint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (weights.Length - i).Vector128Count<float>();
            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                InverseQuantizationOperator.Invoke(Vector128.LoadUnsafe(ref first, (nuint)i)).StoreUnsafe(ref first, (nuint)i);
            }
        }

        for (; i < weights.Length; i++)
        {
            Unsafe.Add(ref first, i) = InverseQuantizationOperator.Invoke(Unsafe.Add(ref first, i));
        }
    }

    /// <summary>
    /// Computes the base-two exponential needed by a positive quantization-band interpolation.
    /// </summary>
    /// <param name="value">The prepared adjacent-band logarithm multiplied by a fraction in [0,1).</param>
    /// <returns>The positive interpolation multiplier.</returns>
    /// <remarks>
    /// The 3/3 rational coefficients come from libjxl's FastPow2f in base/fast_math-inl.h.
    /// Encoded half-precision deltas bound adjacent factors between 1/65505 and 65505, so the exponent stays within (-16,16).
    /// Range reduction leaves a fraction in [0,1), then the IEEE exponent restores the integral power of two.
    /// This matrix operation uses single precision throughout and does not apply pixel component limits.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float MatrixExp2(float value)
    {
        float integral = MathF.Floor(value);
        float scale = BitConverter.Int32BitsToSingle(((int)integral + MatrixExponentBias) << MatrixMantissaBits);
        float fraction = value - integral;
        float numerator = fraction + MatrixExpNumerator2;
        numerator = float.MultiplyAddEstimate(numerator, fraction, MatrixExpNumerator1);
        numerator = float.MultiplyAddEstimate(numerator, fraction, MatrixExpNumerator0);
        numerator *= scale;
        float denominator = float.MultiplyAddEstimate(fraction, MatrixExpDenominator3, MatrixExpDenominator2);
        denominator = float.MultiplyAddEstimate(denominator, fraction, MatrixExpDenominator1);
        denominator = float.MultiplyAddEstimate(denominator, fraction, MatrixExpDenominator0);
        return numerator / denominator;
    }

    /// <inheritdoc cref="MatrixExp2(float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> MatrixExp2(Vector128<float> value)
    {
        Vector128<float> integral = Vector128.Floor(value);
        Vector128<float> scale = ((Vector128.ConvertToInt32(integral) + Vector128.Create(MatrixExponentBias)) << MatrixMantissaBits).AsSingle();
        Vector128<float> fraction = value - integral;
        Vector128<float> numerator = fraction + Vector128.Create(MatrixExpNumerator2);
        numerator = Vector128.MultiplyAddEstimate(numerator, fraction, Vector128.Create(MatrixExpNumerator1));
        numerator = Vector128.MultiplyAddEstimate(numerator, fraction, Vector128.Create(MatrixExpNumerator0));
        numerator *= scale;
        Vector128<float> denominator = Vector128.MultiplyAddEstimate(
            fraction, Vector128.Create(MatrixExpDenominator3), Vector128.Create(MatrixExpDenominator2));

        denominator = Vector128.MultiplyAddEstimate(denominator, fraction, Vector128.Create(MatrixExpDenominator1));
        denominator = Vector128.MultiplyAddEstimate(denominator, fraction, Vector128.Create(MatrixExpDenominator0));
        return numerator / denominator;
    }

    /// <inheritdoc cref="MatrixExp2(float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> MatrixExp2(Vector256<float> value)
    {
        Vector256<float> integral = Vector256.Floor(value);
        Vector256<float> scale = ((Vector256.ConvertToInt32(integral) + Vector256.Create(MatrixExponentBias)) << MatrixMantissaBits).AsSingle();
        Vector256<float> fraction = value - integral;
        Vector256<float> numerator = fraction + Vector256.Create(MatrixExpNumerator2);
        numerator = Vector256.MultiplyAddEstimate(numerator, fraction, Vector256.Create(MatrixExpNumerator1));
        numerator = Vector256.MultiplyAddEstimate(numerator, fraction, Vector256.Create(MatrixExpNumerator0));
        numerator *= scale;
        Vector256<float> denominator = Vector256.MultiplyAddEstimate(
            fraction, Vector256.Create(MatrixExpDenominator3), Vector256.Create(MatrixExpDenominator2));

        denominator = Vector256.MultiplyAddEstimate(denominator, fraction, Vector256.Create(MatrixExpDenominator1));
        denominator = Vector256.MultiplyAddEstimate(denominator, fraction, Vector256.Create(MatrixExpDenominator0));
        return numerator / denominator;
    }

    /// <inheritdoc cref="MatrixExp2(float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> MatrixExp2(Vector512<float> value)
    {
        Vector512<float> integral = Vector512.Floor(value);
        Vector512<float> scale = ((Vector512.ConvertToInt32(integral) + Vector512.Create(MatrixExponentBias)) << MatrixMantissaBits).AsSingle();
        Vector512<float> fraction = value - integral;
        Vector512<float> numerator = fraction + Vector512.Create(MatrixExpNumerator2);
        numerator = Vector512.MultiplyAddEstimate(numerator, fraction, Vector512.Create(MatrixExpNumerator1));
        numerator = Vector512.MultiplyAddEstimate(numerator, fraction, Vector512.Create(MatrixExpNumerator0));
        numerator *= scale;
        Vector512<float> denominator = Vector512.MultiplyAddEstimate(
            fraction, Vector512.Create(MatrixExpDenominator3), Vector512.Create(MatrixExpDenominator2));

        denominator = Vector512.MultiplyAddEstimate(denominator, fraction, Vector512.Create(MatrixExpDenominator1));
        denominator = Vector512.MultiplyAddEstimate(denominator, fraction, Vector512.Create(MatrixExpDenominator0));
        return numerator / denominator;
    }

    /// <summary>
    /// Interpolates independent radial frequencies using prepared adjacent-band logarithms.
    /// </summary>
    private readonly ref struct RadialQuantizationOperator
    {
        private readonly ReadOnlySpan<float> bands;
        private readonly ReadOnlySpan<float> logarithms;
        private readonly float xScale;

        /// <summary>
        /// Initializes a new instance of the <see cref="RadialQuantizationOperator"/> struct.
        /// </summary>
        /// <param name="bands">Positive absolute weights for the encoded radial bands.</param>
        /// <param name="logarithms">The base-two logarithm of each next-band to current-band ratio.</param>
        /// <param name="xScale">The horizontal coefficient-index to band-distance multiplier.</param>
        public RadialQuantizationOperator(ReadOnlySpan<float> bands, ReadOnlySpan<float> logarithms, float xScale)
        {
            this.bands = bands;
            this.logarithms = logarithms;
            this.xScale = xScale;
        }

        /// <summary>
        /// Computes one coefficient weight with the same distance and interpolation arithmetic as the SIMD paths.
        /// </summary>
        /// <param name="column">The horizontal coefficient index.</param>
        /// <param name="dySquared">The row's squared vertical band distance.</param>
        /// <returns>The interpolated relative weight.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float column, float dySquared)
        {
            float dx = column * this.xScale;
            return this.Interpolate(MathF.Sqrt(float.MultiplyAddEstimate(dx, dx, dySquared)));
        }

        /// <summary>
        /// Interpolates one nonnegative position below the final band, including the sparse AFV frequency positions.
        /// </summary>
        /// <param name="position">A band index plus its fractional position in the following interval.</param>
        /// <returns>The geometrically interpolated relative weight.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Interpolate(float position)
        {
            int index = (int)position;
            float exponent = this.logarithms[index] * (position - index);
            return this.bands[index] * MatrixExp2(exponent);
        }

        /// <summary>
        /// Computes 4 independent coefficient weights in horizontal lane order.
        /// </summary>
        /// <param name="columns">The horizontal coefficient indices.</param>
        /// <param name="dySquared">The row's squared vertical band distance.</param>
        /// <returns>The interpolated relative weights.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> columns, float dySquared)
        {
            Vector128<float> dx = columns * Vector128.Create(this.xScale);
            Vector128<float> distance = Vector128.Sqrt(Vector128.MultiplyAddEstimate(dx, dx, Vector128.Create(dySquared)));
            Vector128<int> indices = Vector128.ConvertToInt32(distance);
            ref int bandBits = ref Unsafe.As<float, int>(ref MemoryMarshal.GetReference(this.bands));
            ref int logarithmBits = ref Unsafe.As<float, int>(ref MemoryMarshal.GetReference(this.logarithms));

            // The distance is nonnegative, so truncation supplies floor. The format's diagonal epsilon proves the gather indices.
            // Shared gathers preserve logical lane order on every ISA. Reinterpretation changes storage only, not float values.
            Vector128<float> seeds = Vector128_.Gather(ref bandBits, indices).AsSingle();
            Vector128<float> ratios = Vector128_.Gather(ref logarithmBits, indices).AsSingle();
            Vector128<float> fractions = distance - Vector128.ConvertToSingle(indices);
            return seeds * MatrixExp2(ratios * fractions);
        }

        /// <summary>
        /// Computes 8 independent coefficient weights in horizontal lane order.
        /// </summary>
        /// <param name="columns">The horizontal coefficient indices.</param>
        /// <param name="dySquared">The row's squared vertical band distance.</param>
        /// <returns>The interpolated relative weights.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> columns, float dySquared)
        {
            Vector256<float> dx = columns * Vector256.Create(this.xScale);
            Vector256<float> distance = Vector256.Sqrt(Vector256.MultiplyAddEstimate(dx, dx, Vector256.Create(dySquared)));
            Vector256<int> indices = Vector256.ConvertToInt32(distance);
            ref int bandBits = ref Unsafe.As<float, int>(ref MemoryMarshal.GetReference(this.bands));
            ref int logarithmBits = ref Unsafe.As<float, int>(ref MemoryMarshal.GetReference(this.logarithms));

            // The distance is nonnegative, so truncation supplies floor. The format's diagonal epsilon proves the gather indices.
            // Shared gathers preserve logical lane order on every ISA. Reinterpretation changes storage only, not float values.
            Vector256<float> seeds = Vector256_.Gather(ref bandBits, indices).AsSingle();
            Vector256<float> ratios = Vector256_.Gather(ref logarithmBits, indices).AsSingle();
            Vector256<float> fractions = distance - Vector256.ConvertToSingle(indices);
            return seeds * MatrixExp2(ratios * fractions);
        }

        /// <summary>
        /// Computes 16 independent coefficient weights in horizontal lane order.
        /// </summary>
        /// <param name="columns">The horizontal coefficient indices.</param>
        /// <param name="dySquared">The row's squared vertical band distance.</param>
        /// <returns>The interpolated relative weights.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> columns, float dySquared)
        {
            Vector512<float> dx = columns * Vector512.Create(this.xScale);
            Vector512<float> distance = Vector512.Sqrt(Vector512.MultiplyAddEstimate(dx, dx, Vector512.Create(dySquared)));
            Vector512<int> indices = Vector512.ConvertToInt32(distance);
            ref int bandBits = ref Unsafe.As<float, int>(ref MemoryMarshal.GetReference(this.bands));
            ref int logarithmBits = ref Unsafe.As<float, int>(ref MemoryMarshal.GetReference(this.logarithms));

            // The distance is nonnegative, so truncation supplies floor. The format's diagonal epsilon proves the gather indices.
            // Shared gathers preserve logical lane order on every ISA. Reinterpretation changes storage only, not float values.
            Vector512<float> seeds = Vector512_.Gather(ref bandBits, indices).AsSingle();
            Vector512<float> ratios = Vector512_.Gather(ref logarithmBits, indices).AsSingle();
            Vector512<float> fractions = distance - Vector512.ConvertToSingle(indices);
            return seeds * MatrixExp2(ratios * fractions);
        }
    }

    /// <summary>
    /// Expands RAW integers with the encoded denominator and rejects invalid nonpositive samples.
    /// </summary>
    private readonly struct RawQuantizationOperator
    {
        private readonly float denominator;

        /// <summary>
        /// Initializes a new instance of the <see cref="RawQuantizationOperator"/> struct.
        /// </summary>
        /// <param name="denominator">The finite positive denominator from the RAW description.</param>
        public RawQuantizationOperator(float denominator) => this.denominator = denominator;

        /// <summary>
        /// Converts one positive integer to a relative weight.
        /// </summary>
        /// <param name="sample">The decoded positive table integer.</param>
        /// <returns>The reciprocal of the scaled integer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(int sample)
        {
            if (sample <= 0)
            {
                throw new InvalidImageContentException("The JPEG XL RAW quantization table contains a nonpositive sample.");
            }

            return 1F / (this.denominator * sample);
        }

        /// <summary>
        /// Converts 4 independent RAW integer lanes to relative weights.
        /// </summary>
        /// <param name="samples">The decoded positive table integers.</param>
        /// <returns>The reciprocals of the scaled integers, in unchanged lane order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<int> samples)
        {
            if (!Vector128.GreaterThanAll(samples, Vector128<int>.Zero))
            {
                throw new InvalidImageContentException("The JPEG XL RAW quantization table contains a nonpositive sample.");
            }

            return Vector128<float>.One / (Vector128.Create(this.denominator) * Vector128.ConvertToSingle(samples));
        }

        /// <summary>
        /// Converts 8 independent RAW integer lanes to relative weights.
        /// </summary>
        /// <param name="samples">The decoded positive table integers.</param>
        /// <returns>The reciprocals of the scaled integers, in unchanged lane order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<int> samples)
        {
            if (!Vector256.GreaterThanAll(samples, Vector256<int>.Zero))
            {
                throw new InvalidImageContentException("The JPEG XL RAW quantization table contains a nonpositive sample.");
            }

            return Vector256<float>.One / (Vector256.Create(this.denominator) * Vector256.ConvertToSingle(samples));
        }

        /// <summary>
        /// Converts 16 independent RAW integer lanes to relative weights.
        /// </summary>
        /// <param name="samples">The decoded positive table integers.</param>
        /// <returns>The reciprocals of the scaled integers, in unchanged lane order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<int> samples)
        {
            if (!Vector512.GreaterThanAll(samples, Vector512<int>.Zero))
            {
                throw new InvalidImageContentException("The JPEG XL RAW quantization table contains a nonpositive sample.");
            }

            return Vector512<float>.One / (Vector512.Create(this.denominator) * Vector512.ConvertToSingle(samples));
        }
    }

    /// <summary>
    /// Validates relative weights in [1e-8,1e8) and returns their decoder reciprocals.
    /// </summary>
    private readonly struct InverseQuantizationOperator
    {
        /// <summary>
        /// Validates and inverts one positive finite weight.
        /// </summary>
        /// <param name="weight">The generated relative coefficient weight.</param>
        /// <returns>The decoder coefficient multiplier.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Invoke(float weight)
        {
            if (!(weight >= MinimumQuantizationWeight && weight < MaximumQuantizationWeight))
            {
                throw new InvalidImageContentException("The JPEG XL quantization weight is outside its permitted range.");
            }

            return 1F / weight;
        }

        /// <summary>
        /// Validates and inverts 4 independent weight lanes.
        /// </summary>
        /// <param name="weights">The generated relative coefficient weights.</param>
        /// <returns>The decoder coefficient multipliers.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Invoke(Vector128<float> weights)
        {
            // Ordered comparisons reject NaN as well as infinities and weights outside the format's finite range.
            if (!Vector128.GreaterThanOrEqualAll(weights, Vector128.Create(MinimumQuantizationWeight))
                || !Vector128.LessThanAll(weights, Vector128.Create(MaximumQuantizationWeight)))
            {
                throw new InvalidImageContentException("The JPEG XL quantization weight is outside its permitted range.");
            }

            return Vector128<float>.One / weights;
        }

        /// <summary>
        /// Validates and inverts 8 independent weight lanes.
        /// </summary>
        /// <param name="weights">The generated relative coefficient weights.</param>
        /// <returns>The decoder coefficient multipliers.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> Invoke(Vector256<float> weights)
        {
            // Ordered comparisons reject NaN as well as infinities and weights outside the format's finite range.
            if (!Vector256.GreaterThanOrEqualAll(weights, Vector256.Create(MinimumQuantizationWeight))
                || !Vector256.LessThanAll(weights, Vector256.Create(MaximumQuantizationWeight)))
            {
                throw new InvalidImageContentException("The JPEG XL quantization weight is outside its permitted range.");
            }

            return Vector256<float>.One / weights;
        }

        /// <summary>
        /// Validates and inverts 16 independent weight lanes.
        /// </summary>
        /// <param name="weights">The generated relative coefficient weights.</param>
        /// <returns>The decoder coefficient multipliers.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<float> Invoke(Vector512<float> weights)
        {
            // Ordered comparisons reject NaN as well as infinities and weights outside the format's finite range.
            if (!Vector512.GreaterThanOrEqualAll(weights, Vector512.Create(MinimumQuantizationWeight))
                || !Vector512.LessThanAll(weights, Vector512.Create(MaximumQuantizationWeight)))
            {
                throw new InvalidImageContentException("The JPEG XL quantization weight is outside its permitted range.");
            }

            return Vector512<float>.One / weights;
        }
    }
}
