// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;

internal abstract partial class JxlColorConverterBase
{
    /// <summary>
    /// Restores YCbCr-encoded CMY and combines it with the black component for shared ICC conversion.
    /// </summary>
    internal readonly struct YccKOperator : IJxlColorConverterOperator
    {
        /// <inheritdoc/>
        public static int ComponentCount => 4;

        /// <inheritdoc/>
        public static int PackedComponentCount => 4;

        /// <inheritdoc/>
        public static int ScratchComponentCount => 3;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToProfile(ref float c0, ref float c1, ref float c2, ref float c3)
            => YCbCrOperator.ConvertToProfile(ref c0, ref c1, ref c2, ref c3);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToProfile(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
            => YCbCrOperator.ConvertToProfile(ref c0, ref c1, ref c2, ref c3);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToProfile(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
            => YCbCrOperator.ConvertToProfile(ref c0, ref c1, ref c2, ref c3);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToProfile(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
            => YCbCrOperator.ConvertToProfile(ref c0, ref c1, ref c2, ref c3);

        /// <inheritdoc/>
        public static void ConvertToRgb(Span<float> packed, Span<Vector4> destination, Span<float> scratch, ColorProfileConverter converter)
        {
            // Packed device vectors alias destination and retain K in W. The separate three-component
            // scratch row receives YCbCr, then its decoded CMY values through the existing shared model.
            Span<Vector4> device = MemoryMarshal.Cast<float, Vector4>(packed);
            Span<YCbCr> yCbCr = MemoryMarshal.Cast<float, YCbCr>(scratch);
            YCbCr.FromScaledVector4(device, yCbCr);
            Span<Rgb> rgb = MemoryMarshal.Cast<float, Rgb>(scratch);
            YCbCr.ToProfileConnectionSpace(converter.Options, yCbCr, rgb);

            // Native processing applies YCbCr restoration before the CMYK profile transform. Replace
            // only the first three components, then reverse all four normalized ink values, including retained K.
            Numerics.WithXYZ(MemoryMarshal.Cast<Rgb, Vector3>(rgb), device);
            TensorPrimitives.Subtract(1F, packed, packed);
            converter.ConvertUsingIccProfile(device, destination);
        }
    }
}
