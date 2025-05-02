// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class RgbVector : JpegColorConverterVector
    {
        public RgbVector(int precision)
            : base(JpegColorSpace.RGB, precision)
        {
        }

        /// <inheritdoc/>
        protected override void ConvertToRgbInPlaceVectorized(in ComponentValues values)
        {
            ref Vector<float> rBase =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector<float> gBase =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector<float> bBase =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component2));

            var scale = new Vector<float>(1 / this.MaximumValue);

            nuint n = values.Component0.VectorCount<float>();
            for (nuint i = 0; i < n; i++)
            {
                ref Vector<float> r = ref Unsafe.Add(ref rBase, i);
                ref Vector<float> g = ref Unsafe.Add(ref gBase, i);
                ref Vector<float> b = ref Unsafe.Add(ref bBase, i);
                r *= scale;
                g *= scale;
                b *= scale;
            }
        }

        /// <inheritdoc/>
        protected override void ConvertToRgbInPlaceScalarRemainder(in ComponentValues values)
            => RgbScalar.ConvertToRgbInplace(values, this.MaximumValue);

        /// <inheritdoc/>
        protected override void ConvertFromRgbVectorized(in ComponentValues values, Span<float> r, Span<float> g, Span<float> b)
        {
            r.CopyTo(values.Component0);
            g.CopyTo(values.Component1);
            b.CopyTo(values.Component2);
        }

        /// <inheritdoc/>
        protected override void ConvertFromRgbScalarRemainder(in ComponentValues values, Span<float> r, Span<float> g, Span<float> b)
            => RgbScalar.ConvertFromRgb(values, r, g, b);
    }
}
