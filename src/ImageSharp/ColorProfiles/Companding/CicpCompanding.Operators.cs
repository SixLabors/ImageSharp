// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using static SixLabors.ImageSharp.ColorProfiles.Companding.CompandingVectorOperations;

namespace SixLabors.ImageSharp.ColorProfiles.Companding;

/// <content>
/// Provides scalar and SIMD operators for each H.273 transfer family.
/// </content>
internal static partial class CicpCompanding
{
    /// <summary>
    /// Converts base-ten logarithms and powers to natural logarithms and exponentials.
    /// </summary>
    private const float NaturalLogOfTen = 2.302585092994046F;

    /// <summary>
    /// Defines the two directions of one transfer curve for scalar and independent SIMD components.
    /// </summary>
    private interface ITransferCurve
    {
        /// <summary>
        /// Decodes one component.
        /// </summary>
        /// <param name="value">The input component.</param>
        /// <returns>The converted component.</returns>
        public static abstract float Expand(float value);

        /// <summary>
        /// Decodes independent SIMD component lanes.
        /// </summary>
        /// <typeparam name="TVector">The register type.</typeparam>
        /// <typeparam name="TOperations">The register operations.</typeparam>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        public static abstract TVector Expand<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>;

        /// <summary>
        /// Encodes one component.
        /// </summary>
        /// <param name="value">The input component.</param>
        /// <returns>The converted component.</returns>
        public static abstract float Compress(float value);

        /// <summary>
        /// Encodes independent SIMD component lanes.
        /// </summary>
        /// <typeparam name="TVector">The register type.</typeparam>
        /// <typeparam name="TOperations">The register operations.</typeparam>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        public static abstract TVector Compress<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>;
    }

    /// <summary>
    /// Converts nonlinear signal values to their H.273 linear-domain values using the selected SIMD width.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The corresponding linear-domain values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TVector ToLinear<TVector, TOperations>(CicpTransferCharacteristics transferCharacteristics, TVector value)
        where TVector : struct
        where TOperations : struct, ITransferVectorOperations<TVector>
    {
        switch (transferCharacteristics)
        {
            case CicpTransferCharacteristics.ItuRBt709_6:
            case CicpTransferCharacteristics.ItuRBt601_7:
            case CicpTransferCharacteristics.ItuRBt2020_2_10bit:
            case CicpTransferCharacteristics.ItuRBt2020_2_12bit:
                return Bt709Curve.Expand<TVector, TOperations>(value);
            case CicpTransferCharacteristics.Gamma2_2:
                return Gamma22Curve.Expand<TVector, TOperations>(value);
            case CicpTransferCharacteristics.Gamma2_8:
                return Gamma28Curve.Expand<TVector, TOperations>(value);
            case CicpTransferCharacteristics.SmpteSt240:
                return Smpte240Curve.Expand<TVector, TOperations>(value);
            case CicpTransferCharacteristics.Linear:
                return LinearCurve.Expand<TVector, TOperations>(value);
            case CicpTransferCharacteristics.Log100:
                return Log100Curve.Expand<TVector, TOperations>(value);
            case CicpTransferCharacteristics.Log100Sqrt:
                return Log100SqrtCurve.Expand<TVector, TOperations>(value);
            case CicpTransferCharacteristics.Iec61966_2_4:
                return Iec61966Curve.Expand<TVector, TOperations>(value);
            case CicpTransferCharacteristics.ItuRBt1361_0:
                return Bt1361Curve.Expand<TVector, TOperations>(value);
            case CicpTransferCharacteristics.Iec61966_2_1:
                return SrgbCurve.Expand<TVector, TOperations>(value);
            case CicpTransferCharacteristics.SmpteSt2084:
                return PqCurve.Expand<TVector, TOperations>(value);
            case CicpTransferCharacteristics.SmpteSt428_1:
                return Smpte428Curve.Expand<TVector, TOperations>(value);
            case CicpTransferCharacteristics.AribStdB67:
                return HlgCurve.Expand<TVector, TOperations>(value);
            default:
                return Bt709Curve.Expand<TVector, TOperations>(value);
        }
    }

