// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;

internal sealed partial class JxlSplines
{
    /// <summary>
    /// The reference 1/(2*sqrt(2)) integration offset from libjxl splines.cc DrawSegment.
    /// It sets the two pixel-integration endpoints before division by Gaussian width.
    /// </summary>
    private const float GaussianIntegralOffset = .353553391F;

    /// <summary>
    /// The leading quartic coefficient of libjxl base/fast_math-inl.h FastErff's reciprocal polynomial.
    /// </summary>
    private const float ErfCoefficient0 = .0777394369F;

    /// <summary>
    /// The cubic coefficient of the FastErff denominator polynomial in the absolute integration endpoint.
    /// </summary>
    private const float ErfCoefficient1 = .000205260015F;

    /// <summary>
    /// The quadratic coefficient of the FastErff denominator polynomial in the absolute integration endpoint.
    /// </summary>
    private const float ErfCoefficient2 = .232120216F;

    /// <summary>
    /// The linear coefficient of the FastErff denominator polynomial. Its constant term is one.
    /// </summary>
    private const float ErfCoefficient3 = .277820801F;

    /// <summary>
    /// Evaluates the reference error-function approximation for Gaussian integration.
    /// </summary>
    /// <param name="value">The signed normalized integration endpoint.</param>
    /// <returns>The approximate error function with the input sign.</returns>
    /// <remarks>
    /// The four coefficients are the fitted FastErff values from libjxl base/fast_math-inl.h.
    /// The reference documents an L1 approximation error of 7e-4. No pixel or alpha bounds apply here.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Erf(float value)
    {
        // Both references use this fitted fourth-power reciprocal polynomial. The runtime and the
        // existing shared numeric helpers have no error-function operation to reuse.
        float absolute = MathF.Abs(value);
        float denominator = float.MultiplyAddEstimate(absolute, ErfCoefficient0, ErfCoefficient1);
        denominator = float.MultiplyAddEstimate(denominator, absolute, ErfCoefficient2);
        denominator = float.MultiplyAddEstimate(denominator, absolute, ErfCoefficient3);
        denominator = float.MultiplyAddEstimate(denominator, absolute, 1F);
        float reciprocal = 1F / (denominator * denominator);
        return MathF.CopySign(float.MultiplyAddEstimate(-reciprocal, reciprocal, 1F), value);
    }

    /// <inheritdoc cref="Erf(float)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> Erf(Vector128<float> value)
    {
        Vector128<float> absolute = Vector128.Abs(value);
        Vector128<float> denominator = Vector128.MultiplyAddEstimate(absolute, Vector128.Create(ErfCoefficient0), Vector128.Create(ErfCoefficient1));
        denominator = Vector128.MultiplyAddEstimate(denominator, absolute, Vector128.Create(ErfCoefficient2));
        denominator = Vector128.MultiplyAddEstimate(denominator, absolute, Vector128.Create(ErfCoefficient3));
        denominator = Vector128.MultiplyAddEstimate(denominator, absolute, Vector128<float>.One);
        Vector128<float> reciprocal = Vector128<float>.One / (denominator * denominator);
        return Vector128.CopySign(Vector128.MultiplyAddEstimate(-reciprocal, reciprocal, Vector128<float>.One), value);
    }

    /// <inheritdoc cref="Erf(float)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> Erf(Vector256<float> value)
    {
        Vector256<float> absolute = Vector256.Abs(value);
        Vector256<float> denominator = Vector256.MultiplyAddEstimate(absolute, Vector256.Create(ErfCoefficient0), Vector256.Create(ErfCoefficient1));
        denominator = Vector256.MultiplyAddEstimate(denominator, absolute, Vector256.Create(ErfCoefficient2));
        denominator = Vector256.MultiplyAddEstimate(denominator, absolute, Vector256.Create(ErfCoefficient3));
        denominator = Vector256.MultiplyAddEstimate(denominator, absolute, Vector256<float>.One);
        Vector256<float> reciprocal = Vector256<float>.One / (denominator * denominator);
        return Vector256.CopySign(Vector256.MultiplyAddEstimate(-reciprocal, reciprocal, Vector256<float>.One), value);
    }

