// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class CmykVector : JpegColorConverterVector
    {
        public CmykVector(int precision)
            : base(JpegColorSpace.Cmyk, precision)
        {
        }

        /// <inheritdoc/>
        protected override void ConvertToRgbInplaceVectorized(in ComponentValues values)
        {
            ref Vector<float> cBase =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector<float> mBase =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector<float> yBase =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component2));
            ref Vector<float> kBase =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component3));

            Vector<float> scale = new Vector<float>(1 / (this.MaximumValue * this.MaximumValue));

            nuint n = values.Component0.VectorCount<float>();
            for (nuint i = 0; i < n; i++)
            {
                ref Vector<float> c = ref Unsafe.Add(ref cBase, i);
                ref Vector<float> m = ref Unsafe.Add(ref mBase, i);
                ref Vector<float> y = ref Unsafe.Add(ref yBase, i);
                Vector<float> k = Unsafe.Add(ref kBase, i);

                k *= scale;
                c *= k;
                m *= k;
                y *= k;
            }
        }

        /// <inheritdoc/>
        protected override void ConvertToRgbInplaceScalarRemainder(in ComponentValues values)
             => CmykScalar.ConvertToRgbInplace(values, this.MaximumValue);

        /// <inheritdoc/>
        protected override void ConvertFromRgbVectorized(in ComponentValues values, Span<float> r, Span<float> g, Span<float> b)
            => ConvertFromRgbInplaceVectorized(in values, this.MaximumValue, r, g, b);

        /// <inheritdoc/>
        protected override void ConvertFromRgbScalarRemainder(in ComponentValues values, Span<float> r, Span<float> g, Span<float> b)
            => ConvertFromRgbInplaceRemainder(values, this.MaximumValue, r, g, b);

        public static void ConvertFromRgbInplaceVectorized(in ComponentValues values, float maxValue, Span<float> r, Span<float> g, Span<float> b)
        {
            ref Vector<float> destC =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector<float> destM =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector<float> destY =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component2));
            ref Vector<float> destK =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component3));

            ref Vector<float> srcR =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(r));
            ref Vector<float> srcG =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(g));
            ref Vector<float> srcB =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(b));

            // Used for the color conversion
            Vector<float> scale = new Vector<float>(maxValue);

            nuint n = values.Component0.VectorCount<float>();
            for (nuint i = 0; i < n; i++)
            {
                Vector<float> ctmp = scale - Unsafe.Add(ref srcR, i);
                Vector<float> mtmp = scale - Unsafe.Add(ref srcG, i);
                Vector<float> ytmp = scale - Unsafe.Add(ref srcB, i);
                Vector<float> ktmp = Vector.Min(ctmp, Vector.Min(mtmp, ytmp));

                Vector<int> kMask = Vector.Equals(ktmp, scale);
                ctmp = Vector.AndNot((ctmp - ktmp) / (scale - ktmp), kMask.As<int, float>());
                mtmp = Vector.AndNot((mtmp - ktmp) / (scale - ktmp), kMask.As<int, float>());
                ytmp = Vector.AndNot((ytmp - ktmp) / (scale - ktmp), kMask.As<int, float>());

                Unsafe.Add(ref destC, i) = scale - (ctmp * scale);
                Unsafe.Add(ref destM, i) = scale - (mtmp * scale);
                Unsafe.Add(ref destY, i) = scale - (ytmp * scale);
                Unsafe.Add(ref destK, i) = scale - ktmp;
            }
        }

        public static void ConvertFromRgbInplaceRemainder(in ComponentValues values, float maxValue, Span<float> r, Span<float> g, Span<float> b)
            => CmykScalar.ConvertFromRgb(values, maxValue, r, g, b);
    }
}
