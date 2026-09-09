// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using static SixLabors.ImageSharp.Formats.Heif.Components.HeifTransferVectorOperations;

namespace SixLabors.ImageSharp.Formats.Heif.Components;

/// <content>
/// Provides fixed-width vector overloads and shared H.273 transfer operations for HEIF color conversion. One lane
/// represents one normalized color component. Closed vector operations implementations bind the 128-, 256-, or 512-bit implementation
/// once per row kernel, while conditional selection evaluates piecewise transfer curves without per-lane branches.
/// Inputs to logarithms and powers are bounded before evaluation because SIMD selection evaluates both branches.
/// </content>
internal static partial class HeifTransferFunctions
{
    /// <summary>
    /// Converts four nonlinear signal values to their H.273 linear-domain values.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The corresponding linear-domain values.</returns>
    public static Vector128<float> ToLinear(CicpTransferCharacteristics transferCharacteristics, Vector128<float> value)
        => ToLinear<Vector128<float>, Vector128Operations>(transferCharacteristics, value);

    /// <summary>
    /// Converts eight nonlinear signal values to their H.273 linear-domain values.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The corresponding linear-domain values.</returns>
    public static Vector256<float> ToLinear(CicpTransferCharacteristics transferCharacteristics, Vector256<float> value)
        => ToLinear<Vector256<float>, Vector256Operations>(transferCharacteristics, value);

    /// <summary>
    /// Converts sixteen nonlinear signal values to their H.273 linear-domain values.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The corresponding linear-domain values.</returns>
    public static Vector512<float> ToLinear(CicpTransferCharacteristics transferCharacteristics, Vector512<float> value)
        => ToLinear<Vector512<float>, Vector512Operations>(transferCharacteristics, value);

    /// <summary>
    /// Converts four linear signal values to their H.273 nonlinear-domain values.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The linear signal values.</param>
    /// <returns>The corresponding nonlinear-domain values.</returns>
    public static Vector128<float> ToGamma(CicpTransferCharacteristics transferCharacteristics, Vector128<float> value)
        => ToGamma<Vector128<float>, Vector128Operations>(transferCharacteristics, value);

    /// <summary>
    /// Converts eight linear signal values to their H.273 nonlinear-domain values.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The linear signal values.</param>
    /// <returns>The corresponding nonlinear-domain values.</returns>
    public static Vector256<float> ToGamma(CicpTransferCharacteristics transferCharacteristics, Vector256<float> value)
        => ToGamma<Vector256<float>, Vector256Operations>(transferCharacteristics, value);

    /// <summary>
    /// Converts sixteen linear signal values to their H.273 nonlinear-domain values.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The linear signal values.</param>
    /// <returns>The corresponding nonlinear-domain values.</returns>
    public static Vector512<float> ToGamma(CicpTransferCharacteristics transferCharacteristics, Vector512<float> value)
        => ToGamma<Vector512<float>, Vector512Operations>(transferCharacteristics, value);

