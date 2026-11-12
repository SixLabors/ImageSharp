// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.ColorProfiles.Companding;

/// <content>
/// Provides the shared component formulas and signed scalar and SIMD operators.
/// </content>
public static partial class PqCompanding
{
    /// <summary>
    /// The PQ rational offset.
    /// </summary>
    private const float C1 = 0.8359375F;

    /// <summary>
    /// The PQ numerator scale.
    /// </summary>
    private const float C2 = 18.8515625F;

    /// <summary>
    /// The PQ denominator scale.
    /// </summary>
    private const float C3 = 18.6875F;

    /// <summary>
    /// The PQ outer exponent.
    /// </summary>
    private const float M = 78.84375F;

    /// <summary>
    /// The PQ inner exponent.
    /// </summary>
    private const float N = 0.1593017578125F;

    /// <summary>
    /// The exact common value of 1 minus C1 and C2 minus C3.
    /// </summary>
    private const float RationalOffset = 0.1640625F;

    /// <summary>
    /// The degree-1 expm1 coefficient: the reciprocal of 1 factorial.
    /// </summary>
    private const float Expm1FirstCoefficient = 1F;

    /// <summary>
    /// The degree-2 expm1 coefficient: the reciprocal of 2 factorial.
    /// </summary>
    private const float Expm1SecondCoefficient = 1F / 2F;

    /// <summary>
    /// The degree-3 expm1 coefficient: the reciprocal of 3 factorial.
    /// </summary>
    private const float Expm1ThirdCoefficient = 1F / 6F;

    /// <summary>
    /// The degree-4 expm1 coefficient: the reciprocal of 4 factorial.
    /// </summary>
    private const float Expm1FourthCoefficient = 1F / 24F;

    /// <summary>
    /// The degree-5 expm1 coefficient: the reciprocal of 5 factorial.
    /// </summary>
    private const float Expm1FifthCoefficient = 1F / 120F;

    /// <summary>
    /// The degree-6 expm1 coefficient: the reciprocal of 6 factorial.
    /// </summary>
    private const float Expm1SixthCoefficient = 1F / 720F;

    /// <summary>
    /// The degree-7 expm1 coefficient: the reciprocal of 7 factorial.
    /// </summary>
    private const float Expm1SeventhCoefficient = 1F / 5040F;

    /// <summary>
    /// Applies the fractional part of the inverse PQ inner exponent after its integer factor.
    /// </summary>
    private static readonly TensorPrimitives_.PowOperator InverseInnerPower = new((1F / N) - 6F);

    /// <summary>
    /// Applies the PQ inner exponent before evaluating the forward rational term.
    /// </summary>
    private static readonly TensorPrimitives_.PowOperator InnerPower = new(N);

    /// <summary>
    /// Applies the fractional part of the PQ outer exponent after its integer factor.
    /// </summary>
    private static readonly TensorPrimitives_.PowOperator OuterPower = new(M - 78F);

    /// <summary>
    /// Decodes nonnegative PQ components relative to 10000 cd/m².
    /// </summary>
    /// <param name="value">The nonnegative component magnitude, or independent SIMD component magnitudes.</param>
    /// <returns>The converted component magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float ExpandMagnitude(float value)
    {
        // Write x^(1/M) as 1+d. Evaluating expm1(log(x)/M) retains the small root offset before the PQ rational amplifies it.
        float logarithm = MathF.Log(value) / M;

        // Positive-light PQ has log(C1) <= logarithm < log(C2/C3), or approximately [-0.179202,0.008741].
        // The degree-seven expm1 series has truncation error below 2.661e-11 on that interval, before float rounding.
        // For smaller finite inputs the series remains monotone and the numerator floor supplies zero light.
        // Log(0) is negative infinity: Horner yields d=-infinity, so the floored numerator divided by infinity is still zero.
        float polynomial = Expm1SeventhCoefficient;
        polynomial = float.MultiplyAddEstimate(polynomial, logarithm, Expm1SixthCoefficient);
        polynomial = float.MultiplyAddEstimate(polynomial, logarithm, Expm1FifthCoefficient);
        polynomial = float.MultiplyAddEstimate(polynomial, logarithm, Expm1FourthCoefficient);
        polynomial = float.MultiplyAddEstimate(polynomial, logarithm, Expm1ThirdCoefficient);
        polynomial = float.MultiplyAddEstimate(polynomial, logarithm, Expm1SecondCoefficient);
        polynomial = float.MultiplyAddEstimate(polynomial, logarithm, Expm1FirstCoefficient);
        float offset = logarithm * polynomial;

        // C2-C3 = 1-C1 exactly. Use the small offset directly instead of subtracting two values near nineteen.
        float numerator = MathF.Max(RationalOffset + offset, 0F);
        float denominator = float.MultiplyAddEstimate(-C3, offset, RationalOffset);
        float ratio = numerator / denominator;

        // Beyond the pole, the denominator is negative and the fractional power is undefined.
        // Pass NaN to the power operator so the caller applies its usual bounds for an undefined result.
        ratio = ratio < 0F ? float.NaN : ratio;

        // Split 1/N into its exact integer part and its float fractional remainder. This keeps the shared power approximation
        // from amplifying its logarithm error by the full inverse exponent. Integer powers use the same ordered products in every width.
        float square = ratio * ratio;
        float cube = square * ratio;
        return (cube * cube) * InverseInnerPower.Invoke(ratio);
    }

