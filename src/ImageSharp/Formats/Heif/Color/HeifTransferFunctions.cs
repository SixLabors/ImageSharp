// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Cicp;

namespace SixLabors.ImageSharp.Formats.Heif.Color;

/// <summary>
/// Applies the H.273 transfer characteristics used by HEIF color conversion.
/// </summary>
internal static partial class HeifTransferFunctions
{
    /// <summary>
    /// The BT.709 and BT.2020 nonlinear scale factor.
    /// </summary>
    private const float Bt709Alpha = 1.09929682680944F;

    /// <summary>
    /// The BT.709 and BT.2020 linear-domain transition point.
    /// </summary>
    private const float Bt709Beta = 0.018053968510807F;

    /// <summary>
    /// The SMPTE ST 240 nonlinear scale factor.
    /// </summary>
    private const float Smpte240Alpha = 1.111572195921731F;

    /// <summary>
    /// The SMPTE ST 240 linear-domain transition point.
    /// </summary>
    private const float Smpte240Beta = 0.022821585529445F;

    /// <summary>
    /// The sRGB nonlinear scale factor.
    /// </summary>
    private const float SrgbAlpha = 1.0550107189475866F;

    /// <summary>
    /// The sRGB linear-domain transition point.
    /// </summary>
    private const float SrgbBeta = 0.0030412825601275209F;

    /// <summary>
    /// The SMPTE ST 2084 first rational constant.
    /// </summary>
    private const float PqC1 = 0.8359375F;

    /// <summary>
    /// The SMPTE ST 2084 numerator scale.
    /// </summary>
    private const float PqC2 = 18.8515625F;

    /// <summary>
    /// The SMPTE ST 2084 denominator scale.
    /// </summary>
    private const float PqC3 = 18.6875F;

    /// <summary>
    /// The SMPTE ST 2084 outer exponent.
    /// </summary>
    private const float PqM = 78.84375F;

    /// <summary>
    /// The SMPTE ST 2084 inner exponent.
    /// </summary>
    private const float PqN = 0.1593017578125F;

    /// <summary>
    /// The SMPTE ST 428 luminance normalization factor.
    /// </summary>
    private const float Smpte428Scale = 0.91655527974030934F;

    /// <summary>
    /// The HLG logarithmic scale.
    /// </summary>
    private const float HlgA = 0.17883277F;

    /// <summary>
    /// The HLG logarithmic offset.
    /// </summary>
    private const float HlgB = 0.28466892F;

    /// <summary>
    /// The HLG output offset.
    /// </summary>
    private const float HlgC = 0.55991073F;

    /// <summary>
    /// Converts a nonlinear signal value to its H.273 linear-domain value.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The nonlinear signal value.</param>
    /// <returns>The corresponding linear-domain value.</returns>
    public static float ToLinear(CicpTransferCharacteristics transferCharacteristics, float value)
    {
        switch (transferCharacteristics)
        {
            case CicpTransferCharacteristics.ItuRBt709_6:
            case CicpTransferCharacteristics.ItuRBt601_7:
            case CicpTransferCharacteristics.ItuRBt2020_2_10bit:
            case CicpTransferCharacteristics.ItuRBt2020_2_12bit:
                return ToLinearBt709(value);
            case CicpTransferCharacteristics.Gamma2_2:
                return MathF.Pow(Math.Clamp(value, 0F, 1F), 2.2F);
            case CicpTransferCharacteristics.Gamma2_8:
                return MathF.Pow(Math.Clamp(value, 0F, 1F), 2.8F);
            case CicpTransferCharacteristics.SmpteSt240:
                return ToLinearSmpte240(value);
            case CicpTransferCharacteristics.Linear:
                return Math.Clamp(value, 0F, 1F);
            case CicpTransferCharacteristics.Log100:
                // Zero represents an interval rather than one linear value. The midpoint matches libavif and
                // minimizes the worst-case round-trip error when constant-luminance content is decoded.
                return value <= 0F ? 0.005F : MathF.Pow(10F, 2F * (MathF.Min(value, 1F) - 1F));
            case CicpTransferCharacteristics.Log100Sqrt:
                return value <= 0F ? 0.00158113883F : MathF.Pow(10F, 2.5F * (MathF.Min(value, 1F) - 1F));
            case CicpTransferCharacteristics.Iec61966_2_4:
                return ToLinearIec61966(value);
            case CicpTransferCharacteristics.ItuRBt1361_0:
                return ToLinearBt1361(value);
            case CicpTransferCharacteristics.Iec61966_2_1:
                return ToLinearSrgb(value);
            case CicpTransferCharacteristics.SmpteSt2084:
                return ToLinearPq(value);
            case CicpTransferCharacteristics.SmpteSt428_1:
                return MathF.Pow(MathF.Max(value, 0F), 2.6F) / Smpte428Scale;
            case CicpTransferCharacteristics.AribStdB67:
                return ToLinearHlg(value);
            default:
                // H.273 leaves unspecified and reserved transfer values to the application. Match libavif's
                // deterministic BT.709 fallback for still-image conversion.
                return ToLinearBt709(value);
        }
    }

