// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using static SixLabors.ImageSharp.ColorProfiles.Companding.CompandingVectorOperations;

namespace SixLabors.ImageSharp.ColorProfiles.Companding;

/// <summary>
/// Applies the transfer characteristics defined by H.273.
/// </summary>
/// <remarks>
/// The transfer functions and their coefficients are defined in Section 8.2 and Table 3 of
/// <see href="https://www.itu.int/rec/T-REC-H.273-201612-S/en">ITU-T H.273 (2016)</see>.
/// </remarks>
internal static partial class CicpCompanding
{
    // The modifier separates H.273 code points in the existing companding cache. Each table
    // samples the same scalar curve as the direct path, including the H.273 coefficients.
    private static readonly Func<double, double, double> ExpandFunction = static (value, characteristic)
        => ToLinear((CicpTransferCharacteristics)(int)characteristic, (float)value);

    private static readonly Func<double, double, double> CompressFunction = static (value, characteristic)
        => ToGamma((CicpTransferCharacteristics)(int)characteristic, (float)value);

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
    /// The H.273 code 13 scale. Together with SrgbBeta it makes the curve continuous in value and slope.
    /// </summary>
    private const float SrgbAlpha = 1.0550107189475866F;

    /// <summary>
    /// The H.273 code 13 linear-domain junction. IEC sRGB instead uses its published rounded coefficients.
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
    /// Expands RGB with the H.273 curve. Eight-bit conversion uses bounded tables. Higher precision uses direct arithmetic.
    /// </summary>
    /// <param name="values">The colors to transform in place. Alpha remains unchanged.</param>
    /// <param name="transferCharacteristics">The signaled transfer curve.</param>
    /// <param name="bitDepth">The required precision per color component. Only eight-bit conversion uses lookup tables.</param>
    public static void Expand(Span<Vector4> values, CicpTransferCharacteristics transferCharacteristics, int bitDepth)
    {
        if (bitDepth == 8)
        {
            CompandingUtilities.Compand(values, CompandingUtilities.GetExpandLookupTable<CicpCompandingKey>(ExpandFunction, (int)transferCharacteristics));
        }
        else
        {
            ExpandExtended(values, transferCharacteristics);
        }
    }

    /// <summary>
    /// Expands RGB with the H.273 curve. Eight-bit conversion uses bounded tables. Higher precision uses direct arithmetic.
    /// </summary>
    /// <param name="value">The color to transform. Alpha remains unchanged.</param>
    /// <param name="transferCharacteristics">The signaled transfer curve.</param>
    /// <param name="bitDepth">The required precision per color component. Only eight-bit conversion uses lookup tables.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    public static Vector4 Expand(Vector4 value, CicpTransferCharacteristics transferCharacteristics, int bitDepth)
    {
        if (bitDepth == 8)
        {
            return CompandingUtilities.Compand(
                value,
                CompandingUtilities.GetExpandLookupTable<CicpCompandingKey>(ExpandFunction, (int)transferCharacteristics));
        }

        ExpandExtended(MemoryMarshal.CreateSpan(ref value, 1), transferCharacteristics);
        return value;
    }

    /// <summary>
    /// Compresses RGB with the H.273 curve. Eight-bit conversion uses bounded tables. Higher precision uses direct arithmetic.
    /// </summary>
    /// <param name="values">The colors to transform in place. Alpha remains unchanged.</param>
    /// <param name="transferCharacteristics">The signaled transfer curve.</param>
    /// <param name="bitDepth">The required precision per color component. Only eight-bit conversion uses lookup tables.</param>
    public static void Compress(Span<Vector4> values, CicpTransferCharacteristics transferCharacteristics, int bitDepth)
    {
        if (bitDepth == 8)
        {
            CompandingUtilities.Compand(values, CompandingUtilities.GetCompressLookupTable<CicpCompandingKey>(CompressFunction, (int)transferCharacteristics));
        }
        else
        {
            CompressExtended(values, transferCharacteristics);
        }
    }

    /// <summary>
    /// Compresses RGB with the H.273 curve. Eight-bit conversion uses bounded tables. Higher precision uses direct arithmetic.
    /// </summary>
    /// <param name="value">The color to transform. Alpha remains unchanged.</param>
    /// <param name="transferCharacteristics">The signaled transfer curve.</param>
    /// <param name="bitDepth">The required precision per color component. Only eight-bit conversion uses lookup tables.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    public static Vector4 Compress(Vector4 value, CicpTransferCharacteristics transferCharacteristics, int bitDepth)
    {
        if (bitDepth == 8)
        {
            return CompandingUtilities.Compand(
                value,
                CompandingUtilities.GetCompressLookupTable<CicpCompandingKey>(CompressFunction, (int)transferCharacteristics));
        }

        CompressExtended(MemoryMarshal.CreateSpan(ref value, 1), transferCharacteristics);
        return value;
    }