    /// <inheritdoc cref="Erf(float)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> Erf(Vector512<float> value)
    {
        Vector512<float> absolute = Vector512.Abs(value);
        Vector512<float> denominator = Vector512.MultiplyAddEstimate(absolute, Vector512.Create(ErfCoefficient0), Vector512.Create(ErfCoefficient1));
        denominator = Vector512.MultiplyAddEstimate(denominator, absolute, Vector512.Create(ErfCoefficient2));
        denominator = Vector512.MultiplyAddEstimate(denominator, absolute, Vector512.Create(ErfCoefficient3));
        denominator = Vector512.MultiplyAddEstimate(denominator, absolute, Vector512<float>.One);
        Vector512<float> reciprocal = Vector512<float>.One / (denominator * denominator);
        return Vector512.CopySign(Vector512.MultiplyAddEstimate(-reciprocal, reciprocal, Vector512<float>.One), value);
    }

    /// <summary>
    /// Evaluates the reference float cosine for the bounded continuous spline DCT arguments.
    /// </summary>
    /// <remarks>
    /// Arguments range from zero through (31 * pi / 32) * 31.5 radians. This is the float-only
    /// FastCosf algorithm in libjxl base/fast_math-inl.h and jxl-rs util/fast_math.rs.
    /// The runtime cosine widens float lanes to double; this kernel retains the reference float arithmetic.
    /// </remarks>
    private readonly struct CosineOperator : TensorPrimitives_.IUnaryOperator
    {
        /// <summary>
        /// The 2*pi-radian period used by libjxl base/fast_math-inl.h FastCosf range reduction.
        /// </summary>
        private const float Period = 2F * MathF.PI;

        /// <summary>
        /// The reciprocal period that converts radians to complete cosine cycles before flooring.
        /// </summary>
        private const float InversePeriod = .5F / MathF.PI;

        /// <summary>
        /// The pi/2-radian boundary used to fold cosine arguments and retain the final sign.
        /// </summary>
        private const float HalfPi = MathF.PI / 2F;

        /// <summary>
        /// The one-quarter angle scale used by FastCosf before its two angle-doubling steps.
        /// </summary>
        private const float QuarterAngle = .25F;

        /// <summary>
        /// The reference fourth-degree coefficient of FastCosf's fitted polynomial on [0, pi/8].
        /// </summary>
        private const float QuarticCoefficient = .06960438F;

        /// <summary>
        /// The reference second-degree coefficient of FastCosf's fitted polynomial on [0, pi/8].
        /// </summary>
        private const float QuadraticCoefficient = -.84087373F;

        /// <summary>
        /// The reference constant term of FastCosf's fitted polynomial, scaled by 2^(3/4) for angle doubling.
        /// </summary>
        private const float ConstantCoefficient = 1.68179268F;

        /// <summary>
        /// Evaluates one bounded angle with range folding and two angle doublings.
        /// </summary>
        /// <param name="value">The nonnegative spline DCT angle.</param>
        /// <returns>The reference cosine approximation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float value)
        {
            float reduced = value - (MathF.Floor(value * InversePeriod) * Period);
            float folded = MathF.Min(reduced, Period - reduced);
            bool negative = folded >= HalfPi;
            float quarter = (negative ? MathF.PI - folded : folded) * QuarterAngle;
            float squared = quarter * quarter;
            float fourth = squared * squared;
            float polynomial = float.MultiplyAddEstimate(
                fourth,
                QuarticCoefficient,
                float.MultiplyAddEstimate(squared, QuadraticCoefficient, ConstantCoefficient));

            float doubled = float.MultiplyAddEstimate(polynomial, polynomial, -RootTwo);
            float result = float.MultiplyAddEstimate(doubled, doubled, -1F);
            return negative ? -result : result;
        }

        /// <inheritdoc cref="Invoke(float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            // Each lane is one frequency. Fold it to [0, pi/2], evaluate the fitted quartic at
            // one quarter of the angle, then double twice and restore the folded sign.
            Vector128<float> reduced = value - (Vector128.Floor(value * Vector128.Create(InversePeriod)) * Vector128.Create(Period));
            Vector128<float> folded = Vector128.Min(reduced, Vector128.Create(Period) - reduced);
            Vector128<float> negative = Vector128.GreaterThanOrEqual(folded, Vector128.Create(HalfPi));
            Vector128<float> quarter = Vector128.ConditionalSelect(negative, Vector128.Create(MathF.PI) - folded, folded) * Vector128.Create(QuarterAngle);
            Vector128<float> squared = quarter * quarter;
            Vector128<float> fourth = squared * squared;
            Vector128<float> polynomial = Vector128.MultiplyAddEstimate(
                fourth,
                Vector128.Create(QuarticCoefficient),
                Vector128.MultiplyAddEstimate(squared, Vector128.Create(QuadraticCoefficient), Vector128.Create(ConstantCoefficient)));

            Vector128<float> doubled = Vector128.MultiplyAddEstimate(polynomial, polynomial, Vector128.Create(-RootTwo));
            Vector128<float> result = Vector128.MultiplyAddEstimate(doubled, doubled, -Vector128<float>.One);
            return Vector128.ConditionalSelect(negative, -result, result);
        }

