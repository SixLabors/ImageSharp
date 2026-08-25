// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Av1TransferVectorOperators;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <content>
/// Provides the SIMD implementations of the H.273 transfer characteristics used by AV1 color conversion.
/// </content>
internal static partial class Av1TransferFunctions
{
    /// <summary>
    /// Converts four nonlinear signal values to their H.273 linear-domain values.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The corresponding linear-domain values.</returns>
    public static Vector128<float> ToLinear(ObuTransferCharacteristics transferCharacteristics, Vector128<float> value)
        => ToLinear<Vector128<float>, Vector128Operator>(transferCharacteristics, value);

    /// <summary>
    /// Converts eight nonlinear signal values to their H.273 linear-domain values.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The corresponding linear-domain values.</returns>
    public static Vector256<float> ToLinear(ObuTransferCharacteristics transferCharacteristics, Vector256<float> value)
        => ToLinear<Vector256<float>, Vector256Operator>(transferCharacteristics, value);

    /// <summary>
    /// Converts sixteen nonlinear signal values to their H.273 linear-domain values.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The corresponding linear-domain values.</returns>
    public static Vector512<float> ToLinear(ObuTransferCharacteristics transferCharacteristics, Vector512<float> value)
        => ToLinear<Vector512<float>, Vector512Operator>(transferCharacteristics, value);

    /// <summary>
    /// Converts four linear signal values to their H.273 nonlinear-domain values.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The linear signal values.</param>
    /// <returns>The corresponding nonlinear-domain values.</returns>
    public static Vector128<float> ToGamma(ObuTransferCharacteristics transferCharacteristics, Vector128<float> value)
        => ToGamma<Vector128<float>, Vector128Operator>(transferCharacteristics, value);

    /// <summary>
    /// Converts eight linear signal values to their H.273 nonlinear-domain values.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The linear signal values.</param>
    /// <returns>The corresponding nonlinear-domain values.</returns>
    public static Vector256<float> ToGamma(ObuTransferCharacteristics transferCharacteristics, Vector256<float> value)
        => ToGamma<Vector256<float>, Vector256Operator>(transferCharacteristics, value);

    /// <summary>
    /// Converts sixteen linear signal values to their H.273 nonlinear-domain values.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The linear signal values.</param>
    /// <returns>The corresponding nonlinear-domain values.</returns>
    public static Vector512<float> ToGamma(ObuTransferCharacteristics transferCharacteristics, Vector512<float> value)
        => ToGamma<Vector512<float>, Vector512Operator>(transferCharacteristics, value);

    /// <summary>
    /// Converts nonlinear signal values to their H.273 linear-domain values using the selected SIMD width.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperator">The operations for the SIMD vector type.</typeparam>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The corresponding linear-domain values.</returns>
    private static TVector ToLinear<TVector, TOperator>(ObuTransferCharacteristics transferCharacteristics, TVector value)
        where TVector : struct
        where TOperator : struct, ITransferVectorOperator<TVector>
    {
        TVector zero = TOperator.Create(0F);
        TVector one = TOperator.Create(1F);

        switch (transferCharacteristics)
        {
            case ObuTransferCharacteristics.Bt709:
            case ObuTransferCharacteristics.Bt601:
            case ObuTransferCharacteristics.Bt202010Bit:
            case ObuTransferCharacteristics.Bt202012Bit:
                return ToLinearBt709<TVector, TOperator>(value);
            case ObuTransferCharacteristics.Bt470M:
                return Power<TVector, TOperator>(TOperator.Min(TOperator.Max(value, zero), one), 2.2F);
            case ObuTransferCharacteristics.Bt470BG:
                return Power<TVector, TOperator>(TOperator.Min(TOperator.Max(value, zero), one), 2.8F);
            case ObuTransferCharacteristics.Smpte240:
                return ToLinearSmpte240<TVector, TOperator>(value);
            case ObuTransferCharacteristics.Linear:
                return TOperator.Min(TOperator.Max(value, zero), one);
            case ObuTransferCharacteristics.Log100:
            {
                // H.273 assigns an interval to zero for logarithmic curves. The scalar midpoint convention is
                // selected lane-wise after evaluating the positive branch, which keeps the hot path branchless.
                TVector exponent = TOperator.Multiply(TOperator.Subtract(TOperator.Min(value, one), one), TOperator.Create(2F * 2.302585092994046F));
                TVector positive = TOperator.Exp(exponent);
                return TOperator.ConditionalSelect(TOperator.LessThanOrEqual(value, zero), TOperator.Create(0.005F), positive);
            }

            case ObuTransferCharacteristics.Log100Sqrt10:
            {
                TVector exponent = TOperator.Multiply(TOperator.Subtract(TOperator.Min(value, one), one), TOperator.Create(2.5F * 2.302585092994046F));
                TVector positive = TOperator.Exp(exponent);
                return TOperator.ConditionalSelect(TOperator.LessThanOrEqual(value, zero), TOperator.Create(0.00158113883F), positive);
            }

            case ObuTransferCharacteristics.Iec61966:
                return ToLinearIec61966<TVector, TOperator>(value);
            case ObuTransferCharacteristics.Bt1361:
                return ToLinearBt1361<TVector, TOperator>(value);
            case ObuTransferCharacteristics.Srgb:
                return ToLinearSrgb<TVector, TOperator>(value);
            case ObuTransferCharacteristics.Smpte2084:
                return ToLinearPq<TVector, TOperator>(value);
            case ObuTransferCharacteristics.Smpte428:
                return TOperator.Divide(Power<TVector, TOperator>(TOperator.Max(value, zero), 2.6F), TOperator.Create(Smpte428Scale));
            case ObuTransferCharacteristics.Hlg:
                return ToLinearHlg<TVector, TOperator>(value);
            default:
                return ToLinearBt709<TVector, TOperator>(value);
        }
    }

