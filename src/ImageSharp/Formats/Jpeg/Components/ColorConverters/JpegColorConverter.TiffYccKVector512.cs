// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class TiffYccKVector512 : JpegColorConverterVector512
    {
        public TiffYccKVector512(int precision)
            : base(JpegColorSpace.TiffYccK, precision)
        {
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInPlaceWithIcc(Configuration configuration, in ComponentValues values, IccProfile profile)
            => TiffYccKScalar.ConvertToRgbInPlaceWithIcc(configuration, profile, values, this.MaximumValue);

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

            Vector512<float> scale = Vector512.Create(1F / this.MaximumValue);
            Vector512<float> chromaOffset = Vector512.Create(this.HalfValue) * scale;
            Vector512<float> rCrMult = Vector512.Create(YCbCrScalar.RCrMult);
            Vector512<float> gCbMult = Vector512.Create(-YCbCrScalar.GCbMult);
            Vector512<float> gCrMult = Vector512.Create(-YCbCrScalar.GCrMult);
            Vector512<float> bCbMult = Vector512.Create(YCbCrScalar.BCbMult);

            nuint n = values.Component0.Vector512Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                ref Vector512<float> c0 = ref Unsafe.Add(ref c0Base, i);
                ref Vector512<float> c1 = ref Unsafe.Add(ref c1Base, i);
                ref Vector512<float> c2 = ref Unsafe.Add(ref c2Base, i);
                ref Vector512<float> c3 = ref Unsafe.Add(ref c3Base, i);

                Vector512<float> y = c0 * scale;
                Vector512<float> cb = (c1 * scale) - chromaOffset;
                Vector512<float> cr = (c2 * scale) - chromaOffset;
                Vector512<float> scaledK = Vector512<float>.One - (c3 * scale);

                // r = y + (1.402F * cr);
                // g = y - (0.344136F * cb) - (0.714136F * cr);
                // b = y + (1.772F * cb);
                Vector512<float> r = Vector512_.MultiplyAdd(y, cr, rCrMult) * scaledK;
                Vector512<float> g = Vector512_.MultiplyAdd(Vector512_.MultiplyAdd(y, cb, gCbMult), cr, gCrMult) * scaledK;
                Vector512<float> b = Vector512_.MultiplyAdd(y, cb, bCbMult) * scaledK;

                c0 = r;
                c1 = g;
                c2 = b;
            }
        }

        /// <inheritdoc/>
        protected override void ConvertFromRgbVectorized(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
            => ConvertFromRgbVectorized(in values, this.MaximumValue, this.HalfValue, rLane, gLane, bLane);

        /// <inheritdoc/>
        protected override void ConvertToRgbInPlaceScalarRemainder(in ComponentValues values)
             => TiffYccKScalar.ConvertToRgbInPlace(values, this.MaximumValue, this.HalfValue);

        /// <inheritdoc/>
        protected override void ConvertFromRgbScalarRemainder(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
            => TiffYccKScalar.ConvertFromRgb(values, this.HalfValue, this.MaximumValue, rLane, gLane, bLane);

        internal static void ConvertFromRgbVectorized(in ComponentValues values, float maxValue, float halfValue, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            ref Vector512<float> srcR =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(rLane));
            ref Vector512<float> srcG =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(gLane));
            ref Vector512<float> srcB =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(bLane));

            ref Vector512<float> destY =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector512<float> destCb =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector512<float> destCr =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(values.Component2));
            ref Vector512<float> destK =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(values.Component3));

            Vector512<float> maxSourceValue = Vector512.Create(255F);
            Vector512<float> maxSampleValue = Vector512.Create(maxValue);
            Vector512<float> chromaOffset = Vector512.Create(halfValue);

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
                Vector512<float> r = Unsafe.Add(ref srcR, i) / maxSourceValue;
                Vector512<float> g = Unsafe.Add(ref srcG, i) / maxSourceValue;
                Vector512<float> b = Unsafe.Add(ref srcB, i) / maxSourceValue;
                Vector512<float> ktmp = Vector512<float>.One - Vector512.Max(r, Vector512.Min(g, b));

                Vector512<float> kMask = ~Vector512.Equals(ktmp, Vector512<float>.One);
                Vector512<float> divisor = Vector512<float>.One - ktmp;

                r /= divisor;
                g /= divisor;
                b /= divisor;

                // y  =   0 + (0.299 * r) + (0.587 * g) + (0.114 * b)
                // cb = 128 - (0.168736 * r) - (0.331264 * g) + (0.5 * b)
                // cr = 128 + (0.5 * r) - (0.418688 * g) - (0.081312 * b)
                Vector512<float> y = Vector512_.MultiplyAdd(Vector512_.MultiplyAdd(f0114 * b, f0587, g), f0299, r);
                Vector512<float> cb = chromaOffset + Vector512_.MultiplyAdd(Vector512_.MultiplyAdd(f05 * b, fn0331264, g), fn0168736, r);
                Vector512<float> cr = chromaOffset + Vector512_.MultiplyAdd(Vector512_.MultiplyAdd(fn0081312F * b, fn0418688, g), f05, r);

                Unsafe.Add(ref destY, i) = y * maxSampleValue;
                Unsafe.Add(ref destCb, i) = chromaOffset + (cb * maxSampleValue);
                Unsafe.Add(ref destCr, i) = chromaOffset + (cr * maxSampleValue);
                Unsafe.Add(ref destK, i) = ktmp * maxSampleValue;
            }
        }
    }
}
