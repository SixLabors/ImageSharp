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

            var scale = new Vector<float>(1 / (this.MaximumValue * this.MaximumValue));

            nuint n = values.Component0.VectorCount<float>();
            for (nuint i = 0; i < n; i++)
            {
                ref Vector<float> c = ref Extensions.UnsafeAdd(ref cBase, i);
                ref Vector<float> m = ref Extensions.UnsafeAdd(ref mBase, i);
                ref Vector<float> y = ref Extensions.UnsafeAdd(ref yBase, i);
                Vector<float> k = Extensions.UnsafeAdd(ref kBase, i);

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
            var scale = new Vector<float>(maxValue);

            nuint n = values.Component0.VectorCount<float>();
            for (nuint i = 0; i < n; i++)
            {
                Vector<float> ctmp = scale - Extensions.UnsafeAdd(ref srcR, i);
                Vector<float> mtmp = scale - Extensions.UnsafeAdd(ref srcG, i);
                Vector<float> ytmp = scale - Extensions.UnsafeAdd(ref srcB, i);
                Vector<float> ktmp = Vector.Min(ctmp, Vector.Min(mtmp, ytmp));

                var kMask = Vector.Equals(ktmp, scale);
                ctmp = Vector.AndNot((ctmp - ktmp) / (scale - ktmp), kMask.As<int, float>());
                mtmp = Vector.AndNot((mtmp - ktmp) / (scale - ktmp), kMask.As<int, float>());
                ytmp = Vector.AndNot((ytmp - ktmp) / (scale - ktmp), kMask.As<int, float>());

                Extensions.UnsafeAdd(ref destC, i) = scale - (ctmp * scale);
                Extensions.UnsafeAdd(ref destM, i) = scale - (mtmp * scale);
                Extensions.UnsafeAdd(ref destY, i) = scale - (ytmp * scale);
                Extensions.UnsafeAdd(ref destK, i) = scale - ktmp;
            }
        }

        public static void ConvertFromRgbInplaceRemainder(in ComponentValues values, float maxValue, Span<float> r, Span<float> g, Span<float> b)
            => CmykScalar.ConvertFromRgb(values, maxValue, r, g, b);
    }
}
