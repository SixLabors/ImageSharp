// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

internal sealed partial class JxlQuantizer
{
    /// <summary>
    /// Restores quantized coefficient magnitudes before matrix scaling.
    /// </summary>
    /// <remarks>
    /// Inputs are numeric integer-to-float conversions, not reinterpreted integer bits.
    /// Zero remains zero. Values of magnitude one use the channel bias; larger values use q - numerator / q.
    /// This uses the equation documented in libjxl's quantizer-inl.h, with float division instead of its approximate reciprocal.
    /// </remarks>
    public readonly struct QuantizationBiasOperator : TensorPrimitives_.IUnaryOperator
    {
        private readonly float unitBias;
        private readonly float numerator;

        /// <summary>
        /// Initializes a new instance of the <see cref="QuantizationBiasOperator"/> struct.
        /// </summary>
        /// <param name="channelIndex">The XYB channel index.</param>
        /// <param name="biases">The three channel biases followed by the shared reciprocal numerator.</param>
        public QuantizationBiasOperator(int channelIndex, ReadOnlySpan<float> biases)
        {
            this.unitBias = biases[channelIndex];
            this.numerator = biases[3];
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float value)
            => value == 0F ? 0F : MathF.Abs(value) <= 1F ? value * this.unitBias : value - (this.numerator / value);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            // Every lane is one signed coefficient. The low-magnitude mask selects exactly 0 and +/-1.
            // Division can produce infinity in zero lanes; selection replaces those lanes with exact zero.
            // A negative encoded bias must not turn a zero coefficient into negative zero.
            Vector128<float> unit = Vector128.ConditionalSelect(
                Vector128.Equals(value, Vector128<float>.Zero),
                Vector128<float>.Zero,
                value * Vector128.Create(this.unitBias));

            Vector128<float> adjusted = value - (Vector128.Create(this.numerator) / value);
            return Vector128.ConditionalSelect(Vector128.LessThanOrEqual(Vector128.Abs(value), Vector128<float>.One), unit, adjusted);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            // Every lane is one signed coefficient. The low-magnitude mask selects exactly 0 and +/-1.
            // Division can produce infinity in zero lanes; selection replaces those lanes with exact zero.
            // A negative encoded bias must not turn a zero coefficient into negative zero.
            Vector256<float> unit = Vector256.ConditionalSelect(
                Vector256.Equals(value, Vector256<float>.Zero),
                Vector256<float>.Zero,
                value * Vector256.Create(this.unitBias));

            Vector256<float> adjusted = value - (Vector256.Create(this.numerator) / value);
            return Vector256.ConditionalSelect(Vector256.LessThanOrEqual(Vector256.Abs(value), Vector256<float>.One), unit, adjusted);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            // Every lane is one signed coefficient. The low-magnitude mask selects exactly 0 and +/-1.
            // Division can produce infinity in zero lanes; selection replaces those lanes with exact zero.
            // A negative encoded bias must not turn a zero coefficient into negative zero.
            Vector512<float> unit = Vector512.ConditionalSelect(
                Vector512.Equals(value, Vector512<float>.Zero),
                Vector512<float>.Zero,
                value * Vector512.Create(this.unitBias));

            Vector512<float> adjusted = value - (Vector512.Create(this.numerator) / value);
            return Vector512.ConditionalSelect(Vector512.LessThanOrEqual(Vector512.Abs(value), Vector512<float>.One), unit, adjusted);
        }
    }
}
