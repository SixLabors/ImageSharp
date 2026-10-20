// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;

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
        /// Applies the expand curve to independent components.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        public static abstract float Expand(float value);

        /// <summary>
        /// Applies the expand curve to independent components.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        public static abstract Vector128<float> Expand(Vector128<float> value);

        /// <summary>
        /// Applies the expand curve to independent components.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        public static abstract Vector256<float> Expand(Vector256<float> value);

        /// <summary>
        /// Applies the expand curve to independent components.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        public static abstract Vector512<float> Expand(Vector512<float> value);

        /// <summary>
        /// Applies the compress curve to independent components.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        public static abstract float Compress(float value);

        /// <summary>
        /// Applies the compress curve to independent components.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        public static abstract Vector128<float> Compress(Vector128<float> value);

        /// <summary>
        /// Applies the compress curve to independent components.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        public static abstract Vector256<float> Compress(Vector256<float> value);

        /// <summary>
        /// Applies the compress curve to independent components.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        public static abstract Vector512<float> Compress(Vector512<float> value);
    }

    /// <summary>
    /// Converts nonlinear signal values to their H.273 linear-domain values using the selected SIMD width.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The corresponding linear-domain values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> ToLinearCore(CicpTransferCharacteristics transferCharacteristics, Vector128<float> value)
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
                return CompandingUtilities.Compand(value, default(ExpandOperator<Bt709Curve>), PixelColorRange.Extended);
        }
    }

    /// <summary>
    /// Converts nonlinear signal values to their H.273 linear-domain values using the selected SIMD width.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The corresponding linear-domain values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> ToLinearCore(CicpTransferCharacteristics transferCharacteristics, Vector256<float> value)
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
                return CompandingUtilities.Compand(value, default(ExpandOperator<Bt709Curve>), PixelColorRange.Extended);
        }
    }

    /// <summary>
    /// Converts nonlinear signal values to their H.273 linear-domain values using the selected SIMD width.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The nonlinear signal values.</param>
    /// <returns>The corresponding linear-domain values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> ToLinearCore(CicpTransferCharacteristics transferCharacteristics, Vector512<float> value)
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
                return CompandingUtilities.Compand(value, default(ExpandOperator<Bt709Curve>), PixelColorRange.Extended);
        }
    }

    /// <summary>
    /// Converts linear signal values to their H.273 nonlinear-domain values using the selected SIMD width.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The linear signal values.</param>
    /// <returns>The corresponding nonlinear-domain values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> ToGammaCore(CicpTransferCharacteristics transferCharacteristics, Vector128<float> value)
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
                return CompandingUtilities.Compand(value, default(CompressOperator<Bt709Curve>), PixelColorRange.Extended);
        }
    }

    /// <summary>
    /// Converts linear signal values to their H.273 nonlinear-domain values using the selected SIMD width.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The linear signal values.</param>
    /// <returns>The corresponding nonlinear-domain values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> ToGammaCore(CicpTransferCharacteristics transferCharacteristics, Vector256<float> value)
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
                return CompandingUtilities.Compand(value, default(CompressOperator<Bt709Curve>), PixelColorRange.Extended);
        }
    }

    /// <summary>
    /// Converts linear signal values to their H.273 nonlinear-domain values using the selected SIMD width.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="value">The linear signal values.</param>
    /// <returns>The corresponding nonlinear-domain values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> ToGammaCore(CicpTransferCharacteristics transferCharacteristics, Vector512<float> value)
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
                return CompandingUtilities.Compand(value, default(CompressOperator<Bt709Curve>), PixelColorRange.Extended);
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
        public float Invoke(float value) => TCurve.Expand(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
            => TCurve.Expand(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
            => TCurve.Expand(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
            => TCurve.Expand(value);
    }

    /// <summary>
    /// Applies one CICP curve to independent components at every supported width.
    /// </summary>
    /// <typeparam name="TCurve">The characteristic selected before traversal.</typeparam>
    private readonly struct CompressOperator<TCurve> : TensorPrimitives_.IUnaryOperator
        where TCurve : struct, ITransferCurve
    {
        /// <inheritdoc/>
        public float Invoke(float value) => TCurve.Compress(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
            => TCurve.Compress(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
            => TCurve.Compress(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
            => TCurve.Compress(value);
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
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The linear signal values.</returns>
        public static Vector128<float> Expand(Vector128<float> value)
        {
            Vector128<float> zero = Vector128.Create(0F);
            Vector128<float> one = Vector128.Create(1F);
            Vector128<float> converted = new Rec709Companding.ExpandOperator(
                Bt709Alpha,
                Bt709Alpha - 1F,
                Rec709Companding.LinearSlope * Bt709Beta).Invoke(value);

            // H.273 bounds this curve to [0,1]. The shared operator itself retains extended RGB.
            // Keep the original comparison order, including the result for nonfinite input.
            Vector128<float> bounded = Vector128.ConditionalSelect(Vector128.LessThan(value, one), converted, one);
            return Vector128.ConditionalSelect(Vector128.LessThan(value, zero), zero, bounded);
        }

        /// <summary>
        /// Applies the inverse BT.709-family opto-electronic transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The linear signal values.</returns>
        public static Vector256<float> Expand(Vector256<float> value)
        {
            Vector256<float> zero = Vector256.Create(0F);
            Vector256<float> one = Vector256.Create(1F);
            Vector256<float> converted = new Rec709Companding.ExpandOperator(
                Bt709Alpha,
                Bt709Alpha - 1F,
                Rec709Companding.LinearSlope * Bt709Beta).Invoke(value);

            // H.273 bounds this curve to [0,1]. The shared operator itself retains extended RGB.
            // Keep the original comparison order, including the result for nonfinite input.
            Vector256<float> bounded = Vector256.ConditionalSelect(Vector256.LessThan(value, one), converted, one);
            return Vector256.ConditionalSelect(Vector256.LessThan(value, zero), zero, bounded);
        }

        /// <summary>
        /// Applies the inverse BT.709-family opto-electronic transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The linear signal values.</returns>
        public static Vector512<float> Expand(Vector512<float> value)
        {
            Vector512<float> zero = Vector512.Create(0F);
            Vector512<float> one = Vector512.Create(1F);
            Vector512<float> converted = new Rec709Companding.ExpandOperator(
                Bt709Alpha,
                Bt709Alpha - 1F,
                Rec709Companding.LinearSlope * Bt709Beta).Invoke(value);

            // H.273 bounds this curve to [0,1]. The shared operator itself retains extended RGB.
            // Keep the original comparison order, including the result for nonfinite input.
            Vector512<float> bounded = Vector512.ConditionalSelect(Vector512.LessThan(value, one), converted, one);
            return Vector512.ConditionalSelect(Vector512.LessThan(value, zero), zero, bounded);
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
        /// <param name="value">The linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static Vector128<float> Compress(Vector128<float> value)
        {
            Vector128<float> zero = Vector128.Create(0F);
            Vector128<float> one = Vector128.Create(1F);
            Vector128<float> converted = new Rec709Companding.CompressOperator(Bt709Alpha, Bt709Alpha - 1F, Bt709Beta).Invoke(value);

            // H.273 bounds this curve to [0,1]. The shared operator itself retains extended RGB.
            // Keep the original comparison order, including the result for nonfinite input.
            Vector128<float> bounded = Vector128.ConditionalSelect(Vector128.LessThan(value, one), converted, one);
            return Vector128.ConditionalSelect(Vector128.LessThan(value, zero), zero, bounded);
        }

        /// <summary>
        /// Applies the BT.709-family opto-electronic transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static Vector256<float> Compress(Vector256<float> value)
        {
            Vector256<float> zero = Vector256.Create(0F);
            Vector256<float> one = Vector256.Create(1F);
            Vector256<float> converted = new Rec709Companding.CompressOperator(Bt709Alpha, Bt709Alpha - 1F, Bt709Beta).Invoke(value);

            // H.273 bounds this curve to [0,1]. The shared operator itself retains extended RGB.
            // Keep the original comparison order, including the result for nonfinite input.
            Vector256<float> bounded = Vector256.ConditionalSelect(Vector256.LessThan(value, one), converted, one);
            return Vector256.ConditionalSelect(Vector256.LessThan(value, zero), zero, bounded);
        }

        /// <summary>
        /// Applies the BT.709-family opto-electronic transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static Vector512<float> Compress(Vector512<float> value)
        {
            Vector512<float> zero = Vector512.Create(0F);
            Vector512<float> one = Vector512.Create(1F);
            Vector512<float> converted = new Rec709Companding.CompressOperator(Bt709Alpha, Bt709Alpha - 1F, Bt709Beta).Invoke(value);

            // H.273 bounds this curve to [0,1]. The shared operator itself retains extended RGB.
            // Keep the original comparison order, including the result for nonfinite input.
            Vector512<float> bounded = Vector512.ConditionalSelect(Vector512.LessThan(value, one), converted, one);
            return Vector512.ConditionalSelect(Vector512.LessThan(value, zero), zero, bounded);
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
        public static Vector128<float> Expand(Vector128<float> value)
        {
            Vector128<float> zero = Vector128.Create(0F);
            Vector128<float> one = Vector128.Create(1F);

            return Vector128_.Pow(Vector128.Min(Vector128.Max(value, zero), one), Gamma);
        }

        /// <inheritdoc/>
        public static Vector256<float> Expand(Vector256<float> value)
        {
            Vector256<float> zero = Vector256.Create(0F);
            Vector256<float> one = Vector256.Create(1F);

            return Vector256_.Pow(Vector256.Min(Vector256.Max(value, zero), one), Gamma);
        }

        /// <inheritdoc/>
        public static Vector512<float> Expand(Vector512<float> value)
        {
            Vector512<float> zero = Vector512.Create(0F);
            Vector512<float> one = Vector512.Create(1F);

            return Vector512_.Pow(Vector512.Min(Vector512.Max(value, zero), one), Gamma);
        }

        /// <inheritdoc/>
        public static float Compress(float value)
        {
            return new TensorPrimitives_.PowOperator(1F / Gamma).Invoke(Math.Clamp(value, 0F, 1F));
        }

        /// <inheritdoc/>
        public static Vector128<float> Compress(Vector128<float> value)
        {
            Vector128<float> zero = Vector128.Create(0F);
            Vector128<float> one = Vector128.Create(1F);

            return Vector128_.Pow(Vector128.Min(Vector128.Max(value, zero), one), 1F / Gamma);
        }

        /// <inheritdoc/>
        public static Vector256<float> Compress(Vector256<float> value)
        {
            Vector256<float> zero = Vector256.Create(0F);
            Vector256<float> one = Vector256.Create(1F);

            return Vector256_.Pow(Vector256.Min(Vector256.Max(value, zero), one), 1F / Gamma);
        }

        /// <inheritdoc/>
        public static Vector512<float> Compress(Vector512<float> value)
        {
            Vector512<float> zero = Vector512.Create(0F);
            Vector512<float> one = Vector512.Create(1F);

            return Vector512_.Pow(Vector512.Min(Vector512.Max(value, zero), one), 1F / Gamma);
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
        public static Vector128<float> Expand(Vector128<float> value)
        {
            Vector128<float> zero = Vector128.Create(0F);
            Vector128<float> one = Vector128.Create(1F);

            return Vector128_.Pow(Vector128.Min(Vector128.Max(value, zero), one), Gamma);
        }

        /// <inheritdoc/>
        public static Vector256<float> Expand(Vector256<float> value)
        {
            Vector256<float> zero = Vector256.Create(0F);
            Vector256<float> one = Vector256.Create(1F);

            return Vector256_.Pow(Vector256.Min(Vector256.Max(value, zero), one), Gamma);
        }

        /// <inheritdoc/>
        public static Vector512<float> Expand(Vector512<float> value)
        {
            Vector512<float> zero = Vector512.Create(0F);
            Vector512<float> one = Vector512.Create(1F);

            return Vector512_.Pow(Vector512.Min(Vector512.Max(value, zero), one), Gamma);
        }

        /// <inheritdoc/>
        public static float Compress(float value)
        {
            return new TensorPrimitives_.PowOperator(1F / Gamma).Invoke(Math.Clamp(value, 0F, 1F));
        }

        /// <inheritdoc/>
        public static Vector128<float> Compress(Vector128<float> value)
        {
            Vector128<float> zero = Vector128.Create(0F);
            Vector128<float> one = Vector128.Create(1F);

            return Vector128_.Pow(Vector128.Min(Vector128.Max(value, zero), one), 1F / Gamma);
        }

        /// <inheritdoc/>
        public static Vector256<float> Compress(Vector256<float> value)
        {
            Vector256<float> zero = Vector256.Create(0F);
            Vector256<float> one = Vector256.Create(1F);

            return Vector256_.Pow(Vector256.Min(Vector256.Max(value, zero), one), 1F / Gamma);
        }

        /// <inheritdoc/>
        public static Vector512<float> Compress(Vector512<float> value)
        {
            Vector512<float> zero = Vector512.Create(0F);
            Vector512<float> one = Vector512.Create(1F);

            return Vector512_.Pow(Vector512.Min(Vector512.Max(value, zero), one), 1F / Gamma);
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
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The linear signal values.</returns>
        public static Vector128<float> Expand(Vector128<float> value)
        {
            Vector128<float> zero = Vector128.Create(0F);
            Vector128<float> one = Vector128.Create(1F);
            Vector128<float> linear = Vector128.Divide(value, Vector128.Create(4F));
            Vector128<float> baseValue = Vector128.Divide(Vector128.Add(value, Vector128.Create(Smpte240Alpha - 1F)), Vector128.Create(Smpte240Alpha));
            Vector128<float> nonlinear = Vector128_.Pow(Vector128.Max(baseValue, zero), 1F / Rec709Companding.Exponent);
            Vector128<float> belowOne = Vector128.ConditionalSelect(Vector128.LessThan(value, Vector128.Create(4F * Smpte240Beta)), linear, nonlinear);
            Vector128<float> bounded = Vector128.ConditionalSelect(Vector128.LessThan(value, one), belowOne, one);
            return Vector128.ConditionalSelect(Vector128.LessThan(value, zero), zero, bounded);
        }

        /// <summary>
        /// Applies the inverse SMPTE ST 240 opto-electronic transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The linear signal values.</returns>
        public static Vector256<float> Expand(Vector256<float> value)
        {
            Vector256<float> zero = Vector256.Create(0F);
            Vector256<float> one = Vector256.Create(1F);
            Vector256<float> linear = Vector256.Divide(value, Vector256.Create(4F));
            Vector256<float> baseValue = Vector256.Divide(Vector256.Add(value, Vector256.Create(Smpte240Alpha - 1F)), Vector256.Create(Smpte240Alpha));
            Vector256<float> nonlinear = Vector256_.Pow(Vector256.Max(baseValue, zero), 1F / Rec709Companding.Exponent);
            Vector256<float> belowOne = Vector256.ConditionalSelect(Vector256.LessThan(value, Vector256.Create(4F * Smpte240Beta)), linear, nonlinear);
            Vector256<float> bounded = Vector256.ConditionalSelect(Vector256.LessThan(value, one), belowOne, one);
            return Vector256.ConditionalSelect(Vector256.LessThan(value, zero), zero, bounded);
        }

        /// <summary>
        /// Applies the inverse SMPTE ST 240 opto-electronic transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The linear signal values.</returns>
        public static Vector512<float> Expand(Vector512<float> value)
        {
            Vector512<float> zero = Vector512.Create(0F);
            Vector512<float> one = Vector512.Create(1F);
            Vector512<float> linear = Vector512.Divide(value, Vector512.Create(4F));
            Vector512<float> baseValue = Vector512.Divide(Vector512.Add(value, Vector512.Create(Smpte240Alpha - 1F)), Vector512.Create(Smpte240Alpha));
            Vector512<float> nonlinear = Vector512_.Pow(Vector512.Max(baseValue, zero), 1F / Rec709Companding.Exponent);
            Vector512<float> belowOne = Vector512.ConditionalSelect(Vector512.LessThan(value, Vector512.Create(4F * Smpte240Beta)), linear, nonlinear);
            Vector512<float> bounded = Vector512.ConditionalSelect(Vector512.LessThan(value, one), belowOne, one);
            return Vector512.ConditionalSelect(Vector512.LessThan(value, zero), zero, bounded);
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
        /// <param name="value">The linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static Vector128<float> Compress(Vector128<float> value)
        {
            Vector128<float> zero = Vector128.Create(0F);
            Vector128<float> one = Vector128.Create(1F);
            Vector128<float> linear = Vector128.Multiply(value, Vector128.Create(4F));
            Vector128<float> nonlinear = Vector128.Subtract(
                Vector128.Multiply(Vector128.Create(Smpte240Alpha), Vector128_.Pow(Vector128.Max(value, zero), Rec709Companding.Exponent)),
                Vector128.Create(Smpte240Alpha - 1F));

            Vector128<float> belowOne = Vector128.ConditionalSelect(Vector128.LessThan(value, Vector128.Create(Smpte240Beta)), linear, nonlinear);
            Vector128<float> bounded = Vector128.ConditionalSelect(Vector128.LessThan(value, one), belowOne, one);
            return Vector128.ConditionalSelect(Vector128.LessThan(value, zero), zero, bounded);
        }

        /// <summary>
        /// Applies the SMPTE ST 240 opto-electronic transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static Vector256<float> Compress(Vector256<float> value)
        {
            Vector256<float> zero = Vector256.Create(0F);
            Vector256<float> one = Vector256.Create(1F);
            Vector256<float> linear = Vector256.Multiply(value, Vector256.Create(4F));
            Vector256<float> nonlinear = Vector256.Subtract(
                Vector256.Multiply(Vector256.Create(Smpte240Alpha), Vector256_.Pow(Vector256.Max(value, zero), Rec709Companding.Exponent)),
                Vector256.Create(Smpte240Alpha - 1F));

            Vector256<float> belowOne = Vector256.ConditionalSelect(Vector256.LessThan(value, Vector256.Create(Smpte240Beta)), linear, nonlinear);
            Vector256<float> bounded = Vector256.ConditionalSelect(Vector256.LessThan(value, one), belowOne, one);
            return Vector256.ConditionalSelect(Vector256.LessThan(value, zero), zero, bounded);
        }

        /// <summary>
        /// Applies the SMPTE ST 240 opto-electronic transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static Vector512<float> Compress(Vector512<float> value)
        {
            Vector512<float> zero = Vector512.Create(0F);
            Vector512<float> one = Vector512.Create(1F);
            Vector512<float> linear = Vector512.Multiply(value, Vector512.Create(4F));
            Vector512<float> nonlinear = Vector512.Subtract(
                Vector512.Multiply(Vector512.Create(Smpte240Alpha), Vector512_.Pow(Vector512.Max(value, zero), Rec709Companding.Exponent)),
                Vector512.Create(Smpte240Alpha - 1F));

            Vector512<float> belowOne = Vector512.ConditionalSelect(Vector512.LessThan(value, Vector512.Create(Smpte240Beta)), linear, nonlinear);
            Vector512<float> bounded = Vector512.ConditionalSelect(Vector512.LessThan(value, one), belowOne, one);
            return Vector512.ConditionalSelect(Vector512.LessThan(value, zero), zero, bounded);
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
        public static Vector128<float> Expand(Vector128<float> value)
        {
            Vector128<float> zero = Vector128.Create(0F);
            Vector128<float> one = Vector128.Create(1F);

            return Vector128.Min(Vector128.Max(value, zero), one);
        }

        /// <inheritdoc/>
        public static Vector256<float> Expand(Vector256<float> value)
        {
            Vector256<float> zero = Vector256.Create(0F);
            Vector256<float> one = Vector256.Create(1F);

            return Vector256.Min(Vector256.Max(value, zero), one);
        }

        /// <inheritdoc/>
        public static Vector512<float> Expand(Vector512<float> value)
        {
            Vector512<float> zero = Vector512.Create(0F);
            Vector512<float> one = Vector512.Create(1F);

            return Vector512.Min(Vector512.Max(value, zero), one);
        }

        /// <inheritdoc/>
        public static float Compress(float value)
        {
            return Math.Clamp(value, 0F, 1F);
        }

        /// <inheritdoc/>
        public static Vector128<float> Compress(Vector128<float> value)
        {
            Vector128<float> zero = Vector128.Create(0F);
            Vector128<float> one = Vector128.Create(1F);

            return Vector128.Min(Vector128.Max(value, zero), one);
        }

        /// <inheritdoc/>
        public static Vector256<float> Compress(Vector256<float> value)
        {
            Vector256<float> zero = Vector256.Create(0F);
            Vector256<float> one = Vector256.Create(1F);

            return Vector256.Min(Vector256.Max(value, zero), one);
        }

        /// <inheritdoc/>
        public static Vector512<float> Compress(Vector512<float> value)
        {
            Vector512<float> zero = Vector512.Create(0F);
            Vector512<float> one = Vector512.Create(1F);

            return Vector512.Min(Vector512.Max(value, zero), one);
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
        public static Vector128<float> Expand(Vector128<float> value)
        {
            Vector128<float> zero = Vector128.Create(0F);
            Vector128<float> one = Vector128.Create(1F);

            // H.273 assigns an interval to zero for logarithmic curves. The scalar midpoint convention is
            // selected lane-wise after evaluating the positive branch, which keeps the hot path branchless.
            Vector128<float> exponent = Vector128.Multiply(Vector128.Subtract(Vector128.Min(value, one), one), Vector128.Create(2F * NaturalLogOfTen));
            Vector128<float> positive = Vector128.Exp(exponent);
            return Vector128.ConditionalSelect(Vector128.LessThanOrEqual(value, zero), Vector128.Create(ZeroCodeValue), positive);
        }

        /// <inheritdoc/>
        public static Vector256<float> Expand(Vector256<float> value)
        {
            Vector256<float> zero = Vector256.Create(0F);
            Vector256<float> one = Vector256.Create(1F);

            // H.273 assigns an interval to zero for logarithmic curves. The scalar midpoint convention is
            // selected lane-wise after evaluating the positive branch, which keeps the hot path branchless.
            Vector256<float> exponent = Vector256.Multiply(Vector256.Subtract(Vector256.Min(value, one), one), Vector256.Create(2F * NaturalLogOfTen));
            Vector256<float> positive = Vector256.Exp(exponent);
            return Vector256.ConditionalSelect(Vector256.LessThanOrEqual(value, zero), Vector256.Create(ZeroCodeValue), positive);
        }

        /// <inheritdoc/>
        public static Vector512<float> Expand(Vector512<float> value)
        {
            Vector512<float> zero = Vector512.Create(0F);
            Vector512<float> one = Vector512.Create(1F);

            // H.273 assigns an interval to zero for logarithmic curves. The scalar midpoint convention is
            // selected lane-wise after evaluating the positive branch, which keeps the hot path branchless.
            Vector512<float> exponent = Vector512.Multiply(Vector512.Subtract(Vector512.Min(value, one), one), Vector512.Create(2F * NaturalLogOfTen));
            Vector512<float> positive = Vector512.Exp(exponent);
            return Vector512.ConditionalSelect(Vector512.LessThanOrEqual(value, zero), Vector512.Create(ZeroCodeValue), positive);
        }

        /// <inheritdoc/>
        public static float Compress(float value)
        {
            return value <= LinearCutoff ? 0F : 1F + (MathF.Log10(MathF.Min(value, 1F)) / 2F);
        }

        /// <inheritdoc/>
        public static Vector128<float> Compress(Vector128<float> value)
        {
            Vector128<float> zero = Vector128.Create(0F);
            Vector128<float> one = Vector128.Create(1F);

            // ConditionalSelect evaluates both branches. Bound the unused logarithm input so negative values do not produce NaN in that branch.
            Vector128<float> threshold = Vector128.Create(LinearCutoff);
            Vector128<float> bounded = Vector128.Min(Vector128.Max(value, threshold), one);
            Vector128<float> positive = Vector128.Add(one, Vector128.Divide(Vector128.Log(bounded), Vector128.Create(2F * NaturalLogOfTen)));
            return Vector128.ConditionalSelect(Vector128.LessThanOrEqual(value, threshold), zero, positive);
        }

        /// <inheritdoc/>
        public static Vector256<float> Compress(Vector256<float> value)
        {
            Vector256<float> zero = Vector256.Create(0F);
            Vector256<float> one = Vector256.Create(1F);

            // ConditionalSelect evaluates both branches. Bound the unused logarithm input so negative values do not produce NaN in that branch.
            Vector256<float> threshold = Vector256.Create(LinearCutoff);
            Vector256<float> bounded = Vector256.Min(Vector256.Max(value, threshold), one);
            Vector256<float> positive = Vector256.Add(one, Vector256.Divide(Vector256.Log(bounded), Vector256.Create(2F * NaturalLogOfTen)));
            return Vector256.ConditionalSelect(Vector256.LessThanOrEqual(value, threshold), zero, positive);
        }

        /// <inheritdoc/>
        public static Vector512<float> Compress(Vector512<float> value)
        {
            Vector512<float> zero = Vector512.Create(0F);
            Vector512<float> one = Vector512.Create(1F);

            // ConditionalSelect evaluates both branches. Bound the unused logarithm input so negative values do not produce NaN in that branch.
            Vector512<float> threshold = Vector512.Create(LinearCutoff);
            Vector512<float> bounded = Vector512.Min(Vector512.Max(value, threshold), one);
            Vector512<float> positive = Vector512.Add(one, Vector512.Divide(Vector512.Log(bounded), Vector512.Create(2F * NaturalLogOfTen)));
            return Vector512.ConditionalSelect(Vector512.LessThanOrEqual(value, threshold), zero, positive);
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
        public static Vector128<float> Expand(Vector128<float> value)
        {
            Vector128<float> zero = Vector128.Create(0F);
            Vector128<float> one = Vector128.Create(1F);

            Vector128<float> exponent = Vector128.Multiply(Vector128.Subtract(Vector128.Min(value, one), one), Vector128.Create(2.5F * NaturalLogOfTen));
            Vector128<float> positive = Vector128.Exp(exponent);
            return Vector128.ConditionalSelect(Vector128.LessThanOrEqual(value, zero), Vector128.Create(ZeroCodeValue), positive);
        }

        /// <inheritdoc/>
        public static Vector256<float> Expand(Vector256<float> value)
        {
            Vector256<float> zero = Vector256.Create(0F);
            Vector256<float> one = Vector256.Create(1F);

            Vector256<float> exponent = Vector256.Multiply(Vector256.Subtract(Vector256.Min(value, one), one), Vector256.Create(2.5F * NaturalLogOfTen));
            Vector256<float> positive = Vector256.Exp(exponent);
            return Vector256.ConditionalSelect(Vector256.LessThanOrEqual(value, zero), Vector256.Create(ZeroCodeValue), positive);
        }

        /// <inheritdoc/>
        public static Vector512<float> Expand(Vector512<float> value)
        {
            Vector512<float> zero = Vector512.Create(0F);
            Vector512<float> one = Vector512.Create(1F);

            Vector512<float> exponent = Vector512.Multiply(Vector512.Subtract(Vector512.Min(value, one), one), Vector512.Create(2.5F * NaturalLogOfTen));
            Vector512<float> positive = Vector512.Exp(exponent);
            return Vector512.ConditionalSelect(Vector512.LessThanOrEqual(value, zero), Vector512.Create(ZeroCodeValue), positive);
        }

        /// <inheritdoc/>
        public static float Compress(float value)
        {
            return value <= LinearCutoff ? 0F : 1F + (MathF.Log10(MathF.Min(value, 1F)) / 2.5F);
        }

        /// <inheritdoc/>
        public static Vector128<float> Compress(Vector128<float> value)
        {
            Vector128<float> zero = Vector128.Create(0F);
            Vector128<float> one = Vector128.Create(1F);

            // ConditionalSelect evaluates both branches. Bound the unused logarithm input so negative values do not produce NaN in that branch.
            Vector128<float> threshold = Vector128.Create(LinearCutoff);
            Vector128<float> bounded = Vector128.Min(Vector128.Max(value, threshold), one);
            Vector128<float> positive = Vector128.Add(one, Vector128.Divide(Vector128.Log(bounded), Vector128.Create(2.5F * NaturalLogOfTen)));
            return Vector128.ConditionalSelect(Vector128.LessThanOrEqual(value, threshold), zero, positive);
        }

        /// <inheritdoc/>
        public static Vector256<float> Compress(Vector256<float> value)
        {
            Vector256<float> zero = Vector256.Create(0F);
            Vector256<float> one = Vector256.Create(1F);

            // ConditionalSelect evaluates both branches. Bound the unused logarithm input so negative values do not produce NaN in that branch.
            Vector256<float> threshold = Vector256.Create(LinearCutoff);
            Vector256<float> bounded = Vector256.Min(Vector256.Max(value, threshold), one);
            Vector256<float> positive = Vector256.Add(one, Vector256.Divide(Vector256.Log(bounded), Vector256.Create(2.5F * NaturalLogOfTen)));
            return Vector256.ConditionalSelect(Vector256.LessThanOrEqual(value, threshold), zero, positive);
        }

        /// <inheritdoc/>
        public static Vector512<float> Compress(Vector512<float> value)
        {
            Vector512<float> zero = Vector512.Create(0F);
            Vector512<float> one = Vector512.Create(1F);

            // ConditionalSelect evaluates both branches. Bound the unused logarithm input so negative values do not produce NaN in that branch.
            Vector512<float> threshold = Vector512.Create(LinearCutoff);
            Vector512<float> bounded = Vector512.Min(Vector512.Max(value, threshold), one);
            Vector512<float> positive = Vector512.Add(one, Vector512.Divide(Vector512.Log(bounded), Vector512.Create(2.5F * NaturalLogOfTen)));
            return Vector512.ConditionalSelect(Vector512.LessThanOrEqual(value, threshold), zero, positive);
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
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The linear signal values.</returns>
        public static Vector128<float> Expand(Vector128<float> value)
        {
            Vector128<float> negativeBase = Vector128.Divide(Vector128.Subtract(value, Vector128.Create(Bt709Alpha - 1F)), Vector128.Create(-Bt709Alpha));
            Vector128<float> negative = Vector128.Negate(Vector128_.Pow(Vector128.Max(negativeBase, Vector128.Create(0F)), 1F / Rec709Companding.Exponent));
            Vector128<float> linear = Vector128.Divide(value, Vector128.Create(Rec709Companding.LinearSlope));
            Vector128<float> positiveBase = Vector128.Divide(Vector128.Add(value, Vector128.Create(Bt709Alpha - 1F)), Vector128.Create(Bt709Alpha));
            Vector128<float> positive = Vector128_.Pow(Vector128.Max(positiveBase, Vector128.Create(0F)), 1F / Rec709Companding.Exponent);
            Vector128<float> centerOrPositive = Vector128.ConditionalSelect(
                Vector128.LessThan(value, Vector128.Create(Rec709Companding.LinearSlope * Bt709Beta)),
                linear,
                positive);

            return Vector128.ConditionalSelect(
                Vector128.LessThan(value, Vector128.Create(-Rec709Companding.LinearSlope * Bt709Beta)),
                negative,
                centerOrPositive);
        }

        /// <summary>
        /// Applies the inverse extended IEC 61966-2-4 transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The linear signal values.</returns>
        public static Vector256<float> Expand(Vector256<float> value)
        {
            Vector256<float> negativeBase = Vector256.Divide(Vector256.Subtract(value, Vector256.Create(Bt709Alpha - 1F)), Vector256.Create(-Bt709Alpha));
            Vector256<float> negative = Vector256.Negate(Vector256_.Pow(Vector256.Max(negativeBase, Vector256.Create(0F)), 1F / Rec709Companding.Exponent));
            Vector256<float> linear = Vector256.Divide(value, Vector256.Create(Rec709Companding.LinearSlope));
            Vector256<float> positiveBase = Vector256.Divide(Vector256.Add(value, Vector256.Create(Bt709Alpha - 1F)), Vector256.Create(Bt709Alpha));
            Vector256<float> positive = Vector256_.Pow(Vector256.Max(positiveBase, Vector256.Create(0F)), 1F / Rec709Companding.Exponent);
            Vector256<float> centerOrPositive = Vector256.ConditionalSelect(
                Vector256.LessThan(value, Vector256.Create(Rec709Companding.LinearSlope * Bt709Beta)),
                linear,
                positive);

            return Vector256.ConditionalSelect(
                Vector256.LessThan(value, Vector256.Create(-Rec709Companding.LinearSlope * Bt709Beta)),
                negative,
                centerOrPositive);
        }

        /// <summary>
        /// Applies the inverse extended IEC 61966-2-4 transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The linear signal values.</returns>
        public static Vector512<float> Expand(Vector512<float> value)
        {
            Vector512<float> negativeBase = Vector512.Divide(Vector512.Subtract(value, Vector512.Create(Bt709Alpha - 1F)), Vector512.Create(-Bt709Alpha));
            Vector512<float> negative = Vector512.Negate(Vector512_.Pow(Vector512.Max(negativeBase, Vector512.Create(0F)), 1F / Rec709Companding.Exponent));
            Vector512<float> linear = Vector512.Divide(value, Vector512.Create(Rec709Companding.LinearSlope));
            Vector512<float> positiveBase = Vector512.Divide(Vector512.Add(value, Vector512.Create(Bt709Alpha - 1F)), Vector512.Create(Bt709Alpha));
            Vector512<float> positive = Vector512_.Pow(Vector512.Max(positiveBase, Vector512.Create(0F)), 1F / Rec709Companding.Exponent);
            Vector512<float> centerOrPositive = Vector512.ConditionalSelect(
                Vector512.LessThan(value, Vector512.Create(Rec709Companding.LinearSlope * Bt709Beta)),
                linear,
                positive);

            return Vector512.ConditionalSelect(
                Vector512.LessThan(value, Vector512.Create(-Rec709Companding.LinearSlope * Bt709Beta)),
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
        /// <param name="value">The linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static Vector128<float> Compress(Vector128<float> value)
        {
            Vector128<float> zero = Vector128.Create(0F);
            Vector128<float> negative = Vector128.Add(
                Vector128.Negate(Vector128.Multiply(
                    Vector128.Create(Bt709Alpha),
                    Vector128_.Pow(Vector128.Max(Vector128.Negate(value), zero), Rec709Companding.Exponent))),
                Vector128.Create(Bt709Alpha - 1F));

            Vector128<float> linear = Vector128.Multiply(value, Vector128.Create(Rec709Companding.LinearSlope));
            Vector128<float> positive = Vector128.Subtract(
                Vector128.Multiply(Vector128.Create(Bt709Alpha), Vector128_.Pow(Vector128.Max(value, zero), Rec709Companding.Exponent)),
                Vector128.Create(Bt709Alpha - 1F));

            Vector128<float> centerOrPositive = Vector128.ConditionalSelect(Vector128.LessThan(value, Vector128.Create(Bt709Beta)), linear, positive);
            return Vector128.ConditionalSelect(Vector128.LessThan(value, Vector128.Create(-Bt709Beta)), negative, centerOrPositive);
        }

        /// <summary>
        /// Applies the extended IEC 61966-2-4 transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static Vector256<float> Compress(Vector256<float> value)
        {
            Vector256<float> zero = Vector256.Create(0F);
            Vector256<float> negative = Vector256.Add(
                Vector256.Negate(Vector256.Multiply(
                    Vector256.Create(Bt709Alpha),
                    Vector256_.Pow(Vector256.Max(Vector256.Negate(value), zero), Rec709Companding.Exponent))),
                Vector256.Create(Bt709Alpha - 1F));

            Vector256<float> linear = Vector256.Multiply(value, Vector256.Create(Rec709Companding.LinearSlope));
            Vector256<float> positive = Vector256.Subtract(
                Vector256.Multiply(Vector256.Create(Bt709Alpha), Vector256_.Pow(Vector256.Max(value, zero), Rec709Companding.Exponent)),
                Vector256.Create(Bt709Alpha - 1F));

            Vector256<float> centerOrPositive = Vector256.ConditionalSelect(Vector256.LessThan(value, Vector256.Create(Bt709Beta)), linear, positive);
            return Vector256.ConditionalSelect(Vector256.LessThan(value, Vector256.Create(-Bt709Beta)), negative, centerOrPositive);
        }

        /// <summary>
        /// Applies the extended IEC 61966-2-4 transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static Vector512<float> Compress(Vector512<float> value)
        {
            Vector512<float> zero = Vector512.Create(0F);
            Vector512<float> negative = Vector512.Add(
                Vector512.Negate(Vector512.Multiply(
                    Vector512.Create(Bt709Alpha),
                    Vector512_.Pow(Vector512.Max(Vector512.Negate(value), zero), Rec709Companding.Exponent))),
                Vector512.Create(Bt709Alpha - 1F));

            Vector512<float> linear = Vector512.Multiply(value, Vector512.Create(Rec709Companding.LinearSlope));
            Vector512<float> positive = Vector512.Subtract(
                Vector512.Multiply(Vector512.Create(Bt709Alpha), Vector512_.Pow(Vector512.Max(value, zero), Rec709Companding.Exponent)),
                Vector512.Create(Bt709Alpha - 1F));

            Vector512<float> centerOrPositive = Vector512.ConditionalSelect(Vector512.LessThan(value, Vector512.Create(Bt709Beta)), linear, positive);
            return Vector512.ConditionalSelect(Vector512.LessThan(value, Vector512.Create(-Bt709Beta)), negative, centerOrPositive);
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
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The linear signal values.</returns>
        public static Vector128<float> Expand(Vector128<float> value)
        {
            Vector128<float> zero = Vector128.Create(0F);
            Vector128<float> negativeBase = Vector128.Divide(
                Vector128.Subtract(value, Vector128.Create(NegativeOffset)),
                Vector128.Create(NegativeScale));

            Vector128<float> negative = Vector128.Divide(
                Vector128_.Pow(Vector128.Max(negativeBase, zero), 1F / Rec709Companding.Exponent),
                Vector128.Create(-4F));

            Vector128<float> negativeOrPositive = Vector128.ConditionalSelect(
                Vector128.LessThan(value, zero),
                negative,
                Bt709Curve.Expand(value));

            return Vector128.ConditionalSelect(Vector128.LessThan(value, Vector128.Create(-0.25F)), Vector128.Create(-0.25F), negativeOrPositive);
        }

        /// <summary>
        /// Applies the inverse extended BT.1361 transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The linear signal values.</returns>
        public static Vector256<float> Expand(Vector256<float> value)
        {
            Vector256<float> zero = Vector256.Create(0F);
            Vector256<float> negativeBase = Vector256.Divide(
                Vector256.Subtract(value, Vector256.Create(NegativeOffset)),
                Vector256.Create(NegativeScale));

            Vector256<float> negative = Vector256.Divide(
                Vector256_.Pow(Vector256.Max(negativeBase, zero), 1F / Rec709Companding.Exponent),
                Vector256.Create(-4F));

            Vector256<float> negativeOrPositive = Vector256.ConditionalSelect(
                Vector256.LessThan(value, zero),
                negative,
                Bt709Curve.Expand(value));

            return Vector256.ConditionalSelect(Vector256.LessThan(value, Vector256.Create(-0.25F)), Vector256.Create(-0.25F), negativeOrPositive);
        }

        /// <summary>
        /// Applies the inverse extended BT.1361 transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The linear signal values.</returns>
        public static Vector512<float> Expand(Vector512<float> value)
        {
            Vector512<float> zero = Vector512.Create(0F);
            Vector512<float> negativeBase = Vector512.Divide(
                Vector512.Subtract(value, Vector512.Create(NegativeOffset)),
                Vector512.Create(NegativeScale));

            Vector512<float> negative = Vector512.Divide(
                Vector512_.Pow(Vector512.Max(negativeBase, zero), 1F / Rec709Companding.Exponent),
                Vector512.Create(-4F));

            Vector512<float> negativeOrPositive = Vector512.ConditionalSelect(
                Vector512.LessThan(value, zero),
                negative,
                Bt709Curve.Expand(value));

            return Vector512.ConditionalSelect(Vector512.LessThan(value, Vector512.Create(-0.25F)), Vector512.Create(-0.25F), negativeOrPositive);
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
        /// <param name="value">The linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static Vector128<float> Compress(Vector128<float> value)
        {
            Vector128<float> zero = Vector128.Create(0F);
            Vector128<float> negativePower = Vector128_.Pow(Vector128.Max(Vector128.Multiply(Vector128.Create(-4F), value), zero), Rec709Companding.Exponent);
            Vector128<float> negative = Vector128.Add(
                Vector128.Multiply(Vector128.Create(NegativeScale), negativePower),
                Vector128.Create(NegativeOffset));

            Vector128<float> negativeOrPositive = Vector128.ConditionalSelect(
                Vector128.LessThan(value, zero),
                negative,
                Bt709Curve.Compress(value));

            return Vector128.ConditionalSelect(Vector128.LessThan(value, Vector128.Create(-0.25F)), Vector128.Create(-0.25F), negativeOrPositive);
        }

        /// <summary>
        /// Applies the extended BT.1361 transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static Vector256<float> Compress(Vector256<float> value)
        {
            Vector256<float> zero = Vector256.Create(0F);
            Vector256<float> negativePower = Vector256_.Pow(Vector256.Max(Vector256.Multiply(Vector256.Create(-4F), value), zero), Rec709Companding.Exponent);
            Vector256<float> negative = Vector256.Add(
                Vector256.Multiply(Vector256.Create(NegativeScale), negativePower),
                Vector256.Create(NegativeOffset));

            Vector256<float> negativeOrPositive = Vector256.ConditionalSelect(
                Vector256.LessThan(value, zero),
                negative,
                Bt709Curve.Compress(value));

            return Vector256.ConditionalSelect(Vector256.LessThan(value, Vector256.Create(-0.25F)), Vector256.Create(-0.25F), negativeOrPositive);
        }

        /// <summary>
        /// Applies the extended BT.1361 transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static Vector512<float> Compress(Vector512<float> value)
        {
            Vector512<float> zero = Vector512.Create(0F);
            Vector512<float> negativePower = Vector512_.Pow(Vector512.Max(Vector512.Multiply(Vector512.Create(-4F), value), zero), Rec709Companding.Exponent);
            Vector512<float> negative = Vector512.Add(
                Vector512.Multiply(Vector512.Create(NegativeScale), negativePower),
                Vector512.Create(NegativeOffset));

            Vector512<float> negativeOrPositive = Vector512.ConditionalSelect(
                Vector512.LessThan(value, zero),
                negative,
                Bt709Curve.Compress(value));

            return Vector512.ConditionalSelect(Vector512.LessThan(value, Vector512.Create(-0.25F)), Vector512.Create(-0.25F), negativeOrPositive);
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
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The linear signal values.</returns>
        public static Vector128<float> Expand(Vector128<float> value)
            => new SRgbCompanding.ExpandOperator(SrgbAlpha, SrgbAlpha - 1F, SRgbCompanding.LinearSlope * SrgbBeta).Invoke(value);

        /// <summary>
        /// Applies the inverse extended IEC 61966-2-1 transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The linear signal values.</returns>
        public static Vector256<float> Expand(Vector256<float> value)
            => new SRgbCompanding.ExpandOperator(SrgbAlpha, SrgbAlpha - 1F, SRgbCompanding.LinearSlope * SrgbBeta).Invoke(value);

        /// <summary>
        /// Applies the inverse extended IEC 61966-2-1 transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The linear signal values.</returns>
        public static Vector512<float> Expand(Vector512<float> value)
            => new SRgbCompanding.ExpandOperator(SrgbAlpha, SrgbAlpha - 1F, SRgbCompanding.LinearSlope * SrgbBeta).Invoke(value);

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
        /// <param name="value">The linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static Vector128<float> Compress(Vector128<float> value)
            => new SRgbCompanding.CompressOperator(SrgbAlpha, SrgbAlpha - 1F, SrgbBeta).Invoke(value);

        /// <summary>
        /// Applies the extended IEC 61966-2-1 transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static Vector256<float> Compress(Vector256<float> value)
            => new SRgbCompanding.CompressOperator(SrgbAlpha, SrgbAlpha - 1F, SrgbBeta).Invoke(value);

        /// <summary>
        /// Applies the extended IEC 61966-2-1 transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static Vector512<float> Compress(Vector512<float> value)
            => new SRgbCompanding.CompressOperator(SrgbAlpha, SrgbAlpha - 1F, SrgbBeta).Invoke(value);
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
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The normalized linear signal values.</returns>
        public static Vector128<float> Expand(Vector128<float> value)
        {
            Vector128<float> zero = Vector128.Create(0F);
            Vector128<float> nonlinearPower = Vector128_.Pow(Vector128.Min(Vector128.Max(value, zero), Vector128.Create(1F)), 1F / PqM);
            Vector128<float> numerator = Vector128.Max(Vector128.Subtract(nonlinearPower, Vector128.Create(PqC1)), zero);
            Vector128<float> denominator = Vector128.Subtract(Vector128.Create(PqC2), Vector128.Multiply(Vector128.Create(PqC3), nonlinearPower));
            Vector128<float> positive = Vector128.Min(Vector128_.Pow(Vector128.Divide(numerator, denominator), 1F / PqN), Vector128.Create(1F));
            return Vector128.ConditionalSelect(Vector128.LessThanOrEqual(value, zero), zero, positive);
        }

        /// <summary>
        /// Applies the inverse SMPTE ST 2084 perceptual-quantizer transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The normalized linear signal values.</returns>
        public static Vector256<float> Expand(Vector256<float> value)
        {
            Vector256<float> zero = Vector256.Create(0F);
            Vector256<float> nonlinearPower = Vector256_.Pow(Vector256.Min(Vector256.Max(value, zero), Vector256.Create(1F)), 1F / PqM);
            Vector256<float> numerator = Vector256.Max(Vector256.Subtract(nonlinearPower, Vector256.Create(PqC1)), zero);
            Vector256<float> denominator = Vector256.Subtract(Vector256.Create(PqC2), Vector256.Multiply(Vector256.Create(PqC3), nonlinearPower));
            Vector256<float> positive = Vector256.Min(Vector256_.Pow(Vector256.Divide(numerator, denominator), 1F / PqN), Vector256.Create(1F));
            return Vector256.ConditionalSelect(Vector256.LessThanOrEqual(value, zero), zero, positive);
        }

        /// <summary>
        /// Applies the inverse SMPTE ST 2084 perceptual-quantizer transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The normalized linear signal values.</returns>
        public static Vector512<float> Expand(Vector512<float> value)
        {
            Vector512<float> zero = Vector512.Create(0F);
            Vector512<float> nonlinearPower = Vector512_.Pow(Vector512.Min(Vector512.Max(value, zero), Vector512.Create(1F)), 1F / PqM);
            Vector512<float> numerator = Vector512.Max(Vector512.Subtract(nonlinearPower, Vector512.Create(PqC1)), zero);
            Vector512<float> denominator = Vector512.Subtract(Vector512.Create(PqC2), Vector512.Multiply(Vector512.Create(PqC3), nonlinearPower));
            Vector512<float> positive = Vector512.Min(Vector512_.Pow(Vector512.Divide(numerator, denominator), 1F / PqN), Vector512.Create(1F));
            return Vector512.ConditionalSelect(Vector512.LessThanOrEqual(value, zero), zero, positive);
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
        /// <param name="value">The normalized linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static Vector128<float> Compress(Vector128<float> value)
        {
            Vector128<float> zero = Vector128.Create(0F);
            Vector128<float> linearPower = Vector128_.Pow(Vector128.Min(Vector128.Max(value, zero), Vector128.Create(1F)), PqN);
            Vector128<float> numerator = Vector128.Add(Vector128.Create(PqC1), Vector128.Multiply(Vector128.Create(PqC2), linearPower));
            Vector128<float> denominator = Vector128.Add(Vector128.Create(1F), Vector128.Multiply(Vector128.Create(PqC3), linearPower));
            Vector128<float> positive = Vector128.Min(Vector128_.Pow(Vector128.Divide(numerator, denominator), PqM), Vector128.Create(1F));
            return Vector128.ConditionalSelect(Vector128.LessThanOrEqual(value, zero), zero, positive);
        }

        /// <summary>
        /// Applies the SMPTE ST 2084 perceptual-quantizer transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The normalized linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static Vector256<float> Compress(Vector256<float> value)
        {
            Vector256<float> zero = Vector256.Create(0F);
            Vector256<float> linearPower = Vector256_.Pow(Vector256.Min(Vector256.Max(value, zero), Vector256.Create(1F)), PqN);
            Vector256<float> numerator = Vector256.Add(Vector256.Create(PqC1), Vector256.Multiply(Vector256.Create(PqC2), linearPower));
            Vector256<float> denominator = Vector256.Add(Vector256.Create(1F), Vector256.Multiply(Vector256.Create(PqC3), linearPower));
            Vector256<float> positive = Vector256.Min(Vector256_.Pow(Vector256.Divide(numerator, denominator), PqM), Vector256.Create(1F));
            return Vector256.ConditionalSelect(Vector256.LessThanOrEqual(value, zero), zero, positive);
        }

        /// <summary>
        /// Applies the SMPTE ST 2084 perceptual-quantizer transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The normalized linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static Vector512<float> Compress(Vector512<float> value)
        {
            Vector512<float> zero = Vector512.Create(0F);
            Vector512<float> linearPower = Vector512_.Pow(Vector512.Min(Vector512.Max(value, zero), Vector512.Create(1F)), PqN);
            Vector512<float> numerator = Vector512.Add(Vector512.Create(PqC1), Vector512.Multiply(Vector512.Create(PqC2), linearPower));
            Vector512<float> denominator = Vector512.Add(Vector512.Create(1F), Vector512.Multiply(Vector512.Create(PqC3), linearPower));
            Vector512<float> positive = Vector512.Min(Vector512_.Pow(Vector512.Divide(numerator, denominator), PqM), Vector512.Create(1F));
            return Vector512.ConditionalSelect(Vector512.LessThanOrEqual(value, zero), zero, positive);
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
        public static Vector128<float> Expand(Vector128<float> value)
        {
            Vector128<float> zero = Vector128.Create(0F);

            return Vector128.Divide(Vector128_.Pow(Vector128.Max(value, zero), Gamma), Vector128.Create(Smpte428Scale));
        }

        /// <inheritdoc/>
        public static Vector256<float> Expand(Vector256<float> value)
        {
            Vector256<float> zero = Vector256.Create(0F);

            return Vector256.Divide(Vector256_.Pow(Vector256.Max(value, zero), Gamma), Vector256.Create(Smpte428Scale));
        }

        /// <inheritdoc/>
        public static Vector512<float> Expand(Vector512<float> value)
        {
            Vector512<float> zero = Vector512.Create(0F);

            return Vector512.Divide(Vector512_.Pow(Vector512.Max(value, zero), Gamma), Vector512.Create(Smpte428Scale));
        }

        /// <inheritdoc/>
        public static float Compress(float value)
        {
            return new TensorPrimitives_.PowOperator(1F / Gamma).Invoke(Smpte428Scale * MathF.Max(value, 0F));
        }

        /// <inheritdoc/>
        public static Vector128<float> Compress(Vector128<float> value)
        {
            Vector128<float> zero = Vector128.Create(0F);

            return Vector128_.Pow(Vector128.Multiply(Vector128.Create(Smpte428Scale), Vector128.Max(value, zero)), 1F / Gamma);
        }

        /// <inheritdoc/>
        public static Vector256<float> Compress(Vector256<float> value)
        {
            Vector256<float> zero = Vector256.Create(0F);

            return Vector256_.Pow(Vector256.Multiply(Vector256.Create(Smpte428Scale), Vector256.Max(value, zero)), 1F / Gamma);
        }

        /// <inheritdoc/>
        public static Vector512<float> Compress(Vector512<float> value)
        {
            Vector512<float> zero = Vector512.Create(0F);

            return Vector512_.Pow(Vector512.Multiply(Vector512.Create(Smpte428Scale), Vector512.Max(value, zero)), 1F / Gamma);
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
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The normalized linear signal values.</returns>
        public static Vector128<float> Expand(Vector128<float> value)
        {
            Vector128<float> zero = Vector128.Create(0F);
            Vector128<float> bounded = Vector128.Min(Vector128.Max(value, zero), Vector128.Create(1F));
            Vector128<float> linear = Vector128.Divide(Vector128.Multiply(bounded, bounded), Vector128.Create(3F));
            Vector128<float> exponent = Vector128.Divide(Vector128.Subtract(bounded, Vector128.Create(HlgC)), Vector128.Create(HlgA));
            Vector128<float> logarithmic = Vector128.Divide(Vector128.Add(Vector128.Exp(exponent), Vector128.Create(HlgB)), Vector128.Create(12F));
            Vector128<float> positive = Vector128.ConditionalSelect(Vector128.LessThanOrEqual(value, Vector128.Create(0.5F)), linear, logarithmic);
            return Vector128.ConditionalSelect(Vector128.LessThanOrEqual(value, zero), zero, positive);
        }

        /// <summary>
        /// Applies the inverse HLG opto-electronic transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The normalized linear signal values.</returns>
        public static Vector256<float> Expand(Vector256<float> value)
        {
            Vector256<float> zero = Vector256.Create(0F);
            Vector256<float> bounded = Vector256.Min(Vector256.Max(value, zero), Vector256.Create(1F));
            Vector256<float> linear = Vector256.Divide(Vector256.Multiply(bounded, bounded), Vector256.Create(3F));
            Vector256<float> exponent = Vector256.Divide(Vector256.Subtract(bounded, Vector256.Create(HlgC)), Vector256.Create(HlgA));
            Vector256<float> logarithmic = Vector256.Divide(Vector256.Add(Vector256.Exp(exponent), Vector256.Create(HlgB)), Vector256.Create(12F));
            Vector256<float> positive = Vector256.ConditionalSelect(Vector256.LessThanOrEqual(value, Vector256.Create(0.5F)), linear, logarithmic);
            return Vector256.ConditionalSelect(Vector256.LessThanOrEqual(value, zero), zero, positive);
        }

        /// <summary>
        /// Applies the inverse HLG opto-electronic transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The nonlinear signal values.</param>
        /// <returns>The normalized linear signal values.</returns>
        public static Vector512<float> Expand(Vector512<float> value)
        {
            Vector512<float> zero = Vector512.Create(0F);
            Vector512<float> bounded = Vector512.Min(Vector512.Max(value, zero), Vector512.Create(1F));
            Vector512<float> linear = Vector512.Divide(Vector512.Multiply(bounded, bounded), Vector512.Create(3F));
            Vector512<float> exponent = Vector512.Divide(Vector512.Subtract(bounded, Vector512.Create(HlgC)), Vector512.Create(HlgA));
            Vector512<float> logarithmic = Vector512.Divide(Vector512.Add(Vector512.Exp(exponent), Vector512.Create(HlgB)), Vector512.Create(12F));
            Vector512<float> positive = Vector512.ConditionalSelect(Vector512.LessThanOrEqual(value, Vector512.Create(0.5F)), linear, logarithmic);
            return Vector512.ConditionalSelect(Vector512.LessThanOrEqual(value, zero), zero, positive);
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
        /// <param name="value">The normalized linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static Vector128<float> Compress(Vector128<float> value)
        {
            Vector128<float> zero = Vector128.Create(0F);
            Vector128<float> bounded = Vector128.Min(Vector128.Max(value, zero), Vector128.Create(1F));
            Vector128<float> linear = Vector128.Sqrt(Vector128.Multiply(Vector128.Create(3F), bounded));

            // Clamp the logarithm input for inactive lanes. SIMD conditional selection evaluates both branches,
            // while the scalar definition evaluates Log only above the 1/12 transition.
            Vector128<float> logarithmInput = Vector128.Max(
                Vector128.Subtract(Vector128.Multiply(Vector128.Create(12F), bounded), Vector128.Create(HlgB)),
                Vector128.Create(float.Epsilon));

            Vector128<float> logarithmic = Vector128.Add(Vector128.Multiply(Vector128.Create(HlgA), Vector128.Log(logarithmInput)), Vector128.Create(HlgC));
            Vector128<float> positive = Vector128.ConditionalSelect(Vector128.LessThanOrEqual(bounded, Vector128.Create(1F / 12F)), linear, logarithmic);
            return Vector128.ConditionalSelect(Vector128.LessThanOrEqual(value, zero), zero, positive);
        }

        /// <summary>
        /// Applies the HLG opto-electronic transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The normalized linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static Vector256<float> Compress(Vector256<float> value)
        {
            Vector256<float> zero = Vector256.Create(0F);
            Vector256<float> bounded = Vector256.Min(Vector256.Max(value, zero), Vector256.Create(1F));
            Vector256<float> linear = Vector256.Sqrt(Vector256.Multiply(Vector256.Create(3F), bounded));

            // Clamp the logarithm input for inactive lanes. SIMD conditional selection evaluates both branches,
            // while the scalar definition evaluates Log only above the 1/12 transition.
            Vector256<float> logarithmInput = Vector256.Max(
                Vector256.Subtract(Vector256.Multiply(Vector256.Create(12F), bounded), Vector256.Create(HlgB)),
                Vector256.Create(float.Epsilon));

            Vector256<float> logarithmic = Vector256.Add(Vector256.Multiply(Vector256.Create(HlgA), Vector256.Log(logarithmInput)), Vector256.Create(HlgC));
            Vector256<float> positive = Vector256.ConditionalSelect(Vector256.LessThanOrEqual(bounded, Vector256.Create(1F / 12F)), linear, logarithmic);
            return Vector256.ConditionalSelect(Vector256.LessThanOrEqual(value, zero), zero, positive);
        }

        /// <summary>
        /// Applies the HLG opto-electronic transfer function to a SIMD vector.
        /// </summary>
        /// <param name="value">The normalized linear signal values.</param>
        /// <returns>The nonlinear signal values.</returns>
        public static Vector512<float> Compress(Vector512<float> value)
        {
            Vector512<float> zero = Vector512.Create(0F);
            Vector512<float> bounded = Vector512.Min(Vector512.Max(value, zero), Vector512.Create(1F));
            Vector512<float> linear = Vector512.Sqrt(Vector512.Multiply(Vector512.Create(3F), bounded));

            // Clamp the logarithm input for inactive lanes. SIMD conditional selection evaluates both branches,
            // while the scalar definition evaluates Log only above the 1/12 transition.
            Vector512<float> logarithmInput = Vector512.Max(
                Vector512.Subtract(Vector512.Multiply(Vector512.Create(12F), bounded), Vector512.Create(HlgB)),
                Vector512.Create(float.Epsilon));

            Vector512<float> logarithmic = Vector512.Add(Vector512.Multiply(Vector512.Create(HlgA), Vector512.Log(logarithmInput)), Vector512.Create(HlgC));
            Vector512<float> positive = Vector512.ConditionalSelect(Vector512.LessThanOrEqual(bounded, Vector512.Create(1F / 12F)), linear, logarithmic);
            return Vector512.ConditionalSelect(Vector512.LessThanOrEqual(value, zero), zero, positive);
        }
    }
}
