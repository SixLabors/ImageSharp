// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using static SixLabors.ImageSharp.ColorProfiles.Companding.CompandingVectorOperations;

namespace SixLabors.ImageSharp.ColorProfiles.Companding;

/// <content>
/// Provides scalar and SIMD operators for extended-range companding.
/// </content>
public static partial class LCompanding
{
    // L* values are divided by the reference-white lightness before pixel storage.
    private const float MaximumLightness = 100F;
    private const float LightnessScale = 1.16F;
    private const float LightnessOffset = 0.16F;
    private const float LinearThreshold = 0.08F;

    // Retain the exponent used by the existing L* implementation in both scalar and SIMD methods.
    private const float CubeRootExponent = 0.3333333F;

    /// <summary>
    /// Decodes components with the L transfer curve.
    /// </summary>
    internal readonly struct ExpandOperator : TensorPrimitives_.IUnaryOperator
    {
        /// <inheritdoc/>
        public static bool PreserveAlpha => true;

        /// <inheritdoc/>
        public float Invoke(float value)
            => value <= LinearThreshold ? (MaximumLightness * value) / CieConstants.Kappa : Numerics.Pow3((value + LightnessOffset) / LightnessScale);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value) => Invoke<Vector128<float>, Vector128Operations>(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value) => Invoke<Vector256<float>, Vector256Operations>(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value) => Invoke<Vector512<float>, Vector512Operations>(value);

        /// <summary>
        /// Applies the curve to independent component lanes without clipping the final values.
        /// </summary>
        /// <typeparam name="TVector">The register type.</typeparam>
        /// <typeparam name="TOperations">The register operations.</typeparam>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TVector Invoke<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            // L* stores lightness divided by 100. Its inverse is linear through 0.08, then cubic.
            TVector linear = TOperations.Divide(TOperations.Multiply(value, TOperations.Create(MaximumLightness)), TOperations.Create(CieConstants.Kappa));
            TVector basis = TOperations.Divide(TOperations.Add(value, TOperations.Create(LightnessOffset)), TOperations.Create(LightnessScale));
            TVector nonlinear = TOperations.Multiply(TOperations.Multiply(basis, basis), basis);
            return TOperations.ConditionalSelect(TOperations.LessThanOrEqual(value, TOperations.Create(LinearThreshold)), linear, nonlinear);
        }
    }

    /// <summary>
    /// Encodes components with the L transfer curve.
    /// </summary>
    internal readonly struct CompressOperator : TensorPrimitives_.IUnaryOperator
    {
        /// <inheritdoc/>
        public static bool PreserveAlpha => true;

        /// <inheritdoc/>
        public float Invoke(float value)
            => value <= CieConstants.Epsilon
                ? (value * CieConstants.Kappa) / MaximumLightness
                : (LightnessScale * new TensorPrimitives_.PowOperator(CubeRootExponent).Invoke(value)) - LightnessOffset;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value) => Invoke<Vector128<float>, Vector128Operations>(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value) => Invoke<Vector256<float>, Vector256Operations>(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value) => Invoke<Vector512<float>, Vector512Operations>(value);

        /// <summary>
        /// Applies the curve to independent component lanes without clipping the final values.
        /// </summary>
        /// <typeparam name="TVector">The register type.</typeparam>
        /// <typeparam name="TOperations">The register operations.</typeparam>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TVector Invoke<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            // L* uses the linear segment for negative and near-black XYZ values.
            // Keep the existing cube-root exponent rather than substituting a different approximation.
            TVector linear = TOperations.Divide(TOperations.Multiply(value, TOperations.Create(CieConstants.Kappa)), TOperations.Create(MaximumLightness));
            TVector power = TOperations.Pow(TOperations.Clamp(value, TOperations.Create(0F), TOperations.Create(float.PositiveInfinity)), CubeRootExponent);
            TVector nonlinear = TOperations.Subtract(TOperations.Multiply(TOperations.Create(LightnessScale), power), TOperations.Create(LightnessOffset));
            return TOperations.ConditionalSelect(TOperations.LessThanOrEqual(value, TOperations.Create(CieConstants.Epsilon)), linear, nonlinear);
        }
    }
}