    /// <summary>
    /// Decodes nonnegative PQ components relative to 10000 cd/m².
    /// </summary>
    /// <param name="value">The nonnegative component magnitude, or independent SIMD component magnitudes.</param>
    /// <returns>The converted component magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<float> ExpandMagnitude(Vector128<float> value)
    {
        Vector128<float> logarithm = Vector128.Divide(Vector128.Log(value), Vector128.Create(M));

        // Each lane evaluates the same degree-seven expm1 series as the scalar path. No component widens or uses a lane loop.
        Vector128<float> polynomial = Vector128.Create(Expm1SeventhCoefficient);
        polynomial = Vector128.MultiplyAddEstimate(polynomial, logarithm, Vector128.Create(Expm1SixthCoefficient));
        polynomial = Vector128.MultiplyAddEstimate(polynomial, logarithm, Vector128.Create(Expm1FifthCoefficient));
        polynomial = Vector128.MultiplyAddEstimate(polynomial, logarithm, Vector128.Create(Expm1FourthCoefficient));
        polynomial = Vector128.MultiplyAddEstimate(polynomial, logarithm, Vector128.Create(Expm1ThirdCoefficient));
        polynomial = Vector128.MultiplyAddEstimate(polynomial, logarithm, Vector128.Create(Expm1SecondCoefficient));
        polynomial = Vector128.MultiplyAddEstimate(polynomial, logarithm, Vector128.Create(Expm1FirstCoefficient));
        Vector128<float> offset = Vector128.Multiply(logarithm, polynomial);

        Vector128<float> numerator = Vector128.Max(Vector128.Add(Vector128.Create(RationalOffset), offset), Vector128<float>.Zero);
        Vector128<float> denominator = Vector128.MultiplyAddEstimate(Vector128.Create(-C3), offset, Vector128.Create(RationalOffset));
        Vector128<float> ratio = Vector128.Divide(numerator, denominator);

        // Each lane keeps the scalar domain rule before the nonnegative power operation.
        ratio = Vector128.ConditionalSelect(Vector128.LessThan(ratio, Vector128<float>.Zero), Vector128.Create(float.NaN), ratio);

        Vector128<float> square = Vector128.Multiply(ratio, ratio);
        Vector128<float> cube = Vector128.Multiply(square, ratio);
        return Vector128.Multiply(Vector128.Multiply(cube, cube), InverseInnerPower.Invoke(ratio));
    }