    /// <summary>
    /// Converts linear signal values to their H.273 nonlinear-domain values using the selected SIMD width.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperator">The operations for the SIMD vector type.</typeparam>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The linear signal values.</param>
    /// <returns>The corresponding nonlinear-domain values.</returns>
    private static TVector ToGamma<TVector, TOperator>(ObuTransferCharacteristics transferCharacteristics, TVector value)
        where TVector : struct
        where TOperator : struct, ITransferVectorOperator<TVector>
    {
        TVector zero = TOperator.Create(0F);
        TVector one = TOperator.Create(1F);

        switch (transferCharacteristics)
        {
            case ObuTransferCharacteristics.Bt709:
            case ObuTransferCharacteristics.Bt601:
            case ObuTransferCharacteristics.Bt202010Bit:
            case ObuTransferCharacteristics.Bt202012Bit:
                return ToGammaBt709<TVector, TOperator>(value);
            case ObuTransferCharacteristics.Bt470M:
                return Power<TVector, TOperator>(TOperator.Min(TOperator.Max(value, zero), one), 1F / 2.2F);
            case ObuTransferCharacteristics.Bt470BG:
                return Power<TVector, TOperator>(TOperator.Min(TOperator.Max(value, zero), one), 1F / 2.8F);
            case ObuTransferCharacteristics.Smpte240:
                return ToGammaSmpte240<TVector, TOperator>(value);
            case ObuTransferCharacteristics.Linear:
                return TOperator.Min(TOperator.Max(value, zero), one);
            case ObuTransferCharacteristics.Log100:
            {
                // Clamp inactive lanes to the threshold before Log. ConditionalSelect does not short-circuit,
                // so this prevents negative input lanes from contaminating the vector operation with NaN values.
                TVector threshold = TOperator.Create(0.01F);
                TVector bounded = TOperator.Min(TOperator.Max(value, threshold), one);
                TVector positive = TOperator.Add(one, TOperator.Divide(TOperator.Log(bounded), TOperator.Create(2F * 2.302585092994046F)));
                return TOperator.ConditionalSelect(TOperator.LessThanOrEqual(value, threshold), zero, positive);
            }

            case ObuTransferCharacteristics.Log100Sqrt10:
            {
                TVector threshold = TOperator.Create(0.00316227766F);
                TVector bounded = TOperator.Min(TOperator.Max(value, threshold), one);
                TVector positive = TOperator.Add(one, TOperator.Divide(TOperator.Log(bounded), TOperator.Create(2.5F * 2.302585092994046F)));
                return TOperator.ConditionalSelect(TOperator.LessThanOrEqual(value, threshold), zero, positive);
            }

            case ObuTransferCharacteristics.Iec61966:
                return ToGammaIec61966<TVector, TOperator>(value);
            case ObuTransferCharacteristics.Bt1361:
                return ToGammaBt1361<TVector, TOperator>(value);
            case ObuTransferCharacteristics.Srgb:
                return ToGammaSrgb<TVector, TOperator>(value);
            case ObuTransferCharacteristics.Smpte2084:
                return ToGammaPq<TVector, TOperator>(value);
            case ObuTransferCharacteristics.Smpte428:
                return Power<TVector, TOperator>(TOperator.Multiply(TOperator.Create(Smpte428Scale), TOperator.Max(value, zero)), 1F / 2.6F);
            case ObuTransferCharacteristics.Hlg:
                return ToGammaHlg<TVector, TOperator>(value);
            default:
                return ToGammaBt709<TVector, TOperator>(value);
        }
    }