    /// <summary>
    /// Converts a linear signal value to its H.273 nonlinear-domain value.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The linear signal value.</param>
    /// <returns>The corresponding nonlinear-domain value.</returns>
    public static float ToGamma(CicpTransferCharacteristics transferCharacteristics, float value)
    {
        switch (transferCharacteristics)
        {
            case CicpTransferCharacteristics.ItuRBt709_6:
            case CicpTransferCharacteristics.ItuRBt601_7:
            case CicpTransferCharacteristics.ItuRBt2020_2_10bit:
            case CicpTransferCharacteristics.ItuRBt2020_2_12bit:
                return ToGammaBt709(value);
            case CicpTransferCharacteristics.Gamma2_2:
                return MathF.Pow(Math.Clamp(value, 0F, 1F), 1F / 2.2F);
            case CicpTransferCharacteristics.Gamma2_8:
                return MathF.Pow(Math.Clamp(value, 0F, 1F), 1F / 2.8F);
            case CicpTransferCharacteristics.SmpteSt240:
                return ToGammaSmpte240(value);
            case CicpTransferCharacteristics.Linear:
                return Math.Clamp(value, 0F, 1F);
            case CicpTransferCharacteristics.Log100:
                return value <= 0.01F ? 0F : 1F + (MathF.Log10(MathF.Min(value, 1F)) / 2F);
            case CicpTransferCharacteristics.Log100Sqrt:
                return value <= 0.00316227766F ? 0F : 1F + (MathF.Log10(MathF.Min(value, 1F)) / 2.5F);
            case CicpTransferCharacteristics.Iec61966_2_4:
                return ToGammaIec61966(value);
            case CicpTransferCharacteristics.ItuRBt1361_0:
                return ToGammaBt1361(value);
            case CicpTransferCharacteristics.Iec61966_2_1:
                return ToGammaSrgb(value);
            case CicpTransferCharacteristics.SmpteSt2084:
                return ToGammaPq(value);
            case CicpTransferCharacteristics.SmpteSt428_1:
                return MathF.Pow(Smpte428Scale * MathF.Max(value, 0F), 1F / 2.6F);
            case CicpTransferCharacteristics.AribStdB67:
                return ToGammaHlg(value);
            default:
                return ToGammaBt709(value);
        }
    }

    /// <summary>
    /// Applies the inverse BT.709-family opto-electronic transfer function.
    /// </summary>
    /// <param name="value">The nonlinear signal value.</param>
    /// <returns>The linear signal value.</returns>
    private static float ToLinearBt709(float value)
    {
        if (value < 0F)
        {
            return 0F;
        }

        if (value < 4.5F * Bt709Beta)
        {
            return value / 4.5F;
        }

        return value < 1F
            ? MathF.Pow((value + (Bt709Alpha - 1F)) / Bt709Alpha, 1F / 0.45F)
            : 1F;
    }

    /// <summary>
    /// Applies the BT.709-family opto-electronic transfer function.
    /// </summary>
    /// <param name="value">The linear signal value.</param>
    /// <returns>The nonlinear signal value.</returns>
    private static float ToGammaBt709(float value)
    {
        if (value < 0F)
        {
            return 0F;
        }

        if (value < Bt709Beta)
        {
            return value * 4.5F;
        }

        return value < 1F
            ? (Bt709Alpha * MathF.Pow(value, 0.45F)) - (Bt709Alpha - 1F)
            : 1F;
    }

