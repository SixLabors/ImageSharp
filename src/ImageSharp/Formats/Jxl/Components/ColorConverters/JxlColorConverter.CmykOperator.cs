// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;

internal abstract partial class JxlColorConverterBase
{
    /// <summary>
    /// Converts native inverted CMYK to conventional ICC device components.
    /// </summary>
    internal readonly struct CmykOperator : IJxlColorConverterOperator
    {
        /// <inheritdoc/>
        public static int ComponentCount => 4;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref float c0, ref float c1, ref float c2, ref float c3)
        {
            // ICC consumes ink amounts, whereas the codestream stores remaining light in all four channels.
            c0 = 1F - c0;
            c1 = 1F - c1;
            c2 = 1F - c2;
            c3 = 1F - c3;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
        {
            // ICC consumes ink amounts, whereas the codestream stores remaining light in all four channels.
            c0 = Vector128<float>.One - c0;
            c1 = Vector128<float>.One - c1;
            c2 = Vector128<float>.One - c2;
            c3 = Vector128<float>.One - c3;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
        {
            // ICC consumes ink amounts, whereas the codestream stores remaining light in all four channels.
            c0 = Vector256<float>.One - c0;
            c1 = Vector256<float>.One - c1;
            c2 = Vector256<float>.One - c2;
            c3 = Vector256<float>.One - c3;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
        {
            // ICC consumes ink amounts, whereas the codestream stores remaining light in all four channels.
            c0 = Vector512<float>.One - c0;
            c1 = Vector512<float>.One - c1;
            c2 = Vector512<float>.One - c2;
            c3 = Vector512<float>.One - c3;
        }
    }
}
