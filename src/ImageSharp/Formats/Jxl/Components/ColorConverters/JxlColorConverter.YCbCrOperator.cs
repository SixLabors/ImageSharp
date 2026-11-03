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
    /// Reorders JPEG XL chroma and luma for the shared YCbCr matrix and subsequent profile conversion.
    /// </summary>
    internal readonly struct YCbCrOperator : IJxlColorConverterOperator
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
            // Native reconstruction supplies [Cb,Y,Cr]. The shared color model consumes [Y,Cb,Cr].
            float cb = c0;
            c0 = c1;
            c1 = cb;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToProfile(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
        {
            // Registers contain one component across four pixels. Swap whole registers without changing lane order.
            Vector128<float> cb = c0;
            c0 = c1;
            c1 = cb;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToProfile(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
        {
            // The eight-pixel path applies the same component swap and retains every sample bit.
            Vector256<float> cb = c0;
            c0 = c1;
            c1 = cb;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertToProfile(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
        {
            // The sixteen-pixel path applies the same component swap and retains every sample bit.
            Vector512<float> cb = c0;
            c0 = c1;
            c1 = cb;
        }

        /// <inheritdoc/>
        public static void ConvertToRgb(Span<float> packed, Span<Vector4> destination, Span<float> scratch, ColorProfileConverter converter)
        {
            // The factory supplies BT.601 with the native luma offset. The shared matrix converts
            // the complete three-float block before overwriting it with RGB, so exact overlap is safe.
            Span<YCbCr> yCbCr = MemoryMarshal.Cast<float, YCbCr>(packed);
            Span<Rgb> rgb = MemoryMarshal.Cast<float, Rgb>(packed);
            YCbCr.ToProfileConnectionSpace(converter.Options, yCbCr, rgb);
            if (converter.Options.SourceIccProfile is not null)
            {
                // Device RGB remains in ICC component units. Alpha is separate from these three color channels.
                Rgb.ToScaledVector4(rgb, destination);
                converter.ConvertUsingIccProfile(destination, destination);
            }
            else if (converter.Options.SourceRgbWorkingSpace.Equals(converter.Options.TargetRgbWorkingSpace))
            {
                RgbOperator.ConvertToRgb(packed, destination, scratch, converter);
            }
            else
            {
                // Matrix conversion restores source RGB. A different compact working space still needs
                // its own shared transfer and primary conversion before the final output is packed.
                RgbWorkingSpaceOperator.ConvertToRgb(packed, destination, scratch, converter);
            }
        }
    }
}
