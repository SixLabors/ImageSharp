// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Av1TransferVectorOperators;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <content>
/// Provides the stateless scalar and SIMD operators used by AV1 YUV-to-RGB row conversion.
/// </content>
internal static partial class Av1YuvConverter
{
    /// <summary>
    /// Defines one H.273 YUV-to-RGB operation for scalar and SIMD traversal.
    /// </summary>
    private interface IYuvToRgbOperator
    {
        /// <summary>
        /// Gets a value indicating whether the operator consumes chroma components.
        /// </summary>
        public static abstract bool UsesChroma { get; }

        /// <summary>
        /// Converts one normalized YUV sample to RGB.
        /// </summary>
        /// <param name="y">The normalized luma, replaced by red.</param>
        /// <param name="cb">The normalized blue-difference component, replaced by green.</param>
        /// <param name="cr">The normalized red-difference component, replaced by blue.</param>
        /// <param name="parameters">The resolved H.273 conversion parameters.</param>
        public static abstract void Convert(ref float y, ref float cb, ref float cr, in YuvToRgbParameters parameters);

        /// <summary>
        /// Converts four normalized YUV samples to RGB.
        /// </summary>
        /// <param name="y">The normalized luma lanes, replaced by red.</param>
        /// <param name="cb">The normalized blue-difference lanes, replaced by green.</param>
        /// <param name="cr">The normalized red-difference lanes, replaced by blue.</param>
        /// <param name="parameters">The resolved H.273 conversion parameters.</param>
        public static abstract void Convert(ref Vector128<float> y, ref Vector128<float> cb, ref Vector128<float> cr, in YuvToRgbParameters parameters);

        /// <summary>
        /// Converts eight normalized YUV samples to RGB.
        /// </summary>
        /// <param name="y">The normalized luma lanes, replaced by red.</param>
        /// <param name="cb">The normalized blue-difference lanes, replaced by green.</param>
        /// <param name="cr">The normalized red-difference lanes, replaced by blue.</param>
        /// <param name="parameters">The resolved H.273 conversion parameters.</param>
        public static abstract void Convert(ref Vector256<float> y, ref Vector256<float> cb, ref Vector256<float> cr, in YuvToRgbParameters parameters);

        /// <summary>
        /// Converts sixteen normalized YUV samples to RGB.
        /// </summary>
        /// <param name="y">The normalized luma lanes, replaced by red.</param>
        /// <param name="cb">The normalized blue-difference lanes, replaced by green.</param>
        /// <param name="cr">The normalized red-difference lanes, replaced by blue.</param>
        /// <param name="parameters">The resolved H.273 conversion parameters.</param>
        public static abstract void Convert(ref Vector512<float> y, ref Vector512<float> cb, ref Vector512<float> cr, in YuvToRgbParameters parameters);
    }

    /// <summary>
    /// Stores the resolved H.273 values shared by every scalar and SIMD lane.
    /// </summary>
    private readonly struct YuvToRgbParameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="YuvToRgbParameters"/> struct.
        /// </summary>
        /// <param name="kr">The red luma coefficient.</param>
        /// <param name="kg">The green luma coefficient.</param>
        /// <param name="kb">The blue luma coefficient.</param>
        /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
        /// <param name="constantLuminanceScales">The constant-luminance chroma scales.</param>
        /// <param name="lumaBias">The encoded luma bias.</param>
        /// <param name="lumaScale">The encoded luma range.</param>
        /// <param name="chromaBias">The encoded chroma midpoint.</param>
        /// <param name="chromaScale">The encoded chroma range.</param>
        public YuvToRgbParameters(
            float kr,
            float kg,
            float kb,
            ObuTransferCharacteristics transferCharacteristics,
            in ConstantLuminanceScales constantLuminanceScales,
            float lumaBias,
            float lumaScale,
            float chromaBias,
            float chromaScale)
        {
            this.Kr = kr;
            this.Kg = kg;
            this.Kb = kb;
            this.TransferCharacteristics = transferCharacteristics;
            this.ConstantLuminanceScales = constantLuminanceScales;
            this.LumaBias = lumaBias;
            this.LumaScale = lumaScale;
            this.ChromaBias = chromaBias;
            this.ChromaScale = chromaScale;
        }