    /// <summary>
    /// Converts linear signal values to their H.273 nonlinear-domain values using the selected SIMD width.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The linear signal values.</param>
    /// <returns>The corresponding nonlinear-domain values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TVector ToGamma<TVector, TOperations>(CicpTransferCharacteristics transferCharacteristics, TVector value)
        where TVector : struct
        where TOperations : struct, ITransferVectorOperations<TVector>
    {
        switch (transferCharacteristics)
        {
            case CicpTransferCharacteristics.ItuRBt709_6:
            case CicpTransferCharacteristics.ItuRBt601_7:
            case CicpTransferCharacteristics.ItuRBt2020_2_10bit:
            case CicpTransferCharacteristics.ItuRBt2020_2_12bit:
                return Bt709Curve.Compress<TVector, TOperations>(value);
            case CicpTransferCharacteristics.Gamma2_2:
                return Gamma22Curve.Compress<TVector, TOperations>(value);
            case CicpTransferCharacteristics.Gamma2_8:
                return Gamma28Curve.Compress<TVector, TOperations>(value);
            case CicpTransferCharacteristics.SmpteSt240:
                return Smpte240Curve.Compress<TVector, TOperations>(value);
            case CicpTransferCharacteristics.Linear:
                return LinearCurve.Compress<TVector, TOperations>(value);
            case CicpTransferCharacteristics.Log100:
                return Log100Curve.Compress<TVector, TOperations>(value);
            case CicpTransferCharacteristics.Log100Sqrt:
                return Log100SqrtCurve.Compress<TVector, TOperations>(value);
            case CicpTransferCharacteristics.Iec61966_2_4:
                return Iec61966Curve.Compress<TVector, TOperations>(value);
            case CicpTransferCharacteristics.ItuRBt1361_0:
                return Bt1361Curve.Compress<TVector, TOperations>(value);
            case CicpTransferCharacteristics.Iec61966_2_1:
                return SrgbCurve.Compress<TVector, TOperations>(value);
            case CicpTransferCharacteristics.SmpteSt2084:
                return PqCurve.Compress<TVector, TOperations>(value);
            case CicpTransferCharacteristics.SmpteSt428_1:
                return Smpte428Curve.Compress<TVector, TOperations>(value);
            case CicpTransferCharacteristics.AribStdB67:
                return HlgCurve.Compress<TVector, TOperations>(value);
            default:
                return Bt709Curve.Compress<TVector, TOperations>(value);
        }
    }

    /// <summary>
    /// Applies one CICP curve to independent components at every supported width.
    /// </summary>
    /// <typeparam name="TCurve">The characteristic selected before traversal.</typeparam>
    private readonly struct ExpandOperator<TCurve> : TensorPrimitives_.IUnaryOperator
        where TCurve : struct, ITransferCurve
    {
        /// <inheritdoc/>
        public static bool PreserveAlpha => true;

        /// <inheritdoc/>
        public float Invoke(float value) => TCurve.Expand(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
            => TCurve.Expand<Vector128<float>, Vector128Operations>(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
            => TCurve.Expand<Vector256<float>, Vector256Operations>(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
            => TCurve.Expand<Vector512<float>, Vector512Operations>(value);
    }

    /// <summary>
    /// Applies one CICP curve to independent components at every supported width.
    /// </summary>
    /// <typeparam name="TCurve">The characteristic selected before traversal.</typeparam>
    private readonly struct CompressOperator<TCurve> : TensorPrimitives_.IUnaryOperator
        where TCurve : struct, ITransferCurve
    {
        /// <inheritdoc/>
        public static bool PreserveAlpha => true;

        /// <inheritdoc/>
        public float Invoke(float value) => TCurve.Compress(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
            => TCurve.Compress<Vector128<float>, Vector128Operations>(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
            => TCurve.Compress<Vector256<float>, Vector256Operations>(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
            => TCurve.Compress<Vector512<float>, Vector512Operations>(value);
    }

    /// <summary>
    /// Applies the ItuRBt709_6 transfer curve with its specified coefficients and range rules.
    /// </summary>
    private readonly struct Bt709Curve : ITransferCurve
    {
        /// <summary>
        /// Applies the inverse BT.709-family opto-electronic transfer function.
        /// </summary>
        /// <param name="value">The nonlinear signal value.</param>
        /// <returns>The linear signal value.</returns>
        public static float Expand(float value)
        {
            if (value < 0F)
            {
                return 0F;
            }

            if (value < Rec709Companding.LinearSlope * Bt709Beta)
            {
                return value / Rec709Companding.LinearSlope;
            }

            return value < 1F
                ? new Rec709Companding.ExpandOperator(Bt709Alpha, Bt709Alpha - 1F, Rec709Companding.LinearSlope * Bt709Beta).Invoke(value)
                : 1F;
        }

        /// <summary>
        /// Applies the inverse BT.709-family opto-electronic transfer function to a SIMD vector.
        /// </summary>
        /// <typeparam name="TVector">The SIMD vector type.</typeparam>
        /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The linear signal values.</returns>
        public static TVector Expand<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            TVector zero = TOperations.Create(0F);
            TVector one = TOperations.Create(1F);
            TVector converted = new Rec709Companding.ExpandOperator(
                Bt709Alpha,
                Bt709Alpha - 1F,
                Rec709Companding.LinearSlope * Bt709Beta).Invoke<TVector, TOperations>(value);

            // H.273 bounds this curve to [0,1]. The shared operator itself retains extended RGB.
            // Keep the original comparison order, including the result for nonfinite input.
            TVector bounded = TOperations.ConditionalSelect(TOperations.LessThan(value, one), converted, one);
            return TOperations.ConditionalSelect(TOperations.LessThan(value, zero), zero, bounded);
        }

        /// <summary>
        /// Applies the BT.709-family opto-electronic transfer function.
        /// </summary>
        /// <param name="value">The linear signal value.</param>
        /// <returns>The nonlinear signal value.</returns>
        public static float Compress(float value)
        {
            if (value < 0F)
            {
                return 0F;
            }

            if (value < Bt709Beta)
            {
                return value * Rec709Companding.LinearSlope;
            }

            return value < 1F
                ? new Rec709Companding.CompressOperator(Bt709Alpha, Bt709Alpha - 1F, Bt709Beta).Invoke(value)
                : 1F;
        }

        /// <summary>
        /// Applies the BT.709-family opto-electronic transfer function to a SIMD vector.
        /// </summary>
        /// <typeparam name="TVector">The SIMD vector type.</typeparam>
        /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
        /// <param name="value">The linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static TVector Compress<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            TVector zero = TOperations.Create(0F);
            TVector one = TOperations.Create(1F);
            TVector converted = new Rec709Companding.CompressOperator(Bt709Alpha, Bt709Alpha - 1F, Bt709Beta).Invoke<TVector, TOperations>(value);

            // H.273 bounds this curve to [0,1]. The shared operator itself retains extended RGB.
            // Keep the original comparison order, including the result for nonfinite input.
            TVector bounded = TOperations.ConditionalSelect(TOperations.LessThan(value, one), converted, one);
            return TOperations.ConditionalSelect(TOperations.LessThan(value, zero), zero, bounded);
        }
    }

    /// <summary>
    /// Applies the Gamma2_2 transfer curve with its specified coefficients and range rules.
    /// </summary>
    private readonly struct Gamma22Curve : ITransferCurve
    {
        /// <summary>
        /// The H.273 power exponent for this transfer characteristic.
        /// </summary>
        private const float Gamma = 2.2F;

        /// <inheritdoc/>
        public static float Expand(float value)
        {
            return new TensorPrimitives_.PowOperator(Gamma).Invoke(Math.Clamp(value, 0F, 1F));
        }

        /// <inheritdoc/>
        public static TVector Expand<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            TVector zero = TOperations.Create(0F);
            TVector one = TOperations.Create(1F);

            return TOperations.Pow(TOperations.Min(TOperations.Max(value, zero), one), Gamma);
        }

        /// <inheritdoc/>
        public static float Compress(float value)
        {
            return new TensorPrimitives_.PowOperator(1F / Gamma).Invoke(Math.Clamp(value, 0F, 1F));
        }

        /// <inheritdoc/>
        public static TVector Compress<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            TVector zero = TOperations.Create(0F);
            TVector one = TOperations.Create(1F);

            return TOperations.Pow(TOperations.Min(TOperations.Max(value, zero), one), 1F / Gamma);
        }
    }

    /// <summary>
    /// Applies the Gamma2_8 transfer curve with its specified coefficients and range rules.
    /// </summary>
    private readonly struct Gamma28Curve : ITransferCurve
    {
        /// <summary>
        /// The H.273 power exponent for this transfer characteristic.
        /// </summary>
        private const float Gamma = 2.8F;

        /// <inheritdoc/>
        public static float Expand(float value)
        {
            return new TensorPrimitives_.PowOperator(Gamma).Invoke(Math.Clamp(value, 0F, 1F));
        }

        /// <inheritdoc/>
        public static TVector Expand<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            TVector zero = TOperations.Create(0F);
            TVector one = TOperations.Create(1F);

            return TOperations.Pow(TOperations.Min(TOperations.Max(value, zero), one), Gamma);
        }

        /// <inheritdoc/>
        public static float Compress(float value)
        {
            return new TensorPrimitives_.PowOperator(1F / Gamma).Invoke(Math.Clamp(value, 0F, 1F));
        }

        /// <inheritdoc/>
        public static TVector Compress<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            TVector zero = TOperations.Create(0F);
            TVector one = TOperations.Create(1F);

            return TOperations.Pow(TOperations.Min(TOperations.Max(value, zero), one), 1F / Gamma);
        }
    }