    /// <summary>
    /// Converts nonlinear signal values to their H.273 linear-domain values using the selected SIMD width.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The corresponding linear-domain values.</returns>
    private static TVector ToLinear<TVector, TOperations>(CicpTransferCharacteristics transferCharacteristics, TVector value)
        where TVector : struct
        where TOperations : struct, ITransferVectorOperations<TVector>
    {
        TVector zero = TOperations.Create(0F);
        TVector one = TOperations.Create(1F);

        switch (transferCharacteristics)
        {
            case CicpTransferCharacteristics.ItuRBt709_6:
            case CicpTransferCharacteristics.ItuRBt601_7:
            case CicpTransferCharacteristics.ItuRBt2020_2_10bit:
            case CicpTransferCharacteristics.ItuRBt2020_2_12bit:
                return ToLinearBt709<TVector, TOperations>(value);
            case CicpTransferCharacteristics.Gamma2_2:
                return Power<TVector, TOperations>(TOperations.Min(TOperations.Max(value, zero), one), 2.2F);
            case CicpTransferCharacteristics.Gamma2_8:
                return Power<TVector, TOperations>(TOperations.Min(TOperations.Max(value, zero), one), 2.8F);
            case CicpTransferCharacteristics.SmpteSt240:
                return ToLinearSmpte240<TVector, TOperations>(value);
            case CicpTransferCharacteristics.Linear:
                return TOperations.Min(TOperations.Max(value, zero), one);
            case CicpTransferCharacteristics.Log100:
            {
                // H.273 assigns an interval to zero for logarithmic curves. The scalar midpoint convention is
                // selected lane-wise after evaluating the positive branch, which keeps the hot path branchless.
                TVector exponent = TOperations.Multiply(TOperations.Subtract(TOperations.Min(value, one), one), TOperations.Create(2F * 2.302585092994046F));
                TVector positive = TOperations.Exp(exponent);
                return TOperations.ConditionalSelect(TOperations.LessThanOrEqual(value, zero), TOperations.Create(0.005F), positive);
            }

            case CicpTransferCharacteristics.Log100Sqrt:
            {
                TVector exponent = TOperations.Multiply(TOperations.Subtract(TOperations.Min(value, one), one), TOperations.Create(2.5F * 2.302585092994046F));
                TVector positive = TOperations.Exp(exponent);
                return TOperations.ConditionalSelect(TOperations.LessThanOrEqual(value, zero), TOperations.Create(0.00158113883F), positive);
            }

            case CicpTransferCharacteristics.Iec61966_2_4:
                return ToLinearIec61966<TVector, TOperations>(value);
            case CicpTransferCharacteristics.ItuRBt1361_0:
                return ToLinearBt1361<TVector, TOperations>(value);
            case CicpTransferCharacteristics.Iec61966_2_1:
                return ToLinearSrgb<TVector, TOperations>(value);
            case CicpTransferCharacteristics.SmpteSt2084:
                return ToLinearPq<TVector, TOperations>(value);
            case CicpTransferCharacteristics.SmpteSt428_1:
                return TOperations.Divide(Power<TVector, TOperations>(TOperations.Max(value, zero), 2.6F), TOperations.Create(Smpte428Scale));
            case CicpTransferCharacteristics.AribStdB67:
                return ToLinearHlg<TVector, TOperations>(value);
            default:
                return ToLinearBt709<TVector, TOperations>(value);
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
    private static TVector ToGamma<TVector, TOperations>(CicpTransferCharacteristics transferCharacteristics, TVector value)
        where TVector : struct
        where TOperations : struct, ITransferVectorOperations<TVector>
    {
        TVector zero = TOperations.Create(0F);
        TVector one = TOperations.Create(1F);

        switch (transferCharacteristics)
        {
            case CicpTransferCharacteristics.ItuRBt709_6:
            case CicpTransferCharacteristics.ItuRBt601_7:
            case CicpTransferCharacteristics.ItuRBt2020_2_10bit:
            case CicpTransferCharacteristics.ItuRBt2020_2_12bit:
                return ToGammaBt709<TVector, TOperations>(value);
            case CicpTransferCharacteristics.Gamma2_2:
                return Power<TVector, TOperations>(TOperations.Min(TOperations.Max(value, zero), one), 1F / 2.2F);
            case CicpTransferCharacteristics.Gamma2_8:
                return Power<TVector, TOperations>(TOperations.Min(TOperations.Max(value, zero), one), 1F / 2.8F);
            case CicpTransferCharacteristics.SmpteSt240:
                return ToGammaSmpte240<TVector, TOperations>(value);
            case CicpTransferCharacteristics.Linear:
                return TOperations.Min(TOperations.Max(value, zero), one);
            case CicpTransferCharacteristics.Log100:
            {
                // Clamp inactive lanes to the threshold before Log. ConditionalSelect does not short-circuit,
                // so this prevents negative input lanes from contaminating the vector operation with NaN values.
                TVector threshold = TOperations.Create(0.01F);
                TVector bounded = TOperations.Min(TOperations.Max(value, threshold), one);
                TVector positive = TOperations.Add(one, TOperations.Divide(TOperations.Log(bounded), TOperations.Create(2F * 2.302585092994046F)));
                return TOperations.ConditionalSelect(TOperations.LessThanOrEqual(value, threshold), zero, positive);
            }

            case CicpTransferCharacteristics.Log100Sqrt:
            {
                TVector threshold = TOperations.Create(0.00316227766F);
                TVector bounded = TOperations.Min(TOperations.Max(value, threshold), one);
                TVector positive = TOperations.Add(one, TOperations.Divide(TOperations.Log(bounded), TOperations.Create(2.5F * 2.302585092994046F)));
                return TOperations.ConditionalSelect(TOperations.LessThanOrEqual(value, threshold), zero, positive);
            }

            case CicpTransferCharacteristics.Iec61966_2_4:
                return ToGammaIec61966<TVector, TOperations>(value);
            case CicpTransferCharacteristics.ItuRBt1361_0:
                return ToGammaBt1361<TVector, TOperations>(value);
            case CicpTransferCharacteristics.Iec61966_2_1:
                return ToGammaSrgb<TVector, TOperations>(value);
            case CicpTransferCharacteristics.SmpteSt2084:
                return ToGammaPq<TVector, TOperations>(value);
            case CicpTransferCharacteristics.SmpteSt428_1:
                return Power<TVector, TOperations>(TOperations.Multiply(TOperations.Create(Smpte428Scale), TOperations.Max(value, zero)), 1F / 2.6F);
            case CicpTransferCharacteristics.AribStdB67:
                return ToGammaHlg<TVector, TOperations>(value);
            default:
                return ToGammaBt709<TVector, TOperations>(value);
        }
    }

    /// <summary>
    /// Applies the inverse BT.709-family opto-electronic transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The linear signal values.</returns>
    private static TVector ToLinearBt709<TVector, TOperations>(TVector value)
        where TVector : struct
        where TOperations : struct, ITransferVectorOperations<TVector>
    {
        TVector zero = TOperations.Create(0F);
        TVector one = TOperations.Create(1F);
        TVector linear = TOperations.Divide(value, TOperations.Create(4.5F));
        TVector baseValue = TOperations.Divide(TOperations.Add(value, TOperations.Create(Bt709Alpha - 1F)), TOperations.Create(Bt709Alpha));
        TVector nonlinear = Power<TVector, TOperations>(TOperations.Max(baseValue, zero), 1F / 0.45F);
        TVector belowOne = TOperations.ConditionalSelect(TOperations.LessThan(value, TOperations.Create(4.5F * Bt709Beta)), linear, nonlinear);

        // The comparisons deliberately mirror the scalar ordering. This preserves the H.273 lower and upper
        // saturation rules while allowing all lanes to execute without data-dependent branches.
        TVector bounded = TOperations.ConditionalSelect(TOperations.LessThan(value, one), belowOne, one);
        return TOperations.ConditionalSelect(TOperations.LessThan(value, zero), zero, bounded);
    }

    /// <summary>
    /// Applies the BT.709-family opto-electronic transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The linear signal values.</param>
    /// <returns>The nonlinear signal values.</returns>
    private static TVector ToGammaBt709<TVector, TOperations>(TVector value)
        where TVector : struct
        where TOperations : struct, ITransferVectorOperations<TVector>
    {
        TVector zero = TOperations.Create(0F);
        TVector one = TOperations.Create(1F);
        TVector linear = TOperations.Multiply(value, TOperations.Create(4.5F));
        TVector nonlinear = TOperations.Subtract(
            TOperations.Multiply(TOperations.Create(Bt709Alpha), Power<TVector, TOperations>(TOperations.Max(value, zero), 0.45F)),
            TOperations.Create(Bt709Alpha - 1F));
        TVector belowOne = TOperations.ConditionalSelect(TOperations.LessThan(value, TOperations.Create(Bt709Beta)), linear, nonlinear);
        TVector bounded = TOperations.ConditionalSelect(TOperations.LessThan(value, one), belowOne, one);
        return TOperations.ConditionalSelect(TOperations.LessThan(value, zero), zero, bounded);
    }

    /// <summary>
    /// Applies the inverse SMPTE ST 240 opto-electronic transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The linear signal values.</returns>
    private static TVector ToLinearSmpte240<TVector, TOperations>(TVector value)
        where TVector : struct
        where TOperations : struct, ITransferVectorOperations<TVector>
    {
        TVector zero = TOperations.Create(0F);
        TVector one = TOperations.Create(1F);
        TVector linear = TOperations.Divide(value, TOperations.Create(4F));
        TVector baseValue = TOperations.Divide(TOperations.Add(value, TOperations.Create(Smpte240Alpha - 1F)), TOperations.Create(Smpte240Alpha));
        TVector nonlinear = Power<TVector, TOperations>(TOperations.Max(baseValue, zero), 1F / 0.45F);
        TVector belowOne = TOperations.ConditionalSelect(TOperations.LessThan(value, TOperations.Create(4F * Smpte240Beta)), linear, nonlinear);
        TVector bounded = TOperations.ConditionalSelect(TOperations.LessThan(value, one), belowOne, one);
        return TOperations.ConditionalSelect(TOperations.LessThan(value, zero), zero, bounded);
    }

    /// <summary>
    /// Applies the SMPTE ST 240 opto-electronic transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The linear signal values.</param>
    /// <returns>The nonlinear signal values.</returns>
    private static TVector ToGammaSmpte240<TVector, TOperations>(TVector value)
        where TVector : struct
        where TOperations : struct, ITransferVectorOperations<TVector>
    {
        TVector zero = TOperations.Create(0F);
        TVector one = TOperations.Create(1F);
        TVector linear = TOperations.Multiply(value, TOperations.Create(4F));
        TVector nonlinear = TOperations.Subtract(
            TOperations.Multiply(TOperations.Create(Smpte240Alpha), Power<TVector, TOperations>(TOperations.Max(value, zero), 0.45F)),
            TOperations.Create(Smpte240Alpha - 1F));
        TVector belowOne = TOperations.ConditionalSelect(TOperations.LessThan(value, TOperations.Create(Smpte240Beta)), linear, nonlinear);
        TVector bounded = TOperations.ConditionalSelect(TOperations.LessThan(value, one), belowOne, one);
        return TOperations.ConditionalSelect(TOperations.LessThan(value, zero), zero, bounded);
    }

    /// <summary>
    /// Applies the inverse extended IEC 61966-2-4 transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The linear signal values.</returns>
    private static TVector ToLinearIec61966<TVector, TOperations>(TVector value)
        where TVector : struct
        where TOperations : struct, ITransferVectorOperations<TVector>
    {
        TVector negativeBase = TOperations.Divide(TOperations.Subtract(value, TOperations.Create(Bt709Alpha - 1F)), TOperations.Create(-Bt709Alpha));
        TVector negative = TOperations.Negate(Power<TVector, TOperations>(TOperations.Max(negativeBase, TOperations.Create(0F)), 1F / 0.45F));
        TVector linear = TOperations.Divide(value, TOperations.Create(4.5F));
        TVector positiveBase = TOperations.Divide(TOperations.Add(value, TOperations.Create(Bt709Alpha - 1F)), TOperations.Create(Bt709Alpha));
        TVector positive = Power<TVector, TOperations>(TOperations.Max(positiveBase, TOperations.Create(0F)), 1F / 0.45F);
        TVector centerOrPositive = TOperations.ConditionalSelect(TOperations.LessThan(value, TOperations.Create(4.5F * Bt709Beta)), linear, positive);
        return TOperations.ConditionalSelect(TOperations.LessThan(value, TOperations.Create(-4.5F * Bt709Beta)), negative, centerOrPositive);
    }

    /// <summary>
    /// Applies the extended IEC 61966-2-4 transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The linear signal values.</param>
    /// <returns>The nonlinear signal values.</returns>
    private static TVector ToGammaIec61966<TVector, TOperations>(TVector value)
        where TVector : struct
        where TOperations : struct, ITransferVectorOperations<TVector>
    {
        TVector zero = TOperations.Create(0F);
        TVector negative = TOperations.Add(
            TOperations.Negate(TOperations.Multiply(
                TOperations.Create(Bt709Alpha),
                Power<TVector, TOperations>(TOperations.Max(TOperations.Negate(value), zero), 0.45F))),
            TOperations.Create(Bt709Alpha - 1F));

        TVector linear = TOperations.Multiply(value, TOperations.Create(4.5F));
        TVector positive = TOperations.Subtract(
            TOperations.Multiply(TOperations.Create(Bt709Alpha), Power<TVector, TOperations>(TOperations.Max(value, zero), 0.45F)),
            TOperations.Create(Bt709Alpha - 1F));
        TVector centerOrPositive = TOperations.ConditionalSelect(TOperations.LessThan(value, TOperations.Create(Bt709Beta)), linear, positive);
        return TOperations.ConditionalSelect(TOperations.LessThan(value, TOperations.Create(-Bt709Beta)), negative, centerOrPositive);
    }

    /// <summary>
    /// Applies the inverse extended BT.1361 transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The linear signal values.</returns>
    private static TVector ToLinearBt1361<TVector, TOperations>(TVector value)
        where TVector : struct
        where TOperations : struct, ITransferVectorOperations<TVector>
    {
        TVector zero = TOperations.Create(0F);
        TVector negativeBase = TOperations.Divide(TOperations.Subtract(value, TOperations.Create(0.02482420670236F)), TOperations.Create(-0.27482420670236F));
        TVector negative = TOperations.Divide(Power<TVector, TOperations>(TOperations.Max(negativeBase, zero), 1F / 0.45F), TOperations.Create(-4F));
        TVector negativeOrPositive = TOperations.ConditionalSelect(TOperations.LessThan(value, zero), negative, ToLinearBt709<TVector, TOperations>(value));
        return TOperations.ConditionalSelect(TOperations.LessThan(value, TOperations.Create(-0.25F)), TOperations.Create(-0.25F), negativeOrPositive);
    }

    /// <summary>
    /// Applies the extended BT.1361 transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The linear signal values.</param>
    /// <returns>The nonlinear signal values.</returns>
    private static TVector ToGammaBt1361<TVector, TOperations>(TVector value)
        where TVector : struct
        where TOperations : struct, ITransferVectorOperations<TVector>
    {
        TVector zero = TOperations.Create(0F);
        TVector negativePower = Power<TVector, TOperations>(TOperations.Max(TOperations.Multiply(TOperations.Create(-4F), value), zero), 0.45F);
        TVector negative = TOperations.Add(TOperations.Multiply(TOperations.Create(-0.27482420670236F), negativePower), TOperations.Create(0.02482420670236F));
        TVector negativeOrPositive = TOperations.ConditionalSelect(TOperations.LessThan(value, zero), negative, ToGammaBt709<TVector, TOperations>(value));
        return TOperations.ConditionalSelect(TOperations.LessThan(value, TOperations.Create(-0.25F)), TOperations.Create(-0.25F), negativeOrPositive);
    }

    /// <summary>
    /// Applies the inverse extended IEC 61966-2-1 transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The linear signal values.</returns>
    private static TVector ToLinearSrgb<TVector, TOperations>(TVector value)
        where TVector : struct
        where TOperations : struct, ITransferVectorOperations<TVector>
    {
        TVector zero = TOperations.Create(0F);
        TVector negativeBase = TOperations.Divide(TOperations.Subtract(value, TOperations.Create(SrgbAlpha - 1F)), TOperations.Create(-SrgbAlpha));
        TVector negative = TOperations.Negate(Power<TVector, TOperations>(TOperations.Max(negativeBase, zero), 2.4F));
        TVector linear = TOperations.Divide(value, TOperations.Create(12.92F));
        TVector positiveBase = TOperations.Divide(TOperations.Add(value, TOperations.Create(SrgbAlpha - 1F)), TOperations.Create(SrgbAlpha));
        TVector positive = Power<TVector, TOperations>(TOperations.Max(positiveBase, zero), 2.4F);
        TVector centerOrPositive = TOperations.ConditionalSelect(TOperations.LessThan(value, TOperations.Create(12.92F * SrgbBeta)), linear, positive);
        return TOperations.ConditionalSelect(TOperations.LessThan(value, TOperations.Create(-12.92F * SrgbBeta)), negative, centerOrPositive);
    }

    /// <summary>
    /// Applies the extended IEC 61966-2-1 transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The linear signal values.</param>
    /// <returns>The nonlinear signal values.</returns>
    private static TVector ToGammaSrgb<TVector, TOperations>(TVector value)
        where TVector : struct
        where TOperations : struct, ITransferVectorOperations<TVector>
    {
        TVector zero = TOperations.Create(0F);
        TVector negative = TOperations.Add(
            TOperations.Negate(TOperations.Multiply(
                TOperations.Create(SrgbAlpha),
                Power<TVector, TOperations>(TOperations.Max(TOperations.Negate(value), zero), 1F / 2.4F))),
            TOperations.Create(SrgbAlpha - 1F));

        TVector linear = TOperations.Multiply(value, TOperations.Create(12.92F));
        TVector positive = TOperations.Subtract(
            TOperations.Multiply(TOperations.Create(SrgbAlpha), Power<TVector, TOperations>(TOperations.Max(value, zero), 1F / 2.4F)),
            TOperations.Create(SrgbAlpha - 1F));
        TVector centerOrPositive = TOperations.ConditionalSelect(TOperations.LessThan(value, TOperations.Create(SrgbBeta)), linear, positive);
        return TOperations.ConditionalSelect(TOperations.LessThan(value, TOperations.Create(-SrgbBeta)), negative, centerOrPositive);
    }

    /// <summary>
    /// Applies the inverse SMPTE ST 2084 perceptual-quantizer transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The normalized linear signal values.</returns>
    private static TVector ToLinearPq<TVector, TOperations>(TVector value)
        where TVector : struct
        where TOperations : struct, ITransferVectorOperations<TVector>
    {
        TVector zero = TOperations.Create(0F);
        TVector nonlinearPower = Power<TVector, TOperations>(TOperations.Min(TOperations.Max(value, zero), TOperations.Create(1F)), 1F / PqM);
        TVector numerator = TOperations.Max(TOperations.Subtract(nonlinearPower, TOperations.Create(PqC1)), zero);
        TVector denominator = TOperations.Subtract(TOperations.Create(PqC2), TOperations.Multiply(TOperations.Create(PqC3), nonlinearPower));
        TVector positive = TOperations.Min(Power<TVector, TOperations>(TOperations.Divide(numerator, denominator), 1F / PqN), TOperations.Create(1F));
        return TOperations.ConditionalSelect(TOperations.LessThanOrEqual(value, zero), zero, positive);
    }

    /// <summary>
    /// Applies the SMPTE ST 2084 perceptual-quantizer transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The normalized linear signal values.</param>
    /// <returns>The nonlinear signal values.</returns>
    private static TVector ToGammaPq<TVector, TOperations>(TVector value)
        where TVector : struct
        where TOperations : struct, ITransferVectorOperations<TVector>
    {
        TVector zero = TOperations.Create(0F);
        TVector linearPower = Power<TVector, TOperations>(TOperations.Min(TOperations.Max(value, zero), TOperations.Create(1F)), PqN);
        TVector numerator = TOperations.Add(TOperations.Create(PqC1), TOperations.Multiply(TOperations.Create(PqC2), linearPower));
        TVector denominator = TOperations.Add(TOperations.Create(1F), TOperations.Multiply(TOperations.Create(PqC3), linearPower));
        TVector positive = TOperations.Min(Power<TVector, TOperations>(TOperations.Divide(numerator, denominator), PqM), TOperations.Create(1F));
        return TOperations.ConditionalSelect(TOperations.LessThanOrEqual(value, zero), zero, positive);
    }

    /// <summary>
    /// Applies the inverse HLG opto-electronic transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The normalized linear signal values.</returns>
    private static TVector ToLinearHlg<TVector, TOperations>(TVector value)
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
    /// Applies the HLG opto-electronic transfer function to a SIMD vector.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The normalized linear signal values.</param>
    /// <returns>The nonlinear signal values.</returns>
    private static TVector ToGammaHlg<TVector, TOperations>(TVector value)
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

    /// <summary>
    /// Raises nonnegative SIMD values to a scalar exponent.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    /// <typeparam name="TOperations">The operations for the SIMD vector type.</typeparam>
    /// <param name="value">The nonnegative base values.</param>
    /// <param name="exponent">The exponent applied to every lane.</param>
    /// <returns>The exponentiated values.</returns>
    private static TVector Power<TVector, TOperations>(TVector value, float exponent)
        where TVector : struct
        where TOperations : struct, ITransferVectorOperations<TVector>
    {
        // System.Numerics.Tensors does not currently vectorize Pow. Expressing positive powers as Exp(Log(x) * y)
        // uses the .NET 10 cross-platform vector math kernels and keeps all transfer-function lanes in SIMD.
        return TOperations.Exp(TOperations.Multiply(TOperations.Log(value), TOperations.Create(exponent)));
    }
}

/// <summary>
/// Contains the vector-width operations used by the shared H.273 transfer-function formulas.
/// </summary>
internal static class HeifTransferVectorOperations
{
    /// <summary>
    /// Defines the lane-wise operations required by the shared H.273 SIMD formulas.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    public interface ITransferVectorOperations<TVector>
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
    public readonly struct Vector128Operations : ITransferVectorOperations<Vector128<float>>
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
    public readonly struct Vector256Operations : ITransferVectorOperations<Vector256<float>>
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
    public readonly struct Vector512Operations : ITransferVectorOperations<Vector512<float>>
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