        /// <summary>
        /// Gets the red luma coefficient.
        /// </summary>
        public float Kr { get; }

        /// <summary>
        /// Gets the green luma coefficient.
        /// </summary>
        public float Kg { get; }

        /// <summary>
        /// Gets the blue luma coefficient.
        /// </summary>
        public float Kb { get; }

        /// <summary>
        /// Gets the signaled transfer characteristics.
        /// </summary>
        public ObuTransferCharacteristics TransferCharacteristics { get; }

        /// <summary>
        /// Gets the constant-luminance chroma scales.
        /// </summary>
        public ConstantLuminanceScales ConstantLuminanceScales { get; }

        /// <summary>
        /// Gets the encoded luma bias.
        /// </summary>
        public float LumaBias { get; }

        /// <summary>
        /// Gets the encoded luma range.
        /// </summary>
        public float LumaScale { get; }

        /// <summary>
        /// Gets the encoded chroma midpoint.
        /// </summary>
        public float ChromaBias { get; }

        /// <summary>
        /// Gets the encoded chroma range.
        /// </summary>
        public float ChromaScale { get; }
    }

    /// <summary>
    /// Replicates luma into all three RGB components for monochrome input.
    /// </summary>
    private readonly struct MonochromeOperator : IYuvToRgbOperator
    {
        /// <inheritdoc/>
        public static bool UsesChroma => false;

        /// <inheritdoc/>
        public static void Convert(ref float y, ref float cb, ref float cr, in YuvToRgbParameters parameters)
        {
            cb = y;
            cr = y;
        }

        /// <inheritdoc/>
        public static void Convert(ref Vector128<float> y, ref Vector128<float> cb, ref Vector128<float> cr, in YuvToRgbParameters parameters)
        {
            cb = y;
            cr = y;
        }

        /// <inheritdoc/>
        public static void Convert(ref Vector256<float> y, ref Vector256<float> cb, ref Vector256<float> cr, in YuvToRgbParameters parameters)
        {
            cb = y;
            cr = y;
        }

        /// <inheritdoc/>
        public static void Convert(ref Vector512<float> y, ref Vector512<float> cb, ref Vector512<float> cr, in YuvToRgbParameters parameters)
        {
            cb = y;
            cr = y;
        }
    }

    /// <summary>
    /// Converts coefficient-based YCbCr signals to RGB.
    /// </summary>
    private readonly struct CoefficientsOperator : IYuvToRgbOperator
    {
        /// <inheritdoc/>
        public static bool UsesChroma => true;

        /// <inheritdoc/>
        public static void Convert(ref float y, ref float cb, ref float cr, in YuvToRgbParameters parameters)
        {
            float r = y + (2F * (1F - parameters.Kr) * cr);
            float g = y - (2F * ((parameters.Kr * (1F - parameters.Kr) * cr) + (parameters.Kb * (1F - parameters.Kb) * cb)) / parameters.Kg);
            float b = y + (2F * (1F - parameters.Kb) * cb);
            y = r;
            cb = g;
            cr = b;
        }

        /// <inheritdoc/>
        public static void Convert(ref Vector128<float> y, ref Vector128<float> cb, ref Vector128<float> cr, in YuvToRgbParameters parameters)
            => YuvToRgbMath.ConvertCoefficients<Vector128<float>, Vector128Operator>(ref y, ref cb, ref cr, in parameters);

        /// <inheritdoc/>
        public static void Convert(ref Vector256<float> y, ref Vector256<float> cb, ref Vector256<float> cr, in YuvToRgbParameters parameters)
            => YuvToRgbMath.ConvertCoefficients<Vector256<float>, Vector256Operator>(ref y, ref cb, ref cr, in parameters);

        /// <inheritdoc/>
        public static void Convert(ref Vector512<float> y, ref Vector512<float> cb, ref Vector512<float> cr, in YuvToRgbParameters parameters)
            => YuvToRgbMath.ConvertCoefficients<Vector512<float>, Vector512Operator>(ref y, ref cb, ref cr, in parameters);
    }

    /// <summary>
    /// Maps identity-coded G, B, and R planes to RGB.
    /// </summary>
    private readonly struct IdentityOperator : IYuvToRgbOperator
    {
        /// <inheritdoc/>
        public static bool UsesChroma => true;

        /// <inheritdoc/>
        public static void Convert(ref float y, ref float cb, ref float cr, in YuvToRgbParameters parameters)
        {
            float r = (((cr * parameters.ChromaScale) + parameters.ChromaBias) - parameters.LumaBias) / parameters.LumaScale;
            float b = (((cb * parameters.ChromaScale) + parameters.ChromaBias) - parameters.LumaBias) / parameters.LumaScale;
            cb = y;
            y = r;
            cr = b;
        }

        /// <inheritdoc/>
        public static void Convert(ref Vector128<float> y, ref Vector128<float> cb, ref Vector128<float> cr, in YuvToRgbParameters parameters)
            => YuvToRgbMath.ConvertIdentity<Vector128<float>, Vector128Operator>(ref y, ref cb, ref cr, in parameters);

        /// <inheritdoc/>
        public static void Convert(ref Vector256<float> y, ref Vector256<float> cb, ref Vector256<float> cr, in YuvToRgbParameters parameters)
            => YuvToRgbMath.ConvertIdentity<Vector256<float>, Vector256Operator>(ref y, ref cb, ref cr, in parameters);

        /// <inheritdoc/>
        public static void Convert(ref Vector512<float> y, ref Vector512<float> cb, ref Vector512<float> cr, in YuvToRgbParameters parameters)
            => YuvToRgbMath.ConvertIdentity<Vector512<float>, Vector512Operator>(ref y, ref cb, ref cr, in parameters);
    }

    /// <summary>
    /// Converts YCgCo signals to RGB.
    /// </summary>
    private readonly struct YCgCoOperator : IYuvToRgbOperator
    {
        /// <inheritdoc/>
        public static bool UsesChroma => true;

        /// <inheritdoc/>
        public static void Convert(ref float y, ref float cb, ref float cr, in YuvToRgbParameters parameters)
        {
            float temporary = y - cb;
            float r = temporary + cr;
            float g = y + cb;
            float b = temporary - cr;
            y = r;
            cb = g;
            cr = b;
        }

        /// <inheritdoc/>
        public static void Convert(ref Vector128<float> y, ref Vector128<float> cb, ref Vector128<float> cr, in YuvToRgbParameters parameters)
            => YuvToRgbMath.ConvertYCgCo<Vector128<float>, Vector128Operator>(ref y, ref cb, ref cr);

        /// <inheritdoc/>
        public static void Convert(ref Vector256<float> y, ref Vector256<float> cb, ref Vector256<float> cr, in YuvToRgbParameters parameters)
            => YuvToRgbMath.ConvertYCgCo<Vector256<float>, Vector256Operator>(ref y, ref cb, ref cr);

        /// <inheritdoc/>
        public static void Convert(ref Vector512<float> y, ref Vector512<float> cb, ref Vector512<float> cr, in YuvToRgbParameters parameters)
            => YuvToRgbMath.ConvertYCgCo<Vector512<float>, Vector512Operator>(ref y, ref cb, ref cr);
    }

    /// <summary>
    /// Converts SMPTE ST 2085 YDzDx signals to RGB.
    /// </summary>
    private readonly struct Smpte2085Operator : IYuvToRgbOperator
    {
        /// <inheritdoc/>
        public static bool UsesChroma => true;

        /// <inheritdoc/>
        public static void Convert(ref float y, ref float cb, ref float cr, in YuvToRgbParameters parameters)
        {
            float g = y;
            float b = ((2F * cb) + y) / 0.986566F;
            float r = (2F * cr) + (0.991902F * y);
            y = r;
            cb = g;
            cr = b;
        }

        /// <inheritdoc/>
        public static void Convert(ref Vector128<float> y, ref Vector128<float> cb, ref Vector128<float> cr, in YuvToRgbParameters parameters)
            => YuvToRgbMath.ConvertSmpte2085<Vector128<float>, Vector128Operator>(ref y, ref cb, ref cr);

        /// <inheritdoc/>
        public static void Convert(ref Vector256<float> y, ref Vector256<float> cb, ref Vector256<float> cr, in YuvToRgbParameters parameters)
            => YuvToRgbMath.ConvertSmpte2085<Vector256<float>, Vector256Operator>(ref y, ref cb, ref cr);

        /// <inheritdoc/>
        public static void Convert(ref Vector512<float> y, ref Vector512<float> cb, ref Vector512<float> cr, in YuvToRgbParameters parameters)
            => YuvToRgbMath.ConvertSmpte2085<Vector512<float>, Vector512Operator>(ref y, ref cb, ref cr);
    }

    /// <summary>
    /// Converts H.273 constant-luminance signals to RGB.
    /// </summary>
    private readonly struct ConstantLuminanceOperator : IYuvToRgbOperator
    {
        /// <inheritdoc/>
        public static bool UsesChroma => true;

        /// <inheritdoc/>
        public static void Convert(ref float y, ref float cb, ref float cr, in YuvToRgbParameters parameters)
        {
            ConstantLuminanceScales scales = parameters.ConstantLuminanceScales;
            float nonlinearBlue = y + (2F * (cb <= 0F ? scales.NegativeBlue : scales.PositiveBlue) * cb);
            float nonlinearRed = y + (2F * (cr <= 0F ? scales.NegativeRed : scales.PositiveRed) * cr);
            float linearY = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, y);
            float linearBlue = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearBlue);
            float linearRed = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearRed);
            float linearGreen = (linearY - (parameters.Kr * linearRed) - (parameters.Kb * linearBlue)) / parameters.Kg;
            y = nonlinearRed;
            cb = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearGreen);
            cr = nonlinearBlue;
        }

        /// <inheritdoc/>
        public static void Convert(ref Vector128<float> y, ref Vector128<float> cb, ref Vector128<float> cr, in YuvToRgbParameters parameters)
            => YuvToRgbMath.ConvertConstantLuminance<Vector128<float>, Vector128Operator>(ref y, ref cb, ref cr, in parameters);

        /// <inheritdoc/>
        public static void Convert(ref Vector256<float> y, ref Vector256<float> cb, ref Vector256<float> cr, in YuvToRgbParameters parameters)
            => YuvToRgbMath.ConvertConstantLuminance<Vector256<float>, Vector256Operator>(ref y, ref cb, ref cr, in parameters);

        /// <inheritdoc/>
        public static void Convert(ref Vector512<float> y, ref Vector512<float> cb, ref Vector512<float> cr, in YuvToRgbParameters parameters)
            => YuvToRgbMath.ConvertConstantLuminance<Vector512<float>, Vector512Operator>(ref y, ref cb, ref cr, in parameters);
    }

    /// <summary>
    /// Converts the PQ-family ICtCp matrix to RGB.
    /// </summary>
    private readonly struct ICtCpOperator : IYuvToRgbOperator
    {
        /// <inheritdoc/>
        public static bool UsesChroma => true;

        /// <inheritdoc/>
        public static void Convert(ref float y, ref float cb, ref float cr, in YuvToRgbParameters parameters)
            => YuvToRgbMath.ConvertICtCpScalar(ref y, ref cb, ref cr, in parameters, false);

        /// <inheritdoc/>
        public static void Convert(ref Vector128<float> y, ref Vector128<float> cb, ref Vector128<float> cr, in YuvToRgbParameters parameters)
            => YuvToRgbMath.ConvertICtCp<Vector128<float>, Vector128Operator>(ref y, ref cb, ref cr, in parameters, false);

        /// <inheritdoc/>
        public static void Convert(ref Vector256<float> y, ref Vector256<float> cb, ref Vector256<float> cr, in YuvToRgbParameters parameters)
            => YuvToRgbMath.ConvertICtCp<Vector256<float>, Vector256Operator>(ref y, ref cb, ref cr, in parameters, false);

        /// <inheritdoc/>
        public static void Convert(ref Vector512<float> y, ref Vector512<float> cb, ref Vector512<float> cr, in YuvToRgbParameters parameters)
            => YuvToRgbMath.ConvertICtCp<Vector512<float>, Vector512Operator>(ref y, ref cb, ref cr, in parameters, false);
    }

    /// <summary>
    /// Converts the HLG-specific ICtCp matrix to RGB.
    /// </summary>
    private readonly struct ICtCpHlgOperator : IYuvToRgbOperator
    {
        /// <inheritdoc/>
        public static bool UsesChroma => true;

        /// <inheritdoc/>
        public static void Convert(ref float y, ref float cb, ref float cr, in YuvToRgbParameters parameters)
            => YuvToRgbMath.ConvertICtCpScalar(ref y, ref cb, ref cr, in parameters, true);

        /// <inheritdoc/>
        public static void Convert(ref Vector128<float> y, ref Vector128<float> cb, ref Vector128<float> cr, in YuvToRgbParameters parameters)
            => YuvToRgbMath.ConvertICtCp<Vector128<float>, Vector128Operator>(ref y, ref cb, ref cr, in parameters, true);

        /// <inheritdoc/>
        public static void Convert(ref Vector256<float> y, ref Vector256<float> cb, ref Vector256<float> cr, in YuvToRgbParameters parameters)
            => YuvToRgbMath.ConvertICtCp<Vector256<float>, Vector256Operator>(ref y, ref cb, ref cr, in parameters, true);

        /// <inheritdoc/>
        public static void Convert(ref Vector512<float> y, ref Vector512<float> cb, ref Vector512<float> cr, in YuvToRgbParameters parameters)
            => YuvToRgbMath.ConvertICtCp<Vector512<float>, Vector512Operator>(ref y, ref cb, ref cr, in parameters, true);
    }

    /// <summary>
    /// Contains the shared vector-width-independent arithmetic used by the color operators.
    /// </summary>
    private static class YuvToRgbMath
    {
        /// <summary>
        /// Converts coefficient-based YCbCr SIMD lanes to RGB.
        /// </summary>
        /// <typeparam name="TVector">The SIMD vector type.</typeparam>
        /// <typeparam name="TOperator">The operations for the SIMD vector type.</typeparam>
        /// <param name="y">The luma lanes, replaced by red.</param>
        /// <param name="cb">The blue-difference lanes, replaced by green.</param>
        /// <param name="cr">The red-difference lanes, replaced by blue.</param>
        /// <param name="parameters">The resolved H.273 conversion parameters.</param>
        public static void ConvertCoefficients<TVector, TOperator>(ref TVector y, ref TVector cb, ref TVector cr, in YuvToRgbParameters parameters)
            where TVector : struct
            where TOperator : struct, ITransferVectorOperator<TVector>
        {
            TVector r = TOperator.MultiplyAddEstimate(TOperator.Create(2F * (1F - parameters.Kr)), cr, y);
            TVector greenDifference = TOperator.MultiplyAddEstimate(
                TOperator.Create(parameters.Kr * (1F - parameters.Kr)),
                cr,
                TOperator.Multiply(TOperator.Create(parameters.Kb * (1F - parameters.Kb)), cb));

            TVector g = TOperator.Subtract(y, TOperator.Multiply(TOperator.Create(2F / parameters.Kg), greenDifference));
            TVector b = TOperator.MultiplyAddEstimate(TOperator.Create(2F * (1F - parameters.Kb)), cb, y);
            y = r;
            cb = g;
            cr = b;
        }

        /// <summary>
        /// Maps identity-coded SIMD lanes to RGB.
        /// </summary>
        /// <typeparam name="TVector">The SIMD vector type.</typeparam>
        /// <typeparam name="TOperator">The operations for the SIMD vector type.</typeparam>
        /// <param name="y">The green lanes, replaced by red.</param>
        /// <param name="cb">The normalized blue plane, replaced by green.</param>
        /// <param name="cr">The normalized red plane, replaced by blue.</param>
        /// <param name="parameters">The resolved H.273 conversion parameters.</param>
        public static void ConvertIdentity<TVector, TOperator>(ref TVector y, ref TVector cb, ref TVector cr, in YuvToRgbParameters parameters)
            where TVector : struct
            where TOperator : struct, ITransferVectorOperator<TVector>
        {
            TVector lumaBias = TOperator.Create(parameters.LumaBias);
            TVector inverseLumaScale = TOperator.Create(1F / parameters.LumaScale);
            TVector chromaScale = TOperator.Create(parameters.ChromaScale);
            TVector chromaBias = TOperator.Create(parameters.ChromaBias);
            TVector r = TOperator.Multiply(TOperator.Subtract(TOperator.MultiplyAddEstimate(cr, chromaScale, chromaBias), lumaBias), inverseLumaScale);
            TVector b = TOperator.Multiply(TOperator.Subtract(TOperator.MultiplyAddEstimate(cb, chromaScale, chromaBias), lumaBias), inverseLumaScale);
            cb = y;
            y = r;
            cr = b;
        }

        /// <summary>
        /// Converts YCgCo SIMD lanes to RGB.
        /// </summary>
        /// <typeparam name="TVector">The SIMD vector type.</typeparam>
        /// <typeparam name="TOperator">The operations for the SIMD vector type.</typeparam>
        /// <param name="y">The luma lanes, replaced by red.</param>
        /// <param name="cb">The green-difference lanes, replaced by green.</param>
        /// <param name="cr">The orange-difference lanes, replaced by blue.</param>
        public static void ConvertYCgCo<TVector, TOperator>(ref TVector y, ref TVector cb, ref TVector cr)
            where TVector : struct
            where TOperator : struct, ITransferVectorOperator<TVector>
        {
            TVector temporary = TOperator.Subtract(y, cb);
            TVector r = TOperator.Add(temporary, cr);
            TVector g = TOperator.Add(y, cb);
            TVector b = TOperator.Subtract(temporary, cr);
            y = r;
            cb = g;
            cr = b;
        }

        /// <summary>
        /// Converts SMPTE ST 2085 SIMD lanes to RGB.
        /// </summary>
        /// <typeparam name="TVector">The SIMD vector type.</typeparam>
        /// <typeparam name="TOperator">The operations for the SIMD vector type.</typeparam>
        /// <param name="y">The luma lanes, replaced by red.</param>
        /// <param name="cb">The blue-difference lanes, replaced by green.</param>
        /// <param name="cr">The red-difference lanes, replaced by blue.</param>
        public static void ConvertSmpte2085<TVector, TOperator>(ref TVector y, ref TVector cb, ref TVector cr)
            where TVector : struct
            where TOperator : struct, ITransferVectorOperator<TVector>
        {
            TVector g = y;
            TVector b = TOperator.Multiply(TOperator.MultiplyAddEstimate(TOperator.Create(2F), cb, y), TOperator.Create(1F / 0.986566F));
            TVector r = TOperator.MultiplyAddEstimate(TOperator.Create(2F), cr, TOperator.Multiply(TOperator.Create(0.991902F), y));
            y = r;
            cb = g;
            cr = b;
        }

        /// <summary>
        /// Converts constant-luminance SIMD lanes to RGB.
        /// </summary>
        /// <typeparam name="TVector">The SIMD vector type.</typeparam>
        /// <typeparam name="TOperator">The operations for the SIMD vector type.</typeparam>
        /// <param name="y">The nonlinear luma lanes, replaced by red.</param>
        /// <param name="cb">The blue-difference lanes, replaced by green.</param>
        /// <param name="cr">The red-difference lanes, replaced by blue.</param>
        /// <param name="parameters">The resolved H.273 conversion parameters.</param>
        public static void ConvertConstantLuminance<TVector, TOperator>(ref TVector y, ref TVector cb, ref TVector cr, in YuvToRgbParameters parameters)
            where TVector : struct
            where TOperator : struct, ITransferVectorOperator<TVector>
        {
            TVector zero = TOperator.Create(0F);
            ConstantLuminanceScales scales = parameters.ConstantLuminanceScales;
            TVector blueScale = TOperator.ConditionalSelect(
                TOperator.LessThanOrEqual(cb, zero),
                TOperator.Create(scales.NegativeBlue),
                TOperator.Create(scales.PositiveBlue));

            TVector redScale = TOperator.ConditionalSelect(
                TOperator.LessThanOrEqual(cr, zero),
                TOperator.Create(scales.NegativeRed),
                TOperator.Create(scales.PositiveRed));

            TVector nonlinearBlue = TOperator.MultiplyAddEstimate(TOperator.Multiply(TOperator.Create(2F), blueScale), cb, y);
            TVector nonlinearRed = TOperator.MultiplyAddEstimate(TOperator.Multiply(TOperator.Create(2F), redScale), cr, y);
            TVector linearY = TOperator.ToLinear(parameters.TransferCharacteristics, y);
            TVector linearBlue = TOperator.ToLinear(parameters.TransferCharacteristics, nonlinearBlue);
            TVector linearRed = TOperator.ToLinear(parameters.TransferCharacteristics, nonlinearRed);
            TVector redAndBlue = TOperator.MultiplyAddEstimate(
                TOperator.Create(parameters.Kr),
                linearRed,
                TOperator.Multiply(TOperator.Create(parameters.Kb), linearBlue));

            TVector linearGreen = TOperator.Divide(TOperator.Subtract(linearY, redAndBlue), TOperator.Create(parameters.Kg));
            y = nonlinearRed;
            cb = TOperator.ToGamma(parameters.TransferCharacteristics, linearGreen);
            cr = nonlinearBlue;
        }

        /// <summary>
        /// Converts one ICtCp sample to RGB using the selected inverse matrix.
        /// </summary>
        /// <param name="y">The intensity, replaced by red.</param>
        /// <param name="cb">The tritan-difference component, replaced by green.</param>
        /// <param name="cr">The protan-difference component, replaced by blue.</param>
        /// <param name="parameters">The resolved H.273 conversion parameters.</param>
        /// <param name="isHlg">Whether to use the HLG-specific inverse ICtCp matrix.</param>
        public static void ConvertICtCpScalar(ref float y, ref float cb, ref float cr, in YuvToRgbParameters parameters, bool isHlg)
        {
            float nonlinearL = isHlg
                ? y + (0.015718580108730413F * cb) + (0.2095810681164055F * cr)
                : y + (0.008609037037932756F * cb) + (0.11102962500302596F * cr);

            float nonlinearM = isHlg
                ? y - (0.015718580108730413F * cb) - (0.2095810681164055F * cr)
                : y - (0.008609037037932756F * cb) - (0.11102962500302596F * cr);

            float nonlinearS = isHlg
                ? y + (1.0212710798422342F * cb) - (0.6052744909924315F * cr)
                : y + (0.5600313357106791F * cb) - (0.32062717498731885F * cr);

            float linearL = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearL);
            float linearM = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearM);
            float linearS = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearS);
            float linearRed = (3.4366066943330784F * linearL) - (2.50645211865627F * linearM) + (0.06984542432319148F * linearS);
            float linearGreen = (-0.7913295555989287F * linearL) + (1.9836004517922907F * linearM) - (0.192270896193362F * linearS);
            float linearBlue = (-0.025949899690592672F * linearL) - (0.09891371471172644F * linearM) + (1.1248636144023192F * linearS);
            y = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearRed);
            cb = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearGreen);
            cr = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearBlue);
        }

        /// <summary>
        /// Converts ICtCp SIMD lanes to RGB using the selected inverse matrix.
        /// </summary>
        /// <typeparam name="TVector">The SIMD vector type.</typeparam>
        /// <typeparam name="TOperator">The operations for the SIMD vector type.</typeparam>
        /// <param name="y">The intensity lanes, replaced by red.</param>
        /// <param name="cb">The tritan-difference lanes, replaced by green.</param>
        /// <param name="cr">The protan-difference lanes, replaced by blue.</param>
        /// <param name="parameters">The resolved H.273 conversion parameters.</param>
        /// <param name="isHlg">Whether to use the HLG-specific inverse ICtCp matrix.</param>
        public static void ConvertICtCp<TVector, TOperator>(ref TVector y, ref TVector cb, ref TVector cr, in YuvToRgbParameters parameters, bool isHlg)
            where TVector : struct
            where TOperator : struct, ITransferVectorOperator<TVector>
        {
            float cbToL = isHlg ? 0.015718580108730413F : 0.008609037037932756F;
            float crToL = isHlg ? 0.2095810681164055F : 0.11102962500302596F;
            float cbToS = isHlg ? 1.0212710798422342F : 0.5600313357106791F;
            float crToS = isHlg ? -0.6052744909924315F : -0.32062717498731885F;
            TVector cbContribution = TOperator.Multiply(TOperator.Create(cbToL), cb);
            TVector crContribution = TOperator.Multiply(TOperator.Create(crToL), cr);
            TVector nonlinearL = TOperator.Add(TOperator.Add(y, cbContribution), crContribution);
            TVector nonlinearM = TOperator.Subtract(TOperator.Subtract(y, cbContribution), crContribution);
            TVector nonlinearS = TOperator.MultiplyAddEstimate(TOperator.Create(crToS), cr, TOperator.MultiplyAddEstimate(TOperator.Create(cbToS), cb, y));
            TVector linearL = TOperator.ToLinear(parameters.TransferCharacteristics, nonlinearL);
            TVector linearM = TOperator.ToLinear(parameters.TransferCharacteristics, nonlinearM);
            TVector linearS = TOperator.ToLinear(parameters.TransferCharacteristics, nonlinearS);
            TVector linearRed = TOperator.MultiplyAddEstimate(
                TOperator.Create(0.06984542432319148F),
                linearS,
                TOperator.MultiplyAddEstimate(
                    TOperator.Create(-2.50645211865627F),
                    linearM,
                    TOperator.Multiply(TOperator.Create(3.4366066943330784F), linearL)));

            TVector linearGreen = TOperator.MultiplyAddEstimate(
                TOperator.Create(-0.192270896193362F),
                linearS,
                TOperator.MultiplyAddEstimate(
                    TOperator.Create(1.9836004517922907F),
                    linearM,
                    TOperator.Multiply(TOperator.Create(-0.7913295555989287F), linearL)));

            TVector linearBlue = TOperator.MultiplyAddEstimate(
                TOperator.Create(1.1248636144023192F),
                linearS,
                TOperator.MultiplyAddEstimate(
                    TOperator.Create(-0.09891371471172644F),
                    linearM,
                    TOperator.Multiply(TOperator.Create(-0.025949899690592672F), linearL)));

            y = TOperator.ToGamma(parameters.TransferCharacteristics, linearRed);
            cb = TOperator.ToGamma(parameters.TransferCharacteristics, linearGreen);
            cr = TOperator.ToGamma(parameters.TransferCharacteristics, linearBlue);
        }
    }
}
