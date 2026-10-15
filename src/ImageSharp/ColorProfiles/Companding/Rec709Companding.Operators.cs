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
public static partial class Rec709Companding
{
    /// <summary>
    /// The power exponent of the nonlinear segment.
    /// </summary>
    internal const float Exponent = 0.45F;

    /// <summary>
    /// The slope of the linear compression segment.
    /// </summary>
    internal const float LinearSlope = 4.5F;

    /// <summary>
    /// Decodes components with the Rec709 transfer curve.
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
        /// <param name="threshold">The junction in the nonlinear input domain.</param>
        public ExpandOperator(float scale, float offset, float threshold)
        {
            this.scale = scale;
            this.offset = offset;
            this.threshold = threshold;
        }

        /// <inheritdoc/>
        public static bool PreserveAlpha => true;

        /// <inheritdoc/>
        public float Invoke(float value)
            => value < this.threshold ? value / LinearSlope
                : MathF.Pow((value + this.offset) / this.scale, 1F / Exponent);

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
        /// Applies the curve to independent component lanes without clipping the final values.
        /// </summary>
        /// <typeparam name="TVector">The register type.</typeparam>
        /// <typeparam name="TOperations">The register operations.</typeparam>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TVector Invoke<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            // The linear segment also defines negative inputs. Bound only the unused power base;
            // selecting its result must not clip the linear segment or values above reference white.
            TVector linear = TOperations.Divide(value, TOperations.Create(LinearSlope));
            TVector basis = TOperations.Divide(TOperations.Add(value, TOperations.Create(this.offset)), TOperations.Create(this.scale));
            TVector power = TOperations.Pow(TOperations.Max(basis, TOperations.Create(0F)), 1F / Exponent);
            TVector nonlinear = power;
            return TOperations.ConditionalSelect(TOperations.LessThan(value, TOperations.Create(this.threshold)), linear, nonlinear);
        }
    }

    /// <summary>
    /// Encodes components with the Rec709 transfer curve.
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
        /// <param name="threshold">The junction in the linear input domain.</param>
        public CompressOperator(float scale, float offset, float threshold)
        {
            this.scale = scale;
            this.offset = offset;
            this.threshold = threshold;
        }

        /// <inheritdoc/>
        public static bool PreserveAlpha => true;

        /// <inheritdoc/>
        public float Invoke(float value)
            => value < this.threshold ? LinearSlope * value
                : (this.scale * MathF.Pow(value, Exponent)) - this.offset;

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
        /// Applies the curve to independent component lanes without clipping the final values.
        /// </summary>
        /// <typeparam name="TVector">The register type.</typeparam>
        /// <typeparam name="TOperations">The register operations.</typeparam>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TVector Invoke<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            // The linear segment also defines negative inputs. Bound only the unused power base;
            // selecting its result must not clip the linear segment or values above reference white.
            TVector linear = TOperations.Multiply(value, TOperations.Create(LinearSlope));
            TVector power = TOperations.Pow(TOperations.Max(value, TOperations.Create(0F)), Exponent);
            TVector nonlinear = TOperations.Subtract(TOperations.Multiply(TOperations.Create(this.scale), power), TOperations.Create(this.offset));
            return TOperations.ConditionalSelect(TOperations.LessThan(value, TOperations.Create(this.threshold)), linear, nonlinear);
        }
    }
}
