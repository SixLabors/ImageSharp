// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class TiffCmykVector512 : JpegColorConverterVector512
    {
        public TiffCmykVector512(int precision)
            : base(JpegColorSpace.TiffCmyk, precision)
        {
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInPlaceWithIcc(Configuration configuration, in ComponentValues values, IccProfile profile)
            => TiffCmykScalar.ConvertToRgbInPlaceWithIcc(configuration, profile, values, this.MaximumValue);

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
            Vector512<float> scale = Vector512.Create(1 / this.MaximumValue);

            nuint n = values.Component0.Vector512Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                ref Vector512<float> c = ref Unsafe.Add(ref c0Base, i);
                ref Vector512<float> m = ref Unsafe.Add(ref c1Base, i);
                ref Vector512<float> y = ref Unsafe.Add(ref c2Base, i);
                Vector512<float> k = Unsafe.Add(ref c3Base, i);

                k = Vector512<float>.One - (k * scale);
                c = (Vector512<float>.One - (c * scale)) * k;
                m = (Vector512<float>.One - (m * scale)) * k;
                y = (Vector512<float>.One - (y * scale)) * k;
            }
        }

        /// <inheritdoc/>
        protected override void ConvertFromRgbVectorized(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
            => ConvertFromRgbVectorized(in values, this.MaximumValue, rLane, gLane, bLane);

        /// <inheritdoc/>
        protected override void ConvertToRgbInPlaceScalarRemainder(in ComponentValues values)
             => TiffCmykScalar.ConvertToRgbInPlace(values, this.MaximumValue);

        /// <inheritdoc/>
        protected override void ConvertFromRgbScalarRemainder(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
            => TiffCmykScalar.ConvertFromRgb(values, this.MaximumValue, rLane, gLane, bLane);

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

                Vector512<float> kMask = ~Vector512.Equals(ktmp, scale);
                Vector512<float> divisor = scale - ktmp;

                ctmp = ((ctmp - ktmp) / divisor) & kMask;
                mtmp = ((mtmp - ktmp) / divisor) & kMask;
                ytmp = ((ytmp - ktmp) / divisor) & kMask;

                Unsafe.Add(ref destC, i) = ctmp * scale;
                Unsafe.Add(ref destM, i) = mtmp * scale;
                Unsafe.Add(ref destY, i) = ytmp * scale;
                Unsafe.Add(ref destK, i) = ktmp;
            }
        }
    }
}