    /// <summary>
    /// Applies the SmpteSt240 transfer curve with its specified coefficients and range rules.
    /// </summary>
    private readonly struct Smpte240Curve : ITransferCurve
    {
        /// <summary>
        /// Applies the inverse SMPTE ST 240 opto-electronic transfer function.
        /// </summary>
        /// <param name="value">The nonlinear signal value.</param>
        /// <returns>The linear signal value.</returns>
        public static float Expand(float value)
        {
            if (value < 0F)
            {
                return 0F;
            }

            if (value < 4F * Smpte240Beta)
            {
                return value / 4F;
            }

            return value < 1F
                ? new TensorPrimitives_.PowOperator(1F / Rec709Companding.Exponent).Invoke((value + (Smpte240Alpha - 1F)) / Smpte240Alpha)
                : 1F;
        }

        /// <summary>
        /// Applies the inverse SMPTE ST 240 opto-electronic transfer function to a SIMD vector.
        /// </summary>
        /// <typeparam name="TVector">The SIMD vector type.</typeparam>
        /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The linear signal values.</returns>
        public static TVector Expand<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            TVector zero = TOperations.Create(0F);
            TVector one = TOperations.Create(1F);
            TVector linear = TOperations.Divide(value, TOperations.Create(4F));
            TVector baseValue = TOperations.Divide(TOperations.Add(value, TOperations.Create(Smpte240Alpha - 1F)), TOperations.Create(Smpte240Alpha));
            TVector nonlinear = TOperations.Pow(TOperations.Max(baseValue, zero), 1F / Rec709Companding.Exponent);
            TVector belowOne = TOperations.ConditionalSelect(TOperations.LessThan(value, TOperations.Create(4F * Smpte240Beta)), linear, nonlinear);
            TVector bounded = TOperations.ConditionalSelect(TOperations.LessThan(value, one), belowOne, one);
            return TOperations.ConditionalSelect(TOperations.LessThan(value, zero), zero, bounded);
        }

