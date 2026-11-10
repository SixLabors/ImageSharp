// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Common.Helpers;

/// <content>
/// Configures a fixed power before component traversal.
/// </content>
internal static partial class TensorPrimitives_
{
    /// <summary>
    /// Raises components in place to a fixed power.
    /// </summary>
    /// <param name="values">The power bases. No base is less than zero.</param>
    /// <param name="exponent">The exponent shared by all components.</param>
    public static void Pow(Span<float> values, float exponent) => Invoke(values, new PowOperator(exponent));

    /// <summary>
    /// Classifies the exponent once and raises bases that are zero or more to that exponent.
    /// </summary>
    /// <remarks>
    /// No base is less than zero. All callers clamp the base, or use its magnitude and restore the sign themselves.
    /// Thus, the operator has no rules for negative bases. NaN bases give NaN.
    /// The sign of the result for a negative zero base is not defined.
    /// </remarks>
    public readonly struct PowOperator : IUnaryOperator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PowOperator"/> struct.
        /// </summary>
        /// <param name="exponent">The fixed exponent for the complete curve or span.</param>
        public PowOperator(float exponent)
        {
            this.Exponent = exponent;
            this.Kind = exponent == 0F ? PowerKind.Zero
                : exponent == 1F ? PowerKind.Identity
                : exponent == 2F ? PowerKind.Square
                : exponent == 3F ? PowerKind.Cube
                : !float.IsFinite(exponent) ? PowerKind.NonFinite
                : PowerKind.Finite;
        }

        /// <summary>
        /// Identifies the exponent cases selected before traversal.
        /// </summary>
        public enum PowerKind
        {
            /// <summary>
            /// Every base raised to zero returns one.
            /// </summary>
            Zero,

            /// <summary>
            /// A power of one returns the original base.
            /// </summary>
            Identity,

            /// <summary>
            /// Squaring uses one multiplication instead of logarithm and exponential approximations.
            /// </summary>
            Square,

            /// <summary>
            /// Cubing uses two multiplications instead of logarithm and exponential approximations.
            /// </summary>
            Cube,

            /// <summary>
            /// Nonfinite exponents use IEEE comparisons without evaluating a logarithm.
            /// </summary>
            NonFinite,

            /// <summary>
            /// Other finite exponents use the logarithm and exponential approximations.
            /// </summary>
            Finite
        }

        /// <summary>
        /// Gets the exponent shared by every component.
        /// </summary>
        public float Exponent { get; }

        /// <summary>
        /// Gets the operation selected during exponent classification.
        /// </summary>
        public PowerKind Kind { get; }

        /// <inheritdoc/>
        public float Invoke(float value)
        {
            switch (this.Kind)
            {
                case PowerKind.Zero:
                    return 1F;
                case PowerKind.Identity:
                    return value;
                case PowerKind.Square:
                    return value * value;
                case PowerKind.Cube:
                    return value * value * value;
                case PowerKind.NonFinite:
                    return MathF.Pow(value, this.Exponent);
            }

            // Use the SIMD approximation in scalar remainders too. A pixel's result must not depend on row length or SIMD support.
            return ApproximatePower(value, this.Exponent);
        }

