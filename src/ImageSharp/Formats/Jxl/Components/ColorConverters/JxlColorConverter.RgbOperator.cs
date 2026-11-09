// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;

internal abstract partial class JxlColorConverterBase
{
    /// <summary>
    /// Retains normalized RGB components without color arithmetic.
    /// </summary>
    internal readonly struct RgbOperator : IJxlColorConverterOperator
    {
        /// <inheritdoc/>
        public static int ComponentCount => 3;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref float c0, ref float c1, ref float c2, ref float c3)
        {
            // Sample normalization already supplied RGB in the required component units.
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
        {
            // Sample normalization already supplied RGB in the required component units.
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
        {
            // Sample normalization already supplied RGB in the required component units.
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
        {
            // Sample normalization already supplied RGB in the required component units.
        }
    }

    /// <summary>
    /// Converts working RGB to one BT.709 luminance component before sample packing.
    /// </summary>
    internal readonly struct LuminanceOperator : IJxlColorConverterOperator
    {
        /// <inheritdoc/>
        public static int ComponentCount => 1;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref float c0, ref float c1, ref float c2, ref float c3)
            => c0 = ColorNumerics.GetBT709Luminance(new Vector4(c0, c1, c2, 0F));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
        {
            // Registers contain separate red, green and blue planes. Use the shared BT.709 weights;
            // keep the weighted products separate from the two float additions.
            Vector4 weights = ColorNumerics.Bt709;
            c0 = ((c0 * Vector128.Create(weights.X)) + (c1 * Vector128.Create(weights.Y))) + (c2 * Vector128.Create(weights.Z));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
        {
            // Registers contain separate red, green and blue planes. Use the shared BT.709 weights;
            // keep the weighted products separate from the two float additions.
            Vector4 weights = ColorNumerics.Bt709;
            c0 = ((c0 * Vector256.Create(weights.X)) + (c1 * Vector256.Create(weights.Y))) + (c2 * Vector256.Create(weights.Z));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
        {
            // Registers contain separate red, green and blue planes. Use the shared BT.709 weights;
            // keep the weighted products separate from the two float additions.
            Vector4 weights = ColorNumerics.Bt709;
            c0 = ((c0 * Vector512.Create(weights.X)) + (c1 * Vector512.Create(weights.Y))) + (c2 * Vector512.Create(weights.Z));
        }
    }
}