    /// <summary>
    /// Converts one nonlinear component with the signaled H.273 transfer curve.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristic.</param>
    /// <param name="value">The input component.</param>
    /// <returns>The converted component.</returns>
    public static float ToLinear(CicpTransferCharacteristics transferCharacteristics, float value)
    {
        switch (transferCharacteristics)
        {
            case CicpTransferCharacteristics.ItuRBt709_6:
            case CicpTransferCharacteristics.ItuRBt601_7:
            case CicpTransferCharacteristics.ItuRBt2020_2_10bit:
            case CicpTransferCharacteristics.ItuRBt2020_2_12bit:
                return Bt709Curve.Expand(value);
            case CicpTransferCharacteristics.Gamma2_2:
                return Gamma22Curve.Expand(value);
            case CicpTransferCharacteristics.Gamma2_8:
                return Gamma28Curve.Expand(value);
            case CicpTransferCharacteristics.SmpteSt240:
                return Smpte240Curve.Expand(value);
            case CicpTransferCharacteristics.Linear:
                return LinearCurve.Expand(value);
            case CicpTransferCharacteristics.Log100:
                return Log100Curve.Expand(value);
            case CicpTransferCharacteristics.Log100Sqrt:
                return Log100SqrtCurve.Expand(value);
            case CicpTransferCharacteristics.Iec61966_2_4:
                return Iec61966Curve.Expand(value);
            case CicpTransferCharacteristics.ItuRBt1361_0:
                return Bt1361Curve.Expand(value);
            case CicpTransferCharacteristics.Iec61966_2_1:
                return SrgbCurve.Expand(value);
            case CicpTransferCharacteristics.SmpteSt2084:
                return PqCurve.Expand(value);
            case CicpTransferCharacteristics.SmpteSt428_1:
                return Smpte428Curve.Expand(value);
            case CicpTransferCharacteristics.AribStdB67:
                return HlgCurve.Expand(value);
            default:
                // H.273 leaves unspecified and reserved values to the application. Retain the BT.709 default.
                return Bt709Curve.Expand(value);
        }
    }

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
    /// Expands RGB directly with the H.273 curve's range rules and retains alpha.
    /// </summary>
    /// <param name="values">The colors to transform in place.</param>
    /// <param name="transferCharacteristics">The transfer curve and its specified range rules.</param>
    public static void ExpandExtended(Span<Vector4> values, CicpTransferCharacteristics transferCharacteristics)
    {
        // Select one curve type for the complete span. Its operator calls the scalar or SIMD
        // arithmetic directly, without another transfer-characteristic switch inside the bulk loop.
        switch (transferCharacteristics)
        {
            case CicpTransferCharacteristics.ItuRBt709_6:
            case CicpTransferCharacteristics.ItuRBt601_7:
            case CicpTransferCharacteristics.ItuRBt2020_2_10bit:
            case CicpTransferCharacteristics.ItuRBt2020_2_12bit:
                TensorPrimitives_.Invoke(values, default(ExpandOperator<Bt709Curve>));
                break;
            case CicpTransferCharacteristics.Gamma2_2:
                TensorPrimitives_.Invoke(values, default(ExpandOperator<Gamma22Curve>));
                break;
            case CicpTransferCharacteristics.Gamma2_8:
                TensorPrimitives_.Invoke(values, default(ExpandOperator<Gamma28Curve>));
                break;
            case CicpTransferCharacteristics.SmpteSt240:
                TensorPrimitives_.Invoke(values, default(ExpandOperator<Smpte240Curve>));
                break;
            case CicpTransferCharacteristics.Linear:
                TensorPrimitives_.Invoke(values, default(ExpandOperator<LinearCurve>));
                break;
            case CicpTransferCharacteristics.Log100:
                TensorPrimitives_.Invoke(values, default(ExpandOperator<Log100Curve>));
                break;
            case CicpTransferCharacteristics.Log100Sqrt:
                TensorPrimitives_.Invoke(values, default(ExpandOperator<Log100SqrtCurve>));
                break;
            case CicpTransferCharacteristics.Iec61966_2_4:
                TensorPrimitives_.Invoke(values, default(ExpandOperator<Iec61966Curve>));
                break;
            case CicpTransferCharacteristics.ItuRBt1361_0:
                TensorPrimitives_.Invoke(values, default(ExpandOperator<Bt1361Curve>));
                break;
            case CicpTransferCharacteristics.Iec61966_2_1:
                TensorPrimitives_.Invoke(values, default(ExpandOperator<SrgbCurve>));
                break;
            case CicpTransferCharacteristics.SmpteSt2084:
                TensorPrimitives_.Invoke(values, default(ExpandOperator<PqCurve>));
                break;
            case CicpTransferCharacteristics.SmpteSt428_1:
                TensorPrimitives_.Invoke(values, default(ExpandOperator<Smpte428Curve>));
                break;
            case CicpTransferCharacteristics.AribStdB67:
                TensorPrimitives_.Invoke(values, default(ExpandOperator<HlgCurve>));
                break;
            default:
                TensorPrimitives_.Invoke(values, default(ExpandOperator<Bt709Curve>));
                break;
        }
    }

