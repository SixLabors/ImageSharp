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
    /// Converts normalized compact-profile RGB through the shared working-space converter.
    /// </summary>
    internal readonly struct RgbWorkingSpaceOperator : IJxlColorConverterOperator
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
            Span<Rgb> rgb = MemoryMarshal.Cast<float, Rgb>(packed);

            // Packed RGB occupies the traversal's three-component scratch row. Destination has separate
            // storage and temporarily holds XYZ; the shared conversion restores packed RGB before final packing.
            Span<CieXyz> pcsScratch = MemoryMarshal.Cast<Vector4, CieXyz>(destination)[..rgb.Length];
            converter.Convert<Rgb, Rgb>(rgb, rgb, pcsScratch);
            Rgb.ToScaledVector4(rgb, destination);
        }
    }
}