        /// <summary>
        /// Applies the SMPTE ST 240 opto-electronic transfer function.
        /// </summary>
        /// <param name="value">The linear signal value.</param>
        /// <returns>The nonlinear signal value.</returns>
        public static float Compress(float value)
        {
            if (value < 0F)
            {
                return 0F;
            }

            if (value < Smpte240Beta)
            {
                return value * 4F;
            }

            return value < 1F
                ? (Smpte240Alpha * new TensorPrimitives_.PowOperator(Rec709Companding.Exponent).Invoke(value)) - (Smpte240Alpha - 1F)
                : 1F;
        }

        /// <summary>
        /// Applies the SMPTE ST 240 opto-electronic transfer function to a SIMD vector.
        /// </summary>
        /// <typeparam name="TVector">The SIMD vector type.</typeparam>
        /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
        /// <param name="value">The linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static TVector Compress<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            TVector zero = TOperations.Create(0F);
            TVector one = TOperations.Create(1F);
            TVector linear = TOperations.Multiply(value, TOperations.Create(4F));
            TVector nonlinear = TOperations.Subtract(
                TOperations.Multiply(TOperations.Create(Smpte240Alpha), TOperations.Pow(TOperations.Max(value, zero), Rec709Companding.Exponent)),
                TOperations.Create(Smpte240Alpha - 1F));

