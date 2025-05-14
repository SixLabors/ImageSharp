// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class RgbVector256 : JpegColorConverterVector256
    {
        public RgbVector256(int precision)
            : base(JpegColorSpace.RGB, precision)
        {
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInPlace(in ComponentValues values)
        {
            ref Vector256<float> rBase =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector256<float> gBase =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector256<float> bBase =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component2));

            // Used for the color conversion
            Vector256<float> scale = Vector256.Create(1 / this.MaximumValue);
            nuint n = values.Component0.Vector256Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                ref Vector256<float> r = ref Unsafe.Add(ref rBase, i);
                ref Vector256<float> g = ref Unsafe.Add(ref gBase, i);
                ref Vector256<float> b = ref Unsafe.Add(ref bBase, i);
                r *= scale;
                g *= scale;
                b *= scale;
            }
        }

        /// <inheritdoc/>
        public override void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            rLane.CopyTo(values.Component0);
            gLane.CopyTo(values.Component1);
            bLane.CopyTo(values.Component2);
        }
    }
}
