// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;

internal abstract partial class JxlColorConverterBase
{
    /// <summary>
    /// Retains normalized RGB components that already use the output working space.
    /// </summary>
    internal readonly struct RgbOperator : IJxlColorConverterOperator
    {
        /// <inheritdoc/>
        public static int ComponentCount => 3;

        /// <inheritdoc/>
        public static int PackedComponentCount => 3;

        /// <inheritdoc/>
        public static int ScratchComponentCount => 3;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToProfile(ref float c0, ref float c1, ref float c2, ref float c3)
        {
            // Sample conversion already normalized RGB. Packing retains the first three components without arithmetic.
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToProfile(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
        {
            // Each register contains one component across four pixels. Identity conversion retains every lane.
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToProfile(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
        {
            // The eight-pixel path has the same component layout and identity operation.
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToProfile(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
        {
            // The sixteen-pixel path has the same component layout and identity operation.
        }

        /// <inheritdoc/>
        public static void ConvertToRgb(Span<float> packed, Span<Vector4> destination, Span<float> scratch, ColorProfileConverter converter)
        {
            // The factory selected equal source and target working spaces. Pack those RGB values directly;
            // expanding and compressing them again would add work and change floating-point rounding.
            Span<Rgb> rgb = MemoryMarshal.Cast<float, Rgb>(packed);
            Rgb.ToScaledVector4(rgb, destination);
        }
    }
}
