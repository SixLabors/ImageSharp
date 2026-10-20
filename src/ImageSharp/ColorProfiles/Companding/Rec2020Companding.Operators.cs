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
public static partial class Rec2020Companding
{
    /// <summary>
    /// Decodes components with the Rec2020 transfer curve.
    /// </summary>
    internal readonly struct ExpandOperator : TensorPrimitives_.IUnaryOperator
    {
        /// <inheritdoc/>
        public float Invoke(float value) => new Rec709Companding.ExpandOperator((float)Alpha, (float)AlphaMinusOne, (float)InverseBeta).Invoke(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value) => Invoke<Vector128<float>, Vector128Operations>(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value) => Invoke<Vector256<float>, Vector256Operations>(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value) => Invoke<Vector512<float>, Vector512Operations>(value);

        /// <summary>
        /// Applies the curve to independent component lanes without clipping the final values.
        /// </summary>
        /// <typeparam name="TVector">The register type.</typeparam>
        /// <typeparam name="TOperations">The register operations.</typeparam>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TVector Invoke<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
            => new Rec709Companding.ExpandOperator((float)Alpha, (float)AlphaMinusOne, (float)InverseBeta).Invoke<TVector, TOperations>(value);
    }

    /// <summary>
    /// Encodes components with the Rec2020 transfer curve.
    /// </summary>
    internal readonly struct CompressOperator : TensorPrimitives_.IUnaryOperator
    {
        /// <inheritdoc/>
        public float Invoke(float value) => new Rec709Companding.CompressOperator((float)Alpha, (float)AlphaMinusOne, (float)Beta).Invoke(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value) => Invoke<Vector128<float>, Vector128Operations>(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value) => Invoke<Vector256<float>, Vector256Operations>(value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value) => Invoke<Vector512<float>, Vector512Operations>(value);

        /// <summary>
        /// Applies the curve to independent component lanes without clipping the final values.
        /// </summary>
        /// <typeparam name="TVector">The register type.</typeparam>
        /// <typeparam name="TOperations">The register operations.</typeparam>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TVector Invoke<TVector, TOperations>(TVector value)
            where TVector : struct
            where TOperations : struct, ITransferVectorOperations<TVector>
            => new Rec709Companding.CompressOperator((float)Alpha, (float)AlphaMinusOne, (float)Beta).Invoke<TVector, TOperations>(value);
    }
}
