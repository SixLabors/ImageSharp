// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

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
        public float Invoke(float value)
            => value <= LinearThreshold ? (MaximumLightness * value) / CieConstants.Kappa : Numerics.Pow3((value + LightnessOffset) / LightnessScale);

        /// <summary>
        /// Applies the curve to independent component lanes without clipping the final values.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            // L* stores lightness divided by 100. Its inverse is linear through 0.08, then cubic.
            Vector128<float> linear = Vector128.Divide(Vector128.Multiply(value, Vector128.Create(MaximumLightness)), Vector128.Create(CieConstants.Kappa));
            Vector128<float> basis = Vector128.Divide(Vector128.Add(value, Vector128.Create(LightnessOffset)), Vector128.Create(LightnessScale));
            Vector128<float> nonlinear = Vector128.Multiply(Vector128.Multiply(basis, basis), basis);
            return Vector128.ConditionalSelect(Vector128.LessThanOrEqual(value, Vector128.Create(LinearThreshold)), linear, nonlinear);
        }

        /// <summary>
        /// Applies the curve to independent component lanes without clipping the final values.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            // L* stores lightness divided by 100. Its inverse is linear through 0.08, then cubic.
            Vector256<float> linear = Vector256.Divide(Vector256.Multiply(value, Vector256.Create(MaximumLightness)), Vector256.Create(CieConstants.Kappa));
            Vector256<float> basis = Vector256.Divide(Vector256.Add(value, Vector256.Create(LightnessOffset)), Vector256.Create(LightnessScale));
            Vector256<float> nonlinear = Vector256.Multiply(Vector256.Multiply(basis, basis), basis);
            return Vector256.ConditionalSelect(Vector256.LessThanOrEqual(value, Vector256.Create(LinearThreshold)), linear, nonlinear);
        }

        /// <summary>
        /// Applies the curve to independent component lanes without clipping the final values.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            // L* stores lightness divided by 100. Its inverse is linear through 0.08, then cubic.
            Vector512<float> linear = Vector512.Divide(Vector512.Multiply(value, Vector512.Create(MaximumLightness)), Vector512.Create(CieConstants.Kappa));
            Vector512<float> basis = Vector512.Divide(Vector512.Add(value, Vector512.Create(LightnessOffset)), Vector512.Create(LightnessScale));
            Vector512<float> nonlinear = Vector512.Multiply(Vector512.Multiply(basis, basis), basis);
            return Vector512.ConditionalSelect(Vector512.LessThanOrEqual(value, Vector512.Create(LinearThreshold)), linear, nonlinear);
        }
    }

    /// <summary>
    /// Encodes components with the L transfer curve.
    /// </summary>
    internal readonly struct CompressOperator : TensorPrimitives_.IUnaryOperator
    {
        /// <inheritdoc/>
        public float Invoke(float value)
            => value <= CieConstants.Epsilon
                ? (value * CieConstants.Kappa) / MaximumLightness
                : (LightnessScale * new TensorPrimitives_.PowOperator(CubeRootExponent).Invoke(value)) - LightnessOffset;

        /// <summary>
        /// Applies the curve to independent component lanes without clipping the final values.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            // L* uses the linear segment for negative and near-black XYZ values.
            // Keep the existing cube-root exponent rather than substituting a different approximation.
            Vector128<float> linear = Vector128.Divide(Vector128.Multiply(value, Vector128.Create(CieConstants.Kappa)), Vector128.Create(MaximumLightness));
            Vector128<float> power = Vector128_.Pow(Vector128.Max(value, Vector128.Create(0F)), CubeRootExponent);
            Vector128<float> nonlinear = Vector128.Subtract(Vector128.Multiply(Vector128.Create(LightnessScale), power), Vector128.Create(LightnessOffset));
            return Vector128.ConditionalSelect(Vector128.LessThanOrEqual(value, Vector128.Create(CieConstants.Epsilon)), linear, nonlinear);
        }

        /// <summary>
        /// Applies the curve to independent component lanes without clipping the final values.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            // L* uses the linear segment for negative and near-black XYZ values.
            // Keep the existing cube-root exponent rather than substituting a different approximation.
            Vector256<float> linear = Vector256.Divide(Vector256.Multiply(value, Vector256.Create(CieConstants.Kappa)), Vector256.Create(MaximumLightness));
            Vector256<float> power = Vector256_.Pow(Vector256.Max(value, Vector256.Create(0F)), CubeRootExponent);
            Vector256<float> nonlinear = Vector256.Subtract(Vector256.Multiply(Vector256.Create(LightnessScale), power), Vector256.Create(LightnessOffset));
            return Vector256.ConditionalSelect(Vector256.LessThanOrEqual(value, Vector256.Create(CieConstants.Epsilon)), linear, nonlinear);
        }

        /// <summary>
        /// Applies the curve to independent component lanes without clipping the final values.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            // L* uses the linear segment for negative and near-black XYZ values.
            // Keep the existing cube-root exponent rather than substituting a different approximation.
            Vector512<float> linear = Vector512.Divide(Vector512.Multiply(value, Vector512.Create(CieConstants.Kappa)), Vector512.Create(MaximumLightness));
            Vector512<float> power = Vector512_.Pow(Vector512.Max(value, Vector512.Create(0F)), CubeRootExponent);
            Vector512<float> nonlinear = Vector512.Subtract(Vector512.Multiply(Vector512.Create(LightnessScale), power), Vector512.Create(LightnessOffset));
            return Vector512.ConditionalSelect(Vector512.LessThanOrEqual(value, Vector512.Create(CieConstants.Epsilon)), linear, nonlinear);
        }
    }
}
