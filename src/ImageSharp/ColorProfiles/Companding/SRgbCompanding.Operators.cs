// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.ColorProfiles.Companding;

/// <content>
/// Provides scalar and SIMD operators for extended-range companding.
/// </content>
public static partial class SRgbCompanding
{
    /// <summary>
    /// The power exponent of the nonlinear segment.
    /// </summary>
    internal const float Exponent = 2.4F;

    /// <summary>
    /// The slope of the linear compression segment.
    /// </summary>
    internal const float LinearSlope = 12.92F;

    /// <summary>
    /// Decodes signed sRGB components with coefficients selected by the caller.
    /// </summary>
    internal readonly struct ExpandOperator : TensorPrimitives_.IUnaryOperator
    {
        private readonly float scale;
        private readonly float offset;
        private readonly float threshold;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpandOperator"/> struct.
        /// </summary>
        /// <param name="scale">The nonlinear scale coefficient.</param>
        /// <param name="offset">The nonlinear offset coefficient.</param>
        /// <param name="threshold">The input magnitude at the linear-to-power junction.</param>
        public ExpandOperator(float scale, float offset, float threshold)
        {
            // IEC sRGB uses its published rounded coefficients. H.273 uses a smooth junction.
            // Keep the offset explicit: subtracting one from a rounded float scale changes its value.
            this.scale = scale;
            this.offset = offset;
            this.threshold = threshold;
        }

        /// <inheritdoc/>
        public float Invoke(float value)
        {
            float magnitude = MathF.Abs(value);
            float result = magnitude <= this.threshold
                ? magnitude / LinearSlope
                : new TensorPrimitives_.PowOperator(Exponent).Invoke((magnitude + this.offset) / this.scale);
            return MathF.CopySign(result, value);
        }

        /// <summary>
        /// Evaluates one magnitude curve and restores the sign of each input component.
        /// </summary>
        /// <param name="value">Independent signed RGB components.</param>
        /// <returns>The converted components without clipping.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            // The extended curve is symmetric about zero. One power evaluation serves both signs.
            // Each lane is independent, so a negative neighbor cannot change another lane's result.
            Vector128<float> magnitude = Vector128.Abs(value);
            Vector128<float> linear = Vector128.Divide(magnitude, Vector128.Create(LinearSlope));
            Vector128<float> basis = Vector128.Divide(Vector128.Add(magnitude, Vector128.Create(this.offset)), Vector128.Create(this.scale));
            Vector128<float> nonlinear = Vector128_.Pow(basis, Exponent);
            Vector128<float> result = Vector128.ConditionalSelect(Vector128.LessThanOrEqual(magnitude, Vector128.Create(this.threshold)), linear, nonlinear);
            return Vector128.CopySign(result, value);
        }

