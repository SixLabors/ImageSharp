// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class RgbVector512 : JpegColorConverterVector512
    {
        public RgbVector512(int precision)
            : base(JpegColorSpace.RGB, precision)
        {
        }

        /// <inheritdoc/>
        protected override void ConvertToRgbInPlaceVectorized(in ComponentValues values)
        {
            ref Vector512<float> rBase =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector512<float> gBase =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector512<float> bBase =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(values.Component2));

            // Used for the color conversion
            Vector512<float> scale = Vector512.Create(1 / this.MaximumValue);
            nuint n = values.Component0.Vector512Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                ref Vector512<float> r = ref Unsafe.Add(ref rBase, i);
                ref Vector512<float> g = ref Unsafe.Add(ref gBase, i);
                ref Vector512<float> b = ref Unsafe.Add(ref bBase, i);
                r *= scale;
                g *= scale;
                b *= scale;
            }
        }

        /// <inheritdoc/>
        protected override void ConvertFromRgbVectorized(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            rLane.CopyTo(values.Component0);
            gLane.CopyTo(values.Component1);
            bLane.CopyTo(values.Component2);
        }

        /// <inheritdoc/>
        protected override void ConvertToRgbInPlaceScalarRemainder(in ComponentValues values)
            => RgbScalar.ConvertToRgbInPlace(values, this.MaximumValue);

        /// <inheritdoc/>
        protected override void ConvertFromRgbScalarRemainder(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
            => RgbScalar.ConvertFromRgb(values, rLane, gLane, bLane);
    }
}
