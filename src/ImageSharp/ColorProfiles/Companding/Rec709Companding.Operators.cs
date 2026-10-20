// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

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
        public float Invoke(float value)
            => value < this.threshold ? value / LinearSlope
                : new TensorPrimitives_.PowOperator(1F / Exponent).Invoke((value + this.offset) / this.scale);

        /// <summary>
        /// Applies the curve to independent component lanes without clipping the final values.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            // The linear segment also defines negative inputs. Bound only the unused power base;
            // selecting its result must not clip the linear segment or values above reference white.
            Vector128<float> linear = Vector128.Divide(value, Vector128.Create(LinearSlope));
            Vector128<float> basis = Vector128.Divide(Vector128.Add(value, Vector128.Create(this.offset)), Vector128.Create(this.scale));
            Vector128<float> power = Vector128_.Pow(Vector128.Max(basis, Vector128.Create(0F)), 1F / Exponent);
            Vector128<float> nonlinear = power;
            return Vector128.ConditionalSelect(Vector128.LessThan(value, Vector128.Create(this.threshold)), linear, nonlinear);
        }

        /// <summary>
        /// Applies the curve to independent component lanes without clipping the final values.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            // The linear segment also defines negative inputs. Bound only the unused power base;
            // selecting its result must not clip the linear segment or values above reference white.
            Vector256<float> linear = Vector256.Divide(value, Vector256.Create(LinearSlope));
            Vector256<float> basis = Vector256.Divide(Vector256.Add(value, Vector256.Create(this.offset)), Vector256.Create(this.scale));
            Vector256<float> power = Vector256_.Pow(Vector256.Max(basis, Vector256.Create(0F)), 1F / Exponent);
            Vector256<float> nonlinear = power;
            return Vector256.ConditionalSelect(Vector256.LessThan(value, Vector256.Create(this.threshold)), linear, nonlinear);
        }

        /// <summary>
        /// Applies the curve to independent component lanes without clipping the final values.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            // The linear segment also defines negative inputs. Bound only the unused power base;
            // selecting its result must not clip the linear segment or values above reference white.
            Vector512<float> linear = Vector512.Divide(value, Vector512.Create(LinearSlope));
            Vector512<float> basis = Vector512.Divide(Vector512.Add(value, Vector512.Create(this.offset)), Vector512.Create(this.scale));
            Vector512<float> power = Vector512_.Pow(Vector512.Max(basis, Vector512.Create(0F)), 1F / Exponent);
            Vector512<float> nonlinear = power;
            return Vector512.ConditionalSelect(Vector512.LessThan(value, Vector512.Create(this.threshold)), linear, nonlinear);
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
        public float Invoke(float value)
            => value < this.threshold ? LinearSlope * value
                : (this.scale * new TensorPrimitives_.PowOperator(Exponent).Invoke(value)) - this.offset;

        /// <summary>
        /// Applies the curve to independent component lanes without clipping the final values.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            // The linear segment also defines negative inputs. Bound only the unused power base;
            // selecting its result must not clip the linear segment or values above reference white.
            Vector128<float> linear = Vector128.Multiply(value, Vector128.Create(LinearSlope));
            Vector128<float> power = Vector128_.Pow(Vector128.Max(value, Vector128.Create(0F)), Exponent);
            Vector128<float> nonlinear = Vector128.Subtract(Vector128.Multiply(Vector128.Create(this.scale), power), Vector128.Create(this.offset));
            return Vector128.ConditionalSelect(Vector128.LessThan(value, Vector128.Create(this.threshold)), linear, nonlinear);
        }

        /// <summary>
        /// Applies the curve to independent component lanes without clipping the final values.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            // The linear segment also defines negative inputs. Bound only the unused power base;
            // selecting its result must not clip the linear segment or values above reference white.
            Vector256<float> linear = Vector256.Multiply(value, Vector256.Create(LinearSlope));
            Vector256<float> power = Vector256_.Pow(Vector256.Max(value, Vector256.Create(0F)), Exponent);
            Vector256<float> nonlinear = Vector256.Subtract(Vector256.Multiply(Vector256.Create(this.scale), power), Vector256.Create(this.offset));
            return Vector256.ConditionalSelect(Vector256.LessThan(value, Vector256.Create(this.threshold)), linear, nonlinear);
        }

        /// <summary>
        /// Applies the curve to independent component lanes without clipping the final values.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            // The linear segment also defines negative inputs. Bound only the unused power base;
            // selecting its result must not clip the linear segment or values above reference white.
            Vector512<float> linear = Vector512.Multiply(value, Vector512.Create(LinearSlope));
            Vector512<float> power = Vector512_.Pow(Vector512.Max(value, Vector512.Create(0F)), Exponent);
            Vector512<float> nonlinear = Vector512.Subtract(Vector512.Multiply(Vector512.Create(this.scale), power), Vector512.Create(this.offset));
            return Vector512.ConditionalSelect(Vector512.LessThan(value, Vector512.Create(this.threshold)), linear, nonlinear);
        }
    }
}
