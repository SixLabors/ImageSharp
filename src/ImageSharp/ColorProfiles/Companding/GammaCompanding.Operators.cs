// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.ColorProfiles.Companding;

/// <content>
/// Provides scalar and SIMD operators for extended-range companding.
/// </content>
public static partial class GammaCompanding
{
    /// <summary>
    /// Decodes components with the Gamma transfer curve.
    /// </summary>
    internal readonly struct ExpandOperator : TensorPrimitives_.IUnaryOperator
    {
        // Extend gamma symmetrically around zero. Fractional powers use the magnitude; the input sign is restored afterward.
        private readonly TensorPrimitives_.PowOperator power;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpandOperator"/> struct.
        /// </summary>
        /// <param name="gamma">The gamma of the nonlinear signal.</param>
        public ExpandOperator(double gamma) => this.power = new((float)gamma);

        /// <inheritdoc/>
        public float Invoke(float value) => MathF.CopySign(this.power.Invoke(MathF.Abs(value)), value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
            => Vector128.CopySign(this.power.Invoke(Vector128.Abs(value)), value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
            => Vector256.CopySign(this.power.Invoke(Vector256.Abs(value)), value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
            => Vector512.CopySign(this.power.Invoke(Vector512.Abs(value)), value);
    }

    /// <summary>
    /// Encodes components with the Gamma transfer curve.
    /// </summary>
    internal readonly struct CompressOperator : TensorPrimitives_.IUnaryOperator
    {
        // Extend gamma symmetrically around zero. Fractional powers use the magnitude; the input sign is restored afterward.
        private readonly TensorPrimitives_.PowOperator power;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompressOperator"/> struct.
        /// </summary>
        /// <param name="gamma">The gamma of the nonlinear signal.</param>
        public CompressOperator(double gamma) => this.power = new((float)(1D / gamma));

        /// <inheritdoc/>
        public float Invoke(float value) => MathF.CopySign(this.power.Invoke(MathF.Abs(value)), value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
            => Vector128.CopySign(this.power.Invoke(Vector128.Abs(value)), value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
            => Vector256.CopySign(this.power.Invoke(Vector256.Abs(value)), value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
            => Vector512.CopySign(this.power.Invoke(Vector512.Abs(value)), value);
    }
}