        /// <inheritdoc cref="Invoke(float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            // Each lane is one frequency. Fold it to [0, pi/2], evaluate the fitted quartic at
            // one quarter of the angle, then double twice and restore the folded sign.
            Vector256<float> reduced = value - (Vector256.Floor(value * Vector256.Create(InversePeriod)) * Vector256.Create(Period));
            Vector256<float> folded = Vector256.Min(reduced, Vector256.Create(Period) - reduced);
            Vector256<float> negative = Vector256.GreaterThanOrEqual(folded, Vector256.Create(HalfPi));
            Vector256<float> quarter = Vector256.ConditionalSelect(negative, Vector256.Create(MathF.PI) - folded, folded) * Vector256.Create(QuarterAngle);
            Vector256<float> squared = quarter * quarter;
            Vector256<float> fourth = squared * squared;
            Vector256<float> polynomial = Vector256.MultiplyAddEstimate(
                fourth,
                Vector256.Create(QuarticCoefficient),
                Vector256.MultiplyAddEstimate(squared, Vector256.Create(QuadraticCoefficient), Vector256.Create(ConstantCoefficient)));

            Vector256<float> doubled = Vector256.MultiplyAddEstimate(polynomial, polynomial, Vector256.Create(-RootTwo));
            Vector256<float> result = Vector256.MultiplyAddEstimate(doubled, doubled, -Vector256<float>.One);
            return Vector256.ConditionalSelect(negative, -result, result);
        }

        /// <inheritdoc cref="Invoke(float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            // Each lane is one frequency. Fold it to [0, pi/2], evaluate the fitted quartic at
            // one quarter of the angle, then double twice and restore the folded sign.
            Vector512<float> reduced = value - (Vector512.Floor(value * Vector512.Create(InversePeriod)) * Vector512.Create(Period));
            Vector512<float> folded = Vector512.Min(reduced, Vector512.Create(Period) - reduced);
            Vector512<float> negative = Vector512.GreaterThanOrEqual(folded, Vector512.Create(HalfPi));
            Vector512<float> quarter = Vector512.ConditionalSelect(negative, Vector512.Create(MathF.PI) - folded, folded) * Vector512.Create(QuarterAngle);
            Vector512<float> squared = quarter * quarter;
            Vector512<float> fourth = squared * squared;
            Vector512<float> polynomial = Vector512.MultiplyAddEstimate(
                fourth,
                Vector512.Create(QuarticCoefficient),
                Vector512.MultiplyAddEstimate(squared, Vector512.Create(QuadraticCoefficient), Vector512.Create(ConstantCoefficient)));

            Vector512<float> doubled = Vector512.MultiplyAddEstimate(polynomial, polynomial, Vector512.Create(-RootTwo));
            Vector512<float> result = Vector512.MultiplyAddEstimate(doubled, doubled, -Vector512<float>.One);
            return Vector512.ConditionalSelect(negative, -result, result);
        }
    }

    /// <summary>
    /// Calculates one Gaussian's integrated intensity at independent horizontal pixel positions.
    /// </summary>
    /// <remarks>
    /// The three color planes consume the same intensity register. Parameters are prepared once per
    /// painted row; no intermediate intensity row or separate tensor traversal is required.
    /// </remarks>
    private readonly struct GaussianOperator : TensorPrimitives_.IUnaryOperator
    {
        private readonly float centerX;
        private readonly float dySquared;
        private readonly float inverseSigma;
        private readonly float scaledIntensity;

        /// <summary>
        /// Initializes a new instance of the <see cref="GaussianOperator"/> struct for one painted row.
        /// </summary>
        /// <param name="centerX">The horizontal Gaussian center in coding coordinates.</param>
        /// <param name="dySquared">The row's squared vertical distance from the center.</param>
        /// <param name="inverseSigma">The reciprocal Gaussian width.</param>
        /// <param name="scaledIntensity">Sigma times one quarter of the sample's arc-length weight.</param>
        public GaussianOperator(float centerX, float dySquared, float inverseSigma, float scaledIntensity)
        {
            this.centerX = centerX;
            this.dySquared = dySquared;
            this.inverseSigma = inverseSigma;
            this.scaledIntensity = scaledIntensity;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float value)
        {
            float dx = value - this.centerX;
            float distance = MathF.Sqrt(float.MultiplyAddEstimate(dx, dx, this.dySquared));
            float first = float.MultiplyAddEstimate(distance, .5F, GaussianIntegralOffset) * this.inverseSigma;
            float second = float.MultiplyAddEstimate(distance, .5F, -GaussianIntegralOffset) * this.inverseSigma;
            float integral = Erf(first) - Erf(second);
            return this.scaledIntensity * (integral * integral);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            // Lanes are consecutive pixels. Broadcast row parameters, evaluate the scalar integral
            // independently, and retain its intensity for all three color-plane updates.
            Vector128<float> dx = value - Vector128.Create(this.centerX);
            Vector128<float> distance = Vector128.Sqrt(Vector128.MultiplyAddEstimate(dx, dx, Vector128.Create(this.dySquared)));
            Vector128<float> first = Vector128.MultiplyAddEstimate(
                distance,
                Vector128.Create(.5F),
                Vector128.Create(GaussianIntegralOffset)) * Vector128.Create(this.inverseSigma);

            Vector128<float> second = Vector128.MultiplyAddEstimate(
                distance,
                Vector128.Create(.5F),
                Vector128.Create(-GaussianIntegralOffset)) * Vector128.Create(this.inverseSigma);

            Vector128<float> integral = Erf(first) - Erf(second);
            return Vector128.Create(this.scaledIntensity) * (integral * integral);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            // Lanes are consecutive pixels. Broadcast row parameters, evaluate the scalar integral
            // independently, and retain its intensity for all three color-plane updates.
            Vector256<float> dx = value - Vector256.Create(this.centerX);
            Vector256<float> distance = Vector256.Sqrt(Vector256.MultiplyAddEstimate(dx, dx, Vector256.Create(this.dySquared)));
            Vector256<float> first = Vector256.MultiplyAddEstimate(
                distance,
                Vector256.Create(.5F),
                Vector256.Create(GaussianIntegralOffset)) * Vector256.Create(this.inverseSigma);

            Vector256<float> second = Vector256.MultiplyAddEstimate(
                distance,
                Vector256.Create(.5F),
                Vector256.Create(-GaussianIntegralOffset)) * Vector256.Create(this.inverseSigma);

            Vector256<float> integral = Erf(first) - Erf(second);
            return Vector256.Create(this.scaledIntensity) * (integral * integral);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            // Lanes are consecutive pixels. Broadcast row parameters, evaluate the scalar integral
            // independently, and retain its intensity for all three color-plane updates.
            Vector512<float> dx = value - Vector512.Create(this.centerX);
            Vector512<float> distance = Vector512.Sqrt(Vector512.MultiplyAddEstimate(dx, dx, Vector512.Create(this.dySquared)));
            Vector512<float> first = Vector512.MultiplyAddEstimate(
                distance,
                Vector512.Create(.5F),
                Vector512.Create(GaussianIntegralOffset)) * Vector512.Create(this.inverseSigma);

            Vector512<float> second = Vector512.MultiplyAddEstimate(
                distance,
                Vector512.Create(.5F),
                Vector512.Create(-GaussianIntegralOffset)) * Vector512.Create(this.inverseSigma);

            Vector512<float> integral = Erf(first) - Erf(second);
            return Vector512.Create(this.scaledIntensity) * (integral * integral);
        }
    }
}
