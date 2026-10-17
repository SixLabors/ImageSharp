// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using static SixLabors.ImageSharp.ColorProfiles.Companding.CompandingVectorOperations;

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
        public static bool PreserveAlpha => true;

        /// <inheritdoc/>
        public float Invoke(float value)
        {
            float magnitude = MathF.Abs(value);
            float result = magnitude <= this.threshold
                ? magnitude / LinearSlope
                : new TensorPrimitives_.PowOperator(Exponent).Invoke((magnitude + this.offset) / this.scale);
            return MathF.CopySign(result, value);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value) => this.Invoke<Vector128<float>, Vector128Operations>(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value) => this.Invoke<Vector256<float>, Vector256Operations>(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value) => this.Invoke<Vector512<float>, Vector512Operations>(value);

        /// <summary>
        /// Evaluates one magnitude curve and restores the sign of each input component.
        /// </summary>
        /// <typeparam name="TVector">The register type.</typeparam>
        /// <typeparam name="TOperations">The register operations selected by the caller.</typeparam>
        /// <param name="value">Independent signed RGB components.</param>
        /// <returns>The converted components without clipping.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TVector Invoke<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            // The extended curve is symmetric about zero. One power evaluation serves both signs.
            // Each lane is independent, so a negative neighbor cannot change another lane's result.
            TVector magnitude = TOperations.Abs(value);
            TVector linear = TOperations.Divide(magnitude, TOperations.Create(LinearSlope));
            TVector basis = TOperations.Divide(TOperations.Add(magnitude, TOperations.Create(this.offset)), TOperations.Create(this.scale));
            TVector nonlinear = TOperations.Pow(basis, Exponent);
            TVector result = TOperations.ConditionalSelect(TOperations.LessThanOrEqual(magnitude, TOperations.Create(this.threshold)), linear, nonlinear);
            return TOperations.CopySign(result, value);
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
        public static bool PreserveAlpha => true;

        /// <inheritdoc/>
        public float Invoke(float value)
        {
            float magnitude = MathF.Abs(value);
            float result = magnitude <= this.threshold
                ? magnitude * LinearSlope
                : (this.scale * new TensorPrimitives_.PowOperator(1F / Exponent).Invoke(magnitude)) - this.offset;
            return MathF.CopySign(result, value);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value) => this.Invoke<Vector128<float>, Vector128Operations>(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value) => this.Invoke<Vector256<float>, Vector256Operations>(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value) => this.Invoke<Vector512<float>, Vector512Operations>(value);

        /// <summary>
        /// Evaluates one magnitude curve and restores the sign of each input component.
        /// </summary>
        /// <typeparam name="TVector">The register type.</typeparam>
        /// <typeparam name="TOperations">The register operations selected by the caller.</typeparam>
        /// <param name="value">Independent signed RGB components.</param>
        /// <returns>The converted components without clipping.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TVector Invoke<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            // The extended curve is symmetric about zero. One power evaluation serves both signs.
            // Each lane is independent, so a negative neighbor cannot change another lane's result.
            TVector magnitude = TOperations.Abs(value);
            TVector linear = TOperations.Multiply(magnitude, TOperations.Create(LinearSlope));
            TVector power = TOperations.Pow(magnitude, 1F / Exponent);
            TVector nonlinear = TOperations.Subtract(TOperations.Multiply(TOperations.Create(this.scale), power), TOperations.Create(this.offset));
            TVector result = TOperations.ConditionalSelect(TOperations.LessThanOrEqual(magnitude, TOperations.Create(this.threshold)), linear, nonlinear);
            return TOperations.CopySign(result, value);
        }
    }
}
