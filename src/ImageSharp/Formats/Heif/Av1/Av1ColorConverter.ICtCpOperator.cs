// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal abstract partial class Av1ColorConverterBase
{
    /// <summary>
    /// Implements BT.2100 ICtCp conversion for scalar and SIMD lanes.
    /// </summary>
    internal readonly struct Av1ICtCpColorOperator : IAv1ColorOperator
    {
        /// <summary>
        /// The PQ Ct contribution to nonlinear L.
        /// </summary>
        public const float PqCtToL = 0.008609037037932756F;

        /// <summary>
        /// The PQ Cp contribution to nonlinear L.
        /// </summary>
        public const float PqCpToL = 0.11102962500302596F;

        /// <summary>
        /// The PQ Ct contribution to nonlinear S.
        /// </summary>
        public const float PqCtToS = 0.5600313357106791F;

        /// <summary>
        /// The PQ Cp contribution to nonlinear S.
        /// </summary>
        public const float PqCpToS = -0.32062717498731885F;

        /// <summary>
        /// The HLG Ct contribution to nonlinear L.
        /// </summary>
        public const float HlgCtToL = 0.015718580108730413F;

        /// <summary>
        /// The HLG Cp contribution to nonlinear L.
        /// </summary>
        public const float HlgCpToL = 0.2095810681164055F;

        /// <summary>
        /// The HLG Ct contribution to nonlinear S.
        /// </summary>
        public const float HlgCtToS = 1.0212710798422342F;

        /// <summary>
        /// The HLG Cp contribution to nonlinear S.
        /// </summary>
        public const float HlgCpToS = -0.6052744909924315F;

        /// <summary>
        /// The linear L contribution to red.
        /// </summary>
        public const float LToRed = 3.4366066943330784F;

        /// <summary>
        /// The linear M contribution to red.
        /// </summary>
        public const float MToRed = -2.50645211865627F;

        /// <summary>
        /// The linear S contribution to red.
        /// </summary>
        public const float SToRed = 0.06984542432319148F;

        /// <summary>
        /// The linear L contribution to green.
        /// </summary>
        public const float LToGreen = -0.7913295555989287F;

        /// <summary>
        /// The linear M contribution to green.
        /// </summary>
        public const float MToGreen = 1.9836004517922907F;

        /// <summary>
        /// The linear S contribution to green.
        /// </summary>
        public const float SToGreen = -0.192270896193362F;

        /// <summary>
        /// The linear L contribution to blue.
        /// </summary>
        public const float LToBlue = -0.025949899690592672F;

        /// <summary>
        /// The linear M contribution to blue.
        /// </summary>
        public const float MToBlue = -0.09891371471172644F;

        /// <summary>
        /// The linear S contribution to blue.
        /// </summary>
        public const float SToBlue = 1.1248636144023192F;

        /// <summary>
        /// The linear red contribution to L.
        /// </summary>
        public const float RedToL = 1688F / 4096F;

        /// <summary>
        /// The linear green contribution to L.
        /// </summary>
        public const float GreenToL = 2146F / 4096F;

        /// <summary>
        /// The linear blue contribution to L.
        /// </summary>
        public const float BlueToL = 262F / 4096F;

        /// <summary>
        /// The linear red contribution to M.
        /// </summary>
        public const float RedToM = 683F / 4096F;

        /// <summary>
        /// The linear green contribution to M.
        /// </summary>
        public const float GreenToM = 2951F / 4096F;

        /// <summary>
        /// The linear blue contribution to M.
        /// </summary>
        public const float BlueToM = 462F / 4096F;

        /// <summary>
        /// The linear red contribution to S.
        /// </summary>
        public const float RedToS = 99F / 4096F;

        /// <summary>
        /// The linear green contribution to S.
        /// </summary>
        public const float GreenToS = 309F / 4096F;

        /// <summary>
        /// The linear blue contribution to S.
        /// </summary>
        public const float BlueToS = 3688F / 4096F;

        /// <summary>
        /// The PQ nonlinear L contribution to Ct.
        /// </summary>
        public const float PqLToCt = 6610F / 4096F;

        /// <summary>
        /// The PQ nonlinear M contribution to Ct.
        /// </summary>
        public const float PqMToCt = -13613F / 4096F;

        /// <summary>
        /// The PQ nonlinear S contribution to Ct.
        /// </summary>
        public const float PqSToCt = 7003F / 4096F;

        /// <summary>
        /// The PQ nonlinear L contribution to Cp.
        /// </summary>
        public const float PqLToCp = 17933F / 4096F;

        /// <summary>
        /// The PQ nonlinear M contribution to Cp.
        /// </summary>
        public const float PqMToCp = -17390F / 4096F;

        /// <summary>
        /// The PQ nonlinear S contribution to Cp.
        /// </summary>
        public const float PqSToCp = -543F / 4096F;

        /// <summary>
        /// The HLG nonlinear L contribution to Ct.
        /// </summary>
        public const float HlgLToCt = 3625F / 4096F;

        /// <summary>
        /// The HLG nonlinear M contribution to Ct.
        /// </summary>
        public const float HlgMToCt = -7465F / 4096F;

        /// <summary>
        /// The HLG nonlinear S contribution to Ct.
        /// </summary>
        public const float HlgSToCt = 3840F / 4096F;

        /// <summary>
        /// The HLG nonlinear L contribution to Cp.
        /// </summary>
        public const float HlgLToCp = 9500F / 4096F;

        /// <summary>
        /// The HLG nonlinear M contribution to Cp.
        /// </summary>
        public const float HlgMToCp = -9212F / 4096F;

        /// <summary>
        /// The HLG nonlinear S contribution to Cp.
        /// </summary>
        public const float HlgSToCp = -288F / 4096F;

        /// <inheritdoc/>
        public static bool ChromaUsesLumaRange => false;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref float intensity, ref float ct, ref float cp, in Av1ColorConversionParameters parameters)
        {
            bool isHlg = parameters.TransferCharacteristics == ObuTransferCharacteristics.Hlg;
            float ctToL = isHlg ? HlgCtToL : PqCtToL;
            float cpToL = isHlg ? HlgCpToL : PqCpToL;

            // The inverse ICtCp matrix first reconstructs nonlinear LMS. The transfer curve is then
            // removed before the fixed LMS-to-RGB matrix and reapplied to the three output primaries.
            float nonlinearL = intensity + (ctToL * ct) + (cpToL * cp);
            float nonlinearM = intensity - (ctToL * ct) - (cpToL * cp);
            float nonlinearS = intensity + ((isHlg ? HlgCtToS : PqCtToS) * ct) + ((isHlg ? HlgCpToS : PqCpToS) * cp);
            float linearL = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearL);
            float linearM = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearM);
            float linearS = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearS);
            float linearRed = (LToRed * linearL) + (MToRed * linearM) + (SToRed * linearS);
            float linearGreen = (LToGreen * linearL) + (MToGreen * linearM) + (SToGreen * linearS);
            float linearBlue = (LToBlue * linearL) + (MToBlue * linearM) + (SToBlue * linearS);

            intensity = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearRed);
            ct = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearGreen);
            cp = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearBlue);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(
            ref Vector128<float> intensity,
            ref Vector128<float> ct,
            ref Vector128<float> cp,
            in Av1ColorConversionParameters parameters)
        {
            bool isHlg = parameters.TransferCharacteristics == ObuTransferCharacteristics.Hlg;
            Vector128<float> ctContribution = Vector128.Create(isHlg ? HlgCtToL : PqCtToL) * ct;
            Vector128<float> cpContribution = Vector128.Create(isHlg ? HlgCpToL : PqCpToL) * cp;
            Vector128<float> nonlinearL = intensity + ctContribution + cpContribution;
            Vector128<float> nonlinearM = intensity - ctContribution - cpContribution;
            Vector128<float> nonlinearS = Vector128.MultiplyAddEstimate(
                Vector128.Create(isHlg ? HlgCpToS : PqCpToS),
                cp,
                Vector128.MultiplyAddEstimate(Vector128.Create(isHlg ? HlgCtToS : PqCtToS), ct, intensity));
            Vector128<float> linearL = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearL);
            Vector128<float> linearM = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearM);
            Vector128<float> linearS = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearS);
            Vector128<float> linearRed = Vector128.MultiplyAddEstimate(
                Vector128.Create(SToRed),
                linearS,
                Vector128.MultiplyAddEstimate(Vector128.Create(MToRed), linearM, Vector128.Create(LToRed) * linearL));
            Vector128<float> linearGreen = Vector128.MultiplyAddEstimate(
                Vector128.Create(SToGreen),
                linearS,
                Vector128.MultiplyAddEstimate(Vector128.Create(MToGreen), linearM, Vector128.Create(LToGreen) * linearL));
            Vector128<float> linearBlue = Vector128.MultiplyAddEstimate(
                Vector128.Create(SToBlue),
                linearS,
                Vector128.MultiplyAddEstimate(Vector128.Create(MToBlue), linearM, Vector128.Create(LToBlue) * linearL));

            intensity = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearRed);
            ct = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearGreen);
            cp = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearBlue);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(
            ref Vector256<float> intensity,
            ref Vector256<float> ct,
            ref Vector256<float> cp,
            in Av1ColorConversionParameters parameters)
        {
            bool isHlg = parameters.TransferCharacteristics == ObuTransferCharacteristics.Hlg;
            Vector256<float> ctContribution = Vector256.Create(isHlg ? HlgCtToL : PqCtToL) * ct;
            Vector256<float> cpContribution = Vector256.Create(isHlg ? HlgCpToL : PqCpToL) * cp;
            Vector256<float> nonlinearL = intensity + ctContribution + cpContribution;
            Vector256<float> nonlinearM = intensity - ctContribution - cpContribution;
            Vector256<float> nonlinearS = Vector256.MultiplyAddEstimate(
                Vector256.Create(isHlg ? HlgCpToS : PqCpToS),
                cp,
                Vector256.MultiplyAddEstimate(Vector256.Create(isHlg ? HlgCtToS : PqCtToS), ct, intensity));
            Vector256<float> linearL = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearL);
            Vector256<float> linearM = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearM);
            Vector256<float> linearS = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearS);
            Vector256<float> linearRed = Vector256.MultiplyAddEstimate(
                Vector256.Create(SToRed),
                linearS,
                Vector256.MultiplyAddEstimate(Vector256.Create(MToRed), linearM, Vector256.Create(LToRed) * linearL));
            Vector256<float> linearGreen = Vector256.MultiplyAddEstimate(
                Vector256.Create(SToGreen),
                linearS,
                Vector256.MultiplyAddEstimate(Vector256.Create(MToGreen), linearM, Vector256.Create(LToGreen) * linearL));
            Vector256<float> linearBlue = Vector256.MultiplyAddEstimate(
                Vector256.Create(SToBlue),
                linearS,
                Vector256.MultiplyAddEstimate(Vector256.Create(MToBlue), linearM, Vector256.Create(LToBlue) * linearL));

            intensity = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearRed);
            ct = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearGreen);
            cp = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearBlue);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(
            ref Vector512<float> intensity,
            ref Vector512<float> ct,
            ref Vector512<float> cp,
            in Av1ColorConversionParameters parameters)
        {
            bool isHlg = parameters.TransferCharacteristics == ObuTransferCharacteristics.Hlg;
            Vector512<float> ctContribution = Vector512.Create(isHlg ? HlgCtToL : PqCtToL) * ct;
            Vector512<float> cpContribution = Vector512.Create(isHlg ? HlgCpToL : PqCpToL) * cp;
            Vector512<float> nonlinearL = intensity + ctContribution + cpContribution;
            Vector512<float> nonlinearM = intensity - ctContribution - cpContribution;
            Vector512<float> nonlinearS = Vector512.MultiplyAddEstimate(
                Vector512.Create(isHlg ? HlgCpToS : PqCpToS),
                cp,
                Vector512.MultiplyAddEstimate(Vector512.Create(isHlg ? HlgCtToS : PqCtToS), ct, intensity));
            Vector512<float> linearL = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearL);
            Vector512<float> linearM = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearM);
            Vector512<float> linearS = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearS);
            Vector512<float> linearRed = Vector512.MultiplyAddEstimate(
                Vector512.Create(SToRed),
                linearS,
                Vector512.MultiplyAddEstimate(Vector512.Create(MToRed), linearM, Vector512.Create(LToRed) * linearL));
            Vector512<float> linearGreen = Vector512.MultiplyAddEstimate(
                Vector512.Create(SToGreen),
                linearS,
                Vector512.MultiplyAddEstimate(Vector512.Create(MToGreen), linearM, Vector512.Create(LToGreen) * linearL));
            Vector512<float> linearBlue = Vector512.MultiplyAddEstimate(
                Vector512.Create(SToBlue),
                linearS,
                Vector512.MultiplyAddEstimate(Vector512.Create(MToBlue), linearM, Vector512.Create(LToBlue) * linearL));

            intensity = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearRed);
            ct = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearGreen);
            cp = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearBlue);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            float red,
            float green,
            float blue,
            in Av1ColorConversionParameters parameters,
            out float intensity,
            out float ct,
            out float cp)
        {
            // ICtCp is defined in nonlinear LMS. Convert RGB to linear light, apply the LMS matrix, then
            // apply the signaled PQ or HLG transfer curve before deriving intensity and the chroma axes.
            float linearRed = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, red);
            float linearGreen = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, green);
            float linearBlue = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, blue);
            float nonlinearL = Av1TransferFunctions.ToGamma(
                parameters.TransferCharacteristics,
                (RedToL * linearRed) + (GreenToL * linearGreen) + (BlueToL * linearBlue));
            float nonlinearM = Av1TransferFunctions.ToGamma(
                parameters.TransferCharacteristics,
                (RedToM * linearRed) + (GreenToM * linearGreen) + (BlueToM * linearBlue));
            float nonlinearS = Av1TransferFunctions.ToGamma(
                parameters.TransferCharacteristics,
                (RedToS * linearRed) + (GreenToS * linearGreen) + (BlueToS * linearBlue));
            intensity = 0.5F * (nonlinearL + nonlinearM);
            bool isHlg = parameters.TransferCharacteristics == ObuTransferCharacteristics.Hlg;
            ct = isHlg
                ? (HlgLToCt * nonlinearL) + (HlgMToCt * nonlinearM) + (HlgSToCt * nonlinearS)
                : (PqLToCt * nonlinearL) + (PqMToCt * nonlinearM) + (PqSToCt * nonlinearS);

            cp = isHlg
                ? (HlgLToCp * nonlinearL) + (HlgMToCp * nonlinearM) + (HlgSToCp * nonlinearS)
                : (PqLToCp * nonlinearL) + (PqMToCp * nonlinearM) + (PqSToCp * nonlinearS);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector128<float> red,
            Vector128<float> green,
            Vector128<float> blue,
            in Av1ColorConversionParameters parameters,
            out Vector128<float> intensity,
            out Vector128<float> ct,
            out Vector128<float> cp)
        {
            Vector128<float> linearRed = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, red);
            Vector128<float> linearGreen = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, green);
            Vector128<float> linearBlue = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, blue);
            Vector128<float> linearL = Vector128.MultiplyAddEstimate(
                Vector128.Create(RedToL),
                linearRed,
                Vector128.MultiplyAddEstimate(Vector128.Create(GreenToL), linearGreen, Vector128.Create(BlueToL) * linearBlue));
            Vector128<float> linearM = Vector128.MultiplyAddEstimate(
                Vector128.Create(RedToM),
                linearRed,
                Vector128.MultiplyAddEstimate(Vector128.Create(GreenToM), linearGreen, Vector128.Create(BlueToM) * linearBlue));
            Vector128<float> linearS = Vector128.MultiplyAddEstimate(
                Vector128.Create(RedToS),
                linearRed,
                Vector128.MultiplyAddEstimate(Vector128.Create(GreenToS), linearGreen, Vector128.Create(BlueToS) * linearBlue));
            Vector128<float> nonlinearL = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearL);
            Vector128<float> nonlinearM = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearM);
            Vector128<float> nonlinearS = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearS);
            intensity = Vector128.Create(0.5F) * (nonlinearL + nonlinearM);
            bool isHlg = parameters.TransferCharacteristics == ObuTransferCharacteristics.Hlg;
            float lToCt = isHlg ? HlgLToCt : PqLToCt;
            float mToCt = isHlg ? HlgMToCt : PqMToCt;
            float sToCt = isHlg ? HlgSToCt : PqSToCt;
            float lToCp = isHlg ? HlgLToCp : PqLToCp;
            float mToCp = isHlg ? HlgMToCp : PqMToCp;
            float sToCp = isHlg ? HlgSToCp : PqSToCp;
            ct = Vector128.MultiplyAddEstimate(
                Vector128.Create(lToCt),
                nonlinearL,
                Vector128.MultiplyAddEstimate(Vector128.Create(mToCt), nonlinearM, Vector128.Create(sToCt) * nonlinearS));
            cp = Vector128.MultiplyAddEstimate(
                Vector128.Create(lToCp),
                nonlinearL,
                Vector128.MultiplyAddEstimate(Vector128.Create(mToCp), nonlinearM, Vector128.Create(sToCp) * nonlinearS));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector256<float> red,
            Vector256<float> green,
            Vector256<float> blue,
            in Av1ColorConversionParameters parameters,
            out Vector256<float> intensity,
            out Vector256<float> ct,
            out Vector256<float> cp)
        {
            Vector256<float> linearRed = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, red);
            Vector256<float> linearGreen = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, green);
            Vector256<float> linearBlue = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, blue);
            Vector256<float> linearL = Vector256.MultiplyAddEstimate(
                Vector256.Create(RedToL),
                linearRed,
                Vector256.MultiplyAddEstimate(Vector256.Create(GreenToL), linearGreen, Vector256.Create(BlueToL) * linearBlue));
            Vector256<float> linearM = Vector256.MultiplyAddEstimate(
                Vector256.Create(RedToM),
                linearRed,
                Vector256.MultiplyAddEstimate(Vector256.Create(GreenToM), linearGreen, Vector256.Create(BlueToM) * linearBlue));
            Vector256<float> linearS = Vector256.MultiplyAddEstimate(
                Vector256.Create(RedToS),
                linearRed,
                Vector256.MultiplyAddEstimate(Vector256.Create(GreenToS), linearGreen, Vector256.Create(BlueToS) * linearBlue));
            Vector256<float> nonlinearL = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearL);
            Vector256<float> nonlinearM = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearM);
            Vector256<float> nonlinearS = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearS);
            intensity = Vector256.Create(0.5F) * (nonlinearL + nonlinearM);
            bool isHlg = parameters.TransferCharacteristics == ObuTransferCharacteristics.Hlg;
            float lToCt = isHlg ? HlgLToCt : PqLToCt;
            float mToCt = isHlg ? HlgMToCt : PqMToCt;
            float sToCt = isHlg ? HlgSToCt : PqSToCt;
            float lToCp = isHlg ? HlgLToCp : PqLToCp;
            float mToCp = isHlg ? HlgMToCp : PqMToCp;
            float sToCp = isHlg ? HlgSToCp : PqSToCp;
            ct = Vector256.MultiplyAddEstimate(
                Vector256.Create(lToCt),
                nonlinearL,
                Vector256.MultiplyAddEstimate(Vector256.Create(mToCt), nonlinearM, Vector256.Create(sToCt) * nonlinearS));
            cp = Vector256.MultiplyAddEstimate(
                Vector256.Create(lToCp),
                nonlinearL,
                Vector256.MultiplyAddEstimate(Vector256.Create(mToCp), nonlinearM, Vector256.Create(sToCp) * nonlinearS));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector512<float> red,
            Vector512<float> green,
            Vector512<float> blue,
            in Av1ColorConversionParameters parameters,
            out Vector512<float> intensity,
            out Vector512<float> ct,
            out Vector512<float> cp)
        {
            Vector512<float> linearRed = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, red);
            Vector512<float> linearGreen = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, green);
            Vector512<float> linearBlue = Av1TransferFunctions.ToLinear(parameters.TransferCharacteristics, blue);
            Vector512<float> linearL = Vector512.MultiplyAddEstimate(
                Vector512.Create(RedToL),
                linearRed,
                Vector512.MultiplyAddEstimate(Vector512.Create(GreenToL), linearGreen, Vector512.Create(BlueToL) * linearBlue));
            Vector512<float> linearM = Vector512.MultiplyAddEstimate(
                Vector512.Create(RedToM),
                linearRed,
                Vector512.MultiplyAddEstimate(Vector512.Create(GreenToM), linearGreen, Vector512.Create(BlueToM) * linearBlue));
            Vector512<float> linearS = Vector512.MultiplyAddEstimate(
                Vector512.Create(RedToS),
                linearRed,
                Vector512.MultiplyAddEstimate(Vector512.Create(GreenToS), linearGreen, Vector512.Create(BlueToS) * linearBlue));
            Vector512<float> nonlinearL = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearL);
            Vector512<float> nonlinearM = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearM);
            Vector512<float> nonlinearS = Av1TransferFunctions.ToGamma(parameters.TransferCharacteristics, linearS);
            intensity = Vector512.Create(0.5F) * (nonlinearL + nonlinearM);
            bool isHlg = parameters.TransferCharacteristics == ObuTransferCharacteristics.Hlg;
            float lToCt = isHlg ? HlgLToCt : PqLToCt;
            float mToCt = isHlg ? HlgMToCt : PqMToCt;
            float sToCt = isHlg ? HlgSToCt : PqSToCt;
            float lToCp = isHlg ? HlgLToCp : PqLToCp;
            float mToCp = isHlg ? HlgMToCp : PqMToCp;
            float sToCp = isHlg ? HlgSToCp : PqSToCp;
            ct = Vector512.MultiplyAddEstimate(
                Vector512.Create(lToCt),
                nonlinearL,
                Vector512.MultiplyAddEstimate(Vector512.Create(mToCt), nonlinearM, Vector512.Create(sToCt) * nonlinearS));
            cp = Vector512.MultiplyAddEstimate(
                Vector512.Create(lToCp),
                nonlinearL,
                Vector512.MultiplyAddEstimate(Vector512.Create(mToCp), nonlinearM, Vector512.Create(sToCp) * nonlinearS));
        }
    }
}
