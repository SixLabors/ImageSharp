// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.ColorProfiles.Icc;

internal static partial class IccColorConverter
{
    /// <summary>
    /// Evaluates a normalized ICC sRGB-form curve with a positive finite exponent selected during setup.
    /// </summary>
    internal readonly struct SRgbCurveOperator : TensorPrimitives_.IUnaryOperator
    {
        private readonly float a;
        private readonly float b;
        private readonly float c;
        private readonly float d;
        private readonly float e;
        private readonly float signMask;
        private readonly TensorPrimitives_.PowOperator power;

        /// <summary>
        /// Initializes a new instance of the <see cref="SRgbCurveOperator"/> struct.
        /// </summary>
        /// <param name="a">The scale inside the power branch.</param>
        /// <param name="b">The offset inside the power branch.</param>
        /// <param name="c">The linear branch scale.</param>
        /// <param name="d">The input at which the power branch starts.</param>
        /// <param name="e">The offset after the power branch.</param>
        /// <param name="power">The power operator for the upper branch.</param>
        public SRgbCurveOperator(float a, float b, float c, float d, float e, TensorPrimitives_.PowOperator power)
            : this(a, b, c, d, e, power, 0F)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SRgbCurveOperator"/> struct with a sign mask for the input.
        /// </summary>
        /// <param name="a">The scale inside the power branch.</param>
        /// <param name="b">The offset inside the power branch.</param>
        /// <param name="c">The linear branch scale.</param>
        /// <param name="d">The input at which the power branch starts.</param>
        /// <param name="e">The offset after the power branch.</param>
        /// <param name="power">The power operator for the upper branch.</param>
        /// <param name="signMask">The mask for the input sign bit, or zero to keep the original input.</param>
        public SRgbCurveOperator(float a, float b, float c, float d, float e, TensorPrimitives_.PowOperator power, float signMask)
        {
            this.a = a;
            this.b = b;
            this.c = c;
            this.d = d;
            this.e = e;
            this.power = power;
            this.signMask = signMask;
        }

        /// <inheritdoc/>
        public float Invoke(float value)
        {
            int bits = BitConverter.SingleToInt32Bits(value);
            int sign = bits & BitConverter.SingleToInt32Bits(this.signMask);
            float result = this.InvokeCore(BitConverter.Int32BitsToSingle(bits ^ sign));
            return BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(result) | sign);
        }

        /// <summary>
        /// Applies the curve to the prepared input.
        /// </summary>
        /// <param name="value">The input after the sign mask is applied.</param>
        /// <returns>The result before sign restoration.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float InvokeCore(float value)
        {
            if (!(value >= this.d))
            {
                return this.c * value;
            }

            // ICC curves bound the power base at zero. Values above one remain available to HDR destinations.
            float input = (this.a * value) + this.b;
            return this.power.Invoke(input < 0F ? 0F : input) + this.e;
        }

        /// <summary>
        /// Applies the selected formula with one independent color component in each lane.
        /// </summary>
        /// <param name="value">The input components, including values above one.</param>
        /// <returns>The converted components in the same lane order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            // Each lane holds one component. A zero mask keeps the original input.
            Vector128<float> sign = value & Vector128.Create(this.signMask);
            value ^= sign;
            Vector128<float> zero = Vector128.Create(0F);
            Vector128<float> active = Vector128.LessThanOrEqual(Vector128.Create(this.d), value);
            Vector128<float> input = Vector128.Add(Vector128.Multiply(Vector128.Create(this.a), value), Vector128.Create(this.b));

            // Inactive lanes use one as a safe power base. The final selection keeps their linear result.
            input = Vector128.ConditionalSelect(active, input, Vector128.Create(1F));
            input = Vector128.ConditionalSelect(Vector128.LessThan(input, zero), zero, input);

            // Use the classified power for exact square/cube arithmetic and the general finite kernel.
            // This matches the general ICC curve, including signed zero and nonfinite inputs.
            Vector128<float> power = this.power.Invoke(input);

            Vector128<float> nonlinear = Vector128.Add(power, Vector128.Create(this.e));
            Vector128<float> linear = Vector128.Multiply(Vector128.Create(this.c), value);
            return Vector128.ConditionalSelect(active, nonlinear, linear) | sign;
        }

        /// <summary>
        /// Applies the selected formula with one independent color component in each lane.
        /// </summary>
        /// <param name="value">The input components, including values above one.</param>
        /// <returns>The converted components in the same lane order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            // Each lane holds one component. A zero mask keeps the original input.
            Vector256<float> sign = value & Vector256.Create(this.signMask);
            value ^= sign;
            Vector256<float> zero = Vector256.Create(0F);
            Vector256<float> active = Vector256.LessThanOrEqual(Vector256.Create(this.d), value);
            Vector256<float> input = Vector256.Add(Vector256.Multiply(Vector256.Create(this.a), value), Vector256.Create(this.b));

            // Inactive lanes use one as a safe power base. The final selection keeps their linear result.
            input = Vector256.ConditionalSelect(active, input, Vector256.Create(1F));
            input = Vector256.ConditionalSelect(Vector256.LessThan(input, zero), zero, input);

            // Use the classified power for exact square/cube arithmetic and the general finite kernel.
            // This matches the general ICC curve, including signed zero and nonfinite inputs.
            Vector256<float> power = this.power.Invoke(input);

            Vector256<float> nonlinear = Vector256.Add(power, Vector256.Create(this.e));
            Vector256<float> linear = Vector256.Multiply(Vector256.Create(this.c), value);
            return Vector256.ConditionalSelect(active, nonlinear, linear) | sign;
        }

        /// <summary>
        /// Applies the selected formula with one independent color component in each lane.
        /// </summary>
        /// <param name="value">The input components, including values above one.</param>
        /// <returns>The converted components in the same lane order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            // Each lane holds one component. A zero mask keeps the original input.
            Vector512<float> sign = value & Vector512.Create(this.signMask);
            value ^= sign;
            Vector512<float> zero = Vector512.Create(0F);
            Vector512<float> active = Vector512.LessThanOrEqual(Vector512.Create(this.d), value);
            Vector512<float> input = Vector512.Add(Vector512.Multiply(Vector512.Create(this.a), value), Vector512.Create(this.b));

            // Inactive lanes use one as a safe power base. The final selection keeps their linear result.
            input = Vector512.ConditionalSelect(active, input, Vector512.Create(1F));
            input = Vector512.ConditionalSelect(Vector512.LessThan(input, zero), zero, input);

            // Use the classified power for exact square/cube arithmetic and the general finite kernel.
            // This matches the general ICC curve, including signed zero and nonfinite inputs.
            Vector512<float> power = this.power.Invoke(input);

            Vector512<float> nonlinear = Vector512.Add(power, Vector512.Create(this.e));
            Vector512<float> linear = Vector512.Multiply(Vector512.Create(this.c), value);
            return Vector512.ConditionalSelect(active, nonlinear, linear) | sign;
        }
    }
}
