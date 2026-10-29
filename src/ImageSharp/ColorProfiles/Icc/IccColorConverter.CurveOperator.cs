// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles.Icc;

internal static partial class IccColorConverter
{
    /// <summary>
    /// Evaluates an ICC identity, gamma, parametric or sampled curve across independent color lanes.
    /// </summary>
    internal readonly struct CurveOperator
    {
        private const IccParametricCurveType InvertedFlag = (IccParametricCurveType)(1 << 3);
        private readonly CurveKind kind;
        private readonly IccParametricCurveType type;
        private readonly LutOperator lookup;
        private readonly TensorPrimitives_.PowOperator power;
        private readonly float a;
        private readonly float b;
        private readonly float c;
        private readonly float d;
        private readonly float e;
        private readonly float f;
        private readonly float boundary;

        /// <summary>
        /// Initializes a new instance of the <see cref="CurveOperator"/> struct.
        /// </summary>
        /// <param name="entry">The curve selected from the profile.</param>
        /// <param name="inverted">Whether to evaluate its inverse.</param>
        public CurveOperator(IccTagDataEntry entry, bool inverted)
        {
            this = default;
            switch (entry)
            {
                case IccCurveTagDataEntry sampled:
                    if (sampled.IsIdentityResponse || (sampled.IsGamma && sampled.Gamma == 1F))
                    {
                        this.kind = CurveKind.Identity;
                    }
                    else if (sampled.IsGamma)
                    {
                        this.kind = CurveKind.Gamma;
                        this.power = new(inverted ? 1F / sampled.Gamma : sampled.Gamma);
                    }
                    else
                    {
                        this.kind = CurveKind.Sampled;
                        this.lookup = new LutOperator(sampled.CurveData, inverted);
                    }

                    break;
                case IccParametricCurveTagDataEntry parametric:
                    IccParametricCurve curve = parametric.Curve;

                    // Select common curve forms once. Their SIMD blocks do not need the general parametric switch.
                    this.kind = curve.Type switch
                    {
                        IccParametricCurveType.Type1 when curve.G == 1F => CurveKind.Identity,
                        IccParametricCurveType.Type1 => CurveKind.Gamma,
                        IccParametricCurveType.SRgb => inverted ? CurveKind.InverseSRgb : CurveKind.SRgb,
                        _ => CurveKind.Parametric
                    };

                    this.type = inverted ? curve.Type | InvertedFlag : curve.Type;
                    this.power = new(inverted ? 1F / curve.G : curve.G);
                    this.a = curve.A;
                    this.b = curve.B;
                    this.c = curve.C;
                    this.d = curve.D;
                    this.e = curve.E;
                    this.f = curve.F;

                    // Curve branch boundaries are profile constants. Keep their original float evaluation order.
                    this.boundary = curve.Type switch
                    {
                        IccParametricCurveType.Cie122_1996 or IccParametricCurveType.Iec61966_3 => -curve.B / curve.A,
                        IccParametricCurveType.SRgb when inverted => MathF.Pow((curve.A * curve.D) + curve.B, curve.G),
                        IccParametricCurveType.Type5 when inverted => (curve.C * curve.D) + curve.F,
                        _ => curve.D,
                    };

                    if (inverted && curve.Type == IccParametricCurveType.SRgb && curve.A > 0F && curve.G > 0F && curve.C != 0F)
                    {
                        // Rewrite (y^(1/g) - b) / a as (a^(-g) * y)^(1/g) - b/a.
                        // Both branches then use the forward curve form, with setup-time reciprocals instead of per-lane divisions.
                        // Profile coefficients can overflow the rewritten scale; retain the original formula in that case.
                        float inverseScale = MathF.Pow(curve.A, -curve.G);
                        if (float.IsFinite(inverseScale))
                        {
                            this.kind = CurveKind.SRgb;
                            this.a = inverseScale;
                            this.b = 0F;
                            this.c = 1F / curve.C;
                            this.d = this.boundary;
                            this.e = -curve.B / curve.A;
                        }
                    }

                    break;
                default:
                    throw new InvalidIccProfileException("Invalid ICC curve entry.");
            }
        }

        /// <summary>
        /// Identifies the curve representation selected during profile setup.
        /// </summary>
        private enum CurveKind
        {
            /// <summary>
            /// The curve returns its input unchanged.
            /// </summary>
            Identity,

            /// <summary>
            /// The curve raises its input to a fixed power.
            /// </summary>
            Gamma,

            /// <summary>
            /// The curve interpolates encoded samples.
            /// </summary>
            Sampled,

            /// <summary>
            /// The curve uses the ICC sRGB piecewise formula with the profile's coefficients.
            /// </summary>
            SRgb,

            /// <summary>
            /// The curve uses the inverse ICC sRGB piecewise formula with the profile's coefficients.
            /// </summary>
            InverseSRgb,

            /// <summary>
            /// The curve applies an ICC parametric formula.
            /// </summary>
            Parametric,
        }

        /// <summary>
        /// Selects the normalized sRGB formula when its power can use the finite SIMD kernel.
        /// </summary>
        /// <param name="curve">The formula with the profile's coefficients and exponent.</param>
        /// <returns>Whether the normalized formula has a positive finite exponent other than one.</returns>
        public bool TryGetSRgb(out SRgbCurveOperator curve)
        {
            if (this.kind == CurveKind.SRgb
                && float.IsFinite(this.power.Exponent) && this.power.Exponent != 1F
                && this.power.Exponent > 0F)
            {
                curve = new(this.a, this.b, this.c, this.d, this.e, this.power);
                return true;
            }

            curve = default;
            return false;
        }

        /// <summary>
        /// Gets the configured power when the curve can use the gamma-only SIMD stage.
        /// </summary>
        /// <param name="gamma">The classified forward or inverse power.</param>
        /// <returns>Whether the curve is a positive finite gamma curve.</returns>
        public bool TryGetGamma(out TensorPrimitives_.PowOperator gamma)
        {
            gamma = this.power;
            return this.kind == CurveKind.Gamma && gamma.Kind == TensorPrimitives_.PowOperator.PowerKind.Finite && gamma.Exponent > 0F;
        }

        /// <summary>
        /// Converts one curve input using the same branches and coefficients as the SIMD implementations.
        /// </summary>
        /// <param name="value">The input component.</param>
        /// <returns>The curve output.</returns>
        public float Convert(float value)
        {
            switch (this.kind)
            {
                case CurveKind.Identity:
                    return value;
                case CurveKind.Gamma:
                    return this.Pow(value);
                case CurveKind.Sampled:
                    return this.lookup.Convert(value);
                case CurveKind.SRgb:
                    return this.ConvertSRgb(value);
                case CurveKind.InverseSRgb:
                    return value >= this.boundary ? (this.Pow(value) - this.b) / this.a : value / this.c;
            }

            return this.type switch
            {
                IccParametricCurveType.Type1 or IccParametricCurveType.Type1 | InvertedFlag => this.Pow(value),
                IccParametricCurveType.Cie122_1996 => value >= this.boundary ? this.Pow((this.a * value) + this.b) : 0F,
                IccParametricCurveType.Iec61966_3 => value >= this.boundary ? this.Pow((this.a * value) + this.b) + this.c : this.c,
                IccParametricCurveType.Type5 => value >= this.d ? this.Pow((this.a * value) + this.b) + this.e : (this.c * value) + this.f,
                IccParametricCurveType.Cie122_1996 | InvertedFlag => (this.Pow(value) - this.b) / this.a,
                IccParametricCurveType.Iec61966_3 | InvertedFlag => value >= this.c ? (this.Pow(value - this.c) - this.b) / this.a : -this.b / this.a,
                IccParametricCurveType.Type5 | InvertedFlag => value >= this.boundary
                    ? (this.Pow(value - this.e) - this.b) / this.a
                    : (value - this.f) / this.c,
                _ => throw new InvalidIccProfileException("Invalid ICC parametric curve type."),
            };
        }

        /// <summary>
        /// Converts 4 independent component values with the configured curve.
        /// </summary>
        /// <param name="value">The input component values.</param>
        /// <returns>The curve outputs in the same lane order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Convert(Vector128<float> value)
        {
            switch (this.kind)
            {
                case CurveKind.Identity:
                    return value;
                case CurveKind.Gamma:
                    return this.Pow(value);
                case CurveKind.Sampled:
                    return this.lookup.Convert(value);
                case CurveKind.SRgb:
                    return this.ConvertSRgb(value);

                case CurveKind.InverseSRgb:
                {
                    // These are the same inverse branches as scalar conversion, using the profile's stored coefficients.
                    Vector128<float> active = Vector128.GreaterThanOrEqual(value, Vector128.Create(this.boundary));
                    Vector128<float> input = Vector128.ConditionalSelect(active, value, Vector128<float>.One);
                    Vector128<float> power = (this.Pow(input) - Vector128.Create(this.b)) / Vector128.Create(this.a);
                    return Vector128.ConditionalSelect(active, power, value / Vector128.Create(this.c));
                }
            }

            return this.ConvertParametric(value);
        }

        /// <summary>
        /// Evaluates the configured parametric formula without copying every branch into each channel's row loop.
        /// </summary>
        /// <param name="value">The curve inputs in independent lanes.</param>
        /// <returns>The curve outputs in the original lane order.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private Vector128<float> ConvertParametric(Vector128<float> value)
        {
            // Keep the general formula switch out of the identity, gamma, sampled and sRGB conversion methods.
            switch (this.type)
            {
                case IccParametricCurveType.Type1:
                case IccParametricCurveType.Type1 | InvertedFlag:
                    return this.Pow(value);
                case IccParametricCurveType.Cie122_1996:
                case IccParametricCurveType.Iec61966_3:
                {
                    Vector128<float> active = Vector128.GreaterThanOrEqual(value, Vector128.Create(this.boundary));
                    Vector128<float> input = (Vector128.Create(this.a) * value) + Vector128.Create(this.b);

                    // Do not evaluate an inactive power branch on a negative base. The scalar curve takes only one branch.
                    input = Vector128.ConditionalSelect(active, input, Vector128<float>.One);
                    Vector128<float> power = this.Pow(input);
                    if (this.type == IccParametricCurveType.Iec61966_3)
                    {
                        power += Vector128.Create(this.c);
                        return Vector128.ConditionalSelect(active, power, Vector128.Create(this.c));
                    }

                    return Vector128.ConditionalSelect(active, power, Vector128<float>.Zero);
                }

                case IccParametricCurveType.SRgb:
                case IccParametricCurveType.Type5:
                {
                    Vector128<float> active = Vector128.GreaterThanOrEqual(value, Vector128.Create(this.d));
                    Vector128<float> input = (Vector128.Create(this.a) * value) + Vector128.Create(this.b);
                    input = Vector128.ConditionalSelect(active, input, Vector128<float>.One);
                    Vector128<float> power = this.Pow(input);
                    Vector128<float> linear = Vector128.Create(this.c) * value;
                    if (this.type == IccParametricCurveType.Type5)
                    {
                        power += Vector128.Create(this.e);
                        linear += Vector128.Create(this.f);
                    }

                    return Vector128.ConditionalSelect(active, power, linear);
                }

                case IccParametricCurveType.Cie122_1996 | InvertedFlag:
                    return (this.Pow(value) - Vector128.Create(this.b)) / Vector128.Create(this.a);
                case IccParametricCurveType.Iec61966_3 | InvertedFlag:
                {
                    Vector128<float> active = Vector128.GreaterThanOrEqual(value, Vector128.Create(this.c));
                    Vector128<float> input = Vector128.ConditionalSelect(active, value - Vector128.Create(this.c), Vector128<float>.One);
                    Vector128<float> power = (this.Pow(input) - Vector128.Create(this.b)) / Vector128.Create(this.a);
                    return Vector128.ConditionalSelect(active, power, Vector128.Create(-this.b / this.a));
                }

                case IccParametricCurveType.Type5 | InvertedFlag:
                {
                    Vector128<float> active = Vector128.GreaterThanOrEqual(value, Vector128.Create(this.boundary));
                    Vector128<float> input = Vector128.ConditionalSelect(active, value - Vector128.Create(this.e), Vector128<float>.One);
                    Vector128<float> power = (this.Pow(input) - Vector128.Create(this.b)) / Vector128.Create(this.a);
                    Vector128<float> linear = (value - Vector128.Create(this.f)) / Vector128.Create(this.c);
                    return Vector128.ConditionalSelect(active, power, linear);
                }

                default:
                    throw new InvalidIccProfileException("Invalid ICC parametric curve type.");
            }
        }

        /// <summary>
        /// Converts 8 independent component values with the configured curve.
        /// </summary>
        /// <param name="value">The input component values.</param>
        /// <returns>The curve outputs in the same lane order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Convert(Vector256<float> value)
        {
            switch (this.kind)
            {
                case CurveKind.Identity:
                    return value;
                case CurveKind.Gamma:
                    return this.Pow(value);
                case CurveKind.Sampled:
                    return this.lookup.Convert(value);
                case CurveKind.SRgb:
                    return this.ConvertSRgb(value);

                case CurveKind.InverseSRgb:
                {
                    // These are the same inverse branches as scalar conversion, using the profile's stored coefficients.
                    Vector256<float> active = Vector256.GreaterThanOrEqual(value, Vector256.Create(this.boundary));
                    Vector256<float> input = Vector256.ConditionalSelect(active, value, Vector256<float>.One);
                    Vector256<float> power = (this.Pow(input) - Vector256.Create(this.b)) / Vector256.Create(this.a);
                    return Vector256.ConditionalSelect(active, power, value / Vector256.Create(this.c));
                }
            }

            return this.ConvertParametric(value);
        }

        /// <summary>
        /// Evaluates the configured parametric formula without copying every branch into each channel's row loop.
        /// </summary>
        /// <param name="value">The curve inputs in independent lanes.</param>
        /// <returns>The curve outputs in the original lane order.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private Vector256<float> ConvertParametric(Vector256<float> value)
        {
            // Keep the general formula switch out of the identity, gamma, sampled and sRGB conversion methods.
            switch (this.type)
            {
                case IccParametricCurveType.Type1:
                case IccParametricCurveType.Type1 | InvertedFlag:
                    return this.Pow(value);
                case IccParametricCurveType.Cie122_1996:
                case IccParametricCurveType.Iec61966_3:
                {
                    Vector256<float> active = Vector256.GreaterThanOrEqual(value, Vector256.Create(this.boundary));
                    Vector256<float> input = (Vector256.Create(this.a) * value) + Vector256.Create(this.b);

                    // Do not evaluate an inactive power branch on a negative base. The scalar curve takes only one branch.
                    input = Vector256.ConditionalSelect(active, input, Vector256<float>.One);
                    Vector256<float> power = this.Pow(input);
                    if (this.type == IccParametricCurveType.Iec61966_3)
                    {
                        power += Vector256.Create(this.c);
                        return Vector256.ConditionalSelect(active, power, Vector256.Create(this.c));
                    }

                    return Vector256.ConditionalSelect(active, power, Vector256<float>.Zero);
                }

                case IccParametricCurveType.SRgb:
                case IccParametricCurveType.Type5:
                {
                    Vector256<float> active = Vector256.GreaterThanOrEqual(value, Vector256.Create(this.d));
                    Vector256<float> input = (Vector256.Create(this.a) * value) + Vector256.Create(this.b);
                    input = Vector256.ConditionalSelect(active, input, Vector256<float>.One);
                    Vector256<float> power = this.Pow(input);
                    Vector256<float> linear = Vector256.Create(this.c) * value;
                    if (this.type == IccParametricCurveType.Type5)
                    {
                        power += Vector256.Create(this.e);
                        linear += Vector256.Create(this.f);
                    }

                    return Vector256.ConditionalSelect(active, power, linear);
                }

                case IccParametricCurveType.Cie122_1996 | InvertedFlag:
                    return (this.Pow(value) - Vector256.Create(this.b)) / Vector256.Create(this.a);
                case IccParametricCurveType.Iec61966_3 | InvertedFlag:
                {
                    Vector256<float> active = Vector256.GreaterThanOrEqual(value, Vector256.Create(this.c));
                    Vector256<float> input = Vector256.ConditionalSelect(active, value - Vector256.Create(this.c), Vector256<float>.One);
                    Vector256<float> power = (this.Pow(input) - Vector256.Create(this.b)) / Vector256.Create(this.a);
                    return Vector256.ConditionalSelect(active, power, Vector256.Create(-this.b / this.a));
                }

                case IccParametricCurveType.Type5 | InvertedFlag:
                {
                    Vector256<float> active = Vector256.GreaterThanOrEqual(value, Vector256.Create(this.boundary));
                    Vector256<float> input = Vector256.ConditionalSelect(active, value - Vector256.Create(this.e), Vector256<float>.One);
                    Vector256<float> power = (this.Pow(input) - Vector256.Create(this.b)) / Vector256.Create(this.a);
                    Vector256<float> linear = (value - Vector256.Create(this.f)) / Vector256.Create(this.c);
                    return Vector256.ConditionalSelect(active, power, linear);
                }

                default:
                    throw new InvalidIccProfileException("Invalid ICC parametric curve type.");
            }
        }

        /// <summary>
        /// Converts 16 independent component values with the configured curve.
        /// </summary>
        /// <param name="value">The input component values.</param>
        /// <returns>The curve outputs in the same lane order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Convert(Vector512<float> value)
        {
            switch (this.kind)
            {
                case CurveKind.Identity:
                    return value;
                case CurveKind.Gamma:
                    return this.Pow(value);
                case CurveKind.Sampled:
                    return this.lookup.Convert(value);
                case CurveKind.SRgb:
                    return this.ConvertSRgb(value);

                case CurveKind.InverseSRgb:
                {
                    // These are the same inverse branches as scalar conversion, using the profile's stored coefficients.
                    Vector512<float> active = Vector512.GreaterThanOrEqual(value, Vector512.Create(this.boundary));
                    Vector512<float> input = Vector512.ConditionalSelect(active, value, Vector512<float>.One);
                    Vector512<float> power = (this.Pow(input) - Vector512.Create(this.b)) / Vector512.Create(this.a);
                    return Vector512.ConditionalSelect(active, power, value / Vector512.Create(this.c));
                }
            }

            return this.ConvertParametric(value);
        }

        /// <summary>
        /// Evaluates the configured parametric formula without copying every branch into each channel's row loop.
        /// </summary>
        /// <param name="value">The curve inputs in independent lanes.</param>
        /// <returns>The curve outputs in the original lane order.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private Vector512<float> ConvertParametric(Vector512<float> value)
        {
            // Keep the general formula switch out of the identity, gamma, sampled and sRGB conversion methods.
            switch (this.type)
            {
                case IccParametricCurveType.Type1:
                case IccParametricCurveType.Type1 | InvertedFlag:
                    return this.Pow(value);
                case IccParametricCurveType.Cie122_1996:
                case IccParametricCurveType.Iec61966_3:
                {
                    Vector512<float> active = Vector512.GreaterThanOrEqual(value, Vector512.Create(this.boundary));
                    Vector512<float> input = (Vector512.Create(this.a) * value) + Vector512.Create(this.b);

                    // Do not evaluate an inactive power branch on a negative base. The scalar curve takes only one branch.
                    input = Vector512.ConditionalSelect(active, input, Vector512<float>.One);
                    Vector512<float> power = this.Pow(input);
                    if (this.type == IccParametricCurveType.Iec61966_3)
                    {
                        power += Vector512.Create(this.c);
                        return Vector512.ConditionalSelect(active, power, Vector512.Create(this.c));
                    }

                    return Vector512.ConditionalSelect(active, power, Vector512<float>.Zero);
                }

                case IccParametricCurveType.SRgb:
                case IccParametricCurveType.Type5:
                {
                    Vector512<float> active = Vector512.GreaterThanOrEqual(value, Vector512.Create(this.d));
                    Vector512<float> input = (Vector512.Create(this.a) * value) + Vector512.Create(this.b);
                    input = Vector512.ConditionalSelect(active, input, Vector512<float>.One);
                    Vector512<float> power = this.Pow(input);
                    Vector512<float> linear = Vector512.Create(this.c) * value;
                    if (this.type == IccParametricCurveType.Type5)
                    {
                        power += Vector512.Create(this.e);
                        linear += Vector512.Create(this.f);
                    }

                    return Vector512.ConditionalSelect(active, power, linear);
                }

                case IccParametricCurveType.Cie122_1996 | InvertedFlag:
                    return (this.Pow(value) - Vector512.Create(this.b)) / Vector512.Create(this.a);
                case IccParametricCurveType.Iec61966_3 | InvertedFlag:
                {
                    Vector512<float> active = Vector512.GreaterThanOrEqual(value, Vector512.Create(this.c));
                    Vector512<float> input = Vector512.ConditionalSelect(active, value - Vector512.Create(this.c), Vector512<float>.One);
                    Vector512<float> power = (this.Pow(input) - Vector512.Create(this.b)) / Vector512.Create(this.a);
                    return Vector512.ConditionalSelect(active, power, Vector512.Create(-this.b / this.a));
                }

                case IccParametricCurveType.Type5 | InvertedFlag:
                {
                    Vector512<float> active = Vector512.GreaterThanOrEqual(value, Vector512.Create(this.boundary));
                    Vector512<float> input = Vector512.ConditionalSelect(active, value - Vector512.Create(this.e), Vector512<float>.One);
                    Vector512<float> power = (this.Pow(input) - Vector512.Create(this.b)) / Vector512.Create(this.a);
                    Vector512<float> linear = (value - Vector512.Create(this.f)) / Vector512.Create(this.c);
                    return Vector512.ConditionalSelect(active, power, linear);
                }

                default:
                    throw new InvalidIccProfileException("Invalid ICC parametric curve type.");
            }
        }

        /// <summary>
        /// Applies the normalized sRGB formula selected during profile setup.
        /// </summary>
        /// <param name="value">The input component.</param>
        /// <returns>The curve result using the profile's own coefficients.</returns>
        public float ConvertSRgb(float value)
            => value >= this.d ? this.Pow((this.a * value) + this.b) + this.e : this.c * value;

        /// <summary>
        /// Applies the selected sRGB formula to 4 independent components without curve-kind dispatch.
        /// </summary>
        /// <param name="value">The input component lanes.</param>
        /// <returns>The curve results using the profile's own coefficients.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> ConvertSRgb(Vector128<float> value)
        {
            // Inactive lanes use a safe power base; the final selection keeps their linear result.
            Vector128<float> active = Vector128.GreaterThanOrEqual(value, Vector128.Create(this.d));
            Vector128<float> input = (Vector128.Create(this.a) * value) + Vector128.Create(this.b);
            input = Vector128.ConditionalSelect(active, input, Vector128<float>.One);
            Vector128<float> power = this.Pow(input);
            return Vector128.ConditionalSelect(active, power + Vector128.Create(this.e), Vector128.Create(this.c) * value);
        }

        /// <summary>
        /// Applies the selected sRGB formula to 8 independent components without curve-kind dispatch.
        /// </summary>
        /// <param name="value">The input component lanes.</param>
        /// <returns>The curve results using the profile's own coefficients.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> ConvertSRgb(Vector256<float> value)
        {
            // Inactive lanes use a safe power base; the final selection keeps their linear result.
            Vector256<float> active = Vector256.GreaterThanOrEqual(value, Vector256.Create(this.d));
            Vector256<float> input = (Vector256.Create(this.a) * value) + Vector256.Create(this.b);
            input = Vector256.ConditionalSelect(active, input, Vector256<float>.One);
            Vector256<float> power = this.Pow(input);
            return Vector256.ConditionalSelect(active, power + Vector256.Create(this.e), Vector256.Create(this.c) * value);
        }

        /// <summary>
        /// Applies the selected sRGB formula to 16 independent components without curve-kind dispatch.
        /// </summary>
        /// <param name="value">The input component lanes.</param>
        /// <returns>The curve results using the profile's own coefficients.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> ConvertSRgb(Vector512<float> value)
        {
            // Inactive lanes use a safe power base; the final selection keeps their linear result.
            Vector512<float> active = Vector512.GreaterThanOrEqual(value, Vector512.Create(this.d));
            Vector512<float> input = (Vector512.Create(this.a) * value) + Vector512.Create(this.b);
            input = Vector512.ConditionalSelect(active, input, Vector512<float>.One);
            Vector512<float> power = this.Pow(input);
            return Vector512.ConditionalSelect(active, power + Vector512.Create(this.e), Vector512.Create(this.c) * value);
        }

        /// <summary>
        /// Evaluates an ICC power curve after limiting negative device values to zero.
        /// </summary>
        /// <param name="value">The power base.</param>
        /// <returns>The curve power.</returns>
        private float Pow(float value)
        {
            // LittleCMS applies this lower bound to ICC gamma curves in both directions. A target matrix can produce
            // negative values outside the device gamut. Keep values above one; this is not SDR companding.
            return this.power.Invoke(value < 0F ? 0F : value);
        }

        /// <summary>
        /// Applies the ICC power curve to independent component lanes, with the same lower bound as scalar conversion.
        /// </summary>
        /// <param name="value">The input component values.</param>
        /// <returns>The curve powers, retaining values above one.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector128<float> Pow(Vector128<float> value)
        {
            value = Vector128.ConditionalSelect(Vector128.LessThan(value, Vector128<float>.Zero), Vector128<float>.Zero, value);

            // ICC bounds supply nonnegative bases; the shared power operator owns exponent classification and IEEE rules.
            return this.power.Invoke(value);
        }

        /// <summary>
        /// Applies the ICC power curve to independent component lanes, with the same lower bound as scalar conversion.
        /// </summary>
        /// <param name="value">The input component values.</param>
        /// <returns>The curve powers, retaining values above one.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector256<float> Pow(Vector256<float> value)
        {
            value = Vector256.ConditionalSelect(Vector256.LessThan(value, Vector256<float>.Zero), Vector256<float>.Zero, value);

            // ICC bounds supply nonnegative bases; the shared power operator owns exponent classification and IEEE rules.
            return this.power.Invoke(value);
        }

        /// <summary>
        /// Applies the ICC power curve to independent component lanes, with the same lower bound as scalar conversion.
        /// </summary>
        /// <param name="value">The input component values.</param>
        /// <returns>The curve powers, retaining values above one.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector512<float> Pow(Vector512<float> value)
        {
            value = Vector512.ConditionalSelect(Vector512.LessThan(value, Vector512<float>.Zero), Vector512<float>.Zero, value);

            // ICC bounds supply nonnegative bases; the shared power operator owns exponent classification and IEEE rules.
            return this.power.Invoke(value);
        }
    }
}