    /// <summary>
    /// Applies the inverse BT.709-family opto-electronic transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperator">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The linear signal values.</returns>
    private static TVector ToLinearBt709<TVector, TOperator>(TVector value)
        where TVector : struct
        where TOperator : struct, ITransferVectorOperator<TVector>
    {
        TVector zero = TOperator.Create(0F);
        TVector one = TOperator.Create(1F);
        TVector linear = TOperator.Divide(value, TOperator.Create(4.5F));
        TVector baseValue = TOperator.Divide(TOperator.Add(value, TOperator.Create(Bt709Alpha - 1F)), TOperator.Create(Bt709Alpha));
        TVector nonlinear = Power<TVector, TOperator>(TOperator.Max(baseValue, zero), 1F / 0.45F);
        TVector belowOne = TOperator.ConditionalSelect(TOperator.LessThan(value, TOperator.Create(4.5F * Bt709Beta)), linear, nonlinear);

        // The comparisons deliberately mirror the scalar ordering. This preserves the H.273 lower and upper
        // saturation rules while allowing all lanes to execute without data-dependent branches.
        TVector bounded = TOperator.ConditionalSelect(TOperator.LessThan(value, one), belowOne, one);
        return TOperator.ConditionalSelect(TOperator.LessThan(value, zero), zero, bounded);
    }

    /// <summary>
    /// Applies the BT.709-family opto-electronic transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperator">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The linear signal values.</param>
    /// <returns>The nonlinear signal values.</returns>
    private static TVector ToGammaBt709<TVector, TOperator>(TVector value)
        where TVector : struct
        where TOperator : struct, ITransferVectorOperator<TVector>
    {
        TVector zero = TOperator.Create(0F);
        TVector one = TOperator.Create(1F);
        TVector linear = TOperator.Multiply(value, TOperator.Create(4.5F));
        TVector nonlinear = TOperator.Subtract(
            TOperator.Multiply(TOperator.Create(Bt709Alpha), Power<TVector, TOperator>(TOperator.Max(value, zero), 0.45F)),
            TOperator.Create(Bt709Alpha - 1F));
        TVector belowOne = TOperator.ConditionalSelect(TOperator.LessThan(value, TOperator.Create(Bt709Beta)), linear, nonlinear);
        TVector bounded = TOperator.ConditionalSelect(TOperator.LessThan(value, one), belowOne, one);
        return TOperator.ConditionalSelect(TOperator.LessThan(value, zero), zero, bounded);
    }

    /// <summary>
    /// Applies the inverse SMPTE ST 240 opto-electronic transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperator">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The linear signal values.</returns>
    private static TVector ToLinearSmpte240<TVector, TOperator>(TVector value)
        where TVector : struct
        where TOperator : struct, ITransferVectorOperator<TVector>
    {
        TVector zero = TOperator.Create(0F);
        TVector one = TOperator.Create(1F);
        TVector linear = TOperator.Divide(value, TOperator.Create(4F));
        TVector baseValue = TOperator.Divide(TOperator.Add(value, TOperator.Create(Smpte240Alpha - 1F)), TOperator.Create(Smpte240Alpha));
        TVector nonlinear = Power<TVector, TOperator>(TOperator.Max(baseValue, zero), 1F / 0.45F);
        TVector belowOne = TOperator.ConditionalSelect(TOperator.LessThan(value, TOperator.Create(4F * Smpte240Beta)), linear, nonlinear);
        TVector bounded = TOperator.ConditionalSelect(TOperator.LessThan(value, one), belowOne, one);
        return TOperator.ConditionalSelect(TOperator.LessThan(value, zero), zero, bounded);
    }

    /// <summary>
    /// Applies the SMPTE ST 240 opto-electronic transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperator">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The linear signal values.</param>
    /// <returns>The nonlinear signal values.</returns>
    private static TVector ToGammaSmpte240<TVector, TOperator>(TVector value)
        where TVector : struct
        where TOperator : struct, ITransferVectorOperator<TVector>
    {
        TVector zero = TOperator.Create(0F);
        TVector one = TOperator.Create(1F);
        TVector linear = TOperator.Multiply(value, TOperator.Create(4F));
        TVector nonlinear = TOperator.Subtract(
            TOperator.Multiply(TOperator.Create(Smpte240Alpha), Power<TVector, TOperator>(TOperator.Max(value, zero), 0.45F)),
            TOperator.Create(Smpte240Alpha - 1F));
        TVector belowOne = TOperator.ConditionalSelect(TOperator.LessThan(value, TOperator.Create(Smpte240Beta)), linear, nonlinear);
        TVector bounded = TOperator.ConditionalSelect(TOperator.LessThan(value, one), belowOne, one);
        return TOperator.ConditionalSelect(TOperator.LessThan(value, zero), zero, bounded);
    }

