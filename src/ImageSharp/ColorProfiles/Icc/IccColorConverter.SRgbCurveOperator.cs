// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using static SixLabors.ImageSharp.ColorProfiles.Companding.CompandingVectorOperations;

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
        private readonly TensorPrimitives_.PowOperator power;

        /// <summary>
        /// Initializes a new instance of the <see cref="SRgbCurveOperator"/> struct.
        /// </summary>
        /// <param name="a">The scale inside the power branch.</param>
        /// <param name="b">The offset inside the power branch.</param>
        /// <param name="c">The linear branch scale.</param>
        /// <param name="d">The input at which the power branch starts.</param>
        /// <param name="e">The offset after the power branch.</param>
        /// <param name="power">The classified positive finite power selected by the curve factory.</param>
        public SRgbCurveOperator(float a, float b, float c, float d, float e, TensorPrimitives_.PowOperator power)
        {
            this.a = a;
            this.b = b;
            this.c = c;
            this.d = d;
            this.e = e;
            this.power = power;
        }

        /// <inheritdoc/>
        public static bool PreserveAlpha => false;

        /// <inheritdoc/>
        public float Invoke(float value)
        {
            if (!(value >= this.d))
            {
                return this.c * value;
            }

            // ICC curves bound the power base at zero. Values above one remain available to HDR destinations.
            float input = (this.a * value) + this.b;
            return this.power.Invoke(input < 0F ? 0F : input) + this.e;
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
        /// Applies the selected formula with one independent color component in each lane.
        /// </summary>
        /// <typeparam name="TVector">The SIMD register type.</typeparam>
        /// <typeparam name="TOperations">The shared operations for this SIMD width.</typeparam>
        /// <param name="value">The input components, including values above one.</param>
        /// <returns>The converted components in the same lane order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TVector Invoke<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
        {
            TVector zero = TOperations.Create(0F);
            TVector active = TOperations.LessThanOrEqual(TOperations.Create(this.d), value);
            TVector input = TOperations.Add(TOperations.Multiply(TOperations.Create(this.a), value), TOperations.Create(this.b));

            // Inactive lanes use one as a safe power base. The final selection keeps their linear result.
            input = TOperations.ConditionalSelect(active, input, TOperations.Create(1F));
            input = TOperations.ConditionalSelect(TOperations.LessThan(input, zero), zero, input);
            TVector power = TOperations.Pow(TOperations.Abs(input), this.power.Exponent);

            // The factory excluded zero, one and nonfinite exponents. Only odd integer powers retain negative zero.
            if (this.power.OddMask != 0)
            {
                power = TOperations.CopySign(power, input);
            }

            TVector nonlinear = TOperations.Add(power, TOperations.Create(this.e));
            TVector linear = TOperations.Multiply(TOperations.Create(this.c), value);
            return TOperations.ConditionalSelect(active, nonlinear, linear);
        }
    }
}
