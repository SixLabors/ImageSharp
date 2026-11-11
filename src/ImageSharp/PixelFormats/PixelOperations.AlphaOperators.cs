// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.PixelFormats;

// The alpha operators let each unpack and pack kernel apply the alpha step in registers, between the conversion and
// the store, so no conversion needs a second pass over the row. The kernels are generic over the operator, so the JIT
// compiles one loop for each alpha step, and the identity operator adds no instructions.
//
// The operators read scaled RGBA groups: lanes 4n to 4n + 3 of a vector are one pixel, with alpha in lane 4n + 3.
// A Vector4 is one 128-bit pixel. The scalar pixel conversions call the 128-bit operator with AsVector128, so the
// scalar and the bulk conversions run the same operator.

/// <content>
/// Contains the alpha operators of the pixel conversions.
/// </content>
internal static partial class PixelOperations
{
    /// <summary>
    /// Defines an alpha step that a bulk conversion applies to RGBA vectors in registers.
    /// </summary>
    public interface IAlphaOperator
    {
        /// <summary>
        /// Applies the alpha step to one RGBA pixel.
        /// </summary>
        /// <param name="source">One RGBA pixel.</param>
        /// <returns>The converted pixel.</returns>
        public static abstract Vector128<float> Apply(Vector128<float> source);

        /// <summary>
        /// Applies the alpha step to two RGBA pixels.
        /// </summary>
        /// <param name="source">Two RGBA pixels.</param>
        /// <returns>The converted pixels.</returns>
        public static abstract Vector256<float> Apply(Vector256<float> source);

        /// <summary>
        /// Applies the alpha step to four RGBA pixels.
        /// </summary>
        /// <param name="source">Four RGBA pixels.</param>
        /// <returns>The converted pixels.</returns>
        public static abstract Vector512<float> Apply(Vector512<float> source);
    }

    /// <summary>
    /// Keeps the components unchanged.
    /// </summary>
    public readonly struct NoAlphaOperator : IAlphaOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Apply(Vector128<float> source) => source;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> Apply(Vector256<float> source) => source;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<float> Apply(Vector512<float> source) => source;
    }

    /// <summary>
    /// Multiplies RGB by alpha for straight normalized RGBA pixels.
    /// </summary>
    /// <remarks>
    /// The input must be bounded for pixel conversion. Callers with input that can be out of range use
    /// <see cref="ClampToPixelRange{TAlpha}(Vector128{float})"/> or
    /// <see cref="ClampToPixelRange{TAlpha}(ReadOnlySpan{Vector4}, Span{Vector4})"/>. The product of bounded components is also bounded.
    /// </remarks>
    public readonly struct AssociateOperator : IAlphaOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Apply(Vector128<float> source) => Numerics.Premultiply(source);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> Apply(Vector256<float> source) => Numerics.Premultiply(source);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<float> Apply(Vector512<float> source) => Numerics.Premultiply(source);
    }

    /// <summary>
    /// Divides RGB by alpha for associated normalized RGBA pixels, then bounds the result for pixel conversion.
    /// </summary>
    /// <remarks>
    /// NaN and negative alpha become zero, so RGB is not divided. Positive infinity becomes <see cref="float.MaxValue"/>.
    /// Positive finite alpha, including alpha above one, is kept for the division.
    /// The result has finite RGB in [-65504,65504] and alpha in [0,1].
    /// </remarks>
    public readonly struct UnassociateOperator : IAlphaOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Apply(Vector128<float> source)
        {
            // Processing can leave alpha outside [0, 1], for example negative alpha from resampler ringing.
            // Make it a valid divisor first. Reducing alpha above one before the division would change the straight RGB.
            Vector128<float> alpha = Vector128_.ShuffleNative(source, 0b_11_11_11_11);
            alpha = ColorNumerics.ClampToFinite(alpha, Vector128<float>.Zero, Vector128.Create(float.MaxValue));

            return ClampToPixelRange(Numerics.UnPremultiply(source, alpha));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> Apply(Vector256<float> source)
        {
            // Each four-lane group uses its own alpha, with the same divisor rule as the 128-bit form.
            Vector256<float> alpha = Vector256_.ShuffleNative(source, 0b_11_11_11_11);
            alpha = ColorNumerics.ClampToFinite(alpha, Vector256<float>.Zero, Vector256.Create(float.MaxValue));

            return ClampToPixelRange(Numerics.UnPremultiply(source, alpha));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<float> Apply(Vector512<float> source)
        {
            // Each four-lane group uses its own alpha, with the same divisor rule as the 128-bit form.
            Vector512<float> alpha = Vector512_.ShuffleNative(source, 0b_11_11_11_11);
            alpha = ColorNumerics.ClampToFinite(alpha, Vector512<float>.Zero, Vector512.Create(float.MaxValue));

            return ClampToPixelRange(Numerics.UnPremultiply(source, alpha));
        }
    }
}
