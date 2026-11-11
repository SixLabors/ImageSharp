// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.PixelFormats;

// A bulk conversion to associated vectors used to unpack a row to straight vectors, then premultiply the row in a
// second pass. A bulk conversion from associated vectors used to unpremultiply the row, then pack it in a second pass.
// The alpha operators let each unpack and pack kernel apply the alpha step in registers, between the conversion and
// the store. The kernels are generic over the operator, so the JIT compiles one loop for each alpha step, and the
// identity operator adds no instructions.
//
// The operators read RGBA groups: lanes 4n to 4n + 3 of a vector are one pixel, with alpha in lane 4n + 3.
// The arithmetic is the arithmetic of the span methods, so the fused and the two-pass forms give the same bits.

/// <content>
/// Contains the alpha operators of the bulk pixel conversions.
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
    /// Multiplies RGB by alpha and keeps alpha, as <see cref="Numerics.Premultiply(Span{System.Numerics.Vector4})"/> does.
    /// </summary>
    public readonly struct PremultiplyOperator : IAlphaOperator
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
    /// Restores straight RGB before packing, as <see cref="UnPremultiplyForPacking(Span{System.Numerics.Vector4})"/> does.
    /// </summary>
    public readonly struct UnPremultiplyOperator : IAlphaOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Apply(Vector128<float> source) => UnPremultiplyForPacking(source);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> Apply(Vector256<float> source) => UnPremultiplyForPacking(source);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<float> Apply(Vector512<float> source) => UnPremultiplyForPacking(source);
    }
}