    /// <summary>
    /// Applies the inverse extended IEC 61966-2-4 transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperator">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The linear signal values.</returns>
    private static TVector ToLinearIec61966<TVector, TOperator>(TVector value)
        where TVector : struct
        where TOperator : struct, ITransferVectorOperator<TVector>
    {
        TVector negativeBase = TOperator.Divide(TOperator.Subtract(value, TOperator.Create(Bt709Alpha - 1F)), TOperator.Create(-Bt709Alpha));
        TVector negative = TOperator.Negate(Power<TVector, TOperator>(TOperator.Max(negativeBase, TOperator.Create(0F)), 1F / 0.45F));
        TVector linear = TOperator.Divide(value, TOperator.Create(4.5F));
        TVector positiveBase = TOperator.Divide(TOperator.Add(value, TOperator.Create(Bt709Alpha - 1F)), TOperator.Create(Bt709Alpha));
        TVector positive = Power<TVector, TOperator>(TOperator.Max(positiveBase, TOperator.Create(0F)), 1F / 0.45F);
        TVector centerOrPositive = TOperator.ConditionalSelect(TOperator.LessThan(value, TOperator.Create(4.5F * Bt709Beta)), linear, positive);
        return TOperator.ConditionalSelect(TOperator.LessThan(value, TOperator.Create(-4.5F * Bt709Beta)), negative, centerOrPositive);
    }

    /// <summary>
    /// Applies the extended IEC 61966-2-4 transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperator">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The linear signal values.</param>
    /// <returns>The nonlinear signal values.</returns>
    private static TVector ToGammaIec61966<TVector, TOperator>(TVector value)
        where TVector : struct
        where TOperator : struct, ITransferVectorOperator<TVector>
    {
        TVector zero = TOperator.Create(0F);
        TVector negative = TOperator.Add(
            TOperator.Negate(TOperator.Multiply(
                TOperator.Create(Bt709Alpha),
                Power<TVector, TOperator>(TOperator.Max(TOperator.Negate(value), zero), 0.45F))),
            TOperator.Create(Bt709Alpha - 1F));

        TVector linear = TOperator.Multiply(value, TOperator.Create(4.5F));
        TVector positive = TOperator.Subtract(
            TOperator.Multiply(TOperator.Create(Bt709Alpha), Power<TVector, TOperator>(TOperator.Max(value, zero), 0.45F)),
            TOperator.Create(Bt709Alpha - 1F));
        TVector centerOrPositive = TOperator.ConditionalSelect(TOperator.LessThan(value, TOperator.Create(Bt709Beta)), linear, positive);
        return TOperator.ConditionalSelect(TOperator.LessThan(value, TOperator.Create(-Bt709Beta)), negative, centerOrPositive);
    }

    /// <summary>
    /// Applies the inverse extended BT.1361 transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperator">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The linear signal values.</returns>
    private static TVector ToLinearBt1361<TVector, TOperator>(TVector value)
        where TVector : struct
        where TOperator : struct, ITransferVectorOperator<TVector>
    {
        TVector zero = TOperator.Create(0F);
        TVector negativeBase = TOperator.Divide(TOperator.Subtract(value, TOperator.Create(0.02482420670236F)), TOperator.Create(-0.27482420670236F));
        TVector negative = TOperator.Divide(Power<TVector, TOperator>(TOperator.Max(negativeBase, zero), 1F / 0.45F), TOperator.Create(-4F));
        TVector negativeOrPositive = TOperator.ConditionalSelect(TOperator.LessThan(value, zero), negative, ToLinearBt709<TVector, TOperator>(value));
        return TOperator.ConditionalSelect(TOperator.LessThan(value, TOperator.Create(-0.25F)), TOperator.Create(-0.25F), negativeOrPositive);
    }

    /// <summary>
    /// Applies the extended BT.1361 transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperator">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The linear signal values.</param>
    /// <returns>The nonlinear signal values.</returns>
    private static TVector ToGammaBt1361<TVector, TOperator>(TVector value)
        where TVector : struct
        where TOperator : struct, ITransferVectorOperator<TVector>
    {
        TVector zero = TOperator.Create(0F);
        TVector negativePower = Power<TVector, TOperator>(TOperator.Max(TOperator.Multiply(TOperator.Create(-4F), value), zero), 0.45F);
        TVector negative = TOperator.Add(TOperator.Multiply(TOperator.Create(-0.27482420670236F), negativePower), TOperator.Create(0.02482420670236F));
        TVector negativeOrPositive = TOperator.ConditionalSelect(TOperator.LessThan(value, zero), negative, ToGammaBt709<TVector, TOperator>(value));
        return TOperator.ConditionalSelect(TOperator.LessThan(value, TOperator.Create(-0.25F)), TOperator.Create(-0.25F), negativeOrPositive);
    }