            TVector belowOne = TOperations.ConditionalSelect(TOperations.LessThan(value, TOperations.Create(Smpte240Beta)), linear, nonlinear);
            TVector bounded = TOperations.ConditionalSelect(TOperations.LessThan(value, one), belowOne, one);
            return TOperations.ConditionalSelect(TOperations.LessThan(value, zero), zero, bounded);
        }
    }

    /// <summary>
    /// Applies the Linear transfer curve with its specified coefficients and range rules.
    /// </summary>
    private readonly struct LinearCurve : ITransferCurve
    {
        /// <inheritdoc/>
        public static float Expand(float value)
        {
            return Math.Clamp(value, 0F, 1F);
        }

        /// <inheritdoc/>
        public static TVector Expand<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            TVector zero = TOperations.Create(0F);
            TVector one = TOperations.Create(1F);

            return TOperations.Min(TOperations.Max(value, zero), one);
        }

        /// <inheritdoc/>
        public static float Compress(float value)
        {
            return Math.Clamp(value, 0F, 1F);
        }

        /// <inheritdoc/>
        public static TVector Compress<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            TVector zero = TOperations.Create(0F);
            TVector one = TOperations.Create(1F);

            return TOperations.Min(TOperations.Max(value, zero), one);
        }
    }

    /// <summary>
    /// Applies the Log100 transfer curve with its specified coefficients and range rules.
    /// </summary>
    private readonly struct Log100Curve : ITransferCurve
    {
        /// <summary>
        /// The largest linear input encoded as zero.
        /// </summary>
        private const float LinearCutoff = 0.01F;

        /// <summary>
        /// The midpoint of the linear interval represented by zero.
        /// </summary>
        private const float ZeroCodeValue = 0.005F;

        /// <inheritdoc/>
        public static float Expand(float value)
        {
            // Zero represents the linear interval [0,0.01]. Its midpoint minimizes the largest round-trip error.
            return value <= 0F ? ZeroCodeValue : new TensorPrimitives_.PowOperator(2F * (MathF.Min(value, 1F) - 1F)).Invoke(10F);
        }

        /// <inheritdoc/>
        public static TVector Expand<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            TVector zero = TOperations.Create(0F);
            TVector one = TOperations.Create(1F);

            // H.273 assigns an interval to zero for logarithmic curves. The scalar midpoint convention is
            // selected lane-wise after evaluating the positive branch, which keeps the hot path branchless.
            TVector exponent = TOperations.Multiply(TOperations.Subtract(TOperations.Min(value, one), one), TOperations.Create(2F * NaturalLogOfTen));
            TVector positive = TOperations.Exp(exponent);
            return TOperations.ConditionalSelect(TOperations.LessThanOrEqual(value, zero), TOperations.Create(ZeroCodeValue), positive);
        }

        /// <inheritdoc/>
        public static float Compress(float value)
        {
            return value <= LinearCutoff ? 0F : 1F + (MathF.Log10(MathF.Min(value, 1F)) / 2F);
        }

        /// <inheritdoc/>
        public static TVector Compress<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            TVector zero = TOperations.Create(0F);
            TVector one = TOperations.Create(1F);

            // Clamp inactive lanes to the threshold before Log. ConditionalSelect does not short-circuit,
            // so this prevents negative input lanes from contaminating the vector operation with NaN values.
            TVector threshold = TOperations.Create(LinearCutoff);
            TVector bounded = TOperations.Min(TOperations.Max(value, threshold), one);
            TVector positive = TOperations.Add(one, TOperations.Divide(TOperations.Log(bounded), TOperations.Create(2F * NaturalLogOfTen)));
            return TOperations.ConditionalSelect(TOperations.LessThanOrEqual(value, threshold), zero, positive);
        }
    }

    /// <summary>
    /// Applies the Log100Sqrt transfer curve with its specified coefficients and range rules.
    /// </summary>
    private readonly struct Log100SqrtCurve : ITransferCurve
    {
        /// <summary>
        /// The largest linear input encoded as zero.
        /// </summary>
        private const float LinearCutoff = 0.00316227766F;

        /// <summary>
        /// The midpoint of the linear interval represented by zero.
        /// </summary>
        private const float ZeroCodeValue = 0.00158113883F;

        /// <inheritdoc/>
        public static float Expand(float value)
        {
            // Use the midpoint of this logarithmic curve's interval for zero, as for Log100.
            return value <= 0F ? ZeroCodeValue : new TensorPrimitives_.PowOperator(2.5F * (MathF.Min(value, 1F) - 1F)).Invoke(10F);
        }

        /// <inheritdoc/>
        public static TVector Expand<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            TVector zero = TOperations.Create(0F);
            TVector one = TOperations.Create(1F);

            TVector exponent = TOperations.Multiply(TOperations.Subtract(TOperations.Min(value, one), one), TOperations.Create(2.5F * NaturalLogOfTen));
            TVector positive = TOperations.Exp(exponent);
            return TOperations.ConditionalSelect(TOperations.LessThanOrEqual(value, zero), TOperations.Create(ZeroCodeValue), positive);
        }

        /// <inheritdoc/>
        public static float Compress(float value)
        {
            return value <= LinearCutoff ? 0F : 1F + (MathF.Log10(MathF.Min(value, 1F)) / 2.5F);
        }

        /// <inheritdoc/>
        public static TVector Compress<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            TVector zero = TOperations.Create(0F);
            TVector one = TOperations.Create(1F);

            TVector threshold = TOperations.Create(LinearCutoff);
            TVector bounded = TOperations.Min(TOperations.Max(value, threshold), one);
            TVector positive = TOperations.Add(one, TOperations.Divide(TOperations.Log(bounded), TOperations.Create(2.5F * NaturalLogOfTen)));
            return TOperations.ConditionalSelect(TOperations.LessThanOrEqual(value, threshold), zero, positive);
        }
    }

    /// <summary>
    /// Applies the Iec61966_2_4 transfer curve with its specified coefficients and range rules.
    /// </summary>
    private readonly struct Iec61966Curve : ITransferCurve
    {
        /// <summary>
        /// Applies the inverse extended IEC 61966-2-4 transfer function.
        /// </summary>
        /// <param name="value">The nonlinear signal value.</param>
        /// <returns>The linear signal value.</returns>
        public static float Expand(float value)
        {
            if (value < -Rec709Companding.LinearSlope * Bt709Beta)
            {
                return -new TensorPrimitives_.PowOperator(1F / Rec709Companding.Exponent).Invoke((value - (Bt709Alpha - 1F)) / -Bt709Alpha);
            }

            return value < Rec709Companding.LinearSlope * Bt709Beta
                ? value / Rec709Companding.LinearSlope
                : new Rec709Companding.ExpandOperator(Bt709Alpha, Bt709Alpha - 1F, Rec709Companding.LinearSlope * Bt709Beta).Invoke(value);
        }

        /// <summary>
        /// Applies the inverse extended IEC 61966-2-4 transfer function to a SIMD vector.
        /// </summary>
        /// <typeparam name="TVector">The SIMD vector type.</typeparam>
        /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The linear signal values.</returns>
        public static TVector Expand<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            TVector negativeBase = TOperations.Divide(TOperations.Subtract(value, TOperations.Create(Bt709Alpha - 1F)), TOperations.Create(-Bt709Alpha));
            TVector negative = TOperations.Negate(TOperations.Pow(TOperations.Max(negativeBase, TOperations.Create(0F)), 1F / Rec709Companding.Exponent));
            TVector linear = TOperations.Divide(value, TOperations.Create(Rec709Companding.LinearSlope));
            TVector positiveBase = TOperations.Divide(TOperations.Add(value, TOperations.Create(Bt709Alpha - 1F)), TOperations.Create(Bt709Alpha));
            TVector positive = TOperations.Pow(TOperations.Max(positiveBase, TOperations.Create(0F)), 1F / Rec709Companding.Exponent);
            TVector centerOrPositive = TOperations.ConditionalSelect(
                TOperations.LessThan(value, TOperations.Create(Rec709Companding.LinearSlope * Bt709Beta)),
                linear,
                positive);

            return TOperations.ConditionalSelect(
                TOperations.LessThan(value, TOperations.Create(-Rec709Companding.LinearSlope * Bt709Beta)),
                negative,
                centerOrPositive);
        }

        /// <summary>
        /// Applies the extended IEC 61966-2-4 transfer function.
        /// </summary>
        /// <param name="value">The linear signal value.</param>
        /// <returns>The nonlinear signal value.</returns>
        public static float Compress(float value)
        {
            if (value < -Bt709Beta)
            {
                return (-Bt709Alpha * new TensorPrimitives_.PowOperator(Rec709Companding.Exponent).Invoke(-value)) + (Bt709Alpha - 1F);
            }

            return value < Bt709Beta
                ? value * Rec709Companding.LinearSlope
                : new Rec709Companding.CompressOperator(Bt709Alpha, Bt709Alpha - 1F, Bt709Beta).Invoke(value);
        }

        /// <summary>
        /// Applies the extended IEC 61966-2-4 transfer function to a SIMD vector.
        /// </summary>
        /// <typeparam name="TVector">The SIMD vector type.</typeparam>
        /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
        /// <param name="value">The linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static TVector Compress<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            TVector zero = TOperations.Create(0F);
            TVector negative = TOperations.Add(
                TOperations.Negate(TOperations.Multiply(
                    TOperations.Create(Bt709Alpha),
                    TOperations.Pow(TOperations.Max(TOperations.Negate(value), zero), Rec709Companding.Exponent))),
                TOperations.Create(Bt709Alpha - 1F));

            TVector linear = TOperations.Multiply(value, TOperations.Create(Rec709Companding.LinearSlope));
            TVector positive = TOperations.Subtract(
                TOperations.Multiply(TOperations.Create(Bt709Alpha), TOperations.Pow(TOperations.Max(value, zero), Rec709Companding.Exponent)),
                TOperations.Create(Bt709Alpha - 1F));

            TVector centerOrPositive = TOperations.ConditionalSelect(TOperations.LessThan(value, TOperations.Create(Bt709Beta)), linear, positive);
            return TOperations.ConditionalSelect(TOperations.LessThan(value, TOperations.Create(-Bt709Beta)), negative, centerOrPositive);
        }
    }

    /// <summary>
    /// Applies the ItuRBt1361_0 transfer curve with its specified coefficients and range rules.
    /// </summary>
    private readonly struct Bt1361Curve : ITransferCurve
    {
        /// <summary>
        /// The scale of the negative nonlinear segment specified by H.273.
        /// </summary>
        private const float NegativeScale = -0.27482420670236F;

        /// <summary>
        /// The offset of the negative nonlinear segment specified by H.273.
        /// </summary>
        private const float NegativeOffset = 0.02482420670236F;

        /// <summary>
        /// Applies the inverse extended BT.1361 transfer function.
        /// </summary>
        /// <param name="value">The nonlinear signal value.</param>
        /// <returns>The linear signal value.</returns>
        public static float Expand(float value)
        {
            if (value < -0.25F)
            {
                return -0.25F;
            }

            if (value < 0F)
            {
                return new TensorPrimitives_.PowOperator(1F / Rec709Companding.Exponent).Invoke((value - NegativeOffset) / NegativeScale) / -4F;
            }

            return Bt709Curve.Expand(value);
        }

        /// <summary>
        /// Applies the inverse extended BT.1361 transfer function to a SIMD vector.
        /// </summary>
        /// <typeparam name="TVector">The SIMD vector type.</typeparam>
        /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The linear signal values.</returns>
        public static TVector Expand<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            TVector zero = TOperations.Create(0F);
            TVector negativeBase = TOperations.Divide(
                TOperations.Subtract(value, TOperations.Create(NegativeOffset)),
                TOperations.Create(NegativeScale));

            TVector negative = TOperations.Divide(
                TOperations.Pow(TOperations.Max(negativeBase, zero), 1F / Rec709Companding.Exponent),
                TOperations.Create(-4F));

            TVector negativeOrPositive = TOperations.ConditionalSelect(
                TOperations.LessThan(value, zero),
                negative,
                Bt709Curve.Expand<TVector, TOperations>(value));

            return TOperations.ConditionalSelect(TOperations.LessThan(value, TOperations.Create(-0.25F)), TOperations.Create(-0.25F), negativeOrPositive);
        }

        /// <summary>
        /// Applies the extended BT.1361 transfer function.
        /// </summary>
        /// <param name="value">The linear signal value.</param>
        /// <returns>The nonlinear signal value.</returns>
        public static float Compress(float value)
        {
            if (value < -0.25F)
            {
                return -0.25F;
            }

            if (value < 0F)
            {
                return (NegativeScale * new TensorPrimitives_.PowOperator(Rec709Companding.Exponent).Invoke(-4F * value)) + NegativeOffset;
            }

            return Bt709Curve.Compress(value);
        }

        /// <summary>
        /// Applies the extended BT.1361 transfer function to a SIMD vector.
        /// </summary>
        /// <typeparam name="TVector">The SIMD vector type.</typeparam>
        /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
        /// <param name="value">The linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static TVector Compress<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            TVector zero = TOperations.Create(0F);
            TVector negativePower = TOperations.Pow(TOperations.Max(TOperations.Multiply(TOperations.Create(-4F), value), zero), Rec709Companding.Exponent);
            TVector negative = TOperations.Add(
                TOperations.Multiply(TOperations.Create(NegativeScale), negativePower),
                TOperations.Create(NegativeOffset));

            TVector negativeOrPositive = TOperations.ConditionalSelect(
                TOperations.LessThan(value, zero),
                negative,
                Bt709Curve.Compress<TVector, TOperations>(value));

            return TOperations.ConditionalSelect(TOperations.LessThan(value, TOperations.Create(-0.25F)), TOperations.Create(-0.25F), negativeOrPositive);
        }
    }

    /// <summary>
    /// Applies the Iec61966_2_1 transfer curve with its specified coefficients and range rules.
    /// </summary>
    private readonly struct SrgbCurve : ITransferCurve
    {
        /// <summary>
        /// Applies the inverse extended IEC 61966-2-1 transfer function.
        /// </summary>
        /// <param name="value">The nonlinear signal value.</param>
        /// <returns>The linear signal value.</returns>
        public static float Expand(float value)
            => new SRgbCompanding.ExpandOperator(SrgbAlpha, SrgbAlpha - 1F, SRgbCompanding.LinearSlope * SrgbBeta).Invoke(value);

        /// <summary>
        /// Applies the inverse extended IEC 61966-2-1 transfer function to a SIMD vector.
        /// </summary>
        /// <typeparam name="TVector">The SIMD vector type.</typeparam>
        /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The linear signal values.</returns>
        public static TVector Expand<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
            => new SRgbCompanding.ExpandOperator(SrgbAlpha, SrgbAlpha - 1F, SRgbCompanding.LinearSlope * SrgbBeta).Invoke<TVector, TOperations>(value);

        /// <summary>
        /// Applies the extended IEC 61966-2-1 transfer function.
        /// </summary>
        /// <param name="value">The linear signal value.</param>
        /// <returns>The nonlinear signal value.</returns>
        public static float Compress(float value)
            => new SRgbCompanding.CompressOperator(SrgbAlpha, SrgbAlpha - 1F, SrgbBeta).Invoke(value);

        /// <summary>
        /// Applies the extended IEC 61966-2-1 transfer function to a SIMD vector.
        /// </summary>
        /// <typeparam name="TVector">The SIMD vector type.</typeparam>
        /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
        /// <param name="value">The linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static TVector Compress<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
            => new SRgbCompanding.CompressOperator(SrgbAlpha, SrgbAlpha - 1F, SrgbBeta).Invoke<TVector, TOperations>(value);
    }

    /// <summary>
    /// Applies the SmpteSt2084 transfer curve with its specified coefficients and range rules.
    /// </summary>
    private readonly struct PqCurve : ITransferCurve
    {
        /// <summary>
        /// Applies the inverse SMPTE ST 2084 perceptual-quantizer transfer function.
        /// </summary>
        /// <param name="value">The nonlinear signal value.</param>
        /// <returns>The normalized linear signal value.</returns>
        public static float Expand(float value)
        {
            if (value <= 0F)
            {
                return 0F;
            }

            float nonlinearPower = new TensorPrimitives_.PowOperator(1F / PqM).Invoke(MathF.Min(value, 1F));
            float numerator = MathF.Max(nonlinearPower - PqC1, 0F);
            float denominator = PqC2 - (PqC3 * nonlinearPower);
            return new TensorPrimitives_.PowOperator(1F / PqN).Invoke(numerator / denominator);
        }

        /// <summary>
        /// Applies the inverse SMPTE ST 2084 perceptual-quantizer transfer function to a SIMD vector.
        /// </summary>
        /// <typeparam name="TVector">The SIMD vector type.</typeparam>
        /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The normalized linear signal values.</returns>
        public static TVector Expand<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            TVector zero = TOperations.Create(0F);
            TVector nonlinearPower = TOperations.Pow(TOperations.Min(TOperations.Max(value, zero), TOperations.Create(1F)), 1F / PqM);
            TVector numerator = TOperations.Max(TOperations.Subtract(nonlinearPower, TOperations.Create(PqC1)), zero);
            TVector denominator = TOperations.Subtract(TOperations.Create(PqC2), TOperations.Multiply(TOperations.Create(PqC3), nonlinearPower));
            TVector positive = TOperations.Min(TOperations.Pow(TOperations.Divide(numerator, denominator), 1F / PqN), TOperations.Create(1F));
            return TOperations.ConditionalSelect(TOperations.LessThanOrEqual(value, zero), zero, positive);
        }

        /// <summary>
        /// Applies the SMPTE ST 2084 perceptual-quantizer transfer function.
        /// </summary>
        /// <param name="value">The normalized linear signal value.</param>
        /// <returns>The nonlinear signal value.</returns>
        public static float Compress(float value)
        {
            if (value <= 0F)
            {
                return 0F;
            }

            float linearPower = new TensorPrimitives_.PowOperator(PqN).Invoke(MathF.Min(value, 1F));
            return new TensorPrimitives_.PowOperator(PqM).Invoke((PqC1 + (PqC2 * linearPower)) / (1F + (PqC3 * linearPower)));
        }

        /// <summary>
        /// Applies the SMPTE ST 2084 perceptual-quantizer transfer function to a SIMD vector.
        /// </summary>
        /// <typeparam name="TVector">The SIMD vector type.</typeparam>
        /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
        /// <param name="value">The normalized linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static TVector Compress<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            TVector zero = TOperations.Create(0F);
            TVector linearPower = TOperations.Pow(TOperations.Min(TOperations.Max(value, zero), TOperations.Create(1F)), PqN);
            TVector numerator = TOperations.Add(TOperations.Create(PqC1), TOperations.Multiply(TOperations.Create(PqC2), linearPower));
            TVector denominator = TOperations.Add(TOperations.Create(1F), TOperations.Multiply(TOperations.Create(PqC3), linearPower));
            TVector positive = TOperations.Min(TOperations.Pow(TOperations.Divide(numerator, denominator), PqM), TOperations.Create(1F));
            return TOperations.ConditionalSelect(TOperations.LessThanOrEqual(value, zero), zero, positive);
        }
    }

    /// <summary>
    /// Applies the SmpteSt428_1 transfer curve with its specified coefficients and range rules.
    /// </summary>
    private readonly struct Smpte428Curve : ITransferCurve
    {
        /// <summary>
        /// The H.273 power exponent for this transfer characteristic.
        /// </summary>
        private const float Gamma = 2.6F;

        /// <inheritdoc/>
        public static float Expand(float value)
        {
            return new TensorPrimitives_.PowOperator(Gamma).Invoke(MathF.Max(value, 0F)) / Smpte428Scale;
        }

        /// <inheritdoc/>
        public static TVector Expand<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            TVector zero = TOperations.Create(0F);

            return TOperations.Divide(TOperations.Pow(TOperations.Max(value, zero), Gamma), TOperations.Create(Smpte428Scale));
        }

        /// <inheritdoc/>
        public static float Compress(float value)
        {
            return new TensorPrimitives_.PowOperator(1F / Gamma).Invoke(Smpte428Scale * MathF.Max(value, 0F));
        }

        /// <inheritdoc/>
        public static TVector Compress<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            TVector zero = TOperations.Create(0F);

            return TOperations.Pow(TOperations.Multiply(TOperations.Create(Smpte428Scale), TOperations.Max(value, zero)), 1F / Gamma);
        }
    }

    /// <summary>
    /// Applies the AribStdB67 transfer curve with its specified coefficients and range rules.
    /// </summary>
    private readonly struct HlgCurve : ITransferCurve
    {
        /// <summary>
        /// Applies the inverse HLG opto-electronic transfer function.
        /// </summary>
        /// <param name="value">The nonlinear signal value.</param>
        /// <returns>The normalized linear signal value.</returns>
        public static float Expand(float value)
        {
            if (value <= 0F)
            {
                return 0F;
            }

            return value <= 0.5F
                ? (value * value) / 3F
                : (MathF.Exp((MathF.Min(value, 1F) - HlgC) / HlgA) + HlgB) / 12F;
        }

        /// <summary>
        /// Applies the inverse HLG opto-electronic transfer function to a SIMD vector.
        /// </summary>
        /// <typeparam name="TVector">The SIMD vector type.</typeparam>
        /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The normalized linear signal values.</returns>
        public static TVector Expand<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            TVector zero = TOperations.Create(0F);
            TVector bounded = TOperations.Min(TOperations.Max(value, zero), TOperations.Create(1F));
            TVector linear = TOperations.Divide(TOperations.Multiply(bounded, bounded), TOperations.Create(3F));
            TVector exponent = TOperations.Divide(TOperations.Subtract(bounded, TOperations.Create(HlgC)), TOperations.Create(HlgA));
            TVector logarithmic = TOperations.Divide(TOperations.Add(TOperations.Exp(exponent), TOperations.Create(HlgB)), TOperations.Create(12F));
            TVector positive = TOperations.ConditionalSelect(TOperations.LessThanOrEqual(value, TOperations.Create(0.5F)), linear, logarithmic);
            return TOperations.ConditionalSelect(TOperations.LessThanOrEqual(value, zero), zero, positive);
        }

        /// <summary>
        /// Applies the HLG opto-electronic transfer function.
        /// </summary>
        /// <param name="value">The normalized linear signal value.</param>
        /// <returns>The nonlinear signal value.</returns>
        public static float Compress(float value)
        {
            if (value <= 0F)
            {
                return 0F;
            }

            float bounded = MathF.Min(value, 1F);
            return bounded <= 1F / 12F
                ? MathF.Sqrt(3F * bounded)
                : (HlgA * MathF.Log((12F * bounded) - HlgB)) + HlgC;
        }

        /// <summary>
        /// Applies the HLG opto-electronic transfer function to a SIMD vector.
        /// </summary>
        /// <typeparam name="TVector">The SIMD vector type.</typeparam>
        /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
        /// <param name="value">The normalized linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static TVector Compress<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            TVector zero = TOperations.Create(0F);
            TVector bounded = TOperations.Min(TOperations.Max(value, zero), TOperations.Create(1F));
            TVector linear = TOperations.Sqrt(TOperations.Multiply(TOperations.Create(3F), bounded));

            // Clamp the logarithm input for inactive lanes. SIMD conditional selection evaluates both branches,
            // while the scalar definition evaluates Log only above the 1/12 transition.
            TVector logarithmInput = TOperations.Max(
                TOperations.Subtract(TOperations.Multiply(TOperations.Create(12F), bounded), TOperations.Create(HlgB)),
                TOperations.Create(float.Epsilon));

            TVector logarithmic = TOperations.Add(TOperations.Multiply(TOperations.Create(HlgA), TOperations.Log(logarithmInput)), TOperations.Create(HlgC));
            TVector positive = TOperations.ConditionalSelect(TOperations.LessThanOrEqual(bounded, TOperations.Create(1F / 12F)), linear, logarithmic);
            return TOperations.ConditionalSelect(TOperations.LessThanOrEqual(value, zero), zero, positive);
        }
    }
}
