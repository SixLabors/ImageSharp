// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class CmykVector512 : JpegColorConverterVector512
    {
        public CmykVector512(int precision)
            : base(JpegColorSpace.Cmyk, precision)
        {
        }

        /// <inheritdoc/>
        protected override void ConvertToRgbInPlaceVectorized(in ComponentValues values)
        {
            ref Vector512<float> c0Base =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector512<float> c1Base =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector512<float> c2Base =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(values.Component2));
            ref Vector512<float> c3Base =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(values.Component3));

            // Used for the color conversion
            Vector512<float> scale = Vector512.Create(1 / (this.MaximumValue * this.MaximumValue));

            nuint n = values.Component0.Vector512Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                ref Vector512<float> c = ref Unsafe.Add(ref c0Base, i);
                ref Vector512<float> m = ref Unsafe.Add(ref c1Base, i);
                ref Vector512<float> y = ref Unsafe.Add(ref c2Base, i);
                Vector512<float> k = Unsafe.Add(ref c3Base, i);

                k *= scale;
                c *= k;
                m *= k;
                y *= k;
            }
        }

        /// <inheritdoc/>
        protected override void ConvertFromRgbVectorized(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
            => ConvertFromRgbVectorized(in values, this.MaximumValue, rLane, gLane, bLane);

        /// <inheritdoc/>
        protected override void ConvertToRgbInPlaceScalarRemainder(in ComponentValues values)
             => CmykScalar.ConvertToRgbInPlace(values, this.MaximumValue);

        /// <inheritdoc/>
        protected override void ConvertFromRgbScalarRemainder(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
            => CmykScalar.ConvertFromRgb(values, this.MaximumValue, rLane, gLane, bLane);

        internal static void ConvertFromRgbVectorized(in ComponentValues values, float maxValue, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            ref Vector512<float> destC =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector512<float> destM =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector512<float> destY =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(values.Component2));
            ref Vector512<float> destK =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(values.Component3));

            ref Vector512<float> srcR =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(rLane));
            ref Vector512<float> srcG =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(gLane));
            ref Vector512<float> srcB =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(bLane));

            Vector512<float> scale = Vector512.Create(maxValue);

            nuint n = values.Component0.Vector512Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                Vector512<float> ctmp = scale - Unsafe.Add(ref srcR, i);
                Vector512<float> mtmp = scale - Unsafe.Add(ref srcG, i);
                Vector512<float> ytmp = scale - Unsafe.Add(ref srcB, i);
                Vector512<float> ktmp = Vector512.Min(ctmp, Vector512.Min(mtmp, ytmp));

                Vector512<float> kMask = Vector512.Equals(ktmp, scale);
                ctmp = Vector512.AndNot((ctmp - ktmp) / (scale - ktmp), kMask);
                mtmp = Vector512.AndNot((mtmp - ktmp) / (scale - ktmp), kMask);
                ytmp = Vector512.AndNot((ytmp - ktmp) / (scale - ktmp), kMask);

                Unsafe.Add(ref destC, i) = scale - (ctmp * scale);
                Unsafe.Add(ref destM, i) = scale - (mtmp * scale);
                Unsafe.Add(ref destY, i) = scale - (ytmp * scale);
                Unsafe.Add(ref destK, i) = scale - ktmp;
            }
        }
    }
}