    /// <summary>
    /// Applies the inverse SMPTE ST 240 opto-electronic transfer function.
    /// </summary>
    /// <param name="value">The nonlinear signal value.</param>
    /// <returns>The linear signal value.</returns>
    private static float ToLinearSmpte240(float value)
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
            ? MathF.Pow((value + (Smpte240Alpha - 1F)) / Smpte240Alpha, 1F / 0.45F)
            : 1F;
    }

    /// <summary>
    /// Applies the SMPTE ST 240 opto-electronic transfer function.
    /// </summary>
    /// <param name="value">The linear signal value.</param>
    /// <returns>The nonlinear signal value.</returns>
    private static float ToGammaSmpte240(float value)
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
            ? (Smpte240Alpha * MathF.Pow(value, 0.45F)) - (Smpte240Alpha - 1F)
            : 1F;
    }

    /// <summary>
    /// Applies the inverse extended IEC 61966-2-4 transfer function.
    /// </summary>
    /// <param name="value">The nonlinear signal value.</param>
    /// <returns>The linear signal value.</returns>
    private static float ToLinearIec61966(float value)
    {
        if (value < -4.5F * Bt709Beta)
        {
            return -MathF.Pow((value - (Bt709Alpha - 1F)) / -Bt709Alpha, 1F / 0.45F);
        }

        return value < 4.5F * Bt709Beta
            ? value / 4.5F
            : MathF.Pow((value + (Bt709Alpha - 1F)) / Bt709Alpha, 1F / 0.45F);
    }

    /// <summary>
    /// Applies the extended IEC 61966-2-4 transfer function.
    /// </summary>
    /// <param name="value">The linear signal value.</param>
    /// <returns>The nonlinear signal value.</returns>
    private static float ToGammaIec61966(float value)
    {
        if (value < -Bt709Beta)
        {
            return (-Bt709Alpha * MathF.Pow(-value, 0.45F)) + (Bt709Alpha - 1F);
        }

        return value < Bt709Beta
            ? value * 4.5F
            : (Bt709Alpha * MathF.Pow(value, 0.45F)) - (Bt709Alpha - 1F);
    }

    /// <summary>
    /// Applies the inverse extended BT.1361 transfer function.
    /// </summary>
    /// <param name="value">The nonlinear signal value.</param>
    /// <returns>The linear signal value.</returns>
    private static float ToLinearBt1361(float value)
    {
        if (value < -0.25F)
        {
            return -0.25F;
        }

        if (value < 0F)
        {
            return MathF.Pow((value - 0.02482420670236F) / -0.27482420670236F, 1F / 0.45F) / -4F;
        }

        return ToLinearBt709(value);
    }

    /// <summary>
    /// Applies the extended BT.1361 transfer function.
    /// </summary>
    /// <param name="value">The linear signal value.</param>
    /// <returns>The nonlinear signal value.</returns>
    private static float ToGammaBt1361(float value)
    {
        if (value < -0.25F)
        {
            return -0.25F;
        }

        if (value < 0F)
        {
            return (-0.27482420670236F * MathF.Pow(-4F * value, 0.45F)) + 0.02482420670236F;
        }

        return ToGammaBt709(value);
    }

    /// <summary>
    /// Applies the inverse extended IEC 61966-2-1 transfer function.
    /// </summary>
    /// <param name="value">The nonlinear signal value.</param>
    /// <returns>The linear signal value.</returns>
    private static float ToLinearSrgb(float value)
    {
        if (value < -12.92F * SrgbBeta)
        {
            return -MathF.Pow((value - (SrgbAlpha - 1F)) / -SrgbAlpha, 2.4F);
        }

        return value < 12.92F * SrgbBeta
            ? value / 12.92F
            : MathF.Pow((value + (SrgbAlpha - 1F)) / SrgbAlpha, 2.4F);
    }

    /// <summary>
    /// Applies the extended IEC 61966-2-1 transfer function.
    /// </summary>
    /// <param name="value">The linear signal value.</param>
    /// <returns>The nonlinear signal value.</returns>
    private static float ToGammaSrgb(float value)
    {
        if (value < -SrgbBeta)
        {
            return (-SrgbAlpha * MathF.Pow(-value, 1F / 2.4F)) + (SrgbAlpha - 1F);
        }

        return value < SrgbBeta
            ? value * 12.92F
            : (SrgbAlpha * MathF.Pow(value, 1F / 2.4F)) - (SrgbAlpha - 1F);
    }

    /// <summary>
    /// Applies the inverse SMPTE ST 2084 perceptual-quantizer transfer function.
    /// </summary>
    /// <param name="value">The nonlinear signal value.</param>
    /// <returns>The normalized linear signal value.</returns>
    private static float ToLinearPq(float value)
    {
        if (value <= 0F)
        {
            return 0F;
        }

        float nonlinearPower = MathF.Pow(MathF.Min(value, 1F), 1F / PqM);
        float numerator = MathF.Max(nonlinearPower - PqC1, 0F);
        float denominator = PqC2 - (PqC3 * nonlinearPower);
        return MathF.Pow(numerator / denominator, 1F / PqN);
    }

    /// <summary>
    /// Applies the SMPTE ST 2084 perceptual-quantizer transfer function.
    /// </summary>
    /// <param name="value">The normalized linear signal value.</param>
    /// <returns>The nonlinear signal value.</returns>
    private static float ToGammaPq(float value)
    {
        if (value <= 0F)
        {
            return 0F;
        }

        float linearPower = MathF.Pow(MathF.Min(value, 1F), PqN);
        return MathF.Pow((PqC1 + (PqC2 * linearPower)) / (1F + (PqC3 * linearPower)), PqM);
    }

    /// <summary>
    /// Applies the inverse HLG opto-electronic transfer function.
    /// </summary>
    /// <param name="value">The nonlinear signal value.</param>
    /// <returns>The normalized linear signal value.</returns>
    private static float ToLinearHlg(float value)
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
    /// Applies the HLG opto-electronic transfer function.
    /// </summary>
    /// <param name="value">The normalized linear signal value.</param>
    /// <returns>The nonlinear signal value.</returns>
    private static float ToGammaHlg(float value)
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
}
