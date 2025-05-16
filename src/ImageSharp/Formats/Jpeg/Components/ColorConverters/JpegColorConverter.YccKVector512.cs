// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using Vector512_ = SixLabors.ImageSharp.Common.Helpers.Vector512Utilities;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class YccKVector512 : JpegColorConverterVector512
    {
        public YccKVector512(int precision)
            : base(JpegColorSpace.Ycck, precision)
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
            ref Vector512<float> kBase =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(values.Component3));

            // Used for the color conversion
            Vector512<float> chromaOffset = Vector512.Create(-this.HalfValue);
            Vector512<float> scale = Vector512.Create(1 / (this.MaximumValue * this.MaximumValue));
            Vector512<float> max = Vector512.Create(this.MaximumValue);
            Vector512<float> rCrMult = Vector512.Create(YCbCrScalar.RCrMult);
            Vector512<float> gCbMult = Vector512.Create(-YCbCrScalar.GCbMult);
            Vector512<float> gCrMult = Vector512.Create(-YCbCrScalar.GCrMult);
            Vector512<float> bCbMult = Vector512.Create(YCbCrScalar.BCbMult);

            // Walking 8 elements at one step:
            nuint n = values.Component0.Vector512Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                // y = yVals[i];
                // cb = cbVals[i] - 128F;
                // cr = crVals[i] - 128F;
                // k = kVals[i] / 256F;
                ref Vector512<float> c0 = ref Unsafe.Add(ref c0Base, i);
                ref Vector512<float> c1 = ref Unsafe.Add(ref c1Base, i);
                ref Vector512<float> c2 = ref Unsafe.Add(ref c2Base, i);
                Vector512<float> y = c0;
                Vector512<float> cb = c1 + chromaOffset;
                Vector512<float> cr = c2 + chromaOffset;
                Vector512<float> scaledK = Unsafe.Add(ref kBase, i) * scale;

                // r = y + (1.402F * cr);
                // g = y - (0.344136F * cb) - (0.714136F * cr);
                // b = y + (1.772F * cb);
                Vector512<float> r = Vector512_.MultiplyAdd(y, cr, rCrMult);
                Vector512<float> g = Vector512_.MultiplyAdd(Vector512_.MultiplyAdd(y, cb, gCbMult), cr, gCrMult);
                Vector512<float> b = Vector512_.MultiplyAdd(y, cb, bCbMult);

                r = max - Vector512_.RoundToNearestInteger(r);
                g = max - Vector512_.RoundToNearestInteger(g);
                b = max - Vector512_.RoundToNearestInteger(b);

                r *= scaledK;
                g *= scaledK;
                b *= scaledK;

                c0 = r;
                c1 = g;
                c2 = b;
            }
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInPlaceWithIcc(Configuration configuration, in ComponentValues values, IccProfile profile)
            => YccKScalar.ConvertToRgbInPlaceWithIcc(configuration, profile, values, this.MaximumValue);

        /// <inheritdoc/>
        protected override void ConvertToRgbInPlaceScalarRemainder(in ComponentValues values)
            => YccKScalar.ConvertToRgpInPlace(values, this.MaximumValue, this.HalfValue);

        /// <inheritdoc/>
        protected override void ConvertFromRgbVectorized(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            // rgb -> cmyk
            CmykVector512.ConvertFromRgbVectorized(in values, this.MaximumValue, rLane, gLane, bLane);

            // cmyk -> ycck
            ref Vector512<float> destY =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector512<float> destCb =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector512<float> destCr =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(values.Component2));

            ref Vector512<float> srcR = ref destY;
            ref Vector512<float> srcG = ref destCb;
            ref Vector512<float> srcB = ref destCr;

            // Used for the color conversion
            Vector512<float> maxSampleValue = Vector512.Create(this.MaximumValue);

            Vector512<float> chromaOffset = Vector512.Create(this.HalfValue);

            Vector512<float> f0299 = Vector512.Create(0.299f);
            Vector512<float> f0587 = Vector512.Create(0.587f);
            Vector512<float> f0114 = Vector512.Create(0.114f);
            Vector512<float> fn0168736 = Vector512.Create(-0.168736f);
            Vector512<float> fn0331264 = Vector512.Create(-0.331264f);
            Vector512<float> fn0418688 = Vector512.Create(-0.418688f);
            Vector512<float> fn0081312F = Vector512.Create(-0.081312F);
            Vector512<float> f05 = Vector512.Create(0.5f);

            nuint n = values.Component0.Vector512Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                Vector512<float> r = maxSampleValue - Unsafe.Add(ref srcR, i);
                Vector512<float> g = maxSampleValue - Unsafe.Add(ref srcG, i);
                Vector512<float> b = maxSampleValue - Unsafe.Add(ref srcB, i);

                // y  =   0 + (0.299 * r) + (0.587 * g) + (0.114 * b)
                // cb = 128 - (0.168736 * r) - (0.331264 * g) + (0.5 * b)
                // cr = 128 + (0.5 * r) - (0.418688 * g) - (0.081312 * b)
                Vector512<float> y = Vector512_.MultiplyAdd(Vector512_.MultiplyAdd(f0114 * b, f0587, g), f0299, r);
                Vector512<float> cb = chromaOffset + Vector512_.MultiplyAdd(Vector512_.MultiplyAdd(f05 * b, fn0331264, g), fn0168736, r);
                Vector512<float> cr = chromaOffset + Vector512_.MultiplyAdd(Vector512_.MultiplyAdd(fn0081312F * b, fn0418688, g), f05, r);

                Unsafe.Add(ref destY, i) = y;
                Unsafe.Add(ref destCb, i) = cb;
                Unsafe.Add(ref destCr, i) = cr;
            }
        }

        /// <inheritdoc/>
        protected override void ConvertFromRgbScalarRemainder(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            // rgb -> cmyk
            CmykScalar.ConvertFromRgb(in values, this.MaximumValue, rLane, gLane, bLane);

            // cmyk -> ycck
            YccKScalar.ConvertFromRgb(in values, this.HalfValue, this.MaximumValue, rLane, gLane, bLane);
        }
    }
}