    /// <summary>
    /// Applies the inverse extended IEC 61966-2-1 transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperator">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The linear signal values.</returns>
    private static TVector ToLinearSrgb<TVector, TOperator>(TVector value)
        where TVector : struct
        where TOperator : struct, ITransferVectorOperator<TVector>
    {
        TVector zero = TOperator.Create(0F);
        TVector negativeBase = TOperator.Divide(TOperator.Subtract(value, TOperator.Create(SrgbAlpha - 1F)), TOperator.Create(-SrgbAlpha));
        TVector negative = TOperator.Negate(Power<TVector, TOperator>(TOperator.Max(negativeBase, zero), 2.4F));
        TVector linear = TOperator.Divide(value, TOperator.Create(12.92F));
        TVector positiveBase = TOperator.Divide(TOperator.Add(value, TOperator.Create(SrgbAlpha - 1F)), TOperator.Create(SrgbAlpha));
        TVector positive = Power<TVector, TOperator>(TOperator.Max(positiveBase, zero), 2.4F);
        TVector centerOrPositive = TOperator.ConditionalSelect(TOperator.LessThan(value, TOperator.Create(12.92F * SrgbBeta)), linear, positive);
        return TOperator.ConditionalSelect(TOperator.LessThan(value, TOperator.Create(-12.92F * SrgbBeta)), negative, centerOrPositive);
    }

    /// <summary>
    /// Applies the extended IEC 61966-2-1 transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperator">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The linear signal values.</param>
    /// <returns>The nonlinear signal values.</returns>
    private static TVector ToGammaSrgb<TVector, TOperator>(TVector value)
        where TVector : struct
        where TOperator : struct, ITransferVectorOperator<TVector>
    {
        TVector zero = TOperator.Create(0F);
        TVector negative = TOperator.Add(
            TOperator.Negate(TOperator.Multiply(
                TOperator.Create(SrgbAlpha),
                Power<TVector, TOperator>(TOperator.Max(TOperator.Negate(value), zero), 1F / 2.4F))),
            TOperator.Create(SrgbAlpha - 1F));

        TVector linear = TOperator.Multiply(value, TOperator.Create(12.92F));
        TVector positive = TOperator.Subtract(
            TOperator.Multiply(TOperator.Create(SrgbAlpha), Power<TVector, TOperator>(TOperator.Max(value, zero), 1F / 2.4F)),
            TOperator.Create(SrgbAlpha - 1F));
        TVector centerOrPositive = TOperator.ConditionalSelect(TOperator.LessThan(value, TOperator.Create(SrgbBeta)), linear, positive);
        return TOperator.ConditionalSelect(TOperator.LessThan(value, TOperator.Create(-SrgbBeta)), negative, centerOrPositive);
    }

    /// <summary>
    /// Applies the inverse SMPTE ST 2084 perceptual-quantizer transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperator">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The normalized linear signal values.</returns>
    private static TVector ToLinearPq<TVector, TOperator>(TVector value)
        where TVector : struct
        where TOperator : struct, ITransferVectorOperator<TVector>
    {
        TVector zero = TOperator.Create(0F);
        TVector nonlinearPower = Power<TVector, TOperator>(TOperator.Min(TOperator.Max(value, zero), TOperator.Create(1F)), 1F / PqM);
        TVector numerator = TOperator.Max(TOperator.Subtract(nonlinearPower, TOperator.Create(PqC1)), zero);
        TVector denominator = TOperator.Subtract(TOperator.Create(PqC2), TOperator.Multiply(TOperator.Create(PqC3), nonlinearPower));
        TVector positive = TOperator.Min(Power<TVector, TOperator>(TOperator.Divide(numerator, denominator), 1F / PqN), TOperator.Create(1F));
        return TOperator.ConditionalSelect(TOperator.LessThanOrEqual(value, zero), zero, positive);
    }

    /// <summary>
    /// Applies the SMPTE ST 2084 perceptual-quantizer transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperator">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The normalized linear signal values.</param>
    /// <returns>The nonlinear signal values.</returns>
    private static TVector ToGammaPq<TVector, TOperator>(TVector value)
        where TVector : struct
        where TOperator : struct, ITransferVectorOperator<TVector>
    {
        TVector zero = TOperator.Create(0F);
        TVector linearPower = Power<TVector, TOperator>(TOperator.Min(TOperator.Max(value, zero), TOperator.Create(1F)), PqN);
        TVector numerator = TOperator.Add(TOperator.Create(PqC1), TOperator.Multiply(TOperator.Create(PqC2), linearPower));
        TVector denominator = TOperator.Add(TOperator.Create(1F), TOperator.Multiply(TOperator.Create(PqC3), linearPower));
        TVector positive = TOperator.Min(Power<TVector, TOperator>(TOperator.Divide(numerator, denominator), PqM), TOperator.Create(1F));
        return TOperator.ConditionalSelect(TOperator.LessThanOrEqual(value, zero), zero, positive);
    }