        /// <summary>
        /// Evaluates one magnitude curve and restores the sign of each input component.
        /// </summary>
        /// <param name="value">Independent signed RGB components.</param>
        /// <returns>The converted components without clipping.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            // The extended curve is symmetric about zero. One power evaluation serves both signs.
            // Each lane is independent, so a negative neighbor cannot change another lane's result.
            Vector256<float> magnitude = Vector256.Abs(value);
            Vector256<float> linear = Vector256.Divide(magnitude, Vector256.Create(LinearSlope));
            Vector256<float> basis = Vector256.Divide(Vector256.Add(magnitude, Vector256.Create(this.offset)), Vector256.Create(this.scale));
            Vector256<float> nonlinear = Vector256_.Pow(basis, Exponent);
            Vector256<float> result = Vector256.ConditionalSelect(Vector256.LessThanOrEqual(magnitude, Vector256.Create(this.threshold)), linear, nonlinear);
            return Vector256.CopySign(result, value);
        }

        /// <summary>
        /// Evaluates one magnitude curve and restores the sign of each input component.
        /// </summary>
        /// <param name="value">Independent signed RGB components.</param>
        /// <returns>The converted components without clipping.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            // The extended curve is symmetric about zero. One power evaluation serves both signs.
            // Each lane is independent, so a negative neighbor cannot change another lane's result.
            Vector512<float> magnitude = Vector512.Abs(value);
            Vector512<float> linear = Vector512.Divide(magnitude, Vector512.Create(LinearSlope));
            Vector512<float> basis = Vector512.Divide(Vector512.Add(magnitude, Vector512.Create(this.offset)), Vector512.Create(this.scale));
            Vector512<float> nonlinear = Vector512_.Pow(basis, Exponent);
            Vector512<float> result = Vector512.ConditionalSelect(Vector512.LessThanOrEqual(magnitude, Vector512.Create(this.threshold)), linear, nonlinear);
            return Vector512.CopySign(result, value);
        }
    }

    /// <summary>
    /// Encodes signed sRGB components with coefficients selected by the caller.
    /// </summary>
    internal readonly struct CompressOperator : TensorPrimitives_.IUnaryOperator
    {
        private readonly float scale;
        private readonly float offset;
        private readonly float threshold;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompressOperator"/> struct.
        /// </summary>
        /// <param name="scale">The nonlinear scale coefficient.</param>
        /// <param name="offset">The nonlinear offset coefficient.</param>
        /// <param name="threshold">The input magnitude at the linear-to-power junction.</param>
        public CompressOperator(float scale, float offset, float threshold)
        {
            // IEC sRGB uses its published rounded coefficients. H.273 uses a smooth junction.
            // Keep the offset explicit: subtracting one from a rounded float scale changes its value.
            this.scale = scale;
            this.offset = offset;
            this.threshold = threshold;
        }

        /// <inheritdoc/>
        public float Invoke(float value)
        {
            float magnitude = MathF.Abs(value);
            float result = magnitude <= this.threshold
                ? magnitude * LinearSlope
                : (this.scale * new TensorPrimitives_.PowOperator(1F / Exponent).Invoke(magnitude)) - this.offset;
            return MathF.CopySign(result, value);
        }

        /// <summary>
        /// Evaluates one magnitude curve and restores the sign of each input component.
        /// </summary>
        /// <param name="value">Independent signed RGB components.</param>
        /// <returns>The converted components without clipping.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            // The extended curve is symmetric about zero. One power evaluation serves both signs.
            // Each lane is independent, so a negative neighbor cannot change another lane's result.
            Vector128<float> magnitude = Vector128.Abs(value);
            Vector128<float> linear = Vector128.Multiply(magnitude, Vector128.Create(LinearSlope));
            Vector128<float> power = Vector128_.Pow(magnitude, 1F / Exponent);
            Vector128<float> nonlinear = Vector128.Subtract(Vector128.Multiply(Vector128.Create(this.scale), power), Vector128.Create(this.offset));
            Vector128<float> result = Vector128.ConditionalSelect(Vector128.LessThanOrEqual(magnitude, Vector128.Create(this.threshold)), linear, nonlinear);
            return Vector128.CopySign(result, value);
        }

        /// <summary>
        /// Evaluates one magnitude curve and restores the sign of each input component.
        /// </summary>
        /// <param name="value">Independent signed RGB components.</param>
        /// <returns>The converted components without clipping.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            // The extended curve is symmetric about zero. One power evaluation serves both signs.
            // Each lane is independent, so a negative neighbor cannot change another lane's result.
            Vector256<float> magnitude = Vector256.Abs(value);
            Vector256<float> linear = Vector256.Multiply(magnitude, Vector256.Create(LinearSlope));
            Vector256<float> power = Vector256_.Pow(magnitude, 1F / Exponent);
            Vector256<float> nonlinear = Vector256.Subtract(Vector256.Multiply(Vector256.Create(this.scale), power), Vector256.Create(this.offset));
            Vector256<float> result = Vector256.ConditionalSelect(Vector256.LessThanOrEqual(magnitude, Vector256.Create(this.threshold)), linear, nonlinear);
            return Vector256.CopySign(result, value);
        }

        /// <summary>
        /// Evaluates one magnitude curve and restores the sign of each input component.
        /// </summary>
        /// <param name="value">Independent signed RGB components.</param>
        /// <returns>The converted components without clipping.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            // The extended curve is symmetric about zero. One power evaluation serves both signs.
            // Each lane is independent, so a negative neighbor cannot change another lane's result.
            Vector512<float> magnitude = Vector512.Abs(value);
            Vector512<float> linear = Vector512.Multiply(magnitude, Vector512.Create(LinearSlope));
            Vector512<float> power = Vector512_.Pow(magnitude, 1F / Exponent);
            Vector512<float> nonlinear = Vector512.Subtract(Vector512.Multiply(Vector512.Create(this.scale), power), Vector512.Create(this.offset));
            Vector512<float> result = Vector512.ConditionalSelect(Vector512.LessThanOrEqual(magnitude, Vector512.Create(this.threshold)), linear, nonlinear);
            return Vector512.CopySign(result, value);
        }
    }
}
