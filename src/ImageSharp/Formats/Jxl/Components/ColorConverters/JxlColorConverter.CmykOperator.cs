// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;

internal abstract partial class JxlColorConverterBase
{
    /// <summary>
    /// Converts inverted JPEG XL CMYK components to normalized ICC ink amounts.
    /// </summary>
    internal readonly struct CmykOperator : IJxlColorConverterOperator
    {
        /// <inheritdoc/>
        public static int ComponentCount => 4;

        /// <inheritdoc/>
        public static int PackedComponentCount => 4;

        /// <inheritdoc/>
        public static int ScratchComponentCount => 0;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToProfile(ref float c0, ref float c1, ref float c2, ref float c3)
        {
            // Native jxl_cms.cc reverses all four components before its CMYK transform. Shared ICC
            // conversion uses normalized ink amounts, so use 1-x rather than LCMS's percentage scale.
            c0 = 1F - c0;
            c1 = 1F - c1;
            c2 = 1F - c2;
            c3 = 1F - c3;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToProfile(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
        {
            // Registers contain C, M, Y and K across four pixels. Every lane uses the scalar 1-x equation.
            c0 = Vector128<float>.One - c0;
            c1 = Vector128<float>.One - c1;
            c2 = Vector128<float>.One - c2;
            c3 = Vector128<float>.One - c3;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToProfile(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
        {
            // The eight-pixel path retains the same four planar components and subtraction order.
            c0 = Vector256<float>.One - c0;
            c1 = Vector256<float>.One - c1;
            c2 = Vector256<float>.One - c2;
            c3 = Vector256<float>.One - c3;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToProfile(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
        {
            // The sixteen-pixel path retains the same four planar components and subtraction order.
            c0 = Vector512<float>.One - c0;
            c1 = Vector512<float>.One - c1;
            c2 = Vector512<float>.One - c2;
            c3 = Vector512<float>.One - c3;
        }

        /// <inheritdoc/>
        public static void ConvertToRgb(Span<float> packed, Span<Vector4> destination, Span<float> scratch, ColorProfileConverter converter)
            => IccOperator.ConvertToRgb(packed, destination, scratch, converter);
    }
}
