// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.ColorProfiles.Companding;

/// <summary>
/// Applies the transfer characteristics defined by H.273.
/// </summary>
/// <remarks>
/// Inputs use the selected H.273 curve's normalized component units, not integer sample values or video-range offsets.
/// Expand and ToLinear accept nonlinear input. Compress and ToGamma accept linear input.
/// SDR inputs use [0,1]. Extended inputs use [-65504,65504] in the same normalized units.
/// APIs without a colorRange parameter expect extended inputs, subject to the selected curve's domain restrictions.
/// PQ and HLG restrict both linear and nonlinear components to [0,1], including when the selected range is extended.
/// RGB results are clamped to the selected normalized range. RGBA APIs retain alpha.
/// SIMD component APIs treat every lane as color data, including the fourth lane.
/// After curve evaluation, NaN results map to the lower bound; infinite results map to the corresponding bounds.
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
    /// Expands RGB with the H.273 curve. Eight-bit SDR conversion uses tables. Other ranges and precisions use direct arithmetic.
    /// </summary>
    /// <param name="values">The normalized nonlinear RGB colors. Each vector contains RGB in X, Y, Z and unchanged alpha in W.</param>
    /// <param name="transferCharacteristics">The signaled transfer curve.</param>
    /// <param name="bitDepth">The required precision per color component.</param>
    /// <param name="colorRange">The expected normalized RGB input range and final result limits. The H.273 curve can impose narrower limits.</param>
    public static void Expand(Span<Vector4> values, CicpTransferCharacteristics transferCharacteristics, int bitDepth, PixelColorRange colorRange)
    {
        // H.273 includes power and logarithmic curves that lose precision with table interpolation.
        // Retain the eight-bit SDR limit until each curve is verified at higher precision.
        if (bitDepth == 8 && colorRange == PixelColorRange.Sdr)
        {
            CompandingUtilities.Compand(values, CompandingUtilities.GetExpandLookupTable<CicpCompandingKey>(ExpandFunction, (int)transferCharacteristics));
        }
        else
        {
            ExpandCore(values, transferCharacteristics, colorRange);
        }
    }

    /// <summary>
    /// Expands RGB with the H.273 curve. Eight-bit SDR conversion uses tables. Other ranges and precisions use direct arithmetic.
    /// </summary>
    /// <param name="value">The normalized nonlinear RGB color. X, Y, Z contain RGB; W contains unchanged alpha.</param>
    /// <param name="transferCharacteristics">The signaled transfer curve.</param>
    /// <param name="bitDepth">The required precision per color component.</param>
    /// <param name="colorRange">The expected normalized RGB input range and final result limits. The H.273 curve can impose narrower limits.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    public static Vector4 Expand(Vector4 value, CicpTransferCharacteristics transferCharacteristics, int bitDepth, PixelColorRange colorRange)
    {
        // H.273 includes power and logarithmic curves that lose precision with table interpolation.
        // Retain the eight-bit SDR limit until each curve is verified at higher precision.
        if (bitDepth == 8 && colorRange == PixelColorRange.Sdr)
        {
            return CompandingUtilities.Compand(
                value,
                CompandingUtilities.GetExpandLookupTable<CicpCompandingKey>(ExpandFunction, (int)transferCharacteristics));
        }

        ExpandCore(MemoryMarshal.CreateSpan(ref value, 1), transferCharacteristics, colorRange);
        return value;
    }

    /// <summary>
    /// Compresses RGB with the H.273 curve. Eight-bit SDR conversion uses tables. Other ranges and precisions use direct arithmetic.
    /// </summary>
    /// <param name="values">The normalized linear RGB colors. Each vector contains RGB in X, Y, Z and unchanged alpha in W.</param>
    /// <param name="transferCharacteristics">The signaled transfer curve.</param>
    /// <param name="bitDepth">The required precision per color component.</param>
    /// <param name="colorRange">The expected normalized RGB input range and final result limits. The H.273 curve can impose narrower limits.</param>
    public static void Compress(Span<Vector4> values, CicpTransferCharacteristics transferCharacteristics, int bitDepth, PixelColorRange colorRange)
    {
        // H.273 includes power and logarithmic curves that lose precision with table interpolation.
        // Retain the eight-bit SDR limit until each curve is verified at higher precision.
        if (bitDepth == 8 && colorRange == PixelColorRange.Sdr)
        {
            CompandingUtilities.Compand(values, CompandingUtilities.GetCompressLookupTable<CicpCompandingKey>(CompressFunction, (int)transferCharacteristics));
        }
        else
        {
            CompressCore(values, transferCharacteristics, colorRange);
        }
    }

    /// <summary>
    /// Compresses RGB with the H.273 curve. Eight-bit SDR conversion uses tables. Other ranges and precisions use direct arithmetic.
    /// </summary>
    /// <param name="value">The normalized linear RGB color. X, Y, Z contain RGB; W contains unchanged alpha.</param>
    /// <param name="transferCharacteristics">The signaled transfer curve.</param>
    /// <param name="bitDepth">The required precision per color component.</param>
    /// <param name="colorRange">The expected normalized RGB input range and final result limits. The H.273 curve can impose narrower limits.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    public static Vector4 Compress(Vector4 value, CicpTransferCharacteristics transferCharacteristics, int bitDepth, PixelColorRange colorRange)
    {
        // H.273 includes power and logarithmic curves that lose precision with table interpolation.
        // Retain the eight-bit SDR limit until each curve is verified at higher precision.
        if (bitDepth == 8 && colorRange == PixelColorRange.Sdr)
        {
            return CompandingUtilities.Compand(
                value,
                CompandingUtilities.GetCompressLookupTable<CicpCompandingKey>(CompressFunction, (int)transferCharacteristics));
        }

        CompressCore(MemoryMarshal.CreateSpan(ref value, 1), transferCharacteristics, colorRange);
        return value;
    }

    /// <summary>
    /// Converts one nonlinear component with the signaled H.273 transfer curve.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristic.</param>
    /// <param name="value">The normalized nonlinear component in the selected H.273 curve's domain.</param>
    /// <returns>The converted component.</returns>
    public static float ToLinear(CicpTransferCharacteristics transferCharacteristics, float value)
    {
        switch (transferCharacteristics)
        {
            case CicpTransferCharacteristics.ItuRBt709_6:
            case CicpTransferCharacteristics.ItuRBt601_7:
            case CicpTransferCharacteristics.ItuRBt2020_2_10bit:
            case CicpTransferCharacteristics.ItuRBt2020_2_12bit:
                return CompandingUtilities.Compand(value, default(ExpandOperator<Bt709Curve>), PixelColorRange.Extended);
            case CicpTransferCharacteristics.Gamma2_2:
                return CompandingUtilities.Compand(value, default(ExpandOperator<Gamma22Curve>), PixelColorRange.Extended);
            case CicpTransferCharacteristics.Gamma2_8:
                return CompandingUtilities.Compand(value, default(ExpandOperator<Gamma28Curve>), PixelColorRange.Extended);
            case CicpTransferCharacteristics.SmpteSt240:
                return CompandingUtilities.Compand(value, default(ExpandOperator<Smpte240Curve>), PixelColorRange.Extended);
            case CicpTransferCharacteristics.Linear:
                return CompandingUtilities.Compand(value, default(ExpandOperator<LinearCurve>), PixelColorRange.Extended);
            case CicpTransferCharacteristics.Log100:
                return CompandingUtilities.Compand(value, default(ExpandOperator<Log100Curve>), PixelColorRange.Extended);
            case CicpTransferCharacteristics.Log100Sqrt:
                return CompandingUtilities.Compand(value, default(ExpandOperator<Log100SqrtCurve>), PixelColorRange.Extended);
            case CicpTransferCharacteristics.Iec61966_2_4:
                return CompandingUtilities.Compand(value, default(ExpandOperator<Iec61966Curve>), PixelColorRange.Extended);
            case CicpTransferCharacteristics.ItuRBt1361_0:
                return CompandingUtilities.Compand(value, default(ExpandOperator<Bt1361Curve>), PixelColorRange.Extended);
            case CicpTransferCharacteristics.Iec61966_2_1:
                return CompandingUtilities.Compand(value, default(ExpandOperator<SrgbCurve>), PixelColorRange.Extended);
            case CicpTransferCharacteristics.SmpteSt2084:
                return CompandingUtilities.Compand(value, default(ExpandOperator<PqCurve>), PixelColorRange.Extended);
            case CicpTransferCharacteristics.SmpteSt428_1:
                return CompandingUtilities.Compand(value, default(ExpandOperator<Smpte428Curve>), PixelColorRange.Extended);
            case CicpTransferCharacteristics.AribStdB67:
                return CompandingUtilities.Compand(value, default(ExpandOperator<HlgCurve>), PixelColorRange.Extended);
            default:
                // H.273 leaves unspecified and reserved values to the application. Retain the BT.709 default.
                return CompandingUtilities.Compand(value, default(ExpandOperator<Bt709Curve>), PixelColorRange.Extended);
        }
    }

    /// <summary>
    /// Converts four nonlinear signal values to their H.273 linear-domain values.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The normalized nonlinear components in the selected H.273 curve's domain. Every SIMD lane is a color component.</param>
    /// <returns>The corresponding linear-domain values.</returns>
    public static Vector128<float> ToLinear(CicpTransferCharacteristics transferCharacteristics, Vector128<float> value)
        => ToLinearCore(transferCharacteristics, value);

    /// <summary>
    /// Converts eight nonlinear signal values to their H.273 linear-domain values.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The normalized nonlinear components in the selected H.273 curve's domain. Every SIMD lane is a color component.</param>
    /// <returns>The corresponding linear-domain values.</returns>
    public static Vector256<float> ToLinear(CicpTransferCharacteristics transferCharacteristics, Vector256<float> value)
        => ToLinearCore(transferCharacteristics, value);

    /// <summary>
    /// Converts sixteen nonlinear signal values to their H.273 linear-domain values.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The normalized nonlinear components in the selected H.273 curve's domain. Every SIMD lane is a color component.</param>
    /// <returns>The corresponding linear-domain values.</returns>
    public static Vector512<float> ToLinear(CicpTransferCharacteristics transferCharacteristics, Vector512<float> value)
        => ToLinearCore(transferCharacteristics, value);

    /// <summary>
    /// Expands RGB directly with the H.273 curve's range rules and retains alpha.
    /// </summary>
    /// <param name="values">The normalized nonlinear RGB colors. Each vector contains RGB in X, Y, Z and unchanged alpha in W.</param>
    /// <param name="transferCharacteristics">The transfer curve and its specified range rules.</param>
    public static void ExpandExtended(Span<Vector4> values, CicpTransferCharacteristics transferCharacteristics)
        => ExpandCore(values, transferCharacteristics, PixelColorRange.Extended);

    /// <summary>
    /// Selects one transfer curve and applies the requested RGB result range.
    /// </summary>
    /// <param name="values">The colors to transform. Alpha remains unchanged.</param>
    /// <param name="transferCharacteristics">The transfer curve.</param>
    /// <param name="colorRange">The range applied to the converted RGB components.</param>
    private static void ExpandCore(Span<Vector4> values, CicpTransferCharacteristics transferCharacteristics, PixelColorRange colorRange)
    {
        // Select one curve type for the complete span. Its operator calls the scalar or SIMD
        // arithmetic directly, without another transfer-characteristic switch inside the bulk loop.
        switch (transferCharacteristics)
        {
            case CicpTransferCharacteristics.ItuRBt709_6:
            case CicpTransferCharacteristics.ItuRBt601_7:
            case CicpTransferCharacteristics.ItuRBt2020_2_10bit:
            case CicpTransferCharacteristics.ItuRBt2020_2_12bit:
                CompandingUtilities.Compand(values, default(ExpandOperator<Bt709Curve>), colorRange);
                break;
            case CicpTransferCharacteristics.Gamma2_2:
                CompandingUtilities.Compand(values, default(ExpandOperator<Gamma22Curve>), colorRange);
                break;
            case CicpTransferCharacteristics.Gamma2_8:
                CompandingUtilities.Compand(values, default(ExpandOperator<Gamma28Curve>), colorRange);
                break;
            case CicpTransferCharacteristics.SmpteSt240:
                CompandingUtilities.Compand(values, default(ExpandOperator<Smpte240Curve>), colorRange);
                break;
            case CicpTransferCharacteristics.Linear:
                CompandingUtilities.Compand(values, default(ExpandOperator<LinearCurve>), colorRange);
                break;
            case CicpTransferCharacteristics.Log100:
                CompandingUtilities.Compand(values, default(ExpandOperator<Log100Curve>), colorRange);
                break;
            case CicpTransferCharacteristics.Log100Sqrt:
                CompandingUtilities.Compand(values, default(ExpandOperator<Log100SqrtCurve>), colorRange);
                break;
            case CicpTransferCharacteristics.Iec61966_2_4:
                CompandingUtilities.Compand(values, default(ExpandOperator<Iec61966Curve>), colorRange);
                break;
            case CicpTransferCharacteristics.ItuRBt1361_0:
                CompandingUtilities.Compand(values, default(ExpandOperator<Bt1361Curve>), colorRange);
                break;
            case CicpTransferCharacteristics.Iec61966_2_1:
                CompandingUtilities.Compand(values, default(ExpandOperator<SrgbCurve>), colorRange);
                break;
            case CicpTransferCharacteristics.SmpteSt2084:
                CompandingUtilities.Compand(values, default(ExpandOperator<PqCurve>), colorRange);
                break;
            case CicpTransferCharacteristics.SmpteSt428_1:
                CompandingUtilities.Compand(values, default(ExpandOperator<Smpte428Curve>), colorRange);
                break;
            case CicpTransferCharacteristics.AribStdB67:
                CompandingUtilities.Compand(values, default(ExpandOperator<HlgCurve>), colorRange);
                break;
            default:
                CompandingUtilities.Compand(values, default(ExpandOperator<Bt709Curve>), colorRange);
                break;
        }
    }

    /// <summary>
    /// Converts one linear component with the signaled H.273 transfer curve.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristic.</param>
    /// <param name="value">The normalized linear component in the selected H.273 curve's domain.</param>
    /// <returns>The converted component.</returns>
    public static float ToGamma(CicpTransferCharacteristics transferCharacteristics, float value)
    {
        switch (transferCharacteristics)
        {
            case CicpTransferCharacteristics.ItuRBt709_6:
            case CicpTransferCharacteristics.ItuRBt601_7:
            case CicpTransferCharacteristics.ItuRBt2020_2_10bit:
            case CicpTransferCharacteristics.ItuRBt2020_2_12bit:
                return CompandingUtilities.Compand(value, default(CompressOperator<Bt709Curve>), PixelColorRange.Extended);
            case CicpTransferCharacteristics.Gamma2_2:
                return CompandingUtilities.Compand(value, default(CompressOperator<Gamma22Curve>), PixelColorRange.Extended);
            case CicpTransferCharacteristics.Gamma2_8:
                return CompandingUtilities.Compand(value, default(CompressOperator<Gamma28Curve>), PixelColorRange.Extended);
            case CicpTransferCharacteristics.SmpteSt240:
                return CompandingUtilities.Compand(value, default(CompressOperator<Smpte240Curve>), PixelColorRange.Extended);
            case CicpTransferCharacteristics.Linear:
                return CompandingUtilities.Compand(value, default(CompressOperator<LinearCurve>), PixelColorRange.Extended);
            case CicpTransferCharacteristics.Log100:
                return CompandingUtilities.Compand(value, default(CompressOperator<Log100Curve>), PixelColorRange.Extended);
            case CicpTransferCharacteristics.Log100Sqrt:
                return CompandingUtilities.Compand(value, default(CompressOperator<Log100SqrtCurve>), PixelColorRange.Extended);
            case CicpTransferCharacteristics.Iec61966_2_4:
                return CompandingUtilities.Compand(value, default(CompressOperator<Iec61966Curve>), PixelColorRange.Extended);
            case CicpTransferCharacteristics.ItuRBt1361_0:
                return CompandingUtilities.Compand(value, default(CompressOperator<Bt1361Curve>), PixelColorRange.Extended);
            case CicpTransferCharacteristics.Iec61966_2_1:
                return CompandingUtilities.Compand(value, default(CompressOperator<SrgbCurve>), PixelColorRange.Extended);
            case CicpTransferCharacteristics.SmpteSt2084:
                return CompandingUtilities.Compand(value, default(CompressOperator<PqCurve>), PixelColorRange.Extended);
            case CicpTransferCharacteristics.SmpteSt428_1:
                return CompandingUtilities.Compand(value, default(CompressOperator<Smpte428Curve>), PixelColorRange.Extended);
            case CicpTransferCharacteristics.AribStdB67:
                return CompandingUtilities.Compand(value, default(CompressOperator<HlgCurve>), PixelColorRange.Extended);
            default:
                // H.273 leaves unspecified and reserved values to the application. Retain the BT.709 default.
                return CompandingUtilities.Compand(value, default(CompressOperator<Bt709Curve>), PixelColorRange.Extended);
        }
    }

    /// <summary>
    /// Converts four linear signal values to their H.273 nonlinear-domain values.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The normalized linear components in the selected H.273 curve's domain. Every SIMD lane is a color component.</param>
    /// <returns>The corresponding nonlinear-domain values.</returns>
    public static Vector128<float> ToGamma(CicpTransferCharacteristics transferCharacteristics, Vector128<float> value)
        => ToGammaCore(transferCharacteristics, value);

    /// <summary>
    /// Converts eight linear signal values to their H.273 nonlinear-domain values.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The normalized linear components in the selected H.273 curve's domain. Every SIMD lane is a color component.</param>
    /// <returns>The corresponding nonlinear-domain values.</returns>
    public static Vector256<float> ToGamma(CicpTransferCharacteristics transferCharacteristics, Vector256<float> value)
        => ToGammaCore(transferCharacteristics, value);

    /// <summary>
    /// Converts sixteen linear signal values to their H.273 nonlinear-domain values.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The normalized linear components in the selected H.273 curve's domain. Every SIMD lane is a color component.</param>
    /// <returns>The corresponding nonlinear-domain values.</returns>
    public static Vector512<float> ToGamma(CicpTransferCharacteristics transferCharacteristics, Vector512<float> value)
        => ToGammaCore(transferCharacteristics, value);

    /// <summary>
    /// Compresses RGB directly with the H.273 curve's range rules and retains alpha.
    /// </summary>
    /// <param name="values">The normalized linear RGB colors. Each vector contains RGB in X, Y, Z and unchanged alpha in W.</param>
    /// <param name="transferCharacteristics">The transfer curve and its specified range rules.</param>
    public static void CompressExtended(Span<Vector4> values, CicpTransferCharacteristics transferCharacteristics)
        => CompressCore(values, transferCharacteristics, PixelColorRange.Extended);

    /// <summary>
    /// Selects one transfer curve and applies the requested RGB result range.
    /// </summary>
    /// <param name="values">The colors to transform. Alpha remains unchanged.</param>
    /// <param name="transferCharacteristics">The transfer curve.</param>
    /// <param name="colorRange">The range applied to the converted RGB components.</param>
    private static void CompressCore(Span<Vector4> values, CicpTransferCharacteristics transferCharacteristics, PixelColorRange colorRange)
    {
        // Select one curve type for the complete span. Its operator calls the scalar or SIMD
        // arithmetic directly, without another transfer-characteristic switch inside the bulk loop.
        switch (transferCharacteristics)
        {
            case CicpTransferCharacteristics.ItuRBt709_6:
            case CicpTransferCharacteristics.ItuRBt601_7:
            case CicpTransferCharacteristics.ItuRBt2020_2_10bit:
            case CicpTransferCharacteristics.ItuRBt2020_2_12bit:
                CompandingUtilities.Compand(values, default(CompressOperator<Bt709Curve>), colorRange);
                break;
            case CicpTransferCharacteristics.Gamma2_2:
                CompandingUtilities.Compand(values, default(CompressOperator<Gamma22Curve>), colorRange);
                break;
            case CicpTransferCharacteristics.Gamma2_8:
                CompandingUtilities.Compand(values, default(CompressOperator<Gamma28Curve>), colorRange);
                break;
            case CicpTransferCharacteristics.SmpteSt240:
                CompandingUtilities.Compand(values, default(CompressOperator<Smpte240Curve>), colorRange);
                break;
            case CicpTransferCharacteristics.Linear:
                CompandingUtilities.Compand(values, default(CompressOperator<LinearCurve>), colorRange);
                break;
            case CicpTransferCharacteristics.Log100:
                CompandingUtilities.Compand(values, default(CompressOperator<Log100Curve>), colorRange);
                break;
            case CicpTransferCharacteristics.Log100Sqrt:
                CompandingUtilities.Compand(values, default(CompressOperator<Log100SqrtCurve>), colorRange);
                break;
            case CicpTransferCharacteristics.Iec61966_2_4:
                CompandingUtilities.Compand(values, default(CompressOperator<Iec61966Curve>), colorRange);
                break;
            case CicpTransferCharacteristics.ItuRBt1361_0:
                CompandingUtilities.Compand(values, default(CompressOperator<Bt1361Curve>), colorRange);
                break;
            case CicpTransferCharacteristics.Iec61966_2_1:
                CompandingUtilities.Compand(values, default(CompressOperator<SrgbCurve>), colorRange);
                break;
            case CicpTransferCharacteristics.SmpteSt2084:
                CompandingUtilities.Compand(values, default(CompressOperator<PqCurve>), colorRange);
                break;
            case CicpTransferCharacteristics.SmpteSt428_1:
                CompandingUtilities.Compand(values, default(CompressOperator<Smpte428Curve>), colorRange);
                break;
            case CicpTransferCharacteristics.AribStdB67:
                CompandingUtilities.Compand(values, default(CompressOperator<HlgCurve>), colorRange);
                break;
            default:
                CompandingUtilities.Compand(values, default(CompressOperator<Bt709Curve>), colorRange);
                break;
        }
    }

    /// <summary>
    /// Separates H.273 tables from the other companders in the shared cache.
    /// </summary>
    private sealed class CicpCompandingKey;
}