    /// <summary>
    /// Decodes nonnegative PQ components relative to 10000 cd/m².
    /// </summary>
    /// <param name="value">The nonnegative component magnitude, or independent SIMD component magnitudes.</param>
    /// <returns>The converted component magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector256<float> ExpandMagnitude(Vector256<float> value)
    {
        Vector256<float> logarithm = Vector256.Divide(Vector256.Log(value), Vector256.Create(M));

        // Each lane evaluates the same degree-seven expm1 series as the scalar path. No component widens or uses a lane loop.
        Vector256<float> polynomial = Vector256.Create(Expm1SeventhCoefficient);
        polynomial = Vector256.MultiplyAddEstimate(polynomial, logarithm, Vector256.Create(Expm1SixthCoefficient));
        polynomial = Vector256.MultiplyAddEstimate(polynomial, logarithm, Vector256.Create(Expm1FifthCoefficient));
        polynomial = Vector256.MultiplyAddEstimate(polynomial, logarithm, Vector256.Create(Expm1FourthCoefficient));
        polynomial = Vector256.MultiplyAddEstimate(polynomial, logarithm, Vector256.Create(Expm1ThirdCoefficient));
        polynomial = Vector256.MultiplyAddEstimate(polynomial, logarithm, Vector256.Create(Expm1SecondCoefficient));
        polynomial = Vector256.MultiplyAddEstimate(polynomial, logarithm, Vector256.Create(Expm1FirstCoefficient));
        Vector256<float> offset = Vector256.Multiply(logarithm, polynomial);

        Vector256<float> numerator = Vector256.Max(Vector256.Add(Vector256.Create(RationalOffset), offset), Vector256<float>.Zero);
        Vector256<float> denominator = Vector256.MultiplyAddEstimate(Vector256.Create(-C3), offset, Vector256.Create(RationalOffset));
        Vector256<float> ratio = Vector256.Divide(numerator, denominator);

        // Each lane keeps the scalar domain rule before the nonnegative power operation.
        ratio = Vector256.ConditionalSelect(Vector256.LessThan(ratio, Vector256<float>.Zero), Vector256.Create(float.NaN), ratio);

        Vector256<float> square = Vector256.Multiply(ratio, ratio);
        Vector256<float> cube = Vector256.Multiply(square, ratio);
        return Vector256.Multiply(Vector256.Multiply(cube, cube), InverseInnerPower.Invoke(ratio));
    }

    /// <summary>
    /// Decodes nonnegative PQ components relative to 10000 cd/m².
    /// </summary>
    /// <param name="value">The nonnegative component magnitude, or independent SIMD component magnitudes.</param>
    /// <returns>The converted component magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector512<float> ExpandMagnitude(Vector512<float> value)
    {
        Vector512<float> logarithm = Vector512.Divide(Vector512.Log(value), Vector512.Create(M));

        // Each lane evaluates the same degree-seven expm1 series as the scalar path. No component widens or uses a lane loop.
        Vector512<float> polynomial = Vector512.Create(Expm1SeventhCoefficient);
        polynomial = Vector512.MultiplyAddEstimate(polynomial, logarithm, Vector512.Create(Expm1SixthCoefficient));
        polynomial = Vector512.MultiplyAddEstimate(polynomial, logarithm, Vector512.Create(Expm1FifthCoefficient));
        polynomial = Vector512.MultiplyAddEstimate(polynomial, logarithm, Vector512.Create(Expm1FourthCoefficient));
        polynomial = Vector512.MultiplyAddEstimate(polynomial, logarithm, Vector512.Create(Expm1ThirdCoefficient));
        polynomial = Vector512.MultiplyAddEstimate(polynomial, logarithm, Vector512.Create(Expm1SecondCoefficient));
        polynomial = Vector512.MultiplyAddEstimate(polynomial, logarithm, Vector512.Create(Expm1FirstCoefficient));
        Vector512<float> offset = Vector512.Multiply(logarithm, polynomial);

        Vector512<float> numerator = Vector512.Max(Vector512.Add(Vector512.Create(RationalOffset), offset), Vector512<float>.Zero);
        Vector512<float> denominator = Vector512.MultiplyAddEstimate(Vector512.Create(-C3), offset, Vector512.Create(RationalOffset));
        Vector512<float> ratio = Vector512.Divide(numerator, denominator);

        // Each lane keeps the scalar domain rule before the nonnegative power operation.
        ratio = Vector512.ConditionalSelect(Vector512.LessThan(ratio, Vector512<float>.Zero), Vector512.Create(float.NaN), ratio);

        Vector512<float> square = Vector512.Multiply(ratio, ratio);
        Vector512<float> cube = Vector512.Multiply(square, ratio);
        return Vector512.Multiply(Vector512.Multiply(cube, cube), InverseInnerPower.Invoke(ratio));
    }