    /// <summary>
    /// Converts one linear component with the signaled H.273 transfer curve.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristic.</param>
    /// <param name="value">The input component.</param>
    /// <returns>The converted component.</returns>
    public static float ToGamma(CicpTransferCharacteristics transferCharacteristics, float value)
    {
        switch (transferCharacteristics)
        {
            case CicpTransferCharacteristics.ItuRBt709_6:
            case CicpTransferCharacteristics.ItuRBt601_7:
            case CicpTransferCharacteristics.ItuRBt2020_2_10bit:
            case CicpTransferCharacteristics.ItuRBt2020_2_12bit:
                return Bt709Curve.Compress(value);
            case CicpTransferCharacteristics.Gamma2_2:
                return Gamma22Curve.Compress(value);
            case CicpTransferCharacteristics.Gamma2_8:
                return Gamma28Curve.Compress(value);
            case CicpTransferCharacteristics.SmpteSt240:
                return Smpte240Curve.Compress(value);
            case CicpTransferCharacteristics.Linear:
                return LinearCurve.Compress(value);
            case CicpTransferCharacteristics.Log100:
                return Log100Curve.Compress(value);
            case CicpTransferCharacteristics.Log100Sqrt:
                return Log100SqrtCurve.Compress(value);
            case CicpTransferCharacteristics.Iec61966_2_4:
                return Iec61966Curve.Compress(value);
            case CicpTransferCharacteristics.ItuRBt1361_0:
                return Bt1361Curve.Compress(value);
            case CicpTransferCharacteristics.Iec61966_2_1:
                return SrgbCurve.Compress(value);
            case CicpTransferCharacteristics.SmpteSt2084:
                return PqCurve.Compress(value);
            case CicpTransferCharacteristics.SmpteSt428_1:
                return Smpte428Curve.Compress(value);
            case CicpTransferCharacteristics.AribStdB67:
                return HlgCurve.Compress(value);
            default:
                // H.273 leaves unspecified and reserved values to the application. Retain the BT.709 default.
                return Bt709Curve.Compress(value);
        }
    }

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
    /// Compresses RGB directly with the H.273 curve's range rules and retains alpha.
    /// </summary>
    /// <param name="values">The colors to transform in place.</param>
    /// <param name="transferCharacteristics">The transfer curve and its specified range rules.</param>
    public static void CompressExtended(Span<Vector4> values, CicpTransferCharacteristics transferCharacteristics)
    {
        // Select one curve type for the complete span. Its operator calls the scalar or SIMD
        // arithmetic directly, without another transfer-characteristic switch inside the bulk loop.
        switch (transferCharacteristics)
        {
            case CicpTransferCharacteristics.ItuRBt709_6:
            case CicpTransferCharacteristics.ItuRBt601_7:
            case CicpTransferCharacteristics.ItuRBt2020_2_10bit:
            case CicpTransferCharacteristics.ItuRBt2020_2_12bit:
                TensorPrimitives_.Invoke(values, default(CompressOperator<Bt709Curve>));
                break;
            case CicpTransferCharacteristics.Gamma2_2:
                TensorPrimitives_.Invoke(values, default(CompressOperator<Gamma22Curve>));
                break;
            case CicpTransferCharacteristics.Gamma2_8:
                TensorPrimitives_.Invoke(values, default(CompressOperator<Gamma28Curve>));
                break;
            case CicpTransferCharacteristics.SmpteSt240:
                TensorPrimitives_.Invoke(values, default(CompressOperator<Smpte240Curve>));
                break;
            case CicpTransferCharacteristics.Linear:
                TensorPrimitives_.Invoke(values, default(CompressOperator<LinearCurve>));
                break;
            case CicpTransferCharacteristics.Log100:
                TensorPrimitives_.Invoke(values, default(CompressOperator<Log100Curve>));
                break;
            case CicpTransferCharacteristics.Log100Sqrt:
                TensorPrimitives_.Invoke(values, default(CompressOperator<Log100SqrtCurve>));
                break;
            case CicpTransferCharacteristics.Iec61966_2_4:
                TensorPrimitives_.Invoke(values, default(CompressOperator<Iec61966Curve>));
                break;
            case CicpTransferCharacteristics.ItuRBt1361_0:
                TensorPrimitives_.Invoke(values, default(CompressOperator<Bt1361Curve>));
                break;
            case CicpTransferCharacteristics.Iec61966_2_1:
                TensorPrimitives_.Invoke(values, default(CompressOperator<SrgbCurve>));
                break;
            case CicpTransferCharacteristics.SmpteSt2084:
                TensorPrimitives_.Invoke(values, default(CompressOperator<PqCurve>));
                break;
            case CicpTransferCharacteristics.SmpteSt428_1:
                TensorPrimitives_.Invoke(values, default(CompressOperator<Smpte428Curve>));
                break;
            case CicpTransferCharacteristics.AribStdB67:
                TensorPrimitives_.Invoke(values, default(CompressOperator<HlgCurve>));
                break;
            default:
                TensorPrimitives_.Invoke(values, default(CompressOperator<Bt709Curve>));
                break;
        }
    }

    /// <summary>
    /// Separates H.273 tables from the other companders in the shared cache.
    /// </summary>
    private sealed class CicpCompandingKey;
}
