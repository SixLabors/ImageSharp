// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class GrayScaleVector : JpegColorConverterVector
    {
        public GrayScaleVector(int precision)
            : base(JpegColorSpace.Grayscale, precision)
        {
        }

        /// <inheritdoc/>
        protected override void ConvertToRgbInplaceVectorized(in ComponentValues values)
        {
            ref Vector<float> cBase =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component0));

            Vector<float> scale = new(1 / this.MaximumValue);

            nuint n = values.Component0.VectorCount<float>();
            for (nuint i = 0; i < n; i++)
            {
                ref Vector<float> c0 = ref Unsafe.Add(ref cBase, i);
                c0 *= scale;
            }
        }

        /// <inheritdoc/>
        protected override void ConvertToRgbInplaceScalarRemainder(in ComponentValues values)
            => GrayscaleScalar.ConvertToRgbInplace(values.Component0, this.MaximumValue);

        /// <inheritdoc/>
        protected override void ConvertFromRgbVectorized(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            ref Vector<float> destLuma =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component0));

            ref Vector<float> srcR =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(rLane));
            ref Vector<float> srcG =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(gLane));
            ref Vector<float> srcB =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(bLane));

            Vector<float> rMult = new(0.299f);
            Vector<float> gMult = new(0.587f);
            Vector<float> bMult = new(0.114f);

            nuint n = values.Component0.VectorCount<float>();
            for (nuint i = 0; i < n; i++)
            {
                Vector<float> r = Unsafe.Add(ref srcR, i);
                Vector<float> g = Unsafe.Add(ref srcR, i);
                Vector<float> b = Unsafe.Add(ref srcR, i);

                // luminocity = (0.299 * r) + (0.587 * g) + (0.114 * b)
                Unsafe.Add(ref destLuma, i) = (rMult * r) + (gMult * g) + (bMult * b);
            }
        }

        /// <inheritdoc/>
        protected override void ConvertFromRgbScalarRemainder(in ComponentValues values, Span<float> r, Span<float> g, Span<float> b)
            => GrayscaleScalar.ConvertCoreInplaceFromRgb(values, r, g, b);
    }
}