    /// <summary>
    /// Encodes nonnegative PQ components relative to 10000 cd/m².
    /// </summary>
    /// <param name="value">The nonnegative component magnitude, or independent SIMD component magnitudes.</param>
    /// <returns>The converted component magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float CompressMagnitude(float value)
    {
        float power = InnerPower.Invoke(value);
        float numerator = C1 + (C2 * power);
        float denominator = 1F + (C3 * power);
        float ratio = numerator / denominator;

        // M = 78 + 0.84375 exactly. Apply the integer factor by squaring so the shared logarithm error is raised only to 0.84375.
        float square = ratio * ratio;
        float fourth = square * square;
        float eighth = fourth * fourth;
        float sixteenth = eighth * eighth;
        float thirtySecond = sixteenth * sixteenth;
        float sixtyFourth = thirtySecond * thirtySecond;
        float integer = ((sixtyFourth * eighth) * fourth) * square;
        return integer * OuterPower.Invoke(ratio);
    }

    /// <summary>
    /// Encodes nonnegative PQ components relative to 10000 cd/m².
    /// </summary>
    /// <param name="value">The nonnegative component magnitude, or independent SIMD component magnitudes.</param>
    /// <returns>The converted component magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<float> CompressMagnitude(Vector128<float> value)
    {
        Vector128<float> power = InnerPower.Invoke(value);
        Vector128<float> numerator = Vector128.Add(Vector128.Create(C1), Vector128.Multiply(Vector128.Create(C2), power));
        Vector128<float> denominator = Vector128.Add(Vector128.Create(1F), Vector128.Multiply(Vector128.Create(C3), power));
        Vector128<float> ratio = Vector128.Divide(numerator, denominator);

        // The integer multiplication tree and fractional remainder match the scalar path.
        Vector128<float> square = Vector128.Multiply(ratio, ratio);
        Vector128<float> fourth = Vector128.Multiply(square, square);
        Vector128<float> eighth = Vector128.Multiply(fourth, fourth);
        Vector128<float> sixteenth = Vector128.Multiply(eighth, eighth);
        Vector128<float> thirtySecond = Vector128.Multiply(sixteenth, sixteenth);
        Vector128<float> sixtyFourth = Vector128.Multiply(thirtySecond, thirtySecond);
        Vector128<float> integer = Vector128.Multiply(Vector128.Multiply(Vector128.Multiply(sixtyFourth, eighth), fourth), square);
        return Vector128.Multiply(integer, OuterPower.Invoke(ratio));
    }

    /// <summary>
    /// Encodes nonnegative PQ components relative to 10000 cd/m².
    /// </summary>
    /// <param name="value">The nonnegative component magnitude, or independent SIMD component magnitudes.</param>
    /// <returns>The converted component magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector256<float> CompressMagnitude(Vector256<float> value)
    {
        Vector256<float> power = InnerPower.Invoke(value);
        Vector256<float> numerator = Vector256.Add(Vector256.Create(C1), Vector256.Multiply(Vector256.Create(C2), power));
        Vector256<float> denominator = Vector256.Add(Vector256.Create(1F), Vector256.Multiply(Vector256.Create(C3), power));
        Vector256<float> ratio = Vector256.Divide(numerator, denominator);

        // The integer multiplication tree and fractional remainder match the scalar path.
        Vector256<float> square = Vector256.Multiply(ratio, ratio);
        Vector256<float> fourth = Vector256.Multiply(square, square);
        Vector256<float> eighth = Vector256.Multiply(fourth, fourth);
        Vector256<float> sixteenth = Vector256.Multiply(eighth, eighth);
        Vector256<float> thirtySecond = Vector256.Multiply(sixteenth, sixteenth);
        Vector256<float> sixtyFourth = Vector256.Multiply(thirtySecond, thirtySecond);
        Vector256<float> integer = Vector256.Multiply(Vector256.Multiply(Vector256.Multiply(sixtyFourth, eighth), fourth), square);
        return Vector256.Multiply(integer, OuterPower.Invoke(ratio));
    }