    /// <summary>
    /// Applies the inverse HLG opto-electronic transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperator">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The normalized linear signal values.</returns>
    private static TVector ToLinearHlg<TVector, TOperator>(TVector value)
        where TVector : struct
        where TOperator : struct, ITransferVectorOperator<TVector>
    {
        TVector zero = TOperator.Create(0F);
        TVector bounded = TOperator.Min(TOperator.Max(value, zero), TOperator.Create(1F));
        TVector linear = TOperator.Divide(TOperator.Multiply(bounded, bounded), TOperator.Create(3F));
        TVector exponent = TOperator.Divide(TOperator.Subtract(bounded, TOperator.Create(HlgC)), TOperator.Create(HlgA));
        TVector logarithmic = TOperator.Divide(TOperator.Add(TOperator.Exp(exponent), TOperator.Create(HlgB)), TOperator.Create(12F));
        TVector positive = TOperator.ConditionalSelect(TOperator.LessThanOrEqual(value, TOperator.Create(0.5F)), linear, logarithmic);
        return TOperator.ConditionalSelect(TOperator.LessThanOrEqual(value, zero), zero, positive);
    }

    /// <summary>
    /// Applies the HLG opto-electronic transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperator">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The normalized linear signal values.</param>
    /// <returns>The nonlinear signal values.</returns>
    private static TVector ToGammaHlg<TVector, TOperator>(TVector value)
        where TVector : struct
        where TOperator : struct, ITransferVectorOperator<TVector>
    {
        TVector zero = TOperator.Create(0F);
        TVector bounded = TOperator.Min(TOperator.Max(value, zero), TOperator.Create(1F));
        TVector linear = TOperator.Sqrt(TOperator.Multiply(TOperator.Create(3F), bounded));

        // Clamp the logarithm input for inactive lanes. SIMD conditional selection evaluates both branches,
        // while the scalar definition evaluates Log only above the 1/12 transition.
        TVector logarithmInput = TOperator.Max(
            TOperator.Subtract(TOperator.Multiply(TOperator.Create(12F), bounded), TOperator.Create(HlgB)),
            TOperator.Create(float.Epsilon));
        TVector logarithmic = TOperator.Add(TOperator.Multiply(TOperator.Create(HlgA), TOperator.Log(logarithmInput)), TOperator.Create(HlgC));
        TVector positive = TOperator.ConditionalSelect(TOperator.LessThanOrEqual(bounded, TOperator.Create(1F / 12F)), linear, logarithmic);
        return TOperator.ConditionalSelect(TOperator.LessThanOrEqual(value, zero), zero, positive);
    }

    /// <summary>
    /// Raises nonnegative SIMD values to a scalar exponent.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperator">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The nonnegative base values.</param>
    /// <param name="exponent">The exponent applied to every lane.</param>
    /// <returns>The exponentiated values.</returns>
    private static TVector Power<TVector, TOperator>(TVector value, float exponent)
        where TVector : struct
        where TOperator : struct, ITransferVectorOperator<TVector>
    {
        // System.Numerics.Tensors does not currently vectorize Pow. Expressing positive powers as Exp(Log(x) * y)
        // uses the .NET 10 cross-platform vector math kernels and keeps all transfer-function lanes in SIMD.
        return TOperator.Exp(TOperator.Multiply(TOperator.Log(value), TOperator.Create(exponent)));
    }
}

