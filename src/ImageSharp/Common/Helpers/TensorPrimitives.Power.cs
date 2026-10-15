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
                : !float.IsFinite(exponent) ? PowerKind.Scalar
                : PowerKind.Finite;

            // These are exponent properties, not input properties. Compute them at curve setup.
            // Register masks apply the resulting sign rules without reclassifying the exponent.
            this.IntegerMask = float.IsInteger(exponent) ? uint.MaxValue : 0;
            this.OddMask = float.IsOddInteger(exponent) ? uint.MaxValue : 0;
            this.ZeroResult = exponent < 0F ? float.PositiveInfinity : 0F;
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
            /// Nonfinite exponents use the scalar IEEE implementation.
            /// </summary>
            Scalar,

            /// <summary>
            /// Finite exponents use SIMD arithmetic and the precomputed sign masks.
            /// </summary>
            Finite
        }

        /// <inheritdoc/>
        public static bool PreserveAlpha => false;

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

        /// <summary>
        /// Gets the magnitude returned for a zero base.
        /// </summary>
        public float ZeroResult { get; }

        /// <inheritdoc/>
        public float Invoke(float value) => (float)Math.Pow(value, this.Exponent);

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
                case PowerKind.Scalar:
                    // Nonfinite exponents require IEEE cases such as 1^NaN = 1.
                    Span<float> lanes = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(
                        ref Unsafe.As<Vector128<float>, float>(ref value),
                        Vector128<float>.Count);

                    for (uint i = 0; i < lanes.Length; i++)
                    {
                        lanes[(int)i] = MathF.Pow(lanes[(int)i], this.Exponent);
                    }

                    return value;
                default:
                    return Numerics.PowFinite(value, in this);
            }
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
                case PowerKind.Scalar:
                    // Nonfinite exponents require IEEE cases such as 1^NaN = 1.
                    Span<float> lanes = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(
                        ref Unsafe.As<Vector256<float>, float>(ref value),
                        Vector256<float>.Count);

                    for (uint i = 0; i < lanes.Length; i++)
                    {
                        lanes[(int)i] = MathF.Pow(lanes[(int)i], this.Exponent);
                    }

                    return value;
                default:
                    return Numerics.PowFinite(value, in this);
            }
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
                case PowerKind.Scalar:
                    // Nonfinite exponents require IEEE cases such as 1^NaN = 1.
                    Span<float> lanes = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(
                        ref Unsafe.As<Vector512<float>, float>(ref value),
                        Vector512<float>.Count);

                    for (uint i = 0; i < lanes.Length; i++)
                    {
                        lanes[(int)i] = MathF.Pow(lanes[(int)i], this.Exponent);
                    }

                    return value;
                default:
                    return Numerics.PowFinite(value, in this);
            }
        }
    }
}