        /// <summary>
        /// Evaluates the same binary32 logarithm and exponential approximation as the SIMD helpers.
        /// </summary>
        /// <param name="value">The nonnegative power base.</param>
        /// <param name="exponent">The finite nonzero exponent.</param>
        /// <returns>The approximate power, including zero, infinity and NaN results.</returns>
        private static float ApproximatePower(float value, float exponent)
        {
            const float ExponentScale = 1F / (1 << 23);
            const float LogOffset = 124.225514990F;
            const float LogLinear = 1.498030302F;
            const float LogNumerator = 1.725879990F;
            const float LogDenominator = 0.3520887068F;
            const float ExpOffset = 121.274057500F;
            const float ExpLinear = 1.490129070F;
            const float ExpNumerator = 27.728023300F;
            const float ExpDenominator = 4.84252568F;
            const float SignificandScale = 1 << 23;
            const float SubnormalScale = 1 << 24;

            // Zero gives the same result as the SIMD helpers for both zero signs: zero, or infinity for a negative exponent.
            if (value == 0F)
            {
                return exponent < 0F ? float.PositiveInfinity : 0F;
            }

            if (float.IsPositiveInfinity(value))
            {
                return exponent < 0F ? 0F : value;
            }

            if (value == 1F || float.IsNaN(value))
            {
                return value;
            }

            // Normalize subnormal bases before reading their exponent bits. Subtract the scaling exponent after the logarithm.
            bool small = BitConverter.SingleToUInt32Bits(value) < 0x00800000;
            int bits = BitConverter.SingleToInt32Bits(small ? value * SubnormalScale : value);
            float mantissa = BitConverter.Int32BitsToSingle((bits & 0x007FFFFF) | 0x3F000000);
            float logarithm = ((float)bits * ExponentScale) - LogOffset;
            logarithm -= LogLinear * mantissa;
            logarithm -= LogNumerator / (LogDenominator + mantissa);
            logarithm -= small ? 24F : 0F;
            float power = logarithm * exponent;
            if (power < -150F)
            {
                return 0F;
            }

            if (power > 128F)
            {
                return float.PositiveInfinity;
            }

            // Construct normal exponent bits, then scale into the subnormal or upper boundary range.
            // These bounds protect the bit conversion; they do not clip source or destination RGB values.
            bool low = power < -126F;
            bool high = power > 127F;
            float adjusted = power + (low ? 24F : 0F) - (high ? 1F : 0F);
            float fraction = adjusted - MathF.Floor(adjusted);
            float outputBits = adjusted + ExpOffset - (ExpLinear * fraction);
            outputBits += ExpNumerator / (ExpDenominator - fraction);
            outputBits *= SignificandScale;
            outputBits = Math.Clamp(outputBits, 0F, (float)0x7F800000);
            float result = BitConverter.Int32BitsToSingle((int)outputBits);
            result *= low ? 1F / SubnormalScale : 1F;
            return result * (high ? 2F : 1F);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            switch (this.Kind)
            {
                case PowerKind.Zero:
                    return Vector128<float>.One;
                case PowerKind.Identity:
                    return value;
                case PowerKind.Square:
                    return value * value;
                case PowerKind.Cube:
                    return value * value * value;
            }

            if (this.Kind == PowerKind.NonFinite)
            {
                // Infinite powers depend only on whether the base is below, equal to, or above one.
                // A negative infinite exponent reverses zero and infinity. No lane needs scalar power evaluation.
                Vector128<float> below = Vector128.Create(this.Exponent < 0F ? float.PositiveInfinity : 0F);
                Vector128<float> above = Vector128.Create(this.Exponent < 0F ? 0F : float.PositiveInfinity);
                Vector128<float> special = Vector128.ConditionalSelect(Vector128.LessThan(value, Vector128<float>.One), below, above);
                special = Vector128.ConditionalSelect(Vector128.Equals(value, Vector128<float>.One), Vector128<float>.One, special);

                // NaN bases remain NaN. A NaN exponent also returns NaN, except that positive one always returns one.
                special = Vector128.ConditionalSelect(Vector128.Equals(value, value), special, value);
                if (float.IsNaN(this.Exponent))
                {
                    special = Vector128.ConditionalSelect(Vector128.Equals(value, Vector128<float>.One), Vector128<float>.One, Vector128.Create(this.Exponent));
                }

                return special;
            }

            return Vector128_.Pow(value, this.Exponent);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            switch (this.Kind)
            {
                case PowerKind.Zero:
                    return Vector256<float>.One;
                case PowerKind.Identity:
                    return value;
                case PowerKind.Square:
                    return value * value;
                case PowerKind.Cube:
                    return value * value * value;
            }

            if (this.Kind == PowerKind.NonFinite)
            {
                // Infinite powers depend only on whether the base is below, equal to, or above one.
                // A negative infinite exponent reverses zero and infinity. No lane needs scalar power evaluation.
                Vector256<float> below = Vector256.Create(this.Exponent < 0F ? float.PositiveInfinity : 0F);
                Vector256<float> above = Vector256.Create(this.Exponent < 0F ? 0F : float.PositiveInfinity);
                Vector256<float> special = Vector256.ConditionalSelect(Vector256.LessThan(value, Vector256<float>.One), below, above);
                special = Vector256.ConditionalSelect(Vector256.Equals(value, Vector256<float>.One), Vector256<float>.One, special);

                // NaN bases remain NaN. A NaN exponent also returns NaN, except that positive one always returns one.
                special = Vector256.ConditionalSelect(Vector256.Equals(value, value), special, value);
                if (float.IsNaN(this.Exponent))
                {
                    special = Vector256.ConditionalSelect(Vector256.Equals(value, Vector256<float>.One), Vector256<float>.One, Vector256.Create(this.Exponent));
                }

                return special;
            }

            return Vector256_.Pow(value, this.Exponent);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            switch (this.Kind)
            {
                case PowerKind.Zero:
                    return Vector512<float>.One;
                case PowerKind.Identity:
                    return value;
                case PowerKind.Square:
                    return value * value;
                case PowerKind.Cube:
                    return value * value * value;
            }

            if (this.Kind == PowerKind.NonFinite)
            {
                // Infinite powers depend only on whether the base is below, equal to, or above one.
                // A negative infinite exponent reverses zero and infinity. No lane needs scalar power evaluation.
                Vector512<float> below = Vector512.Create(this.Exponent < 0F ? float.PositiveInfinity : 0F);
                Vector512<float> above = Vector512.Create(this.Exponent < 0F ? 0F : float.PositiveInfinity);
                Vector512<float> special = Vector512.ConditionalSelect(Vector512.LessThan(value, Vector512<float>.One), below, above);
                special = Vector512.ConditionalSelect(Vector512.Equals(value, Vector512<float>.One), Vector512<float>.One, special);

                // NaN bases remain NaN. A NaN exponent also returns NaN, except that positive one always returns one.
                special = Vector512.ConditionalSelect(Vector512.Equals(value, value), special, value);
                if (float.IsNaN(this.Exponent))
                {
                    special = Vector512.ConditionalSelect(Vector512.Equals(value, Vector512<float>.One), Vector512<float>.One, Vector512.Create(this.Exponent));
                }

                return special;
            }

            return Vector512_.Pow(value, this.Exponent);
        }
    }
}
