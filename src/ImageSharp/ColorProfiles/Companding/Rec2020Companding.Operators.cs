// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.ColorProfiles.Companding;

/// <content>
/// Provides scalar and SIMD operators for extended-range companding.
/// </content>
public static partial class Rec2020Companding
{
    /// <summary>
    /// Decodes components with the Rec2020 transfer curve.
    /// </summary>
    internal readonly struct ExpandOperator : TensorPrimitives_.IUnaryOperator
    {
        /// <inheritdoc/>
        public float Invoke(float value) => new Rec709Companding.ExpandOperator((float)Alpha, (float)AlphaMinusOne, (float)InverseBeta).Invoke(value);

        /// <summary>
        /// Applies the curve to independent component lanes without clipping the final values.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
            => new Rec709Companding.ExpandOperator((float)Alpha, (float)AlphaMinusOne, (float)InverseBeta).Invoke(value);

        /// <summary>
        /// Applies the curve to independent component lanes without clipping the final values.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
            => new Rec709Companding.ExpandOperator((float)Alpha, (float)AlphaMinusOne, (float)InverseBeta).Invoke(value);

        /// <summary>
        /// Applies the curve to independent component lanes without clipping the final values.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
            => new Rec709Companding.ExpandOperator((float)Alpha, (float)AlphaMinusOne, (float)InverseBeta).Invoke(value);
    }

    /// <summary>
    /// Encodes components with the Rec2020 transfer curve.
    /// </summary>
    internal readonly struct CompressOperator : TensorPrimitives_.IUnaryOperator
    {
        /// <inheritdoc/>
        public float Invoke(float value) => new Rec709Companding.CompressOperator((float)Alpha, (float)AlphaMinusOne, (float)Beta).Invoke(value);

        /// <summary>
        /// Applies the curve to independent component lanes without clipping the final values.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
            => new Rec709Companding.CompressOperator((float)Alpha, (float)AlphaMinusOne, (float)Beta).Invoke(value);

        /// <summary>
        /// Applies the curve to independent component lanes without clipping the final values.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
            => new Rec709Companding.CompressOperator((float)Alpha, (float)AlphaMinusOne, (float)Beta).Invoke(value);

        /// <summary>
        /// Applies the curve to independent component lanes without clipping the final values.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
            => new Rec709Companding.CompressOperator((float)Alpha, (float)AlphaMinusOne, (float)Beta).Invoke(value);
    }
}