    /// <summary>
    /// Encodes nonnegative PQ components relative to 10000 cd/m².
    /// </summary>
    /// <param name="value">The nonnegative component magnitude, or independent SIMD component magnitudes.</param>
    /// <returns>The converted component magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector512<float> CompressMagnitude(Vector512<float> value)
    {
        Vector512<float> power = InnerPower.Invoke(value);
        Vector512<float> numerator = Vector512.Add(Vector512.Create(C1), Vector512.Multiply(Vector512.Create(C2), power));
        Vector512<float> denominator = Vector512.Add(Vector512.Create(1F), Vector512.Multiply(Vector512.Create(C3), power));
        Vector512<float> ratio = Vector512.Divide(numerator, denominator);

        // The integer multiplication tree and fractional remainder match the scalar path.
        Vector512<float> square = Vector512.Multiply(ratio, ratio);
        Vector512<float> fourth = Vector512.Multiply(square, square);
        Vector512<float> eighth = Vector512.Multiply(fourth, fourth);
        Vector512<float> sixteenth = Vector512.Multiply(eighth, eighth);
        Vector512<float> thirtySecond = Vector512.Multiply(sixteenth, sixteenth);
        Vector512<float> sixtyFourth = Vector512.Multiply(thirtySecond, thirtySecond);
        Vector512<float> integer = Vector512.Multiply(Vector512.Multiply(Vector512.Multiply(sixtyFourth, eighth), fourth), square);
        return Vector512.Multiply(integer, OuterPower.Invoke(ratio));
    }

    /// <summary>
    /// Decodes signed PQ component magnitudes before final result bounds.
    /// </summary>
    internal readonly struct ExpandOperator : TensorPrimitives_.IUnaryOperator
    {
        private readonly float scale;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpandOperator"/> struct.
        /// </summary>
        /// <param name="scale">The finite positive normalization factor prepared from a validated peak luminance.</param>
        public ExpandOperator(float scale) => this.scale = scale;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float value)
        {
            float magnitude = ExpandMagnitude(MathF.Abs(value));
            magnitude *= this.scale;
            return MathF.CopySign(magnitude, value);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            Vector128<float> magnitude = ExpandMagnitude(Vector128.Abs(value));
            magnitude = Vector128.Multiply(magnitude, Vector128.Create(this.scale));
            return Vector128.CopySign(magnitude, value);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            Vector256<float> magnitude = ExpandMagnitude(Vector256.Abs(value));
            magnitude = Vector256.Multiply(magnitude, Vector256.Create(this.scale));
            return Vector256.CopySign(magnitude, value);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            Vector512<float> magnitude = ExpandMagnitude(Vector512.Abs(value));
            magnitude = Vector512.Multiply(magnitude, Vector512.Create(this.scale));
            return Vector512.CopySign(magnitude, value);
        }
    }

    /// <summary>
    /// Encodes signed PQ component magnitudes before final result bounds.
    /// </summary>
    internal readonly struct CompressOperator : TensorPrimitives_.IUnaryOperator
    {
        private readonly float scale;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompressOperator"/> struct.
        /// </summary>
        /// <param name="scale">The finite positive normalization factor prepared from a validated peak luminance.</param>
        public CompressOperator(float scale) => this.scale = scale;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float value)
        {
            float magnitude = CompressMagnitude(MathF.Abs(value) * this.scale);

            // PQ has a nonzero mathematical code at zero light. Preserve exact signed zero at the public component boundary.
            magnitude = value == 0F ? 0F : magnitude;
            return MathF.CopySign(magnitude, value);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            Vector128<float> magnitude = CompressMagnitude(Vector128.Multiply(Vector128.Abs(value), Vector128.Create(this.scale)));

            // PQ has a nonzero mathematical code at zero light. Preserve exact signed zero at the public component boundary.
            magnitude = Vector128.ConditionalSelect(Vector128.Equals(value, Vector128<float>.Zero), Vector128<float>.Zero, magnitude);
            return Vector128.CopySign(magnitude, value);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            Vector256<float> magnitude = CompressMagnitude(Vector256.Multiply(Vector256.Abs(value), Vector256.Create(this.scale)));

            // PQ has a nonzero mathematical code at zero light. Preserve exact signed zero at the public component boundary.
            magnitude = Vector256.ConditionalSelect(Vector256.Equals(value, Vector256<float>.Zero), Vector256<float>.Zero, magnitude);
            return Vector256.CopySign(magnitude, value);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            Vector512<float> magnitude = CompressMagnitude(Vector512.Multiply(Vector512.Abs(value), Vector512.Create(this.scale)));

            // PQ has a nonzero mathematical code at zero light. Preserve exact signed zero at the public component boundary.
            magnitude = Vector512.ConditionalSelect(Vector512.Equals(value, Vector512<float>.Zero), Vector512<float>.Zero, magnitude);
            return Vector512.CopySign(magnitude, value);
        }
    }
}
