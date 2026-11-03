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
    /// Converts normalized XYB through the shared transform configured for the codestream.
    /// </summary>
    internal readonly struct XybOperator : IJxlColorConverterOperator
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
            => RgbOperator.ConvertToProfile(ref c0, ref c1, ref c2, ref c3);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToProfile(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
            => RgbOperator.ConvertToProfile(ref c0, ref c1, ref c2, ref c3);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToProfile(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
            => RgbOperator.ConvertToProfile(ref c0, ref c1, ref c2, ref c3);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToProfile(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
            => RgbOperator.ConvertToProfile(ref c0, ref c1, ref c2, ref c3);

        /// <inheritdoc/>
        public static void ConvertToRgb(Span<float> packed, Span<Vector4> destination, Span<float> scratch, ColorProfileConverter converter)
        {
            // Both models occupy three floats. Shared conversion loads a complete source block before
            // storing that block's RGB, so exact overlap reuses the packed row without copying it.
            Span<Xyb> xyb = MemoryMarshal.Cast<float, Xyb>(packed);
            Span<Rgb> rgb = MemoryMarshal.Cast<float, Rgb>(packed);
            Xyb.ToProfileConnectionSpace(converter.Options, xyb, rgb);
            Rgb.ToScaledVector4(rgb, destination);
        }
    }
}
