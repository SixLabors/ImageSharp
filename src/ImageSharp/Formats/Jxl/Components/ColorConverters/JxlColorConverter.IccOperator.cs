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
    /// Retains normalized device components for the shared ICC transform without assigning an RGB model.
    /// </summary>
    internal readonly struct IccOperator : IJxlColorConverterOperator
    {
        /// <inheritdoc/>
        public static int ComponentCount => 3;

        /// <inheritdoc/>
        public static int PackedComponentCount => 4;

        /// <inheritdoc/>
        public static int ScratchComponentCount => 0;

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
            // Four slots hold one ICC device sample. The profile selects its active components;
            // the fourth slot is not image alpha. The caller restores alpha after color conversion.
            Span<Vector4> samples = MemoryMarshal.Cast<float, Vector4>(packed);
            converter.ConvertUsingIccProfile(samples, destination);
        }
    }
}