/// <summary>
/// Contains the stateless vector-width operators used by the shared H.273 transfer-function formulas.
/// </summary>
internal static class Av1TransferVectorOperators
{
    /// <summary>
    /// Defines the lane-wise operations required by the shared H.273 SIMD formulas.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    public interface ITransferVectorOperator<TVector>
        where TVector : struct
    {
        /// <summary>
        /// Creates a vector whose lanes contain the specified value.
        /// </summary>
        /// <param name="value">The value copied to every lane.</param>
        /// <returns>The created vector.</returns>
        public static abstract TVector Create(float value);

        /// <summary>
        /// Adds corresponding vector lanes.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The lane-wise sum.</returns>
        public static abstract TVector Add(TVector left, TVector right);

        /// <summary>
        /// Subtracts corresponding vector lanes.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The lane-wise difference.</returns>
        public static abstract TVector Subtract(TVector left, TVector right);

        /// <summary>
        /// Multiplies corresponding vector lanes.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The lane-wise product.</returns>
        public static abstract TVector Multiply(TVector left, TVector right);

        /// <summary>
        /// Multiplies corresponding vector lanes and adds an addend using the fastest supported estimate.
        /// </summary>
        /// <param name="left">The left multiplication operand.</param>
        /// <param name="right">The right multiplication operand.</param>
        /// <param name="addend">The value added to the product.</param>
        /// <returns>The lane-wise multiply-add result.</returns>
        public static abstract TVector MultiplyAddEstimate(TVector left, TVector right, TVector addend);

        /// <summary>
        /// Divides corresponding vector lanes.
        /// </summary>
        /// <param name="left">The dividend.</param>
        /// <param name="right">The divisor.</param>
        /// <returns>The lane-wise quotient.</returns>
        public static abstract TVector Divide(TVector left, TVector right);

        /// <summary>
        /// Negates every vector lane.
        /// </summary>
        /// <param name="value">The input vector.</param>
        /// <returns>The negated vector.</returns>
        public static abstract TVector Negate(TVector value);

        /// <summary>
        /// Selects the smaller value in each pair of lanes.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The lane-wise minimum.</returns>
        public static abstract TVector Min(TVector left, TVector right);

        /// <summary>
        /// Selects the larger value in each pair of lanes.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The lane-wise maximum.</returns>
        public static abstract TVector Max(TVector left, TVector right);

        /// <summary>
        /// Compares whether each left lane is less than its right lane.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The comparison mask.</returns>
        public static abstract TVector LessThan(TVector left, TVector right);

        /// <summary>
        /// Compares whether each left lane is less than or equal to its right lane.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The comparison mask.</returns>
        public static abstract TVector LessThanOrEqual(TVector left, TVector right);

        /// <summary>
        /// Selects lanes from two vectors according to a comparison mask.
        /// </summary>
        /// <param name="condition">The comparison mask.</param>
        /// <param name="left">The value selected for set mask lanes.</param>
        /// <param name="right">The value selected for clear mask lanes.</param>
        /// <returns>The selected lanes.</returns>
        public static abstract TVector ConditionalSelect(TVector condition, TVector left, TVector right);

        /// <summary>
        /// Computes the natural exponential of every vector lane.
        /// </summary>
        /// <param name="value">The input vector.</param>
        /// <returns>The lane-wise exponential.</returns>
        public static abstract TVector Exp(TVector value);

        /// <summary>
        /// Computes the natural logarithm of every vector lane.
        /// </summary>
        /// <param name="value">The input vector.</param>
        /// <returns>The lane-wise logarithm.</returns>
        public static abstract TVector Log(TVector value);

        /// <summary>
        /// Computes the square root of every vector lane.
        /// </summary>
        /// <param name="value">The input vector.</param>
        /// <returns>The lane-wise square root.</returns>
        public static abstract TVector Sqrt(TVector value);
    }

    /// <summary>
    /// Maps the shared transfer-function formulas to 128-bit vector operations.
    /// </summary>
    public readonly struct Vector128Operator : ITransferVectorOperator<Vector128<float>>
    {
        /// <inheritdoc/>
        public static Vector128<float> Create(float value) => Vector128.Create(value);

        /// <inheritdoc/>
        public static Vector128<float> Add(Vector128<float> left, Vector128<float> right) => left + right;

        /// <inheritdoc/>
        public static Vector128<float> Subtract(Vector128<float> left, Vector128<float> right) => left - right;

        /// <inheritdoc/>
        public static Vector128<float> Multiply(Vector128<float> left, Vector128<float> right) => left * right;

        /// <inheritdoc/>
        public static Vector128<float> MultiplyAddEstimate(Vector128<float> left, Vector128<float> right, Vector128<float> addend)
            => Vector128.MultiplyAddEstimate(left, right, addend);

        /// <inheritdoc/>
        public static Vector128<float> Divide(Vector128<float> left, Vector128<float> right) => left / right;

        /// <inheritdoc/>
        public static Vector128<float> Negate(Vector128<float> value) => -value;

        /// <inheritdoc/>
        public static Vector128<float> Min(Vector128<float> left, Vector128<float> right) => Vector128.Min(left, right);

        /// <inheritdoc/>
        public static Vector128<float> Max(Vector128<float> left, Vector128<float> right) => Vector128.Max(left, right);

        /// <inheritdoc/>
        public static Vector128<float> LessThan(Vector128<float> left, Vector128<float> right) => Vector128.LessThan(left, right);

        /// <inheritdoc/>
        public static Vector128<float> LessThanOrEqual(Vector128<float> left, Vector128<float> right) => Vector128.LessThanOrEqual(left, right);

        /// <inheritdoc/>
        public static Vector128<float> ConditionalSelect(Vector128<float> condition, Vector128<float> left, Vector128<float> right)
            => Vector128.ConditionalSelect(condition, left, right);

        /// <inheritdoc/>
        public static Vector128<float> Exp(Vector128<float> value) => Vector128.Exp(value);

        /// <inheritdoc/>
        public static Vector128<float> Log(Vector128<float> value) => Vector128.Log(value);

        /// <inheritdoc/>
        public static Vector128<float> Sqrt(Vector128<float> value) => Vector128.Sqrt(value);
    }

