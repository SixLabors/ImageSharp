// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Components;

/// <content>
/// Provides IPT-C2 transfer-domain matrix conversion for scalar and SIMD lanes.
/// </content>
internal abstract partial class HeifColorConverterBase
{
    /// <summary>
    /// Implements IPT-C2 conversion for scalar and SIMD lanes.
    /// </summary>
    internal readonly struct HeifIptC2ColorOperator : IHeifColorOperator
    {
        /// <summary>
        /// The linear red contribution to L.
        /// </summary>
        public const float RedToL = 1747F / 4096F;

        /// <summary>
        /// The linear green contribution to L.
        /// </summary>
        public const float GreenToL = 2169F / 4096F;

        /// <summary>
        /// The linear blue contribution to L.
        /// </summary>
        public const float BlueToL = 180F / 4096F;

        /// <summary>
        /// The linear red contribution to M.
        /// </summary>
        public const float RedToM = 673F / 4096F;

        /// <summary>
        /// The linear green contribution to M.
        /// </summary>
        public const float GreenToM = 3029F / 4096F;

        /// <summary>
        /// The linear blue contribution to M.
        /// </summary>
        public const float BlueToM = 394F / 4096F;

        /// <summary>
        /// The linear red contribution to S.
        /// </summary>
        public const float RedToS = 50F / 4096F;

        /// <summary>
        /// The linear green contribution to S.
        /// </summary>
        public const float GreenToS = 207F / 4096F;

        /// <summary>
        /// The linear blue contribution to S.
        /// </summary>
        public const float BlueToS = 3839F / 4096F;

        /// <summary>
        /// The nonlinear L contribution to intensity.
        /// </summary>
        public const float LToIntensity = 1638F / 4096F;

        /// <summary>
        /// The nonlinear M contribution to intensity.
        /// </summary>
        public const float MToIntensity = 1638F / 4096F;

        /// <summary>
        /// The nonlinear S contribution to intensity.
        /// </summary>
        public const float SToIntensity = 820F / 4096F;

        /// <summary>
        /// The nonlinear L contribution to the protan axis.
        /// </summary>
        public const float LToProtan = 18248F / 4096F;

        /// <summary>
        /// The nonlinear M contribution to the protan axis.
        /// </summary>
        public const float MToProtan = -19870F / 4096F;

        /// <summary>
        /// The nonlinear S contribution to the protan axis.
        /// </summary>
        public const float SToProtan = 1622F / 4096F;

        /// <summary>
        /// The nonlinear L contribution to the tritan axis.
        /// </summary>
        public const float LToTritan = 3300F / 4096F;

        /// <summary>
        /// The nonlinear M contribution to the tritan axis.
        /// </summary>
        public const float MToTritan = 1463F / 4096F;

        /// <summary>
        /// The nonlinear S contribution to the tritan axis.
        /// </summary>
        public const float SToTritan = -4763F / 4096F;

        /// <summary>
        /// The protan contribution to nonlinear L.
        /// </summary>
        public const float ProtanToL = 0.0975578875686935F;

        /// <summary>
        /// The tritan contribution to nonlinear L.
        /// </summary>
        public const float TritanToL = 0.20538292958984272F;

        /// <summary>
        /// The protan contribution to nonlinear M.
        /// </summary>
        public const float ProtanToM = -0.11388362209560723F;

        /// <summary>
        /// The tritan contribution to nonlinear M.
        /// </summary>
        public const float TritanToM = 0.13337828363655785F;

        /// <summary>
        /// The protan contribution to nonlinear S.
        /// </summary>
        public const float ProtanToS = 0.032611650189127685F;

        /// <summary>
        /// The tritan contribution to nonlinear S.
        /// </summary>
        public const float TritanToS = -0.6766961795912734F;

        /// <summary>
        /// The linear L contribution to red.
        /// </summary>
        public const float LToRed = 3.2374662424353895F;

        /// <summary>
        /// The linear M contribution to red.
        /// </summary>
        public const float MToRed = -2.324205800020636F;

        /// <summary>
        /// The linear S contribution to red.
        /// </summary>
        public const float SToRed = 0.08673955758524626F;

        /// <summary>
        /// The linear L contribution to green.
        /// </summary>
        public const float LToGreen = -0.7188754693535147F;

        /// <summary>
        /// The linear M contribution to green.
        /// </summary>
        public const float MToGreen = 1.877899954242238F;

        /// <summary>
        /// The linear S contribution to green.
        /// </summary>
        public const float SToGreen = -0.1590244848887234F;

        /// <summary>
        /// The linear L contribution to blue.
        /// </summary>
        public const float LToBlue = -0.003403513926958051F;

        /// <summary>
        /// The linear M contribution to blue.
        /// </summary>
        public const float MToBlue = -0.07098593397424108F;

        /// <summary>
        /// The linear S contribution to blue.
        /// </summary>
        public const float SToBlue = 1.074389447901199F;

        /// <inheritdoc/>
        public static bool ChromaUsesLumaRange => false;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(ref float intensity, ref float protan, ref float tritan, in HeifColorConversionParameters parameters)
        {
            // IPT-C2 stores opponent axes around intensity in nonlinear LMS. Undo both matrices around the
            // signaled transfer function so the final RGB values remain in the source signal domain.
            float nonlinearL = intensity + (ProtanToL * protan) + (TritanToL * tritan);
            float nonlinearM = intensity + (ProtanToM * protan) + (TritanToM * tritan);
            float nonlinearS = intensity + (ProtanToS * protan) + (TritanToS * tritan);
            float linearL = HeifTransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearL);
            float linearM = HeifTransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearM);
            float linearS = HeifTransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearS);
            float linearRed = (LToRed * linearL) + (MToRed * linearM) + (SToRed * linearS);
            float linearGreen = (LToGreen * linearL) + (MToGreen * linearM) + (SToGreen * linearS);
            float linearBlue = (LToBlue * linearL) + (MToBlue * linearM) + (SToBlue * linearS);

            intensity = HeifTransferFunctions.ToGamma(parameters.TransferCharacteristics, linearRed);
            protan = HeifTransferFunctions.ToGamma(parameters.TransferCharacteristics, linearGreen);
            tritan = HeifTransferFunctions.ToGamma(parameters.TransferCharacteristics, linearBlue);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(
            ref Vector128<float> intensity,
            ref Vector128<float> protan,
            ref Vector128<float> tritan,
            in HeifColorConversionParameters parameters)
        {
            Vector128<float> nonlinearL = Vector128.MultiplyAddEstimate(
                Vector128.Create(TritanToL),
                tritan,
                Vector128.MultiplyAddEstimate(Vector128.Create(ProtanToL), protan, intensity));

            Vector128<float> nonlinearM = Vector128.MultiplyAddEstimate(
                Vector128.Create(TritanToM),
                tritan,
                Vector128.MultiplyAddEstimate(Vector128.Create(ProtanToM), protan, intensity));

            Vector128<float> nonlinearS = Vector128.MultiplyAddEstimate(
                Vector128.Create(TritanToS),
                tritan,
                Vector128.MultiplyAddEstimate(Vector128.Create(ProtanToS), protan, intensity));

            Vector128<float> linearL = HeifTransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearL);
            Vector128<float> linearM = HeifTransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearM);
            Vector128<float> linearS = HeifTransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearS);
            Vector128<float> linearRed = Vector128.MultiplyAddEstimate(
                Vector128.Create(SToRed), linearS, Vector128.MultiplyAddEstimate(Vector128.Create(MToRed), linearM, Vector128.Create(LToRed) * linearL));

            Vector128<float> linearGreen = Vector128.MultiplyAddEstimate(
                Vector128.Create(SToGreen), linearS, Vector128.MultiplyAddEstimate(Vector128.Create(MToGreen), linearM, Vector128.Create(LToGreen) * linearL));

            Vector128<float> linearBlue = Vector128.MultiplyAddEstimate(
                Vector128.Create(SToBlue), linearS, Vector128.MultiplyAddEstimate(Vector128.Create(MToBlue), linearM, Vector128.Create(LToBlue) * linearL));

            intensity = HeifTransferFunctions.ToGamma(parameters.TransferCharacteristics, linearRed);
            protan = HeifTransferFunctions.ToGamma(parameters.TransferCharacteristics, linearGreen);
            tritan = HeifTransferFunctions.ToGamma(parameters.TransferCharacteristics, linearBlue);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(
            ref Vector256<float> intensity,
            ref Vector256<float> protan,
            ref Vector256<float> tritan,
            in HeifColorConversionParameters parameters)
        {
            Vector256<float> nonlinearL = Vector256.MultiplyAddEstimate(
                Vector256.Create(TritanToL),
                tritan,
                Vector256.MultiplyAddEstimate(Vector256.Create(ProtanToL), protan, intensity));

            Vector256<float> nonlinearM = Vector256.MultiplyAddEstimate(
                Vector256.Create(TritanToM),
                tritan,
                Vector256.MultiplyAddEstimate(Vector256.Create(ProtanToM), protan, intensity));

            Vector256<float> nonlinearS = Vector256.MultiplyAddEstimate(
                Vector256.Create(TritanToS),
                tritan,
                Vector256.MultiplyAddEstimate(Vector256.Create(ProtanToS), protan, intensity));

            Vector256<float> linearL = HeifTransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearL);
            Vector256<float> linearM = HeifTransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearM);
            Vector256<float> linearS = HeifTransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearS);
            Vector256<float> linearRed = Vector256.MultiplyAddEstimate(
                Vector256.Create(SToRed), linearS, Vector256.MultiplyAddEstimate(Vector256.Create(MToRed), linearM, Vector256.Create(LToRed) * linearL));

            Vector256<float> linearGreen = Vector256.MultiplyAddEstimate(
                Vector256.Create(SToGreen), linearS, Vector256.MultiplyAddEstimate(Vector256.Create(MToGreen), linearM, Vector256.Create(LToGreen) * linearL));

            Vector256<float> linearBlue = Vector256.MultiplyAddEstimate(
                Vector256.Create(SToBlue), linearS, Vector256.MultiplyAddEstimate(Vector256.Create(MToBlue), linearM, Vector256.Create(LToBlue) * linearL));

            intensity = HeifTransferFunctions.ToGamma(parameters.TransferCharacteristics, linearRed);
            protan = HeifTransferFunctions.ToGamma(parameters.TransferCharacteristics, linearGreen);
            tritan = HeifTransferFunctions.ToGamma(parameters.TransferCharacteristics, linearBlue);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToRgb(
            ref Vector512<float> intensity,
            ref Vector512<float> protan,
            ref Vector512<float> tritan,
            in HeifColorConversionParameters parameters)
        {
            Vector512<float> nonlinearL = Vector512.MultiplyAddEstimate(
                Vector512.Create(TritanToL),
                tritan,
                Vector512.MultiplyAddEstimate(Vector512.Create(ProtanToL), protan, intensity));

            Vector512<float> nonlinearM = Vector512.MultiplyAddEstimate(
                Vector512.Create(TritanToM),
                tritan,
                Vector512.MultiplyAddEstimate(Vector512.Create(ProtanToM), protan, intensity));

            Vector512<float> nonlinearS = Vector512.MultiplyAddEstimate(
                Vector512.Create(TritanToS),
                tritan,
                Vector512.MultiplyAddEstimate(Vector512.Create(ProtanToS), protan, intensity));

            Vector512<float> linearL = HeifTransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearL);
            Vector512<float> linearM = HeifTransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearM);
            Vector512<float> linearS = HeifTransferFunctions.ToLinear(parameters.TransferCharacteristics, nonlinearS);
            Vector512<float> linearRed = Vector512.MultiplyAddEstimate(
                Vector512.Create(SToRed), linearS, Vector512.MultiplyAddEstimate(Vector512.Create(MToRed), linearM, Vector512.Create(LToRed) * linearL));

            Vector512<float> linearGreen = Vector512.MultiplyAddEstimate(
                Vector512.Create(SToGreen), linearS, Vector512.MultiplyAddEstimate(Vector512.Create(MToGreen), linearM, Vector512.Create(LToGreen) * linearL));

            Vector512<float> linearBlue = Vector512.MultiplyAddEstimate(
                Vector512.Create(SToBlue), linearS, Vector512.MultiplyAddEstimate(Vector512.Create(MToBlue), linearM, Vector512.Create(LToBlue) * linearL));

            intensity = HeifTransferFunctions.ToGamma(parameters.TransferCharacteristics, linearRed);
            protan = HeifTransferFunctions.ToGamma(parameters.TransferCharacteristics, linearGreen);
            tritan = HeifTransferFunctions.ToGamma(parameters.TransferCharacteristics, linearBlue);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            float red,
            float green,
            float blue,
            in HeifColorConversionParameters parameters,
            out float intensity,
            out float protan,
            out float tritan)
        {
            // The encoded RGB signal is linearized before the LMS matrix, then the signaled transfer function
            // is reapplied to each LMS component before the fixed IPT-C2 opponent matrix.
            float linearRed = HeifTransferFunctions.ToLinear(parameters.TransferCharacteristics, red);
            float linearGreen = HeifTransferFunctions.ToLinear(parameters.TransferCharacteristics, green);
            float linearBlue = HeifTransferFunctions.ToLinear(parameters.TransferCharacteristics, blue);
            float nonlinearL = HeifTransferFunctions.ToGamma(
                parameters.TransferCharacteristics,
                (RedToL * linearRed) + (GreenToL * linearGreen) + (BlueToL * linearBlue));

            float nonlinearM = HeifTransferFunctions.ToGamma(
                parameters.TransferCharacteristics,
                (RedToM * linearRed) + (GreenToM * linearGreen) + (BlueToM * linearBlue));

            float nonlinearS = HeifTransferFunctions.ToGamma(
                parameters.TransferCharacteristics,
                (RedToS * linearRed) + (GreenToS * linearGreen) + (BlueToS * linearBlue));

            intensity = (LToIntensity * nonlinearL) + (MToIntensity * nonlinearM) + (SToIntensity * nonlinearS);
            protan = (LToProtan * nonlinearL) + (MToProtan * nonlinearM) + (SToProtan * nonlinearS);
            tritan = (LToTritan * nonlinearL) + (MToTritan * nonlinearM) + (SToTritan * nonlinearS);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector128<float> red,
            Vector128<float> green,
            Vector128<float> blue,
            in HeifColorConversionParameters parameters,
            out Vector128<float> intensity,
            out Vector128<float> protan,
            out Vector128<float> tritan)
        {
            Vector128<float> linearRed = HeifTransferFunctions.ToLinear(parameters.TransferCharacteristics, red);
            Vector128<float> linearGreen = HeifTransferFunctions.ToLinear(parameters.TransferCharacteristics, green);
            Vector128<float> linearBlue = HeifTransferFunctions.ToLinear(parameters.TransferCharacteristics, blue);
            Vector128<float> linearL = Vector128.MultiplyAddEstimate(
                Vector128.Create(BlueToL),
                linearBlue,
                Vector128.MultiplyAddEstimate(Vector128.Create(GreenToL), linearGreen, Vector128.Create(RedToL) * linearRed));

            Vector128<float> linearM = Vector128.MultiplyAddEstimate(
                Vector128.Create(BlueToM),
                linearBlue,
                Vector128.MultiplyAddEstimate(Vector128.Create(GreenToM), linearGreen, Vector128.Create(RedToM) * linearRed));

            Vector128<float> linearS = Vector128.MultiplyAddEstimate(
                Vector128.Create(BlueToS),
                linearBlue,
                Vector128.MultiplyAddEstimate(Vector128.Create(GreenToS), linearGreen, Vector128.Create(RedToS) * linearRed));

            Vector128<float> nonlinearL = HeifTransferFunctions.ToGamma(parameters.TransferCharacteristics, linearL);
            Vector128<float> nonlinearM = HeifTransferFunctions.ToGamma(parameters.TransferCharacteristics, linearM);
            Vector128<float> nonlinearS = HeifTransferFunctions.ToGamma(parameters.TransferCharacteristics, linearS);
            intensity = Vector128.MultiplyAddEstimate(
                Vector128.Create(SToIntensity),
                nonlinearS,
                Vector128.MultiplyAddEstimate(Vector128.Create(MToIntensity), nonlinearM, Vector128.Create(LToIntensity) * nonlinearL));

            protan = Vector128.MultiplyAddEstimate(
                Vector128.Create(SToProtan),
                nonlinearS,
                Vector128.MultiplyAddEstimate(Vector128.Create(MToProtan), nonlinearM, Vector128.Create(LToProtan) * nonlinearL));

            tritan = Vector128.MultiplyAddEstimate(
                Vector128.Create(SToTritan),
                nonlinearS,
                Vector128.MultiplyAddEstimate(Vector128.Create(MToTritan), nonlinearM, Vector128.Create(LToTritan) * nonlinearL));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector256<float> red,
            Vector256<float> green,
            Vector256<float> blue,
            in HeifColorConversionParameters parameters,
            out Vector256<float> intensity,
            out Vector256<float> protan,
            out Vector256<float> tritan)
        {
            Vector256<float> linearRed = HeifTransferFunctions.ToLinear(parameters.TransferCharacteristics, red);
            Vector256<float> linearGreen = HeifTransferFunctions.ToLinear(parameters.TransferCharacteristics, green);
            Vector256<float> linearBlue = HeifTransferFunctions.ToLinear(parameters.TransferCharacteristics, blue);
            Vector256<float> linearL = Vector256.MultiplyAddEstimate(
                Vector256.Create(BlueToL),
                linearBlue,
                Vector256.MultiplyAddEstimate(Vector256.Create(GreenToL), linearGreen, Vector256.Create(RedToL) * linearRed));

            Vector256<float> linearM = Vector256.MultiplyAddEstimate(
                Vector256.Create(BlueToM),
                linearBlue,
                Vector256.MultiplyAddEstimate(Vector256.Create(GreenToM), linearGreen, Vector256.Create(RedToM) * linearRed));

            Vector256<float> linearS = Vector256.MultiplyAddEstimate(
                Vector256.Create(BlueToS),
                linearBlue,
                Vector256.MultiplyAddEstimate(Vector256.Create(GreenToS), linearGreen, Vector256.Create(RedToS) * linearRed));

            Vector256<float> nonlinearL = HeifTransferFunctions.ToGamma(parameters.TransferCharacteristics, linearL);
            Vector256<float> nonlinearM = HeifTransferFunctions.ToGamma(parameters.TransferCharacteristics, linearM);
            Vector256<float> nonlinearS = HeifTransferFunctions.ToGamma(parameters.TransferCharacteristics, linearS);
            intensity = Vector256.MultiplyAddEstimate(
                Vector256.Create(SToIntensity),
                nonlinearS,
                Vector256.MultiplyAddEstimate(Vector256.Create(MToIntensity), nonlinearM, Vector256.Create(LToIntensity) * nonlinearL));

            protan = Vector256.MultiplyAddEstimate(
                Vector256.Create(SToProtan),
                nonlinearS,
                Vector256.MultiplyAddEstimate(Vector256.Create(MToProtan), nonlinearM, Vector256.Create(LToProtan) * nonlinearL));

            tritan = Vector256.MultiplyAddEstimate(
                Vector256.Create(SToTritan),
                nonlinearS,
                Vector256.MultiplyAddEstimate(Vector256.Create(MToTritan), nonlinearM, Vector256.Create(LToTritan) * nonlinearL));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector512<float> red,
            Vector512<float> green,
            Vector512<float> blue,
            in HeifColorConversionParameters parameters,
            out Vector512<float> intensity,
            out Vector512<float> protan,
            out Vector512<float> tritan)
        {
            Vector512<float> linearRed = HeifTransferFunctions.ToLinear(parameters.TransferCharacteristics, red);
            Vector512<float> linearGreen = HeifTransferFunctions.ToLinear(parameters.TransferCharacteristics, green);
            Vector512<float> linearBlue = HeifTransferFunctions.ToLinear(parameters.TransferCharacteristics, blue);
            Vector512<float> linearL = Vector512.MultiplyAddEstimate(
                Vector512.Create(BlueToL),
                linearBlue,
                Vector512.MultiplyAddEstimate(Vector512.Create(GreenToL), linearGreen, Vector512.Create(RedToL) * linearRed));

            Vector512<float> linearM = Vector512.MultiplyAddEstimate(
                Vector512.Create(BlueToM),
                linearBlue,
                Vector512.MultiplyAddEstimate(Vector512.Create(GreenToM), linearGreen, Vector512.Create(RedToM) * linearRed));

            Vector512<float> linearS = Vector512.MultiplyAddEstimate(
                Vector512.Create(BlueToS),
                linearBlue,
                Vector512.MultiplyAddEstimate(Vector512.Create(GreenToS), linearGreen, Vector512.Create(RedToS) * linearRed));

            Vector512<float> nonlinearL = HeifTransferFunctions.ToGamma(parameters.TransferCharacteristics, linearL);
            Vector512<float> nonlinearM = HeifTransferFunctions.ToGamma(parameters.TransferCharacteristics, linearM);
            Vector512<float> nonlinearS = HeifTransferFunctions.ToGamma(parameters.TransferCharacteristics, linearS);
            intensity = Vector512.MultiplyAddEstimate(
                Vector512.Create(SToIntensity),
                nonlinearS,
                Vector512.MultiplyAddEstimate(Vector512.Create(MToIntensity), nonlinearM, Vector512.Create(LToIntensity) * nonlinearL));

            protan = Vector512.MultiplyAddEstimate(
                Vector512.Create(SToProtan),
                nonlinearS,
                Vector512.MultiplyAddEstimate(Vector512.Create(MToProtan), nonlinearM, Vector512.Create(LToProtan) * nonlinearL));

            tritan = Vector512.MultiplyAddEstimate(
                Vector512.Create(SToTritan),
                nonlinearS,
                Vector512.MultiplyAddEstimate(Vector512.Create(MToTritan), nonlinearM, Vector512.Create(LToTritan) * nonlinearL));
        }
    }
}
