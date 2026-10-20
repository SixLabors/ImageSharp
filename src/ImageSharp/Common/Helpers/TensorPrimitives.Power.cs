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
    /// <param name="values">The power bases.</param>
    /// <param name="exponent">The exponent shared by all components.</param>
    public static void Pow(Span<float> values, float exponent) => Invoke(values, new PowOperator(exponent));

    /// <summary>
    /// Classifies the exponent once and retains the IEEE power rules for every component.
    /// </summary>
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
                : !float.IsFinite(exponent) ? PowerKind.NonFinite
                : PowerKind.Finite;

            // These are exponent properties, not input properties. Compute them at curve setup.
            // Register masks apply the resulting sign rules without reclassifying the exponent.
            this.IntegerMask = float.IsInteger(exponent) ? uint.MaxValue : 0;
            this.OddMask = float.IsOddInteger(exponent) ? uint.MaxValue : 0;
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
            /// Nonfinite exponents use IEEE comparisons without evaluating a logarithm.
            /// </summary>
            NonFinite,

            /// <summary>
            /// Finite exponents use SIMD arithmetic and the precomputed sign masks.
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

        /// <summary>
        /// Gets an all-bits-set mask when negative finite bases have a real result.
        /// </summary>
        public uint IntegerMask { get; }

        /// <summary>
        /// Gets an all-bits-set mask when the result retains the base sign.
        /// </summary>
        public uint OddMask { get; }

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
                case PowerKind.NonFinite:
                    return MathF.Pow(value, this.Exponent);
            }

            // Use the SIMD approximation in scalar remainders too. A pixel's result must not depend on row length or SIMD support.
            float magnitude = MathF.Abs(value);
            float result = ApproximatePower(magnitude, this.Exponent);
            if (this.OddMask != 0)
            {
                result = MathF.CopySign(result, value);
            }

            return value < 0F && float.IsFinite(magnitude) && this.IntegerMask == 0 ? float.NaN : result;
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

            if (value == 0F || float.IsPositiveInfinity(value))
            {
                return exponent < 0F ? 1F / value : value;
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
            }

            if (this.Kind == PowerKind.NonFinite)
            {
                // Infinite powers depend only on whether the magnitude is below, equal to, or above one.
                // A negative infinite exponent reverses zero and infinity. No lane needs scalar power evaluation.
                Vector128<float> absolute = Vector128.Abs(value);
                Vector128<float> below = Vector128.Create(this.Exponent < 0F ? float.PositiveInfinity : 0F);
                Vector128<float> above = Vector128.Create(this.Exponent < 0F ? 0F : float.PositiveInfinity);
                Vector128<float> special = Vector128.ConditionalSelect(Vector128.LessThan(absolute, Vector128<float>.One), below, above);
                special = Vector128.ConditionalSelect(Vector128.Equals(absolute, Vector128<float>.One), Vector128<float>.One, special);

                // NaN bases remain NaN. A NaN exponent also returns NaN, except that positive one always returns one.
                special = Vector128.ConditionalSelect(Vector128.Equals(value, value), special, value);
                if (float.IsNaN(this.Exponent))
                {
                    special = Vector128.ConditionalSelect(Vector128.Equals(value, Vector128<float>.One), Vector128<float>.One, Vector128.Create(this.Exponent));
                }

                return special;
            }

            // Use one magnitude calculation for all finite exponents. Per-lane masks restore zero and sign rules,
            // so mixed inputs need no second copy of the log2/exp2 kernel or whole-vector sign test.
            Vector128<float> magnitude = Vector128.Abs(value);
            Vector128<float> result = Vector128_.Pow(magnitude, this.Exponent);

            // Odd integer powers retain the base sign. Fractional powers reject finite negative bases.
            Vector128<float> oddMask = Vector128.Create(this.OddMask).AsSingle();
            result = Vector128.ConditionalSelect(oddMask, Vector128.CopySign(result, value), result);
            Vector128<float> negativeFinite = Vector128.LessThan(value, Vector128<float>.Zero)
                & Vector128.LessThan(magnitude, Vector128.Create(float.PositiveInfinity));
            negativeFinite &= ~Vector128.Create(this.IntegerMask).AsSingle();
            return Vector128.ConditionalSelect(negativeFinite, Vector128.Create(float.NaN), result);
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
            }

            if (this.Kind == PowerKind.NonFinite)
            {
                // Infinite powers depend only on whether the magnitude is below, equal to, or above one.
                // A negative infinite exponent reverses zero and infinity. No lane needs scalar power evaluation.
                Vector256<float> absolute = Vector256.Abs(value);
                Vector256<float> below = Vector256.Create(this.Exponent < 0F ? float.PositiveInfinity : 0F);
                Vector256<float> above = Vector256.Create(this.Exponent < 0F ? 0F : float.PositiveInfinity);
                Vector256<float> special = Vector256.ConditionalSelect(Vector256.LessThan(absolute, Vector256<float>.One), below, above);
                special = Vector256.ConditionalSelect(Vector256.Equals(absolute, Vector256<float>.One), Vector256<float>.One, special);

                // NaN bases remain NaN. A NaN exponent also returns NaN, except that positive one always returns one.
                special = Vector256.ConditionalSelect(Vector256.Equals(value, value), special, value);
                if (float.IsNaN(this.Exponent))
                {
                    special = Vector256.ConditionalSelect(Vector256.Equals(value, Vector256<float>.One), Vector256<float>.One, Vector256.Create(this.Exponent));
                }

                return special;
            }

            // Use one magnitude calculation for all finite exponents. Per-lane masks restore zero and sign rules,
            // so mixed inputs need no second copy of the log2/exp2 kernel or whole-vector sign test.
            Vector256<float> magnitude = Vector256.Abs(value);
            Vector256<float> result = Vector256_.Pow(magnitude, this.Exponent);

            // Odd integer powers retain the base sign. Fractional powers reject finite negative bases.
            Vector256<float> oddMask = Vector256.Create(this.OddMask).AsSingle();
            result = Vector256.ConditionalSelect(oddMask, Vector256.CopySign(result, value), result);
            Vector256<float> negativeFinite = Vector256.LessThan(value, Vector256<float>.Zero)
                & Vector256.LessThan(magnitude, Vector256.Create(float.PositiveInfinity));
            negativeFinite &= ~Vector256.Create(this.IntegerMask).AsSingle();
            return Vector256.ConditionalSelect(negativeFinite, Vector256.Create(float.NaN), result);
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
            }

            if (this.Kind == PowerKind.NonFinite)
            {
                // Infinite powers depend only on whether the magnitude is below, equal to, or above one.
                // A negative infinite exponent reverses zero and infinity. No lane needs scalar power evaluation.
                Vector512<float> absolute = Vector512.Abs(value);
                Vector512<float> below = Vector512.Create(this.Exponent < 0F ? float.PositiveInfinity : 0F);
                Vector512<float> above = Vector512.Create(this.Exponent < 0F ? 0F : float.PositiveInfinity);
                Vector512<float> special = Vector512.ConditionalSelect(Vector512.LessThan(absolute, Vector512<float>.One), below, above);
                special = Vector512.ConditionalSelect(Vector512.Equals(absolute, Vector512<float>.One), Vector512<float>.One, special);

                // NaN bases remain NaN. A NaN exponent also returns NaN, except that positive one always returns one.
                special = Vector512.ConditionalSelect(Vector512.Equals(value, value), special, value);
                if (float.IsNaN(this.Exponent))
                {
                    special = Vector512.ConditionalSelect(Vector512.Equals(value, Vector512<float>.One), Vector512<float>.One, Vector512.Create(this.Exponent));
                }

                return special;
            }

            // Use one magnitude calculation for all finite exponents. Per-lane masks restore zero and sign rules,
            // so mixed inputs need no second copy of the log2/exp2 kernel or whole-vector sign test.
            Vector512<float> magnitude = Vector512.Abs(value);
            Vector512<float> result = Vector512_.Pow(magnitude, this.Exponent);

            // Odd integer powers retain the base sign. Fractional powers reject finite negative bases.
            Vector512<float> oddMask = Vector512.Create(this.OddMask).AsSingle();
            result = Vector512.ConditionalSelect(oddMask, Vector512.CopySign(result, value), result);
            Vector512<float> negativeFinite = Vector512.LessThan(value, Vector512<float>.Zero)
                & Vector512.LessThan(magnitude, Vector512.Create(float.PositiveInfinity));
            negativeFinite &= ~Vector512.Create(this.IntegerMask).AsSingle();
            return Vector512.ConditionalSelect(negativeFinite, Vector512.Create(float.NaN), result);
        }
    }
}
