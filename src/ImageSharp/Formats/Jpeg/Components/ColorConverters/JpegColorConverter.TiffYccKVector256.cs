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
    internal sealed class TiffYccKVector256 : JpegColorConverterVector256
    {
        public TiffYccKVector256(int precision)
            : base(JpegColorSpace.TiffYccK, precision)
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

            Vector256<float> scale = Vector256.Create(1F / this.MaximumValue);
            Vector256<float> chromaOffset = Vector256.Create(this.HalfValue) * scale;
            Vector256<float> rCrMult = Vector256.Create(YCbCrScalar.RCrMult);
            Vector256<float> gCbMult = Vector256.Create(-YCbCrScalar.GCbMult);
            Vector256<float> gCrMult = Vector256.Create(-YCbCrScalar.GCrMult);
            Vector256<float> bCbMult = Vector256.Create(YCbCrScalar.BCbMult);

            nuint n = values.Component0.Vector256Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                ref Vector256<float> c0 = ref Unsafe.Add(ref c0Base, i);
                ref Vector256<float> c1 = ref Unsafe.Add(ref c1Base, i);
                ref Vector256<float> c2 = ref Unsafe.Add(ref c2Base, i);
                ref Vector256<float> c3 = ref Unsafe.Add(ref c3Base, i);

                Vector256<float> y = c0 * scale;
                Vector256<float> cb = (c1 * scale) - chromaOffset;
                Vector256<float> cr = (c2 * scale) - chromaOffset;
                Vector256<float> scaledK = Vector256<float>.One - (c3 * scale);

                // r = y + (1.402F * cr);
                // g = y - (0.344136F * cb) - (0.714136F * cr);
                // b = y + (1.772F * cb);
                Vector256<float> r = Vector256_.MultiplyAdd(y, cr, rCrMult) * scaledK;
                Vector256<float> g = Vector256_.MultiplyAdd(Vector256_.MultiplyAdd(y, cb, gCbMult), cr, gCrMult) * scaledK;
                Vector256<float> b = Vector256_.MultiplyAdd(y, cb, bCbMult) * scaledK;

                c0 = r;
                c1 = g;
                c2 = b;
            }
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInPlaceWithIcc(Configuration configuration, in ComponentValues values, IccProfile profile)
            => TiffYccKScalar.ConvertToRgbInPlaceWithIcc(configuration, profile, values, this.MaximumValue);

        /// <inheritdoc/>
        public override void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            ref Vector256<float> srcR =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(rLane));
            ref Vector256<float> srcG =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(gLane));
            ref Vector256<float> srcB =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(bLane));

            ref Vector256<float> destY =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector256<float> destCb =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector256<float> destCr =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component2));
            ref Vector256<float> destK =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component3));

            Vector256<float> maxSourceValue = Vector256.Create(255F);
            Vector256<float> maxSampleValue = Vector256.Create(this.MaximumValue);
            Vector256<float> chromaOffset = Vector256.Create(this.HalfValue);

            Vector256<float> f0299 = Vector256.Create(0.299f);
            Vector256<float> f0587 = Vector256.Create(0.587f);
            Vector256<float> f0114 = Vector256.Create(0.114f);
            Vector256<float> fn0168736 = Vector256.Create(-0.168736f);
            Vector256<float> fn0331264 = Vector256.Create(-0.331264f);
            Vector256<float> fn0418688 = Vector256.Create(-0.418688f);
            Vector256<float> fn0081312F = Vector256.Create(-0.081312F);
            Vector256<float> f05 = Vector256.Create(0.5f);

            nuint n = values.Component0.Vector256Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                Vector256<float> r = Unsafe.Add(ref srcR, i) / maxSourceValue;
                Vector256<float> g = Unsafe.Add(ref srcG, i) / maxSourceValue;
                Vector256<float> b = Unsafe.Add(ref srcB, i) / maxSourceValue;
                Vector256<float> ktmp = Vector256<float>.One - Vector256.Max(r, Vector256.Min(g, b));

                Vector256<float> kMask = ~Vector256.Equals(ktmp, Vector256<float>.One);
                Vector256<float> divisor = Vector256<float>.One / (Vector256<float>.One - ktmp);

                r = (r * divisor) & kMask;
                g = (g * divisor) & kMask;
                b = (b * divisor) & kMask;

                // y  =   0 + (0.299 * r) + (0.587 * g) + (0.114 * b)
                // cb = 128 - (0.168736 * r) - (0.331264 * g) + (0.5 * b)
                // cr = 128 + (0.5 * r) - (0.418688 * g) - (0.081312 * b)
                Vector256<float> y = Vector256_.MultiplyAdd(Vector256_.MultiplyAdd(f0114 * b, f0587, g), f0299, r);
                Vector256<float> cb = chromaOffset + Vector256_.MultiplyAdd(Vector256_.MultiplyAdd(f05 * b, fn0331264, g), fn0168736, r);
                Vector256<float> cr = chromaOffset + Vector256_.MultiplyAdd(Vector256_.MultiplyAdd(fn0081312F * b, fn0418688, g), f05, r);

                Unsafe.Add(ref destY, i) = y * maxSampleValue;
                Unsafe.Add(ref destCb, i) = chromaOffset + (cb * maxSampleValue);
                Unsafe.Add(ref destCr, i) = chromaOffset + (cr * maxSampleValue);
                Unsafe.Add(ref destK, i) = ktmp * maxSampleValue;
            }
        }
    }
}