    /// <summary>
    /// Maps the shared transfer-function formulas to 256-bit vector operations.
    /// </summary>
    public readonly struct Vector256Operator : ITransferVectorOperator<Vector256<float>>
    {
        /// <inheritdoc/>
        public static Vector256<float> Create(float value) => Vector256.Create(value);

        /// <inheritdoc/>
        public static Vector256<float> Add(Vector256<float> left, Vector256<float> right) => left + right;

        /// <inheritdoc/>
        public static Vector256<float> Subtract(Vector256<float> left, Vector256<float> right) => left - right;

        /// <inheritdoc/>
        public static Vector256<float> Multiply(Vector256<float> left, Vector256<float> right) => left * right;

        /// <inheritdoc/>
        public static Vector256<float> MultiplyAddEstimate(Vector256<float> left, Vector256<float> right, Vector256<float> addend)
            => Vector256.MultiplyAddEstimate(left, right, addend);

        /// <inheritdoc/>
        public static Vector256<float> Divide(Vector256<float> left, Vector256<float> right) => left / right;

        /// <inheritdoc/>
        public static Vector256<float> Negate(Vector256<float> value) => -value;

        /// <inheritdoc/>
        public static Vector256<float> Min(Vector256<float> left, Vector256<float> right) => Vector256.Min(left, right);

        /// <inheritdoc/>
        public static Vector256<float> Max(Vector256<float> left, Vector256<float> right) => Vector256.Max(left, right);

        /// <inheritdoc/>
        public static Vector256<float> LessThan(Vector256<float> left, Vector256<float> right) => Vector256.LessThan(left, right);

        /// <inheritdoc/>
        public static Vector256<float> LessThanOrEqual(Vector256<float> left, Vector256<float> right) => Vector256.LessThanOrEqual(left, right);

        /// <inheritdoc/>
        public static Vector256<float> ConditionalSelect(Vector256<float> condition, Vector256<float> left, Vector256<float> right)
            => Vector256.ConditionalSelect(condition, left, right);

        /// <inheritdoc/>
        public static Vector256<float> Exp(Vector256<float> value) => Vector256.Exp(value);

        /// <inheritdoc/>
        public static Vector256<float> Log(Vector256<float> value) => Vector256.Log(value);

        /// <inheritdoc/>
        public static Vector256<float> Sqrt(Vector256<float> value) => Vector256.Sqrt(value);
    }

    /// <summary>
    /// Maps the shared transfer-function formulas to 512-bit vector operations.
    /// </summary>
    public readonly struct Vector512Operator : ITransferVectorOperator<Vector512<float>>
    {
        /// <inheritdoc/>
        public static Vector512<float> Create(float value) => Vector512.Create(value);

        /// <inheritdoc/>
        public static Vector512<float> Add(Vector512<float> left, Vector512<float> right) => left + right;

        /// <inheritdoc/>
        public static Vector512<float> Subtract(Vector512<float> left, Vector512<float> right) => left - right;

        /// <inheritdoc/>
        public static Vector512<float> Multiply(Vector512<float> left, Vector512<float> right) => left * right;

        /// <inheritdoc/>
        public static Vector512<float> MultiplyAddEstimate(Vector512<float> left, Vector512<float> right, Vector512<float> addend)
            => Vector512.MultiplyAddEstimate(left, right, addend);

        /// <inheritdoc/>
        public static Vector512<float> Divide(Vector512<float> left, Vector512<float> right) => left / right;

        /// <inheritdoc/>
        public static Vector512<float> Negate(Vector512<float> value) => -value;

        /// <inheritdoc/>
        public static Vector512<float> Min(Vector512<float> left, Vector512<float> right) => Vector512.Min(left, right);

        /// <inheritdoc/>
        public static Vector512<float> Max(Vector512<float> left, Vector512<float> right) => Vector512.Max(left, right);

        /// <inheritdoc/>
        public static Vector512<float> LessThan(Vector512<float> left, Vector512<float> right) => Vector512.LessThan(left, right);

        /// <inheritdoc/>
        public static Vector512<float> LessThanOrEqual(Vector512<float> left, Vector512<float> right) => Vector512.LessThanOrEqual(left, right);

        /// <inheritdoc/>
        public static Vector512<float> ConditionalSelect(Vector512<float> condition, Vector512<float> left, Vector512<float> right)
            => Vector512.ConditionalSelect(condition, left, right);

        /// <inheritdoc/>
        public static Vector512<float> Exp(Vector512<float> value) => Vector512.Exp(value);

        /// <inheritdoc/>
        public static Vector512<float> Log(Vector512<float> value) => Vector512.Log(value);

        /// <inheritdoc/>
        public static Vector512<float> Sqrt(Vector512<float> value) => Vector512.Sqrt(value);
    }
}
