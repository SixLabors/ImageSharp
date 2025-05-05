// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class CmykVector128 : JpegColorConverterVector128
    {
        public CmykVector128(int precision)
            : base(JpegColorSpace.Cmyk, precision)
        {
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInPlace(in ComponentValues values)
        {
            ref Vector128<float> c0Base =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector128<float> c1Base =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector128<float> c2Base =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component2));
            ref Vector128<float> c3Base =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component3));

            // Used for the color conversion
            Vector128<float> scale = Vector128.Create(1 / (this.MaximumValue * this.MaximumValue));

            nuint n = values.Component0.Vector128Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                ref Vector128<float> c = ref Unsafe.Add(ref c0Base, i);
                ref Vector128<float> m = ref Unsafe.Add(ref c1Base, i);
                ref Vector128<float> y = ref Unsafe.Add(ref c2Base, i);
                Vector128<float> k = Unsafe.Add(ref c3Base, i);

                k *= scale;
                c *= k;
                m *= k;
                y *= k;
            }
        }

        /// <inheritdoc/>
        public override void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
            => ConvertFromRgb(in values, this.MaximumValue, rLane, gLane, bLane);

        public static void ConvertFromRgb(in ComponentValues values, float maxValue, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            ref Vector128<float> destC =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector128<float> destM =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector128<float> destY =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component2));
            ref Vector128<float> destK =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component3));

            ref Vector128<float> srcR =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(rLane));
            ref Vector128<float> srcG =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(gLane));
            ref Vector128<float> srcB =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(bLane));

            Vector128<float> scale = Vector128.Create(maxValue);

            nuint n = values.Component0.Vector128Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                Vector128<float> ctmp = scale - Unsafe.Add(ref srcR, i);
                Vector128<float> mtmp = scale - Unsafe.Add(ref srcG, i);
                Vector128<float> ytmp = scale - Unsafe.Add(ref srcB, i);
                Vector128<float> ktmp = Vector128.Min(ctmp, Vector128.Min(mtmp, ytmp));

                Vector128<float> kMask = Vector128.Equals(ktmp, scale);
                ctmp = Vector128.AndNot((ctmp - ktmp) / (scale - ktmp), kMask);
                mtmp = Vector128.AndNot((mtmp - ktmp) / (scale - ktmp), kMask);
                ytmp = Vector128.AndNot((ytmp - ktmp) / (scale - ktmp), kMask);

                Unsafe.Add(ref destC, i) = scale - (ctmp * scale);
                Unsafe.Add(ref destM, i) = scale - (mtmp * scale);
                Unsafe.Add(ref destY, i) = scale - (ytmp * scale);
                Unsafe.Add(ref destK, i) = scale - ktmp;
            }
        }
    }
}
