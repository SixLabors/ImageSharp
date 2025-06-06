// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class TiffCmykVector256 : JpegColorConverterVector256
    {
        public TiffCmykVector256(int precision)
            : base(JpegColorSpace.TiffCmyk, precision)
        {
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInPlace(in ComponentValues values)
        {
            ref Vector256<float> c0Base =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector256<float> c1Base =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector256<float> c2Base =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component2));
            ref Vector256<float> c3Base =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component3));

            Vector256<float> scale = Vector256.Create(1 / this.MaximumValue);

            nuint n = values.Component0.Vector256Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                ref Vector256<float> c = ref Unsafe.Add(ref c0Base, i);
                ref Vector256<float> m = ref Unsafe.Add(ref c1Base, i);
                ref Vector256<float> y = ref Unsafe.Add(ref c2Base, i);
                Vector256<float> k = Unsafe.Add(ref c3Base, i);

                k = Vector256<float>.One - (k * scale);
                c = (Vector256<float>.One - (c * scale)) * k;
                m = (Vector256<float>.One - (m * scale)) * k;
                y = (Vector256<float>.One - (y * scale)) * k;
            }
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInPlaceWithIcc(Configuration configuration, in ComponentValues values, IccProfile profile)
            => CmykScalar.ConvertToRgbInPlaceWithIcc(configuration, profile, values, this.MaximumValue);

        /// <inheritdoc/>
        public override void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
            => ConvertFromRgb(in values, this.MaximumValue, rLane, gLane, bLane);

        public static void ConvertFromRgb(in ComponentValues values, float maxValue, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            ref Vector256<float> destC =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector256<float> destM =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector256<float> destY =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component2));
            ref Vector256<float> destK =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component3));

            ref Vector256<float> srcR =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(rLane));
            ref Vector256<float> srcG =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(gLane));
            ref Vector256<float> srcB =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(bLane));

            Vector256<float> scale = Vector256.Create(maxValue);

            nuint n = values.Component0.Vector256Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                Vector256<float> ctmp = scale - Unsafe.Add(ref srcR, i);
                Vector256<float> mtmp = scale - Unsafe.Add(ref srcG, i);
                Vector256<float> ytmp = scale - Unsafe.Add(ref srcB, i);
                Vector256<float> ktmp = Vector256.Min(ctmp, Vector256.Min(mtmp, ytmp));

                Vector256<float> kMask = ~Vector256.Equals(ktmp, scale);
                Vector256<float> divisor = Vector256<float>.One / (scale - ktmp);

                ctmp = ((ctmp - ktmp) * divisor) & kMask;
                mtmp = ((mtmp - ktmp) * divisor) & kMask;
                ytmp = ((ytmp - ktmp) * divisor) & kMask;

                Unsafe.Add(ref destC, i) = ctmp * scale;
                Unsafe.Add(ref destM, i) = mtmp * scale;
                Unsafe.Add(ref destY, i) = ytmp * scale;
                Unsafe.Add(ref destK, i) = ktmp;
            }
        }
    }
}
